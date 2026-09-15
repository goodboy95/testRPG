"""Contact sheets and locally playable video from actual v5 probe captures; never changes game assets."""
from pathlib import Path
import json,sys,re
from PIL import Image,ImageDraw,ImageFont
R=Path(__file__).resolve().parents[2];O=R/'outputs/village-v5'
FONT=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',26)
SMALL=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',19)
IDS=['house02','house03','house13','house15'];LABELS=['米白老房 · 木凳与收纳','浅灰翻修 · 晾晒与储物','暖墙露砖 · 筐具与晾晒','溪东浅墙 · 竹架与雨水桶']
def paths():
    samples=[];baselines=[]
    for p in sorted(O.glob('*/houses.json')):
        d=json.loads(p.read_text());(baselines if d['baseline'] else samples).append((p.parent,d))
    return next(p for p,d in reversed(samples) if len(d['checks'])>100),baselines[-1][0]
def sheets():
    sample,old=paths()
    for res in ['1280x720','1600x900','1920x1080']:
        sheet=Image.new('RGB',(1600,2010),'#ebe8dc');draw=ImageDraw.Draw(sheet)
        draw.text((20,12),'四户住宅样板｜左：现行 v4　右：独立 v5',font=FONT,fill='#293a35')
        for row,key in enumerate(IDS):
            y=65+row*485;draw.text((20,y),key+' · '+LABELS[row],font=SMALL,fill='#293a35')
            for col,folder in enumerate([old,sample]):
                im=Image.open(folder/res/(key+'-side.png'));im=im.resize((800,450),Image.Resampling.LANCZOS);sheet.paste(im,(col*800,y+28))
        sheet.save(O/('compare-'+res+'.jpg'),quality=92)
    sheet=Image.new('RGB',(1600,1030),'#ebe8dc');draw=ImageDraw.Draw(sheet);draw.text((20,12),'四户住宅 v5 · 独立样板，正式村庄未切换',font=FONT,fill='#293a35')
    for i,key in enumerate(IDS):
        x=i%2*800;y=55+i//2*485;draw.text((x+12,y),LABELS[i],font=SMALL,fill='#293a35');im=Image.open(sample/'1600x900'/(key+'-side.png')).resize((800,450),Image.Resampling.LANCZOS);sheet.paste(im,(x,y+27))
    sheet.save(O/'four-houses-preview.jpg',quality=93)
    # Dimensioned local plans include actual fixture footprints and free loading zones.
    source=(R/'scripts/presentation/VillageHouseArtLayout3D.cs').read_text();d=json.loads(re.search(r'Data = """\s*(.*?)\s*""";',source,re.S)[1]);layout=json.loads((R/'assets/generated/village/layout-v4.json').read_text())
    sheet=Image.new('RGB',(1600,1440),'#ebe8dc');draw=ImageDraw.Draw(sheet)
    for i,h in enumerate(d['houses']):
        spec=next(b for b in layout['geometry']['buildings'] if b['id']==h['id']);w,_,depth=spec['size'];sx=i%2*800;sy=i//2*720
        def xy(x,z):return sx+400+x*46,sy+135+z*46
        def rect(x,z,w,d,fill,outline):draw.rectangle([xy(x,z),xy(x+w,z+d)],fill=fill,outline=outline,width=2)
        draw.text((sx+25,sy+12),h['id']+' · '+LABELS[i],font=FONT,fill='#293a35')
        draw.text((sx+25,sy+53),f'门前局部（后部省略）；房屋 {w}×{depth} m；通道 2.8 m',font=SMALL,fill='#293a35')
        rect(-w/2-1.5,-1,w+3,depth/2+7,'#d0ccbe','#686b5c');rect(-w/2,-1,w,depth/2+1,'#8c9385','#454e43')
        rect(-1.4,depth/2,2.8,6,'#e8e3cf','#79a392');rect(*h['loading'],'#cbdac0','#537750')
        draw.text(xy(h['loading'][0]+.1,h['loading'][1]+.7),'3×3 m\n卸货净空',font=SMALL,fill='#293a35')
        for p in d['props']:
            if p['house']!=h['id']:continue
            x,y,z=p['position'];pw,_,pd=p['size'];rect(x-pw/2,z-pd/2,pw,pd,'#a77b50' if y<2 else '#87a5ad','#514a3b')
        draw.text((sx+25,sy+663),'棕：地面摆设　蓝：阳台布　尺寸为建筑局部坐标',font=SMALL,fill='#293a35')
    sheet.save(O/'four-houses-plan.png')
    print('V5_REVIEW_SHEETS_PASS',sample,old)
def videos():
    import cv2
    sample,_=paths();records=[]
    for folder in sorted(sample.glob('*/*-frames')):
        frames=sorted(folder.glob('*.jpg'));im=cv2.imread(str(frames[0]));h,w=im.shape[:2];target=folder.with_suffix('.mp4')
        if not target.exists():
            writer=cv2.VideoWriter(str(target),cv2.CAP_MSMF,cv2.VideoWriter_fourcc(*'avc1'),15,(w,h));assert writer.isOpened()
            for p in frames:writer.write(cv2.imread(str(p)))
            writer.release()
        cap=cv2.VideoCapture(str(target));assert int(cap.get(cv2.CAP_PROP_FRAME_COUNT))==len(frames);ok,frame=cap.read();assert ok;cap.release()
        records.append({'file':str(target.relative_to(R)),'frames':len(frames),'resolution':[w,h],'encodedFps':15,'sampling':'every four physics steps, plus render capture; nominal 15fps review recording, not a performance measurement'})
        sheet=Image.new('RGB',(1280,740),'#ebe8dc');draw=ImageDraw.Draw(sheet)
        for i in range(4):
            k=round(i*(len(frames)-1)/3);im=Image.open(frames[k]).resize((640,360),Image.Resampling.LANCZOS);sheet.paste(im,(i%2*640,i//2*370))
        sheet.save(folder.with_suffix('.jpg'),quality=90)
    (O/'recordings.json').write_text(json.dumps(records,indent=2));print('V5_VIDEOS_PASS',len(records))
if __name__=='__main__':
    if len(sys.argv)>1 and sys.argv[1]=='videos':videos()
    else:sheets()
