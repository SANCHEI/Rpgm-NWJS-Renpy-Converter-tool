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
MAX_WORKERS = max(1, min(int(os.environ.get("MAX_WORKERS", "4")), 12))
PNG_COMPRESSION_LEVEL = max(0, min(int(os.environ.get("PNG_COMPRESSION_LEVEL", "1")), 9))
MAX_WARNINGS = int(os.environ.get("MAX_WARNINGS", "120"))
SAVE_WORKERS = max(1, min(int(os.environ.get("SAVE_WORKERS", "2")), 4))
SAVE_BACKLOG = max(SAVE_WORKERS * 4, 4)

IMAGE_EXTENSIONS = (".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tga", ".tiff")
VIDEO_EXTENSIONS = (".mp4", ".webm", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".3gp")
AUDIO_EXTENSIONS = (".ogg", ".wav", ".mp3", ".flac")
DIRECT_EXTENSIONS = IMAGE_EXTENSIONS + VIDEO_EXTENSIONS + AUDIO_EXTENSIONS
TEXTURE_TYPES = ("Texture2D", "Sprite", "Cubemap", "Texture3D", "Texture2DArray")
MODE_TEXTURES = EXTRACT_MODE in ("textures", "media", "all")
MODE_VIDEOS = EXTRACT_MODE in ("videos", "media", "all")
MODE_AUDIOS = EXTRACT_MODE in ("audios", "all")
MODE_MESHES = EXTRACT_MODE in ("meshes", "all")

reserved_paths = set()
path_lock = threading.Lock()
warning_lock = threading.Lock()
warning_count = 0
warning_suppressed = False


def log(message):
    print(message)
    sys.stdout.flush()


def log_warn(message):
    global warning_count, warning_suppressed
    if MAX_WARNINGS < 0:
        log(message)
        return
    with warning_lock:
        if warning_count < MAX_WARNINGS:
            warning_count += 1
            output = message
        elif not warning_suppressed:
            warning_suppressed = True
            output = "WARN:Unity warnings limit reached; additional warnings are counted but hidden."
        else:
            return
    log(output)


def safe_component(value, fallback):
    cleaned = "".join(c for c in str(value or "") if c.isalnum() or c in "._- ")
    cleaned = cleaned.strip(" .")
    return cleaned or fallback


def safe_getattr(value, name, fallback=None):
    try:
        return getattr(value, name, fallback)
    except Exception:
        return fallback


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
    if not source_file or not os.path.isfile(source_file):
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
    image.save(path, "PNG", compress_level=PNG_COMPRESSION_LEVEL, optimize=False)
    return os.path.getsize(path), int(renamed)


def export_mesh(path, mesh):
    path, renamed = unique_path(path)
    ensure_parent(path)
    with open(path, "w", encoding="utf-8") as output:
        output.write(mesh.export("obj"))
    return os.path.getsize(path), int(renamed)


