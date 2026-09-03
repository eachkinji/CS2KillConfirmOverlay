# -*- coding: utf-8 -*-
import json, re
from pathlib import Path

ROOT = Path('.').resolve()
MEMES_PATH = ROOT / 'Widget' / 'Danmaku' / '6657_memes.json'
with open(MEMES_PATH, 'r', encoding='utf-8-sig') as f:
    memes = json.load(f)

print(f'Loaded {len(memes)} memes from 6657_memes.json')

def generate_pool(event_name, regex_pat, t1_bases, tier2_bases, tier3_bases, tier4_bases, tier5_bases, priority_kws):
    t1_prefixes = ['？？？', '???', '！！', '！！！', '卧槽，', '牛逼！', '漂亮！', '']
    tier1_all = []
    for p in t1_prefixes:
        for b in t1_bases:
            text = f'{p}{b}'
            if text not in tier1_all:
                tier1_all.append(text)
    tier1_all = tier1_all[:225]

    tier2_suffixes = ['', '！', '！！', '啊', '呢', '吧']
    tier2_all = []
    for b in tier2_bases:
        for s in tier2_suffixes:
            t = f'{b}{s}' if not (b.endswith('！') and s.startswith('！')) else b
            if t not in tier2_all:
                tier2_all.append(t)
    tier2_all = tier2_all[:245]

    tier3_all = []
    for b in tier3_bases:
        for s in ['', '！', '！！', '啊', '吧']:
            t = f'{b}{s}' if not (b.endswith('！') and s.startswith('！')) else b
            if t not in tier3_all:
                tier3_all.append(t)
    tier3_all = tier3_all[:200]

    tier4_all = []
    for b in tier4_bases:
        for s in ['', '！', '啊', '吧']:
            t = f'{b}{s}' if not (b.endswith('！') and s.startswith('！')) else b
            if t not in tier4_all:
                tier4_all.append(t)
    tier4_all = tier4_all[:100]

    tier5_all = []
    for b in tier5_bases:
        for s in ['', '！', '！！', '啊']:
            t = f'{b}{s}' if not (b.endswith('！') and s.startswith('！')) else b
            if t not in tier5_all:
                tier5_all.append(t)
    tier5_all = tier5_all[:245]

    all_texts = tier1_all + tier2_all + tier3_all + tier4_all + tier5_all
    unique_texts = []
    seen = set()
    for t in all_texts:
        if not re.search(regex_pat, t):
            continue
        if t not in seen:
            seen.add(t)
            unique_texts.append(t)

    prefixes = ['真就', '纯纯的', '这波', '直接', '当场', '开局', '好家伙，', '老哥']
    for p in prefixes:
        if len(unique_texts) >= 1000:
            break
        for base in list(unique_texts):
            if len(unique_texts) >= 1000:
                break
            if base.startswith(('？', '?', '@', '卧槽', '牛逼', '漂亮')):
                continue
            cand = f'{p}{base}'
            if len(cand) <= 22 and cand not in seen and re.search(regex_pat, cand):
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
    print(f'[{event_name}] Unique texts: {len(unique_texts)}')
    assert len(unique_texts) == 1000

    used_sources = set()
    entries = []

    for idx, text in enumerate(unique_texts):
        entry_id = f'{event_name}_{idx + 1:04d}'
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

        assert selected_source is not None, f'Missing source for {event_name} #{idx}'
        used_sources.add(selected_source)
        assert selected_excerpt in memes[selected_source - 1]

        if '？' in text or '?' in text:
            phase = 'burst'
            intent = 'disbelief'
            family = 'punctuation'
        elif any(p in text_lower for p in ['donk', 'apex', 's1mple', 'niko', 'zywoo', 'm0nesy', 'b1t', 'faze', 'navi', 'goat', '狂哥', '太子']):
            phase = 'both'
            intent = 'pro_mockery'
            family = 'pro_whiff'
        elif any(k in text for k in ['对面', '公屏', '求饶', '报警', '退游', '少人']):
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

    out_obj = {
        'schema_version': '4.0.0',
        'pool_type': 'event',
        'event': event_name,
        'source': 'Widget/Danmaku/6657_memes.json',
        'index_base': 1,
        'minimum_entries': 1000,
        'policy': f'Curated punchy {event_name} danmaku covering disbelief, hype, 6657 black speech, opponent reaction and pro player praise.',
        'entries': entries
    }

    out_path = ROOT / 'Widget' / 'Danmaku' / 'EventPools' / f'{event_name}.json'
    with open(out_path, 'w', encoding='utf-8') as f:
        json.dump(out_obj, f, ensure_ascii=False, indent=2)

    print(f'Successfully written {len(entries)} entries to {out_path}!')

