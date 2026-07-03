# -*- coding: utf-8 -*-
from __future__ import print_function

import io
import json
import os
import re
import shutil
import struct
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
TEXT_EXTENSIONS = (
    ".txt", ".json", ".xml", ".csv", ".tsv", ".ini", ".cfg", ".conf", ".yaml", ".yml",
    ".po", ".pot", ".mo", ".tmx", ".strings", ".lang", ".loc", ".rpy", ".rpym", ".ks",
    ".js", ".css", ".html", ".htm", ".bytes"
)
DIRECT_EXTENSIONS = IMAGE_EXTENSIONS + VIDEO_EXTENSIONS + AUDIO_EXTENSIONS + TEXT_EXTENSIONS
ARCHIVE_EXTENSIONS = (".assets", ".bundle", ".unity3d")
OPTIONAL_BUNDLE_EXTENSIONS = (".bundle", ".unity3d")
SKIP_DIR_NAMES = ("bepinex", "dotnet", "mono", "crashpad", "logs")
TEXTURE_TYPES = ("Texture2D", "Sprite", "Cubemap", "Texture3D", "Texture2DArray")
TEXT_RECOVERY_TYPES = ("MonoBehaviour", "ScriptableObject", "Object")
MODE_TEXTURES = EXTRACT_MODE in ("textures", "media", "textures-text", "all")
MODE_VIDEOS = EXTRACT_MODE in ("videos", "media", "all")
MODE_AUDIOS = EXTRACT_MODE in ("audios", "all")
MODE_TRANSLATION_CANDIDATES = EXTRACT_MODE == "translation-candidates"
MODE_TEXT = EXTRACT_MODE in ("text", "textures-text", "all")
MODE_MESHES = EXTRACT_MODE in ("meshes", "all")
MODE_TEXT_RECOVERY = MODE_TEXT

TEXT_PATH_HINTS = (
    "text", "local", "locale", "language", "translation", "dialog", "dialogue", "message",
    "scenario", "script", "story", "subtitle", "line", "chapter", "event", "drama",
    "adultonlytext", "emai", "live_data", "sex_data", "spycamera"
)
TEXT_FIELD_HINTS = (
    "text", "message", "dialog", "dialogue", "scenario", "script", "story", "subtitle",
    "line", "body", "content", "value", "title", "description", "json", "data"
)
MAX_RECOVERED_STRINGS_PER_OBJECT = 400
MIN_RECOVERED_TEXT_LENGTH = 2
MAX_RAW_STRINGS_PER_ARCHIVE = 2000
MAX_TRANSLATION_CANDIDATES_PER_ARCHIVE = 4000
RAW_TEXT_MIN_LENGTH = 4
RAW_TEXT_EXTENSIONS = (".json", ".bytes", ".asset", ".txt", ".csv", ".xml")

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


def has_text_hint(value, hints=TEXT_PATH_HINTS):
    lower = str(value or "").replace("\\", "/").lower()
    return any(hint in lower for hint in hints)


def object_path_id(obj):
    for attr in ("path_id", "m_PathID"):
        value = safe_getattr(obj, attr)
        if value is not None:
            return value
    return None


def build_container_paths(env):
    paths = {}
    container = safe_getattr(env, "container")
    if not container:
        return paths
    try:
        items = container.items()
    except Exception:
        return paths
    for asset_path, entry in items:
        candidates = []
        if isinstance(entry, (list, tuple)):
            candidates.extend(entry)
        else:
            candidates.append(entry)
        for candidate in candidates:
            target = safe_getattr(candidate, "asset") or safe_getattr(candidate, "obj") or candidate
            path_id = object_path_id(target)
            if path_id is not None and asset_path:
                paths[path_id] = str(asset_path)
    return paths


