import bz2
import mmap
import os
import shutil
import struct
import sys

from PIL import Image


PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
PNG_END = b"IEND"
IMAGE_EXTENSIONS = {
    ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".svg", ".ico", ".tif", ".tiff"
}
MAX_TEXTURE_BYTES = 512 * 1024 * 1024
MAX_PIXELS = 100_000_000


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
    if os.path.isfile(game_path):
        return [os.path.abspath(game_path)]
    output_path = os.path.abspath(output_path)
    matches = []
    for root, directories, files in os.walk(game_path):
        directories[:] = [
            name for name in directories
            if not os.path.abspath(os.path.join(root, name)).startswith(output_path + os.sep)
            and name.lower() != "extracted"
        ]
        for name in files:
            lowered = name.lower()
            if lowered == "data.win" or lowered.startswith("audiogroup") and lowered.endswith(".dat"):
                matches.append(os.path.join(root, name))
    return sorted(set(matches))


def copy_original(source, output_path):
    destination = safe_output_path(output_path, os.path.join("originals", os.path.basename(source)))
    destination, renamed = unique_path(destination)
    os.makedirs(os.path.dirname(destination), exist_ok=True)
    shutil.copy2(source, destination)
    return os.path.getsize(destination), int(renamed)


def png_size(data, offset):
    if data[offset:offset + len(PNG_SIGNATURE)] != PNG_SIGNATURE:
        return 0
    position = offset + len(PNG_SIGNATURE)
    chunks = 0
    while position + 12 <= len(data) and chunks < 100000:
        size = struct.unpack_from(">I", data, position)[0]
        chunk_type = data[position + 4:position + 8]
        if size > 256 * 1024 * 1024 or position + 12 + size > len(data):
            return 0
        position += 12 + size
        chunks += 1
        if chunk_type == PNG_END:
            return position - offset if size == 0 else 0
    return 0


def sign_extend(value, bits):
    sign = 1 << (bits - 1)
    return value - (1 << bits) if value & sign else value


def decode_fioq(data):
    if len(data) < 12 or data[:4] != b"fioq":
        raise ValueError("GameMaker QOI header was not found.")
    width, height = struct.unpack_from("<HH", data, 4)
    encoded_size = struct.unpack_from("<I", data, 8)[0]
    if not width or not height or width * height > MAX_PIXELS:
        raise ValueError("GameMaker QOI dimensions are invalid.")
    if encoded_size > MAX_TEXTURE_BYTES or 12 + encoded_size > len(data):
        raise ValueError("GameMaker QOI payload is truncated or too large.")

    chunks = memoryview(data)[12:12 + encoded_size]
    index = [[0, 0, 0, 0] for _ in range(64)]
    pixels = bytearray(width * height * 4)
    red = green = blue = 0
    alpha = 255
    position = 0
    run = 0
    for pixel in range(width * height):
        if run:
            run -= 1
        else:
            if position >= len(chunks):
                raise ValueError("GameMaker QOI pixel stream is truncated.")
            first = chunks[position]
            position += 1
            if first & 0xC0 == 0x00:
                red, green, blue, alpha = index[first & 0x3F]
            elif first & 0xE0 == 0x40:
                run = first & 0x1F
            elif first & 0xE0 == 0x60:
                if position >= len(chunks):
                    raise ValueError("GameMaker QOI run is truncated.")
                run = ((first & 0x1F) << 8 | chunks[position]) + 32
                position += 1
            elif first & 0xC0 == 0x80:
                red = (red + sign_extend((first >> 4) & 0x03, 2)) & 0xFF
                green = (green + sign_extend((first >> 2) & 0x03, 2)) & 0xFF
                blue = (blue + sign_extend(first & 0x03, 2)) & 0xFF
            elif first & 0xE0 == 0xC0:
                if position >= len(chunks):
                    raise ValueError("GameMaker QOI diff is truncated.")
                merged = first << 8 | chunks[position]
                position += 1
                red = (red + sign_extend((merged >> 8) & 0x1F, 5)) & 0xFF
                green = (green + sign_extend((merged >> 4) & 0x0F, 4)) & 0xFF
                blue = (blue + sign_extend(merged & 0x0F, 4)) & 0xFF
            elif first & 0xF0 == 0xE0:
                if position + 2 > len(chunks):
                    raise ValueError("GameMaker QOI alpha diff is truncated.")
                merged = first << 16 | chunks[position] << 8 | chunks[position + 1]
                position += 2
                red = (red + sign_extend((merged >> 15) & 0x1F, 5)) & 0xFF
                green = (green + sign_extend((merged >> 10) & 0x1F, 5)) & 0xFF
                blue = (blue + sign_extend((merged >> 5) & 0x1F, 5)) & 0xFF
                alpha = (alpha + sign_extend(merged & 0x1F, 5)) & 0xFF
            elif first & 0xF0 == 0xF0:
                if first & 8:
                    red = chunks[position]
                    position += 1
                if first & 4:
                    green = chunks[position]
                    position += 1
                if first & 2:
                    blue = chunks[position]
                    position += 1
                if first & 1:
                    alpha = chunks[position]
                    position += 1
            slot = (red ^ green ^ blue ^ alpha) & 63
            index[slot] = [red, green, blue, alpha]
        target = pixel * 4
        pixels[target:target + 4] = bytes((red, green, blue, alpha))
    return width, height, bytes(pixels), 12 + encoded_size


