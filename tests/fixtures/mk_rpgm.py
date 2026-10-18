"""Deterministic RPG Maker fixtures for RpgmCrypt regression tests."""
import os
import struct
import sys
import zlib

KEY = bytes([0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF,
             0x10, 0x32, 0x54, 0x76, 0x98, 0xBA, 0xDC, 0xFE])


def chunk(t, d):
    return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)


def png_2x2():
    ihdr = struct.pack(">IIBBBBB", 2, 2, 8, 6, 0, 0, 0)
    raw = b"\x00" + bytes([10, 20, 30, 40] * 2) + b"\x00" + bytes([50, 60, 70, 80] * 2)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", zlib.compress(raw)) + chunk(b"IEND", b"")


def enc(payload):
    body = bytearray(payload)
    for i in range(min(16, len(body))):
        body[i] ^= KEY[i]
    return b"RPGMV" + bytes(11) + bytes(body)


root = sys.argv[1]
os.makedirs(root, exist_ok=True)
png = png_2x2()
with open(os.path.join(root, "enc_png.rpgmvp"), "wb") as f:
    f.write(enc(png))
with open(os.path.join(root, "enc_png_expected.png"), "wb") as f:
    f.write(png)
with open(os.path.join(root, "plain_copy.png_"), "wb") as f:
    f.write(png)
with open(os.path.join(root, "key.hex.txt"), "w") as f:
    f.write(KEY.hex())
print("rpgmvp fixtures ready")
