# -*- coding: utf-8 -*-
from __future__ import print_function

import io
import json
import os
import shutil
import sys
import threading
from collections import Counter
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
TEXT_EXTENSIONS = (
    ".txt", ".json", ".xml", ".csv", ".tsv", ".ini", ".cfg", ".conf", ".yaml", ".yml",
    ".po", ".pot", ".mo", ".tmx", ".strings", ".lang", ".loc", ".rpy", ".rpym", ".ks",
    ".js", ".css", ".html", ".htm", ".bytes"
)
DIRECT_EXTENSIONS = IMAGE_EXTENSIONS + VIDEO_EXTENSIONS + AUDIO_EXTENSIONS + TEXT_EXTENSIONS
ARCHIVE_EXTENSIONS = (".assets", ".bundle", ".unity3d")
OPTIONAL_BUNDLE_EXTENSIONS = (".bundle", ".unity3d")
UNITY_BUNDLE_SIGNATURES = (b"UnityFS", b"UnityWeb", b"UnityRaw")
UNITY_BUNDLE_SCAN_BYTES = 1024 * 1024
ADDRESSABLE_HINTS = (
    "/streamingassets/aa/",
    "\\streamingassets\\aa\\",
    "/assetbundles/",
    "\\assetbundles\\",
    "/bundles/",
    "\\bundles\\",
)
SKIP_DIR_NAMES = ("bepinex", "dotnet", "mono", "crashpad", "logs")
TEXTURE_TYPES = ("Texture2D", "Sprite", "Cubemap", "Texture3D", "Texture2DArray")
MODE_TEXTURES = EXTRACT_MODE in ("textures", "media", "media-audio", "textures-text", "all")
MODE_VIDEOS = EXTRACT_MODE in ("videos", "media", "media-audio", "all")
MODE_AUDIOS = EXTRACT_MODE in ("audios", "media-audio", "all")
MODE_TEXT = EXTRACT_MODE in ("text", "textures-text", "all")
MODE_MESHES = EXTRACT_MODE in ("meshes", "all")
MODE_ANIMATIONS = EXTRACT_MODE in ("animations", "all")

reserved_paths = set()
path_lock = threading.Lock()
warning_lock = threading.Lock()
warning_count = 0
warning_suppressed = False
embedded_offset_cache = {}
addressable_labels = {}

OBJECT_EXPORT_HINTS = {
    "Texture2D": "Images only / Images + Video / Images + Video + Audio / Everything",
    "Sprite": "Images only / Images + Video / Images + Video + Audio / Everything",
    "Cubemap": "Images only / Images + Video / Images + Video + Audio / Everything",
    "Texture3D": "Images only / Images + Video / Images + Video + Audio / Everything",
    "Texture2DArray": "Images only / Images + Video / Images + Video + Audio / Everything",
    "VideoClip": "Images + Video / Images + Video + Audio / Everything",
    "AudioClip": "Images + Video + Audio / Everything",
    "TextAsset": "Text only / Images + Text / Everything",
    "Mesh": "Everything",
    "AnimationClip": "Everything",
}
INTERNAL_OBJECT_TYPES = set((
    "GameObject", "Transform", "RectTransform", "MonoBehaviour", "MonoScript", "Material", "Shader",
    "MeshRenderer", "SkinnedMeshRenderer", "MeshFilter", "Animator", "Animation", "AudioSource",
    "Canvas", "CanvasRenderer", "Camera", "Light", "ParticleSystem", "SpriteRenderer",
))


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


def short_component(value, fallback, limit=72):
    cleaned = safe_component(value, fallback)
    if len(cleaned) <= limit:
        return cleaned
    return cleaned[:limit].rstrip(" ._-") or fallback


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
    if parts:
        key = os.path.splitext(os.path.basename(file_path))[0].lower()
        label = addressable_labels.get(key)
        if label:
            parts[-1] = short_component(parts[-1] + "__" + label, parts[-1])
    return os.path.join(OUTPUT_PATH, "archives", *parts)


def cache_dir():
    return os.path.join(OUTPUT_PATH, "diagnostics", "cache")


