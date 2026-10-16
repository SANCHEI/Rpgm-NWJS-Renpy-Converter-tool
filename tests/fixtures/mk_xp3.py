"""Deterministic XP3 fixtures for native extractor regression tests."""
import os
import struct
import sys
import zlib

MAGIC = b"XP3\r\n \n\x1a\x8bg\x01"


def chunk(name, data):
    return name + struct.pack("<Q", len(data)) + data


def file_payload(name, seg_recs):
    name_u16 = name.encode("utf-16le")
    orig = sum(r[2] for r in seg_recs)
    stored = sum(r[3] for r in seg_recs)
    info = struct.pack("<IQQH", 0, orig, stored, len(name_u16) // 2) + name_u16
    segm = b"".join(struct.pack("<IQQQ", *r) for r in seg_recs)
    return chunk(b"File", chunk(b"info", info) + chunk(b"segm", segm))


def build(path, files):
    prepared = []
    for name, segs in files:
        psegs = []
        for flags, data, comp in segs:
            stored = zlib.compress(data) if comp else data
            psegs.append((flags | (1 if comp else 0), data, stored))
        prepared.append((name, psegs))
    dummy = b"".join(
        file_payload(name, [(f, 0, len(d), len(s)) for f, d, s in psegs])
        for name, psegs in prepared)
    block = b"\x00" + struct.pack("<Q", len(dummy)) + dummy
    data_base = 11 + 8 + len(block)
    payload = b""
    blobs = []
    cursor = data_base
    for name, psegs in prepared:
        recs = []
        for flags, data, stored in psegs:
            recs.append((flags, cursor, len(data), len(stored)))
            blobs.append(stored)
            cursor += len(stored)
        payload += file_payload(name, recs)
    assert len(payload) == len(dummy)
    block = b"\x00" + struct.pack("<Q", len(payload)) + payload
    with open(path, "wb") as f:
        f.write(MAGIC)
        f.write(struct.pack("<Q", 11 + 8))
        f.write(block)
        for b in blobs:
            f.write(b)


def tlg5_mixed_6x1():
    p0 = b"\x08\x0a\x14\x1e\x00\x00"
    p1 = (1, bytes([1, 2, 3, 4, 5, 6]))
    p2 = b"\x00\x05\x05\x05\x05\x05\x05"
    p3 = (1, bytes([7, 8, 9, 10, 11, 12]))
    out = bytearray(b"TLG5.0\x00raw\x1a")
    out += bytes([4])
    out += struct.pack("<III", 6, 1, 1)
    out += struct.pack("<I", 1)
    for marker, data in [(0, p0), p1, (0, p2), p3]:
        out += bytes([marker]) + struct.pack("<I", len(data)) + data
    return bytes(out)


root = sys.argv[1]
os.makedirs(root, exist_ok=True)
hello = b"Hello XP3 native!"
big = (b"EFGH" * 4096)
build(os.path.join(root, "a.xp3"), [
    ("docs\\hello.txt", [(0, hello, False)]),
    ("data/split.bin", [(0, b"ABCD", False), (0, big, True)]),
])
build(os.path.join(root, "b.xp3"), [
    ("images/picture.tlg", [(0, tlg5_mixed_6x1(), False)]),
])
# encrypted entry archive (must fail cleanly, like the script)
name_u16 = "secret.txt".encode("utf-16le")
info = struct.pack("<IQQH", 0x80000000, 3, 3, len(name_u16) // 2) + name_u16
segm = struct.pack("<IQQQ", 0, 100, 3, 3)
payload = chunk(b"File", chunk(b"info", info) + chunk(b"segm", segm))
block = b"\x00" + struct.pack("<Q", len(payload)) + payload
with open(os.path.join(root, "c.xp3"), "wb") as f:
    f.write(MAGIC)
    f.write(struct.pack("<Q", 19))
    f.write(block)
    f.write(b"XYZ")
# padding is deterministic by construction (no random bytes are decoded)
print("xp3 fixtures ready")
