"""Reconstruct cooked UMG kill scenes into transparent GPU-rendered frames."""
import argparse
import json
import math
import io
import os
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from build_packs import Assets, Scene, HERE, IDENTITY, mul, translate, sample, read
from ue_effects import Effects
from ue_particles import Cascade


def render_label(face,text,size,outline):
    """Four pixels per UI unit; keep layout metrics stable for existing clips."""
    logical=ImageFont.truetype(str(face),size)
    w=math.ceil(logical.getlength(text))+outline*2;h=size+outline*2
    scale=4;ttf=ImageFont.truetype(str(face),size*scale);bounds=ttf.getbbox(text)
    im=Image.new('RGBA',(w*scale,h*scale))
    ImageDraw.Draw(im).text((outline*scale,outline*scale-bounds[1]),text,font=ttf,fill='white',
        stroke_width=2 if outline else 0,stroke_fill=(0,0,0,170))
    return im,w,h


class EffectScene(Scene):
    def __init__(self,path,assets,fx,animation_name='Anim_Normal',kill_type='normal',count=1,root_name=None):
        super().__init__(path,assets,animation_name=animation_name,root_name=root_name)
        self.fx=fx;self.cascade=Cascade(fx);self.kill_type=kill_type
        self.current=None;self.time=0;self.activation={}
        self.count=count;self.nested={};self.labels={};self.dynamic=None;self.native_sprite=None
        cdo=next((o for o in self.data if o['Name'].startswith('Default__')),{}).get('Properties',{})
        configs=cdo.get('StarConfig',[])
        self.star_indices=configs[min(len(configs)-1,count-1)]['Numbers'] if configs else list(range(min(count,5)))
        self.star_delay=cdo.get('DelayPlayStarAnimation',.7);self.star_step=cdo.get('DelayPlayStarAnimationEachOne',.07)
        for name,tracks in self.tracks.items():
            times=[]
            for prop,section in tracks:
                if prop=='bReactivate':
                    curve=section.get('BoolCurve',{})
                    previous=False
                    for t,v in zip(curve.get('Times',[]),curve.get('Values',[])):
                        value=v.get('Value',False) if isinstance(v,dict) else v
                        if value and not previous:times.append(t['Value']/60000)
                        previous=value
            self.activation[name]=times

    def paint_order_key(self,item):
        # Official five-star footage never activates UW_Kill for tiers 1..5.
        # Higher CS/preview counts are Kill Confirm's compatibility extension:
        # retain a readable count on a separate foreground plane. This is not
        # a claim about the game's native controller for six or more kills.
        foreground=self.count>5 and item[1]=='WBP_FeedBackSets_Count'
        return (1 if foreground else 0,item[0])

    def child_scene(self,obj):
        kind=obj['Type']
        if kind not in self.nested:
            filename=kind[:-2]+'.json'
            path=next((self.assets.research/'yugiri-json').rglob(filename),None)
            if path is None:
                path=next((self.assets.research/'preview/parsed').rglob(filename))
            anim='Anim_StarShowUp' if 'Star_' in kind else 'Anim_ComboKill'
            self.nested[kind]=EffectScene(path,self.assets,self.fx,anim,self.kill_type,self.count,'CanvasPanel_0')
        return self.nested[kind]

    def desired(self,obj):
        if obj['Type'].startswith('WBP_'):return self.child_scene(obj).desired(self.child_scene(obj).root)
        if obj['Type']=='Spacer':return tuple(obj.get('Properties',{}).get('Size',{}).get(k,0) for k in 'XY')
        if obj['Type']=='TextBlock':return self.label(obj)[1:]
        if obj['Type']=='WidgetSwitcher':
            children=self.children(obj)
            return self.desired(children[0][0]) if children else (0,0)
        return super().desired(obj)

    def children(self,obj):
        children=super().children(obj)
        if obj['Type']=='WidgetSwitcher':
            digit=self.count//10 if obj['Name'].endswith('Number1') else self.count%10
            return [(o,s) for o,s in children if o['Name'].startswith('T_'+str(digit)) and o['Name'].split('_')[1]==str(digit)]
        return children

    def label(self,obj):
        p=obj.get('Properties',{});name=obj['Name']
        text={'normal':'击败','headshot':'精准击败','knife':'近战击败','grenade':'道具击败'}[self.kill_type] if name=='Text_FeedbackTypeName' else ('击败' if name=='FinishedTxt' else '复仇')
        font=p.get('Font',{});size=round(font.get('Size',14)*96/72);outline=font.get('OutlineSettings',{}).get('OutlineSize',0)
        key=(text,size,outline)
        if key not in self.labels:
            face=self.assets.research/'yugiri-source/PM/Content/PaperMan/UI/Fonts/Calabiyau-Bold.ufont'
            im,w,h=render_label(face,text,size,outline)
            dest=HERE/'textures'/('label-'+str(size)+'-'+str(outline)+'-'+str({'击败':0,'精准击败':1,'近战击败':2,'道具击败':3,'复仇':4}[text])+'.png')
            encoded=io.BytesIO();im.save(encoded,format='PNG');data=encoded.getvalue()
            if not dest.exists() or dest.read_bytes()!=data:
                # Shared label names are reused across parallel theme workers.
                # Publish complete PNG bytes, never truncate a live reader's file.
                temporary=dest.with_name(dest.name+'.'+str(os.getpid())+'.tmp')
                temporary.write_bytes(data);temporary.replace(dest)
            self.labels[key]=(dest,w,h)
        return self.labels[key]

    def parameters(self,obj,path,time):
        result={};material=self.fx.material(path)
        for prop,section in self.tracks.get(obj['Name'],[]):
            if prop!='__material__':continue
            for value in section.get('ColorParameterNamesAndCurves',[]):
                name=value['ParameterName'];base=material['vector'].get(name,(0,0,0,0))
                result[name]=tuple(sample(value.get(key+'Curve'),time,base[i]) for i,key in enumerate(('Red','Green','Blue','Alpha')))
            for value in section.get('ScalarParameterNamesAndCurves',[]):
                name=value['ParameterName'];result[name]=sample(value.get('ParameterCurve'),time,material['scalar'].get(name,0))
        return result

    def paint_extra(self,obj,v,w,h,matrix,alpha,time,output,layer=0):
        self.current=obj;self.time=time;p=obj.get('Properties',{})
        if obj['Type'].startswith('WBP_'):
            child=self.child_scene(obj);local_time=time
            if 'Star_' in obj['Type']:
                siblings=self.children(self.nodes['Panel_Stars']);index=next(i for i,(o,s) in enumerate(siblings) if o['Name']==obj['Name'])
                if index not in self.star_indices:return layer
                local_time-=self.star_delay+self.star_indices.index(index)*self.star_step
            elif self.count<=5:
                # Its CanvasPanel_6 starts at zero opacity; only explicitly
                # activate the combo animation for the over-five extension.
                return layer
            if local_time<0:return layer
            child.dynamic=self.dynamic
            child.native_sprite=self.native_sprite
            child.paint_queue=self.paint_queue
            # UUserWidget is wrapped by SObjectWidget/SCompoundWidget, whose
            # content starts one Slate layer above its caller.
            maximum=child.paint(child.root,{},(w,h),matrix,alpha,local_time,output,True,layer+1)
            self.omitted.update(child.omitted)
            return maximum
        elif obj['Type']=='TextBlock':
            file,tw,th=self.label(obj);color=p.get('ColorAndOpacity',{}).get('SpecifiedColor',{})
            halign=p.get('Justification','ETextJustify::Left')
            x=(w-tw)/2 if halign.endswith('Center') else (w-tw if halign.endswith('Right') else 0)
            tint=tuple(color.get(k,1) for k in 'RGB')+(alpha*color.get('A',1),)
            if self.dynamic and obj['Name']=='Text_FeedbackTypeName':self.dynamic('event-label.png',file,(x,0,tw,th),(0,0,tw,th),matrix,tint)
            else:(self.native_sprite or self.fx.sprite)(file,(x,0,tw,th),(0,0,tw*4,th*4),matrix,tint)
        elif obj['Type']=='SimpleMeshWidget':
            path=p['Brush']['ResourceObject']['ObjectPath']
            yaw=p.get('Rotation',{}).get('Yaw',0)
            for prop,s in self.tracks.get(obj['Name'],[]):
                if prop=='Rotation':yaw=sample(s.get('Curves[1]'),time,yaw)
            a=math.radians(yaw)
            # These UI meshes are planar. Cooked pitch/roll are 0 or 180,
            # i.e. reflections in the original UE XY plane before yaw.
            rotation=p.get('Rotation',{});pitch=rotation.get('Pitch',0);roll=rotation.get('Roll',0)
            assert abs(pitch%180)<.001 and abs(roll%180)<.001,('Unsupported non-planar mesh rotation',rotation)
            flipx,flipy=math.cos(math.radians(pitch)),math.cos(math.radians(roll))
            transform=mul((math.cos(a)*flipx,math.sin(a)*flipx,-math.sin(a)*flipy,math.cos(a)*flipy,w/2,h/2),matrix)
            self.fx.effect(path,self.fx.mesh(p['Mesh']['ObjectPath']),transform,time,(1,1,1,alpha),self.parameters(obj,path,time))
            self.fx.stats['meshes']+=1
        elif obj['Type']=='ParticleSystemWidget':
            if not p.get('ParticleSystemTemplate'):return
            starts=self.activation.get(obj['Name'],[])
            eligible=[t for t in starts if t<=time]
            if starts and not eligible:return
            start=max(eligible) if eligible else 0
            self.cascade.draw(p['ParticleSystemTemplate']['ObjectPath'],time-start,mul(translate(w/2,h/2),matrix),alpha)

    def brush(self,brush,w,h,matrix,alpha,v,output,fraction=1,fill_type=''):
        ref=brush.get('ResourceObject',{}).get('ObjectPath','')
        tint=brush.get('TintColor',{}).get('SpecifiedColor',{})
        color=tuple(v[k]*tint.get(k.upper(),1) for k in 'rgba')
        color=(*color[:3],color[3]*alpha)
        if w<=0 or h<=0 or color[3]<=.0001 or fraction<=0:return
        if brush.get('DrawAs')=='ESlateBrushDrawType::NoDrawType':return
        if self.current['Name']=='Image_FeedbackIcon':
            suffix={'normal':9,'headshot':4,'knife':11,'grenade':12}[self.kill_type]
            file=self.assets.research/'preview/assets'/('T_Dynamic_KillFeedback_React'+str(suffix)+'.png')
            with Image.open(file) as im:sw,sh=im.size
            if self.dynamic:self.dynamic('event-icon.png',file,(0,0,w,h),(0,0,sw,sh),matrix,color)
            else:self.fx.sprite(file,(0,0,w,h),(0,0,sw,sh),matrix,color)
        elif ref in self.fx.objects and self.fx.objects[ref]['Type'] in ('Material','MaterialInstanceConstant'):
            self.fx.effect(ref,self.fx.quad(0,0,w,h),matrix,self.time,color,self.parameters(self.current,ref,self.time))
        elif ref in self.fx.pngs:
            tex=self.fx.texture(ref);(self.native_sprite or self.fx.sprite)(self.fx.pngs[ref],(0,0,w,h),(0,0,*tex.size),matrix,color)
        else:
            commands=[]
            super().brush(brush,w,h,matrix,alpha,v,commands,fraction,fill_type)
            for c in commands:
                # Base sprite command tint is encoded for Win2D. GPU uses linear.
                tint=tuple(x/12.92 if x<=.04045 else ((x+.055)/1.055)**2.4 for x in c[15:18])+(c[18],)
                file=HERE/'textures'/self.textures[int(c[0])]
                if self.dynamic and self.path.stem=='WBP_FeedBackSets_Count' and self.current['Name'].startswith('T_') and self.current['Name'][2:].isdigit():
                    self.dynamic('event-count.png',file,c[1:5],c[5:9],c[9:15],tint)
                else:(self.native_sprite or self.fx.sprite)(file,c[1:5],c[5:9],c[9:15],tint)

    def render(self,time):
        self.fx.clear()
        self.paint_ordered((120,122),translate(self.fx.width/2-60,self.fx.height/2-61),time,[])
        return self.fx.capture()


