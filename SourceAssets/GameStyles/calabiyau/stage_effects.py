"""Validate the complete runtime closure and stage only referenced resources."""
import gzip
import json
import math
import shutil
from pathlib import Path
from PIL import Image

HERE=Path(__file__).resolve().parent
REPO=HERE.parents[2]
DEST=REPO/'Widget/Assets/GameStyles/calabiyau'


def main():
    # Shipping the complete research library would add gigabytes to the app.
    # The independent ZIP builder owns distribution of non-default themes.
    import sys, importlib.util
    if '--research-full-runtime' not in sys.argv:
        spec=importlib.util.spec_from_file_location('calabiyau_modules',REPO/'Build-CalabiyauExternalPacks.py')
        module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
        print(module.stage_base(HERE,DEST));return
    catalog=json.loads((HERE/'packs.json').read_text(encoding='utf8'));assert len(catalog)==161
    referenced={'crosshair.json.gz','PORTING.md','packs.json'};textures=set();commands=0;frames=0;dimensions={}
    def validate(file):
        nonlocal commands,frames
        clip=json.loads(gzip.decompress(file.read_bytes()))
        assert clip['version']==1 and clip['fps']==60 and len(clip['frames'])==math.ceil(clip['duration']*60)
        for name in clip['textures']:
            assert Path(name).name==name and name.endswith('.png')
            if name not in dimensions:
                with Image.open(HERE/'textures'/name) as im:dimensions[name]=im.size;im.verify()
            textures.add(name)
        for frame in clip['frames']:
            for c in frame:
                assert len(c)==19 and all(math.isfinite(v) for v in c)
                assert 0<=c[18]<=1,(file,'opacity',c[18])
                assert int(c[0])==c[0] and 0<=c[0]<len(clip['textures'])
                w,h=dimensions[clip['textures'][int(c[0])]]
                assert c[5]>=-.001 and c[6]>=-.001 and c[5]+c[7]<=w+.001 and c[6]+c[8]<=h+.001,(file,c,w,h)
                commands+=1
            frames+=1
        return clip
    validate(HERE/'crosshair.json.gz')
    for entry in catalog:
        variant=entry['variant'];folder=HERE/'iconpacks'/variant
        report=json.loads((folder/'reconstructed.json').read_text(encoding='utf8'));assert report['revision']==3
        assert report['variant']==variant and len(report['clips'])==5
        for count in range(1,6):
            file=folder/('lower-'+str(count)+'.json.gz');validate(file);referenced.add(file.relative_to(HERE).as_posix())
        entry['name']=entry['name'].replace('（2D试验）','');entry['lower']='iconpacks/'+variant+'/lower-1.json.gz'
        entry['scope']='UMG sprites, Cascade particles, mesh geometry, reconstructed materials, native star/count widgets'
        entry['excluded']=[];entry['revision']=3;entry['clips']=report['clips'];entry['draws']=report['draws']
        (folder/'manifest.json').write_text(json.dumps(entry,ensure_ascii=False,indent=2),encoding='utf8')
        shutil.copy2(folder/'preview.png',folder/'preview-v2.png')
        referenced.update('iconpacks/'+variant+'/'+n for n in ['manifest.json','reconstructed.json','preview-v2.png'])
    (HERE/'packs.json').write_text(json.dumps(catalog,ensure_ascii=False,indent=2),encoding='utf8')
    textures.update(p.name for p in (HERE/'textures').glob('event-*.png'))
    referenced.update('textures/'+name for name in textures)
    for folder in ['killconfirm','soundpacks']:
        referenced.update(p.relative_to(HERE).as_posix() for p in (HERE/folder).rglob('*') if p.is_file())
    for relative in sorted(referenced):
        source=HERE/relative;target=DEST/relative;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target)
    # Keep obsolete generated files for diagnosis, outside runtime packaging.
    archive=REPO/'Output/CalabiyauObsoleteRuntime'
    for file in list(DEST.rglob('*')):
        if not file.is_file() or file.relative_to(DEST).as_posix() in referenced:continue
        target=archive/file.relative_to(DEST)
        file.resolve().relative_to(DEST.resolve());target.resolve().relative_to(archive.resolve())
        target.parent.mkdir(parents=True,exist_ok=True)
        if target.exists():target=target.with_name(target.stem+'-previous'+target.suffix)
        shutil.move(str(file),str(target))
    report={'packs':len(catalog),'clips':len(catalog)*5+1,'frames':frames,'commands':commands,'textures':len(textures),
            'runtime_files':len(referenced),'runtime_bytes':sum((DEST/p).stat().st_size for p in referenced)}
    (REPO/'Output/CalabiyauRuntimeManifest.json').write_text(json.dumps(report,indent=2),encoding='utf8');print(report)


if __name__=='__main__':main()
