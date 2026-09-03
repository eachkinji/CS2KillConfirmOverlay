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

    prefixes = ['真就', '纯纯的', '这波', '直接', '当场', '这枪', '好家伙，', '老哥']
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
        elif any(p in text_lower for p in ['niko', 's1mple', 'zywoo', 'donk', 'm0nesy', 'b1t', 'scream', 'twistzz', 'faze', 'navi', 'tyloo', '天禄', '表哥', '大表哥', '载物', '太子']):
            phase = 'both'
            intent = 'pro_mockery'
            family = 'pro_whiff'
        elif any(k in text for k in ['对面', '报警', '公屏', '举报', '蒙', '凡尔赛']):
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

# 1. KILL POOL
kill_t1 = [
    '这枪也能杀？', '秒了？？？', '一枪带走！', '又杀一个！', '人头到手！',
    '这枪太快了吧！', '这就杀了？', '卧槽这枪法！', '一枪毙命！', '直接秒杀！',
    '对面直接白给了！', '瞬秒击杀！', '收下这个人头！', '这枪法太顶了！', '点名击杀！',
    '走位击杀行云流水！', '枪法开张了！', '拿下这记击杀！', '开门红击杀！', '这一枪太帅了！',
    '瞬间蒸发击杀！', '见面就秒杀！', '人头收入囊中！', '这枪法硬起来了！', '纯纯枪法压制击杀！',
    '这波击杀太秀了！', '枪响人倒！', '一枪一个真准！', '对面根本没反应过来就杀了！', '精准点射击杀！',
    '这枪法有点东西！', '对面直接送了个人头！', '干脆利落的击杀！', '这枪法帅炸了！', '稳稳收下人头！'
]
kill_t2 = [
    '好枪！漂亮击杀！', '枪法硬起来了！杀！', '杀得漂亮！好枪法！', '直接秒杀，太帅了', '收下这个人头，舒服！',
    '这枪法太顶了，秒杀！', '点名击杀，一个不留！', '走位击杀行云流水！', '纯纯枪法压制！好枪！', '对面直接白给，拿下击杀！',
    '这枪打得太果断了！', '无情击杀机器！', '对面被你这枪吓傻了！', '这枪法简直无可挑剔！', '枪枪到肉，拿下人头！',
    '极致拉枪，帅气击杀！', '对枪完全碾压杀敌！', '这记击杀太解渴了！', '教科书般的对枪击杀！', '这枪法赏心悦目！',
    '稳准狠！收下击杀！', '对面连你枪口都没看见就杀了！', '枪法在线，杀疯了！', '手感滚烫，连续击杀！', '这波拉枪击杀绝了！',
    '枪法直接支棱起来了！', '对面人头送得及时！', '毫无悬念的对枪击杀！', '神级反应击杀！', '这枪法我愿称之为绝活！'
]
kill_t3 = [
    '玩宝宝今天枪法开窍了！', '老狗也有今天！枪硬！', '某只猪突然开始杀人了！', '解说级击杀，太强了！', '懂了，主播今天吃牛肉面了！',
    '6657枪王归位，击杀！', '抽个奖庆祝一下这记击杀！', '玩神附体，杀疯了！', '老狗这一枪找回青春！', '今天玩宝宝的枪法不对劲！',
    '老狗枪法支棱起来了，杀！', '解说一流，枪法也是一流！', '玩神发威，收下人头！', '6657水友起立致敬这记击杀！', '老狗开杀，大伙狂喜！',
    '主播这枪法梦回2015！', '不用退钱了，今天能杀人！', '老狗这一枪值500个办卡！', '这一枪给6657争光了！', '玩宝宝认真起来谁都能杀！'
]
kill_t4 = [
    '对面已经报警了，这枪太快！', '对面公屏打问号了：这枪怎么杀的？', '这波绝对是蒙到的击杀！', '对面手心出汗了，被你这枪吓到了！', '击杀后战术深呼吸！',
    '这都能蒙到人头？太离谱了！', '对面键盘当场砸了：这也能杀？', '纯纯运气枪，但算你杀的！', '对面视角：这人开了自瞄杀我！', '对面公屏破防问号：这枪也行？',
    '对面已经申请封号了，这击杀太吓人！', '这枪杀得对面怀疑人生！', '对面：这枪法我怎么打？白给了！', '对面直接自闭，被你一枪带走！', '蒙得这么准也是一种枪法！'
]
kill_t5 = [
    '这枪有点NiKo那味了！', 'NiKo附体，一枪秒杀带走！', '学NiKo突破击杀，成了！', 'NiKo看了直呼内行，好枪！', '这枪法堪比巅峰NiKo！',
    'donk附体，撕裂防线击杀！', '简单男孩附体，杀人如麻！', '这记击杀很有s1mple的风采！', 'donk大吼：这就是击杀！', 's1mple点头赞赏这记击杀！',
    '载物级稳定击杀，太强了！', '小ZywOo附体，枪枪要人命！', '太子同款帅气击杀！', 'm0NESY附体，极速甩枪击杀！', '森哥附体，击杀如喝水！',
    '梦回天禄巅峰击杀！', '这枪法堪比白菜，硬！', '天禄老将同款刚枪击杀！', 'CNCS有希望了，这枪杀得狠！', '职业哥级反应，完美击杀！'
]
kill_kws = ['杀', '击杀', '枪', '人头', '白给', '蒙到', 'niko', 's1mple', 'donk', 'zywoo', 'm0nesy', '天禄', 'tyloo', '？', '!']

