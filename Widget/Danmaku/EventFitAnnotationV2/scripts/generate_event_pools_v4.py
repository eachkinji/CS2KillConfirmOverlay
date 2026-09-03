#!/usr/bin/env python3
"""Build large, source-traceable, event-specific danmaku pools.

Every generated line keeps a 1-based source index and an exact excerpt from
6657_memes.json.  The event-specific lead is transformation scaffolding; the
source excerpt supplies the vocabulary and meme voice.
"""

from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path


DANMAKU_ROOT = Path(__file__).resolve().parents[2]
ANNOTATION_ROOT = DANMAKU_ROOT / "Annotation"
EVENT_POOL_ROOT = DANMAKU_ROOT / "EventPools"
LIFECYCLE_POOL_ROOT = DANMAKU_ROOT / "LifecyclePools"
TARGET_COUNT = 1000

BAD_TARGETS = {"pro_player", "pro_team", "external_figure", "caster_host"}
STYLE_STANCES = {
    "tease_playful",
    "cynical_sarcastic",
    "hype_excitement",
    "cheer_praise",
    "flame_streamer",
}
REQUIRED_TARGETS = {"streamer", "game_system", "chat_audience"}
OFF_TOPIC_TOPICS = {
    "acg_popculture",
    "external_sports_competition",
    "external_figure_personal_life",
    "pro_scene_drama",
    "pro_transfer_roster",
    "pro_trophy_history",
    "pro_ranking_rating",
    "pro_sniper_awp",
}
PRO_TEXT_RE = re.compile(
    r"(?i)(NiKo|s1mple|donk|ZywOo|载物|dev1ce|地外丝|karrigan|大表哥|表猪|"
    r"m0NESY|sh1ro|若子|broky|ropz|twistzz|总监|aleksib|小李子|electronic|"
    r"cadian|点子哥|snax|fallen|tarik|shroud|kennyS|coldzera|flusha|stewie2k|"
    r"tenz|ququ|佳代子|伟伟|马西西|冬瓜强|茄子|老汤|马圣|阿杜|dupreeh|"
    r"FaZe|Falcons|猎鹰|Vitality|小蜜蜂|Spirit|绿龙|Navi|MOUZ|老鼠|G2|"
    r"Astralis|Heroic|Virtus|Cloud9|Liquid|液体|Complexity|FURIA|黑豹|BLG|TES|T1|EDG)"
)

FRAMES = (
    "{lead}，{excerpt}",
    "{lead}：{excerpt}",
    "{lead}，难怪弹幕说{excerpt}",
    "{lead}，只能拿原话评价：{excerpt}",
    "{lead}，这下真成了{excerpt}",
    "{lead}，先别急着吹，{excerpt}",
    "{lead}，直播间当场复读：{excerpt}",
    "{lead}，看完只想说{excerpt}",
    "{lead}，这波配得上{excerpt}",
    "{lead}，结果满屏都在刷：{excerpt}",
)

