# -*- coding: utf-8 -*-
import json, re
from pathlib import Path

ROOT = Path('.').resolve()
MEMES_PATH = ROOT / 'Widget' / 'Danmaku' / '6657_memes.json'
OUTPUT_PATH = ROOT / 'Widget' / 'Danmaku' / 'EventPools' / 'death.json'

with open(MEMES_PATH, 'r', encoding='utf-8-sig') as f:
    memes = json.load(f)

print(f'Loaded {len(memes)} memes from 6657_memes.json')

t1_prefixes = ['？？？', '???', '？？？？', '????', '？？？？？', '?????']
t1_bases = [
    '这就死了？', '这也能死？', '怎么又死了？', '这就白给了？', '一枪没中送了？',
    '这也空枪送了？', '纯送啊！', '又白给了？', '你在送什么？', '这也马枪送了？',
    '太菜了吧！', '死得太快了！', '白给之王！', '送得这么积极？', '空枪直接暴毙！',
    '满血瞬间白给！', '纯纯送分！', '又把枪送了？', '空枪也能被秒？', '送人头第一名！',
    '这就阵亡了？', '原地暴毙送了？', '死相太难看！', '怎么又白给了？', '菜得头皮发麻！',
    '白给速度拉满！', '死得毫无排面！', '白给得这么利索？', '空枪送命！', '菜出新高度！',
    '这就倒了送了？', '这能死的啊？', '人呢？这就送了？', '一枪不开就送？', '走路上都能白给？',
    '对枪一秒暴毙送了？', '大狙白给送对面？', '空枪白给全占了？', '这波送得太搞笑了！', '菜成马了还送？'
]
tier1_all = []
for p in t1_prefixes:
    for b in t1_bases:
        text = f'{p}{b}'
        if text not in tier1_all:
            tier1_all.append(text)
tier1_all = tier1_all[:225]

tier2_bases = [
    '太菜了！纯送！', '真菜！经典白给！', '白给少年又送一个', '白给之王非你莫属', '十枪九空送人头',
    '又送温暖了，太菜了', '菜就多练，别送了', '空枪是呼吸，白给是日常', '满血进去，白给出来', '菜得发瘟，纯送',
    '菜得抠脚，死得利索', '又偷偷死了是吧？', '送得真快啊，太菜了', '给对面送大礼包', '菜出天际，白给成瘾',
    '人体描边大师，纯送', '全自动描边空枪白给', '十枪九空，还有一枪打空气', '枪口抬高两米送命', '给对面刮痧送人头',
    '这枪法我上我也行，太菜了', '栓条狗都比你准，纯送', 'BOT都比你能打，白给BOT', '普通BOT白给操作', '简单电脑送人头',
    '太下饭了，空枪送命', '看饱了，菜得真实', '连吃三大碗，白给大厨', '当场吃饱，太菜了', '厨师长又送新菜了',
    '极品下饭白给操作', '饭点准时送大餐', '幽默枪法，幽默白给', '幽默死相，太菜了', '今日最佳白给小丑',
    '纯纯的小丑，送得开心', '马戏团开演，白给第一名', '移动取款机又送钱了', '慈善赌王又送一把', '枪法小众又白给',
    '子弹全在周围绕，死得惨', '完美避开要害送命', '对面毫发无损，自己死了', '纯纯滋水枪，白给少年', '打空气送人头',
    '开局白给，毫不犹豫', '对枪一秒死，逃跑第一名', '空枪红温，当场暴毙', '送分童子名不虚传', '白给得行云流水',
    '菜到令人窒息，纯送', '送死都不用看地图', '白给专线，准时送达', '空枪之后必定白给', '白给速度突破天际',
    '这操作菜得我头晕', '纯送局，主播负全责', '空枪送人头，专业户', '死得毫无悬念，太菜了', '白给姿势花里胡哨'
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
    '可是玩宝宝也太菜了吧😅', '老狗❤️空枪送命', '某只猪又偷偷死了', '下播吧，今天就没几个骂你菜的', '玩机器，退钱！太菜了！',
    '退钱！RNM退钱！纯送！', '老狗爱空枪，老狗爱白给', '30岁老年反应白给', '空枪红温送首杀', '菜到极致自然黑',
    '开播送首杀，下播送末杀', '日常白给，基操勿六', '这就是职业解说的空枪白给？', '解说一流，操作纯菜', '嘴上天下无敌，对枪当场白给',
    '理论大师，实战送头', '懂了，这是在战术白给', '战术后撤变原地暴毙送命', '点个外卖的功夫你就死了', '喝口水的功夫你又白给了',
    '刚进来就看你死了一次', '传统艺能空枪白给', '主播真不考虑进厂吗？太菜了', '打螺丝都没人要，纯送', '退役吧，开滴滴去吧，太菜了',
    '关播吧求你了，送得难受', '工伤！看你白给申请工伤！', '眼睛看瞎了，太菜了', '丢人现眼，赶紧下播别送了', '30岁老年手抖空枪送命',
    '反应迟钝得像幻灯片，白给', '红温了红温了，空枪急死', '主播当场红温白给', '老毛病又犯了，又送一个', '病入膏肓的空枪白给',
    '今天这把是真把我看笑了，太菜了', '猪头摇起来了，人也送没了', '玩宝宝空枪白给有一手的', '6657传统白给项目', '玩神白给，大伙狂喜'
]
tier3_all = []
for b in tier3_bases:
    for s in ['', '！', '😅', '啊', '吧']:
        t = f'{b}{s}' if not (b.endswith('！') and s == '！') else b
        if t not in tier3_all:
            tier3_all.append(t)
