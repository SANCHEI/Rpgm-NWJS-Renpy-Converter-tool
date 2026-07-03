import base64
import hashlib
import io
import os
import re
import struct
import sys

try:
    from PIL import Image
except Exception:
    Image = None

from pyuepak.aes_windows import aes_cfb_decrypt


MAGIC = b"GDPC"
PACK_DIR_ENCRYPTED = 1
PACK_REL_FILEBASE = 2
PACK_FILE_ENCRYPTED = 1
PACK_FILE_REMOVAL = 2
MAX_ENTRY_BYTES = 4 * 1024 * 1024 * 1024
MAX_ENTRY_COUNT = 200000
MAX_KEY_FILE_BYTES = 4 * 1024 * 1024
MAX_SCANNED_EXE_BYTES = 256 * 1024 * 1024
HEX_KEY = re.compile(rb"(?<![0-9a-fA-F])[0-9a-fA-F]{64}(?![0-9a-fA-F])")
BASE64_KEY = re.compile(rb"(?<![A-Za-z0-9+/])[A-Za-z0-9+/]{43}=(?![A-Za-z0-9+/=])")
KEY_SOURCES = []
IMPORTED_TEXTURE_EXTENSIONS = (".ctex", ".stex")
IMPORTED_AUDIO_SUFFIXES = (".oggvorbisstr", ".oggstr", ".mp3str")


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


def imported_preview_stem(path):
    name = os.path.basename(path)
    stem = os.path.splitext(name)[0]
    match = re.match(r"^(.*)-[0-9a-fA-F]{32}(?:\.[^.]+)?$", stem)
    if match:
        stem = match.group(1)
    original_stem, _ = os.path.splitext(stem)
    return original_stem or stem or "asset"


def extract_embedded_image(data):
    candidates = []
    webp = data.find(b"RIFF")
    while webp >= 0:
        if webp + 12 <= len(data) and data[webp + 8:webp + 12] == b"WEBP":
            size = struct.unpack_from("<I", data, webp + 4)[0] + 8
            if size > 12 and webp + size <= len(data):
                candidates.append((webp, webp + size, ".webp"))
                break
        webp = data.find(b"RIFF", webp + 1)

    png = data.find(b"\x89PNG\r\n\x1a\n")
    if png >= 0:
        end = data.find(b"IEND", png)
        if end >= 0 and end + 8 <= len(data):
            candidates.append((png, end + 8, ".png"))

    jpg = data.find(b"\xff\xd8\xff")
    if jpg >= 0:
        end = data.find(b"\xff\xd9", jpg + 2)
        if end >= 0:
            candidates.append((jpg, end + 2, ".jpg"))

    if not candidates:
        return None, None
    start, end, extension = sorted(candidates, key=lambda item: item[0])[0]
    return data[start:end], extension


def is_imported_texture_path(path):
    lowered = path.lower().replace("\\", "/")
    if lowered.endswith(IMPORTED_TEXTURE_EXTENSIONS):
        return True
    name = os.path.basename(lowered)
    return "/.godot/imported/" in lowered and re.search(r"\.(png|jpg|jpeg|webp)-[0-9a-f]{32}\.", name) is not None


def plausible_dimension(value):
    return 0 < value <= 16384


def iter_u32_pairs(data):
    limit = min(len(data) - 8, 128)
    seen = set()
    preferred = (8, 12, 16, 20, 24, 28, 32, 36, 40, 48, 64)
    for offset in preferred:
        if offset + 8 <= len(data):
            width = struct.unpack_from("<I", data, offset)[0]
            height = struct.unpack_from("<I", data, offset + 4)[0]
            if plausible_dimension(width) and plausible_dimension(height):
                key = (width, height)
                if key not in seen:
                    seen.add(key)
                    yield width, height
    for offset in range(0, limit + 1, 4):
        width = struct.unpack_from("<I", data, offset)[0]
        height = struct.unpack_from("<I", data, offset + 4)[0]
        if plausible_dimension(width) and plausible_dimension(height):
            key = (width, height)
            if key not in seen:
                seen.add(key)
                yield width, height