def decode_bz2qoi(data):
    if len(data) < 14 or data[:4] != b"2zoq":
        raise ValueError("GameMaker BZ2QOI header was not found.")
    header_size = 12 if data[12:14] == b"BZ" else 8
    decoder = bz2.BZ2Decompressor()
    raw = decoder.decompress(data[header_size:], MAX_TEXTURE_BYTES)
    if not decoder.eof:
        raise ValueError("GameMaker BZ2QOI payload is truncated or too large.")
    width, height, pixels, _ = decode_fioq(raw)
    consumed = len(data[header_size:]) - len(decoder.unused_data)
    return width, height, pixels, header_size + consumed


def save_texture(output_path, folder, index, image, raw_extension=None, raw_data=None):
    destination = safe_output_path(output_path, os.path.join(folder, "texture-page-{:04d}.png".format(index)))
    destination, renamed = unique_path(destination)
    os.makedirs(os.path.dirname(destination), exist_ok=True)
    try:
        image.save(destination, "PNG")
    finally:
        image.close()
    byte_count = os.path.getsize(destination)
    if raw_extension and raw_data:
        raw_path = safe_output_path(output_path, os.path.join(folder, "texture-page-{:04d}{}".format(index, raw_extension)))
        raw_path, raw_renamed = unique_path(raw_path)
        with open(raw_path, "wb") as output:
            output.write(raw_data)
        return byte_count + len(raw_data), int(renamed) + int(raw_renamed), 2
    return byte_count, int(renamed), 1


