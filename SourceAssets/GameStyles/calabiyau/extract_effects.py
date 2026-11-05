"""Resolve graphics imports using CUE4Parse's exact FName ObjectPaths."""
import argparse
import json
import re
import shutil
import subprocess
import sys
from pathlib import Path


def main():
    parser=argparse.ArgumentParser();parser.add_argument('research',type=Path);parser.add_argument('--variants',default='*')
    args=parser.parse_args();r=args.research
    sys.path.insert(0,str(r.parents[1]/'tools'));import calabiyau_scan as scan
    entries={e['path']:e for e in json.loads((r/'entries.json').read_text(encoding='utf8'))}
    by_name={Path(k).name:k for k in entries if k.endswith('.uasset')}
    source=r/'yugiri-source';out=r/'yugiri-json';render=r/'yugiri-render-input';refs=set();missing=set()
    def collect(data):return set(re.findall(r'"ObjectPath":\s*"(/Game/[^".]+)"',json.dumps(data)))
    for pattern in args.variants.split(','):
        for f in (r/'structure-parsed').rglob('WBP_IconFeedbackPage_PC_'+pattern+'.json'):
            nodes=[o for o in json.loads(f.read_text(encoding='utf-8-sig')) if ':WidgetTree\'' in o.get('Outer',{}).get('ObjectName','')]
            refs.update(collect(nodes))
            for o in nodes:
                if o['Type'].startswith('WBP_'):
                    key=by_name.get(o['Type'][:-2]+'.uasset')
                    if key:refs.add('/Game/'+key.removeprefix('PM/Content/').removesuffix('.uasset'))
    exe=r.parents[1]/'tools/cue4parse-cli/cue4parse.exe'
    def export(pattern,fmt=None):
        cmd=[str(exe),'-i',str(source),'-o',str(out if fmt=='json' else render),'-g','GAME_Strinova','-p',pattern,'-y']
        if fmt:cmd+=['-f',fmt]
        else:cmd+=['--mesh-format','Gltf2','--material-depth','AllLayers']
        subprocess.run(cmd,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,check=True)
    for iteration in range(16):
        added=0
        for ref in refs:
            base='PM/Content/'+ref.removeprefix('/Game/')
            if base+'.uasset' not in entries:missing.add(ref);continue
            for ext in ['.uasset','.uexp','.ubulk','.ufont']:
                key=base+ext;dst=source/key
                if key not in entries or dst.exists():continue
                src=r/'extracted'/key
                if not src.exists():src=scan.extract(entries[key])
                dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,dst)
                if ext=='.uasset':added+=1
        print('pass',iteration,'added',added,flush=True)
        if added:export('*.uasset','json')
        refs=set()
        for f in out.rglob('*.json'):refs.update(collect(json.loads(f.read_text(encoding='utf-8-sig'))))
        refs={ref for ref in refs if not (source/('PM/Content/'+ref.removeprefix('/Game/')+'.uasset')).exists()}
        if refs<=missing:break
    else:raise RuntimeError('Unresolved dependency cycle')
    for package in source.rglob('*.uasset'):
        key=str(package.relative_to(source).with_suffix('.ufont')).replace('\\','/')
        if key in entries and not (source/key).exists():shutil.copy2(scan.extract(entries[key]),source/key)
    (r/'yugiri-unresolved-imports.json').write_text(json.dumps(sorted(missing),indent=2),encoding='utf8')
    export('*.uasset','png');export('*/Meshes/*.uasset')
    print('complete',len(list(source.rglob('*.uasset'))),'packages; missing',len(missing),flush=True)


if __name__=='__main__':main()