def try_decode_uncompressed_texture(data):
    if Image is None or len(data) < 32:
        return None, "Pillow is unavailable"
    formats = ((4, "RGBA"), (3, "RGB"), (2, "LA"), (1, "L"))
    for width, height in iter_u32_pairs(data):
        pixels = width * height
        if pixels <= 0 or pixels > 268435456:
            continue
        for bpp, mode in formats:
            required = pixels * bpp
            if required <= 0 or required > len(data):
                continue
            for offset in range(16, min(len(data), 512), 4):
                remaining = len(data) - offset
                if remaining == required or remaining >= required and remaining - required <= 64:
                    try:
                        image = Image.frombytes(mode, (width, height), data[offset:offset + required])
                        output = io.BytesIO()
                        image.save(output, format="PNG")
                        return output.getvalue(), "raw-{}-{}x{}".format(mode, width, height)
                    except Exception:
                        continue
    return None, "unsupported compressed or unknown Godot texture format"


def write_imported_preview(destination, data):
    if not is_imported_texture_path(destination):
        return 0, 0, 0, "", ""
    image, extension = extract_embedded_image(data)
    method = "embedded" if image else ""
    if not image:
        image, method = try_decode_uncompressed_texture(data)
        extension = ".png" if image else ""
    if not image:
        return 0, 0, 0, "", method or "unsupported compressed or unknown Godot texture format"
    preview_name = imported_preview_stem(destination) + extension
    preview_path = os.path.join(os.path.dirname(destination), preview_name)
    preview_path, was_renamed = unique_path(preview_path)
    with open(preview_path, "wb") as output:
        output.write(image)
    return 1, len(image), int(was_renamed), method, ""

def choose_offset(raw_offset, size, archive_size, pck_start):
    relative_offset = pck_start + raw_offset
    if relative_offset + size <= archive_size:
        return relative_offset
    if raw_offset + size <= archive_size:
        return raw_offset
    raise ValueError("PCK entry points outside the archive.")


def parse_key(value):
    value = value.strip().strip("\"'")
    if not value:
        return None
    try:
        if len(value) == 64:
            decoded = bytes.fromhex(value)
        else:
            decoded = base64.b64decode(value, validate=True)
        return decoded if len(decoded) == 32 else None
    except (ValueError, TypeError):
        return None


def append_key(keys, key):
    if key is not None and key not in keys:
        keys.append(key)
        return True
    return False


def add_key(keys, value, source):
    key = parse_key(value)
    if append_key(keys, key):
        KEY_SOURCES.append(source)


def discover_keys(game_path, output_path):
    keys = []
    del KEY_SOURCES[:]
    optional = os.environ.get("OPTIONAL_KEY", "")
    for value in re.split(r"[\s,;]+", optional):
        add_key(keys, value, "manual field")

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
                if lowered in ("keys.txt", "godot.keys", "godot-key.txt") and size <= MAX_KEY_FILE_BYTES:
                    with open(path, "r", encoding="utf-8", errors="ignore") as stream:
                        for line in stream:
                            add_key(keys, line.split("#", 1)[0], lowered)
                elif lowered.endswith(".exe") and size <= MAX_SCANNED_EXE_BYTES:
                    with open(path, "rb") as stream:
                        content = stream.read()
                    for match in HEX_KEY.findall(content):
                        add_key(keys, match.decode("ascii"), name)
                    for match in BASE64_KEY.findall(content):
                        add_key(keys, match.decode("ascii"), name)
            except OSError:
                continue
    return keys


def decrypt_block(stream, key):
    expected_digest = read_exact(stream, 16)
    size = read_u64(stream)
    if size > MAX_ENTRY_BYTES:
        raise ValueError("Encrypted Godot block is too large.")
    iv = read_exact(stream, 16)
    encrypted = read_exact(stream, (size + 15) & ~15)
    data = aes_cfb_decrypt(key, iv, encrypted)[:size]
    if hashlib.md5(data).digest() != expected_digest:
        raise ValueError("Godot encrypted block MD5 mismatch.")
    return data


def decrypt_with_candidates(stream, keys):
    if not keys:
        raise UnsupportedArchive("Encrypted Godot PCK: no key candidates were found. Paste a 64-character HEX key or place it in keys.txt next to the game.")
    start = stream.tell()
    for key in keys:
        try:
            stream.seek(start)
            return decrypt_block(stream, key), key
        except (OSError, ValueError):
            continue
    raise UnsupportedArchive("Encrypted Godot PCK: tried {} key candidate(s), but none passed MD5 validation. The key is likely wrong or the archive uses an unsupported encryption variant.".format(len(keys)))