def main():
    parser=argparse.ArgumentParser();parser.add_argument('research',type=Path);parser.add_argument('--variant',default='10113203');parser.add_argument('--out',type=Path,required=True)
    args=parser.parse_args();args.out.mkdir(exist_ok=True,parents=True)
    assets=Assets(args.research);fx=Effects(args.research)
    path=next((args.research/'structure-parsed').rglob('WBP_IconFeedbackPage_PC_'+args.variant+'.json'))
    scenes=['Anim_Normal','Anim_MaxCount','Anim_MaxCount_3','Anim_MaxCount_4','Anim_MaxCount_5']
    times=[.12,.3,.5,.7,1,1.5,2,2.7]
    montage=Image.new('RGB',(len(times)*384,len(scenes)*384),(22,25,32));draw=ImageDraw.Draw(montage)
    for row,animation in enumerate(scenes):
        scene=EffectScene(path,assets,fx,animation,count=row+1)
        for col,t in enumerate(times):
            frame=scene.render(t);frame.save(args.out/(animation+'-'+str(t)+'.png'))
            frame=frame.resize((384,384),Image.Resampling.LANCZOS);montage.paste(frame,(col*384,row*384),frame)
            draw.text((col*384+8,row*384+8),animation+' '+str(t),fill='white')
        print(animation,'omitted',sorted(scene.omitted),flush=True)
    montage.save(args.out/'contact.png');print(fx.stats)


if __name__=='__main__':main()
