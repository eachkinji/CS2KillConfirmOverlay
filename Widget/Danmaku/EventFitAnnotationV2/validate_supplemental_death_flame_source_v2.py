#!/usr/bin/env python3
import hashlib
import json
import re
from collections import Counter
from pathlib import Path


ROOT = Path(__file__).resolve().parent
SOURCE_PATH = ROOT.parent / "6657_memes.json"
POOL_PATH = ROOT / "supplemental_death_flame_source_v2.json"
EXPECTED_INDICES = {
    166, 7053, 9279, 10175, 13111, 13295, 15916, 16180,
    17396, 18957, 21201, 22562, 22855, 23193, 23291,
}
EXPECTED_FAMILIES = {
    "direct_vegetable": 10,
    "aim_miss": 3,
    "misplay": 2,
}
DIRECT_MARKERS = re.compile(r"菜|空枪|十枪九空|白给|别玩")


def fail(message: str) -> None:
    raise SystemExit(f"FAIL: {message}")


with SOURCE_PATH.open("r", encoding="utf-8-sig") as handle:
    source = json.load(handle)
with POOL_PATH.open("r", encoding="utf-8") as handle:
    pool = json.load(handle)

if pool.get("pool_id") != "supplemental_death_flame_source_v2":
    fail("unexpected pool_id")
if pool.get("event_label") != "death_flame":
    fail("unexpected event_label")
if pool.get("source") != "Widget/Danmaku/6657_memes.json":
    fail("source pool must point to the original meme library")

messages = pool.get("messages")
if not isinstance(messages, list) or len(messages) != len(EXPECTED_INDICES):
    fail(f"expected exactly {len(EXPECTED_INDICES)} messages")

ids = set()
indices = set()
texts = set()
families = Counter()
for position, message in enumerate(messages, start=1):
    expected_id = f"death_flame_src_{position:03d}"
    if message.get("id") != expected_id:
        fail(f"unexpected id at position {position}: {message.get('id')!r}")
    source_index = message.get("source_index")
    if not isinstance(source_index, int) or not 1 <= source_index <= len(source):
        fail(f"invalid source_index in {expected_id}")
    source_text = source[source_index - 1]
    if message.get("source_text") != source_text:
        fail(f"source text mismatch at index {source_index}")
    digest = hashlib.sha256(source_text.encode("utf-8")).hexdigest()
    if message.get("sha256") != digest:
        fail(
            f"sha256 mismatch at index {source_index}: "
            f"expected {digest}, got {message.get('sha256')} from {SOURCE_PATH}"
        )
    if not DIRECT_MARKERS.search(source_text):
        fail(f"missing direct flame marker at index {source_index}")
    if message.get("phase") not in {"burst", "aftermath", "both"}:
        fail(f"invalid phase in {expected_id}")
    family = message.get("family")
    if family not in EXPECTED_FAMILIES:
        fail(f"invalid family in {expected_id}: {family!r}")
    if expected_id in ids or source_index in indices or source_text in texts:
        fail(f"duplicate id, source index, or exact text in {expected_id}")
    ids.add(expected_id)
    indices.add(source_index)
    texts.add(source_text)
    families[family] += 1

if indices != EXPECTED_INDICES:
    fail(f"source index set mismatch: {sorted(indices)}")
if dict(families) != EXPECTED_FAMILIES:
    fail(f"family counts mismatch: {dict(families)}")

print("PASS: supplemental source death_flame pool validated")
print(f"total={len(messages)}")
print("families=" + ", ".join(f"{name}:{families[name]}" for name in sorted(families)))
