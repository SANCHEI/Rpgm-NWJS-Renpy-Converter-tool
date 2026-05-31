import os
import struct
import sys
import zlib


MAGIC = b"XP3\r\n \n\x1a\x8bg\x01"
INDEX_CONTINUES = 0x80
INDEX_COMPRESSED = 0x01
SEGMENT_COMPRESSED = 0x01


def read_exact(stream, size):
    value = stream.read(size)
    if len(value) != size:
        raise ValueError("Unexpected end of XP3 archive.")
    return value


def read_u8(stream):
    return struct.unpack("<B", read_exact(stream, 1))[0]


def read_u64(stream):
    return struct.unpack("<Q", read_exact(stream, 8))[0]


def unpack_chunks(data):
    position = 0
    while position < len(data):
        if position + 12 > len(data):
            raise ValueError("Broken XP3 chunk header.")
        name = data[position:position + 4]
        size = struct.unpack_from("<Q", data, position + 4)[0]
        position += 12
        end = position + size
        if end > len(data):
            raise ValueError("Broken XP3 chunk size.")
        yield name, data[position:end]
        position = end


def read_index(stream, offset):
    stream.seek(offset)
    while True:
        flags = read_u8(stream)
        if flags != INDEX_CONTINUES:
            break
        read_exact(stream, 8)
        stream.seek(read_u64(stream))

    if flags == INDEX_COMPRESSED:
        compressed_size = read_u64(stream)
        original_size = read_u64(stream)
        block = zlib.decompress(read_exact(stream, compressed_size))
        if len(block) != original_size:
            raise ValueError("XP3 index size mismatch.")
        return block
    if flags == 0:
        return read_exact(stream, read_u64(stream))
    raise ValueError("Unsupported XP3 index flags: {}".format(flags))


def parse_entries(data):
    entries = []
    for chunk_name, chunk_data in unpack_chunks(data):
        if chunk_name != b"File":
            continue
        info = None
        segments = []
        for name, value in unpack_chunks(chunk_data):
            if name == b"info":
                if len(value) < 22:
                    raise ValueError("Broken XP3 info chunk.")
                flags, original_size, stored_size, name_length = struct.unpack_from("<IQQH", value)
                if flags & 0x80000000:
                    raise ValueError("Encrypted XP3 entries are not supported yet.")
                filename = value[22:22 + name_length * 2].decode("utf-16le", "replace")
                info = (filename, original_size, stored_size, flags)
            elif name == b"segm":
                if len(value) % 28:
                    raise ValueError("Broken XP3 segment chunk.")
                for position in range(0, len(value), 28):
                    segments.append(struct.unpack_from("<IQQQ", value, position))
        if info is not None and segments:
            entries.append((info, segments))
    return entries


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


def find_archives(game_path, output_path):
    output_path = os.path.abspath(output_path)
    archives = []
    for root, directories, files in os.walk(game_path):
        directories[:] = [
            name for name in directories
            if not os.path.abspath(os.path.join(root, name)).startswith(output_path + os.sep)
        ]
        archives.extend(os.path.join(root, name) for name in files if name.lower().endswith(".xp3"))
    return sorted(set(archives))


def extract_archive(archive, output_path):
    archive_output = os.path.join(output_path, "archives", normalize_name(os.path.splitext(os.path.basename(archive))[0]))
    extracted = 0
    byte_count = 0
    renamed = 0
    with open(archive, "rb") as stream:
        if read_exact(stream, len(MAGIC)) != MAGIC:
            raise ValueError("XP3 header was not found.")
        index = read_index(stream, read_u64(stream))
        for info, segments in parse_entries(index):
            filename, expected_size, _, _ = info
            content = bytearray()
            for flags, offset, original_size, stored_size in segments:
                stream.seek(offset)
                value = read_exact(stream, stored_size)
                if flags & SEGMENT_COMPRESSED:
                    value = zlib.decompress(value)
                if len(value) != original_size:
                    raise ValueError("XP3 segment size mismatch for {}".format(filename))
                content.extend(value)
            if len(content) != expected_size:
                raise ValueError("XP3 file size mismatch for {}".format(filename))
            destination = safe_output_path(archive_output, filename)
            destination, was_renamed = unique_path(destination)
            renamed += int(was_renamed)
            os.makedirs(os.path.dirname(destination), exist_ok=True)
            with open(destination, "wb") as output:
                output.write(content)
            extracted += 1
            byte_count += len(content)
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
            print("Processing XP3: {}".format(os.path.relpath(archive, game_path)))
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
