"""Bake the compact built-in default's real multi-kill UMG branches and stars."""
import argparse
import json
import time
from pathlib import Path

from bake_effects import Atlas, Bake
from build_packs import Assets, HERE, read, write_clip
from reconstruct import EffectScene
from ue_effects import Effects


def register_default_effects(fx, research):
    """The default screen-effect dependencies live outside the theme corpus."""
    for path in (research/'default-json').rglob('*.json'):
        key='/Game/'+str(path.with_suffix('')).replace('\\','/').split('/PM/Content/',1)[1]
        data=read(path);fx.packages[key]=data
        for obj in data:
            if obj['Type'] in ('Material','MaterialInstanceConstant','Texture2D','ParticleSystem','StaticMesh'):
                fx.objects[key]=obj
    for path in (research/'default-render-input').rglob('*.png'):
        key='/Game/'+str(path.with_suffix('')).replace('\\','/').split('/PM/Content/',1)[1]
        fx.pngs[key]=path


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('research',type=Path)
    args=parser.parse_args()
    path=args.research/'preview/parsed/extracted/PM/Content/PaperMan/UI/BP/PC/InGame/HUDPC/FeedBack/WBP_IconFeedbackPage.json'
    assets=Assets(args.research);fx=Effects(args.research)
    register_default_effects(fx,args.research)
    folder=HERE/'killconfirm';folder.mkdir(exist_ok=True)
    atlas=Atlas('default');additive=Atlas('add-default')
    reports=[];all_textures=set();started=time.monotonic()
    for count in range(1,7):
        animation='Anim_Normal' if count==1 else 'Anim_MaxCount' if count==2 else 'Anim_MaxCount_'+str(min(count,5))
        scene=EffectScene(path,assets,fx,animation,count=count)
        clip=Bake(scene,atlas,additive).run()
        omitted=scene.omitted-{'MovieSceneEventTrack'}
        if omitted:raise RuntimeError('Default omitted '+str(omitted))
        dest=folder/('default-lower-'+str(count)+'.json.gz')
        write_clip(dest,clip)
        all_textures.update(clip['textures'])
        # Unlike themes, the original single-kill Anim_Normal explicitly hides
        # Panel_Stars. Multi-kill branches reveal it before the 0.7 s star cue.
        expected=[] if count==1 else [scene.star_delay+i*scene.star_step for i in range(len(scene.star_indices))]
        reports.append({'count':count,'animation':animation,'duration':clip['duration'],
                        'star_indices':[] if count==1 else scene.star_indices,'star_times':expected,
                        'textures':len(clip['textures']),'clip_bytes':dest.stat().st_size})
        print('Default',count,animation,round(time.monotonic()-started,1),'s',flush=True)
    atlas.flush();additive.flush()
    sizes={name:(HERE/'textures'/name).stat().st_size for name in sorted(all_textures)}
    report={'version':1,'source':str(path),'clips':reports,'textures':sizes,
            'texture_bytes':sum(sizes.values()),'clip_bytes':sum(r['clip_bytes'] for r in reports),
            'count_policy':'Original Normal has no stars; 2..5 use native MaxCount branches. Six-plus uses five stars and a product compatibility counter.'}
    (folder/'default-reconstructed.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print('Default total:',report['texture_bytes']+report['clip_bytes'],'bytes',flush=True)


if __name__=='__main__':main()
