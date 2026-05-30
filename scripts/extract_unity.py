# -*- coding: utf-8 -*-
from __future__ import print_function

import io
import os
import shutil
import sys
import threading
from concurrent.futures import ThreadPoolExecutor, as_completed

if sys.version_info[0] >= 3:
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding="utf-8", errors="replace")

import UnityPy


GAME_PATH = os.environ.get("GAME_PATH", "")
OUTPUT_PATH = os.environ.get("OUTPUT_PATH", "")
EXTRACT_MODE = os.environ.get("EXTRACT_MODE", "all")
INCLUDE_BUNDLES = os.environ.get("INCLUDE_BUNDLES", "1") == "1"
MAX_WORKERS = max(1, min(int(os.environ.get("MAX_WORKERS", "4")), 8))

DIRECT_EXTENSIONS = (
    ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tga", ".tiff",
    ".mp4", ".webm", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".3gp",
    ".ogg", ".wav", ".mp3", ".flac",
)
TEXTURE_TYPES = ("Texture2D", "Sprite", "Cubemap", "Texture3D", "Texture2DArray")

reserved_paths = set()
path_lock = threading.Lock()


def log(message):
    print(message)
    sys.stdout.flush()


def safe_component(value, fallback):
    cleaned = "".join(c for c in str(value or "") if c.isalnum() or c in "._- ")
    cleaned = cleaned.strip(" .")
    return cleaned or fallback


def unique_path(path):
    base, ext = os.path.splitext(path)
    candidate = path
    suffix = 2
    with path_lock:
        while candidate.lower() in reserved_paths or os.path.exists(candidate):
            candidate = "{0} ({1}){2}".format(base, suffix, ext)
            suffix += 1
        reserved_paths.add(candidate.lower())
    return candidate, candidate != path


def ensure_parent(path):
    parent = os.path.dirname(path)
    if parent:
        os.makedirs(parent, exist_ok=True)


def archive_output_dir(file_path):
    relative = os.path.relpath(file_path, GAME_PATH)
    stem = os.path.splitext(relative)[0]
    parts = [safe_component(part, "archive") for part in stem.replace("\\", "/").split("/")]
    return os.path.join(OUTPUT_PATH, "archives", *parts)


def resolve_resource(file_path, source):
    if not source:
        return ""
    source = str(source).replace("\\", os.sep).replace("/", os.sep)
    if source.startswith("archive:" + os.sep):
        source = source[len("archive:" + os.sep):]
    if os.path.isabs(source):
        return source
    return os.path.normpath(os.path.join(os.path.dirname(file_path), source))


def read_streamed_resource(file_path, resource):
    if not resource or not hasattr(resource, "m_Source"):
        return None
    source_file = resolve_resource(file_path, resource.m_Source)
    if not source_file or not os.path.exists(source_file):
        return None
    with open(source_file, "rb") as stream:
        stream.seek(resource.m_Offset)
        return stream.read(resource.m_Size)


def save_bytes(path, data):
    path, renamed = unique_path(path)
    ensure_parent(path)
    with open(path, "wb") as output:
        output.write(data)
    return len(data), int(renamed)


def save_image(path, image):
    path, renamed = unique_path(path)
    ensure_parent(path)
    image.save(path)
    return os.path.getsize(path), int(renamed)


def export_mesh(path, mesh):
    path, renamed = unique_path(path)
    ensure_parent(path)
    mesh.export(path)
    return os.path.getsize(path), int(renamed)


