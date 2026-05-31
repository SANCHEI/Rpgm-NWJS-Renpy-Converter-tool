import hashlib
import os
import struct
import sys


MAGIC = b"GDPC"
PACK_DIR_ENCRYPTED = 1
PACK_REL_FILEBASE = 2
PACK_FILE_ENCRYPTED = 1
PACK_FILE_REMOVAL = 2


class UnsupportedArchive(Exception):
    pass


def read_exact(stream, size):
    value = stream.read(size)
    if len(value) != size:
        raise ValueError("Unexpected end of PCK archive.")
    return value


def read_u32(stream):
    return struct.unpack("<I", read_exact(stream, 4))[0]


def read_u64(stream):
    return struct.unpack("<Q", read_exact(stream, 8))[0]


def locate_pck(stream):
    stream.seek(0, os.SEEK_END)
    archive_size = stream.tell()
    if archive_size < 4:
        return None

    stream.seek(0)
    if stream.read(4) == MAGIC:
        return 0

    if archive_size < 12:
        return None
    stream.seek(archive_size - 4)
    if stream.read(4) != MAGIC:
        return None

    stream.seek(archive_size - 12)
    embedded_size = read_u64(stream)
    for candidate in (archive_size - embedded_size - 8, archive_size - embedded_size - 12):
        if candidate < 0:
            continue
        stream.seek(candidate)
        if stream.read(4) == MAGIC:
            return candidate
    return None


def normalize_name(value):
    value = value.replace("\\", "/")
    if value.startswith("res://"):
        value = value[6:]
    parts = []
    for part in value.split("/"):
        if not part or part == ".":
            continue
        if part == "..":
            raise ValueError("Archive entry escapes the output folder.")
        clean = "".join(char for char in part if char not in '<>:"|?*\0')
        parts.append(clean or "asset")
    return os.path.join(*parts) if parts else "asset"


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


def safe_output_path(root, relative):
    root = os.path.abspath(root)
    candidate = os.path.abspath(os.path.join(root, normalize_name(relative)))
    if candidate != root and not candidate.startswith(root + os.sep):
        raise ValueError("Archive entry escapes the output folder.")
    return candidate


def choose_offset(raw_offset, size, archive_size, pck_start):
    relative_offset = pck_start + raw_offset
    if relative_offset + size <= archive_size:
        return relative_offset
    if raw_offset + size <= archive_size:
        return raw_offset
    raise ValueError("PCK entry points outside the archive.")


def read_entries(stream, pck_start):
    stream.seek(0, os.SEEK_END)
    archive_size = stream.tell()
    stream.seek(pck_start + 4)
    pack_version = read_u32(stream)
    read_u32(stream)
    read_u32(stream)
    read_u32(stream)

    if pack_version == 1:
        read_exact(stream, 16 * 4)
        file_count = read_u32(stream)
        entries = []
        for _ in range(file_count):
            path_length = read_u32(stream)
            path = read_exact(stream, path_length).rstrip(b"\0").decode("utf-8", "replace")
            raw_offset = read_u64(stream)
            size = read_u64(stream)
            digest = read_exact(stream, 16)
            entries.append((path, choose_offset(raw_offset, size, archive_size, pck_start), size, digest))
        return entries

    if pack_version not in (2, 3):
        raise UnsupportedArchive("Unsupported Godot PCK format version: {}".format(pack_version))

    pack_flags = read_u32(stream)
    if pack_flags & PACK_DIR_ENCRYPTED:
        raise UnsupportedArchive("Encrypted Godot PCK directories are not supported yet.")

    file_base = read_u64(stream)
    if pack_version == 3 or pack_flags & PACK_REL_FILEBASE:
        file_base += pck_start

    if pack_version == 3:
        directory_offset = read_u64(stream) + pck_start
        stream.seek(directory_offset)
    else:
        read_exact(stream, 16 * 4)

    file_count = read_u32(stream)
    entries = []
    for _ in range(file_count):
        path_length = read_u32(stream)
        path = read_exact(stream, path_length).rstrip(b"\0").decode("utf-8", "replace")
        raw_offset = read_u64(stream)
        size = read_u64(stream)
        digest = read_exact(stream, 16)
        flags = read_u32(stream)
        if flags & PACK_FILE_REMOVAL:
            continue
        if flags & PACK_FILE_ENCRYPTED:
            raise UnsupportedArchive("Encrypted Godot PCK files are not supported yet.")
        entries.append((path, file_base + raw_offset, size, digest))
    return entries


def is_embedded_pck(path):
    try:
        with open(path, "rb") as stream:
            return locate_pck(stream) is not None
    except OSError:
        return False


def find_archives(game_path, output_path):
    output_path = os.path.abspath(output_path)
    archives = []
    for root, directories, files in os.walk(game_path):
        root_path = os.path.abspath(root)
        directories[:] = [
            name for name in directories
            if not os.path.abspath(os.path.join(root, name)).startswith(output_path + os.sep)
        ]
        for name in files:
            path = os.path.join(root, name)
            lowered = name.lower()
            if lowered.endswith(".pck") or lowered.endswith(".exe") and is_embedded_pck(path):
                archives.append(path)
    return sorted(set(archives))


def extract_archive(archive, output_path):
    extracted = 0
    byte_count = 0
    renamed = 0
    archive_output = os.path.join(output_path, "archives", normalize_name(os.path.splitext(os.path.basename(archive))[0]))
    with open(archive, "rb") as stream:
        pck_start = locate_pck(stream)
        if pck_start is None:
            raise ValueError("PCK header was not found.")
        entries = read_entries(stream, pck_start)
        for name, offset, size, expected_digest in entries:
            stream.seek(offset)
            data = read_exact(stream, size)
            if expected_digest != b"\0" * 16 and hashlib.md5(data).digest() != expected_digest:
                print("WARN:MD5 mismatch for {}".format(name))
            destination = safe_output_path(archive_output, name)
            destination, was_renamed = unique_path(destination)
            renamed += int(was_renamed)
            os.makedirs(os.path.dirname(destination), exist_ok=True)
            with open(destination, "wb") as output:
                output.write(data)
            extracted += 1
            byte_count += len(data)
    return extracted, byte_count, renamed


def main():
    game_path = os.environ["GAME_PATH"]
    output_path = os.environ["OUTPUT_PATH"]
    os.makedirs(output_path, exist_ok=True)
    archives = find_archives(game_path, output_path)
    print("TOTAL:{}".format(len(archives)))

    extracted = 0
    byte_count = 0
    errors = 0
    renamed = 0
    for index, archive in enumerate(archives, 1):
        try:
            print("Processing PCK: {}".format(os.path.relpath(archive, game_path)))
            current_extracted, current_bytes, current_renamed = extract_archive(archive, output_path)
            extracted += current_extracted
            byte_count += current_bytes
            renamed += current_renamed
        except Exception as error:
            errors += 1
            print("WARN:{}: {}".format(os.path.basename(archive), error))
        print("PROGRESS:{}:{}:{}".format(index, len(archives), byte_count))

    print("RESULT:{}:{}:{}:{}".format(extracted, byte_count, errors, renamed))
    return 0 if archives else 1


if __name__ == "__main__":
    sys.exit(main())
