"""Build separate CF packages from an external source archive; no bundled media."""
import argparse
import json
import shutil
import tempfile
import zipfile
from pathlib import Path

ICONS = [
    ("default", "Original", "原版"), ("vip", "Vip", "会员"),
    ("angelic_beast", "AngelicBeast", "天使野兽"),
    ("anniversary_10", "Anniversary10", "十周年"),
    ("anniversary_15", "Anniversary15", "十五周年"), ("cfpl", "CFPL", "职业联赛"),
    ("rankmach_2019_1", "Rankmach2019_1", "排位赛一"),
    ("rankmach_2019_2", "Rankmach2019_2", "排位赛二"),
]


def build(source, output):
    output.mkdir(parents=True, exist_ok=True)
    art = source / "KillConfirmCode"
    produced = []

    def archive(folder, name):
        target = output / (name + ".zip")
        with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED) as z:
            for file in sorted(folder.rglob("*")):
                if file.is_file():
                    z.write(file, str(Path(name) / file.relative_to(folder)))
        produced.append(target)

    with tempfile.TemporaryDirectory(prefix="cf-external-") as temp:
        for key, legacy, name in ICONS:
            dest = Path(temp) / key
            dest.mkdir()
            for folder in [art, art / "Original", art / "Knife", art / "FirstLast",
                           art / "CommonFx", art / "EliteUpgrade", art / "WeaponBadge", art / legacy]:
                for file in folder.iterdir():
                    if file.is_file() and file.suffix.lower() in (".png", ".tga", ".jpg", ".webp"):
                        # One casing per canonical filename, including on case-sensitive hosts.
                        shutil.copy2(file, dest / file.name.lower())
            shutil.copy2(dest / "badge_headshot.png", dest / "pack_head.png")
            if key == "default":
                # Preserve the retired frame exports outside the application as well.
                shutil.copytree(source / "crossfire" / "animations", dest / "legacy_frames")
            title = "穿越火线—" + name + "—图标包"
            (dest / "manifest.json").write_text(json.dumps({
                "format_version": 1, "package_kind": "crossfire_icon", "id": key,
                "game_style": "crossfire", "display_name_zh_cn": title,
            }, ensure_ascii=False, indent=2), encoding="utf-8")
            archive(dest, title)

        for folder in sorted((source / "crossfire" / "soundpacks").iterdir()):
            if not folder.is_dir():
                continue
            dest = Path(temp) / folder.name
            shutil.copytree(folder, dest)
            manifest = json.loads((dest / "manifest.json").read_text(encoding="utf-8-sig"))
            name = manifest["name"].replace(" (", "—").replace("(", "—").replace(")", "")
            title = "穿越火线—" + name + "—音频包"
            manifest.update(package_kind="crossfire_voice", format_version=1, display_name_zh_cn=title)
            (dest / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
            character = folder.name.removeprefix("crossfire_").removesuffix("_gr").removesuffix("_bl")
            head = source / "PackIcons" / ({"v_sex": "cfsex"}.get(character, character) + ".png")
            shutil.copy2(head, dest / "pack_head.png")
            archive(dest, title)
    return produced


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    packages = build(args.source, args.output)
    print(f"Built {len(packages)} independent packages in {args.output}")
