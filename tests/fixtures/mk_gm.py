"""Deterministic GameMaker fixtures for native extractor regression tests."""
import os
import random
import struct
import sys
import zlib

RNG = random.Random(1234)


def chunk(t, d):
    return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)


def png_2x2():
    ihdr = struct.pack(">IIBBBBB", 2, 2, 8, 6, 0, 0, 0)
    raw = b"\x00" + bytes([10, 20, 30, 40] * 2) + b"\x00" + bytes([50, 60, 70, 80] * 2)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", zlib.compress(raw)) + chunk(b"IEND", b"")


def fioq(width, height, payload):
    return b"fioq" + struct.pack("<HHI", width, height, len(payload)) + payload


# texture 1: 4x1 color-mask, index-hit, diff8, run8(1)
T1 = bytes([0xFF, 10, 20, 30, 255, 0x3F, 0xA0, 0x41])
# NOTE 2-bit diff value 2 means -2: pixels end with (8,20,30,255) twice.
T1PX = bytes([10, 20, 30, 255, 10, 20, 30, 255, 8, 20, 30, 255, 8, 20, 30, 255])
# texture 2: 36x1 color-mask, diff16, adiff, run16(33)
T2 = bytes([0xFF, 100, 50, 25, 200, 0xCA, 0xD2, 0xE0, 0x84, 0x21, 0x60, 0x01])
T2PX = (bytes([100, 50, 25, 200, 110, 47, 27, 200, 111, 48, 28, 201])
        + bytes([111, 48, 28, 201]) * 33)

root = sys.argv[1]
os.makedirs(root, exist_ok=True)
blob = b"GAMEDATA" + RNG.randbytes(100)
blob += png_2x2()
blob += RNG.randbytes(500)
blob += fioq(4, 1, T1)
blob += RNG.randbytes(300)
blob += fioq(36, 1, T2)
blob += RNG.randbytes(700)
blob += b"fioq" + struct.pack("<HHI", 2, 2, 100) + b"\x00" * 5
blob += RNG.randbytes(200)
blob += b"TRAILER"
with open(os.path.join(root, "data.win"), "wb") as f:
    f.write(blob)
with open(os.path.join(root, "screenshot.png"), "wb") as f:
    f.write(png_2x2())
with open(os.path.join(root, "notes.txt"), "wb") as f:
    f.write(b"not an image")
with open(os.path.join(root, "t1_expected.bin"), "wb") as f:
    f.write(T1PX)
with open(os.path.join(root, "t2_expected.bin"), "wb") as f:
    f.write(bytes([100, 50, 25, 200, 110, 47, 27, 200, 111, 48, 28, 201]) + bytes([111, 48, 28, 201]) * 33)
print("gm fixtures ready")
