"""Run the reference repository's pure helpers, without installing its Qt GUI.

Usage: python compare_reference.py <cs2-customizer checkout> <C# fixture directory>
No reference source is copied into this project or modified.
"""
import ast
import csv
import dataclasses
import json
import os
from pathlib import Path
import re
import sys
import types
import zipfile


def helpers(path, names, module_name):
    tree = ast.parse(path.read_text(encoding="utf-8-sig"))
    selected = []
    for node in tree.body:
        if isinstance(node, (ast.FunctionDef, ast.ClassDef)) and node.name in names:
            selected.append(node)
        elif isinstance(node, ast.Assign) and any(isinstance(t, ast.Name) and t.id in names for t in node.targets):
            selected.append(node)
    module = types.ModuleType(module_name)
    module.__dict__.update(os=os, json=json, re=re, zipfile=zipfile,
                           dataclass=dataclasses.dataclass, field=dataclasses.field,
                           KillIconImportError=ValueError)
    sys.modules[module_name] = module
    exec(compile(ast.Module(body=selected, type_ignores=[]), str(path), "exec"), module.__dict__)
    return module


reference, fixtures = map(Path, sys.argv[1:3])
overlay = helpers(reference / "kill_icon_overlay.py", {
    "DEFAULT_FPS", "MIN_FPS", "MAX_FPS", "MAX_HOLD_SECONDS", "DEFAULT_STATIC_HOLD_SECONDS",
    "clamp_fps", "clamp_hold", "playback_state"
}, "reference_timeline")
# Source probing imports the pure clamps from this module; no Qt is loaded.
sys.modules["kill_icon_overlay"] = overlay
count = 0
with (fixtures / "timeline.csv").open(newline="") as source:
    for row in csv.DictReader(source):
        actual = overlay.playback_state(float(row["elapsed"]), int(row["fps"]), int(row["frames"]),
                                        0, 0, float(row["hold"]))
        if row["finished"] == "1":
            assert actual is None, row
        else:
            assert actual is not None and actual[0] == int(row["index"]), row
            assert actual[1] == 1.0, (actual, row)
        count += 1

importer = helpers(reference / "core/kill_icon_import.py", {
    "LEVEL_ALIASES", "HEADSHOT_ALIASES", "parse_level_name",
    "ANIMATED_EXTENSIONS", "STATIC_EXTENSIONS", "SEQUENCE_EXTENSIONS", "_sorted_sequence_files",
    "SINGLE_FILE_EXTENSIONS", "MAX_FRAMES", "MAX_FRAME_EDGE", "DEFAULT_FPS", "RATE_SAMPLE_FRAMES",
    "SourceProbe", "_pil", "_fit", "_as_int", "_fps_from_durations", "_static_probe_hold",
    "_sibling_metadata_path", "_resolve_sheet_image", "_probe_spritesheet", "_probe_animation", "probe_source"
}, "reference_importer")
alias_count = 0
with (fixtures / "aliases.tsv").open(encoding="utf-8") as source:
    for line in source:
        name, actual = line.rstrip("\n").split("\t")
        parsed = importer.parse_level_name(name)
        expected = "" if parsed is None else f"{parsed[0]}{parsed[1]}"
        assert actual == expected, (name, actual, expected)
        alias_count += 1
assert [Path(p).name for p in importer._sorted_sequence_files(str(fixtures / "legacy/1"))] == ["2.png", "10.png"]

# Exercise the reference's actual PNG -> sibling JSON dispatch against the same
# oversized atlas used by the C# picker/import/runtime regression.
atlas_result = json.loads((fixtures / "wide-atlas-result.json").read_text(encoding="utf-8-sig"))
for extension in ("png", "json"):
    atlas_probe = importer.probe_source(str(fixtures / f"wide-atlas/sheet.{extension}"))
    assert atlas_probe.kind == "spritesheet", atlas_probe
    assert (atlas_probe.frame_width, atlas_probe.frame_height, atlas_probe.frame_count,
            atlas_probe.fps, atlas_probe.hold_seconds, atlas_probe.grid[0]) == (
        atlas_result["frame_width"], atlas_result["frame_height"], atlas_result["frames"],
        atlas_result["fps"], atlas_result["hold_seconds"], atlas_result["cols"]), atlas_probe
raw_probe = importer.probe_source(str(fixtures / "large-raw-frames/frame.png"))
raw_result = json.loads((fixtures / "raw-frame-result.json").read_text(encoding="utf-8-sig"))
assert raw_probe.kind == "animation" and raw_probe.warnings, raw_probe
assert (raw_probe.frame_width, raw_probe.frame_height, raw_probe.frame_count) == (
    raw_result["frame_width"], raw_result["frame_height"], raw_result["frames"]), raw_probe

pack = helpers(reference / "core/kill_icon_pack.py", {
    "PACK_VERSION", "MANIFEST_NAME", "LEVEL_ENTRY_RE", "MAX_ENTRIES",
    "MAX_UNCOMPRESSED_BYTES", "MAX_COMPRESSION_RATIO", "PackProbe",
    "_safe_relpath", "_iter_safe_members", "_strip_single_root", "probe_pack", "_collect_loose_items"
}, "reference_pack")
pack.parse_level_name = importer.parse_level_name
probe = pack.probe_pack(str(fixtures / "roundtrip.zip"))
assert probe.name == "兼容测试" and probe.levels == [(1, "")], probe
assert not probe.warnings, probe.warnings
loose = pack.probe_pack(str(fixtures / "loose.zip"))
assert loose.name == "Theme" and [(k, v) for k, v, _ in loose.loose_items] == [(1, ""), (3, "hs"), (5, "")], loose
mixed = pack.probe_pack(str(fixtures / "mixed.zip"))
assert mixed.levels == [(1, "")] and mixed.warnings, mixed
print(f"PASS: {count} timeline states, {alias_count} aliases, numeric frame order, PNG/JSON atlas detection, raw frame sizing, standard/loose package rules and exported ZIP agree with the reference helpers.")