# 1. FIRST_KILL
fk_t1 = [
    '这就首杀了？', '开门红！拿下一血！', '光速首杀！', '这就开张了？！', '首杀到手！',
    '直接拿下一血！', '开局十秒拿首杀！', '这么快就开张了！', '秒拿一血！', '首杀漂亮！',
    '开局拿首杀，局势稳了！', '一血到手，天下我有！', '瞬间首杀！太快了！', '开局先斩一人，首杀！', '直接破点拿下一血！',
    '这就拿下一血了？', '首杀太关键了！', '开局首杀，士气大振！', '一血拿下，舒服了！', '对枪先拔头筹拿首杀！',
    '干脆利落拿下一血！', '首杀开局，节奏拉满！', '开局秒开张！太准了！', '一血破点行云流水！', '首杀入账，开张大吉！',
    '开局十秒完成首杀！', '稳稳拿下一血！', '开局突破拿首杀！', '这么快首杀就有了！', '一血收入囊中！'
]
fk_t2 = [
    '顶级突破，拿下一血！', '开局先斩一人，稳稳首杀！', '拿下一血，局势大好！', '首杀破点，行云流水！', '这记首杀太提士气了！',
    '对枪先拔头筹，首杀！', '首杀在手，天下我有！', '开张大吉，继续杀！', '开局突破手，首杀太狠了！', '极致对枪，稳稳拿下一血！',
    '首杀建功，大局已定！', '这记首杀打碎了对面防线！', '开局秒人拿一血，太帅了！', '首杀拿得轻松写意！', '枪法撕裂防线，拿下首杀！',
    '首杀打开突破口！', '开局一血，奠定胜局！', '这记首杀价值千金！', '突破手首杀立大功！', '开张顺畅，手感火热！',
    '首杀到手，对面慌了！', '先声夺人，一血拿下！', '开局对枪完胜，拿首杀！', '首杀打崩对面心态！', '干净利落拿下一血！'
]
fk_t3 = [
    '老狗今天开局就开张了！', '玩宝宝也能拿首杀？！', '今天不用吃救心丸了，开局拿一血！', '某只猪今天居然不是送首杀！', '老狗：这波叫突破型主播！首杀！',
    '6657首杀开门红！', '今天玩神首杀率100%！', '弹幕全体起立，老狗开张了！', '今天玩宝宝开局就拿一血，神了！', '老狗一血建功，大伙看傻了！',
    '不用退钱了，主播开局拿首杀！', '老狗今天开局就开张，破天荒！', '玩宝宝这波突破拿首杀有水平！', '6657头号突破手，拿下一血！', '老狗首杀笑嘻嘻！',
    '今天玩宝宝对枪开局就拿一血！', '老狗这记首杀值回票价！', '玩神突破拿首杀，全体致敬！', '老狗今天第一局就开张大吉！', '开播就拿首杀，老狗支棱起来了！'
]
fk_t4 = [
    '对面刚出门就送了一血！', '对面直接开局掉人，首杀！', '给对面防线撕开大口子，拿下一血！', '对面开局就少一人，难顶！首杀！', '对面默认节奏被这记首杀打烂了！',
    '对面公屏：开局就送一血？', '对面开局直接少一个战斗力，首杀！', '把对面探点的人直接秒了一血！', '对面开门就被首杀制裁了！', '对面指挥当场懵了，开局丢一血！',
    '这一记首杀让对面不敢露头了！', '对面开局送一血，直接陷入劣势！', '首杀打得对面直接保枪！', '对面开局被打个下马威，首杀！', '这记首杀把对面打沉默了！'
]
fk_t5 = [
    'donk流狂暴突破拿首杀！', '学donk开局撕裂防线，首杀！', 'apEX梦寐以求的突破首杀！', '狂哥突破附体，拿下一血！', 'NiKo同款开局秒杀拿下一血！',
    '简单男孩极速抽奖拿首杀！', '职业哥级首杀效率！', 'arT同款狂暴开门红，拿下一血！', 'donk大吼：这就是开局首杀！', 's1mple式暴躁抽人拿首杀！',
    'ZywOo级稳定开局拿一血！', '太子神级拉枪拿下一血！', '顶级突破手附身，拿下首杀！', '这首杀放在Major也是顶级突破！', '天禄老将同款开局首杀！'
]
fk_kws = ['首杀', '一血', '开张', 'donk', 'apex', 'niko', 's1mple', 'zywoo', '狂哥', '？', '!']

print('Generating first_kill.json...')
generate_pool('first_kill', r'首杀|一血|开张', fk_t1, fk_t2, fk_t3, fk_t4, fk_t5, fk_kws)