EVENTS = {
    "kill": {
        "keywords": ("杀", "击杀", "枪法", "人头", "秒了", "拿下", "nb", "牛"),
        "topics": ("streamer_skill_gameplay", "pro_gunplay_aim", "pro_headshot"),
        "leads": (
            "刚杀一个就开始装了", "这波击杀居然真让他拿到了", "人头到账，玩神立刻上嘴脸",
            "这枪打中以后直播间都沉默了", "偶尔杀一个，闹麻了", "击杀提示一亮，玩神又觉得自己行了",
            "对面送来一个人头", "这次击杀先记玩神一功", "真让他杀到了一个", "拿到击杀以后猪头都摇起来了",
        ),
    },
    "first_kill": {
        "keywords": ("首杀", "一血", "第一个", "开张", "杀", "拿下"),
        "topics": ("pro_entry_trade", "pro_score_outcome", "streamer_skill_gameplay"),
        "leads": (
            "首杀刚拿到就开始邀功了", "一血到账，玩神终于开张", "第一个人头居然是他拿的",
            "首杀提示一亮，直播间先笑了", "这把总算不是零杀开局", "拿个首杀就摇起来了",
            "玩神首杀成功，像过年一样", "一血到手，先别急着吹", "首杀真让他混到了", "刚开局就拿首杀，今天不对劲",
        ),
    },
    "headshot": {
        "keywords": ("爆头", "一枪头", "颗秒", "锁头", "头线", "枪法", "定位"),
        "topics": ("pro_headshot", "pro_gunplay_aim", "streamer_skill_gameplay"),
        "leads": (
            "爆头到账，先检查是不是蒙的", "这一枪居然真打到头了", "颗秒以后玩神立刻膨胀",
            "头线碰巧对上了一次", "爆头提示出来全直播间都惊了", "这枪一发入魂，多少有点陌生",
            "玩神打出爆头，像中了大奖", "一枪头拿下，先别急着装", "真锁到头了，今天有挂相", "爆头了，对面估计也没想到",
        ),
    },
    "knife_kill": {
        "keywords": ("刀", "近战", "背刺", "鞭尸"),
        "topics": ("pro_knife_melee", "pro_whiff_blunder"),
        "leads": (
            "真让玩神把人刀了", "刀杀到账，对面可以退网了", "这把刀掏出来居然不是送",
            "近战击杀成功，节目效果拉满", "一刀下去对面道心碎了", "刀到人头，玩神开始摇了",
            "对面被刀，今晚估计睡不着", "玩神刀人成功，多少有点难看", "这次刀杀够对面记一年", "刀都能杀到，今天确实怪",
        ),
    },
    "grenade_kill": {
        "keywords": ("雷", "炸", "投掷物", "手雷", "道具"),
        "topics": ("pro_utility_grenade", "pro_whiff_blunder"),
        "leads": (
            "一颗雷居然真炸死人了", "手雷击杀到账，玩神又懂道具了", "这雷扔完对面直接没了",
            "投掷物收人头，枪法暂时下班", "对面被雷活活炸死，太配合了", "雷一响，人头到账",
            "玩神这颗雷总算没炸队友", "炸到人以后立刻开始讲道理", "这次手雷有点东西，但不多", "一雷收掉，节目效果有了",
        ),
    },
    "multi_kill": {
        "keywords": ("多杀", "双杀", "三杀", "四杀", "五杀", "连杀", "乱杀"),
        "topics": ("pro_multikill_clutch", "pro_gunplay_aim", "streamer_skill_gameplay"),
        "leads": (
            "连着杀人以后玩神已经不认识自己了", "多杀提示一出来，直播间开始做梦", "这波居然让他连续收了几个",
            "双杀三杀往上叠，猪头也跟着摇", "连杀到账，先看对面是不是挂机", "玩神突然多杀，服务器都不习惯",
            "这波连收几个，多少有点离谱", "多杀成功，今天可以少喷两句", "连续击杀以后立刻开始装高手", "一口气杀这么多，对面真给面子",
        ),
    },
    "epic_streak": {
        "keywords": ("连杀", "五杀", "超神", "无敌", "暴走", "乱杀", "杀疯"),
        "topics": ("pro_multikill_clutch", "pro_ranking_rating", "streamer_skill_gameplay"),
        "leads": (
            "高连杀以后玩神已经开始飞了", "这波杀疯，先查对面是不是人机", "连杀数字越高，猪头摇得越快",
            "玩神居然打出高连杀，服务器出问题了", "连续乱杀，今天真让他装到了", "超神提示一出，全直播间都不敢信",
            "这段连杀够他吹一个月", "杀到停不下来，对面排队送是吧", "高连杀到账，暂时允许叫一声神", "一路连杀，玩神今晚又睡不着了",
        ),
    },
    "last_kill": {
        "keywords": ("最后", "末杀", "收尾", "残局", "赛点", "拿下"),
        "topics": ("pro_multikill_clutch", "pro_score_outcome", "pro_comeback_choke"),
        "leads": (
            "最后一杀真让玩神收掉了", "收尾成功，今天居然没掉链子", "末杀到账，比赛总算结束",
            "关键最后一枪居然打中了", "赛点由玩神收尾，多少有点惊险", "残局最后一个人头拿下",
            "玩神完成末杀，立刻开始邀功", "最后一杀到手，先别急着复盘", "这次真把最后一个收了", "收尾以后猪头已经摇起来了",
        ),
    },
    "assist": {
        "keywords": ("助攻", "补枪", "配合", "拉枪线", "跟枪"),
        "topics": ("pro_entry_trade", "pro_tactics_igl", "streamer_skill_gameplay"),
        "leads": (
            "助攻到账，玩神也算参与了", "这波补枪没拿头，只拿到助攻", "队友杀完，玩神负责领助攻",
            "配合成功，虽然主要不是他的功劳", "助攻提示亮了，先混一份荣誉", "拉枪线拉出一个助攻，也算有用",
            "补到一点伤害，助攻稳稳入账", "玩神混到助攻，立刻开始指挥", "这波团队配合里终于有他", "助攻加一，存在感也加一",
        ),
    },
    "death": {
        "keywords": ("死", "阵亡", "白给", "空枪", "菜", "送", "马枪"),
        "topics": ("pro_whiff_blunder", "streamer_skill_gameplay", "streamer_stubborn_rage"),
        "leads": (
            "这波一死，弹幕直接开喷", "又白给一个，问号先刷起来", "阵亡提示一亮，全直播间都绷不住了",
            "空枪空到把自己送了", "这次死得太有节目效果", "玩神倒下，菜字已经刷屏",
            "人刚死，锅已经甩出去了", "这波阵亡只能说确实有水平", "送完这一波还准备怪谁", "又死了，先把问号和菜端上来",
        ),
    },
    "round_win": {
        "keywords": ("赢", "胜利", "拿下", "翻盘", "回合"),
        "topics": ("pro_score_outcome", "pro_comeback_choke", "streamer_skill_gameplay"),
        "leads": (
            "回合赢了，玩神立刻开始邀功", "这一分居然真拿下了", "胜利提示出来以后猪头摇起来了",
            "赢个回合就觉得全是自己功劳", "这回合拿下，暂时允许吹一下", "队友带赢以后玩神先笑了",
            "回合胜利到账，先别急着上嘴脸", "真赢了，直播间反而不习惯", "这一分拿得多少有点难看", "回合结束，玩神又觉得战术全对",
        ),
    },
    "round_loss": {
        "keywords": ("输", "失败", "没了", "打不过", "丢分", "回合"),
        "topics": ("pro_score_outcome", "pro_comeback_choke", "pro_whiff_blunder"),
        "leads": (
            "回合一输，经典甩锅马上开始", "这分丢完，弹幕已经笑了", "失败提示一出，玩神立刻沉默",
            "又输一回合，先想想怪谁", "这回合没了，嘴巴准备上班", "打不过就算了，节目效果倒是有了",
            "回合输掉以后猪头也不摇了", "这分送得太有6657风格", "又丢一分，弹幕开始清算", "输了回合还想装没事发生",
        ),
    },
    "bomb_plant": {
        "keywords": ("下包", "C4", "埋包", "包点", "炸弹"),
        "topics": ("pro_utility_grenade", "pro_tactics_igl", "streamer_skill_gameplay"),
        "leads": (
            "C4终于下好了，玩神开始当指挥", "下包成功，至少这次没把包忘家里", "炸弹埋下以后立刻开始讲战术",
            "包点下包完成，接下来别白给", "玩神把C4放下，像完成大工程", "下完包就开始摇，先看看守不守得住",
            "这次真把包下进去了", "C4计时开始，玩神的嘴也开始了", "埋包成功，多少算干了点正事", "包一下好，全直播间先松口气",
        ),
    },
    "bomb_defuse": {
        "keywords": ("拆包", "拆弹", "钳子", "C4", "炸弹"),
        "topics": ("pro_utility_grenade", "pro_tactics_igl", "streamer_skill_gameplay"),
        "leads": (
            "C4真让玩神拆掉了", "拆包成功，居然没算错时间", "炸弹拆完以后立刻开始装专业",
            "钳子一掏，这次总算没白忙", "玩神完成拆弹，多少有点不真实", "拆包到账，先看看是不是队友保的",
            "C4停止倒计时，全直播间都松了口气", "这次真把炸弹拆了", "拆弹成功，玩神又觉得自己懂了", "包拆下来以后猪头开始摇了",
        ),
    },
    "hostage_interact": {
        "keywords": ("人质", "绑架", "接触", "救援"),
        "topics": ("streamer_skill_gameplay", "pro_tactics_igl", "misc_unclear"),
        "leads": (
            "玩神一碰人质，救援片立刻变喜剧", "接触人质成功，先别把人再送回去", "人质看到玩神靠近估计更紧张了",
            "开始营救人质，节目效果也开始了", "玩神终于找到人质在哪", "这一碰人质，绑匪都沉默了",
            "人质交到玩神手里多少有点危险", "救援刚开始，先别急着邀功", "接触人质以后立刻开始指挥", "玩神准备带人质走，先替人质担心一下",
        ),
    },
    "hostage_rescue": {
        "keywords": ("人质", "救出", "救援", "营救", "撤离"),
        "topics": ("streamer_skill_gameplay", "pro_score_outcome", "pro_tactics_igl"),
        "leads": (
            "人质居然真被玩神救出来了", "营救成功，人质总算脱离危险", "带着人质撤离成功，今天确实怪",
            "玩神完成救援，绑匪都没想到", "人质安全送达，先记玩神一功", "这次真把人质带出来了",
            "救援完成以后猪头已经摇起来了", "人质获救，玩神立刻开始邀功", "撤离成功，至少没把人质带丢", "救出人质以后全直播间都惊了",
        ),
    },
}


