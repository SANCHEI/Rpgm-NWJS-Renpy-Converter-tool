import base64
from collections import Counter
import hashlib
import os
import re
import sys

from pyuepak import PakFile
from pyuepak.oodle import oodle


MAX_KEY_FILE_BYTES = 4 * 1024 * 1024
MAX_SCANNED_EXE_BYTES = 256 * 1024 * 1024
MAX_ENTRY_COUNT = 200000
HEX_KEY = re.compile(rb"(?<![0-9a-fA-F])[0-9a-fA-F]{64}(?![0-9a-fA-F])")
BASE64_KEY = re.compile(rb"(?<![A-Za-z0-9+/])[A-Za-z0-9+/]{43}=(?![A-Za-z0-9+/=])")


def normalize_name(value):
    value = value.replace("\\", "/")
    parts = []
    for part in value.split("/"):
        if not part or part == ".":
            continue
        if part == "..":
            raise ValueError("Archive entry escapes the output folder.")
        clean = "".join(char for char in part if char not in '<>:"|?*\0')
        parts.append(clean or "asset")
    return os.path.join(*parts) if parts else "asset"


def safe_output_path(root, relative):
    root = os.path.abspath(root)
    candidate = os.path.abspath(os.path.join(root, normalize_name(relative)))
    if candidate != root and not candidate.startswith(root + os.sep):
        raise ValueError("Archive entry escapes the output folder.")
    return candidate


def unique_path(path):
    if not os.path.exists(path):
        return path, False
    stem, extension = os.path.splitext(path)
    suffix = 2
    while True:
        candidate = "{} ({}){}".format(stem, suffix, extension)
        if not os.path.exists(candidate):
            return candidate, True
        suffix += 1


def find_files(game_path, output_path):
    output_path = os.path.abspath(output_path)
    paks = []
    utocs = []
    for root, directories, files in os.walk(game_path):
        directories[:] = [
            name for name in directories
            if not os.path.abspath(os.path.join(root, name)).startswith(output_path + os.sep)
        ]
        for name in files:
            path = os.path.join(root, name)
            lowered = name.lower()
            if lowered.endswith(".pak"):
                paks.append(path)
            elif lowered.endswith(".utoc"):
                utocs.append(path)
    return sorted(set(paks)), sorted(set(utocs))


def normalize_key(value):
    value = value.strip().strip("\"'")
    if value.lower().startswith("0x"):
        value = value[2:]
    if not value:
        return None
    try:
        decoded = bytes.fromhex(value) if len(value) == 64 else base64.b64decode(value, validate=True)
        return decoded.hex() if len(decoded) == 32 else None
    except (ValueError, TypeError):
        return None


def append_key(keys, value):
    key = normalize_key(value)
    if key and key not in keys:
        keys.append(key)


def discover_keys(game_path, output_path):
    keys = []
    for value in re.split(r"[\s,;]+", os.environ.get("OPTIONAL_KEY", "")):
        append_key(keys, value)

    output_path = os.path.abspath(output_path)
    for root, directories, files in os.walk(game_path):
        directories[:] = [
            name for name in directories
            if name.lower() != "extracted"
            and not os.path.abspath(os.path.join(root, name)).startswith(output_path + os.sep)
        ]
        for name in files:
            path = os.path.join(root, name)
            lowered = name.lower()
            try:
                size = os.path.getsize(path)
                if lowered in ("keys.txt", "unreal.keys", "aes-keys.txt") and size <= MAX_KEY_FILE_BYTES:
                    with open(path, "r", encoding="utf-8", errors="ignore") as stream:
                        for line in stream:
                            append_key(keys, line.split("#", 1)[0])
                elif lowered.endswith(".exe") and size <= MAX_SCANNED_EXE_BYTES:
                    with open(path, "rb") as stream:
                        content = stream.read()
                    for match in HEX_KEY.findall(content):
                        append_key(keys, match.decode("ascii"))
                    for match in BASE64_KEY.findall(content):
                        append_key(keys, match.decode("ascii"))
            except OSError:
                continue
    return keys


