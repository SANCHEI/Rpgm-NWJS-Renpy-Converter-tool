import bz2
import brotli
import gzip
import hashlib
import importlib.util
import os
import pathlib
import shutil
import struct
import subprocess
import sys
import tempfile
import zlib

from PIL import Image
from pyuepak import PakFile
from pyuepak.aes_windows import aes_cfb_encrypt


ROOT = pathlib.Path(__file__).resolve().parents[1]
MAGIC_GODOT = b"GDPC"
MAGIC_XP3 = b"XP3\r\n \n\x1a\x8bg\x01"


def run_script(script_name, game_path, output_path, optional_key=""):
    environment = os.environ.copy()
    environment["GAME_PATH"] = str(game_path)
    environment["OUTPUT_PATH"] = str(output_path)
    environment["OPTIONAL_KEY"] = optional_key
    process = subprocess.run(
        [sys.executable, str(ROOT / "source" / "scripts" / script_name)],
        env=environment,
        capture_output=True,
        text=True,
    )
    if process.returncode != 0:
        raise AssertionError("{} failed:\n{}\n{}".format(script_name, process.stdout, process.stderr))
    return process.stdout


def run_script_result(script_name, game_path, output_path, optional_key=""):
    environment = os.environ.copy()
    environment["GAME_PATH"] = str(game_path)
    environment["OUTPUT_PATH"] = str(output_path)
    environment["OPTIONAL_KEY"] = optional_key
    return subprocess.run(
        [sys.executable, str(ROOT / "source" / "scripts" / script_name)],
        env=environment,
        capture_output=True,
        text=True,
    )


def assert_extracted(output_path, expected):
    matches = [path for path in output_path.rglob("*") if path.is_file() and path.read_bytes() == expected]
    if not matches:
        raise AssertionError("Extracted content was not found under {}".format(output_path))


def build_godot_v1(path, data):
    filename = b"res://sample/hello.txt"
    header_size = 4 + 4 * 4 + 16 * 4 + 4 + 4 + len(filename) + 8 + 8 + 16
    with path.open("wb") as stream:
        stream.write(MAGIC_GODOT)
        stream.write(struct.pack("<IIII", 1, 3, 5, 0))
        stream.write(b"\0" * (16 * 4))
        stream.write(struct.pack("<I", 1))
        stream.write(struct.pack("<I", len(filename)))
        stream.write(filename)
        stream.write(struct.pack("<QQ", header_size, len(data)))
        stream.write(hashlib.md5(data).digest())
        stream.write(data)


def build_godot_v2(path, data):
    filename = b"res://sample/hello.txt"
    header_size = 4 + 4 * 4 + 4 + 8 + 16 * 4 + 4 + 4 + len(filename) + 8 + 8 + 16 + 4
    with path.open("wb") as stream:
        stream.write(MAGIC_GODOT)
        stream.write(struct.pack("<IIII", 2, 4, 0, 0))
        stream.write(struct.pack("<IQ", 0, 0))
        stream.write(b"\0" * (16 * 4))
        stream.write(struct.pack("<I", 1))
        stream.write(struct.pack("<I", len(filename)))
        stream.write(filename)
        stream.write(struct.pack("<QQ", header_size, len(data)))
        stream.write(hashlib.md5(data).digest())
        stream.write(struct.pack("<I", 0))
        stream.write(data)


def build_godot_v3(path, data):
    filename = b"res://sample/hello.txt"
    directory_offset = 4 + 4 * 4 + 4 + 8 + 8
    data_offset = directory_offset + 4 + 4 + len(filename) + 8 + 8 + 16 + 4
    with path.open("wb") as stream:
        stream.write(MAGIC_GODOT)
        stream.write(struct.pack("<IIII", 3, 4, 5, 0))
        stream.write(struct.pack("<IQQ", 0, 0, directory_offset))
        stream.write(struct.pack("<I", 1))
        stream.write(struct.pack("<I", len(filename)))
        stream.write(filename)
        stream.write(struct.pack("<QQ", data_offset, len(data)))
        stream.write(hashlib.md5(data).digest())
        stream.write(struct.pack("<I", 0))
        stream.write(data)


