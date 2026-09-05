import json
import re
import sys
from collections import Counter
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")

ROOT = Path(__file__).resolve().parent
POOL_PATH = ROOT / "supplemental_opening_wait_v2.json"
ALLOWED_INTENTS = {"open_door", "urge_start", "waiting", "arrival"}
ALLOWED_PHASES = {"burst", "aftermath", "both"}
INTENT_MARKERS = {
    "open_door": re.compile(r"门|营业"),
    "urge_start": re.compile(r"播|上班|开工|上工|营业|启动|打卡|请假|迟到|潜水|准时|鸽|陪|按钮|电源|摄像头|麦克风|刷新"),
    "waiting": re.compile(r"等|蹲|刷新|黑屏|迟到|准时|叫醒|睡醒|睡过头|催|没来|不来|还没播|前排|倒计时|长草"),
    "arrival": re.compile(r"终于|开播|来了|开了|开得|会来|等到|开饭|上线|上班|亮了|回归|营业|回来|声音|黑屏|准时|没有鸽"),
}
FORBIDDEN_REFERENCES = re.compile(
    r"niko|s1mple|donk|zywoo|m0nesy|shroud|ququ|faze|navi|g2|vitality|spirit|falcons|猎鹰|绿龙|小蜜蜂",
    re.IGNORECASE,
)


def fail(message):
    raise SystemExit(f"FAIL: {message}")


def main():
    data = json.loads(POOL_PATH.read_text(encoding="utf-8"))
    messages = data.get("messages", [])
    if data.get("event_label") != "opening_wait":
        fail("event_label must be opening_wait")
    if len(messages) != 140:
        fail(f"expected 140 messages, got {len(messages)}")

    ids = set()
    texts = set()
    intent_counts = Counter()
    phase_counts = Counter()
    for position, item in enumerate(messages, 1):
        expected_id = f"opening_wait_supp_{position:03d}"
        if item.get("id") != expected_id:
            fail(f"non-contiguous id at position {position}: {item.get('id')}")
        text = item.get("text", "")
        intent = item.get("intent")
        phase = item.get("phase")
        if not text.strip() or text != text.strip():
            fail(f"invalid whitespace for {expected_id}")
        if "\n" in text or "\r" in text:
            fail(f"multiline text found in {expected_id}")
        if len(text) > 28:
            fail(f"opening text is too long in {expected_id}: {text}")
        if item["id"] in ids or text in texts:
            fail(f"duplicate id/text at {expected_id}: {text}")
        if intent not in ALLOWED_INTENTS:
            fail(f"invalid intent in {expected_id}: {intent}")
        if phase not in ALLOWED_PHASES:
            fail(f"invalid phase in {expected_id}: {phase}")
        if not INTENT_MARKERS[intent].search(text):
            fail(f"text does not express {intent} in {expected_id}: {text}")
        if FORBIDDEN_REFERENCES.search(text):
            fail(f"external/pro reference in {expected_id}: {text}")
        ids.add(item["id"])
        texts.add(text)
        intent_counts[intent] += 1
        phase_counts[phase] += 1

    if intent_counts != Counter({"open_door": 40, "urge_start": 40, "waiting": 30, "arrival": 30}):
        fail(f"unexpected intent distribution: {dict(intent_counts)}")

    print("PASS: supplemental opening_wait pool validated")
    print(f"total={len(messages)}")
    print("intents=" + ", ".join(f"{key}:{intent_counts[key]}" for key in sorted(intent_counts)))
    print("phases=" + ", ".join(f"{key}:{phase_counts[key]}" for key in sorted(phase_counts)))


if __name__ == "__main__":
    main()