def embedded_cache_path():
    return os.path.join(cache_dir(), "wrapped-unityfs-offsets.json")


def file_cache_key(path):
    try:
        stat = os.stat(path)
        relative = rel_path(path)
        mtime = getattr(stat, "st_mtime_ns", int(stat.st_mtime * 1000000000))
        return "{0}|{1}|{2}".format(relative, stat.st_size, mtime)
    except Exception:
        return rel_path(path)


def load_embedded_offset_cache():
    global embedded_offset_cache
    path = embedded_cache_path()
    if not os.path.isfile(path):
        embedded_offset_cache = {}
        return
    try:
        with open(path, "r", encoding="utf-8") as handle:
            data = json.load(handle)
        embedded_offset_cache = data if isinstance(data, dict) else {}
    except Exception:
        embedded_offset_cache = {}


def save_embedded_offset_cache():
    try:
        os.makedirs(cache_dir(), exist_ok=True)
        with open(embedded_cache_path(), "w", encoding="utf-8") as handle:
            json.dump(embedded_offset_cache, handle, ensure_ascii=False, indent=2, sort_keys=True)
    except Exception as error:
        log_warn("WARN:Unity cache save failed:{0}".format(error))


def flatten_json_strings(value, output):
    if isinstance(value, str):
        output.append(value)
    elif isinstance(value, list):
        for item in value:
            flatten_json_strings(item, output)
    elif isinstance(value, dict):
        for key, item in value.items():
            if isinstance(key, str):
                output.append(key)
            flatten_json_strings(item, output)


def looks_like_address_label(value):
    if not value:
        return False
    lower = value.lower()
    if lower.endswith((".bundle", ".hash", ".json")):
        return False
    if lower.startswith(("http://", "https://", "file://")):
        return False
    if len(value) > 96:
        return False
    return any(ch.isalpha() for ch in value)


def load_addressable_labels():
    global addressable_labels
    labels = {}
    catalogs = []
    try:
        for root, dirs, files in os.walk(GAME_PATH):
            lower = root.lower()
            if "streamingassets" not in lower and os.path.basename(root).lower() != "aa":
                continue
            if "catalog.json" in files:
                catalogs.append(os.path.join(root, "catalog.json"))
            if len(catalogs) >= 20:
                break
    except Exception:
        catalogs = []

    for catalog in catalogs:
        try:
            with open(catalog, "r", encoding="utf-8", errors="replace") as handle:
                data = json.load(handle)
            strings = []
            flatten_json_strings(data, strings)
            for index, text in enumerate(strings):
                lower = text.lower().replace("\\", "/")
                if ".bundle" not in lower:
                    continue
                bundle_name = os.path.basename(lower.split("?", 1)[0])
                key = os.path.splitext(bundle_name)[0].lower()
                if not key or key in labels:
                    continue
                window = strings[max(0, index - 8):min(len(strings), index + 9)]
                candidates = [item for item in window if item != text and looks_like_address_label(item)]
                if candidates:
                    labels[key] = short_component(candidates[0], key)
        except Exception:
            continue
    addressable_labels = labels


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


def text_asset_bytes(data):
    for attr in ("script", "m_Script"):
        value = safe_getattr(data, attr)
        if value is None:
            continue
        if isinstance(value, bytes):
            return value
        if isinstance(value, str):
            return value.encode("utf-8")
        try:
            return bytes(value)
        except Exception:
            continue
    return None


def is_likely_text_bytes(data):
    if not data:
        return True
    sample = data[:8192]
    if sample.startswith((b"\xef\xbb\xbf", b"\xff\xfe", b"\xfe\xff")):
        return True
    if b"\x00" in sample:
        return False
    try:
        sample.decode("utf-8")
        return True
    except Exception:
        pass
    printable = 0
    for item in sample:
        value = item if isinstance(item, int) else ord(item)
        if value in (9, 10, 13) or 32 <= value <= 126 or value >= 128:
            printable += 1
    return printable >= max(1, int(len(sample) * 0.90))


def text_asset_extension(name, data):
    original_ext = os.path.splitext(str(name or ""))[1].lower()
    if original_ext in TEXT_EXTENSIONS and original_ext != ".bytes":
        return original_ext
    return ".txt" if is_likely_text_bytes(data) else ".bytes"


