"""Stage the built-in Sanguosha (三国杀) multi-pack broadcast assets and voice packs."""
import argparse
import gzip
import hashlib
import json
import os
import shutil
import struct
import sys

ROOT = os.path.dirname(os.path.abspath(__file__))
TIERS = [
    # Pack 1: 十周年连破播报 (sanguosha)
    "shoupo", "lianpo", "sanpo", "sipo", "wupo", "liupo", "qipo",
    "shenweizhengqiankun", "wanfumodi", "podi", "miaosha",
    # Pack 2: 官阶七阶神兵 (sanguosha_guanjie)
    "guanjie2", "guanjie3", "guanjie4", "guanjie5", "guanjie6", "guanjie7",
    "dashatesha", "shoujidonghua_jian", "shoujidonghua_fuzi2", "shoujidonghua_chuizi",
    # Pack 3: 自走棋与文和乱武 (sanguosha_zzq)
    "zzq_shousha", "zzq_lianpo", "zzq_sanpo", "lianzhan4", "lianzhan5", "lianzhan6", "lianzhan7",
    "zzq_wanrendi", "zzq_sha", "zzq_shajue",
    # Pack 4: 紧凑无框徽章 (sanguosha_badge)
    "badge_1", "badge_2", "badge_3", "badge_4", "badge_5", "badge_6", "badge_7",
    "badge_8", "badge_9", "badge_10", "badge_11",
    # Pack 5: 神兵装备与阵亡印章 (sanguosha_equip)
    "equip_1", "equip_2", "equip_3", "equip_4", "equip_5", "equip_6", "equip_7",
    "equip_8", "equip_9", "equip_10", "equip_11",
]

SOUND_PACKS = {
    "sanguosha": (
        "三国杀 · 十周年连破播报",
        {
            "1kill.mp3": ("kill_1", "newkill_shoupo"),
            "2kill.mp3": ("kill_2", "newkill_lianpo"),
            "3kill.mp3": ("kill_3", "newkill_sanpo"),
            "4kill.mp3": ("kill_4", "newkill_sipo"),
            "5kill.mp3": ("kill_5", "newkill_wupo"),
            "6kill.mp3": ("kill_6", "newkill_liupo"),
            "7kill.mp3": ("kill_7", "newkill_qipo"),
            "8kill.mp3": ("kill_8", "newkill_shenweizhengqiankun"),
            "headshot.mp3": ("headshot", "newkill_wanfumodi"),
            "knife.mp3": ("knife", "kill_effect_sound"),
            "grenade.mp3": ("grenade", "fireHurtSound2"),
        },
    ),
    "sanguosha_general": (
        "三国杀 · 神将与界徐盛台词",
        {
            "1kill.mp3": ("kill_1", "01_界徐盛_破军1"),
            "2kill.mp3": ("kill_2", "38_神司马懿_连破1"),
            "3kill.mp3": ("kill_3", "11_神甘宁_魄袭2"),
            "4kill.mp3": ("kill_4", "25_威马超_铁骑2"),
            "5kill.mp3": ("kill_5", "40_神赵云_龙魂"),
            "6kill.mp3": ("kill_6", "20_虎牢关吕布_神戟"),
            "7kill.mp3": ("kill_7", "16_神吕布_神愤2"),
            "8kill.mp3": ("kill_8", "22_神曹操_归心2"),
            "headshot.mp3": ("headshot", "36_黄忠_烈弓1"),
            "knife.mp3": ("knife", "04_界徐盛_破军史诗1"),
            "grenade.mp3": ("grenade", "41_界张角_雷击"),
        },
    ),
    "sanguosha_zzq": (
        "三国杀 · 自走棋与乱武语音",
        {
            "1kill.mp3": ("kill_1", "36_自走棋_首杀音效_FirstBlood"),
            "2kill.mp3": ("kill_2", "45_自走棋_连杀播报_男声"),
            "3kill.mp3": ("kill_3", "46_自走棋_连杀播报_女声"),
            "4kill.mp3": ("kill_4", "47_自走棋_多杀播报_男声"),
            "5kill.mp3": ("kill_5", "48_自走棋_多杀播报_女声"),
            "6kill.mp3": ("kill_6", "37_自走棋_神马超冲锋击杀音效"),
            "7kill.mp3": ("kill_7", "38_自走棋_神陆逊业火击杀音效"),
            "8kill.mp3": ("kill_8", "49_牌局MVP结算音效_result_mvp_sound"),
            "headshot.mp3": ("headshot", "31_文和乱武_无双乱舞重击音效"),
            "knife.mp3": ("knife", "39_自走棋_破军斩击音效_PoJun"),
            "grenade.mp3": ("grenade", "27_闪电劈中音效_shandian_success"),
        },
    ),
    "sanguosha_card": (
        "三国杀 · 经典杀闪与神兵音效",
        {
            "1kill.mp3": ("kill_1", "01_杀_经典男声_spell1_1_classic"),
            "2kill.mp3": ("kill_2", "38_武器音效_雌雄双股剑"),
            "3kill.mp3": ("kill_3", "32_武器音效_青龙偃月刀"),
            "4kill.mp3": ("kill_4", "34_武器音效_丈八蛇矛"),
            "5kill.mp3": ("kill_5", "33_武器音效_贯石斧"),
            "6kill.mp3": ("kill_6", "35_武器音效_方天画戟"),
            "7kill.mp3": ("kill_7", "30_武器音效_诸葛连弩"),
            "8kill.mp3": ("kill_8", "52_MVP经典音效_GameMvp"),
            "headshot.mp3": ("headshot", "14_重伤暴击命中_酒杀2点以上伤害_hurtSound2"),
            "knife.mp3": ("knife", "31_武器音效_古锭刀"),
            "grenade.mp3": ("grenade", "44_防具音效_藤甲火杀重伤"),
        },
    ),
}