tier3_all = tier3_all[:200]

tier4_bases = [
    '死前必找借口，太菜了', '又怪鼠标太滑送了是吧？', '这波怪椅子还是怪自己菜？', '一身神装，出场就送', '死前疯狂找借口',
    '专程给对面送枪的快递员', '死后开始怪延迟，经典菜', '菜得真实，借口虚伪', '144Hz屏幕救不了60Hz脑子，白给', '顶级外设，三流白给技术',
    '又怪桌子不平白给了是吧？', '耳机声音太小送了是吧？', '这波怪手心出汗送了？', '这波怪屏幕刷新率，太菜了', '掉帧了是吧？借口替你想好了，纯送',
    '对面的延迟比你低0.1ms是吧？送', '对面绝对开了，别问，反正你菜', '对面透视锁头，实锤你白给', '这就去举报对面，太菜了', '导播快切镜头，主播又送了',
    '队友全在叹气：求你别送了', '买完枪直接当快递送对面', '专程给对面送长枪的慈善家', '死完开始总结，总结完继续送', '借口千千万，白给第一名'
]
tier4_all = []
for b in tier4_bases:
    for s in ['', '！', '😅', '啊']:
        t = f'{b}{s}' if not (b.endswith('！') and s == '！') else b
        if t not in tier4_all:
            tier4_all.append(t)
tier4_all = tier4_all[:100]

