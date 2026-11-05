"""Verify every external ZIP against its manifest and the original bake."""
import argparse
import gzip
import hashlib
import json
import zipfile
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--packages', type=Path, required=True)
parser.add_argument('--source', type=Path, required=True)
args = parser.parse_args()
catalog = json.loads((args.packages / 'index.json').read_text(encoding='utf8'))
assert len(catalog) == 161 and len({p['id'] for p in catalog}) == 161
clips = textures = 0
for i, entry in enumerate(catalog):
    archive = args.packages / entry['file']
    with archive.open('rb') as file:
        assert hashlib.file_digest(file, 'sha256').hexdigest() == entry['sha256']
    with zipfile.ZipFile(archive) as z:
        manifests = [n for n in z.namelist() if n.endswith('/manifest.json')]
        assert len(manifests) == 1
        prefix = manifests[0][:-len('manifest.json')]
        manifest = json.loads(z.read(manifests[0]))
        assert manifest['id'] == entry['id'] and manifest['package_kind'] == 'calabiyau_icon'
        names = {f['path'] for f in manifest['files']}
        assert len(names) == len(manifest['files'])
        assert set(z.namelist()) == {prefix + n for n in names} | {prefix + 'manifest.json'}
        assert {'lower-' + str(n) + '.json.gz' for n in range(1, 7)} <= names
        for f in manifest['files']:
            data = z.read(prefix + f['path'])
            assert len(data) == f['bytes'] and hashlib.sha256(data).hexdigest() == f['sha256']
            if f['path'].startswith('lower-'):
                assert data == (args.source / 'iconpacks' / entry['id'].removeprefix('calabiyau_') / f['path']).read_bytes()
                clip = json.loads(gzip.decompress(data))
                assert clip['fps'] == 60
                for name in clip['textures']:
                    assert name in ('event-icon.png', 'event-label.png', 'event-count.png') or 'textures/' + name in names
                assert all(0 <= c[18] <= 1 for frame in clip['frames'] for c in frame)
                clips += 1
            elif f['path'].startswith('textures/'):
                assert data == (args.source / f['path']).read_bytes()
                textures += 1
    if (i + 1) % 20 == 0: print(f'Validated {i + 1}/161', flush=True)
assert clips == 966
print(f'PASS: 161 independent ZIPs, {clips} source-identical animations, {textures} source-identical textures; all checksums and references valid.')
