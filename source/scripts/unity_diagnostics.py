# -*- coding: utf-8 -*-
from __future__ import print_function

import json
import os
from collections import Counter

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


def skip_reason_for_type(obj_type):
    if obj_type in OBJECT_EXPORT_HINTS:
        return "profile-filtered {0}; use {1}".format(obj_type, OBJECT_EXPORT_HINTS[obj_type])
    if obj_type in INTERNAL_OBJECT_TYPES:
        return "internal scene/runtime object {0}; not an exportable asset".format(obj_type)
    return "unsupported Unity object {0}".format(obj_type)


def _counter_from_results(results, key):
    counter = Counter()
    for item in results:
        counter.update(item.get(key) or {})
    return counter


def build_payload(mode, include_bundles, archives, direct_files, archive_results, addressable_labels, warning_count, archive_extensions, is_signature, format_bytes, rel_path):
    bundle_count = sum(1 for item in archives if item.lower().endswith(".bundle"))
    unity3d_count = sum(1 for item in archives if item.lower().endswith(".unity3d"))
    assets_count = sum(1 for item in archives if item.lower().endswith(".assets"))
    signature_bundle_count = sum(1 for item in archives if not item.lower().endswith(archive_extensions) and is_signature(item))
    archive_input_size = sum(os.path.getsize(item) for item in archives if os.path.isfile(item))
    zero_output = [item for item in archive_results if item.get("extracted", 0) == 0]
    error_archives = [item for item in archive_results if item.get("errors", 0) > 0]
    embedded_offset_count = sum(1 for item in archive_results if item.get("embedded_offset", 0) > 0)
    object_types = _counter_from_results(archive_results, "types")
    exported_types = _counter_from_results(archive_results, "exported_types")
    skip_reasons = _counter_from_results(archive_results, "skip_reasons")
    largest = sorted([item for item in archives if os.path.isfile(item)], key=lambda item: os.path.getsize(item), reverse=True)[:8]

    zero_rows = []
    for item in zero_output[:80]:
        if item.get("objects", 0) == 0 and item.get("embedded_offset", 0) <= 0:
            reason = "no readable Unity objects, unsupported/protected wrapper, or not UnityFS"
        elif item.get("embedded_offset", 0) > 0:
            reason = "wrapped UnityFS recovered, but no profile-matching exportable payload"
        else:
            reason = "objects were filtered by profile or unsupported by exporter"
        zero_rows.append({
            "path": rel_path(item.get("path", "")),
            "objects": item.get("objects", 0),
            "skipped": item.get("skipped", 0),
            "errors": item.get("errors", 0),
            "reason": reason,
        })

    return {
        "mode": mode,
        "include_bundles": bool(include_bundles),
        "archives": len(archives),
        "bundles": bundle_count,
        "addressables_signature_bundles": signature_bundle_count,
        "unity3d": unity3d_count,
        "assets": assets_count,
        "direct_files": len(direct_files),
        "archive_input_size": archive_input_size,
        "archive_input_size_text": format_bytes(archive_input_size),
        "archives_with_output": max(0, len(archives) - len(zero_output)),
        "archives_with_zero_output": len(zero_output),
        "archives_with_errors": len(error_archives),
        "embedded_unityfs_offsets_recovered": embedded_offset_count,
        "skipped_unity_objects": sum(item.get("skipped", 0) for item in archive_results),
        "warnings_emitted": warning_count,
        "addressables_catalog_labels": len(addressable_labels or {}),
        "object_types": [
            {"type": name, "total": count, "exported": exported_types.get(name, 0), "profile": OBJECT_EXPORT_HINTS.get(name, "not currently exportable")}
            for name, count in object_types.most_common(50)
        ],
        "skipped_reasons": [
            {"reason": reason, "count": count}
            for reason, count in skip_reasons.most_common(32)
        ],
        "largest_archives": [
            {"path": rel_path(item), "size": os.path.getsize(item), "size_text": format_bytes(os.path.getsize(item))}
            for item in largest
        ],
        "zero_output_archives": zero_rows,
        "zero_output_archives_truncated": max(0, len(zero_output) - len(zero_rows)),
    }