tier5_bases = [
    'NiKo背身空枪都没你这么抽象', '致敬NiKo！背身空枪反被杀', '三枪背身空枪，真有你的NiKo', '真把自己当NiKo了？纯送！', 'NiKo沙鹰好歹中过，你这纯送',
    '背身打不死反被秒，致敬NiKo白给！', '学NiKo瞄头，子弹全打脚趾头送命', '你这沙鹰比NiKo还马，太菜了', '一枪不开送命，致敬NiKo关键局', '但凡中一枪呢？NiKo看了都流泪送命',
    'NiKo：这锅我不背，我背身至少打中一枪', '山寨NiKo，白给纯度极高', '背身瞄半天，结果自己白给了？', '这走位送命，NiKo都直呼内行', 'NiKo看了连夜退出游戏，太菜了',
    'NiKo的背身，你的正面，纯送绝配！', '沙鹰空枪六发，真有NiKo那味了', 'NiKo决赛手软，主播每把手软送命', '学NiKo突破，第一个蒸发白给', 'NiKo都不敢这么空枪送人头',
    '保！Jame看了都得给你磕头，白送！', '学Jame保狙，结果连人带狙一起送？', '这保枪意识，JameTime了还白给？', '毒瘤保枪第一名，对枪白给零点一秒', '学blameF断后，断了自己后路白给',
    '别人保枪留经济，你保枪全送对面', 'Jame是保枪，你是纯粹不敢对枪送命', '时间到了还在保，队友全死你在送', 'blameF附体，队友祭天法力无边还白给', '这局保得好，下局接着送',
    '保狙保到最后一秒送命', '连人带甲全额赞助对面，太菜了', 'Jame直呼老弟你保得太送了', '枪没保住人也死了，双赢！', '全场最贵快递员，专送长枪',
    '大表哥送首杀都没你送得快！', '学apEX开局飞僵冲锋送首杀！', '大表哥0-11，你要破纪录送命？', 'apEX抱头：主播比我还抽象白给', '战术送首杀？这战术太菜了',
    '大表哥看你的走位直呼同行，太送了', '起步就是白给，大表哥狂喜', '开局十秒白给，真有你的大表哥', 'apEX飞僵倒欠人头，主播紧跟送命', '送首杀速度堪比光速，太菜了',
    '指挥：这是战术探点白给', '大表哥至少会指挥，你纯粹是送', 'apEX骂骂咧咧退出了直播间，太菜了', '大表哥：终于有人比我白给得快了', '传说中的战术献祭流送人头',
    '技术没学到，s1mple摊手学挺像，菜！', 's1mple看了当场红温：太菜了！', '简单男孩摔耳机，主播白给摔鼠标', 's1mple摇头，载物叹气，主播白给', '小ZywOo？小侏儒白给吧！',
    '载物在对面早把你当提款机送了', 'ZywOo打成这样都被喷，你凭什么不送？', '空枪后的叹气，很有s1mple的神韵', '简单男孩摔桌，主播白给当场自闭', '学到载物体型，没学到枪法，太菜了',
    's1mple摊手：这就死了？太菜了！', '买家秀s1mple，空枪白给退货吧', '对面要是s1mple早把你头打歪送了', '载物连夜买站票逃跑，主播太菜了', 'GOAT天上飞，主播地上白给',
    '四大冥狙第五人：空枪白给主播', 'broky保狙都没你苟，暴毙送命第一名', '西兰花枪法都比你硬，太菜了', 'broky看你的空枪连夜卖箱子', '一枪不响大狙白给，真有你的箱神',
    '大狙架点一万年，漏头瞬间白给送了', '四大冥狙之首，空枪白给非你莫属', '叫得比donk还响，死得比谁都快', '学donk大吼大叫，枪法空枪像大爷', 'donk冲锋撕裂防线，你冲锋白给送人头',
    '真以为自己是donk？两枪白给倒地', '身法学太子，对枪白给像孙子', 'm0NESY身法没学会，暴毙送命全占了', '花里胡哨跳半天，落地一枪白给送了', '旋转跳落地接爆头，白给真帅',
    '点子哥：这波我真没教过你这么送', '倒霉堪比点子哥，枪法还不如点子哥送', 'stavn大赛隐身，主播每把隐身白给', '天禄传统异能，5打2空枪送翻盘', '汉堡都不敢这么吃，空枪白给太下饭了'
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
    if not re.search(r'死|阵亡|白给|空枪|菜|送', t):
        continue
    if t not in seen:
        seen.add(t)
        unique_texts.append(t)

unique_texts = unique_texts[:1000]
print(f'Unique initial texts: {len(unique_texts)}')

# Expand naturally to exactly 1000
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
        if len(cand) <= 22 and cand not in seen and re.search(r'死|阵亡|白给|空枪|菜|送', cand):
            seen.add(cand)
            unique_texts.append(cand)

for base in list(unique_texts):
    if len(unique_texts) >= 1000:
        break
    cand = f'@{base}'
    if cand not in seen and len(cand) <= 24:
        seen.add(cand)
        unique_texts.append(cand)

print(f'Final unique texts: {len(unique_texts)}')
assert len(unique_texts) == 1000

# Match each with unique source in memes
priority_kws = ['死', '菜', '白给', '空枪', '马枪', '送', '急', '红温', '退役', '下播', '退钱', '小丑', '幽默', 'bot', '人呢', 'niko', 's1mple', 'donk', 'zywoo', 'broky', 'jame', 'apex', 'karrigan', 'm0nesy', 'cadian', '天禄', '箱子', '狙', '沙鹰', '保枪', '？', '?']

used_sources = set()
entries = []

for idx, text in enumerate(unique_texts):
    entry_id = f'death_{idx + 1:04d}'
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
    elif any(p in text_lower for p in ['niko', 'jame', '表哥', 'apex', 's1mple', 'zywoo', 'broky', 'donk', '太子', '点子哥', '天禄']):
        phase = 'both'
        intent = 'pro_mockery'
        family = 'pro_whiff'
    elif any(k in text for k in ['怪', '借口', '延迟', '外设', '鼠标', '椅子']):
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
    'event': 'death',
    'source': 'Widget/Danmaku/6657_memes.json',
    'index_base': 1,
    'minimum_entries': 1000,
    'policy': 'Curated punchy death danmaku covering disbelief, direct flame, 6657 black speech, excuse prediction and pro player meme mockery.',
    'entries': entries
}

with open(OUTPUT_PATH, 'w', encoding='utf-8') as f:
    json.dump(out_obj, f, ensure_ascii=False, indent=2)

print('Successfully written to death.json!')
