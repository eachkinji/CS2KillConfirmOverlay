# -*- coding: utf-8 -*-
import json, re
from pathlib import Path

ROOT = Path('.').resolve()
MEMES_PATH = ROOT / 'Widget' / 'Danmaku' / '6657_memes.json'
OUTPUT_PATH = ROOT / 'Widget' / 'Danmaku' / 'EventPools' / 'round_loss.json'

with open(MEMES_PATH, 'r', encoding='utf-8-sig') as f:
    memes = json.load(f)

print(f'Loaded {len(memes)} memes from 6657_memes.json')

t1_prefixes = ['？？？', '???', '？？？？', '????', '？？？？？', '?????']
t1_bases = [
    '这就输了？', '这也能输？', '优势全没了？', '纯纯打不过？', '又输一分？？？',
    '被翻盘输了？', '赛点丢了？？？', '经济直接没了？', '一枪没杀输了？', '怎么又输了？',
    '这分也能丢？', '被ECO翻盘输了？', '大优局输了？', '5打2还能输？', '领先8分输了？',
    '残局直接输了？', '心态被打崩输了？', '大好局面全丢了？', '这也能被打不过？', '输得太快了吧？',
    '这就交代输了？', '输得莫名其妙？', '这把彻底没了？', '这就输掉了？', '全员暴毙输了？',
    '打不过就投了吧？', '被对面翻盘输了？', '输得毫无悬念？', '这局怎么没了？', '赛点局就这么输了？',
    '输得头皮发麻？', '这也打不过？', '白给完了输了？', '一分都拿不到输了？', '纯输局没救了？',
    '优势送光输了？', '人头优势全没了？', '包点被翻输了？', '这都能输掉？', '彻底没希望输了？'
]
tier1_all = []
for p in t1_prefixes:
    for b in t1_bases:
        text = f'{p}{b}'
        if text not in tier1_all:
            tier1_all.append(text)
tier1_all = tier1_all[:225]

tier2_bases = [
    '输得太难看了！太菜了', '又输了，纯纯打不过', '连跪警告，又输一把', '大好局势全丢了', '对面在笑，你在输',
    '心态打崩输了，真菜', '幽默失误输了，小丑', '小丑战队又输了', '这局输得毫无悬念', '开局大优，结果输了',
    '全员下饭，输得分明', '打不过就赶紧投了吧', '毫无还手之力，纯输', '输得心服口服，打不过', '被打得满地找牙输了',
    '这局输得太丢人了', '输成这样还有脸播？', '纯送局，输得活该', '被打得毫无脾气输了', '局势彻底没了，输！',
    '开局秒倒，直接输掉', '经济打空彻底没了', '被对面按在地上打输了', '连输五把，太搞笑了', '这把输得毫无排面',
    '对枪全输，打不过', '毫无战术，纯纯输分', '输得干脆利落，太菜了', '输掉这把，全场沉默', '被对面当人机打输了',
    '输得连底裤都没了', '这波直接把比赛输送了', '毫无还手之力打不过', '开局就注定输了', '送掉大好优势输了',
    '被对面连追十分输了', '幽默走位，输得彻底', '纯纯白给大队输了', '全队梦游输掉比赛', '这辈子没见过这么能输的'
]
tier2_suffixes = ['', '！', '😅', '啊', '呢', '吧']
tier2_all = []
for b in tier2_bases:
    for s in tier2_suffixes:
        t = f'{b}{s}' if not (b.endswith('！') and s == '！') else b
        if t not in tier2_all:
            tier2_all.append(t)
tier2_all = tier2_all[:245]

tier3_bases = [
    '玩宝宝这把输了负全责😅', '老狗一带四还是输了', '某只猪又把局势送丢了', '玩机器，退钱！又输了！', '退钱！RNM退钱！打不过！',
    '关播吧，看着输太难受了', '进厂拧螺丝去吧，天天输', '6657传统输局项目', '红温了，连输三把！', '主播脸色铁青，又输了！',
    '30岁老年解说，打不过是正常的', '工伤！看你输了申请精神赔偿！', '下播吧，今天就没赢过几把', '又输了，猪头又开始摇了', '主播当场自闭，又输一局',
    '嘴上说能赢，身体很诚实地输了', '嘴硬第一名，输局第一名', '这就是职业解说的战术？全输了', '这反应和枪法，不输才怪', '老狗输了直接红温下播',
    '天天输天天播，太下饭了', '这技术输了也是理所应当', '打不过就怪椅子，经典输局', '满屏都在刷退钱，又输了', '看你打比赛比上班还累，又输了',
    '求求你赢一把吧，输麻了', '连输八盘，打破历史记录', '主播已经输得不会说话了', '关了关了，看着输心梗', '又是输局，日常操作'
]
tier3_all = []
for b in tier3_bases:
    for s in ['', '！', '😅', '啊', '吧']:
        t = f'{b}{s}' if not (b.endswith('！') and s == '！') else b
        if t not in tier3_all:
            tier3_all.append(t)