def safe_asset_relative_path(asset_path, fallback_name, fallback_ext, fallback_prefix):
    normalized = str(asset_path or "").replace("\\", "/").strip(" /")
    if normalized.lower().startswith("assets/"):
        normalized = normalized[7:]
    if normalized.lower().startswith("addressable/"):
        normalized = normalized[12:]
    if normalized:
        parts = [safe_component(part, "asset") for part in normalized.split("/") if part]
        if parts:
            base = os.path.join(*parts)
            ext = os.path.splitext(base)[1].lower()
            if ext:
                return base
            return base + fallback_ext
    return safe_component(fallback_name, fallback_prefix) + fallback_ext


def read_object_typetree(obj):
    for call in (
        lambda: obj.read_typetree(),
        lambda: obj.read_typetree(wrap=True),
    ):
        try:
            return call()
        except Exception:
            continue
    return None


def decode_text_bytes(value):
    if not value:
        return ""
    raw = bytes(value)
    if not is_likely_text_bytes(raw):
        return ""
    for encoding in ("utf-8-sig", "utf-16", "shift_jis", "cp932"):
        try:
            text = raw.decode(encoding)
            if text:
                return text
        except Exception:
            continue
    return raw.decode("utf-8", "ignore")


def iter_string_fields(value, field_path="", depth=0):
    if depth > 24:
        return
    if isinstance(value, str):
        yield field_path, value
        return
    if isinstance(value, (bytes, bytearray)):
        text = decode_text_bytes(value)
        if text:
            yield field_path, text
        return
    if isinstance(value, dict):
        for key, child in value.items():
            key_text = str(key)
            child_path = key_text if not field_path else field_path + "." + key_text
            for item in iter_string_fields(child, child_path, depth + 1):
                yield item
        return
    if isinstance(value, (list, tuple)):
        for index, child in enumerate(value):
            child_path = "{0}[{1}]".format(field_path, index) if field_path else "[{0}]".format(index)
            for item in iter_string_fields(child, child_path, depth + 1):
                yield item


def has_cjk(text):
    return re.search(r"[\u3040-\u30ff\u3400-\u9fff]", text or "") is not None


def looks_like_useful_text(field_path, text, object_hint):
    stripped = str(text or "").strip()
    lower_field = str(field_path or "").lower()
    if len(stripped) < MIN_RECOVERED_TEXT_LENGTH:
        return False
    if lower_field in ("m_name", "name"):
        return False
    if stripped.startswith(("{", "[")) and stripped.endswith(("}", "]")):
        return True
    if has_cjk(stripped):
        return True
    if "\n" in stripped and len(stripped) >= 20:
        return True
    if has_text_hint(field_path, TEXT_FIELD_HINTS) and len(stripped) >= 8:
        return True
    return False


def recover_text_payload(obj, data, object_name, asset_path):
    object_hint = has_text_hint(asset_path) or has_text_hint(object_name)
    tree = read_object_typetree(obj)
    if tree is None:
        return None
    rows = []
    seen = set()
    for field_path, value in iter_string_fields(tree):
        value = str(value).replace("\r\n", "\n").strip()
        if not looks_like_useful_text(field_path, value, object_hint):
            continue
        key = (field_path, value)
        if key in seen:
            continue
        seen.add(key)
        rows.append({"field": field_path, "text": value})
        if len(rows) >= MAX_RECOVERED_STRINGS_PER_OBJECT:
            break
    if not rows:
        return None
    payload = {
        "assetPath": asset_path or "",
        "name": object_name or "",
        "type": obj.type.name,
        "pathId": object_path_id(obj),
        "strings": rows,
    }
    return json.dumps(payload, ensure_ascii=False, indent=2).encode("utf-8")


def cjk_count(value):
    return len(re.findall(r"[\u3040-\u30ff\u3400-\u9fff]", value or ""))


