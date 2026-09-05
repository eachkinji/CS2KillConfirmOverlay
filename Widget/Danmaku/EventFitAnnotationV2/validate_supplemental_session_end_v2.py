import hashlib
import json
import re
import sys
from collections import Counter
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")

ROOT = Path(__file__).resolve().parent
REPOSITORY_ROOT = ROOT.parents[2]
SOURCE_PATH = REPOSITORY_ROOT / "Widget" / "Danmaku" / "6657_memes.json"
POOL_PATH = ROOT / "supplemental_session_end_v2.json"
EXPECTED_INDICES = {69, 2419, 4933, 8585, 8664, 11268, 14236, 16943, 17331, 23089}
EXPECTED_FAMILIES = Counter({
    "refresh_disappointment": 4,
    "farewell": 2,
    "dont_leave": 2,
    "offline_meta": 2,
})
FAMILY_MARKERS = {
    "refresh_disappointment": re.compile(r"刷新.*(?:失望|崩溃).*(?:去哪了|人呢|刷新)"),
    "farewell": re.compile(r"结束|再见"),
    "dont_leave": re.compile(r"下播"),
    "offline_meta": re.compile(r"下播|主播没了|录播"),
}
FORBIDDEN_REFERENCES = re.compile(
    r"niko|s1mple|donk|zywoo|m0nesy|shroud|ququ|faze|navi|g2|vitality|spirit|falcons|猎鹰|绿龙|小蜜蜂",
    re.IGNORECASE,
)


def fail(message):
    raise SystemExit(f"FAIL: {message}")


def main():
    source = json.loads(SOURCE_PATH.read_text(encoding="utf-8-sig"))
    pool = json.loads(POOL_PATH.read_text(encoding="utf-8"))
    messages = pool.get("messages", [])

    if pool.get("event_label") != "session_end":
        fail("event_label must be session_end")
    if pool.get("selection_policy", {}).get("max_per_semantic_family_per_session") != 1:
        fail("semantic-family limit must be 1")
    if len(messages) != 10:
        fail(f"expected 10 messages, got {len(messages)}")

    seen_ids = set()
    seen_indices = set()
    seen_texts = set()
    family_counts = Counter()
    for position, item in enumerate(messages, 1):
        expected_id = f"session_end_src_{position:03d}"
        if item.get("id") != expected_id:
            fail(f"non-contiguous id at position {position}: {item.get('id')}")
        index = item.get("source_index")
        text = item.get("source_text", "")
        family = item.get("family")
        if not isinstance(index, int) or index < 1 or index > len(source):
            fail(f"invalid source index in {expected_id}: {index}")
        if source[index - 1] != text:
            fail(f"source text mismatch in {expected_id} (index {index})")
        digest = hashlib.sha256(text.encode("utf-8")).hexdigest()
        if item.get("sha256") != digest:
            fail(f"SHA-256 mismatch in {expected_id}")
        if "\n" in text or "\r" in text:
            fail(f"multiline source text in {expected_id}")
        if item["id"] in seen_ids or index in seen_indices or text in seen_texts:
            fail(f"duplicate id/index/text in {expected_id}")
        if family not in FAMILY_MARKERS or not FAMILY_MARKERS[family].search(text):
            fail(f"session-end semantic mismatch in {expected_id}: {text}")
        if FORBIDDEN_REFERENCES.search(text):
            fail(f"external/pro reference in {expected_id}: {text}")
        seen_ids.add(item["id"])
        seen_indices.add(index)
        seen_texts.add(text)
        family_counts[family] += 1

    if seen_indices != EXPECTED_INDICES:
        fail(f"unexpected reviewed source indices: {sorted(seen_indices)}")
    if family_counts != EXPECTED_FAMILIES:
        fail(f"unexpected family distribution: {dict(family_counts)}")

    print("PASS: supplemental session_end pool validated")
    print(f"total={len(messages)}")
    print("families=" + ", ".join(f"{key}:{family_counts[key]}" for key in sorted(family_counts)))


if __name__ == "__main__":
    main()