def clip_pages(path):
    raw = gzip.open(path).read()
    assert raw[:4] == b"SGSD" and struct.unpack_from("<H", raw, 4)[0] == 1
    p = 6 + 24
    (count,) = struct.unpack_from("<H", raw, p); p += 2
    pages = []
    for _ in range(count):
        (n,) = struct.unpack_from("<H", raw, p); p += 2
        name = raw[p:p + n].decode("utf-8"); p += n
        w, h = struct.unpack_from("<HH", raw, p); p += 4
        pages.append((name, w, h))
    return pages


def sha(path):
    return hashlib.sha256(open(path, "rb").read()).hexdigest()


def find_voice_recursive(workshop_root, stem):
    for root, _, files in os.walk(workshop_root):
        for name in sorted(files):
            if name.endswith(".mp3") and stem in name:
                return os.path.join(root, name)
    raise FileNotFoundError(stem)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--animate", required=True)
    ap.add_argument("--voices", required=True)
    ap.add_argument("--clips", required=True)
    ap.add_argument("--logo", required=True)
    a = ap.parse_args()
    from PIL import Image

    battle = os.path.dirname(a.animate.rstrip("\\/"))
    animate_root = os.path.dirname(battle)
    workshop_root = os.path.dirname(os.path.dirname(animate_root))
    search_dirs = [
        a.animate,
        os.path.join(battle, "nianshou2024"),
        os.path.join(battle, "hurtEffect"),
        os.path.join(battle, "state"),
        os.path.join(animate_root, "tavernChess", "battle"),
        os.path.join(animate_root, "tavernChess", "firstKill"),
        battle,
    ]

    widget = os.path.join(ROOT, "Widget", "Assets", "GameStyles", "sanguosha", "killconfirm")
    source = os.path.join(ROOT, "SourceAssets", "GameStyles", "sanguosha", "killconfirm")
    index = []
    for out_root in (widget, source):
        shutil.rmtree(out_root, ignore_errors=True)
    for anim in TIERS:
        clip = os.path.join(a.clips, anim + ".sgsd.gz")
        pages = clip_pages(clip)
        files = [anim + ".sgsd.gz"]
        for out_root in (widget, source):
            d = os.path.join(out_root, anim)
            os.makedirs(d, exist_ok=True)
            shutil.copyfile(clip, os.path.join(d, anim + ".sgsd.gz"))
            for name, w, h in pages:
                if "/" in name:
                    # Cross-clip shared atlas page (already copied into its owner folder)
                    continue
                src = next((os.path.join(sd, name) for sd in search_dirs if os.path.isfile(os.path.join(sd, name))), None)
                if not src:
                    sys.exit(f"{name}: atlas page not found in {search_dirs}")
                with Image.open(src) as im:
                    if im.size != (w, h):
                        sys.exit(f"{name}: atlas page {im.size} does not match clip {w}x{h}")
                shutil.copyfile(src, os.path.join(d, name))
        local_pages = [p[0] for p in pages if "/" not in p[0]]
        files += local_pages
        index.append({"tier": TIERS.index(anim) + 1, "animation": anim,
                      "files": {f: sha(os.path.join(widget, anim, f)) for f in files}})
    with open(os.path.join(source, "index.json"), "w", encoding="utf-8") as f:
        json.dump({"format": "sanguosha-killconfirm", "version": 1, "tiers": index}, f, ensure_ascii=False, indent=2)

    # Stage preview icons for the Sanguosha sub-packs
    previews_map = {
        "sanguosha_guanjie.png": os.path.join(workshop_root, r"01_连破与击杀徽章_合成大图\03_官阶专属击杀徽章与经典破敌\07_大司马官阶_金瓜战锤_纯武器图标.png"),
        "sanguosha_zzq.png": os.path.join(workshop_root, r"01_连破与击杀徽章_合成大图\04_文和乱武与自走棋连破徽章\21_自走棋_首杀_合成徽章.png"),
        "sanguosha_badge.png": os.path.join(workshop_root, r"01_连破与击杀徽章_合成大图\01_十周年连破与特效徽章_紧凑无背景光版\01_首破_一骑当先_紧凑徽章版.png"),
        "sanguosha_equip.png": os.path.join(workshop_root, r"01_连破与击杀徽章_合成大图\06_三国杀武器装备与官阶道具图标\Equip_QingGangJian.png"),
        "sanguosha_general.png": os.path.join(workshop_root, r"preview_assets\general_xusheng.png"),
        "sanguosha_card.png": os.path.join(workshop_root, r"01_连破与击杀徽章_合成大图\06_三国杀武器装备与官阶道具图标\Equip_ZhuGeLianNu.png"),
    }
    for base_root in (
        os.path.join(ROOT, "Widget", "Assets", "GameStyles", "sanguosha", "previews"),
        os.path.join(ROOT, "SourceAssets", "GameStyles", "sanguosha", "previews"),
    ):
        shutil.rmtree(base_root, ignore_errors=True)
        os.makedirs(base_root, exist_ok=True)
        for dst_name, src_path in previews_map.items():
            with Image.open(src_path) as im:
                im_rgba = im.convert("RGBA")
                im_rgba.thumbnail((256, 256), Image.LANCZOS)
                im_rgba.save(os.path.join(base_root, dst_name), compress_level=6)

    soundpacks_root = os.path.join(ROOT, "SourceAssets", "GameStyles", "sanguosha", "soundpacks")
    shutil.rmtree(soundpacks_root, ignore_errors=True)
    for pack_id, (pack_title, voice_spec) in SOUND_PACKS.items():
        pack = os.path.join(soundpacks_root, pack_id)
        os.makedirs(pack, exist_ok=True)
        slots = {}
        for dst_name, (slot_key, stem) in voice_spec.items():
            shutil.copyfile(find_voice_recursive(workshop_root, stem), os.path.join(pack, dst_name))
            slots[slot_key] = dst_name
        manifest = {
            "id": pack_id,
            "name": pack_title,
            "game_style": "sanguosha",
            "version": "1.0",
            "audio": {"base_gain": 1.0, "slots": slots, "overlay_slots": []},
        }
        with open(os.path.join(pack, "manifest.json"), "w", encoding="utf-8") as f:
            json.dump(manifest, f, ensure_ascii=False, indent=2)

    logo = os.path.join(ROOT, "Widget", "Assets", "GameLogos", "sanguosha.png")
    if os.path.abspath(a.logo) != os.path.abspath(logo):
        shutil.copyfile(a.logo, logo)
    print("staged", len(TIERS), "clips across 5 packs, and", len(SOUND_PACKS), "voice packs")


if __name__ == "__main__":
    main()