def godot_encrypted_block(data, key):
    iv = bytes(range(16))
    padded = data + b"\0" * ((-len(data)) & 15)
    return hashlib.md5(data).digest() + struct.pack("<Q", len(data)) + iv + aes_cfb_encrypt(key, iv, padded)


def build_godot_encrypted_v2(path, data):
    key = bytes.fromhex("00112233445566778899aabbccddeeff" * 2)
    filename = b"res://sample/encrypted.txt"
    directory_size = 4 + len(filename) + 8 + 8 + 16 + 4
    prefix_size = 4 + 4 * 4 + 4 + 8 + 16 * 4 + 4
    encrypted_directory_size = 16 + 8 + 16 + ((directory_size + 15) & ~15)
    file_offset = prefix_size + encrypted_directory_size
    directory = (
        struct.pack("<I", len(filename))
        + filename
        + struct.pack("<QQ", file_offset, len(data))
        + hashlib.md5(data).digest()
        + struct.pack("<I", 1)
    )
    with path.open("wb") as stream:
        stream.write(MAGIC_GODOT)
        stream.write(struct.pack("<IIII", 2, 4, 0, 0))
        stream.write(struct.pack("<IQ", 1, 0))
        stream.write(b"\0" * (16 * 4))
        stream.write(struct.pack("<I", 1))
        stream.write(godot_encrypted_block(directory, key))
        stream.write(godot_encrypted_block(data, key))
    (path.parent / "keys.txt").write_text(key.hex(), encoding="ascii")


def xp3_chunk(name, value):
    return name + struct.pack("<Q", len(value)) + value


def build_xp3(path, data):
    filename = "sample/hello.txt"
    data_offset = len(MAGIC_XP3) + 8
    info = struct.pack("<IQQH", 0, len(data), len(data), len(filename)) + filename.encode("utf-16le")
    segments = struct.pack("<IQQQ", 0, data_offset, len(data), len(data))
    file_chunk = xp3_chunk(b"info", info) + xp3_chunk(b"segm", segments)
    index = xp3_chunk(b"File", file_chunk)
    compressed_index = zlib.compress(index)
    with path.open("wb") as stream:
        stream.write(MAGIC_XP3)
        stream.write(struct.pack("<Q", data_offset + len(data)))
        stream.write(data)
        stream.write(b"\x01")
        stream.write(struct.pack("<QQ", len(compressed_index), len(index)))
        stream.write(compressed_index)


def build_tlg5(color):
    red, green, blue, alpha = color
    planes = (blue - green & 0xFF, green, red - green & 0xFF, alpha)
    return TLG5 + bytes((4,)) + struct.pack("<III", 1, 1, 1) + struct.pack("<I", 0) + b"".join(
        b"\x01" + struct.pack("<I", 1) + bytes((value,)) for value in planes
    )


TLG5 = b"TLG5.0\0raw\x1a"


def build_gamemaker(path, _):
    fioq = b"fioq" + struct.pack("<HHI", 1, 1, 5) + bytes((0xFF, 10, 20, 30, 255))
    bz2qoi = b"2zoq" + b"\0" * 4 + bz2.compress(fioq)
    path.write_bytes(fioq + bz2qoi)


