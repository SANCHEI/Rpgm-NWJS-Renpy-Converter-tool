"""Deterministic RenPy RPA fixtures (all official versions + ALT + RPI)."""
import os
import pickle
import struct
import sys
import zlib


def chunk(name, data):
    return name + struct.pack("<Q", len(data)) + data


def file_payload(name, seg_recs):
    name_u16 = name.encode("utf-16le")
    orig = sum(r[2] for r in seg_recs)
    stored = sum(r[3] for r in seg_recs)
    info = struct.pack("<IQQH", 0, orig, stored, len(name_u16) // 2) + name_u16
    segm = b"".join(struct.pack("<IQQQ", *r) for r in seg_recs)
    return chunk(b"File", chunk(b"info", info) + chunk(b"segm", segm))


def build(path, version, entries, protocol, key=None):
    prepared = []
    for name, chunks in entries:
        psegs = []
        for data, prefix in chunks:
            psegs.append((data, prefix))
        prepared.append((name, psegs))
    if version == "RPA-1.0":
        shifted = {}
        for name, psegs in prepared:
            shifted[name] = [(0, len(d)) for d, _ in psegs]
        compressed = zlib.compress(pickle.dumps(shifted, protocol=protocol))
        data_base = len(compressed)
        shifted = {}
        for name, psegs in prepared:
            shifted[name] = [(data_base, len(psegs[0][0]))]
            data_base += len(psegs[0][0])
        compressed = zlib.compress(pickle.dumps(shifted, protocol=protocol))
        with open(path, "wb") as f:
            f.write(compressed)
            for _, psegs in prepared:
                for data, _ in psegs:
                    f.write(data)
        return
    # header length is deterministic per version/key presence
    if version == "ALT-1.0":
        hlen = len("ALT-1.0 %08X %016X\n" % (0, 0))
    elif key is not None:
        hlen = len("%s %016X %08X\n" % (version, 0, key))
    else:
        hlen = len("%s %016X\n" % (version, 0))
    cursor = hlen
    shifted = {}
    blob_parts = []
    for name, psegs in prepared:
        items = []
        for data, prefix in psegs:
            if prefix is None:
                items.append((cursor, len(data)))
            else:
                items.append((cursor, len(data), prefix))
            blob_parts.append(data)
            cursor += len(data)
        shifted[name] = items
    if key is not None:
        xored = {}
        for name, items in shifted.items():
            lst = []
            for it in items:
                if len(it) == 2:
                    lst.append((it[0] ^ key, it[1] ^ key))
                else:
                    lst.append((it[0] ^ key, it[1] ^ key, it[2]))
            xored[name] = lst
        shifted = xored
    pickled = pickle.dumps(shifted, protocol=protocol)
    compressed = zlib.compress(pickled)
    index_offset = cursor
    if version == "ALT-1.0":
        header = ("ALT-1.0 %08X %016X\n" % (key ^ 0xDABE8DF0, index_offset)).encode("ascii")
    elif key is not None:
        header = ("%s %016X %08X\n" % (version, index_offset, key)).encode("ascii")
    else:
        header = ("%s %016X\n" % (version, index_offset)).encode("ascii")
    assert len(header) == hlen, (len(header), hlen)
    with open(path, "wb") as f:
        f.write(header)
        for b in blob_parts:
            f.write(b)
        f.write(compressed)


def tlg5_stub():
    out = bytearray(b"TLG5.0\x00raw\x1a")
    out += bytes([4])
    out += struct.pack("<III", 6, 1, 1)
    out += struct.pack("<I", 1)
    for marker, data in [(0, b"\x08\x0a\x14\x1e\x00\x00"), (1, bytes([1, 2, 3, 4, 5, 6])),
                         (0, b"\x00\x05\x05\x05\x05\x05\x05"), (1, bytes([7, 8, 9, 10, 11, 12]))]:
        out += bytes([marker]) + struct.pack("<I", len(data)) + data
    return bytes(out)


root = sys.argv[1]
os.makedirs(root, exist_ok=True)
script = b"define e = Character('Eileen')\n"
png = bytes([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]) + b"PNGDATA1234"
build(os.path.join(root, "arch20.rpa"), "RPA-2.0",
      [("script/script.rpy", [(script, None)]), ("images/bg.png", [(png, None)])], 2)
build(os.path.join(root, "arch30.rpa"), "RPA-3.0",
      [("script.rpy", [(script, None)]), ("split.dat", [(b"AA", None), (b"BB", b"PRE")])], 4, key=0xDEADBEEF)
build(os.path.join(root, "arch32.rpa"), "RPA-3.2",
      [("a.rpy", [(b"xxx", None)])], 0, key=0x00C0FFEE)
build(os.path.join(root, "arch40.rpa"), "RPA-4.0",
      [("-test/file.rpy", [(script, None)]), ("sfx/sound.ogg", [(b"OggSdata", None)])], 2, key=0x12345678)
build(os.path.join(root, "archalt.rpa"), "ALT-1.0",
      [("alt.rpy", [(b"altdata", None)])], 2, key=0x0BADF00D)
build(os.path.join(root, "arch10.rpi"), "RPA-1.0",
      [("old.rpy", [(b"olddata", None)])], 2)
print("rpa fixtures ready")
