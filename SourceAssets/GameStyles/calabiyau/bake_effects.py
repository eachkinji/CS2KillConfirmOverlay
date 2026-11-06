"""Bake GPU layers, preserving live kill icon/type/count commands in draw order."""
import argparse
import hashlib
import json
import math
import shutil
import time
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
from build_packs import Assets,Scene,HERE,IDENTITY,translate,write_clip,read
from reconstruct import EffectScene
from ue_effects import Effects


class Atlas:
    def __init__(self,variant):
        self.variant=variant;self.pages=0;self.image=Image.new('RGBA',(2048,2048));self.x=self.y=self.row=0;self.entries={}
    def name(self):return 'fx-'+self.variant+'-'+str(self.pages)+'.png'
    def flush(self):
        if not self.y and not self.row:return
        self.image.crop((0,0,2048,self.y+self.row)).save(HERE/'textures'/self.name(),compress_level=2)
    def add(self,image):
        key=hashlib.sha256(image.tobytes()+str(image.size).encode()).digest()
        if key in self.entries:return self.entries[key]
        w,h=image.size
        if w+4>2048 or h+4>2048:raise ValueError('Oversize effect layer')
        if self.x+w+4>2048:self.x=0;self.y+=self.row;self.row=0
        if self.y+h+4>2048:
            self.flush();self.pages+=1;self.image=Image.new('RGBA',(2048,2048));self.x=self.y=self.row=0
        x,y=self.x+2,self.y+2;self.image.paste(image,(x,y));self.row=max(self.row,h+4);self.x+=w+4
        result=(self.name(),x,y,w,h);self.entries[key]=result;return result