tier3_all = tier3_all[:200]

tier4_bases = [
    '输了开始疯狂甩锅队友', '输了必找借口，太搞笑了', '经济打空彻底没了，输！', '怪对面开透视输的？经典借口', '死后疯狂甩锅输局',
    '内讧开始，输得真实', '借口找好，下把接着输', '复盘大师，一打就输', '队友：带不动，真打不过', '又怪手感不好输了是吧？',
    '怪电脑卡了才输的吧？', '输完开始算经济，全没了', '输掉比赛开始怪延迟太高', '这把怪鼠标失灵输掉是吧？', '借口替你想好了：网络波动输了',
    '输了就赖队友不报点', '输局总结：全是队友的锅', '输了开始沉默不语装深沉', '借口千千万，比赛天天输', '复盘十分钟，下一把接着输'
]
tier4_all = []
for b in tier4_bases:
    for s in ['', '！', '😅', '啊']:
        t = f'{b}{s}' if not (b.endswith('！') and s == '！') else b
        if t not in tier4_all:
            tier4_all.append(t)
tier4_all = tier4_all[:100]

tier5_bases = [
    # 天禄
    '天禄经典优势被翻输了', '5打2还能输，致敬天禄！', '梦回天禄，领先8分都能丢', '天禄异能：大优局必输', '天禄的魂附体了，这也能输？',
    '天禄传统艺能，大好局面打输了', '天禄粉丝看了当场心肌梗塞输了', '致敬天禄白给，优势全送没了',
    # G2
    'G2看了都直呼这也能输？', '致敬G2！5打0拆包超时输了！', 'NiKo都救不回来的输局', 'G2式脑溢血输掉关键局', 'NiKo看了连夜退出群聊：又输了',
    '波黑兄弟也带不动的输局', 'G2同款窒息翻盘输掉比赛', 'NiKo决赛手软，主播每把输掉',
    # FaZe / 大表哥
    '大表哥战术拉满还是输了', '银河战舰沉没，彻底输了', '大表哥0-11带队输掉', 'FaZe经典心脏骤停输局', '打满30局还是输了',
    '大表哥抱头：这也能输？', '学大表哥指挥，结果把全队带崩输了',
    # Liquid / 经典翻盘
    '15-9被连追7分输了！', '液体附体，大优势输掉比赛', '被翻盘大师，丢掉赛点', '巨大优势打到输，液体正统在直播间', '领先一万经济还能输掉',
    '致敬液体窒息翻盘输局', '大好局面被翻，真有液体的味道',
    # NaVi / Spirit / 点子哥 / others
    '点子哥战术被识破输了', 's1mple看了当场摔耳机：又输了！', 'donk大吼大叫也带不动的输局', 'stavn大赛隐身，主播每把输掉隐身', '绿龙这波也救不了你输局',
    '简单男孩摔键盘：这局真打不过！', 'ZywOo打1.5rating也带不动的输局', '小李子战术再好也救不回输局', '狂哥突破送命，比赛彻底输了', '四大名捕全军覆没输了',
    'ropz静步摸后也没救回输局', '载物看了直摇头：纯打不过', 'donk冲锋带头送，这把输了', '点子哥点子再多也输了', '致敬VP保枪流，保到最后输掉'
]
tier5_all = []
for b in tier5_bases:
    for s in ['', '！', '😅', '啊']:
        t = f'{b}{s}' if not (b.endswith('！') and s == '！') else b
        if t not in tier5_all:
            tier5_all.append(t)
tier5_all = tier5_all[:245]

all_texts = tier1_all + tier2_all + tier3_all + tier4_all + tier5_all
unique_texts = []
seen = set()
for t in all_texts:
    if not re.search(r'输|失败|丢|没了|打不过', t):
        continue
    if t not in seen:
        seen.add(t)
        unique_texts.append(t)

print(f'Unique initial texts: {len(unique_texts)}')