def load_script(script_name):
    path = ROOT / "source" / "scripts" / script_name
    spec = importlib.util.spec_from_file_location(path.stem, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def build_spite(game):
    module = load_script("extract_spite.py")
    key_material = bytes(range(48))
    external_route = "hscenes/sample.mp4"
    external_plain = b"\0\0\0\x20ftypisom\0\0\x02\0isomiso2avc1mp41"
    archive_route = "images/sample.png"
    archive_plain = b"\x89PNG\r\n\x1a\n\0\0\0\rIHDRsynthetic-spite"
    javascript = 'const video="{}";const image="{}";'.format(external_route, archive_route).encode("utf-8")
    compressed = brotli.compress(javascript)

    executable = bytearray(0x2000)
    pe_offset = 0x80
    optional_offset = pe_offset + 24
    section_offset = optional_offset + 0xF0
    image_base = 0x140000000
    section_va = 0x1000
    section_raw = 0x400

    def virtual_address(raw_offset):
        return image_base + section_va + raw_offset - section_raw

    executable[:2] = b"MZ"
    struct.pack_into("<I", executable, 0x3C, pe_offset)
    executable[pe_offset:pe_offset + 4] = b"PE\0\0"
    struct.pack_into("<H", executable, pe_offset + 4, 0x8664)
    struct.pack_into("<H", executable, pe_offset + 6, 1)
    struct.pack_into("<H", executable, pe_offset + 20, 0xF0)
    struct.pack_into("<H", executable, optional_offset, 0x20B)
    struct.pack_into("<Q", executable, optional_offset + 24, image_base)
    executable[section_offset:section_offset + 8] = b".rdata\0\0"
    struct.pack_into("<IIII", executable, section_offset + 8, 0x1C00, section_va, 0x1C00, section_raw)

    key_offset = 0x500
    value_offset = 0x600
    marker_offset = 0x900
    key = b"/assets/index-test.js"
    executable[key_offset:key_offset + len(key)] = key
    executable[value_offset:value_offset + len(compressed)] = compressed
    executable[marker_offset - len(key_material):marker_offset] = key_material
    executable[marker_offset:marker_offset + len(module.SPITE_MARKER)] = module.SPITE_MARKER
    struct.pack_into(
        "<QQQQ",
        executable,
        0x480,
        virtual_address(key_offset),
        len(key),
        virtual_address(value_offset),
        len(compressed),
    )
    (game / "spite.exe").write_bytes(executable)

    data = game / "data"
    data.mkdir()
    external_identifier = module.route_identifier(external_route)
    external_encrypted = module.derive_cipher(key_material, external_route).encrypt(external_plain)
    (data / (external_identifier + ".dat")).write_bytes(external_encrypted)

    archive_identifier = module.route_identifier(archive_route)
    archive_encrypted = module.derive_cipher(key_material, archive_route).encrypt(archive_plain)
    with (data / "_archive.dat").open("wb") as stream:
        stream.write(b"SPAK")
        stream.write(struct.pack("<II", 1, 1))
        stream.write(archive_identifier.encode("ascii"))
        stream.write(struct.pack("<III", 0, len(archive_encrypted), 0))
        stream.write(archive_encrypted)
    return external_route, external_plain, archive_route, archive_plain


def assert_unreal_compressions():
    from lz4.block import compress as lz4_compress
    from pyuepak.entry import Entry
    from pyuepak.file_io import Reader
    from pyuepak.utils import COMPRESSION
    from pyuepak.version import PakVersion
    from zstandard import ZstdCompressor

    raw = b"unreal-compression-ok-" * 64
    cases = (
        (COMPRESSION.Gzip, gzip.compress(raw)),
        (COMPRESSION.LZ4, lz4_compress(raw, store_size=False)),
        (COMPRESSION.Zstd, ZstdCompressor().compress(raw)),
    )
    for compression, compressed in cases:
        header = (
            struct.pack("<QQQI", 0, len(compressed), len(raw), compression.value - 1)
            + hashlib.sha1(raw).digest()
            + struct.pack("<I", 0)
            + b"\0"
            + struct.pack("<I", len(raw))
        )
        entry = Entry()
        entry.offset = 0
        entry.size = len(raw)
        entry.compressed_size = len(compressed)
        entry.compression = compression
        assert entry.read_file(Reader(header + compressed), PakVersion.V3, bytes(32)) == raw


def assert_unreal_partial_extraction(temp):
    module = load_script("extract_unreal.py")
    available = b"unreal-partial-ok"

    class FakeEntry:
        hash = None

    class FakePak:
        _index = type("Index", (), {"entrys": {
            "sample/available.txt": FakeEntry(),
            "sample/protected.uasset": FakeEntry(),
        }})()

        def list_files(self):
            return sorted(self._index.entrys)

        def read_file(self, filename):
            if filename.endswith(".uasset"):
                raise RuntimeError("no local oo2core decoder")
            return available

    archive = temp / "unreal-partial" / "sample.pak"
    output = temp / "unreal-partial" / "output"
    archive.parent.mkdir(parents=True)
    archive.write_bytes(b"")
    original_open_archive = module.open_archive
    module.open_archive = lambda _archive, _keys: FakePak()
    try:
        assert module.extract_archive(str(archive), str(output), []) == (1, len(available), 0, 1)
    finally:
        module.open_archive = original_open_archive
    assert_extracted(output, available)
    manifest = output / "archives" / "sample" / "Unreal-skipped-files.txt"
    assert "sample/protected.uasset\tno local oo2core decoder" in manifest.read_text(encoding="utf-8")


def build_unreal(path, data):
    pak = PakFile()
    pak.add_file("sample/hello.txt", data)
    pak.write(str(path))


def main():
    temp = pathlib.Path(tempfile.mkdtemp(prefix="GameAssetTool-EngineTests-"))
    try:
        cases = [
            ("godot-v1", "extract_godot.py", build_godot_v1, ".pck", b"godot-v1-ok"),
            ("godot-v2", "extract_godot.py", build_godot_v2, ".pck", b"godot-v2-ok"),
            ("godot-v3", "extract_godot.py", build_godot_v3, ".pck", b"godot-v3-ok"),
            ("godot-encrypted-v2", "extract_godot.py", build_godot_encrypted_v2, ".pck", b"godot-encrypted-ok"),
            ("xp3", "extract_xp3.py", build_xp3, ".xp3", b"xp3-ok"),
            ("unreal", "extract_unreal.py", build_unreal, ".pak", b"unreal-ok"),
        ]
        for label, script_name, builder, extension, content in cases:
            game = temp / label / "game"
            output = temp / label / "output"
            game.mkdir(parents=True)
            builder(game / ("sample" + extension), content)
            log = run_script(script_name, game, output)
            assert "RESULT:1:" in log
            assert_extracted(output, content)
            print("{}=ok".format(label))

        godot_no_key_game = temp / "godot-encrypted-no-key" / "game"
        godot_no_key_output = temp / "godot-encrypted-no-key" / "output"
        godot_no_key_game.mkdir(parents=True)
        build_godot_encrypted_v2(godot_no_key_game / "sample.pck", b"godot-encrypted-no-key")
        (godot_no_key_game / "keys.txt").unlink()
        no_key = run_script_result("extract_godot.py", godot_no_key_game, godot_no_key_output)
        assert no_key.returncode == 0
        assert "Godot key candidate(s): 0" in no_key.stdout
        assert "no key candidates were found" in no_key.stdout

        godot_wrong_key_game = temp / "godot-encrypted-wrong-key" / "game"
        godot_wrong_key_output = temp / "godot-encrypted-wrong-key" / "output"
        godot_wrong_key_game.mkdir(parents=True)
        build_godot_encrypted_v2(godot_wrong_key_game / "sample.pck", b"godot-encrypted-wrong-key")
        (godot_wrong_key_game / "keys.txt").unlink()
        wrong_key = run_script_result(
            "extract_godot.py",
            godot_wrong_key_game,
            godot_wrong_key_output,
            "ff" * 32,
        )
        assert wrong_key.returncode == 0
        assert "Godot key source(s): manual field" in wrong_key.stdout
        assert "tried 1 key candidate(s)" in wrong_key.stdout
        print("godot-encrypted-diagnostics=ok")

        tlg_game = temp / "xp3-tlg5" / "game"
        tlg_output = temp / "xp3-tlg5" / "output"
        tlg_game.mkdir(parents=True)
        tlg = build_tlg5((120, 80, 40, 255))
        build_xp3(tlg_game / "sample.xp3", tlg)
        assert "RESULT:2:" in run_script("extract_xp3.py", tlg_game, tlg_output)
        preview = next(tlg_output.rglob("hello.png"))
        with Image.open(preview) as image:
            assert image.convert("RGBA").getpixel((0, 0)) == (120, 80, 40, 255)
        print("xp3-tlg5=ok")

        gamemaker_game = temp / "gamemaker" / "game"
        gamemaker_output = temp / "gamemaker" / "output"
        gamemaker_game.mkdir(parents=True)
        build_gamemaker(gamemaker_game / "data.win", b"")
        assert "RESULT:5:" in run_script("extract_gamemaker.py", gamemaker_game, gamemaker_output)
        previews = sorted(gamemaker_output.rglob("*.png"))
        assert len(previews) == 2
        for preview in previews:
            with Image.open(preview) as image:
                assert image.convert("RGBA").getpixel((0, 0)) == (10, 20, 30, 255)
        print("gamemaker-qoi=ok")

        spite_game = temp / "spite" / "game"
        spite_output = temp / "spite" / "output"
        spite_game.mkdir(parents=True)
        external_route, external_plain, archive_route, archive_plain = build_spite(spite_game)
        assert "RESULT:2:" in run_script("extract_spite.py", spite_game, spite_output)
        assert (spite_output / "decoded" / external_route).read_bytes() == external_plain
        assert (spite_output / "decoded" / archive_route).read_bytes() == archive_plain
        print("spite-dat=ok")

        assert_unreal_compressions()
        print("unreal-compressions=ok")

        assert_unreal_partial_extraction(temp)
        print("unreal-partial=ok")

        fallback_environment = os.environ.copy()
        fallback_environment["GAME_PATH"] = str(temp / "no-local-oodle")
        fallback_environment["ProgramFiles"] = str(temp / "no-installed-unreal")
        fallback_environment.pop("OODLE_DLL", None)
        fallback = subprocess.run(
            [
                sys.executable,
                "-c",
                "from pyuepak.oodle import oodle\n"
                "assert oodle().name == 'built-in open-source oozextract'\n"
                "print('open_source_oodle_fallback=ok')\n",
            ],
            env=fallback_environment,
            capture_output=True,
            text=True,
        )
        if fallback.returncode != 0:
            raise AssertionError("Open-source Oodle fallback failed:\n{}\n{}".format(fallback.stdout, fallback.stderr))
        print(fallback.stdout.strip())

        from pyuepak.aes_windows import aes_cfb_decrypt, aes_ecb_decrypt
        key = bytes.fromhex("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f")
        encrypted = bytes.fromhex("8ea2b7ca516745bfeafc49904b496089")
        assert aes_ecb_decrypt(key, encrypted).hex() == "00112233445566778899aabbccddeeff"
        cfb_key = bytes.fromhex("603deb1015ca71be2b73aef0857d77811f352c073b6108d72d9810a30914dff4")
        cfb_iv = bytes.fromhex("000102030405060708090a0b0c0d0e0f")
        cfb_plain = bytes.fromhex("6bc1bee22e409f96e93d7e117393172a")
        cfb_encrypted = bytes.fromhex("dc7e84bfda79164b7ecd8486985d3860")
        assert aes_cfb_decrypt(cfb_key, cfb_iv, cfb_encrypted) == cfb_plain
        print("windows_aes=ok")
    finally:
        shutil.rmtree(temp, ignore_errors=True)


if __name__ == "__main__":
    main()