def raw_text_kind(value):
    lower = str(value or "").lower()
    if "texture" in lower or "shader" in lower or "material" in lower:
        if not any(ext in lower for ext in RAW_TEXT_EXTENSIONS):
            return ""
    if any(ext in lower for ext in RAW_TEXT_EXTENSIONS) and ("/" in value or "\\" in value or len(value) >= 12):
        return "asset-path"
    cjk = cjk_count(value)
    if cjk >= 4 and (float(cjk) / max(1, len(value))) >= 0.25:
        return "cjk-text"
    if has_text_hint(value) and (len(value) >= 12 or any(marker in value for marker in ("/", "\\", "_", "@", "."))):
        return "hinted-text"
    return ""


def normalize_raw_text(value):
    value = str(value or "").replace("\x00", "").replace("\r\n", "\n").replace("\r", "\n").strip()
    value = re.sub(r"[\t ]+", " ", value)
    value = re.sub(r"\n+", "\n", value)
    return value[:4000]


def add_raw_candidate(rows, seen, value, source):
    value = normalize_raw_text(value)
    if len(value) < RAW_TEXT_MIN_LENGTH:
        return
    kind = raw_text_kind(value)
    if not kind:
        return
    key = (kind, value)
    if key in seen:
        return
    seen.add(key)
    rows.append({"kind": kind, "source": source, "text": value})


def tsv_escape(value):
    return str(value or "").replace("\t", " ").replace("\r\n", "\\n").replace("\r", "\\n").replace("\n", "\\n")


def looks_like_identifier(value):
    return re.match(r"^[A-Za-z_][A-Za-z0-9_.$<>`+:/| -]{0,120}$", value or "") is not None


def has_control_noise(value):
    return re.search(r"[\x00-\x08\x0b\x0c\x0e-\x1f\x7f]", value or "") is not None


def translation_ui_terms():
    return (
        "new game", "continue", "options", "settings", "quit", "exit", "save", "load",
        "back", "gallery", "start", "retry", "yes", "no", "ok", "cancel", "close",
        "page", "pages", "chapter", "dialog", "dialogue", "message", "text", "skip", "auto", "menu",
        "press", "click", "arrow", "enter", "escape", "inventory", "item", "items"
    )


def looks_like_asset_noise(value):
    text = value or ""
    lower = text.lower()
    if any(token in lower for token in ("sharedassets", ".ress", "unityengine.", "unity.", "system.", "microsoft.", "game.gameplay", "game.ui")):
        return True
    if any(token in lower for token in ("sdf atlas", "liberationsans", "pangolin-regular", "tmp settings", ".notdef", "<noninit>")):
        return True
    if re.fullmatch(r"[0-9a-f]{16,}", lower):
        return True
    if lower.startswith("<") or any(token in lower for token in ("<size", "<align", "<#", "</")):
        return True
    if "/" in text or "\\" in text:
        return True
    if lower.startswith(("assets", "library", "packages", "projectsettings")):
        return True
    if " -> " in text or lower.startswith("base layer."):
        return True
    if lower.startswith("steps ") or lower.startswith("sfx ") or lower.startswith("amb "):
        return True
    if "bokeh filter" in lower or lower.startswith("hintarrow") or lower.startswith("text ("):
        return True
    if re.search(r"[a-z][A-Z]", text) and not any(term in lower for term in translation_ui_terms()):
        return True
    if text.startswith("_") and " " not in text:
        return True
    if "_" in text or "@" in text:
        return True
    shader_terms = (
        "texture", "shader", "material", "sprite", "mesh", "prefab", "normal map", "albedo",
        "metallic", "smoothness", "uv", "axis", "stencil", "alpha", "outline", "glow",
        "gradient", "luminosity", "particles", "distortion", "blur", "billboard", "color ramp",
        "scroll speed", "fade", "shadow", "greyscale", "cutoff", "render queue", "normal",
        "angle", "radians", "render", "low res", "smiling face", "tears of joy", "emoji",
        "color", "rgb", "opacity"
    )
    if any(token in lower for token in shader_terms):
        return True
    if re.search(r"\b(enum|vector|float|range|toggle)\s*\(", lower):
        return True
    if re.search(r"\b[a-z]+\d+[a-z0-9]*\b", lower) and not any(term in lower for term in translation_ui_terms()):
        return True
    punctuation = sum(1 for ch in text if not ch.isalnum() and not ch.isspace())
    punctuation_ratio = float(punctuation) / max(1, len(text))
    if len(text) >= 40 and punctuation_ratio > 0.45:
        return True
    if len(text) < 20 and punctuation_ratio > 0.25 and not any(term in lower for term in translation_ui_terms()):
        return True
    words = re.findall(r"[A-Za-z][A-Za-z'\-]*", text)
    if len(words) <= 2 and not has_cjk(text) and not any(term in lower for term in translation_ui_terms()):
        return True
    return False