def open_archive(archive, keys):
    errors = []
    for key in [None] + keys:
        try:
            pak = PakFile()
            if key:
                pak.set_key(key)
            pak.read(archive)
            files = pak.list_files()
            if len(files) > MAX_ENTRY_COUNT:
                raise ValueError("Unreal PAK contains too many entries.")
            for filename in files:
                normalize_name(filename)
            return pak
        except Exception as error:
            errors.append(str(error))
    detail = errors[-1] if errors else "archive could not be read"
    raise ValueError("Could not open Unreal PAK with the discovered AES keys: {}".format(detail))


def extract_archive(archive, output_path, keys):
    archive_output = os.path.join(output_path, "archives", normalize_name(os.path.splitext(os.path.basename(archive))[0]))
    pak = open_archive(archive, keys)

    extracted = 0
    byte_count = 0
    renamed = 0
    skipped = 0
    skipped_files = []
    skip_reasons = Counter()
    for filename in pak.list_files():
        try:
            data = pak.read_file(filename)
            entry = pak._index.entrys.get(filename)
            if entry is not None and entry.hash and hashlib.sha1(data).digest() != entry.hash:
                raise ValueError("SHA-1 mismatch")
            destination = safe_output_path(archive_output, filename)
            destination, was_renamed = unique_path(destination)
            renamed += int(was_renamed)
            os.makedirs(os.path.dirname(destination), exist_ok=True)
            with open(destination, "wb") as output:
                output.write(data)
            extracted += 1
            byte_count += len(data)
        except Exception as error:
            reason = str(error).strip() or error.__class__.__name__
            skipped += 1
            skip_reasons[reason] += 1
            skipped_files.append("{}\t{}".format(filename, reason))

    if skipped_files:
        os.makedirs(archive_output, exist_ok=True)
        manifest = os.path.join(archive_output, "Unreal-skipped-files.txt")
        with open(manifest, "w", encoding="utf-8") as output:
            output.write("\n".join(skipped_files) + "\n")
        for reason, count in skip_reasons.most_common(10):
            print("WARN:{} file(s) skipped: {}".format(count, reason))
        if len(skip_reasons) > 10:
            print("WARN:{} additional skip reason(s) are listed in {}.".format(
                len(skip_reasons) - 10,
                os.path.relpath(manifest, output_path),
            ))
    return extracted, byte_count, renamed, skipped


def main():
    game_path = os.environ["GAME_PATH"]
    output_path = os.environ["OUTPUT_PATH"]
    os.makedirs(output_path, exist_ok=True)
    keys = discover_keys(game_path, output_path)
    if keys:
        print("Discovered Unreal AES key candidate(s): {}".format(len(keys)))
    archives, utocs = find_files(game_path, output_path)
    total = len(archives) + len(utocs)
    print("TOTAL:{}".format(total))
    print("Oodle decoder: {}".format(getattr(oodle(), "name", "unavailable")))

    extracted = 0
    byte_count = 0
    errors = 0
    renamed = 0
    skipped = 0
    processed = 0
    for archive in archives:
        try:
            print("Processing Unreal PAK: {}".format(os.path.relpath(archive, game_path)))
            current_extracted, current_bytes, current_renamed, current_skipped = extract_archive(archive, output_path, keys)
            extracted += current_extracted
            byte_count += current_bytes
            renamed += current_renamed
            skipped += current_skipped
        except Exception as error:
            errors += 1
            print("WARN:{}: {}".format(os.path.basename(archive), error))
        processed += 1
        print("PROGRESS:{}:{}:{}".format(processed, total, byte_count))

    for archive in utocs:
        errors += 1
        skipped += 1
        processed += 1
        print("WARN:{}: Unreal IoStore (.utoc/.ucas) is not supported yet.".format(os.path.basename(archive)))
        print("PROGRESS:{}:{}:{}".format(processed, total, byte_count))

    print("RESULT:{}:{}:{}:{}:{}".format(extracted, byte_count, errors, renamed, skipped))
    return 0 if total else 1


if __name__ == "__main__":
    sys.exit(main())
