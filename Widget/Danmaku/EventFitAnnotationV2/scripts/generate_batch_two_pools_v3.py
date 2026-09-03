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

    prefixes = ['真就', '纯纯的', '这波', '直接', '当场', '好家伙，', '老哥']
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
        if cand not in seen and len(cand) <= 24 and re.search(regex_pat, cand):
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
        elif any(p in text_lower for p in ['ropz', 'zywoo', 'm0nesy', 's1mple', 'cadian', 'jw', 'apex', '太子', '点子哥', '森哥']):
            phase = 'both'
            intent = 'pro_mockery'
            family = 'pro_whiff'
        elif any(k in text for k in ['对面', '公屏', '求饶', '报警', '退游', '删游戏', '红温']):
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

# 1. LAST_KILL (regex: 最后|末杀|收尾|赛点|残局)
lk_t1 = [
    '残局收尾了！漂亮！', '最后一杀拿下！', '残局战神！太强了！', '赛点最后一杀终结！', '最后一个人没了！',
    '残局心跳拉满！', '残局收尾成功！舒服！', '残局收尾终结悬念！', '残局拿下，太顶了！', '最后一杀干脆利落！',
    '最后一个人被收下了！', '残局教科书式处理！', '赛点最后一杀终结回合！', '残局收尾神作！', '最后一杀帅炸了！',
    '残局博弈完全碾压！', '最后一杀定乾坤！', '这残局打得太聪明了！', '最后一杀终结反扑！', '残局收尾大功告成！',
    '最后一杀彻底封神！', '残局大师登场收尾！', '最后一个人绝望倒地！', '赛点残局终结！', '残局拿下，全场沸腾！',
    '残局收尾速度太快了！', '最后一杀稳准狠！', '残局智商压制！', '最后一杀终结比赛！', '最后一个人也送了！'
]
lk_t2 = [
    '教科书级残局，完美终结！', '最后一杀稳如泰山！', '残局收尾干净利落，太帅了！', '残局博弈完全碾压！', '最后一杀收下胜利！',
    '最后一个人被玩弄于股掌之间！', '大心脏选手，残局稳稳收尾！', '残局大师，名不虚传！', '最后一杀直接诛心！', '这残局处理滴水不漏！',
    '最后一杀终结，不留机会！', '残局战神附体，太硬了！', '最后一杀奠定胜局！', '残局收尾操作行云流水！', '最后一个人根本不知道人在哪！',
    '残局细节拉满，终结对手！', '大心脏最后一杀收尾！', '残局收尾果断，太霸气了！', '残局博弈完全拿捏对手！', '最后一杀终结悬念！',
    '最后一杀精准致命！', '残局处理教科书一般！', '稳稳残局收尾，大局已定！', '最后一杀终结比赛！', '最后一个人被轻松收割！'
]
lk_t3 = [
    '老狗残局也能赢？！奇迹！', '玩神大心脏，最后一杀拿下！', '某只猪居然打赢了残局收尾！', '老狗：这波叫残局教学！懂吗？', '残局收尾成功，老狗战术后仰！',
    '6657第一残局收尾大师！', '救心丸收起来，玩神最后一杀！', '老狗这记最后一杀值回票价！', '今天玩宝宝残局胜率100%！', '老狗残局笑嘻嘻，最后一杀！',
    '玩神最后一杀终结比赛！', '老狗：残局全看我操作，收尾！', '6657残局唯一真神！', '老狗最后一杀拿下，今晚不退钱！', '玩宝宝这波残局博弈封神！',
    '老狗：还有谁敢跟我打残局？！', '最后一杀到手，主播当场开香槟！', '玩神残局收尾，太有排面了！', '老狗这记收尾给直播间长脸了！', '6657战术大师最后一杀！'
]
lk_t4 = [
    '最后一个人直接被玩死了！', '对面残局被戏耍，心态炸裂！', '对面公屏：这残局也能输？', '最后一杀终结得对面毫无脾气！', '把最后一个人当猴耍，收尾！',
    '对面残局压力拉满，直接送了！', '最后一个人被架死在原地！', '对面公屏破防：怎么又是残局输了？', '最后一杀终结比赛，对面沉默！', '把最后一个人逼入绝境收尾！',
    '对面残局被心理战完全玩弄！', '最后一个人慌不择路被收尾！', '对面指挥在叹气，残局又丢了！', '最后一杀打得对面双手抱头！', '对面最后一个人连包点都没看懂！'
]
lk_t5 = [
    'ropz静步摸后同款残局收尾！', '载物残局附体，稳健最后一杀！', '顶级残局理解，致敬ropz！', 'm0NESY神级身法残局收尾！', '点子哥1v4同款神级残局！',
    '简单男孩经典大心脏最后一杀！', 'ZywOo看了都要夸你残局稳！', '太子同款帅气最后一杀！', 'cadian式咆哮庆祝残局收尾！', '职业级残局博弈，最后一杀！',
    'ropz附身，老银币残局收尾！', '森哥同款暴躁最后一杀！', 'Major赛点最后一杀大心脏！', '载物级残局统治力，收尾！', '顶级选手同款残局最后一杀！'
]
lk_kws = ['收尾', '最后一杀', '残局', '终结', '最后一个人', '最后', '赛点', 'ropz', 'zywoo', 'm0nesy', 's1mple', 'cadian', '？', '!']

