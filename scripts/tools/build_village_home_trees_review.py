"""Review contact sheets and video encoding from actual engine captures. No game-asset mutations."""
from pathlib import Path
import json,re,sys
from PIL import Image,ImageDraw,ImageFont
R=Path(__file__).resolve().parents[2];O=R/'outputs/village-v6'
FONT=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',26);SMALL=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',19)
def runs():
    records=[(p.parent,json.loads(p.read_text())) for p in sorted(O.glob('*/home-trees.json'))]
    return [p for p,d in records if not d['baseline']][-1],[p for p,d in records if d['baseline']][-1]
def sheets():
    new,old=runs()
    for res in ['1280x720','1600x900','1920x1080']:
        sheet=Image.new('RGB',(1600,1530),'#e9e5d9');draw=ImageDraw.Draw(sheet);draw.text((20,12),'主角家与全村树木｜左：v5　右：独立 v6',font=FONT,fill='#293a35')
        for row,name in enumerate(['gate','porch','Tree_000-shape']):
            y=65+row*485;draw.text((20,y),{'gate':'村路上的院门辨识','porch':'门廊与日常生活','Tree_000-shape':'普通阔叶树：枝叶替换'}[name],font=SMALL,fill='#293a35')
            for col,folder in enumerate([old,new]):sheet.paste(Image.open(folder/res/(name+'.png')).resize((800,450),Image.Resampling.LANCZOS),(col*800,y+27))
        sheet.save(O/('compare-'+res+'.jpg'),quality=92)
    sheet=Image.new('RGB',(1600,1030),'#e9e5d9');draw=ImageDraw.Draw(sheet);draw.text((20,12),'主角家与全村树木 v6 · 独立样板，正式村庄未切换',font=FONT,fill='#293a35')
    for i,(name,label) in enumerate([('gate','赭红院门与暖色旧家'),('porch','旧长凳、自行车与门廊'),('Tree_000-shape','宽展阔叶树'),('Tree_002-shape','疏冠阔叶树')]):
        x=i%2*800;y=55+i//2*485;draw.text((x+12,y),label,font=SMALL,fill='#293a35');sheet.paste(Image.open(new/'1600x900'/(name+'.png')).resize((800,450),Image.Resampling.LANCZOS),(x,y+27))
    sheet.save(O/'home-trees-preview.jpg',quality=93)
    # Dimensioned home-local plan: all additions stay outside the full-width central route.
    sheet=Image.new('RGB',(1100,1100),'#e9e5d9');draw=ImageDraw.Draw(sheet)
    def xy(x,z):return 550+x*48,340+z*48
    def rect(x,z,w,d,fill):draw.rectangle([xy(x,z),xy(x+w,z+d)],fill=fill,outline='#625c4d',width=2)
    draw.text((35,20),'主角家局部平面 · 建筑 12×9 m / 院地 15×18 m',font=FONT,fill='#293a35')
    rect(-7.5,-6,15,18,'#c9c6b8');rect(-6,-4.5,12,9,'#989e8e');rect(-6,4.5,12,1.5,'#c3b49c');rect(-1.4,4.5,2.8,8,'#dce8ce')
    draw.text(xy(-2.5,0),'原住宅主体 12×9 m\n室内保持',font=FONT,fill='#293a35')
    draw.text(xy(-1.2,7),'2.8 m\n净通路',font=SMALL,fill='#365444')
    data=json.loads(re.search(r'Data = """\s*(.*?)\s*""";', (R/'scripts/presentation/HomeIdentityLayout3D.cs').read_text(),re.S)[1])
    for p in data['props']:
        x,y,z=p['position'];w,h,d=p['size'];rect(x-w/2,z-d/2,w,d,'#a85d39');draw.text(xy(x-w/2 if p['id']=='Bicycle' else -7.1,z+.65),('自行车' if p['id']=='Bicycle' else '收纳')+f'\n{w}×{d} m',font=SMALL,fill='#293a35')
    rect(-4.8,5.02,1.6,.5,'#927452');draw.text(xy(-3.9,6.5),'原长凳',font=SMALL,fill='#293a35')
    draw.text((35,1025),'门洞、院地高度和接坡保持；新增摆设仅静态外观与简单碰撞',font=SMALL,fill='#293a35');sheet.save(O/'home-plan.png')
    print('V6_REVIEW_PASS',new,old)
def videos():
    import cv2
    new,_=runs();records=[]
    for folder in sorted(new.glob('*/*-frames')):
        frames=sorted(folder.glob('*.jpg'));im=cv2.imread(str(frames[0]));h,w=im.shape[:2];target=folder.with_suffix('.mp4')
        if not target.exists():
            writer=cv2.VideoWriter(str(target),cv2.CAP_MSMF,cv2.VideoWriter_fourcc(*'avc1'),15,(w,h));assert writer.isOpened()
            for p in frames:writer.write(cv2.imread(str(p)))
            writer.release()
        cap=cv2.VideoCapture(str(target));assert int(cap.get(cv2.CAP_PROP_FRAME_COUNT))==len(frames);ok,_=cap.read();assert ok;cap.release()
        records.append({'file':str(target.relative_to(R)),'frames':len(frames),'resolution':[w,h],'encodedFps':15,'sampling':'every 4 physics frames plus rendered capture; review video, not a timing benchmark'})
        image=Image.new('RGB',(1280,740),'#e9e5d9')
        for i in range(4):image.paste(Image.open(frames[round(i*(len(frames)-1)/3)]).resize((640,360)),(i%2*640,i//2*370))
        image.save(folder.with_suffix('.jpg'),quality=90)
    (O/'recordings.json').write_text(json.dumps(records,indent=2));print('V6_VIDEOS_PASS',len(records))
if __name__=='__main__':
    if len(sys.argv)>1 and sys.argv[1]=='videos':videos()
    else:sheets()
