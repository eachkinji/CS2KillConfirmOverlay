"""Deterministic evaluator for the exported Cascade sprite modules."""
import hashlib
import math
import random
import numpy as np


def distribution(value,time=0,random_value=.5,dimension=1,default=0):
    table=(value or {}).get('Table',{})
    values=table.get('Values')
    if not values:return np.full(dimension,default,dtype=float)
    count=table.get('EntryCount',1);stride=table.get('EntryStride',dimension)
    if '_samples' not in table:table['_samples']=np.asarray(values,dtype=float).reshape(count,stride)
    samples=table['_samples']
    pos=max(0,min(count-1,(time-table.get('TimeBias',0))*table.get('TimeScale',0)))
    lo=int(pos);hi=min(count-1,lo+1);row=samples[lo]*(1-(pos-lo))+samples[hi]*(pos-lo)
    if table.get('Op',1) in (2,3):
        sub=table.get('SubEntryStride',dimension)
        rv=np.asarray(random_value if isinstance(random_value,(tuple,list,np.ndarray)) else [random_value]*dimension,dtype=float)[:dimension]
        lock=table.get('LockFlag',0)
        if dimension==3:
            if lock==1:rv[1]=rv[0]
            elif lock==2:rv[2]=rv[0]
            elif lock==3:rv[2]=rv[1]
            elif lock==4:rv[:]=rv[0]
        if table.get('Op')==3:rv=(rv>=.5).astype(float)
        row=row[:dimension]*(1-rv)+row[sub:sub+dimension]*rv
    else:row=row[:dimension]
    if len(row)==1 and dimension>1:row=np.repeat(row,dimension)
    return row