def extract_archive(file_path):
    extracted = 0
    total_size = 0
    errors = 0
    renamed = 0
    skipped = 0
    object_count = 0
    pending_saves = []
    save_pool = ThreadPoolExecutor(max_workers=SAVE_WORKERS) if (MODE_TEXTURES or MODE_VIDEOS or MODE_AUDIOS) else None

    def drain_saves(force=False):
        nonlocal extracted, total_size, errors, renamed
        while pending_saves and (force or len(pending_saves) >= SAVE_BACKLOG):
            future = pending_saves.pop(0)
            try:
                size, collision = future.result()
                extracted += 1
                total_size += size
                renamed += collision
            except Exception as error:
                errors += 1
                log_warn("WARN:{0}:{1}".format(os.path.basename(file_path), error))

    try:
        env = UnityPy.load(file_path)
        try:
            object_count = len(env.objects)
        except Exception:
            object_count = 0
        output_dir = archive_output_dir(file_path)
        os.makedirs(output_dir, exist_ok=True)

        for index, obj in enumerate(env.objects):
            obj_type = obj.type.name
            supported = (
                (MODE_TEXTURES and obj_type in TEXTURE_TYPES)
                or (MODE_VIDEOS and obj_type == "VideoClip")
                or (MODE_AUDIOS and obj_type == "AudioClip")
                or (MODE_MESHES and obj_type == "Mesh")
            )
            if not supported:
                skipped += 1
                continue

            try:
                data = obj.read()

                if MODE_TEXTURES and obj_type in TEXTURE_TYPES:
                    name = getattr(data, "name", None) or getattr(data, "m_Name", None)
                    image = safe_getattr(data, "image")
                    if image:
                        image.load()
                        pending_saves.append(
                            save_pool.submit(
                                save_image,
                                os.path.join(output_dir, safe_component(name, "texture_{0}".format(index)) + ".png"),
                                image,
                            )
                        )
                        drain_saves()
                    else:
                        skipped += 1

                elif MODE_VIDEOS and obj_type == "VideoClip":
                    name = getattr(data, "m_Name", None)
                    video_data = read_streamed_resource(file_path, getattr(data, "m_ExternalResources", None))
                    if not video_data:
                        video_data = safe_getattr(data, "video_data")
                    if video_data:
                        ext = ".mp4"
                        resource = getattr(data, "m_ExternalResources", None)
                        original = getattr(resource, "m_OriginalPath", "") if resource else ""
                        if original:
                            ext = os.path.splitext(original)[1] or ext
                        pending_saves.append(
                            save_pool.submit(
                                save_bytes,
                                os.path.join(output_dir, safe_component(name, "video_{0}".format(index)) + ext),
                                video_data,
                            )
                        )
                        drain_saves()
                    else:
                        skipped += 1

                elif MODE_AUDIOS and obj_type == "AudioClip":
                    name = getattr(data, "name", None) or getattr(data, "m_Name", None)
                    audio_data = read_streamed_resource(file_path, getattr(data, "m_Resource", None))
                    if not audio_data:
                        audio_data = safe_getattr(data, "audio_data") or safe_getattr(data, "m_AudioData")
                    if audio_data:
                        pending_saves.append(
                            save_pool.submit(
                                save_bytes,
                                os.path.join(output_dir, safe_component(name, "audio_{0}".format(index)) + ".wav"),
                                audio_data,
                            )
                        )
                        drain_saves()
                    else:
                        skipped += 1

                elif MODE_MESHES and obj_type == "Mesh":
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
                log_warn("WARN:{0}:{1}".format(os.path.basename(file_path), error))

    except Exception as error:
        errors += 1
        log_warn("WARN:{0}:{1}".format(os.path.basename(file_path), error))

    drain_saves(force=True)
    if save_pool:
        save_pool.shutdown(wait=True)

    return {
        "path": file_path,
        "extracted": extracted,
        "size": total_size,
        "errors": errors,
        "renamed": renamed,
        "skipped": skipped,
        "objects": object_count,
    }


def direct_file_supported(filename):
    lower = filename.lower()
    return (
        (MODE_TEXTURES and lower.endswith(IMAGE_EXTENSIONS))
        or (MODE_VIDEOS and lower.endswith(VIDEO_EXTENSIONS))
        or (MODE_AUDIOS and lower.endswith(AUDIO_EXTENSIONS))
    )


def collect_files():
    archives = []
    direct_files = []
    output_abs = os.path.abspath(OUTPUT_PATH)
    extracted_abs = os.path.abspath(os.path.join(GAME_PATH, "extracted"))
    for root, dirs, files in os.walk(GAME_PATH):
        root_abs = os.path.abspath(root)
        dirs[:] = [
            name for name in dirs
            if not os.path.abspath(os.path.join(root, name)).startswith(output_abs)
            and not os.path.abspath(os.path.join(root, name)).startswith(extracted_abs)
        ]
        if root_abs.startswith(output_abs) or root_abs.startswith(extracted_abs):
            continue
        for filename in files:
            full_path = os.path.join(root, filename)
            lower = filename.lower()
            if lower.endswith(".bundle"):
                if INCLUDE_BUNDLES:
                    archives.append(full_path)
            elif lower.endswith(".assets"):
                archives.append(full_path)
            elif lower.endswith(DIRECT_EXTENSIONS) and direct_file_supported(filename):
                direct_files.append(full_path)
    return sorted(set(archives)), sorted(set(direct_files))


def copy_direct_file(source):
    try:
        relative = os.path.relpath(source, GAME_PATH)
        destination, collision = unique_path(os.path.join(OUTPUT_PATH, "direct", relative))
        ensure_parent(destination)
        shutil.copy2(source, destination)
        return 1, os.path.getsize(destination), 0, int(collision)
    except Exception as error:
        log("WARN:{0}:{1}".format(source, error))
        return 0, 0, 1, 0


def copy_direct_files(files):
    extracted = 0
    total_size = 0
    errors = 0
    renamed = 0
    if not files:
        return extracted, total_size, errors, renamed
    processed = 0
    with ThreadPoolExecutor(max_workers=min(MAX_WORKERS, max(1, len(files)))) as executor:
        futures = [executor.submit(copy_direct_file, source) for source in files]
        for future in as_completed(futures):
            item_extracted, item_size, item_errors, item_renamed = future.result()
            extracted += item_extracted
            total_size += item_size
            errors += item_errors
            renamed += item_renamed
            processed += 1
            if processed == 1 or processed == len(files) or processed % 25 == 0:
                log("DIRECT_PROGRESS:{0}:{1}:{2}".format(processed, len(files), total_size))
    return extracted, total_size, errors, renamed