def looks_like_natural_text(text):
    lower = text.lower()
    words = re.findall(r"[A-Za-z][A-Za-z'\-]*", text)
    has_ui_term = any(term in lower for term in translation_ui_terms())
    if cjk_count(text) >= 2:
        return True
    if has_ui_term:
        return True
    if len(text) < 12:
        return False
    if len(words) >= 5:
        return True
    if len(words) >= 3 and any(p in text for p in ".!?,:;[]()'"):
        return True
    return False


def looks_like_translation_candidate(value):
    text = normalize_raw_text(value)
    if len(text) < 4 or len(text) > 2000:
        return False
    if has_control_noise(text):
        return False
    if not any(ch.isalpha() or has_cjk(ch) for ch in text):
        return False
    if looks_like_asset_noise(text):
        return False
    if looks_like_identifier(text) and " " not in text and not has_cjk(text):
        return False
    return looks_like_natural_text(text)


def translation_confidence(value):
    score = 0
    text = normalize_raw_text(value)
    lower = text.lower()
    if cjk_count(text) >= 2:
        score += 3
    if len(re.findall(r"[A-Za-z][A-Za-z'\-]*", text)) >= 3:
        score += 2
    if any(p in text for p in ".!?"):
        score += 2
    if any(term in lower for term in ("page", "chapter", "dialog", "story", "save", "load", "options")):
        score += 1
    if score >= 5:
        return "high"
    if score >= 3:
        return "medium"
    return "low"


def aligned4(value):
    return (int(value) + 3) & ~3


def add_translation_candidate(rows, seen, offset, value, source):
    value = normalize_raw_text(value)
    if not looks_like_translation_candidate(value):
        return
    key = value.lower()
    if key in seen:
        return
    seen.add(key)
    byte_length = len(value.encode("utf-8"))
    patch_max = aligned4(byte_length)
    patch_min = max(0, patch_max - 3)
    rows.append({
        "offset": offset,
        "textOffset": offset + 4,
        "byteLength": byte_length,
        "patchMinBytes": patch_min,
        "patchMaxBytes": patch_max,
        "patchRule": "utf8 length must stay in the same 4-byte Unity string block",
        "source": source,
        "confidence": translation_confidence(value),
        "translation": "",
        "text": value,
    })

def recover_translation_candidates_payload(file_path):
    try:
        with open(file_path, "rb") as stream:
            raw = stream.read()
    except Exception:
        return None, None, 0
    rows = []
    seen = set()
    raw_len = len(raw)
    for offset in range(0, max(0, raw_len - 8)):
        length = struct.unpack_from("<I", raw, offset)[0]
        if length < 4 or length > 2000 or offset + 4 + length > raw_len:
            continue
        chunk = raw[offset + 4:offset + 4 + length]
        if chunk.count(b"\x00"):
            continue
        try:
            value = chunk.decode("utf-8")
        except UnicodeDecodeError:
            continue
        add_translation_candidate(rows, seen, offset, value, "unity-string")
        if len(rows) >= MAX_TRANSLATION_CANDIDATES_PER_ARCHIVE:
            break
    if not rows:
        return None, None, 0
    payload = {
        "archive": rel_path(file_path),
        "mode": "translation-candidates",
        "note": "Candidate UI/story strings recovered from Unity serialized string bytes. Review before translation or patching.",
        "strings": rows,
    }
    tsv_lines = ["archive	offset	text_offset	byte_length	patch_min_bytes	patch_max_bytes	confidence	source	translation	text"]
    archive = rel_path(file_path)
    for row in rows:
        tsv_lines.append("{0}	{1}	{2}	{3}	{4}	{5}	{6}	{7}	{8}	{9}".format(
            tsv_escape(archive),
            row.get("offset", ""),
            row.get("textOffset", ""),
            row.get("byteLength", ""),
            row.get("patchMinBytes", ""),
            row.get("patchMaxBytes", ""),
            tsv_escape(row.get("confidence", "")),
            tsv_escape(row.get("source", "")),
            tsv_escape(row.get("translation", "")),
            tsv_escape(row.get("text", "")),
        ))
    return (
        json.dumps(payload, ensure_ascii=False, indent=2).encode("utf-8"),
        ("\n".join(tsv_lines) + "\n").encode("utf-8"),
        len(rows),
    )