def find_embedded_unity_bundle_offset(file_path):
    key = file_cache_key(file_path)
    cached = embedded_offset_cache.get(key)
    if isinstance(cached, int) and cached > 0:
        return cached
    try:
        with open(file_path, "rb") as handle:
            chunk = handle.read(UNITY_BUNDLE_SCAN_BYTES)
        offsets = [chunk.find(signature) for signature in UNITY_BUNDLE_SIGNATURES]
        offsets = [offset for offset in offsets if offset > 0]
        offset = min(offsets) if offsets else 0
        if offset > 0:
            embedded_offset_cache[key] = offset
        return offset
    except Exception:
        return 0


def skip_reason_for_type(obj_type):
    if obj_type in OBJECT_EXPORT_HINTS:
        return "profile-filtered {0}; use {1}".format(obj_type, OBJECT_EXPORT_HINTS[obj_type])
    if obj_type in INTERNAL_OBJECT_TYPES:
        return "internal scene/runtime object {0}; not an exportable asset".format(obj_type)
    return "unsupported Unity object {0}".format(obj_type)


def load_unity_environment(file_path):
    env = UnityPy.load(file_path)
    try:
        object_count = len(env.objects)
    except Exception:
        object_count = 0
    if object_count > 0:
        return env, 0

    offset = find_embedded_unity_bundle_offset(file_path)
    if offset <= 0:
        return env, 0

    with open(file_path, "rb") as handle:
        handle.seek(offset)
        payload = handle.read()
    fallback_env = UnityPy.load(io.BytesIO(payload))
    try:
        fallback_count = len(fallback_env.objects)
    except Exception:
        fallback_count = 0
    if fallback_count > object_count:
        return fallback_env, offset
    return env, 0


def extract_archive(file_path):
    extracted = 0
    total_size = 0
    errors = 0
    renamed = 0
    skipped = 0
    object_count = 0
    embedded_offset = 0
    type_counts = Counter()
    exported_type_counts = Counter()
    skip_reason_counts = Counter()
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
        env, embedded_offset = load_unity_environment(file_path)
        try:
            object_count = len(env.objects)
        except Exception:
            object_count = 0
        output_dir = archive_output_dir(file_path)

        for index, obj in enumerate(env.objects):
            obj_type = obj.type.name
            type_counts[obj_type] += 1
            supported = (
                (MODE_TEXTURES and obj_type in TEXTURE_TYPES)
                or (MODE_VIDEOS and obj_type == "VideoClip")
                or (MODE_AUDIOS and obj_type == "AudioClip")
                or (MODE_TEXT and obj_type == "TextAsset")
                or (MODE_MESHES and obj_type == "Mesh")
                or (MODE_ANIMATIONS and obj_type == "AnimationClip")
            )
            if not supported:
                skipped += 1
                skip_reason_counts[skip_reason_for_type(obj_type)] += 1
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
                        exported_type_counts[obj_type] += 1
                    else:
                        skipped += 1
                        skip_reason_counts["supported {0} object had no readable image payload".format(obj_type)] += 1

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
                        exported_type_counts[obj_type] += 1
                    else:
                        skipped += 1
                        skip_reason_counts["supported VideoClip had no readable embedded/external payload"] += 1

                elif MODE_AUDIOS and obj_type == "AudioClip":
                    name = getattr(data, "name", None) or getattr(data, "m_Name", None)
                    samples = safe_getattr(data, "samples")
                    saved_sample = False
                    if isinstance(samples, dict) and samples:
                        for sample_name, sample_data in samples.items():
                            if not sample_data:
                                continue
                            sample_ext = os.path.splitext(str(sample_name))[1] or ".wav"
                            sample_base = os.path.splitext(str(sample_name))[0] or safe_component(name, "audio_{0}".format(index))
                            pending_saves.append(
                                save_pool.submit(
                                    save_bytes,
                                    os.path.join(output_dir, safe_component(sample_base, "audio_{0}".format(index)) + sample_ext),
                                    sample_data,
                                )
                            )
                            saved_sample = True
                            drain_saves()
                            exported_type_counts[obj_type] += 1
                    if not saved_sample:
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
                            exported_type_counts[obj_type] += 1
                        else:
                            skipped += 1
                            skip_reason_counts["supported AudioClip had no readable samples/resource payload"] += 1

                elif MODE_TEXT and obj_type == "TextAsset":
                    name = getattr(data, "name", None) or getattr(data, "m_Name", None)
                    text_data = text_asset_bytes(data)
                    if text_data is not None:
                        ext = text_asset_extension(name, text_data)
                        size, collision = save_bytes(
                            os.path.join(output_dir, safe_component(name, "text_{0}".format(index)) + ext),
                            text_data,
                        )
                        extracted += 1
                        total_size += size
                        renamed += collision
                        exported_type_counts[obj_type] += 1
                    else:
                        skipped += 1
                        skip_reason_counts["supported TextAsset had no readable script bytes"] += 1

                elif MODE_MESHES and obj_type == "Mesh":
                    name = getattr(data, "m_Name", None)
                    size, collision = export_mesh(
                        os.path.join(output_dir, safe_component(name, "mesh_{0}".format(index)) + ".obj"),
                        data,
                    )
                    extracted += 1
                    total_size += size
                    renamed += collision
                    exported_type_counts[obj_type] += 1

                elif MODE_ANIMATIONS and obj_type == "AnimationClip":
                    name = getattr(data, "m_Name", None)
                    try:
                        tree = obj.read_typetree()
                    except Exception:
                        tree = {"name": name or "", "type": obj_type}
                    payload = json.dumps(tree, ensure_ascii=False, indent=2, default=str).encode("utf-8")
                    size, collision = save_bytes(
                        os.path.join(output_dir, safe_component(name, "animation_{0}".format(index)) + ".json"),
                        payload,
                    )
                    extracted += 1
                    total_size += size
                    renamed += collision
                    exported_type_counts[obj_type] += 1
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
        "embedded_offset": embedded_offset,
        "types": dict(type_counts),
        "exported_types": dict(exported_type_counts),
        "skip_reasons": dict(skip_reason_counts),
    }