def read_directory(stream, file_count, file_base, archive_size):
    if file_count > MAX_ENTRY_COUNT:
        raise ValueError("Godot PCK contains too many entries.")
    entries = []
    for _ in range(file_count):
        path_length = read_u32(stream)
        if path_length > 1024 * 1024:
            raise ValueError("Godot PCK entry path is too long.")
        path = read_exact(stream, path_length).rstrip(b"\0").decode("utf-8", "replace")
        raw_offset = read_u64(stream)
        size = read_u64(stream)
        if size > MAX_ENTRY_BYTES:
            raise ValueError("Godot PCK entry is too large: {}".format(path))
        digest = read_exact(stream, 16)
        flags = read_u32(stream)
        if flags & PACK_FILE_REMOVAL:
            continue
        offset = file_base + raw_offset
        if offset < 0 or offset > archive_size or not flags & PACK_FILE_ENCRYPTED and offset + size > archive_size:
            raise ValueError("PCK entry points outside the archive: {}".format(path))
        entries.append((path, offset, size, digest, bool(flags & PACK_FILE_ENCRYPTED)))
    return entries


def read_entries(stream, pck_start, keys):
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
            entries.append((path, choose_offset(raw_offset, size, archive_size, pck_start), size, digest, False))
        return entries, None

    if pack_version not in (2, 3, 4):
        raise UnsupportedArchive("Unsupported Godot PCK format version: {}. Supported versions: 1, 2, 3 and compatible 4 archives.".format(pack_version))

    pack_flags = read_u32(stream)
    file_base = read_u64(stream)
    if pack_version in (3, 4) or pack_flags & PACK_REL_FILEBASE:
        file_base += pck_start

    if pack_version in (3, 4):
        directory_offset = read_u64(stream) + pck_start
        stream.seek(directory_offset)
    else:
        read_exact(stream, 16 * 4)

    file_count = read_u32(stream)
    selected_key = None
    directory_stream = stream
    if pack_flags & PACK_DIR_ENCRYPTED:
        directory_data, selected_key = decrypt_with_candidates(stream, keys)
        directory_stream = io.BytesIO(directory_data)
    return read_directory(directory_stream, file_count, file_base, archive_size), selected_key


def is_embedded_pck(path):
    try:
        with open(path, "rb") as stream:
            return locate_pck(stream) is not None
    except OSError:
        return False


def find_archives(game_path, output_path):
    output_path = os.path.abspath(output_path)
    archives = []
    if os.path.isfile(game_path):
        lowered = game_path.lower()
        if lowered.endswith(".pck") or lowered.endswith(".exe") and is_embedded_pck(game_path):
            return [os.path.abspath(game_path)]
        return []
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


def extract_archive(archive, output_path, keys):
    extracted = 0
    byte_count = 0
    renamed = 0
    preview_count = 0
    imported_texture_count = 0
    imported_audio_count = 0
    container_texture_count = 0
    raw_texture_preview_count = 0
    unsupported_texture_reasons = {}
    archive_output = os.path.join(output_path, "archives", normalize_name(os.path.splitext(os.path.basename(archive))[0]))
    with open(archive, "rb") as stream:
        pck_start = locate_pck(stream)
        if pck_start is None:
            raise ValueError("PCK header was not found.")
        entries, selected_key = read_entries(stream, pck_start, keys)
        for name, offset, size, expected_digest, encrypted in entries:
            stream.seek(offset)
            if encrypted:
                data, selected_key = decrypt_with_candidates(stream, ([selected_key] if selected_key else []) + keys)
                if len(data) != size:
                    raise ValueError("Encrypted Godot entry size mismatch for {}".format(name))
            else:
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

            is_imported_texture = is_imported_texture_path(destination)
            if is_imported_texture:
                imported_texture_count += 1
            if destination.lower().endswith(IMPORTED_AUDIO_SUFFIXES):
                imported_audio_count += 1

            preview_extracted, preview_bytes, preview_renamed, preview_method, unsupported_reason = write_imported_preview(destination, data)
            if preview_method.startswith("raw-"):
                raw_texture_preview_count += preview_extracted
            if is_imported_texture and preview_extracted == 0:
                container_texture_count += 1
                if unsupported_reason:
                    unsupported_texture_reasons[unsupported_reason] = unsupported_texture_reasons.get(unsupported_reason, 0) + 1
            extracted += preview_extracted
            byte_count += preview_bytes
            renamed += preview_renamed
            preview_count += preview_extracted
    if preview_count:
        print("Godot imported preview(s): {}".format(preview_count))
    diagnostics = {
        "archive": archive,
        "imported_textures": imported_texture_count,
        "previews": preview_count,
        "texture_containers": container_texture_count,
        "raw_texture_previews": raw_texture_preview_count,
        "unsupported_texture_reasons": unsupported_texture_reasons,
        "imported_audio_streams": imported_audio_count,
    }
    return extracted, byte_count, renamed, diagnostics


