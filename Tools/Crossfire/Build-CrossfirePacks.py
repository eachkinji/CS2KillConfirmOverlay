"""Package each downloaded CF suite separately, without modifying source assets."""
import argparse
import hashlib
import json
import re
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED


def build(source, output):
    source, output = Path(source).resolve(), Path(output).resolve()
    if source == output or source in output.parents:
        raise ValueError("Output must be outside the source collection")
    output.mkdir(parents=True, exist_ok=True)
    common = source / "06_兵种等级徽章" / "多杀火焰光效"
    result, used_names = [], set()
    for category in sorted(source.iterdir()):
        if not category.is_dir():
            continue
        for suite in sorted(category.iterdir()):
            if not suite.is_dir():
                continue
            match = re.match(r"^(.*[\u4e00-\u9fff][^_]*)_", suite.name)
            title = (match.group(1) if match else suite.name).strip()
            if title == "其他":
                rank = re.search(r"RANKMACH(\d+)KILLMARK([23]?)$", suite.name, re.I)
                if rank:
                    title = "排位" + rank.group(1) + "—样式" + (rank.group(2) or "1")
            category_title = category.name.split("_", 1)[-1]
            title = re.sub(r'[<>:"/\\|?*]', "—", title).rstrip(". ")
            name = "穿越火线—" + category_title + "—" + title + "—图标包"
            if name.casefold() in used_names:
                raise ValueError("Duplicate package name: " + name)
            used_names.add(name.casefold())
            entries = {}
            for file in sorted(suite.rglob("*")):
                if not file.is_file():
                    continue
                relative = file.relative_to(suite).as_posix()
                if re.fullmatch(r"SPRITE(?:NORMAL|SPECIAL)?_\d{2}\.PNG", file.name, re.I):
                    relative = "Sprite/" + file.name
                if relative.casefold() in entries:
                    raise ValueError("Duplicate source asset: " + str(file))
                entries[relative.casefold()] = (relative, file)
            if category.name != "06_兵种等级徽章":
                for file in sorted(common.glob("*.PNG")):
                    entries.setdefault(file.name.casefold(), (file.name, file))
            head = None
            for preferred in ["badge_headshot.png", "badge_multi1.png", "badge_assault3.png", "multi2_fx.png"]:
                match = entries.get(preferred)
                if match:
                    head = match[1]
                    break
            if head is None:
                head = next((item[1] for item in entries.values() if item[1].suffix.lower() == ".png"), None)
            if head is None:
                raise ValueError("No cover image in " + str(suite))
            archive = output / (name + ".zip")
            # One Chinese root folder keeps the editor's suggested name readable.
            with ZipFile(archive, "w", ZIP_DEFLATED) as z:
                for relative, file in entries.values():
                    z.write(file, name + "/" + relative)
                z.write(head, name + "/pack_head.png")
                z.writestr(name + "/manifest.json", json.dumps({
                    "game": "crossfire", "kind": "icon", "display_name": name,
                    "head_image": "pack_head.png", "format_version": 1,
                }, ensure_ascii=False, indent=2))
            with ZipFile(archive) as z:
                bad = z.testzip()
                if bad:
                    raise ValueError("Corrupt entry: " + bad)
            result.append({"name": name, "source": str(suite.relative_to(source)),
                           "file": archive.name, "assets": len(entries),
                           "sha256": hashlib.sha256(archive.read_bytes()).hexdigest()})
    (output / "图标包清单.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"packages": len(result), "output": str(output)}, ensure_ascii=False))
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    build(args.source, args.output)
