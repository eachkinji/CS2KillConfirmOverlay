import json
import sys
import traceback
from pathlib import Path
from PIL import Image,ImageDraw
from build_packs import Assets,read
from ue_effects import Effects
from reconstruct import EffectScene

r=Path(sys.argv[1]);out=Path(sys.argv[2]);out.mkdir(parents=True,exist_ok=True)
assets=Assets(r);fx=Effects(r);report=[]
files=sorted((r/'structure-parsed').rglob('WBP_IconFeedbackPage_PC_*.json'))
sheet=Image.new('RGB',(8*300,((len(files)+7)//8)*300),(22,25,32));draw=ImageDraw.Draw(sheet)
for i,path in enumerate(files):
    variant=path.stem.rsplit('_',1)[1];item={'variant':variant,'group':path.parent.name,'errors':[]}
    try:
        available={o['Name'] for o in read(path) if o['Type']=='MovieScene'}
        for count in (1,3,5):
            name='Anim_Normal' if count==1 else 'Anim_MaxCount_'+str(count)
            if name not in available:name='Anim_MaxCount' if 'Anim_MaxCount' in available else 'Anim_Normal'
            s=EffectScene(path,assets,fx,name,count=count)
            for t in (.2,.6,1.2,2.2):
                im=s.render(min(t,s.duration-.02))
                if count==5 and t==1.2:
                    im.save(out/(variant+'.png'));thumb=im.resize((300,300));sheet.paste(thumb,((i%8)*300,(i//8)*300),thumb)
            item['omitted']=sorted(s.omitted)
    except Exception as error:item['errors'].append(str(error));item['trace']=traceback.format_exc()
    draw.text(((i%8)*300+6,(i//8)*300+6),variant+(' ERROR' if item['errors'] else ''),fill='white')
    report.append(item);print(i+1,variant,item.get('errors'),item.get('omitted'),flush=True)
    if i%10==0:(out/'report.json').write_text(json.dumps(report,indent=2),encoding='utf8')
sheet.save(out/'contact.jpg');(out/'report.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('complete',len(report),'errors',sum(bool(r['errors']) for r in report),'stats',fx.stats)
