import hashlib
import hmac
import os
import re
import struct
import sys

import brotli
from Crypto.Cipher import ChaCha20


SPAK_MAGIC = b"SPAK"
SPITE_SALT = b"spite-media-salt"
SPITE_INFO_PREFIX = b"spite-media-chacha20:"
SPITE_MARKER = SPITE_SALT + SPITE_INFO_PREFIX
MAX_ENTRY_COUNT = 100000
MAX_ENTRY_BYTES = 512 * 1024 * 1024
MAX_TOTAL_BYTES = 8 * 1024 * 1024 * 1024
MAX_EXE_BYTES = 256 * 1024 * 1024
TEXT_ASSET_EXTENSIONS = {".js", ".css", ".html", ".json", ".svg"}
RESOURCE_PATH = re.compile(
    r"(?<![A-Za-z0-9_@.+~()/=-])"
    r"(/[A-Za-z0-9_@.+~()/=-]{1,240}|[A-Za-z0-9_@.+~()/-]{1,240})"
    r"\.(png|jpe?g|webp|gif|svg|mp4|webm|ogg|wav|mp3|woff2?|json|js|css|html)"
    r"(?=[^A-Za-z0-9_@.+~()/=-]|$)",
    re.IGNORECASE,
)
SAFE_ASSET_KEY = re.compile(r"/[A-Za-z0-9_@.+~()=/\\-]{1,259}")


def normalize_name(value):
    value = value.replace("\\", "/").lstrip("/")
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


def append_manifest(manifest, source, entry, output, size, flags, status):
    manifest.append("{} | {} | {} | {} | {} | {}".format(source, entry, output, size, flags, status))


def derive_cipher(key_material, route):
    extracted = hmac.new(SPITE_SALT, key_material, hashlib.sha256).digest()
    info = SPITE_INFO_PREFIX + route.encode("utf-8")
    first = hmac.new(extracted, info + b"\x01", hashlib.sha256).digest()
    second = hmac.new(extracted, first + info + b"\x02", hashlib.sha256).digest()
    expanded = first + second
    return ChaCha20.new(key=expanded[:32], nonce=expanded[32:44])


def decrypt_header(key_material, route, header):
    return derive_cipher(key_material, route).decrypt(header)


def detect_extension(header):
    if header.startswith(b"\x89PNG\r\n\x1a\n"):
        return ".png"
    if header.startswith(b"\xff\xd8\xff"):
        return ".jpg"
    if header.startswith((b"GIF87a", b"GIF89a")):
        return ".gif"
    if header.startswith(b"OggS"):
        return ".ogg"
    if header.startswith(b"ID3") or len(header) >= 2 and header[0] == 0xFF and header[1] & 0xE0 == 0xE0:
        return ".mp3"
    if header.startswith(b"RIFF") and header[8:12] == b"WAVE":
        return ".wav"
    if header.startswith(b"RIFF") and header[8:12] == b"WEBP":
        return ".webp"
    if len(header) >= 8 and header[4:8] == b"ftyp":
        return ".mp4"
    if header.startswith(b"\x1aE\xdf\xa3"):
        return ".webm"
    if header.lstrip().startswith(b"<svg"):
        return ".svg"
    return ""


def route_identifier(route):
    return hashlib.sha256(route.encode("utf-8")).hexdigest()[:16]


def read_pe_sections(data):
    if len(data) < 0x40 or data[:2] != b"MZ":
        raise ValueError("SPITE executable is not a PE file.")
    pe_offset = struct.unpack_from("<I", data, 0x3C)[0]
    if pe_offset + 24 > len(data) or data[pe_offset:pe_offset + 4] != b"PE\0\0":
        raise ValueError("SPITE executable PE header is invalid.")
    section_count = struct.unpack_from("<H", data, pe_offset + 6)[0]
    optional_size = struct.unpack_from("<H", data, pe_offset + 20)[0]
    optional_offset = pe_offset + 24
    if optional_offset + optional_size > len(data):
        raise ValueError("SPITE executable optional header is truncated.")
    magic = struct.unpack_from("<H", data, optional_offset)[0]
    if magic == 0x20B:
        image_base = struct.unpack_from("<Q", data, optional_offset + 24)[0]
    elif magic == 0x10B:
        image_base = struct.unpack_from("<I", data, optional_offset + 28)[0]
    else:
        raise ValueError("Unsupported PE optional header.")
    sections = []
    section_offset = optional_offset + optional_size
    for index in range(section_count):
        offset = section_offset + index * 40
        if offset + 40 > len(data):
            raise ValueError("SPITE executable section table is truncated.")
        name = data[offset:offset + 8].split(b"\0", 1)[0].decode("ascii", "ignore")
        virtual_size, virtual_address, raw_size, raw_offset = struct.unpack_from("<IIII", data, offset + 8)
        sections.append((name, virtual_address, virtual_size, raw_offset, raw_size))
    return image_base, sections


