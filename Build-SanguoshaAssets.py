"""Stage the built-in Sanguosha (三国杀) broadcast assets and voice pack."""
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
    "shenweizhengqiankun", "wanfumodi", "shenyimiaoshou", "qingnangjishi", "podi",
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
            "knife.mp3": ("knife", "newkill_shenyimiaoshou"),
            "grenade.mp3": ("grenade", "newkill_qingnangjishi"),
            "podi.mp3": ("podi", "audio_podi"),
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

    # Preview images for retired built-in packs must not survive regeneration.
    for base_root in (
        os.path.join(ROOT, "Widget", "Assets", "GameStyles", "sanguosha", "previews"),
        os.path.join(ROOT, "SourceAssets", "GameStyles", "sanguosha", "previews"),
    ):
        shutil.rmtree(base_root, ignore_errors=True)

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
    print("staged", len(TIERS), "clips, and", len(SOUND_PACKS), "voice packs")


if __name__ == "__main__":
    main()