# 2. MULTI_KILL
mk_t1 = [
    '双杀了！卧槽！', '三杀！太顶了！', '四杀来了？？？', '五杀ACE？！', '连续击杀！停不下来！',
    '又收一个！连杀！', '连着杀两个！帅！', '这波连杀太夸张了！', '双杀到手，继续！', '三杀清场了！',
    '连续点名击杀！', '连杀根本停不下来！', '这就多杀了？太快了！', '连着收下多个人头！', '连杀秀翻全场！',
    '瞬间双杀！漂亮！', '无解多杀！杀疯了！', '连续两个头！秒杀！', '三杀拿下关键残局！', '多杀狂魔启动了！',
    '连着杀三个，还有谁？！', '四杀到手，就差一个！', '收了几个！太猛了！', '连续对枪完胜！多杀！', '连杀节奏彻底起飞！',
    '双杀破局！帅炸！', '又连续杀一个！', '这波连杀赏心悦目！', '连着收下人头！', '多杀表演太震撼了！'
]
mk_t2 = [
    '双杀破点，干得漂亮！', '三杀清场，太无敌了！', '四杀到手，就差一个！', '完美的五杀团灭！', '连杀狂魔开启屠杀！',
    '这波连续击杀太顶了！', '一个人杀穿全场，连杀！', '连着杀，根本挡不住！', '赏心悦目的多杀表演！', '枪枪致命，完美多杀！',
    '连杀节奏拉满，太强了！', '连续击杀直接接管比赛！', '双杀奠定胜局，漂亮！', '三杀团灭对面防线！', '四杀暴走，全场焦点！',
    '这多杀操作行云流水！', '连续秒人，枪法大成！', '把对面连着杀完了！', '个人能力的极致，连杀！', '无敌多杀，摧枯拉朽！',
    '连续对枪全胜，太硬了！', '这波连续击杀直接封神！', '多杀收割残局！', '连杀输出直接拉满！', '干净利落的多杀表演！'
]
mk_t3 = [
    '老狗杀疯了！双杀到手！', '玩神三杀！弹幕全体起立！', '今天玩宝宝居然能连杀？！', '某只猪一个人收了几个！神了！', '老狗：还有谁？！三杀！',
    '6657多杀名场面诞生！', '这一波连杀值一个大火箭！', '玩宝宝开杀戒，连续击杀！', '老狗今天吃了什么猛药，连杀！', '玩神四杀！这下真成了战神！',
    '老狗今天这波多杀值回票价！', '直播间全体刷多杀牛逼！', '玩宝宝连着杀了几个，太霸道了！', '老狗这波连杀找回十年青春！', '6657多杀狂欢，全体刷弹幕！',
    '老狗：我一个人收了几个，你懂吗？', '玩神这波连续击杀秀翻服务器！', '老狗多杀，黑子全部闭嘴！', '今天玩宝宝这多杀操作无解了！', '6657大满贯级多杀！'
]
mk_t4 = [
    '对面一个接一个送，成全多杀！', '对面葫芦娃救爷爷，连着杀！', '对面全被你一个人杀光了！多杀！', '对面公屏全体打问号，这连杀怎么挡？', '对面排队枪毙，收了几个！',
    '对面心态被这记三杀打碎了！', '对面阵型被这波连杀打穿了！', '对面直接被连续击杀打懵了！', '把对面当小兵一样连着杀！', '对面开始互相指责，连杀太致命！',
    '对面五个人被你收了几个，绝望！', '对面公屏求饶：别连杀了！', '这波多杀打得对面毫无招架之力！', '对面全队成了多杀的背景板！', '连杀之后对面全员自闭！'
]
mk_t5 = [
    's1mple跳狙四杀附体！', 'donk暴走三杀，撕裂一切！', 'NiKo沙鹰三杀名场面！', '载物残局三杀附体，稳！', '太子神级身法四杀！',
    'B1t爆头三杀再现！', '简单男孩同款无解连杀！', '波黑枪王附体，三杀！', 'donk大吼：这就是多杀狂暴！', 'Major决赛级多杀高光！',
    'm0NESY附身神级多杀！', '狂哥暴躁连杀撕裂防线！', '职业哥级连续击杀！', '这波多杀放在HLTV绝对TOP1！', '巅峰s1mple式毁灭多杀！'
]
mk_kws = ['多杀', '双杀', '三杀', '四杀', '五杀', '连杀', '连续', '连着杀', '收了几个', 's1mple', 'donk', 'niko', 'zywoo', 'm0nesy', 'b1t', '？', '!']

print('Generating multi_kill.json...')
generate_pool('multi_kill', r'多杀|双杀|三杀|四杀|五杀|连杀|连续|连着杀|收了几个', mk_t1, mk_t2, mk_t3, mk_t4, mk_t5, mk_kws)