def write_godot_diagnostics(output_path, archive_diagnostics):
    path = os.path.join(output_path, "GameAssetTool-godot-diagnostics.txt")
    try:
        textures = sum(item.get("imported_textures", 0) for item in archive_diagnostics)
        previews = sum(item.get("previews", 0) for item in archive_diagnostics)
        containers = sum(item.get("texture_containers", 0) for item in archive_diagnostics)
        audio = sum(item.get("imported_audio_streams", 0) for item in archive_diagnostics)
        raw_previews = sum(item.get("raw_texture_previews", 0) for item in archive_diagnostics)
        unsupported_reasons = {}
        for item in archive_diagnostics:
            for reason, count in item.get("unsupported_texture_reasons", {}).items():
                unsupported_reasons[reason] = unsupported_reasons.get(reason, 0) + count
        with open(path, "w", encoding="utf-8") as output:
            output.write("Godot diagnostics\n")
            output.write("Archives: {}\n".format(len(archive_diagnostics)))
            output.write("Imported texture cache files: {}\n".format(textures))
            output.write("Recovered imported previews: {}\n".format(previews))
            output.write("Recovered raw texture previews: {}\n".format(raw_previews))
            output.write("Texture cache files without embedded preview: {}\n".format(containers))
            output.write("Imported audio stream/cache files: {}\n".format(audio))
            output.write("Note: .ctex/.stex are Godot imported texture containers. Game Asset Tool recovers embedded PNG/JPG/WebP previews and simple raw RGBA/RGB/L8/LA8 payloads when recognizable; compressed GPU formats still need engine-specific decoding.\n")
            if unsupported_reasons:
                output.write("Unsupported texture reasons:\n")
                for reason, count in sorted(unsupported_reasons.items(), key=lambda item: item[1], reverse=True):
                    output.write("- {} x{}\n".format(reason, count))
            if archive_diagnostics:
                output.write("Archive details:\n")
                for item in archive_diagnostics[:80]:
                    output.write("- {} | textures={} | previews={} | texture-containers={} | audio-streams={}\n".format(
                        rel_path(item.get("archive", "")),
                        item.get("imported_textures", 0),
                        item.get("previews", 0),
                        item.get("texture_containers", 0),
                        item.get("imported_audio_streams", 0),
                    ))
    except Exception as error:
        print("WARN:Godot diagnostics failed: {}".format(error))

def main():
    game_path = os.environ["GAME_PATH"]
    output_path = os.environ["OUTPUT_PATH"]
    os.makedirs(output_path, exist_ok=True)
    keys = discover_keys(game_path, output_path)
    if keys:
        print("Discovered Godot key candidate(s): {}".format(len(keys)))
        print("Godot key source(s): {}".format(", ".join(sorted(set(KEY_SOURCES)))))
    else:
        print("Godot key candidate(s): 0")
    archives = find_archives(game_path, output_path)
    display_root = game_path if os.path.isdir(game_path) else os.path.dirname(game_path)
    print("TOTAL:{}".format(len(archives)))

    extracted = 0
    byte_count = 0
    errors = 0
    renamed = 0
    archive_diagnostics = []
    for index, archive in enumerate(archives, 1):
        try:
            print("Processing PCK: {}".format(os.path.relpath(archive, display_root)))
            current_extracted, current_bytes, current_renamed, current_diagnostics = extract_archive(archive, output_path, keys)
            archive_diagnostics.append(current_diagnostics)
            extracted += current_extracted
            byte_count += current_bytes
            renamed += current_renamed
        except Exception as error:
            errors += 1
            print("WARN:{}: {}".format(os.path.basename(archive), error))
        print("PROGRESS:{}:{}:{}".format(index, len(archives), byte_count))

    write_godot_diagnostics(output_path, archive_diagnostics)
    skipped = sum(item.get("texture_containers", 0) for item in archive_diagnostics)
    print("RESULT:{}:{}:{}:{}:{}".format(extracted, byte_count, errors, renamed, skipped))
    return 0 if archives else 1


if __name__ == "__main__":
    sys.exit(main())
