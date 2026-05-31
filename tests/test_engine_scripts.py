import hashlib
import os
import pathlib
import shutil
import struct
import subprocess
import sys
import tempfile
import zlib

from pyuepak import PakFile


ROOT = pathlib.Path(__file__).resolve().parents[1]
MAGIC_GODOT = b"GDPC"
MAGIC_XP3 = b"XP3\r\n \n\x1a\x8bg\x01"


def run_script(script_name, game_path, output_path):
    environment = os.environ.copy()
    environment["GAME_PATH"] = str(game_path)
    environment["OUTPUT_PATH"] = str(output_path)
    process = subprocess.run(
        [sys.executable, str(ROOT / "source" / "scripts" / script_name)],
        env=environment,
        capture_output=True,
        text=True,
    )
    if process.returncode != 0:
        raise AssertionError("{} failed:\n{}\n{}".format(script_name, process.stdout, process.stderr))
    return process.stdout


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

        fallback_environment = os.environ.copy()
        fallback_environment["GAME_PATH"] = str(temp / "no-local-oodle")
        fallback_environment["ProgramFiles"] = str(temp / "no-installed-unreal")
        fallback_environment.pop("OODLE_DLL", None)
        fallback = subprocess.run(
            [
                sys.executable,
                "-c",
                "from pyuepak.oodle import OodleUnavailable, oodle\n"
                "try:\n"
                "    oodle().decompress(b'data', 1)\n"
                "    raise AssertionError('Oodle fallback unexpectedly decompressed data.')\n"
                "except OodleUnavailable as error:\n"
                "    assert 'no local oo2core' in str(error)\n"
                "    print('local_oodle_fallback=ok')\n",
            ],
            env=fallback_environment,
            capture_output=True,
            text=True,
        )
        if fallback.returncode != 0:
            raise AssertionError("Oodle fallback failed:\n{}\n{}".format(fallback.stdout, fallback.stderr))
        print(fallback.stdout.strip())

        from pyuepak.aes_windows import aes_ecb_decrypt
        key = bytes.fromhex("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f")
        encrypted = bytes.fromhex("8ea2b7ca516745bfeafc49904b496089")
        assert aes_ecb_decrypt(key, encrypted).hex() == "00112233445566778899aabbccddeeff"
        print("windows_aes=ok")
    finally:
        shutil.rmtree(temp, ignore_errors=True)


if __name__ == "__main__":
    main()
