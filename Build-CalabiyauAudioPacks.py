"""Build modular, typed Calabiyau audio packs from verified local game exports.

Theme audio has already been rendered from Wwise by audio/export_audio.py.
Character voices are selected through the actual trigger -> RoleVoice -> Chinese
AkEvent -> soundbank graph, never by blindly mixing every file in a kill bank.
The game installation is read-only. Only the five basic SFX ship in the app.
"""
import argparse
import collections
import concurrent.futures
import hashlib
import json
import math
from pathlib import Path
import re
import shutil
import subprocess
import struct
import sys
import wave
import zipfile

ROOT = Path(__file__).resolve().parent
WORKSPACE = ROOT.parent
sys.path.insert(0, str(WORKSPACE / 'tools/calabiyau-render-python'))
import numpy as np


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf8')


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def title(text):
    return re.sub(r'[<>:"/\\|?*]', '－', text).strip().rstrip('.')


def package(output, manifest, files):
    suffix = '语音包' if manifest['audio_kind'] == 'character_voice' else '音效包'
    name = title('卡拉彼丘—' + manifest['display_name'] + '—' + suffix)
    dest = output / suffix / (name + '.zip')
    dest.parent.mkdir(parents=True, exist_ok=True)
    manifest['files'] = [dict(path=key, bytes=path.stat().st_size, sha256=digest(path))
                         for key, path in sorted(files.items())]
    with zipfile.ZipFile(dest, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        archive.writestr(name + '/manifest.json', json.dumps(manifest, ensure_ascii=False, indent=2))
        for key, path in sorted(files.items()):
            archive.write(path, name + '/' + key)
    # Validate the deliverable, including every manifest reference and archive hash.
    with zipfile.ZipFile(dest) as archive:
        assert archive.testzip() is None
        for item in manifest['files']:
            raw = archive.read(name + '/' + item['path'])
            assert len(raw) == item['bytes'] and hashlib.sha256(raw).hexdigest() == item['sha256']
        for value in manifest['audio']['slots'].values():
            assert all(p in files for p in (value if isinstance(value, list) else [value]))
    return dict(id=manifest['id'], name=manifest['display_name'], audio_kind=manifest['audio_kind'],
                file=dest.relative_to(output).as_posix(), bytes=dest.stat().st_size, sha256=digest(dest),
                associated_icon_keys=manifest.get('associated_icon_keys', []))


def sound_effects(output, stage_base):
    audition = ROOT / 'Output/CalabiyauAudio'
    index = read(audition / '音效索引.json')
    results = []
    linked = []
    for group in index['groups']:
        if group.get('training'):
            continue
        variants = group['variants']
        key = 'calabiyau_sfx_' + (variants[0]['id'] if variants else 'default')
        icons = ['calabiyau_' + v['id'] for v in variants] or ['calabiyau']
        linked.extend(icons if variants else [])
        tracks = {x['label']: x for x in group['tracks']}
        files = {f'{i}kill.wav': audition / tracks[f'{i}杀']['file'] for i in range(1, 6)}
        if group.get('image'):
            files['pack_head.png'] = audition / group['image']
        manifest = dict(format_version=1, package_kind='audio', id=key,
                        name=group['name'], display_name=group['name'], game_style='calabiyau',
                        audio_kind='sound_effect', version='1.0', associated_icon_keys=icons,
                        audio=dict(base_gain=1.0, slots={f'kill_{i}': f'{i}kill.wav' for i in range(1, 6)},
                                   overlay_slots=[]),
                        source_events={f'kill_{i}': tracks[f'{i}杀']['event'] for i in range(1, 6)})
        results.append(package(output, manifest, files))
        if not variants and stage_base:
            manifest['id'] = 'calabiyau'
            for base in [ROOT / 'SourceAssets/GameStyles/calabiyau/soundpacks/calabiyau',
                         ROOT / 'Widget/Assets/GameStyles/calabiyau/soundpacks/calabiyau']:
                base.mkdir(parents=True, exist_ok=True)
                for name, path in files.items():
                    shutil.copy2(path, base / name)
                write(base / 'manifest.json', manifest)
    assert len(linked) == 161 and len(set(linked)) == 161
    return results


def voice_sources(research):
    tables = research / 'voice-config-json'
    def table(name):
        return read(next(tables.rglob(name + '.json')))[0]['Rows']
    names = read(next(tables.rglob('ST_RoleName.json')))[0]['StringTable']['KeysToEntries']
    profiles, voices, skins = table('RoleProfile'), table('RoleVoice'), table('RoleSkin')
    plan = read(research / 'audio/voice-source-plan.json')
    events = {p.stem: read(p)[0] for p in (research / 'voice-events-json').rglob('*.json')}
    slots = {'Event.Kill': 'kill_1', **{'UIEvent.ConsecutiveKill.' + word: f'kill_{i}'
             for i, word in enumerate(['Two', 'Three', 'Four', 'Five', 'Six'], 2)}}
    triggers = {row['Id']: row for row in plan['triggers']}
    groups = collections.OrderedDict()
    selected = {}

    def add(row, voice_ids, skin_ids):
        if row['RoleId'] == 306:  # PvE boss, not a selectable player character.
            return
        role = profiles[str(row['RoleId'])]
        role_key = role['NameCn']['Key']
        role_name = names.get(role['NameShortCn']['Key'], names[role_key])
        for voice_id in voice_ids:
            voice = voices[str(voice_id)]
            event_name = voice['AkEvent']['AssetPathName'].split('.')[-1]
            event = events[event_name]
            language = next(x['Value'] for x in event['EventCookedData']['EventLanguageMap']
                            if x['Key']['LanguageName'] == 'Chinese')
            bank = event['Properties']['RequiredBank']['ObjectPath'].split('/')[-1]
            assert '_InGame_Kill_' in bank, bank
            variant = bank.split('_InGame_Kill_')[1]
            # Skin variants use their actual localized name, not red01/red02.
            skin_name = skins[str(skin_ids[0])]['NameCn']['SourceString'] if skin_ids else '原版'
            key = 'calabiyau_voice_' + bank.removeprefix('Vox_').replace('_InGame_Kill_', '_').lower()
            group = groups.setdefault(key, dict(id=key, name=f'{role_name}·{skin_name}',
                role=role_key, role_id=row['RoleId'], skin_ids=skin_ids, bank=bank, slots=collections.defaultdict(list),
                delays={}, cooldowns={}, evidence=[]))
            slot = slots[row['GameplayTag']['TagName']]
            if event_name not in group['slots'][slot]:
                group['slots'][slot].append(event_name)
            group['delays'][slot] = round(row['DelayPlayTime'] * 1000)
            group['cooldowns'][slot] = round(row['CoolDownTime'] * 1000)
            group['evidence'].append(dict(slot=slot, trigger_id=row['Id'], voice_id=voice_id,
                event=event_name, event_id=language['EventId'],
                text=voice.get('Content', {}).get('SourceString'),
                delay_ms=group['delays'][slot], cooldown_ms=group['cooldowns'][slot]))
            selected[event_name] = dict(bank=bank, language=language)

    for row in plan['triggers']:
        add(row, row['RandomVoiceIds'] if row['IsRandom'] else [row['VoiceId']], [])
    for row in plan['upgrades']:
        add(triggers[row['TriggerInGameVoiceId']], row['RandomVoiceIdList'], row['RoleSkinIdList'])
    assert len(groups) == 50 and len({x['role'] for x in groups.values()}) == 24
    for group in groups.values():
        assert set(group['slots']) == {f'kill_{i}' for i in range(1, 7)}, group['name']
    return groups, selected


def decode_voices(research, selected):
    sys.path.insert(0, str(WORKSPACE / 'tools'))
    import calabiyau_scan as scanner
    entries = {e['path']: e for e in read(research / 'entries.json')}
    required = {'PM/Content/WwiseAudio/Init.bnk'}
    for event in selected.values():
        required.add('PM/Content/WwiseAudio/Chinese/' + event['bank'] + '.bnk')
        for media in event['language']['Media']:
            required.add('PM/Content/WwiseAudio/' + media['MediaPathName'])
    for path in sorted(required):
        target = research / 'extracted' / path
        if not target.is_file() or target.stat().st_size != entries[path]['size']:
            scanner.extract(entries[path])
    audio = research / 'audio'
    names = audio / 'voice-wwnames.txt'
    names.write_text('\n'.join(sorted(selected)), encoding='utf8')
    banks = collections.defaultdict(list)
    for name, event in selected.items():
        banks[event['bank']].append(name)
    bank_root = research / 'extracted/PM/Content/WwiseAudio'
    txtp_root = audio / 'character-txtp'
    for i, (bank, events) in enumerate(banks.items(), 1):
        out = txtp_root / bank
        if not all(any(out.glob(name + '*.txtp')) for name in events):
            command = [sys.executable, str(WORKSPACE / 'tools/wwiser/wwiser.pyz'),
                str(bank_root / 'Init.bnk'), str(bank_root / 'Chinese' / (bank + '.bnk')),
                '-d', 'none', '-nl', str(names), '-g', '-gd', '-gv', '1.0', '-gwd',
                '-go', str(out), '-gw', str(bank_root / 'Media/Chinese' / bank),
                '-gf', *[str(selected[name]['language']['EventId']) for name in events]]
            run = subprocess.run(command, capture_output=True, text=True, encoding='utf8', errors='replace')
            (audio / (bank + '.log')).write_text(run.stdout + run.stderr, encoding='utf8')
            if run.returncode:
                raise RuntimeError(bank + ': ' + run.stderr)
        print('Voice bank', i, '/', len(banks), bank, flush=True)
    output = audio / 'character-wav'
    output.mkdir(exist_ok=True)
    floating = audio / 'character-float'
    floating.mkdir(exist_ok=True)

    def decode(item):
        name, event = item
        # Wwise selects a dry or reverberant rendering for some multikill lines.
        # Use the ordinary dry line, verified by the source media's DebugName,
        # rather than baking a map's environmental reverb into the voice pack.
        dry = [m for m in event['language']['Media'] if 'reverb' not in m['DebugName'].lower()]
        if len(dry) > 1:
            # Yugiri's red-skin 5/6-kill reverb files omit the suffix; the
            # alternate lives in Uniskill and the ordinary line at role root.
            dry = [m for m in dry if '\\uniskill\\' not in m['DebugName'].lower()]
        assert len(dry) == 1, name
        candidates = []
        for path in (txtp_root / event['bank']).glob(name + '*.txtp'):
            if path.stem != name and not path.stem.startswith(name + ' '):
                continue
            source = path.read_text(encoding='utf8')
            media_ids = set(int(n) for n in re.findall(r'#\s+Source (\d+)', source))
            if media_ids == {dry[0]['MediaId']}:
                candidates.append(path)
        assert candidates, name
        txp = sorted(candidates, key=lambda p: (len(p.name), p.name))[0]
        text = txp.read_text(encoding='utf8')
        event_id = int(re.search(r'CAkEvent\[\d+\] (\d+)', text).group(1))
        assert event_id == event['language']['EventId'], name
        # Each generated event is a single, resolved media source. The role
        # manifest retains random selection among the six ordinary-kill lines.
        media_lines = [line.strip() for line in text.splitlines() if line.strip() and not line.lstrip().startswith('#') and '.wem' in line]
        assert media_lines and len(set(media_lines)) == 1, name
        dest = floating / (name + '.wav')
        if not dest.exists():
            run = subprocess.run([str(WORKSPACE / 'tools/vgmstream/vgmstream-cli.exe'),
                '-i', '-W', '4', '-o', str(dest), str(txp)], capture_output=True, text=True, encoding='utf8', errors='replace')
            if run.returncode:
                raise RuntimeError(name + ': ' + run.stderr)
        raw = dest.read_bytes()
        assert raw[:4] == b'RIFF' and raw[8:12] == b'WAVE'
        chunks = {}; pos = 12
        while pos + 8 <= len(raw):
            tag, size = struct.unpack_from('<4sI', raw, pos)
            chunks[tag] = raw[pos + 8:pos + 8 + size]
            pos += 8 + size + (size & 1)
        codec, channels, rate = struct.unpack_from('<HHI', chunks[b'fmt '])
        samples = np.frombuffer(chunks[b'data'], dtype='<f4').reshape(-1, channels)
        assert codec == 3 and channels in (1, 2) and rate == 44100
        assert len(samples) > 0 and np.isfinite(samples).all() and np.abs(samples).max() > .0001, name
        return name, samples, channels, rate
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        decoded = list(pool.map(decode, selected.items()))
    gain = min(1.0, .98 / max(float(np.abs(samples).max()) for _, samples, _, _ in decoded))
    result = {}
    for name, samples, channels, rate in decoded:
        dest = output / (name + '.wav')
        pcm = np.rint(samples * gain * 32767).astype('<i2')
        assert pcm.min() > -32768 and pcm.max() < 32767 and np.any(pcm), name
        with wave.open(str(dest), 'wb') as stream:
            stream.setnchannels(channels); stream.setsampwidth(2); stream.setframerate(rate)
            stream.writeframes(pcm.tobytes())
        result[name] = dest
    write(audio / 'character-validation.json', dict(events=len(result), event_ids_verified=True,
          ordinary_dry_media_verified=True, silent_or_clipped_files=0, shared_gain=gain,
          shared_gain_db=20 * math.log10(gain)))
    return result


def voice_covers(research, groups):
    """Use the game's square, skin-specific inventory portrait without resizing it."""
    sys.path.insert(0, str(WORKSPACE / 'tools'))
    import calabiyau_scan as scanner
    skins = read(next((research / 'voice-config-json').rglob('RoleSkin.json')))[0]['Rows']
    entries = {entry['path']: entry for entry in read(research / 'entries.json')}
    source = research / 'voice-cover-source'
    rendered = research / 'voice-cover-png'
    records = {}
    for group in groups.values():
        if group['skin_ids']:
            candidates = [skins[str(skin_id)] for skin_id in group['skin_ids']]
            assert all(skin['RoleId'] == group['role_id'] for skin in candidates), group['name']
            # A voice bank shared by upgraded skins uses the first configured
            # skin, just as its displayed theme name does.
            skin = candidates[0]
        else:
            candidates = [skin for skin in skins.values() if skin['RoleId'] == group['role_id']
                          and skin['Quality'] == 0 and skin['BasicSkinId'] == 0]
            assert len(candidates) == 1, ('Ambiguous default skin', group['name'])
            skin = candidates[0]
        asset = skin['IconItem']['AssetPathName']
        assert asset.startswith('/Game/'), (group['name'], asset)
        package_path = 'PM/Content/' + asset.split('.')[0].removeprefix('/Game/')
        assert package_path + '.uasset' in entries, asset
        for extension in ['.uasset', '.uexp', '.ubulk']:
            path = package_path + extension
            if path not in entries:
                continue
            extracted = research / 'extracted' / path
            if not extracted.is_file() or extracted.stat().st_size != entries[path]['size']:
                extracted = scanner.extract(entries[path])
            destination = source / path
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(extracted, destination)
        records[group['id']] = dict(role_id=group['role_id'], skin_id=skin['RoleSkinId'],
            field='RoleSkin.IconItem', asset=asset, package_path=package_path)
    run = subprocess.run([str(WORKSPACE / 'tools/cue4parse-cli/cue4parse.exe'),
        '-i', str(source), '-o', str(rendered), '-g', 'GAME_Strinova', '-p', '*.uasset',
        '-f', 'png', '-y'], capture_output=True, text=True, encoding='utf8', errors='replace')
    (research / 'voice-cover-export.log').write_text(run.stdout + run.stderr, encoding='utf8')
    if run.returncode:
        raise RuntimeError('Character portrait export failed: ' + run.stderr)
    result = {}
    for key, record in records.items():
        image = rendered / source.name / (record.pop('package_path') + '.png')
        raw = image.read_bytes()
        assert raw[:8] == b'\x89PNG\r\n\x1a\n', image
        width, height = struct.unpack_from('>II', raw, 16)
        assert width == height and width >= 256, (image, width, height)
        record.update(width=width, height=height, sha256=digest(image))
        result[key] = (image, record)
    assert len(result) == 50 and len({record['sha256'] for _, record in result.values()}) == 50
    write(research / 'audio/character-cover-validation.json', dict(
        packs=len(result), native_square_portraits=True, default_or_skin_identity_verified=True,
        covers={key: record for key, (_, record) in result.items()}))
    return result


def character_voices(research, output):
    groups, selected = voice_sources(research)
    decoded = decode_voices(research, selected)
    covers = voice_covers(research, groups)
    results = []
    provenance = []
    for group in groups.values():
        files = {name + '.wav': decoded[name] for values in group['slots'].values() for name in values}
        cover, cover_source = covers[group['id']]
        files['pack_head.png'] = cover
        manifest = dict(format_version=1, package_kind='audio', id=group['id'], game_style='calabiyau',
            name=group['name'], display_name=group['name'], audio_kind='character_voice', version='1.1',
            source_cover=cover_source,
            language='zh-CN', audio=dict(base_gain=1.0, overlay_slots=[],
                slots={slot: [name + '.wav' for name in values] for slot, values in group['slots'].items()},
                slot_delays_ms=group['delays'], slot_cooldowns_ms=group['cooldowns']))
        results.append(package(output, manifest, files))
        provenance.append({**{key: value for key, value in group.items() if key != 'slots'},
                           'cover': cover_source})
    write(output / '角色语音来源.json', provenance)
    return results


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--research', type=Path, default=WORKSPACE / 'workshop/CalabiyauResearch')
    parser.add_argument('--output', type=Path, default=ROOT / 'Output/CalabiyauAudioPacks38')
    parser.add_argument('--stage-base', action='store_true')
    selection = parser.add_mutually_exclusive_group()
    selection.add_argument('--sfx-only', action='store_true')
    selection.add_argument('--voice-only', action='store_true')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    result = [] if args.voice_only else sound_effects(args.output, args.stage_base)
    if not args.sfx_only:
        result += character_voices(args.research, args.output)
    index_path = args.output / 'index.json'
    if (args.voice_only or args.sfx_only) and index_path.is_file():
        updated_kind = 'character_voice' if args.voice_only else 'sound_effect'
        result += [entry for entry in read(index_path) if entry['audio_kind'] != updated_kind]
    write(index_path, result)
    (args.output / '导入说明.txt').write_text(
        '适用于 5.0.1.37 及以上版本。\n'
        '卡拉彼丘 → 音效包：导入主题音效 ZIP；启用图标与音效联动后，切换图标会选择已安装的配套音效。\n'
        '卡拉彼丘 → 语音包：导入角色语音 ZIP，独立选择角色，支持“无语音”。\n'
        '语音包封面使用对应角色或皮肤的原生方形头像；旧包重新导入即可更新封面并保留选择。\n'
        '角色语音只含明确映射的普通击杀与二至六杀，不含角色关系、队友或特定技能彩蛋。\n'
        '保留原始角色台词延迟和各档位冷却，快速连续击杀不会重复堆叠同一句话。\n'
        '每个图标配色仍是独立图标包；共用音效的配色只需安装一份音效包。\n'
        '离线声音还原不模拟游戏运行时动态滤波、空间化或 RTPC；详见原音效试听索引。\n', encoding='utf8')
    print('Validated packs:', len(result), 'bytes:', sum(x['bytes'] for x in result), flush=True)