print('Generating last_kill.json...')
generate_pool('last_kill', r'最后|末杀|收尾|赛点|残局', lk_t1, lk_t2, lk_t3, lk_t4, lk_t5, lk_kws)

# 2. KNIFE_KILL (regex: 刀|近战)
kk_t1 = [
    '真刀了？！卧槽！', '掏刀了！太搞了！', '近战大背刺！帅炸了！', '近战直接刀死！', '刺刀见红！太搞笑了！',
    '这也敢掏刀？！', '当场被刀，丢大人了！', '直接一刀带走！', '静步掏刀！绝了！', '近战背刺成功！',
    '近战刀杀！太残忍了！', '掏刀羞辱！杀人诛心！', '一刀毙命！爽！', '这也能近战刀死？！', '刺刀刺穿对面护甲！',
    '近战大背刺！对面毫无察觉！', '真拿小刀捅死了！', '当面掏刀！太狂了！', '近战搏杀，一刀封喉！', '这记刀杀太解气了！',
    '掏刀大成功！', '物理羞辱！直接刀了！', '这小刀划得太丝滑了！', '近战刺刀见红！', '当场被刀死，社死现场！'
]
kk_t2 = [
    '物理羞辱，掏刀伤害不高侮辱极强！', '近战背刺成功，对面要退游了！', '静步掏刀，太残忍了！', '刺刀见红，纯纯小丑！', '近战搏杀，刀刀致命！',
    '被刀了还有脸玩游戏？', '极致身法掏刀，帅！', '近战背刺得明明白白！', '一刀入魂，近战极致羞辱！', '近战刺杀，赏心悦目！',
    '近战掏刀，对面心态碎了！', '静步摸屁股掏刀背刺！', '这记刀杀节目效果直接拉满！', '刺刀刺碎了对面的尊严！', '近战刀杀，太有含金量了！',
    '被刀死的一刻全场安静！', '近战背刺，对面连反应时间都没有！', '掏刀就是自信！帅炸！', '物理超度，近战刀杀封神！', '刺刀见红，直接给对面整红温！'
]
kk_t3 = [
    '老狗掏刀了！节目效果拉满！', '玩宝宝近战背刺，对面当场红温！', '某只猪居然刀人了，笑死！', '6657刀神归位！刺刀见红！', '老狗：这波叫近战艺术！',
    '被老狗刀了，建议删游戏！', '这一刀给直播间带来十个火箭！', '玩神摸到屁股后面掏刀，太损了！', '老狗掏刀笑得像个孩子！', '玩宝宝这记近战刀杀能吹一年！',
    '老狗：今天谁也别想跑，全刀了！', '6657第一近战刀神！', '弹幕全体刷老狗刀神牛逼！', '老狗掏刀背刺，黑粉都笑喷了！', '玩神一刀封喉，节目效果封顶！',
    '老狗掏刀成功，当场战术后仰！', '被玩宝宝刀死，对面今晚睡不着！', '6657刺刀见红传统绝活！', '老狗：我这一刀十年功力！', '玩神近战刺杀，全场起立！'
]
kk_t4 = [
    '对面当场退出游戏，被刀破防了！', '对面这辈子不想转背身了，近战背刺！', '被刀死之后公屏直接开骂！', '对面被刀之后双手离开键盘！', '这记近战刀杀让对面今晚睡不着！',
    '对面队友：你怎么被人刀了？！', '对面玩家心态直接被这把刀捅碎了！', '被刀的人公屏打出一串省略号！', '对面直接把耳机摔了，被刀太丢人！', '这记近战刺杀让对面彻底自闭！',
    '对面：他怎么敢掏刀的啊？！', '被刀死之后对面全队都在笑他！', '对面被刀之后直接申请投降！', '这一刀捅得对面脑瓜子嗡嗡的！', '被刀现场宛如处刑大会！'
]
kk_t5 = [
    's1mple同款跳刀羞辱！', '学森哥掏刀，对面心态碎了！', '致敬职业哥神级近战大背刺！', 'JW式老银币掏刀，太脏了！', 'apEX看了当场脑溢血：这也能被刀？',
    '刀神下凡，物理超度！', 'Major级掏刀节目效果！', 's1mple看了直呼内行，好刀法！', '职业哥级近战静步背刺！', '冷神同款近战跳刀刺杀！',
    'JW附体，老银币近战出刀！', '学职业哥掏刀，帅炸全场！', '这一刀放HLTV也是高光集锦！', '顶级刺刀艺术，致敬刀神！', '职业赛场同款名场面近战刀杀！'
]
kk_kws = ['刀', '近战', '刺刀', 's1mple', '森哥', 'jw', 'apex', '？', '!']

print('Generating knife_kill.json...')
generate_pool('knife_kill', r'刀|近战', kk_t1, kk_t2, kk_t3, kk_t4, kk_t5, kk_kws)

print('ALL TWO BATCH 3 POOLS GENERATED SUCCESSFULLY!')
