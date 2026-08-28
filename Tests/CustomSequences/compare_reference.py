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
    "DEFAULT_FPS", "MIN_FPS", "MAX_FPS", "MAX_HOLD_SECONDS",
    "clamp_fps", "clamp_hold", "playback_state"
}, "reference_timeline")
count = 0
with (fixtures / "timeline.csv").open(newline="") as source:
    for row in csv.DictReader(source):
        fade = row["fade"] == "1"
        actual = overlay.playback_state(float(row["elapsed"]), int(row["fps"]), int(row["frames"]),
                                        .12 if fade else 0, .25 if fade else 0, float(row["hold"]))
        if row["finished"] == "1":
            assert actual is None, row
        else:
            assert actual is not None and actual[0] == int(row["index"]), row
            assert abs(actual[1] - float(row["opacity"])) < 1e-6, (actual, row)
        count += 1

pack = helpers(reference / "core/kill_icon_pack.py", {
    "PACK_VERSION", "MANIFEST_NAME", "LEVEL_ENTRY_RE", "MAX_ENTRIES",
    "MAX_UNCOMPRESSED_BYTES", "MAX_COMPRESSION_RATIO", "PackProbe",
    "_safe_relpath", "_iter_safe_members", "_strip_single_root", "probe_pack"
}, "reference_pack")
probe = pack.probe_pack(str(fixtures / "roundtrip.zip"))
assert probe.name == "兼容测试" and probe.levels == [(1, "")], probe
assert not probe.warnings, probe.warnings
print(f"PASS: {count} C# timeline states match reference playback_state; reference probe_pack accepts exported ZIP.")
