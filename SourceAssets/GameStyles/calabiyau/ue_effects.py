"""Offscreen UI-effect reconstruction using source material/mesh/Cascade data.

The cooked material expressions are not editor graphs. M_UI_Basic operations
below are an explicit reconstruction of the exported parameter contract, not
the original shader bytecode. Visual comparison is required before shipping.
"""
import json
import math
import re
import struct
from pathlib import Path
import numpy as np
import moderngl
from PIL import Image


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def rgba(v):
    return tuple(v.get(k, 1) for k in 'RGBA')

def identifier(value):
    return ''.join(c if c.isascii() and (c.isalnum() or c=='_') else '_'+str(ord(c))+'_' for c in value)


VERTEX = '''#version 330
in vec2 pos; in vec2 texcoord; in vec4 vertex_color;
uniform vec2 viewport; uniform mat3 transform;
out vec2 uv; out vec4 vc;
void main(){vec3 p=transform*vec3(pos,1); gl_Position=vec4(p.x/viewport.x*2-1,1-p.y/viewport.y*2,0,1); uv=texcoord;vc=vertex_color;}
'''
COMMON = '''#version 330
in vec2 uv;in vec4 vc;out vec4 frag;
uniform float time;uniform vec4 tint;uniform vec4 subuv;uniform vec4 dynamic_params;
vec3 linearize(vec3 c){return mix(c/12.92,pow((c+.055)/1.055,vec3(2.4)),step(vec3(.04045),c));}
vec2 rotateUV(vec2 p,float a){a*=6.28318530718;return mat2(cos(a),-sin(a),sin(a),cos(a))*(p-.5)+.5;}
vec2 radialUV(vec2 p){p=p*2-1;return vec2(atan(p.y,p.x)/6.28318530718+.5,length(p));}
'''