def extract_archive(file_path):
    extracted = 0
    total_size = 0
    errors = 0
    renamed = 0

    try:
        env = UnityPy.load(file_path)
        output_dir = archive_output_dir(file_path)
        os.makedirs(output_dir, exist_ok=True)

        for index, obj in enumerate(env.objects):
            try:
                obj_type = obj.type.name
                data = obj.read()

                if EXTRACT_MODE in ("textures", "all") and obj_type in TEXTURE_TYPES:
                    name = getattr(data, "name", None) or getattr(data, "m_Name", None)
                    image = getattr(data, "image", None)
                    if image:
                        size, collision = save_image(
                            os.path.join(output_dir, safe_component(name, "texture_{0}".format(index)) + ".png"),
                            image,
                        )
                        extracted += 1
                        total_size += size
                        renamed += collision

                elif EXTRACT_MODE in ("videos", "all") and obj_type == "VideoClip":
                    name = getattr(data, "m_Name", None)
                    video_data = read_streamed_resource(file_path, getattr(data, "m_ExternalResources", None))
                    if not video_data:
                        video_data = getattr(data, "video_data", None)
                    if video_data:
                        ext = ".mp4"
                        resource = getattr(data, "m_ExternalResources", None)
                        original = getattr(resource, "m_OriginalPath", "") if resource else ""
                        if original:
                            ext = os.path.splitext(original)[1] or ext
                        size, collision = save_bytes(
                            os.path.join(output_dir, safe_component(name, "video_{0}".format(index)) + ext),
                            video_data,
                        )
                        extracted += 1
                        total_size += size
                        renamed += collision

                elif EXTRACT_MODE in ("audios", "all") and obj_type == "AudioClip":
                    name = getattr(data, "name", None) or getattr(data, "m_Name", None)
                    audio_data = read_streamed_resource(file_path, getattr(data, "m_Resource", None))
                    if not audio_data:
                        audio_data = getattr(data, "audio_data", None) or getattr(data, "m_AudioData", None)
                    if audio_data:
                        size, collision = save_bytes(
                            os.path.join(output_dir, safe_component(name, "audio_{0}".format(index)) + ".wav"),
                            audio_data,
                        )
                        extracted += 1
                        total_size += size
                        renamed += collision

                elif EXTRACT_MODE in ("meshes", "all") and obj_type == "Mesh":
                    name = getattr(data, "m_Name", None)
                    size, collision = export_mesh(
                        os.path.join(output_dir, safe_component(name, "mesh_{0}".format(index)) + ".obj"),
                        data,
                    )
                    extracted += 1
                    total_size += size
                    renamed += collision

            except Exception as error:
                errors += 1
                log("WARN:{0}:{1}".format(os.path.basename(file_path), error))

    except Exception as error:
        errors += 1
        log("WARN:{0}:{1}".format(os.path.basename(file_path), error))

    return extracted, total_size, errors, renamed


def collect_files():
    archives = []
    direct_files = []
    for root, _, files in os.walk(GAME_PATH):
        if os.path.abspath(root).startswith(os.path.abspath(OUTPUT_PATH)):
            continue
        for filename in files:
            full_path = os.path.join(root, filename)
            lower = filename.lower()
            if lower.endswith(".bundle"):
                if INCLUDE_BUNDLES:
                    archives.append(full_path)
            elif lower.endswith(".assets"):
                archives.append(full_path)
            elif lower.endswith(DIRECT_EXTENSIONS):
                direct_files.append(full_path)
    return sorted(set(archives)), sorted(set(direct_files))


def copy_direct_files(files):
    extracted = 0
    total_size = 0
    errors = 0
    renamed = 0
    for source in files:
        try:
            relative = os.path.relpath(source, GAME_PATH)
            destination, collision = unique_path(os.path.join(OUTPUT_PATH, "direct", relative))
            ensure_parent(destination)
            shutil.copy2(source, destination)
            extracted += 1
            total_size += os.path.getsize(destination)
            renamed += int(collision)
        except Exception as error:
            errors += 1
            log("WARN:{0}:{1}".format(source, error))
    return extracted, total_size, errors, renamed


def main():
    if not GAME_PATH or not OUTPUT_PATH:
        log("ERROR:GAME_PATH or OUTPUT_PATH is missing")
        return 2

    os.makedirs(OUTPUT_PATH, exist_ok=True)
    archives, direct_files = collect_files()
    log("TOTAL:{0}".format(len(archives)))
    log("DIRECT:{0}".format(len(direct_files)))

    total_extracted, total_size, total_errors, total_renamed = copy_direct_files(direct_files)
    processed = 0

    with ThreadPoolExecutor(max_workers=MAX_WORKERS) as executor:
        futures = [executor.submit(extract_archive, path) for path in archives]
        for future in as_completed(futures):
            extracted, size, errors, renamed = future.result()
            total_extracted += extracted
            total_size += size
            total_errors += errors
            total_renamed += renamed
            processed += 1
            log("PROGRESS:{0}:{1}:{2}".format(processed, len(archives), total_size))

    log("RESULT:{0}:{1}:{2}:{3}".format(total_extracted, total_size, total_errors, total_renamed))
    return 0 if total_errors == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