def extract_textures(source, output_path):
    extracted = byte_count = renamed = skipped = 0
    folder = os.path.join("embedded", os.path.splitext(os.path.basename(source))[0])
    with open(source, "rb") as stream:
        with mmap.mmap(stream.fileno(), 0, access=mmap.ACCESS_READ) as data:
            position = 0
            while position < len(data):
                candidates = [offset for offset in (
                    data.find(PNG_SIGNATURE, position),
                    data.find(b"fioq", position),
                    data.find(b"2zoq", position),
                ) if offset >= 0]
                if not candidates:
                    break
                offset = min(candidates)
                try:
                    if data[offset:offset + 8] == PNG_SIGNATURE:
                        length = png_size(data, offset)
                        if not length:
                            raise ValueError("Invalid embedded PNG.")
                        raw = data[offset:offset + length]
                        destination = safe_output_path(output_path, os.path.join(folder, "texture-page-{:04d}.png".format(extracted + 1)))
                        destination, was_renamed = unique_path(destination)
                        os.makedirs(os.path.dirname(destination), exist_ok=True)
                        with open(destination, "wb") as output:
                            output.write(raw)
                        byte_count += len(raw)
                        renamed += int(was_renamed)
                        extracted += 1
                    elif data[offset:offset + 4] == b"fioq":
                        raw_size = 12 + struct.unpack_from("<I", data, offset + 8)[0]
                        raw = data[offset:offset + raw_size]
                        width, height, pixels, length = decode_fioq(raw)
                        saved, collisions, count = save_texture(
                            output_path, folder, extracted + 1, Image.frombytes("RGBA", (width, height), pixels), ".qoi", raw[:length]
                        )
                        byte_count += saved
                        renamed += collisions
                        extracted += count
                    else:
                        width, height, pixels, length = decode_bz2qoi(data[offset:])
                        raw = data[offset:offset + length]
                        saved, collisions, count = save_texture(
                            output_path, folder, extracted + 1, Image.frombytes("RGBA", (width, height), pixels), ".bz2qoi", raw
                        )
                        byte_count += saved
                        renamed += collisions
                        extracted += count
                    position = offset + max(length, 1)
                except Exception as error:
                    skipped += 1
                    print("WARN:{}@{}: {}".format(os.path.basename(source), offset, error))
                    position = offset + 1
    return extracted, byte_count, renamed, skipped


def copy_external_images(game_path, output_path):
    if os.path.isfile(game_path):
        return 0, 0, 0, 0
    extracted = byte_count = renamed = skipped = 0
    output_path = os.path.abspath(output_path)
    for root, directories, files in os.walk(game_path):
        directories[:] = [
            name for name in directories
            if name.lower() != "extracted"
            and not os.path.abspath(os.path.join(root, name)).startswith(output_path + os.sep)
        ]
        for name in files:
            source = os.path.join(root, name)
            if os.path.splitext(name)[1].lower() not in IMAGE_EXTENSIONS:
                continue
            try:
                relative = os.path.relpath(source, game_path)
                destination = safe_output_path(output_path, os.path.join("external", relative))
                destination, was_renamed = unique_path(destination)
                os.makedirs(os.path.dirname(destination), exist_ok=True)
                shutil.copy2(source, destination)
                extracted += 1
                byte_count += os.path.getsize(destination)
                renamed += int(was_renamed)
            except Exception:
                skipped += 1
    return extracted, byte_count, renamed, skipped


def main():
    game_path = os.environ["GAME_PATH"]
    output_path = os.environ["OUTPUT_PATH"]
    os.makedirs(output_path, exist_ok=True)
    files = find_files(game_path, output_path)
    print("TOTAL:{}".format(len(files)))

    extracted = byte_count = errors = renamed = skipped = 0
    for index, source in enumerate(files, 1):
        try:
            print("Processing GameMaker container: {}".format(os.path.relpath(source, game_path)))
            original_bytes, original_renamed = copy_original(source, output_path)
            extracted += 1
            byte_count += original_bytes
            renamed += original_renamed
            current = extract_textures(source, output_path)
            extracted += current[0]
            byte_count += current[1]
            renamed += current[2]
            skipped += current[3]
        except Exception as error:
            errors += 1
            print("WARN:{}: {}".format(os.path.basename(source), error))
        print("PROGRESS:{}:{}:{}".format(index, len(files), byte_count))

    external = copy_external_images(game_path, output_path)
    extracted += external[0]
    byte_count += external[1]
    renamed += external[2]
    skipped += external[3]
    print("SKIPPED:{}".format(skipped))
    print("RESULT:{}:{}:{}:{}".format(extracted, byte_count, errors, renamed))
    return 0 if files else 1


if __name__ == "__main__":
    sys.exit(main())
