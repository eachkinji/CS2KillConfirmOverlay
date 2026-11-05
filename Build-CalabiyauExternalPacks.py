"""Build independent Calabiyau theme ZIPs and stage only the basic runtime."""
import argparse
import gzip
import hashlib
import json
import shutil
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent


def base_files(source):
    files = {'crosshair.json.gz'}
    for directory in ('killconfirm', 'soundpacks'):
        files.update(p.relative_to(source).as_posix() for p in (source / directory).rglob('*') if p.is_file())
    # The built-in pack has its own multi-kill scenes. Include only their
    # referenced textures, keeping all independently installed themes external.
    clips = ['crosshair.json.gz'] + [f'killconfirm/default-lower-{count}.json.gz' for count in range(1, 7)]
    for relative in clips:
        clip = json.loads(gzip.decompress((source / relative).read_bytes()))
        files.add(relative)
        for name in clip['textures']:
            if not name.endswith('.png') or any(c in name for c in '/\\:'):
                raise ValueError(f'Invalid basic texture reference: {name}')
            files.add('textures/' + name)
    files.update('textures/' + p.name for p in (source / 'textures').glob('event-*.png'))
    files.update('textures/' + p.name for p in (source / 'textures').glob('label-*.png'))
    return files


def stage_base(source, destination):
    files = base_files(source)
    for name in sorted(files):
        target = destination / name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source / name, target)
    # Preserve old runtime material outside the application instead of shipping it.
    archive = ROOT / 'Output/CalabiyauModularizedRuntime'
    for file in list(destination.rglob('*')):
        if not file.is_file() or file.relative_to(destination).as_posix() in files:
            continue
        relative = file.relative_to(destination)
        target = archive / relative
        file.resolve().relative_to(destination.resolve())
        target.resolve().relative_to(archive.resolve())
        target.parent.mkdir(parents=True, exist_ok=True)
        if target.exists():
            # The complete source is retained separately; use a unique archive name.
            target = target.with_name(target.name + '.previous')
        shutil.move(str(file), str(target))
    return {'files': len(files), 'bytes': sum((destination / n).stat().st_size for n in files)}


def build(source, output, variants=None):
    output.mkdir(parents=True, exist_ok=True)
    catalog = json.loads((source / 'packs.json').read_text(encoding='utf8'))
    result = []
    for entry in catalog:
        if variants and entry['variant'] not in variants: continue
        variant = entry['variant']; folder = source / 'iconpacks' / variant
        # Thumbnails come from the production Win2D render, after the current
        # bake. An older thumbnail can display the very layering bug we fixed.
        preview = folder / 'preview-v2.png'
        clip_paths = [folder / f'lower-{count}.json.gz' for count in range(1, 7)]
        if not preview.is_file() or preview.stat().st_mtime_ns < max(p.stat().st_mtime_ns for p in clip_paths):
            raise ValueError(f'{variant}: regenerate native previews with build_previews.py after baking')
        files = {'pack_head.png': preview}
        for count in range(1, 7):
            path = folder / f'lower-{count}.json.gz'
            clip = json.loads(gzip.decompress(path.read_bytes()))
            assert all(0 <= c[18] <= 1 for frame in clip['frames'] for c in frame)
            files[path.name] = path
            for name in clip['textures']:
                if name not in ('event-icon.png', 'event-label.png', 'event-count.png'):
                    files['textures/' + name] = source / 'textures' / name
        entries = [{'path': name, 'bytes': path.stat().st_size,
                    'sha256': hashlib.file_digest(path.open('rb'), 'sha256').hexdigest()}
                   for name, path in sorted(files.items())]
        manifest = {'format_version': 1, 'package_kind': 'calabiyau_icon', 'game_style': 'calabiyau',
                    'id': 'calabiyau_' + variant, 'display_name': entry['name'], 'files': entries}
        title = '卡拉彼丘—' + entry['name'].replace(' · ', '—') + '—图标包'
        target = output / (title + '.zip')
        with zipfile.ZipFile(target, 'w', zipfile.ZIP_DEFLATED, compresslevel=1) as archive:
            archive.writestr(title + '/manifest.json', json.dumps(manifest, ensure_ascii=False, indent=2))
            for name, path in sorted(files.items()):
                archive.write(path, title + '/' + name)
        result.append({'id': manifest['id'], 'name': entry['name'], 'file': target.name,
                       'bytes': target.stat().st_size,
                       'sha256': hashlib.file_digest(target.open('rb'), 'sha256').hexdigest()})
        print(f'{len(result)}/{len(catalog)} {variant}: {target.stat().st_size / 1024**2:.1f} MB', flush=True)
    (output / 'index.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf8')
    (output / '导入说明.txt').write_text('安装 5.0.1.38 或更高版本后，选择卡拉彼丘 → 图标包 → 导入图标包。\n'
        '每个 ZIP 是一个独立变体，可单个、批量或拖放导入。无需解压，也无需全部安装。\n'
        '基础效果和原生击杀准心内置在程序中。重新导入同一包会更新素材并保留选择，删除包不影响其他主题。\n'
        '本版修正动画图层；已有旧版图标包需要重新导入新版 ZIP，单独更新程序不会替换外部素材。\n', encoding='utf8')
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--source', type=Path, default=ROOT / 'SourceAssets/GameStyles/calabiyau')
    parser.add_argument('--output', type=Path, default=ROOT / 'Output/CalabiyauIconPacks')
    parser.add_argument('--base-only', action='store_true')
    parser.add_argument('--variants', help='Comma-separated variants, for focused validation')
    parser.add_argument('--stage-base', action='store_true')
    args = parser.parse_args()
    if not args.base_only: build(args.source, args.output, set(args.variants.split(',')) if args.variants else None)
    if args.stage_base or args.base_only:
        result = stage_base(args.source, ROOT / 'Widget/Assets/GameStyles/calabiyau')
        (ROOT / 'Output').mkdir(parents=True, exist_ok=True)
        (ROOT / 'Output/CalabiyauBaseManifest.json').write_text(json.dumps(result, indent=2), encoding='utf8')
        print('Basic runtime:', result)