def format_bytes(value):
    value = float(max(0, value))
    units = ("B", "KB", "MB", "GB")
    unit = 0
    while value >= 1024.0 and unit < len(units) - 1:
        value /= 1024.0
        unit += 1
    if unit == 0:
        return "{0} {1}".format(int(value), units[unit])
    return "{0:.1f} {1}".format(value, units[unit])


def rel_path(path):
    try:
        return os.path.relpath(path, GAME_PATH).replace("\\", "/")
    except Exception:
        return path


def write_unity_diagnostics(archives, direct_files, archive_results):
    path = os.path.join(OUTPUT_PATH, "GameAssetTool-unity-diagnostics.txt")
    try:
        bundle_count = sum(1 for item in archives if item.lower().endswith(".bundle"))
        assets_count = sum(1 for item in archives if item.lower().endswith(".assets"))
        archive_input_size = sum(os.path.getsize(item) for item in archives if os.path.isfile(item))
        zero_output = [item for item in archive_results if item.get("extracted", 0) == 0]
        error_archives = [item for item in archive_results if item.get("errors", 0) > 0]
        largest = sorted(
            [item for item in archives if os.path.isfile(item)],
            key=lambda item: os.path.getsize(item),
            reverse=True,
        )[:8]

        with open(path, "w", encoding="utf-8") as output:
            output.write("Unity diagnostics\n")
            output.write("Mode: {0}\n".format(EXTRACT_MODE))
            output.write("Include bundles: {0}\n".format("yes" if INCLUDE_BUNDLES else "no"))
            output.write("Archives: {0}\n".format(len(archives)))
            output.write("Bundles: {0}\n".format(bundle_count))
            output.write("Assets: {0}\n".format(assets_count))
            output.write("Direct files: {0}\n".format(len(direct_files)))
            output.write("Archive input size: {0}\n".format(format_bytes(archive_input_size)))
            output.write("Archives with output: {0}\n".format(max(0, len(archives) - len(zero_output))))
            output.write("Archives with zero output: {0}\n".format(len(zero_output)))
            output.write("Archives with errors: {0}\n".format(len(error_archives)))
            output.write("Skipped Unity objects: {0}\n".format(sum(item.get("skipped", 0) for item in archive_results)))
            output.write("Warnings emitted: {0}\n".format(warning_count))
            if largest:
                output.write("Largest archives:\n")
                for item in largest:
                    output.write("- {0} ({1})\n".format(rel_path(item), format_bytes(os.path.getsize(item))))
            if zero_output:
                output.write("Zero-output archives:\n")
                for item in zero_output[:40]:
                    output.write("- {0} | objects={1} | skipped={2} | errors={3}\n".format(
                        rel_path(item.get("path", "")),
                        item.get("objects", 0),
                        item.get("skipped", 0),
                        item.get("errors", 0),
                    ))
                if len(zero_output) > 40:
                    output.write("- ... {0} more\n".format(len(zero_output) - 40))
    except Exception as error:
        log_warn("WARN:Unity diagnostics failed:{0}".format(error))


def main():
    if not GAME_PATH or not OUTPUT_PATH:
        log("ERROR:GAME_PATH or OUTPUT_PATH is missing")
        return 2

    os.makedirs(OUTPUT_PATH, exist_ok=True)
    log("PHASE:scan")
    archives, direct_files = collect_files()
    log("TOTAL:{0}".format(len(archives)))
    log("DIRECT:{0}".format(len(direct_files)))

    log("PHASE:direct")
    total_extracted, total_size, total_errors, total_renamed = copy_direct_files(direct_files)
    total_skipped = 0
    processed = 0
    archive_results = []

    log("PHASE:archives")
    with ThreadPoolExecutor(max_workers=MAX_WORKERS) as executor:
        futures = [executor.submit(extract_archive, path) for path in archives]
        for future in as_completed(futures):
            result = future.result()
            archive_results.append(result)
            extracted = result.get("extracted", 0)
            size = result.get("size", 0)
            errors = result.get("errors", 0)
            renamed = result.get("renamed", 0)
            skipped = result.get("skipped", 0)
            total_extracted += extracted
            total_size += size
            total_errors += errors
            total_renamed += renamed
            total_skipped += skipped
            processed += 1
            log("PROGRESS:{0}:{1}:{2}".format(processed, len(archives), total_size))

    write_unity_diagnostics(archives, direct_files, archive_results)
    log("RESULT:{0}:{1}:{2}:{3}:{4}".format(total_extracted, total_size, total_errors, total_renamed, total_skipped))
    return 0 if total_errors == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
