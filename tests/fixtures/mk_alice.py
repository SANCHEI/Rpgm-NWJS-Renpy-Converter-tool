"""Deterministic AliceSoft SACT fixtures for native extractor regression tests."""
import os
import struct
import sys

RNG = bytearray(range(256))


def le24(value):
    return bytes([value & 0xFF, (value >> 8) & 0xFF, (value >> 16) & 0xFF])


def png_2x2():
    def chunk(t, d):
        return struct.pack(">I", len(d)) + t + d + struct.pack(">I", 0x12345678)
    ihdr = struct.pack(">IIBBBBB", 2, 2, 8, 6, 0, 0, 0)
    raw = b"\x00" + bytes([10, 20, 30, 40] * 2) + b"\x00" + bytes([50, 60, 70, 80] * 2)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", raw) + chunk(b"IEND", b"")


def jpeg_small():
    return b"\xff\xd8\xff\xe0" + bytes(range(64)) + b"\xff\xd9"


def ogg_small(marker):
    return b"OggS" + bytes([0, 2]) + bytes([marker] * 24)


def entry(payload):
    header = struct.pack("<I", 32) + bytes([11, 22, 33, 44, 55, 66, 77, 88])
    header += bytes(range(12, 32))
    return header + payload


def pad256(blob):
    remainder = len(blob) % 256
    return blob if remainder == 0 else blob + b"\x00" * (256 - remainder)


def build(entries):
    block = 256
    blocks = list(range(1, len(entries) + 1))
    blocks.append(len(entries) + 1)
    table = b"".join(le24(b) for b in blocks)
    if len(table) >= block:
        raise ValueError("table does not fit in the first block")
    body = b"".join(pad256(e) for e in entries)
    return table.ljust(block, b"\x00") + body


root = sys.argv[1]
os.makedirs(root, exist_ok=True)

index_entry = struct.pack("<HH", 1, 1) + struct.pack("<HH", 1, 2) + struct.pack("<HH", 1, 3)

album = build([
    index_entry,
    entry(ogg_small(0xA1)),
    entry(ogg_small(0xA2)),
    entry(b"\x8f\x06\xdd\x01" + bytes(range(96))),
    entry(ogg_small(0xA3)),
])
with open(os.path.join(root, "Game0WA.ald"), "wb") as f:
    f.write(album)

scenes = build([
    index_entry,
    entry(b"\x8f\x06\xdd\x01" + bytes(range(64))),
    entry(png_2x2()),
    entry(jpeg_small()),
    entry(b"\x8f\x06\xdd\x01" + bytes(range(64))),
])
with open(os.path.join(root, "Game0GM.ald"), "wb") as f:
    f.write(scenes)

with open(os.path.join(root, "logo.jpg"), "wb") as f:
    f.write(jpeg_small())
print("alice-sact fixtures ready")