def read_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def exact_excerpt(text: str) -> str:
    text = text.strip()
    address = re.match(r"^@[^：:]{1,28}[：:]\s*", text)
    if address and len(text[address.end() :].strip()) >= 6:
        text = text[address.end() :].strip()
    clauses = [part.strip() for part in re.split(r"[。！？!?；;]", text) if len(part.strip()) >= 6]
    excerpt = min(clauses, key=len) if clauses else text
    if len(excerpt) > 34:
        excerpt = excerpt[:34].rstrip("，、：:,.。！？!?；; ")
    if len(excerpt) < 4:
        excerpt = text[:34].rstrip()
    return excerpt


def stable_number(value: str) -> int:
    return int(hashlib.sha256(value.encode("utf-8")).hexdigest()[:16], 16)


def candidate_score(event: str, annotation: dict, text: str) -> int:
    config = EVENTS[event]
    score = 0
    if any(keyword.lower() in text.lower() for keyword in config["keywords"]):
        score += 20
    score += 7 * len(set(annotation.get("topics", ())) & set(config["topics"]))
    stances = set(annotation.get("stances", ()))
    score += 4 * len(stances & STYLE_STANCES)
    targets = set(annotation.get("targets", ()))
    if "streamer" in targets:
        score += 6
    if "game_system" in targets:
        score += 3
    if len(text) <= 36:
        score += 3
    if "origin_6657" in annotation.get("culture", ()):
        score += 1
    return score


