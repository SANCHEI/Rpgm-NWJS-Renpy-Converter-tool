import os
import sys

from pyuepak import PakFile


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


def extract_archive(archive, output_path, optional_key):
    archive_output = os.path.join(output_path, "archives", normalize_name(os.path.splitext(os.path.basename(archive))[0]))
    pak = PakFile()
    if optional_key:
        pak.set_key(optional_key)
    pak.read(archive)

    extracted = 0
    byte_count = 0
    renamed = 0
    for filename in pak.list_files():
        data = pak.read_file(filename)
        destination = safe_output_path(archive_output, filename)
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
    optional_key = os.environ.get("OPTIONAL_KEY", "").strip()
    os.makedirs(output_path, exist_ok=True)
    archives, utocs = find_files(game_path, output_path)
    total = len(archives) + len(utocs)
    print("TOTAL:{}".format(total))

    extracted = 0
    byte_count = 0
    errors = 0
    renamed = 0
    processed = 0
    for archive in archives:
        try:
            print("Processing Unreal PAK: {}".format(os.path.relpath(archive, game_path)))
            current_extracted, current_bytes, current_renamed = extract_archive(archive, output_path, optional_key)
            extracted += current_extracted
            byte_count += current_bytes
            renamed += current_renamed
        except Exception as error:
            errors += 1
            print("WARN:{}: {}".format(os.path.basename(archive), error))
        processed += 1
        print("PROGRESS:{}:{}:{}".format(processed, total, byte_count))

    for archive in utocs:
        errors += 1
        processed += 1
        print("WARN:{}: Unreal IoStore (.utoc/.ucas) is not supported yet.".format(os.path.basename(archive)))
        print("PROGRESS:{}:{}:{}".format(processed, total, byte_count))

    print("RESULT:{}:{}:{}:{}".format(extracted, byte_count, errors, renamed))
    return 0 if total else 1


if __name__ == "__main__":
    sys.exit(main())