class Effects:
    def __init__(self, research, width=768, height=768):
        self.research = research
        self.width, self.height = width, height
        self.ctx = moderngl.create_standalone_context()
        self.target = self.ctx.texture((width,height),4,dtype='f4')
        self.fbo = self.ctx.framebuffer([self.target])
        self.output = self.ctx.framebuffer([self.ctx.texture((width,height),4,dtype='f1')])
        self.fbo.use()
        self.ctx.enable(moderngl.BLEND)
        self.ctx.blend_func = (moderngl.ONE,moderngl.ONE_MINUS_SRC_ALPHA,moderngl.ONE,moderngl.ONE_MINUS_SRC_ALPHA)
        self.packages, self.objects, self.images, self.materials, self.programs = {}, {}, {}, {}, {}
        for f in (research/'yugiri-json').rglob('*.json'):
            key='/Game/'+str(f.with_suffix('')).replace('\\','/').split('/PM/Content/',1)[1]
            data=read(f);self.packages[key]=data
            for o in data:
                if o['Type'] in ('Material','MaterialInstanceConstant','Texture2D','ParticleSystem','StaticMesh'):
                    self.objects[key]=o
        self.pngs={'/Game/'+str(f.with_suffix('')).replace('\\','/').split('/PM/Content/',1)[1]:f for f in (research/'yugiri-render-input').rglob('*.png')}
        self.stats={'sprites':0,'materials':0,'meshes':0,'particles':0}
        self.meshes={}
        self.dirty=None;self.uniforms={};self.clip_rect=None
        self.sprite_program=self.ctx.program(vertex_shader=VERTEX,fragment_shader=COMMON+'''
uniform sampler2D image;uniform bool srgb;
void main(){vec4 c=texture(image,uv);if(srgb)c.rgb=linearize(c.rgb); c*=tint*vc; c.a=clamp(c.a,0,1);frag=vec4(c.rgb*c.a,c.a);}
''')
        self.post=self.ctx.program(vertex_shader='''#version 330
in vec2 position;out vec2 uv;void main(){gl_Position=vec4(position,0,1);uv=position*.5+.5;}
''',fragment_shader='''#version 330
uniform sampler2D source;uniform bool additive;in vec2 uv;out vec4 frag;
void main(){vec4 c=texture(source,uv);
// Additive materials emit light, not opaque black coverage. The exported
// premultiplied image needs coverage only where there is emitted energy.
if(additive)c.a=max(max(c.r,c.g),c.b);
c.a=clamp(c.a,0,1);vec3 rgb=c.a>1e-7?max(c.rgb/c.a,vec3(0)):vec3(0);
rgb=mix(rgb*12.92,1.055*pow(rgb,vec3(1./2.4))-.055,step(vec3(.0031308),rgb));frag=vec4(clamp(rgb,0,1),c.a);}
''')
        self.post_buffer=self.ctx.buffer(np.array([-1,-1,1,-1,1,1,-1,-1,1,1,-1,1],dtype='f4').tobytes())
        self.post_vao=self.ctx.vertex_array(self.post,[(self.post_buffer,'2f','position')])

    def texture(self,path):
        key=str(path)
        if key not in self.images:
            f=self.pngs.get(key,Path(path))
            if not f.is_file():raise FileNotFoundError('Unresolved render texture: '+key)
            im=Image.open(f).convert('RGBA')
            tex=self.ctx.texture(im.size,4,im.tobytes());tex.filter=(moderngl.LINEAR,moderngl.LINEAR)
            self.images[key]=tex
        return self.images[key]

    def clear(self):
        self.fbo.clear(0,0,0,0)
        self.dirty=None

    def capture(self):
        return self.capture_region(full=True)[0]

    def capture_region(self,full=False,additive=False):
        if full or self.dirty is None:left,top,right,bottom=0,0,self.width,self.height
        else:
            left=max(0,int(math.floor(self.dirty[0]))-2);top=max(0,int(math.floor(self.dirty[1]))-2)
            right=min(self.width,int(math.ceil(self.dirty[2]))+2);bottom=min(self.height,int(math.ceil(self.dirty[3]))+2)
        if right<=left or bottom<=top:return Image.new('RGBA',(1,1)),(0,0)
        self.ctx.disable(moderngl.BLEND);self.output.use();self.target.use(0);self.post['source'].value=0;self.post['additive'].value=additive;self.post_vao.render()
        image=Image.frombytes('RGBA',(right-left,bottom-top),self.output.read(viewport=(left,self.height-bottom,right-left,bottom-top),components=4)).transpose(Image.Transpose.FLIP_TOP_BOTTOM)
        self.fbo.use();self.ctx.enable(moderngl.BLEND)
        return image,(left,top)

    def uniform(self,program,key,value):
        cache=self.uniforms.setdefault(program.glo,{})
        value=tuple(value) if isinstance(value,(list,tuple)) else value
        if cache.get(key)!=value:program[key].value=value;cache[key]=value

    def draw(self,program,vertices,matrix,tint=(1,1,1,1),time=0,subuv=(1,1,0,0)):
        if tint[3]<=.00001:return
        self.uniform(program,'viewport',(self.width,self.height))
        program['transform'].write(np.array([matrix[0],matrix[1],0,matrix[2],matrix[3],0,matrix[4],matrix[5],1],dtype='f4').tobytes())
        for key,val in [('tint',tint),('time',time),('subuv',subuv)]:
            if key in program:self.uniform(program,key,val)
        data=np.asarray(vertices,dtype='f4')
        px=data[:,0]*matrix[0]+data[:,1]*matrix[2]+matrix[4];py=data[:,0]*matrix[1]+data[:,1]*matrix[3]+matrix[5]
        rect=(float(px.min()),float(py.min()),float(px.max()),float(py.max()))
        self.dirty=rect if self.dirty is None else (min(rect[0],self.dirty[0]),min(rect[1],self.dirty[1]),max(rect[2],self.dirty[2]),max(rect[3],self.dirty[3]))
        buffer=self.ctx.buffer(data.tobytes())
        vao=self.ctx.vertex_array(program,[(buffer,'2f 2f 4f','pos','texcoord','vertex_color')])
        # Raster previews share the authored parent clip with the native command
        # path. The delivered v2 commands retain floating-point clip boundaries.
        if self.clip_rect is not None:
            left,top,right,bottom=self.clip_rect
            left,top=math.ceil(left),math.ceil(top);right,bottom=math.floor(right),math.floor(bottom)
            self.ctx.scissor=(left,self.height-bottom,max(0,right-left),max(0,bottom-top))
        try:vao.render(moderngl.TRIANGLES)
        finally:
            self.ctx.scissor=None
            vao.release();buffer.release()

    @staticmethod
    def quad(x,y,w,h,u0=0,v0=0,u1=1,v1=1):
        return [(px,py,u,v,1,1,1,1) for px,py,u,v in [(x,y,u0,v0),(x+w,y,u1,v0),(x+w,y+h,u1,v1),(x,y,u0,v0),(x+w,y+h,u1,v1),(x,y+h,u0,v1)]]

    def sprite(self,path,dest,source,matrix,tint):
        texture=self.texture(path);texture.use(0)
        self.sprite_program['image'].value=0;self.sprite_program['srgb'].value=True
        x,y,w,h=source;tw,th=texture.size
        self.draw(self.sprite_program,self.quad(*dest,x/tw,y/th,(x+w)/tw,(y+h)/th),matrix,tint)
        self.stats['sprites']+=1

    def material(self,path):
        if path in self.materials:return self.materials[path]
        obj=self.objects[path];p=obj['Properties']
        if obj['Type']=='Material':
            cached=p.get('CachedExpressionData',{}).get('Parameters',{})
            data={'scalar':{},'vector':{},'texture':{},'switch':{},'mask':{},'parent':obj['Name'],'blend':p.get('BlendMode','BLEND_Translucent')}
            for entry,values,kind in [('Entries','ScalarValues','scalar'),('Entries[1]','VectorValues','vector'),('Entries[2]','TextureValues','texture')]:
                for info,value in zip(cached.get(entry,{}).get('ParameterInfos',[]),cached.get(values,[])):
                    data[kind][info['Name']]=rgba(value) if kind=='vector' else (value['ObjectPath'] if kind=='texture' and value else value)
        else:
            parent=self.material(p['Parent']['ObjectPath'])
            data={k:dict(v) if isinstance(v,dict) else v for k,v in parent.items()}
            for field,kind in [('ScalarParameterValues','scalar'),('VectorParameterValues','vector'),('TextureParameterValues','texture')]:
                for v in p.get(field,[]):
                    value=v['ParameterValue'];data[kind][v['ParameterInfo']['Name']]=rgba(value) if kind=='vector' else (value['ObjectPath'] if kind=='texture' and value else value)
            for v in p.get('StaticParameters',{}).get('StaticSwitchParameters',[]):data['switch'][v['ParameterInfo']['Name']]=v['Value']
            for v in p.get('StaticParameters',{}).get('StaticComponentMaskParameters',[]):data['mask'][v['ParameterInfo']['Name']]=[k.lower() for k in 'RGBA' if v.get(k)]
            override=p.get('BasePropertyOverrides',{})
            if override.get('bOverride_BlendMode'):data['blend']=override.get('BlendMode','BLEND_Opaque')
        self.materials[path]=data
        return data

    def material_program(self,path):
        if path in self.programs:return self.programs[path]
        m=self.material(path);sw=m['switch'];sc=m['scalar'];vec=m['vector'];tex=m['texture']
        decl=''.join('uniform float s_'+identifier(k)+';\n' for k in sc)+''.join('uniform vec4 v_'+identifier(k)+';\n' for k in vec)
        decl+=''.join('uniform sampler2D t_'+identifier(k)+';\n' for k,v in tex.items() if v)
        if m['parent']=='M_UI_Diffuse':
            shader=COMMON+decl+'void main(){vec4 c=texture(t_Tex,uv);c.rgb=linearize(c.rgb);c*=tint*vc;c.a=clamp(c.a,0,1);frag=vec4(c.rgb*c.a,c.a);}'
        elif m['parent']=='M_UI_CircleRoleMask':
            shader=COMMON+decl+'void main(){vec4 c=texture(t_RoleIcon,uv);c.rgb=linearize(c.rgb);c.a*=1.-smoothstep(.495,.5,length(uv-.5));c*=tint*vc;frag=vec4(c.rgb*c.a,c.a);}'
        elif m['parent']=='M_UI_Basic':
            functions=[]
            for prefix,key in [('Main','Main_Tex'),('Mask','Mask'),('MaskB','MaskB'),('Noise','Noise'),('Dissolve','Dissolve')]:
                if not tex.get(key):continue
                code='vec2 p=base;'
                if sw.get('If_Use_'+prefix+'_Radial'):code+='p=radialUV(p);'
                if sw.get('If_Use_'+prefix+'_UV'):
                    code+=f'p=p*v_{prefix}_UV_01.xy+v_{prefix}_UV_01.zw+v_{prefix}_UV_02.xy*time;'
                if sw.get('If_Use_'+prefix+'UMove_Dynamic'):code+='p.x+=dynamic_params.y;'
                if sw.get('If_Use_'+prefix+'VMove_Dynamic') or sw.get('If_Use_'+prefix+'_VMove_Dynamic'):code+='p.y+=dynamic_params.z;'
                if sw.get('If_Use_'+prefix+'_Rotator'):
                    code+=f'p=rotateUV(p,v_{prefix}_UV_02.z+v_{prefix}_UV_02.w*time);'
                if prefix=='Main':
                    if sw.get('IF_FlipBook'):
                        code+='float cells=v_FlipBook.x*v_FlipBook.y;float f=floor(fract(time*v_FlipBook.w+v_FlipBook.z)*cells);p=(p+vec2(mod(f,v_FlipBook.x),floor(f/v_FlipBook.x)))/v_FlipBook.xy;'
                    code+='p=p*subuv.xy+subuv.zw;'
                for axis in ['U','V']:
                    if sw.get(prefix+'_'+axis+'_Clamp'):
                        a='x' if axis=='U' else 'y';code+=f'if(p.{a}<0||p.{a}>1)return vec4(0);'
                code+=f'return texture(t_{key},p);'
                functions.append(f'vec4 read{prefix}(vec2 base){{{code}}}')
            def channel(prefix,expression):
                channels=m['mask'].get(prefix+'_Channel',['r'])
                return expression+'.'+(channels[0] if channels else 'r')
            main='vec2 base=uv;'
            if sw.get('If_Noise') and tex.get('Noise'):
                main+='vec2 noise=readNoise(base).rg-.5;'
                main+='base+=noise*s_Noise_Int;'
            main+='vec4 mainTex=readMain(base);'
            mc=channel('Main','mainTex')
            main+='vec3 color=mainTex.rgb;'
            if sw.get('If_Desaturation'):
                main+='color=mix(color,vec3(dot(color,vec3(.299,.587,.114))),clamp(s_Delete_Color,0,1));'
            # Main_Channel selects the opacity/color intensity of the FX bitmap.
            main+=f'float alpha={mc};'
            if sw.get('If_Use_Refine'):main+='color=pow(max(color*v_Main_Refine.x,vec3(0)),vec3(max(.001,v_Main_Refine.y)))*v_Main_Refine.z;'
            main+='color*=v_Main_Color.rgb;'
            if sw.get('If_Use_MainColor_Alpha'):main+='alpha*=v_Main_Color.a;'
            if sw.get('If_Mask'):
                main+='float mask='+channel('Mask','readMask(base)')+';'
                if sw.get('If_Mask_Power'):main+='mask=pow(max(mask,0),s_Mask_Power);'
                main+='alpha*=mask*s_Mask_Muitiply;'
            if sw.get('IF_AlphaMaskB'):
                main+='float maskB='+channel('MaskB','readMaskB(base)')+';'
                if sw.get('If_MaskB_Power'):main+='maskB=pow(max(maskB,0),s_MaskB_Power);'
                main+='alpha*=maskB*s_MaskB_Muitiply;'
            if sw.get('If_Dissolve'):
                threshold='dynamic_params.x' if sw.get('If_Use_Dissolve_Dynamic') else 's_Dissolve_Muitiply'
                main+='float dissolve='+channel('Dissolve','readDissolve(base)')+';alpha*=clamp((dissolve-'+threshold+')/max(.001,s_Dissolve_Smooth),0,1);'
            main+='color*=tint.rgb*vc.rgb;alpha=clamp(alpha*tint.a*vc.a,0,1);frag=vec4(color*alpha,alpha);'
            shader=COMMON+decl+'\n'.join(functions)+'\nvoid main(){'+main+'}'
        else:raise ValueError('Unsupported master material '+m['parent'])
        program=self.ctx.program(vertex_shader=VERTEX,fragment_shader=shader)
        self.programs[path]=program
        return program

    def effect(self,path,vertices,matrix,time,tint=(1,1,1,1),overrides=None,subuv=(1,1,0,0),dynamic=(1,1,1,1)):
        m=self.material(path);program=self.material_program(path)
        overrides=overrides or {}
        if 'dynamic_params' in program:self.uniform(program,'dynamic_params',dynamic)
        for prefix,kind in [('s_','scalar'),('v_','vector')]:
            for key,value in m[kind].items():
                if prefix+identifier(key) in program:self.uniform(program,prefix+identifier(key),overrides.get(key,value))
        slot=0
        for key,value in m['texture'].items():
            if value and 't_'+identifier(key) in program:
                self.texture(value).use(slot);program['t_'+identifier(key)].value=slot;slot+=1
        if m['blend']=='BLEND_Additive':self.ctx.blend_func=(moderngl.ONE,moderngl.ONE,moderngl.ONE,moderngl.ONE_MINUS_SRC_ALPHA)
        self.draw(program,vertices,matrix,tint,time,subuv)
        self.ctx.blend_func=(moderngl.ONE,moderngl.ONE_MINUS_SRC_ALPHA,moderngl.ONE,moderngl.ONE_MINUS_SRC_ALPHA)
        self.stats['materials']+=1

    def mesh(self,path):
        if path in self.meshes:return self.meshes[path]
        file=next((self.research/'yugiri-render-input').rglob(path.rsplit('/',1)[1]+'.glb'))
        b=file.read_bytes();length=struct.unpack_from('<I',b,12)[0];doc=json.loads(b[20:20+length]);binary=b[28+length:]
        def accessor(index):
            a=doc['accessors'][index];view=doc['bufferViews'][a['bufferView']]
            n={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']];typ={5126:'f4',5123:'u2',5121:'u1'}[a['componentType']]
            item=np.dtype(typ).itemsize
            array=np.ndarray((a['count'],n),dtype=typ,buffer=binary,offset=view.get('byteOffset',0)+a.get('byteOffset',0),strides=(view.get('byteStride',n*item),item)).copy()
            return array/255 if a.get('normalized') else array
        primitive=doc['meshes'][0]['primitives'][0];attr=primitive['attributes']
        pos=accessor(attr['POSITION']);uv=accessor(attr['TEXCOORD_0']);color=accessor(attr['COLOR_0'])
        # CUE4Parse glTF is metres, with original UE XY stored in glTF X,-Z.
        vertex=np.concatenate([pos[:,[0,2]]*np.array([100,-100]),uv,color],axis=1)
        result=vertex[accessor(primitive['indices']).ravel().astype(int)]
        self.meshes[path]=result
        return result