def va_to_raw(image_base, sections, data, virtual_address, size):
    relative = virtual_address - image_base
    for _, section_va, virtual_size, raw_offset, raw_size in sections:
        if section_va <= relative < section_va + max(virtual_size, raw_size):
            offset = raw_offset + relative - section_va
            if offset >= 0 and offset + size <= len(data):
                return offset
    return None


def read_virtual(image_base, sections, data, virtual_address, size):
    offset = va_to_raw(image_base, sections, data, virtual_address, size)
    return None if offset is None else data[offset:offset + size]


def find_spite_executable(game_path):
    root = game_path if os.path.isdir(game_path) else os.path.dirname(game_path)
    for name in sorted(os.listdir(root)):
        path = os.path.join(root, name)
        if not os.path.isfile(path) or not name.lower().endswith(".exe"):
            continue
        if os.path.getsize(path) > MAX_EXE_BYTES:
            continue
        with open(path, "rb") as stream:
            data = stream.read()
        marker = data.find(SPITE_MARKER)
        if marker >= 48:
            return path, data, data[marker - 48:marker]
    return None, None, None


def extract_resource_routes(executable_data, available_ids):
    image_base, sections = read_pe_sections(executable_data)
    text_parts = []
    for name, _, _, raw_offset, raw_size in sections:
        if name != ".rdata" or raw_offset + raw_size > len(executable_data):
            continue
        for offset in range(raw_offset, raw_offset + raw_size - 31, 16):
            key_pointer, key_size, value_pointer, value_size = struct.unpack_from("<QQQQ", executable_data, offset)
            if not 1 <= key_size <= 260 or not 1 <= value_size <= 50 * 1024 * 1024:
                continue
            key_bytes = read_virtual(image_base, sections, executable_data, key_pointer, key_size)
            if not key_bytes or not key_bytes.startswith(b"/"):
                continue
            try:
                key = key_bytes.decode("utf-8")
            except UnicodeDecodeError:
                continue
            if not SAFE_ASSET_KEY.fullmatch(key) or os.path.splitext(key)[1].lower() not in TEXT_ASSET_EXTENSIONS:
                continue
            value = read_virtual(image_base, sections, executable_data, value_pointer, value_size)
            if value is None:
                continue
            try:
                text_parts.append(brotli.decompress(value).decode("utf-8", "ignore"))
            except brotli.error:
                continue

    routes = {}
    for text in text_parts:
        for match in RESOURCE_PATH.finditer(text):
            value = match.group(1) + "." + match.group(2)
            relative = value.lstrip("/")
            for candidate in (relative, value, "./" + relative):
                identifier = route_identifier(candidate)
                if identifier in available_ids:
                    routes.setdefault(identifier, candidate)
    return routes


def find_dat_files(game_path, output_path):
    if os.path.isfile(game_path):
        return [os.path.abspath(game_path)]
    output_path = os.path.abspath(output_path)
    files = []
    for root, directories, names in os.walk(game_path):
        directories[:] = [
            name for name in directories
            if not os.path.abspath(os.path.join(root, name)).startswith(output_path + os.sep)
            and name.lower() != "extracted"
        ]
        for name in names:
            if name.lower().endswith(".dat"):
                files.append(os.path.join(root, name))
    return sorted(set(files))


def is_spak(path):
    try:
        with open(path, "rb") as stream:
            return stream.read(4) == SPAK_MAGIC
    except OSError:
        return False


def read_spak_entries(path):
    with open(path, "rb") as stream:
        if stream.read(4) != SPAK_MAGIC:
            raise ValueError("SPAK signature not found.")
        version, count = struct.unpack("<II", stream.read(8))
        if version != 1:
            raise ValueError("Unsupported SPAK version: {}".format(version))
        if count > MAX_ENTRY_COUNT:
            raise ValueError("SPAK entry limit exceeded.")
        payload_offset = 12 + count * 28
        if payload_offset > os.path.getsize(path):
            raise ValueError("SPAK table is truncated.")
        entries = []
        total = 0
        for index in range(count):
            raw_name = stream.read(16).split(b"\0", 1)[0].decode("ascii", "ignore")
            offset, size, flags = struct.unpack("<III", stream.read(12))
            if not re.fullmatch(r"[0-9a-fA-F]{16}", raw_name):
                raw_name = "entry-{:04d}".format(index + 1)
            if size > MAX_ENTRY_BYTES or payload_offset + offset + size > os.path.getsize(path):
                continue
            total += size
            if total > MAX_TOTAL_BYTES:
                raise ValueError("SPAK total output limit exceeded.")
            entries.append((raw_name.lower(), payload_offset + offset, size, flags))
        return entries


def write_stream(source, destination, size, cipher=None):
    os.makedirs(os.path.dirname(destination), exist_ok=True)
    remaining = size
    with open(destination, "wb") as output:
        while remaining:
            block = source.read(min(1024 * 1024, remaining))
            if not block:
                raise ValueError("DAT payload is truncated.")
            output.write(cipher.decrypt(block) if cipher else block)
            remaining -= len(block)


def relative_output(output_path, destination):
    return os.path.relpath(destination, output_path).replace("\\", "/")