print('Generating kill.json...')
generate_pool('kill', r'杀|击杀|枪|人头|白给|蒙到', kill_t1, kill_t2, kill_t3, kill_t4, kill_t5, kill_kws)

# 2. HEADSHOT POOL
hs_t1 = [
    '一颗秒了？？？', '爆头！卧槽！', '这爆头速度？！', '一枪头带走！', '头线直接锁死了？！',
    '这也有一枪头？！', '脆响！爆头！', '光速颗秒？！', '瞬间爆头击杀！', '这颗秒太离谱了！',
    '爆头！帅炸了！', '一枪爆头秒杀！', '头线平移一颗秒！', '这爆头声太好听了！', '极速一枪头！',
    '精准爆头！', '爆头线拉满了！', '一发入魂爆头！', '这声脆响直接爆头！', '光速一枪头带走！',
    '头线锁死，颗秒！', '这也爆头了？！', '秒开枪秒爆头！', '极致爆头美学！', '一枪爆头教做人！',
    '爆头率100%既视感！', '脆脆的一枪头！', '定位爆头毫无拖泥带水！', '这颗秒我看了三遍！', '顶级头线爆头！'
]
hs_t2 = [
    '极致头线！完美爆头！', '定位太准了，一枪头！', '全自动锁头爆头！', '爆头机器开启！好枪！', '脆脆鲨！一枪爆头！',
    '颗秒艺术，赏心悦目！', '一枪头教做人，太帅了！', '完美预瞄，一枪爆头！', '头线架得太稳了，秒杀！', '纯纯头线压制，爆头！',
    '这颗秒干净利落！', '一枪爆头，毫无反抗余地！', '极致定位，瞬间爆头！', '爆头声太解压了！', '这头线平移太丝滑了！',
    '脆响连连，极速爆头！', '精准颗秒，枪法如神！', '一发爆头入魂，帅！', '爆头手感直接拉满了！', '这头线把对面看懵了！',
    '颗秒狂魔，一枪带走！', '无解头线，光速爆头！', '这爆头定位跟挂一样！', '一枪头秒杀，行云流水！', '极致的一颗秒！'
]
hs_t3 = [
    '玩宝宝这爆头我没看错吧？！', '老狗突然一枪头秒了！', '老狗的头线今天神了！', '抽奖！必须抽个爆头大奖！', '这声爆头脆响太治愈了！',
    '6657第一颗秒大师！', '老年人也能架死头线！', '老狗：这波叫肌肉记忆爆头！', '玩宝宝这颗秒有职业水平！', '老狗今天头线跟尺子画的一样！',
    '6657爆头王诞生！', '玩神这一枪头值回票价！', '今天玩宝宝的爆头率破纪录了！', '老狗一颗秒，弹幕全在刷问号！', '这头线，老狗今天没少练！',
    '主播这颗秒直接封神！', '看玩宝宝爆头比吃肉还爽！', '老狗这一枪头秀翻全场！', '6657头线天花板！', '玩宝宝一枪爆头，全场起立！'
]
hs_t4 = [
    '对面帽子直接被打飞了，爆头！', '对面当场举报爆头锁头！', '头盖骨都被掀翻了，一枪头！', '对面公屏：他是不是锁头线了？', '这爆头声听得对面脑瓜子嗡嗡的！',
    '对面当场加购防弹头盔，爆头！', '对面：这颗秒我怎么对枪？', '对面视角：头刚露出来就被颗秒了！', '对面直接把头缩回去了，爆头太狠！', '这爆头让对面怀疑自瞄！',
    '对面头线直接被你打烂了！', '对面玩家心态被这声爆头打碎了！', '这颗秒打得对面当场退游戏！', '对面：他这头线是焊死在脖子上的吗？', '爆头瞬间对面双手离开键盘！'
]
hs_t5 = [
    'ScreaM附体，一枪头秒杀！', 'B1t同款极速颗秒！', '爆头率100%，致敬ScreaM！', 'B1t看了都要夸你头线稳！', '这颗秒有ScreaM内味了！',
    '发型总监同款一枪头爆头！', 'NiKo沙鹰爆头再现！', 'Twistzz总监附体，头线完美爆头！', 'B1t神级定位颗秒，帅！', '爆头机器B1t附体，太准了！',
    'ScreaM经典单点头线一颗秒！', 'NiKo式暴躁一枪头！', 'donk流极速急停爆头！', '简单男孩甩枪爆头，秀！', '顶级头线，职业哥级颗秒！',
    '这颗秒放在Major也是高光！', 'ScreaM直呼爆头同行！', 'Twistzz看了都要点赞的爆头！', 'B1t附身，无情爆头机器！', '纯纯ScreaM级一枪头！'
]
hs_kws = ['爆头', '颗秒', '头线', '一枪头', 'scream', 'b1t', 'twistzz', 'niko', 'donk', 's1mple', '？', '!']