class Cascade:
    def __init__(self,fx):
        self.fx=fx
        self.cache={}

    def particles(self,path):
        if path in self.cache:return self.cache[path]
        data=self.fx.packages[path]
        def ref(r):return data[int(r['ObjectPath'].rsplit('.',1)[1])]
        system=next(o for o in data if o['Type']=='ParticleSystem')
        particles=[]
        for ei,eref in enumerate(system['Properties']['Emitters']):
            emitter=ref(eref)['Properties'];lod=ref(emitter['LODLevels'][0])['Properties']
            if lod.get('bEnabled') is False:continue
            required=ref(lod['RequiredModule'])['Properties']
            spawn=ref(lod['SpawnModule'])['Properties']
            modules=[ref(x) for x in lod.get('Modules',[]) if x]
            modules=[o for o in modules if o.get('Properties',{}).get('bEnabled',True)]
            mod={o['Type'].removesuffix('_Seeded'):o.get('Properties',{}) for o in modules}
            if lod.get('TypeDataModule'):
                typedata=ref(lod['TypeDataModule']);mod[typedata['Type']]=typedata.get('Properties',{})
            seed=int.from_bytes(hashlib.sha256((path+str(ei)).encode()).digest()[:8],'little')
            rng=random.Random(seed)
            births=[]
            duration=required.get('EmitterDuration',1)
            loops=required.get('EmitterLoops',0) or 8
            for loop in range(min(loops,8)):
                start=required.get('EmitterDelay',0)+loop*duration
                for burst in spawn.get('BurstList',[]):
                    n=burst['Count']
                    if burst.get('CountLow',-1)>=0:n=rng.randint(burst['CountLow'],n)
                    births.extend([start+burst.get('Time',0)]*n)
                rate=float(distribution(spawn.get('Rate'),loop*duration)[0])*float(distribution(spawn.get('RateScale'),default=1)[0])
                if rate>0:births.extend(start+(i+.5)/rate for i in range(min(1000,int(rate*duration))))
            for pi,birth in enumerate(births):
                rand=[rng.random() for _ in range(24)]
                def dist(typ,key,dim=1,default=0,index=0,t=birth):
                    return distribution(mod.get(typ,{}).get(key),t,rand[index:index+dim],dim,default)
                lifetime=float(dist('ParticleModuleLifetime','Lifetime',default=1)[0])
                pos=dist('ParticleModuleLocation','StartLocation',3,index=1)
                size=dist('ParticleModuleSize','StartSize',3,25,2)
                velocity=dist('ParticleModuleVelocity','StartVelocity',3,index=3)
                for primitive in ['ParticleModuleLocationPrimitiveCylinder','ParticleModuleLocationPrimitiveSphere']:
                    loc=mod.get(primitive)
                    if not loc:continue
                    radius=float(distribution(loc.get('StartRadius'),birth,rand[15])[0])
                    angle=rand[16]*math.tau
                    if primitive.endswith('Cylinder'):
                        radius*=1 if loc.get('SurfaceOnly',False) else math.sqrt(rand[17])
                        height=float(distribution(loc.get('StartHeight'),birth,rand[18])[0])
                        offset=np.array([radius*math.cos(angle),radius*math.sin(angle),(rand[19]-.5)*height])
                        axis=loc.get('HeightAxis','PMLPC_HEIGHTAXIS_Z')
                        if axis.endswith('_X'):offset=offset[[2,0,1]]
                        elif axis.endswith('_Y'):offset=offset[[0,2,1]]
                    else:
                        z=rand[17]*2-1;radius*=1 if loc.get('SurfaceOnly',False) else rand[18]**(1/3)
                        offset=np.array([math.sqrt(1-z*z)*math.cos(angle),math.sqrt(1-z*z)*math.sin(angle),z])*radius
                    for dim,axis in enumerate('XYZ'):
                        if not loc.get('Positive_'+axis,True):offset[dim]=-abs(offset[dim])
                        if not loc.get('Negative_'+axis,True):offset[dim]=abs(offset[dim])
                    pos+=offset+distribution(loc.get('StartLocation'),birth,rand[19:22],3)
                    if loc.get('Velocity',False):velocity+=offset*float(distribution(loc.get('VelocityScale'),birth,rand[20],default=1)[0])
                radial=float(dist('ParticleModuleVelocity','StartVelocityRadial',index=21)[0])
                if np.linalg.norm(pos)>0:velocity+=pos/np.linalg.norm(pos)*radial
                cone=mod.get('ParticleModuleVelocityCone')
                if cone:
                    angle=float(distribution(cone.get('Angle'),birth,rand[4])[0])*math.pi/180
                    speed=float(distribution(cone.get('Velocity'),birth,rand[5])[0])
                    direction=cone.get('Direction',{'X':0,'Y':0,'Z':1});direction=np.array([direction.get(k,0) for k in 'XYZ'],dtype=float);direction/=max(.00001,np.linalg.norm(direction))
                    axis=np.array([1.,0.,0.]) if abs(direction[0])<.9 else np.array([0.,1.,0.])
                    b1=np.cross(direction,axis);b1/=np.linalg.norm(b1);b2=np.cross(direction,b1)
                    velocity+=(direction*math.cos(angle)+(b1*math.cos(rand[6]*math.tau)+b2*math.sin(rand[6]*math.tau))*math.sin(angle))*speed
                acceleration=dist('ParticleModuleAcceleration','Acceleration',3,index=7)
                drag=float(dist('ParticleModuleAccelerationDrag','DragCoefficientRaw',index=8)[0])
                rotation=float(dist('ParticleModuleRotation','StartRotation',index=9)[0])*math.tau
                rotation_rate=float(dist('ParticleModuleRotationRate','StartRotationRate',index=10)[0])*math.tau
                particles.append(dict(emitter=ei,required=required,modules=mod,birth=birth,life=lifetime,position=pos,size=size,velocity=velocity,
                                      acceleration=acceleration,drag=drag,rotation=rotation,rotation_rate=rotation_rate,random=rand))
        self.cache[path]=particles
        return particles

    def draw(self,path,seconds,matrix,opacity):
        from build_packs import mul,translate
        ribbons={}
        for particle in self.particles(path):
            age=seconds-particle['birth'];life=particle['life']
            if age<0 or age>=life:continue
            q=age/life;mod=particle['modules'];required=particle['required'];rv=particle['random']
            def at(typ,key,dim=1,default=0):return distribution(mod.get(typ,{}).get(key),q,rv[12:12+dim],dim,default)
            if 'ParticleModuleColorOverLife' in mod:
                color=at('ParticleModuleColorOverLife','ColorOverLife',3,1);alpha=float(at('ParticleModuleColorOverLife','AlphaOverLife',default=1)[0])*opacity
            else:
                color=distribution(mod.get('ParticleModuleColor',{}).get('StartColor'),particle['birth'],rv[12:15],3,1)
                alpha=float(distribution(mod.get('ParticleModuleColor',{}).get('StartAlpha'),particle['birth'],rv[15],default=1)[0])*opacity
            color*=at('ParticleModuleColorScaleOverLife','ColorScaleOverLife',3,1)
            alpha*=float(at('ParticleModuleColorScaleOverLife','AlphaScaleOverLife',default=1)[0])
            size=particle['size']*at('ParticleModuleSizeMultiplyLife','LifeMultiplier',3,1)
            size*=at('ParticleModuleSizeScale','SizeScale',3,1)
            if required.get('ScreenAlignment','PSA_Square')=='PSA_Square':size[1]=size[0]
            pos=particle['position'].copy();v=particle['velocity'];a=particle['acceleration'];drag=particle['drag']
            if drag>0:
                exp=math.exp(-drag*age);pos+=v*(1-exp)/drag+a*(age/drag-(1-exp)/drag**2)
            else:pos+=v*age+a*age*age*.5
            vel_life=mod.get('ParticleModuleVelocityOverLifetime')
            if vel_life:
                points=[distribution(vel_life.get('VelOverLife'),q*i/8,rv[16:19],3,1) for i in range(9)]
                average=(sum(points)-.5*(points[0]+points[-1]))/8
                pos+=(average if vel_life.get('Absolute',False) else v*average)*age-v*age
            if 'ParticleModuleLocationDirect' in mod:pos=at('ParticleModuleLocationDirect','Location',3)+at('ParticleModuleLocationDirect','LocationOffset',3)
            orbit=mod.get('ParticleModuleOrbit')
            if orbit:
                offset=distribution(orbit.get('OffsetAmount'),q,rv[13],3)
                rot=distribution(orbit.get('RotationAmount'),0,rv[14],3)+distribution(orbit.get('RotationRateAmount'),0,rv[15],3)*age
                angle=rot[2]*math.tau
                pos[:2]+=np.array([offset[0]*math.cos(angle)-offset[1]*math.sin(angle),offset[0]*math.sin(angle)+offset[1]*math.cos(angle)])
            angle=particle['rotation']+particle['rotation_rate']*age
            if 'ParticleModuleRotationRateMultiplyLife' in mod:angle=particle['rotation']+particle['rotation_rate']*age*float(at('ParticleModuleRotationRateMultiplyLife','LifeMultiplier',default=1)[0])
            local=mul((math.cos(angle),math.sin(angle),-math.sin(angle),math.cos(angle),pos[0],pos[1]),matrix)
            columns=required.get('SubImages_Horizontal',1);rows=required.get('SubImages_Vertical',1)
            index=float(at('ParticleModuleSubUV','SubImageIndex')[0]);index=max(0,min(columns*rows-1,index));frame=int(index)
            subuv=(1/columns,1/rows,(frame%columns)/columns,(frame//columns)/rows)
            dynamic=[1.,1.,1.,1.]
            for i,param in enumerate(mod.get('ParticleModuleParameterDynamic',{}).get('DynamicParams',[])[:4]):
                moment=particle['birth'] if param.get('bSpawnTimeOnly') else (seconds if param.get('bUseEmitterTime') else q)
                dynamic[i]=float(distribution(param.get('ParamValue'),moment,rv[20+i],default=1)[0])
            if 'ParticleModuleTypeDataRibbon' in mod:
                ribbons.setdefault(particle['emitter'],[]).append((pos[:2],size[0],(*color,alpha),required['Material']['ObjectPath'],dynamic))
                continue
            self.fx.effect(required['Material']['ObjectPath'],self.fx.quad(-size[0]/2,-size[1]/2,size[0],size[1]),local,seconds,(*color,alpha),subuv=subuv,dynamic=dynamic)
            self.fx.stats['particles']+=1
        for points in ribbons.values():
            if len(points)<2:continue
            vertices=[];strip=[]
            for i,(pos,width,color,material,dynamic) in enumerate(points):
                tangent=points[min(i+1,len(points)-1)][0]-points[max(0,i-1)][0]
                normal=np.array([-tangent[1],tangent[0]])/max(1e-8,np.linalg.norm(tangent))*width/2
                u=i/(len(points)-1)
                strip.append([(*tuple(pos-normal),u,0,*color),(*tuple(pos+normal),u,1,*color)])
            for i in range(len(strip)-1):vertices.extend([strip[i][0],strip[i][1],strip[i+1][1],strip[i][0],strip[i+1][1],strip[i+1][0]])
            self.fx.effect(points[0][3],vertices,matrix,seconds,dynamic=points[0][4]);self.fx.stats['particles']+=len(points)