def direct_file_supported(filename):
    lower = filename.lower()
    return (
        (MODE_TEXTURES and lower.endswith(IMAGE_EXTENSIONS))
        or (MODE_VIDEOS and lower.endswith(VIDEO_EXTENSIONS))
        or (MODE_AUDIOS and lower.endswith(AUDIO_EXTENSIONS))
        or (MODE_TEXT and lower.endswith(TEXT_EXTENSIONS))
    )


def should_probe_unity_bundle(path):
    ext = os.path.splitext(path)[1].lower()
    if ext in ARCHIVE_EXTENSIONS or ext in DIRECT_EXTENSIONS or ext in (".ress", ".resource"):
        return False
    lower = path.lower()
    if any(hint in lower for hint in ADDRESSABLE_HINTS):
        return True
    parent = os.path.basename(os.path.dirname(path)).lower()
    if parent.endswith(".bundle"):
        return True
    return not ext


def is_unity_bundle_signature(path):
    try:
        if os.path.getsize(path) < 16:
            return False
        with open(path, "rb") as handle:
            head = handle.read(16)
        return any(head.startswith(signature) for signature in UNITY_BUNDLE_SIGNATURES)
    except Exception:
        return False


def collect_files():
    archives = []
    direct_files = []
    output_abs = os.path.abspath(OUTPUT_PATH)
    extracted_abs = os.path.abspath(os.path.join(GAME_PATH, "extracted"))
    for root, dirs, files in os.walk(GAME_PATH):
        root_abs = os.path.abspath(root)
        dirs[:] = [
            name for name in dirs
            if name.lower() not in SKIP_DIR_NAMES
            and not os.path.abspath(os.path.join(root, name)).startswith(output_abs)
            and not os.path.abspath(os.path.join(root, name)).startswith(extracted_abs)
        ]
        if root_abs.startswith(output_abs) or root_abs.startswith(extracted_abs):
            continue
        for filename in files:
            full_path = os.path.join(root, filename)
            lower = filename.lower()
            if lower.endswith(OPTIONAL_BUNDLE_EXTENSIONS):
                if INCLUDE_BUNDLES:
                    archives.append(full_path)
            elif lower.endswith(".assets"):
                archives.append(full_path)
            elif INCLUDE_BUNDLES and should_probe_unity_bundle(full_path) and is_unity_bundle_signature(full_path):
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
        unity3d_count = sum(1 for item in archives if item.lower().endswith(".unity3d"))
        assets_count = sum(1 for item in archives if item.lower().endswith(".assets"))
        signature_bundle_count = sum(
            1 for item in archives
            if not item.lower().endswith(ARCHIVE_EXTENSIONS) and is_unity_bundle_signature(item)
        )
        archive_input_size = sum(os.path.getsize(item) for item in archives if os.path.isfile(item))
        zero_output = [item for item in archive_results if item.get("extracted", 0) == 0]
        error_archives = [item for item in archive_results if item.get("errors", 0) > 0]
        embedded_offset_count = sum(1 for item in archive_results if item.get("embedded_offset", 0) > 0)
        object_types = Counter()
        exported_types = Counter()
        skip_reasons = Counter()
        for item in archive_results:
            object_types.update(item.get("types") or {})
            exported_types.update(item.get("exported_types") or {})
            skip_reasons.update(item.get("skip_reasons") or {})
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
            output.write("Addressables/signature bundles: {0}\n".format(signature_bundle_count))
            output.write("Unity3D: {0}\n".format(unity3d_count))
            output.write("Assets: {0}\n".format(assets_count))
            output.write("Direct files: {0}\n".format(len(direct_files)))
            output.write("Archive input size: {0}\n".format(format_bytes(archive_input_size)))
            output.write("Archives with output: {0}\n".format(max(0, len(archives) - len(zero_output))))
            output.write("Archives with zero output: {0}\n".format(len(zero_output)))
            output.write("Archives with errors: {0}\n".format(len(error_archives)))
            output.write("Embedded UnityFS offsets recovered: {0}\n".format(embedded_offset_count))
            output.write("Skipped Unity objects: {0}\n".format(sum(item.get("skipped", 0) for item in archive_results)))
            output.write("Warnings emitted: {0}\n".format(warning_count))
            if addressable_labels:
                output.write("Addressables catalog labels: {0}\n".format(len(addressable_labels)))
            if object_types:
                output.write("Object type summary:\n")
                for name, count in object_types.most_common(30):
                    exported = exported_types.get(name, 0)
                    hint = OBJECT_EXPORT_HINTS.get(name, "not currently exportable")
                    output.write("- {0}: total={1} exported={2} profile={3}\n".format(name, count, exported, hint))
            if skip_reasons:
                output.write("Skipped reason summary:\n")
                for reason, count in skip_reasons.most_common(16):
                    output.write("- {0}: {1}\n".format(reason, count))
            if largest:
                output.write("Largest archives:\n")
                for item in largest:
                    output.write("- {0} ({1})\n".format(rel_path(item), format_bytes(os.path.getsize(item))))
            if zero_output:
                output.write("Zero-output archives:\n")
                for item in zero_output[:40]:
                    if item.get("objects", 0) == 0 and item.get("embedded_offset", 0) <= 0:
                        reason = "no readable Unity objects, unsupported/protected wrapper, or not UnityFS"
                    elif item.get("embedded_offset", 0) > 0:
                        reason = "wrapped UnityFS recovered, but no profile-matching exportable payload"
                    else:
                        reason = "objects were filtered by profile or unsupported by exporter"
                    output.write("- {0} | objects={1} | skipped={2} | errors={3} | reason={4}\n".format(
                        rel_path(item.get("path", "")),
                        item.get("objects", 0),
                        item.get("skipped", 0),
                        item.get("errors", 0),
                        reason,
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
    load_embedded_offset_cache()
    load_addressable_labels()
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
    save_embedded_offset_cache()
    log("RESULT:{0}:{1}:{2}:{3}:{4}".format(total_extracted, total_size, total_errors, total_renamed, total_skipped))
    return 0 if total_errors == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