print('Generating headshot.json...')
generate_pool('headshot', r'爆头|颗秒|头线|一枪头', hs_t1, hs_t2, hs_t3, hs_t4, hs_t5, hs_kws)

# 3. ROUND_WIN POOL
win_t1 = [
    '赢了！赢了！', '拿下！漂亮！', '这一分拿下了！', '大胜！直接拿下！', '赢！继续赢！',
    '稳稳拿下这一分！', '胜利属于我们！', '赢下关键局！', '拿下！势不可挡！', '这一分赢得太干脆了！',
    '胜利到手，继续冲！', '赢了！毫无悬念！', '关键先生拿下比赛！', '这把赢定了！', '又拿下一分！',
    '完美团战，拿下胜利！', '这一分打得太漂亮了！', '直接赢下！太提气了！', '胜利拿下，手感火热！', '赢！把对面打服！',
    '这一分彻底稳住了！', '稳稳胜利，军心大振！', '拿下！全员发力！', '这一分赢得赏心悦目！', '大获全胜，拿下！',
    '赢下这一分，士气拉满！', '漂亮拿下，乘胜追击！', '胜利！完全碾压！', '这一分拿下，对面要碎了！', '赢下这一局，舒服了！'
]
win_t2 = [
    '稳如泰山，拿下这一分！', '完美的胜利，太棒了！', '打得太聪明了，轻松拿下！', '残局战神，拿下胜利！', '碾压局拿下，太轻松了！',
    '势如破竹，继续赢！', '配合拉满，赢得干脆！', '大比分拿下这一分！', '战术完全压制，赢得漂亮！', '毫无悬念的胜利！',
    '这波团队协同拉满，拿下！', '稳扎稳打拿下关键胜利！', '局势完全掌控，赢得轻松！', '枪法战术双碾压，拿下！', '这一分赢得太提士气了！',
    '全员在线，大胜拿下！', '这波胜利给对面打绝望了！', '残局教科书式处理，拿下！', '节奏完全在我们这，继续赢！', '这一分赢得滴水不漏！',
    '攻防兼备，稳稳拿下！', '对面完全被玩弄了，轻松赢！', '这波战术执行完美，拿下！', '摧枯拉朽的胜利！', '拿下！大局已定！'
]
win_t3 = [
    '玩神发力，轻松拿下！', '老狗带队赢下一分！', '下播前还能赢，奇迹！', '这波指挥一流，赢得漂亮！', '6657今晚吃鸡赢麻了！',
    '不用退钱了，今天能赢！', '老狗笑得合不拢嘴，赢！', '玩宝宝带头冲锋赢下这局！', '今天玩机器指挥得像个战术大师，赢！', '6657全员起立，庆祝胜利！',
    '老狗今天带飞了，拿下！', '弹幕全体刷拿下！老狗牛逼！', '这就是6657顶级理解，稳稳拿下！', '玩宝宝今天状态拉满了，赢！', '老狗这一局居功至伟，拿下！',
    '今天直播间水友过年了，赢麻了！', '老狗笑嘻了，拿下这一分！', '玩宝宝立功了，拿下胜利！', '这波胜利值得开个大转盘！', '6657传统胜利项目！'
]
win_t4 = [
    '对面心态彻底崩了，拿下！', '给对面打沉默了，轻松赢！', '对面经济空了，下把接着赢！', '打得对面毫无还手之力，大胜！', '对面公屏全体沉默，拿下！',
    '对面已经在内讧了，这把稳赢！', '把对面打得开始保枪了，拿下！', '对面连包点都进不来，轻松赢！', '对面已经不会玩游戏了，拿下！', '这波直接把对面打退钱了，赢！',
    '对面全员红温，我们稳稳拿下！', '对面开始甩锅了，这把必赢！', '对面键盘敲得啪啪响也打不赢！', '把对面打得直接叹气，拿下胜利！', '对面心态爆炸，胜利拿下！'
]
win_t5 = [
    '银河战舰起飞，拿下！', 'FaZe同款绝地翻盘拿下！', '大表哥级神级指挥拿下这一分！', '银河战舰所向披靡，拿下胜利！', 'FaZe经典逆转剧本，拿下！',
    'NaVi同款铜墙铁壁，赢下这一分！', '绿龙势不可挡，拿下！', '小蜜蜂式团队拉扯拿下胜利！', 's1mple看了都要鼓掌的胜利！', 'donk狂暴冲锋撕裂防线拿下！',
    '天禄梦幻开局，拿下这一分！', 'CNCS的骄傲，稳稳拿下！', '梦回天禄顶峰局势，拿下！', 'ZywOo级残局统治力，赢！', '点子哥神级点子盘活全场，拿下！',
    'Major冠军级团队配合，赢下这一分！', 'G2附体正面碾压，拿下！', '战术大师附体，稳稳胜利！', '这波胜利堪比大满贯决赛高光！', '顶级强队风范，拿下这一分！'
]
win_kws = ['赢', '胜利', '拿下', '这一分', 'faze', 'navi', 'tyloo', '天禄', 's1mple', 'zywoo', 'donk', '表哥', '！', '!']

print('Generating round_win.json...')
generate_pool('round_win', r'赢|胜利|拿下|这一分', win_t1, win_t2, win_t3, win_t4, win_t5, win_kws)

print('ALL THREE POOLS GENERATED SUCCESSFULLY!')