class Bake:
    def __init__(self,scene,atlas,additive_atlas):
        self.scene=scene;self.atlas=atlas;self.additive_atlas=additive_atlas;self.additive=False
        self.textures=[];self.commands=[];self.last_material=scene.fx.stats['materials'];self.files={}
    def command(self,name,dest,source,matrix=IDENTITY,tint=(1,1,1,1),clip_rect=None):
        if clip_rect is not None:
            left,top,right,bottom=clip_rect
            if right<=left or bottom<=top:return
            x,y,w,h=dest
            corners=[(px*matrix[0]+py*matrix[2]+matrix[4],px*matrix[1]+py*matrix[3]+matrix[5]) for px,py in ((x,y),(x+w,y),(x+w,y+h),(x,y+h))]
            if max(p[0] for p in corners)<=left or min(p[0] for p in corners)>=right or max(p[1] for p in corners)<=top or min(p[1] for p in corners)>=bottom:return
        if name not in self.textures:self.textures.append(name)
        color=[12.92*c if c<=.0031308 else 1.055*max(0,c)**(1/2.4)-.055 for c in tint[:3]]
        # Cubic UMG fades can overshoot. Slate clamps alpha at compositing;
        # exported Win2D draw commands must already use its valid [0, 1] range.
        opacity=max(0,min(1,tint[3]))
        command=[self.textures.index(name),*dest,*source,*matrix,*color,opacity]
        # V2 keeps source pixels and live event replacement intact. The renderer
        # clips after fitting the selected event texture, in scene coordinates.
        if clip_rect is not None:command.extend((left,top,right-left,bottom-top))
        self.commands.append([round(float(n),5) for n in command])
    def flush(self):
        if self.last_material==self.scene.fx.stats['materials']:return
        self.last_material=self.scene.fx.stats['materials']
        image,offset=self.scene.fx.capture_region(additive=self.additive);bbox=image.getbbox(alpha_only=True)
        if bbox:
            bbox=(bbox[0]+offset[0],bbox[1]+offset[1],bbox[2]+offset[0],bbox[3]+offset[1])
            # Reuse unchanged 64px regions across time and kill-count clips.
            # One neighbour pixel around each tile preserves linear filtering.
            for top in range(bbox[1]//64*64,bbox[3],64):
                for left in range(bbox[0]//64*64,bbox[2],64):
                    lx,ly=left-offset[0],top-offset[1]
                    if not image.crop((lx,ly,lx+64,ly+64)).getbbox(alpha_only=True):continue
                    tile=image.crop((lx-1,ly-1,lx+65,ly+65))
                    atlas=self.additive_atlas if self.additive else self.atlas
                    name,x,y,w,h=atlas.add(tile);self.command(name,(left,top,64,64),(x+1,y+1,64,64))
        self.scene.fx.clear()
    def sprite(self,file,dest,source,matrix,tint):
        self.flush();file=Path(file)
        if file not in self.files:
            if file.parent==HERE/'textures':name=file.name
            else:
                name=hashlib.sha256(file.read_bytes()).hexdigest()[:24]+'.png';target=HERE/'textures'/name
                if not target.exists():shutil.copy2(file,target)
            self.files[file]=name
        self.command(self.files[file],dest,source,matrix,tint,getattr(self.scene.fx,'clip_rect',None))
    def dynamic(self,name,file,dest,source,matrix,tint):
        self.flush()
        if name=='event-label.png':
            x,y,w,h=dest;dest=(x+w/2-64,y,128,32);source=(0,0,128,32)
        elif name=='event-icon.png':source=(0,0,96,96)
        elif name=='event-count.png':source=(0,0,80,40)
        self.command(name,dest,source,matrix,tint,getattr(self.scene.fx,'clip_rect',None))
    def run(self):
        self.scene.dynamic=self.dynamic;self.scene.native_sprite=self.sprite;frames=[]
        original=self.scene.fx.effect
        def effect(path,*args,**kwargs):
            additive=self.scene.fx.material(path)['blend']=='BLEND_Additive'
            if additive!=self.additive:self.flush();self.additive=additive
            original(path,*args,**kwargs)
        self.scene.fx.effect=effect
        try:
            for i in range(math.ceil(self.scene.duration*60)):
                self.commands=[];self.scene.fx.clear()
                self.scene.paint_ordered((120,122),translate(self.scene.fx.width/2-60,self.scene.fx.height/2-61),i/60,[])
                self.flush();frames.append(self.commands)
        finally:self.scene.fx.effect=original
        version=2 if self.scene.path.stem.removeprefix('WBP_IconFeedbackPage_PC_') in ('10102206','10102207','10102208','10102209','10102210') else 1
        return dict(version=version,fps=60,duration=self.scene.duration,width=self.scene.fx.width,height=self.scene.fx.height,textures=self.textures,frames=frames)


def dynamic_textures(research,assets):
    scale=4
    font=ImageFont.truetype(str(research/'yugiri-source/PM/Content/PaperMan/UI/Fonts/Calabiyau-Bold.ufont'),19*scale)
    for kind,suffix,label in [('normal',9,'击败'),('headshot',4,'精准击败'),('knife',11,'近战击败'),('grenade',12,'道具击败')]:
        file=research/'preview/assets'/('T_Dynamic_KillFeedback_React'+str(suffix)+'.png')
        Image.open(file).convert('RGBA').save(HERE/'textures'/('event-icon-'+kind+'.png'))
        im=Image.new('RGBA',(128*scale,32*scale));bounds=font.getbbox(label);width=font.getlength(label)
        ImageDraw.Draw(im).text(((128*scale-width)/2,2*scale-bounds[1]),label,font=font,fill='white',stroke_width=2,stroke_fill=(0,0,0,170));im.save(HERE/'textures'/('event-label-'+kind+'.png'))
    for kind in ('icon','label'):shutil.copy2(HERE/'textures'/('event-'+kind+'-normal.png'),HERE/'textures'/('event-'+kind+'.png'))
    glyphs={}
    for n in range(10):
        ref='/Game/PaperMan/UI/Atlas/PC/InGame/HUDPC/Frames/SPR_UI_HUDPC_REAC_Kill_T'+str(n)
        crop=assets.crop({'ResourceObject':{'ObjectPath':ref}})
        if crop:glyphs[str(n)]=Image.open(HERE/'textures'/crop[0]).convert('RGBA')
    for count in range(1,100):
        text=str(count);imgs=[glyphs[x] for x in text];height=max(x.height for x in imgs);im=Image.new('RGBA',(sum(x.width for x in imgs),height));x=0
        for img in imgs:im.paste(img,(x,(height-img.height)//2));x+=img.width
        # Keep original glyph pixels instead of stretching them into 80 x 40.
        im.save(HERE/'textures'/('event-count-'+str(count)+'.png'))
    shutil.copy2(HERE/'textures'/'event-count-5.png',HERE/'textures'/'event-count.png')


def main():
    parser=argparse.ArgumentParser();parser.add_argument('research',type=Path);parser.add_argument('--variants',default='*');parser.add_argument('--resume',action='store_true');parser.add_argument('--shard',type=int,default=0);parser.add_argument('--shards',type=int,default=1);parser.add_argument('--skip-dynamic',action='store_true')
    args=parser.parse_args();assets=Assets(args.research);fx=Effects(args.research)
    if not args.skip_dynamic:dynamic_textures(args.research,assets)
    files=[]
    for pattern in args.variants.split(','):files.extend(sorted((args.research/'structure-parsed').rglob('WBP_IconFeedbackPage_PC_'+pattern+'.json')))
    for index,path in enumerate(files):
        if index%args.shards!=args.shard:continue
        start=time.monotonic();variant=path.stem.rsplit('_',1)[1];folder=HERE/'iconpacks'/variant;folder.mkdir(exist_ok=True,parents=True)
        revision=8 if variant in ('10502201','10502202','10502203','10502204','10502205') else 7
        if variant in ('10102206','10102207','10102208','10102209','10102210'):revision=9
        if args.resume and (folder/'reconstructed.json').exists() and read(folder/'reconstructed.json').get('revision')==revision:continue
        atlas=Atlas(variant);additive_atlas=Atlas('add-'+variant);available={o['Name'] for o in read(path) if o['Type']=='MovieScene'};clips=[];before=dict(fx.stats)
        for count in range(1,7):
            anim='Anim_Normal' if count==1 else 'Anim_MaxCount_'+str(min(count,5))
            if anim not in available:anim='Anim_MaxCount' if 'Anim_MaxCount' in available else 'Anim_Normal'
            scene=EffectScene(path,assets,fx,anim,count=count);clip=Bake(scene,atlas,additive_atlas).run()
            omitted=scene.omitted-{'MovieSceneEventTrack'}
            if omitted:raise RuntimeError(variant+' omitted '+str(omitted))
            if count==5:clip['overkill']='lower-6.json.gz'
            write_clip(folder/('lower-'+str(count)+'.json.gz'),clip);clips.append({'count':count,'scene':anim,'duration':clip['duration'],'textures':len(clip['textures'])})
            print(variant,'clip',str(count)+'/6',round(time.monotonic()-start,1),'seconds',flush=True)
            if count==5:
                scene.dynamic=None;scene.native_sprite=None
                preview=scene.render(min(1.8,scene.duration-.1));bounds=preview.getchannel('A').point(lambda a:255 if a>12 else 0).getbbox()
                if bounds:
                    preview=preview.crop((bounds[0]-12,bounds[1]-12,bounds[2]+12,bounds[3]+12));preview.thumbnail((256,256),Image.Resampling.LANCZOS)
                preview.save(folder/'preview.png')
        atlas.flush();additive_atlas.flush()
        stats={k:fx.stats[k]-before[k] for k in before}
        manifest={'revision':revision,'variant':variant,'clips':clips,'draws':stats,'source':str(path),'paint_order':'Slate LayerId with ExplicitCanvasChildZOrder enabled; over-five compatibility counter on its own foreground plane','count_policy':'Native tiers 1..5 do not activate UW_Kill; optional sixth scene preserves a readable count for Kill Confirm overkill events','shader':'Reconstructed M_UI_Basic / M_UI_Diffuse parameter operations; not Unreal shader bytecode.'}
        if revision==8:
            manifest['native_widget_reference']='Flavia PMImage plane calibrated to BV1fTwVeYECN at 492/498/507/513s; Moon behind icon, BG_1 rings in front. CustomAnimationNames selects the authored moon phase for each count.'
        if revision==9:
            manifest['clipping']='Authored CanvasPanel_0 ClipToBounds (190x43 local units; 196x43 for 10102207), transformed with parent geometry; v2 scene-coordinate clip rectangles preserve original sprites and dynamic event icons. Requires Kill Confirm 5.0.1.43.'
        (folder/'reconstructed.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
        print(index+1,len(files),variant,round(time.monotonic()-start,1),'seconds',atlas.pages+1,'atlases',stats,flush=True)
        # Textures uploaded for other themes need not remain resident during baking.
        for texture in fx.images.values():texture.release()
        fx.images.clear()


if __name__=='__main__':main()