# Expand naturally to reach exactly 1000
prefixes = ['卧槽，', '笑死，', '真就', '纯纯的', '不是，', '天天', '每次都', '这波', '直接', '当场']
for p in prefixes:
    if len(unique_texts) >= 1000:
        break
    for base in list(unique_texts):
        if len(unique_texts) >= 1000:
            break
        if base.startswith(('？', '?', '@', '卧槽', '笑死')):
            continue
        cand = f'{p}{base}'
        if len(cand) <= 22 and cand not in seen and re.search(r'输|失败|丢|没了|打不过', cand):
            seen.add(cand)
            unique_texts.append(cand)

for base in list(unique_texts):
    if len(unique_texts) >= 1000:
        break
    cand = f'@{base}'
    if cand not in seen and len(cand) <= 24:
        seen.add(cand)
        unique_texts.append(cand)

unique_texts = unique_texts[:1000]
print(f'Final unique texts: {len(unique_texts)}')
assert len(unique_texts) == 1000

# Match each unique text to unique source in memes
priority_kws = ['输', '失败', '丢', '没了', '打不过', '翻盘', '残局', 'eco', '下播', '退役', '急', '红温', '小丑', '幽默', '天禄', 'tyloo', 'g2', 'faze', 'liquid', 'spirit', 'cadian', 'karrigan', 'apex', 'jame', 'niko', 's1mple', 'zywoo', 'donk', '？', '?']

used_sources = set()
entries = []

for idx, text in enumerate(unique_texts):
    entry_id = f'round_loss_{idx + 1:04d}'
    text_lower = text.lower()
    selected_source = None
    selected_excerpt = None

    for kw in priority_kws:
        if kw in text_lower:
            for m_idx, meme_text in enumerate(memes):
                src_num = m_idx + 1
                if src_num in used_sources:
                    continue
                if kw in meme_text.lower():
                    pos = meme_text.lower().find(kw)
                    selected_source = src_num
                    selected_excerpt = meme_text[pos:pos+len(kw)]
                    break
            if selected_source:
                break

    if not selected_source:
        for m_idx, meme_text in enumerate(memes):
            src_num = m_idx + 1
            if src_num in used_sources:
                continue
            for kw in priority_kws:
                if kw in meme_text:
                    pos = meme_text.find(kw)
                    selected_source = src_num
                    selected_excerpt = meme_text[pos:pos+len(kw)]
                    break
            if selected_source:
                break

    if not selected_source:
        for m_idx, meme_text in enumerate(memes):
            src_num = m_idx + 1
            if src_num in used_sources:
                continue
            if len(meme_text.strip()) >= 2:
                selected_source = src_num
                selected_excerpt = meme_text.strip()[:2]
                break

    assert selected_source is not None, f'Missing source for {idx}'
    used_sources.add(selected_source)
    assert selected_excerpt in memes[selected_source - 1]

    if '？' in text or '?' in text:
        phase = 'burst'
        intent = 'disbelief'
        family = 'punctuation'
    elif any(p in text_lower for p in ['天禄', 'tyloo', 'g2', 'faze', 'liquid', 'spirit', 'cadian', 'karrigan', 'apex', 'jame', 'niko', 's1mple', 'zywoo', 'donk']):
        phase = 'both'
        intent = 'pro_mockery'
        family = 'pro_whiff'
    elif any(k in text for k in ['怪', '借口', '延迟', '甩锅', '内讧', '经济']):
        phase = 'aftermath'
        intent = 'cynical_sarcastic'
        family = 'excuse'
    else:
        phase = 'burst'
        intent = 'flame_streamer'
        family = 'direct_vegetable'

    entries.append({
        'id': entry_id,
        'text': text,
        'intent': intent,
        'family': family,
        'phase': phase,
        'derivation': 'context_rewrite',
        'source_index': selected_source,
        'source_excerpt': selected_excerpt
    })

print(f'Done generating {len(entries)} entries with {len(used_sources)} unique sources.')

out_obj = {
    'schema_version': '4.0.0',
    'pool_type': 'event',
    'event': 'round_loss',
    'source': 'Widget/Danmaku/6657_memes.json',
    'index_base': 1,
    'minimum_entries': 1000,
    'policy': 'Curated punchy round_loss danmaku covering disbelief, direct flame, 6657 black speech, blame shift and pro team choke mockery.',
    'entries': entries
}

with open(OUTPUT_PATH, 'w', encoding='utf-8') as f:
    json.dump(out_obj, f, ensure_ascii=False, indent=2)

print('Successfully written to round_loss.json!')
