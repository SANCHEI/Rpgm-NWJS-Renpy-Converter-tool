"""Deterministic Godot PCK fixtures: v1, v2 (+imported texture), embedded-exe, v3 encrypted.

The v3 pair is encrypted through source/scripts/pyuepak_aes_windows.py (Windows
bcrypt binding, the same primitive family the production Python extractor and
the C# AesManaged code paths implement), so no test-only crypto lives here.
"""
import hashlib
import os
import struct
import sys
import zlib

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "source", "scripts"))
from pyuepak_aes_windows import aes_cfb_encrypt

MAGIC = b"GDPC"


def _cfb_encrypt(key, iv, data):
    padded = data + b"\x00" * ((-len(data)) % 16)
    return aes_cfb_encrypt(key, iv, padded)


def _enc_block(key, iv, plain):
    enc = _cfb_encrypt(key, iv, plain)
    return hashlib.md5(plain).digest() + struct.pack("<Q", len(plain)) + iv + enc


def chunk(tag, data):
    return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)


def png_2x2():
    ihdr = struct.pack(">IIBBBBB", 2, 2, 8, 6, 0, 0, 0)
    raw = b"\x00" + bytes([200, 10, 20, 30] * 2) + b"\x00" + bytes([40, 50, 60, 70] * 2)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", zlib.compress(raw)) + chunk(b"IEND", b"")


def build_v1(path, files):
    header_len = 4 + 4 + 12 + 64 + 4
    index_len = sum(4 + len(n.encode("utf-8")) + 8 + 8 + 16 for n, _ in files)
    cursor = header_len + index_len
    index = b""
    blobs = []
    for name, data in files:
        raw = name.encode("utf-8")
        index += struct.pack("<I", len(raw)) + raw + struct.pack("<QQ", cursor, len(data)) + b"\x00" * 16
        blobs.append(data)
        cursor += len(data)
    with open(path, "wb") as out:
        out.write(MAGIC + struct.pack("<I", 1) + struct.pack("<III", 0, 0, 0) + b"\x00" * 64)
        out.write(struct.pack("<I", len(files)))
        out.write(index)
        for blob in blobs:
            out.write(blob)


def build_v2(path, files):
    header_len = 4 + 4 + 12 + 4 + 8 + 64 + 4
    index_len = sum(4 + len(n.encode("utf-8")) + 8 + 8 + 16 + 4 for n, _ in files)
    cursor = header_len + index_len
    index = b""
    blobs = []
    for name, data in files:
        raw = name.encode("utf-8")
        index += struct.pack("<I", len(raw)) + raw + struct.pack("<QQ", cursor, len(data))
        index += b"\x00" * 16 + struct.pack("<I", 0)
        blobs.append(data)
        cursor += len(data)
    with open(path, "wb") as out:
        out.write(MAGIC + struct.pack("<I", 2) + struct.pack("<III", 0, 0, 0))
        out.write(struct.pack("<I", 0) + struct.pack("<Q", 0) + b"\x00" * 64)
        out.write(struct.pack("<I", len(files)))
        out.write(index)
        for blob in blobs:
            out.write(blob)


def entry(path, offset, size, encrypted):
    raw = path.encode("utf-8")
    return (struct.pack("<I", len(raw)) + raw + struct.pack("<QQ", offset, size)
            + b"\x00" * 16 + struct.pack("<I", 1 if encrypted else 0))


def build_v3_file_encrypted(path, key, iv):
    secret = b"top-secret-godot-payload-0123456789"
    open_data = b"open-data"
    with open(path, "wb") as out:
        out.write(MAGIC + struct.pack("<I", 3) + struct.pack("<III", 0, 0, 0))
        out.write(struct.pack("<I", 0) + struct.pack("<Q", 0) + struct.pack("<Q", 40))
        blob0 = 40 + 4 + len(entry("res://secret.bin", 0, 0, True)) + len(entry("res://open.txt", 0, 0, False))
        block = _enc_block(key, iv, secret)
        out.write(struct.pack("<I", 2))
        out.write(entry("res://secret.bin", blob0, len(secret), True))
        out.write(entry("res://open.txt", blob0 + len(block), len(open_data), False))
        out.write(block)
        out.write(open_data)


def build_v3_dir_encrypted(path, key, iv):
    payload = b"innerdata"
    entries = b""
    entry_len = len(entry("res://inner.txt", 0, 0, False))
    block_len = 16 + 8 + 16 + ((entry_len + 15) // 16) * 16
    data_at = 40 + 4 + block_len
    entries = entry("res://inner.txt", data_at, len(payload), False)
    with open(path, "wb") as out:
        out.write(MAGIC + struct.pack("<I", 3) + struct.pack("<III", 0, 0, 0))
        out.write(struct.pack("<I", 1) + struct.pack("<Q", 0) + struct.pack("<Q", 40))
        out.write(struct.pack("<I", 1))
        out.write(_enc_block(key, iv, entries))
        out.write(payload)


root = sys.argv[1]
os.makedirs(root, exist_ok=True)
png = png_2x2()
build_v1(os.path.join(root, "game_v1.pck"), [
    ("res://scripts/main.gd", b"extends Node\n"),
    ("res://icon.png", png),
])
build_v2(os.path.join(root, "game_v2.pck"), [
    ("res://scenes/level.tscn", b"[gd_scene]\n"),
    ("res://.godot/imported/icon.png-abc123abc123abc123abc123abc123ab.ctex", png),
    ("res://audio/theme.ogg", b"OggS" + bytes(200)),
])
inner = os.path.join(root, "inner.pck")
build_v2(inner, [("res://data.bin", b"0123456789ABCDEF")])
with open(inner, "rb") as src:
    payload = src.read()
os.remove(inner)
with open(os.path.join(root, "game_embedded.exe"), "wb") as out:
    out.write(b"MZFAKEHEADER" + bytes(100))
    out.write(payload)
    out.write(struct.pack("<Q", len(payload)))
    out.write(MAGIC)
key = bytes((i * 7 + 3) % 256 for i in range(32))
iv = bytes(range(1, 17))
build_v3_file_encrypted(os.path.join(root, "game_v3enc.pck"), key, iv)
build_v3_dir_encrypted(os.path.join(root, "game_v3encdir.pck"), key, iv)
with open(os.path.join(root, "key.hex.txt"), "w") as out:
    out.write(key.hex())
print("godot fixtures ready")