def source_candidates(memes: list[str], annotations: list[dict]) -> list[tuple[dict, str, str]]:
    result = []
    for annotation in annotations:
        index = int(annotation["index"])
        text = str(memes[index - 1]).strip()
        targets = set(annotation.get("targets", ()))
        stances = set(annotation.get("stances", ()))
        topics = set(annotation.get("topics", ()))
        if targets & BAD_TARGETS or not (targets & REQUIRED_TARGETS):
            continue
        if topics & OFF_TOPIC_TOPICS or not (stances & STYLE_STANCES):
            continue
        if annotation.get("entities"):
            continue
        if not 4 <= len(text) <= 72 or "\n" in text or "\r" in text:
            continue
        if "http://" in text or "https://" in text or PRO_TEXT_RE.search(text):
            continue
        excerpt = exact_excerpt(text)
        if excerpt not in text or len(excerpt) < 4:
            continue
        result.append((annotation, text, excerpt))
    return result


def seed_entries(event: str) -> list[dict]:
    current_path = EVENT_POOL_ROOT / f"{event}.json"
    if current_path.exists():
        current = read_json(current_path)
        reviewed = [
            entry
            for entry in current.get("entries", ())
            if entry.get("template_id") == "reviewed_seed"
        ]
        if reviewed:
            return reviewed
    return []