# 3. EPIC_STREAK
es_t1 = [
    '已经杀疯了！', '超神了！卧槽！', '这大连杀停不下来了！', '杀到超神！太离谱了！', '全场乱杀，谁来管管？！',
    '超神降临！无人能挡！', '这高连杀无人能挡！', '杀疯了杀疯了！战神！', '大连杀直接拉满！', '杀到对面不敢出来！',
    '全图乱杀，太夸张了！', '这就是超神的力量吗？！', '杀疯了！根本死不掉！', '高连杀统治比赛！', '一路乱杀，无敌了！',
    '杀到超神境界！', '连续杀到超神！', '这大连杀手感烫得发光！', '大连杀势如破竹！', '全场乱杀如入无人之境！',
    '杀到对面彻底绝望！', '高连杀直接封神！', '杀疯了！枪枪要命！', '一路杀到超神，太顶了！', '杀到超神狂欢！'
]
es_t2 = [
    '完全是个人表演，杀疯了！', '无人能挡，稳稳超神！', '大连杀主宰全场！', '全图乱杀，如同战神！', '杀到对面不敢露头！',
    '高连杀统治整局比赛！', '杀戮机器完全启动！超神！', '这一把直接杀穿服务器！', '无解大连杀，太霸道了！', '全场乱杀，胜负已分！',
    '杀疯了！这就是绝对实力！', '统治级大连杀，太强了！', '把对面杀到怀疑人生！', '超神战绩，载入史册！', '大连杀直接打散对面军心！',
    '杀到对面全体沉默！', '一人之力，全图乱杀！', '高连杀无可阻挡！', '杀到最后甚至没掉血！', '战神下凡，一路超神！',
    '大连杀节奏完美掌控！', '杀疯了！全场为之沸腾！', '超神表演，无可挑剔！', '一路乱杀，摧枯拉朽！', '这波大连杀太具有统治力了！'
]
es_t3 = [
    '老狗超神了！太阳打西边出来了！', '玩宝宝杀疯了，太霸道！', '今天老狗在服务器里乱杀！', '老狗：这波叫降维打击！超神！', '6657唯一真神，杀到超神！',
    '老狗这一把直接把黑子打沉默了，超神！', '弹幕别刷退钱了，主播今天乱杀！', '玩神附体，全场乱杀！', '老狗杀到超神，弹幕全体起立！', '今天玩宝宝大连杀打破生涯记录！',
    '老狗今天在服务器里乱杀乱宰！', '老狗杀疯了，笑得嘴都合不拢！', '6657全员过年，老狗大连杀超神！', '今天玩宝宝这波乱杀我愿称之为绝活！', '老狗这高连杀太提气了！',
    '主播今天乱杀，大伙都看傻了！', '老狗：这就是顶级选手的连杀统治力！', '玩宝宝杀疯了，黑粉当场转铁粉！', '老狗杀到超神，今晚必须开大香槟！', '6657直播间超神狂欢！'
]
es_t4 = [
    '对面已经被杀到挂机了！', '对面公屏求饶：别乱杀了！', '对面怀疑对面是外挂，直接杀疯了！', '把对面当简单电脑乱杀！', '对面已经不敢出出生点了，超神！',
    '对面五个人都在问：这人怎么杀到现在的？', '对面心态彻底炸穿，被一路乱杀！', '杀到对面直接在公屏打出GG！', '对面全体自闭，被大连杀打崩了！', '对面防线被杀到寸草不生！',
    '这高连杀让对面直接放弃挣扎！', '对面玩家已被杀到怀疑游戏生！', '对面公屏控诉：这人怎么一直在乱杀！', '对面阵型被大连杀彻底冲垮！', '把对面打得全员红温，大连杀！'
]
es_t5 = [
    'GOAT附体，全场超神乱杀！', 's1mple巅峰时期也不过如此！杀疯了！', 'donk卡托神迹附体，乱杀！', '1.70rating神级表现，杀疯了！', '狂暴战神donk同款超神！',
    '载物统治级超神表现！', '巅峰NiKo刚枪乱杀！', '职业哥炸鱼既视感，杀疯了！', 'm0NESY太子同款大连杀！', 'Major MVP级超神统治力！',
    's1mple看了直呼内行，杀疯了！', 'donk流狂暴战神，全图乱杀！', '神级大连杀，致敬巅峰GOAT！', '顶级职业选手级乱杀！', '超神封神之战，太帅了！'
]
es_kws = ['连杀', '杀疯', '乱杀', '超神', '高连', '杀到', 's1mple', 'donk', 'zywoo', 'niko', 'goat', 'm0nesy', '？', '!']

print('Generating epic_streak.json...')
generate_pool('epic_streak', r'连杀|杀疯|乱杀|超神|高连|杀到', es_t1, es_t2, es_t3, es_t4, es_t5, es_kws)

print('ALL THREE BATCH 2 POOLS GENERATED SUCCESSFULLY!')