def recover_raw_text_payload(file_path):
    try:
        with open(file_path, "rb") as stream:
            raw = stream.read()
    except Exception:
        return None
    if not raw:
        return None

    rows = []
    seen = set()

    path_pattern = re.compile(
        rb"(?:Assets/Addressable/)?[A-Za-z0-9_./() \-]{3,}\.(?:json|bytes|asset|txt|csv|xml)",
        re.IGNORECASE,
    )
    for match in path_pattern.finditer(raw):
        add_raw_candidate(rows, seen, match.group(0).decode("utf-8", "ignore"), "ascii-path")
        if len(rows) >= MAX_RAW_STRINGS_PER_ARCHIVE:
            break

    decoded_sources = (
        ("utf-8", raw.decode("utf-8", "ignore")),
        ("utf-16le", raw.decode("utf-16le", "ignore")),
        ("cp932", raw.decode("cp932", "ignore")),
    )
    split_pattern = re.compile(r"[\x00-\x08\x0b\x0c\x0e-\x1f]+")
    for source, decoded in decoded_sources:
        if len(rows) >= MAX_RAW_STRINGS_PER_ARCHIVE:
            break
        for chunk in split_pattern.split(decoded):
            chunk = normalize_raw_text(chunk)
            if len(chunk) > 2000:
                for submatch in re.finditer(r"[\u3040-\u30ff\u3400-\u9fff][^\x00\r\n]{3,300}", chunk):
                    add_raw_candidate(rows, seen, submatch.group(0), source)
                    if len(rows) >= MAX_RAW_STRINGS_PER_ARCHIVE:
                        break
            else:
                add_raw_candidate(rows, seen, chunk, source)
            if len(rows) >= MAX_RAW_STRINGS_PER_ARCHIVE:
                break

    if not rows:
        return None
    payload = {
        "archive": rel_path(file_path),
        "mode": "raw-text-carving",
        "note": "Recovered from raw bundle bytes because UnityPy did not expose these entries as exportable objects.",
        "strings": rows,
    }
    return json.dumps(payload, ensure_ascii=False, indent=2).encode("utf-8")