def choose_destination(output_path, route, fallback, header, key_material, status):
    if route and key_material:
        decrypted = decrypt_header(key_material, route, header)
        if detect_extension(decrypted):
            return safe_output_path(output_path, os.path.join("decoded", route)), derive_cipher(key_material, route), "decoded"
    extension = detect_extension(header)
    if extension:
        return safe_output_path(output_path, os.path.join("decoded", fallback + extension)), None, "open-media"
    return safe_output_path(output_path, os.path.join("protected", fallback + ".dat")), None, status


def extract_archive(path, output_path, routes, key_material, manifest, progress):
    archive = os.path.splitext(os.path.basename(path))[0]
    entries = read_spak_entries(path)
    with open(path, "rb") as stream:
        for identifier, offset, size, flags in entries:
            stream.seek(offset)
            header = stream.read(min(size, 64))
            stream.seek(offset)
            fallback = os.path.join("archives", archive, identifier)
            destination, cipher, status = choose_destination(
                output_path, routes.get(identifier), fallback, header, key_material, "protected-or-unknown"
            )
            destination, renamed = unique_path(destination)
            write_stream(stream, destination, size, cipher)
            append_manifest(
                manifest, os.path.basename(path), identifier, relative_output(output_path, destination), size, flags, status
            )
            progress.record(size, renamed)


def extract_external(path, root_path, output_path, routes, key_material, manifest, progress):
    relative = os.path.relpath(path, root_path)
    identifier = os.path.splitext(os.path.basename(path))[0].lower()
    size = os.path.getsize(path)
    with open(path, "rb") as stream:
        header = stream.read(min(size, 64))
        stream.seek(0)
        fallback = os.path.join("external", relative)
        if fallback.lower().endswith(".dat"):
            fallback = fallback[:-4]
        destination, cipher, status = choose_destination(
            output_path, routes.get(identifier), fallback, header, key_material, "protected-or-unknown"
        )
        destination, renamed = unique_path(destination)
        write_stream(stream, destination, size, cipher)
    append_manifest(manifest, "external", relative, relative_output(output_path, destination), size, "-", status)
    progress.record(size, renamed)


class Progress:
    def __init__(self, total):
        self.total = total
        self.processed = 0
        self.bytes = 0
        self.renamed = 0

    def record(self, size, renamed):
        self.processed += 1
        self.bytes += size
        self.renamed += int(renamed)
        print("PROGRESS:{}:{}:{}".format(self.processed, self.total, self.bytes), flush=True)


def main():
    game_path = os.environ.get("GAME_PATH", "")
    output_path = os.environ.get("OUTPUT_PATH", "")
    if not game_path or not output_path:
        print("ERROR: GAME_PATH and OUTPUT_PATH must be set.", file=sys.stderr)
        return 2

    game_path = os.path.abspath(game_path)
    root_path = game_path if os.path.isdir(game_path) else os.path.dirname(game_path)
    output_path = os.path.abspath(output_path)
    os.makedirs(output_path, exist_ok=True)
    dat_files = find_dat_files(game_path, output_path)
    archives = [path for path in dat_files if is_spak(path)]
    external = [path for path in dat_files if path not in archives]
    archive_entries = {path: read_spak_entries(path) for path in archives}
    available_ids = {
        identifier
        for entries in archive_entries.values()
        for identifier, _, _, _ in entries
    }
    available_ids.update(
        os.path.splitext(os.path.basename(path))[0].lower()
        for path in external
        if re.fullmatch(r"[0-9a-fA-F]{16}\.dat", os.path.basename(path))
    )

    executable, executable_data, key_material = find_spite_executable(root_path)
    routes = extract_resource_routes(executable_data, available_ids) if executable_data else {}
    total = sum(len(entries) for entries in archive_entries.values()) + len(external)
    progress = Progress(total)
    print("TOTAL:{}".format(total), flush=True)
    print("SPITE:executable={}:routes={}:available={}".format(
        os.path.basename(executable) if executable else "not-found", len(routes), len(available_ids)
    ), flush=True)
    manifest = [
        "Game Asset Tool SPAK DAT / SPITE manifest",
        "Decoded resources use runtime paths recovered from the embedded Tauri frontend bundle.",
        "Blocks without a recovered runtime path remain under protected/ as .dat files.",
        "",
        "Source | Entry | Output | Bytes | Flags | Status",
    ]
    errors = 0
    for archive in archives:
        try:
            extract_archive(archive, output_path, routes, key_material, manifest, progress)
        except Exception as error:
            errors += 1
            manifest.append("{} | ERROR | {}".format(os.path.basename(archive), error))
    for path in external:
        try:
            extract_external(path, root_path, output_path, routes, key_material, manifest, progress)
        except Exception as error:
            errors += 1
            manifest.append("external | {} | ERROR | {}".format(os.path.basename(path), error))

    with open(os.path.join(output_path, "SPAK-DAT-manifest.txt"), "w", encoding="utf-8") as stream:
        stream.write("\n".join(manifest) + "\n")
    print("RESULT:{}:{}:{}:{}".format(progress.processed, progress.bytes, errors, progress.renamed), flush=True)
    return 0 if errors == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