def build_event_pool(event: str, memes: list[str], candidates: list[tuple[dict, str, str]]) -> dict:
    config = EVENTS[event]
    entries = []
    seen_texts = set()
    used_sources = set()

    for seed in seed_entries(event):
        text = re.sub(r"[\r\n]+", " ", str(seed["text"])).strip()
        excerpt = str(seed["source_excerpt"])
        source_index = int(seed["source_index"])
        canonical_event_term = config["keywords"][0]
        if canonical_event_term.lower() not in text.lower():
            text = f"{config['leads'][0]}，{text}"
        if event == "death" and stable_number(f"death-question:{source_index}") % 4 == 0:
            text = text.rstrip("。！!？?") + "？？？"
        if source_index in used_sources or excerpt not in str(memes[source_index - 1]) or text in seen_texts:
            continue
        entries.append(
            {
                "id": f"{event}_{len(entries) + 1:04d}",
                "source_index": source_index,
                "source_excerpt": excerpt,
                "text": text,
                "phase": seed.get("phase", "both"),
                "derivation": "context_rewrite",
                "template_id": "reviewed_seed",
            }
        )
        seen_texts.add(text)
        used_sources.add(source_index)

    ranked = sorted(
        candidates,
        key=lambda item: (
            -candidate_score(event, item[0], item[1]),
            stable_number(f"{event}:{item[0]['index']}"),
        ),
    )
    for annotation, _source_text, excerpt in ranked:
        if len(entries) >= TARGET_COUNT:
            break
        source_index = int(annotation["index"])
        if source_index in used_sources:
            continue
        selector = stable_number(f"template:{event}:{source_index}")
        lead_index = selector % len(config["leads"])
        frame_index = (selector // len(config["leads"])) % len(FRAMES)
        lead = config["leads"][lead_index]
        rendered_excerpt = excerpt + ("…" if len(excerpt) == 34 else "")
        text = FRAMES[frame_index].format(lead=lead, excerpt=rendered_excerpt)
        text = re.sub(r"\s+", " ", text).strip()
        canonical_event_term = config["keywords"][0]
        if canonical_event_term.lower() not in text.lower():
            text = f"{canonical_event_term}，{text}"
        if event == "death" and selector % 4 == 0:
            text = text.rstrip("。！!？?") + "？？？"
        if text in seen_texts or "\n" in text or "\r" in text:
            continue
        entries.append(
            {
                "id": f"{event}_{len(entries) + 1:04d}",
                "source_index": source_index,
                "source_excerpt": excerpt,
                "text": text,
                "phase": "burst" if len(entries) < 350 else "aftermath",
                "derivation": "context_rewrite",
                "template_id": f"{event}_l{lead_index + 1:02d}_f{frame_index + 1:02d}",
            }
        )
        seen_texts.add(text)
        used_sources.add(source_index)

    if len(entries) < TARGET_COUNT:
        raise RuntimeError(f"{event}: only generated {len(entries)} unique entries")
    return {
        "schema_version": "4.0.0",
        "pool_type": "event",
        "event": event,
        "source": "Widget/Danmaku/6657_memes.json",
        "index_base": 1,
        "minimum_entries": TARGET_COUNT,
        "policy": "Every line is an event-context rewrite retaining an exact excerpt from one indexed 6657 source line.",
        "entries": entries,
    }


def write_json(path: Path, value: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main() -> None:
    memes = read_json(DANMAKU_ROOT / "6657_memes.json")
    annotations = read_json(ANNOTATION_ROOT / "6657_annotations_v1.json")["annotations"]
    if len(memes) != len(annotations):
        raise RuntimeError("Source and annotation counts do not match")
    candidates = source_candidates(memes, annotations)
    if len(candidates) < TARGET_COUNT:
        raise RuntimeError(f"Only {len(candidates)} eligible source lines")

    EVENT_POOL_ROOT.mkdir(parents=True, exist_ok=True)
    for event in EVENTS:
        pool = build_event_pool(event, memes, candidates)
        write_json(EVENT_POOL_ROOT / f"{event}.json", pool)
        print(f"{event}: {len(pool['entries'])}")

    lifecycle_sources = {
        "opening_wait": LIFECYCLE_POOL_ROOT / "opening_wait.json",
        "session_end": LIFECYCLE_POOL_ROOT / "session_end.json",
    }
    for name, source_path in lifecycle_sources.items():
        if not source_path.exists():
            raise RuntimeError(f"Missing lifecycle source: {source_path}")
        data = read_json(source_path)
        data["schema_version"] = "4.0.0"
        data["pool_type"] = "lifecycle"
        data["lifecycle"] = name
        write_json(LIFECYCLE_POOL_ROOT / f"{name}.json", data)
        print(f"{name}: {len(data['entries'])}")


if __name__ == "__main__":
    main()