def extract_archive(file_path):
    extracted = 0
    total_size = 0
    errors = 0
    renamed = 0
    skipped = 0
    object_count = 0
    translation_candidates = 0
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
        container_paths = build_container_paths(env)

        for index, obj in enumerate(env.objects):
            obj_type = obj.type.name
            asset_path = container_paths.get(object_path_id(obj), "")
            supported = (
                (MODE_TEXTURES and obj_type in TEXTURE_TYPES)
                or (MODE_VIDEOS and obj_type == "VideoClip")
                or (MODE_AUDIOS and obj_type == "AudioClip")
                or (MODE_TEXT and obj_type == "TextAsset")
                or (MODE_TEXT_RECOVERY and obj_type in TEXT_RECOVERY_TYPES)
                or (MODE_MESHES and obj_type == "Mesh")
            )
            if not supported:
                skipped += 1
                continue

            try:
                data = None
                if MODE_TEXT_RECOVERY and obj_type in TEXT_RECOVERY_TYPES:
                    try:
                        data = obj.read()
                    except Exception:
                        data = None
                else:
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

                elif MODE_TEXT and obj_type == "TextAsset":
                    name = getattr(data, "name", None) or getattr(data, "m_Name", None)
                    text_data = text_asset_bytes(data)
                    if text_data is not None:
                        ext = text_asset_extension(asset_path or name, text_data)
                        relative = safe_asset_relative_path(asset_path, name, ext, "text_{0}".format(index))
                        size, collision = save_bytes(
                            os.path.join(output_dir, "text", relative),
                            text_data,
                        )
                        extracted += 1
                        total_size += size
                        renamed += collision
                    else:
                        skipped += 1

                elif MODE_TEXT_RECOVERY and obj_type in TEXT_RECOVERY_TYPES:
                    name = getattr(data, "name", None) or getattr(data, "m_Name", None) if data is not None else ""
                    payload = recover_text_payload(obj, data, name, asset_path)
                    if payload is not None:
                        relative = safe_asset_relative_path(asset_path, name, ".json", "object_{0}".format(index))
                        base, _ = os.path.splitext(relative)
                        size, collision = save_bytes(
                            os.path.join(output_dir, "recovered-text", base + ".recovered.json"),
                            payload,
                        )
                        extracted += 1
                        total_size += size
                        renamed += collision
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

        if MODE_TRANSLATION_CANDIDATES:
            translation_json, translation_tsv, translation_count = recover_translation_candidates_payload(file_path)
            if translation_json is not None:
                translation_candidates += translation_count
                raw_name = safe_component(os.path.basename(file_path), "archive") + ".translation-candidates"
                size, collision = save_bytes(os.path.join(output_dir, "translation-candidates", raw_name + ".json"), translation_json)
                extracted += 1
                total_size += size
                renamed += collision
                size, collision = save_bytes(os.path.join(output_dir, "translation-candidates", raw_name + ".tsv"), translation_tsv)
                extracted += 1
                total_size += size
                renamed += collision

        if MODE_TEXT_RECOVERY and (object_count == 0 or extracted == 0):
            raw_payload = recover_raw_text_payload(file_path)
            if raw_payload is not None:
                raw_name = safe_component(os.path.basename(file_path), "archive") + ".raw-text.json"
                size, collision = save_bytes(os.path.join(output_dir, "raw-text", raw_name), raw_payload)
                extracted += 1
                total_size += size
                renamed += collision

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
        "translation_candidates": translation_candidates,
    }


def direct_file_supported(filename):
    lower = filename.lower()
    return (
        (MODE_TEXTURES and lower.endswith(IMAGE_EXTENSIONS))
        or (MODE_VIDEOS and lower.endswith(VIDEO_EXTENSIONS))
        or (MODE_AUDIOS and lower.endswith(AUDIO_EXTENSIONS))
        or (MODE_TEXT and lower.endswith(TEXT_EXTENSIONS))
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
            elif lower.startswith("cab-") and not os.path.splitext(filename)[1]:
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
            output.write("Unity3D: {0}\n".format(unity3d_count))
            output.write("Assets: {0}\n".format(assets_count))
            output.write("Direct files: {0}\n".format(len(direct_files)))
            output.write("Archive input size: {0}\n".format(format_bytes(archive_input_size)))
            output.write("Archives with output: {0}\n".format(max(0, len(archives) - len(zero_output))))
            output.write("Archives with zero output: {0}\n".format(len(zero_output)))
            output.write("Archives with errors: {0}\n".format(len(error_archives)))
            output.write("Skipped Unity objects: {0}\n".format(sum(item.get("skipped", 0) for item in archive_results)))
            output.write("Translation candidates: {0}\n".format(sum(item.get("translation_candidates", 0) for item in archive_results)))
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
