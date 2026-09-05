#!/usr/bin/env python3
import json
import re
from collections import Counter
from pathlib import Path


ROOT = Path(__file__).resolve().parent
POOL_PATH = ROOT / "supplemental_death_question_v2.json"
EXPECTED_FAMILIES = {
    "punctuation": 10,
    "disbelief": 10,
    "death_reaction": 10,
    "aim_miss_question": 10,
}


def fail(message: str) -> None:
    raise SystemExit(f"FAIL: {message}")


with POOL_PATH.open("r", encoding="utf-8") as handle:
    pool = json.load(handle)

if pool.get("pool_id") != "supplemental_death_question_v2":
    fail("unexpected pool_id")
if pool.get("event_label") != "death_question":
    fail("unexpected event_label")
if pool.get("source") != "new_curated":
    fail("question pool must be explicitly marked new_curated")

messages = pool.get("messages")
if not isinstance(messages, list) or len(messages) != 40:
    fail("expected exactly 40 messages")

ids = set()
texts = set()
families = Counter()
for position, message in enumerate(messages, start=1):
    expected_id = f"death_question_supp_{position:03d}"
    if message.get("id") != expected_id:
        fail(f"unexpected id at position {position}: {message.get('id')!r}")
    text = message.get("text")
    if not isinstance(text, str) or not text.strip():
        fail(f"empty text in {expected_id}")
    if "\n" in text or "\r" in text:
        fail(f"multiline text in {expected_id}")
    if not re.search(r"[？?]", text):
        fail(f"missing question mark in {expected_id}: {text}")
    if len(text) > 12:
        fail(f"question reaction is too long in {expected_id}: {text}")
    if message.get("phase") not in {"burst", "both"}:
        fail(f"invalid phase in {expected_id}")
    family = message.get("family")
    if family not in EXPECTED_FAMILIES:
        fail(f"invalid family in {expected_id}: {family!r}")
    if expected_id in ids or text in texts:
        fail(f"duplicate id or text in {expected_id}")
    ids.add(expected_id)
    texts.add(text)
    families[family] += 1

if dict(families) != EXPECTED_FAMILIES:
    fail(f"family counts mismatch: {dict(families)}")

print("PASS: supplemental death_question pool validated")
print(f"total={len(messages)}")
print("families=" + ", ".join(f"{name}:{families[name]}" for name in sorted(families)))