def write_diagnostics(output_path, payload):
    if not os.path.isdir(output_path):
        os.makedirs(output_path)
    json_path = os.path.join(output_path, "GameAssetTool-unity-diagnostics.json")
    with open(json_path, "w", encoding="utf-8") as output:
        json.dump(payload, output, ensure_ascii=False, indent=2, sort_keys=True)

    text_path = os.path.join(output_path, "GameAssetTool-unity-diagnostics.txt")
    with open(text_path, "w", encoding="utf-8") as output:
        output.write("Unity diagnostics\n")
        output.write("Mode: {0}\n".format(payload.get("mode", "")))
        output.write("Include bundles: {0}\n".format("yes" if payload.get("include_bundles") else "no"))
        output.write("Archives: {0}\n".format(payload.get("archives", 0)))
        output.write("Bundles: {0}\n".format(payload.get("bundles", 0)))
        output.write("Addressables/signature bundles: {0}\n".format(payload.get("addressables_signature_bundles", 0)))
        output.write("Unity3D: {0}\n".format(payload.get("unity3d", 0)))
        output.write("Assets: {0}\n".format(payload.get("assets", 0)))
        output.write("Direct files: {0}\n".format(payload.get("direct_files", 0)))
        output.write("Archive input size: {0}\n".format(payload.get("archive_input_size_text", "0 B")))
        output.write("Archives with output: {0}\n".format(payload.get("archives_with_output", 0)))
        output.write("Archives with zero output: {0}\n".format(payload.get("archives_with_zero_output", 0)))
        output.write("Archives with errors: {0}\n".format(payload.get("archives_with_errors", 0)))
        output.write("Embedded UnityFS offsets recovered: {0}\n".format(payload.get("embedded_unityfs_offsets_recovered", 0)))
        output.write("Skipped Unity objects: {0}\n".format(payload.get("skipped_unity_objects", 0)))
        output.write("Warnings emitted: {0}\n".format(payload.get("warnings_emitted", 0)))
        if payload.get("addressables_catalog_labels"):
            output.write("Addressables catalog labels: {0}\n".format(payload.get("addressables_catalog_labels")))
        if payload.get("object_types"):
            output.write("Object type summary:\n")
            for item in payload.get("object_types", []):
                output.write("- {0}: total={1} exported={2} profile={3}\n".format(item.get("type"), item.get("total"), item.get("exported"), item.get("profile")))
        if payload.get("skipped_reasons"):
            output.write("Skipped reason summary:\n")
            for item in payload.get("skipped_reasons", []):
                output.write("- {0}: {1}\n".format(item.get("reason"), item.get("count")))
        if payload.get("largest_archives"):
            output.write("Largest archives:\n")
            for item in payload.get("largest_archives", []):
                output.write("- {0} ({1})\n".format(item.get("path"), item.get("size_text")))
        if payload.get("zero_output_archives"):
            output.write("Zero-output archives:\n")
            for item in payload.get("zero_output_archives", [])[:40]:
                output.write("- {0} | objects={1} | skipped={2} | errors={3} | reason={4}\n".format(item.get("path"), item.get("objects"), item.get("skipped"), item.get("errors"), item.get("reason")))
            if payload.get("zero_output_archives_truncated"):
                output.write("- ... {0} more\n".format(payload.get("zero_output_archives_truncated")))
    return json_path, text_path


def build_prediction_text(payload):
    types = payload.get("object_types") or []
    if not types:
        return ""
    wanted = []
    for item in types:
        exported = int(item.get("exported") or 0)
        if exported <= 0:
            continue
        name = item.get("type") or "UnityObject"
        wanted.append("{0}: {1}".format(name, exported))
    return " | ".join(wanted[:8])
