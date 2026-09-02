import json
import re
import sys
from collections import Counter
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")

ROOT = Path(__file__).resolve().parent
POOL_PATH = ROOT / "supplemental_kill_praise_v2.json"
ALLOWED_INTENTS = {"short_hype", "aim_praise", "kill_hype", "aftermath_praise"}
ALLOWED_PHASES = {"burst", "aftermath", "both"}
EXPECTED_INTENTS = Counter({"short_hype": 40, "aim_praise": 40, "kill_hype": 40, "aftermath_praise": 20})
INTENT_MARKERS = {
    "short_hype": re.compile(r"牛|NB|nb|666|枪|准|帅|杀|秒|硬|神|秀|漂亮|可以|东西|起飞|无敌"),
    "aim_praise": re.compile(r"枪|准|瞄|头|秒|反应|定位|扫转|连点|细节|鼠标|正面|刚露|露头|机会|接住|弹无虚发"),
    "kill_hype": re.compile(r"杀|秒|人头|对面|服务器|顶得住|手感|接住|别想跑|枪|战神|实力|压制|操作|像开了|说法|正面|赏心悦目|看爽|流畅|行云|收下|又是一个|继续|再来一个|挡不住"),
    "aftermath_praise": re.compile(r"回放|集锦|十遍|能力|关键|人头|击杀|局面|突破口|站出来|救命|靠这枪|玩神|枪男|手感|准星|判断|枪法|帅|自信"),
}
NEGATIVE_MARKERS = re.compile(r"菜|白给|空枪|送了|下播|退役|被反杀|快跑|失败|输了|垃圾|不行")
FORBIDDEN_REFERENCES = re.compile(
    r"niko|s1mple|donk|zywoo|m0nesy|shroud|ququ|faze|navi|g2|vitality|spirit|falcons|猎鹰|绿龙|小蜜蜂",
    re.IGNORECASE,
)


def fail(message):
    raise SystemExit(f"FAIL: {message}")


def main():
    data = json.loads(POOL_PATH.read_text(encoding="utf-8"))
    messages = data.get("messages", [])
    if data.get("event_label") != "kill_praise":
        fail("event_label must be kill_praise")
    if len(messages) != 140:
        fail(f"expected 140 messages, got {len(messages)}")

    seen_ids = set()
    seen_texts = set()
    intent_counts = Counter()
    phase_counts = Counter()
    for position, item in enumerate(messages, 1):
        expected_id = f"kill_praise_supp_{position:03d}"
        if item.get("id") != expected_id:
            fail(f"non-contiguous id at position {position}: {item.get('id')}")
        text = item.get("text", "")
        intent = item.get("intent")
        phase = item.get("phase")
        if not text.strip() or text != text.strip():
            fail(f"invalid whitespace in {expected_id}")
        if "\n" in text or "\r" in text:
            fail(f"multiline text in {expected_id}")
        if len(text) > 24:
            fail(f"kill praise is too long in {expected_id}: {text}")
        if item["id"] in seen_ids or text in seen_texts:
            fail(f"duplicate id/text in {expected_id}: {text}")
        if intent not in ALLOWED_INTENTS or not INTENT_MARKERS[intent].search(text):
            fail(f"kill-praise intent mismatch in {expected_id}: {text}")
        if phase not in ALLOWED_PHASES:
            fail(f"invalid phase in {expected_id}: {phase}")
        if NEGATIVE_MARKERS.search(text):
            fail(f"negative/death wording leaked into {expected_id}: {text}")
        if FORBIDDEN_REFERENCES.search(text):
            fail(f"external/pro reference in {expected_id}: {text}")
        seen_ids.add(item["id"])
        seen_texts.add(text)
        intent_counts[intent] += 1
        phase_counts[phase] += 1

    if intent_counts != EXPECTED_INTENTS:
        fail(f"unexpected intent distribution: {dict(intent_counts)}")

    print("PASS: supplemental kill_praise pool validated")
    print(f"total={len(messages)}")
    print("intents=" + ", ".join(f"{key}:{intent_counts[key]}" for key in sorted(intent_counts)))
    print("phases=" + ", ".join(f"{key}:{phase_counts[key]}" for key in sorted(phase_counts)))


if __name__ == "__main__":
    main()
