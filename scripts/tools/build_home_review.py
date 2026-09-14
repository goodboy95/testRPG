"""Deterministic home layout drawing and local capture contact sheets (Pillow)."""
import argparse
import json
from pathlib import Path
import re
from PIL import Image, ImageDraw, ImageFont, ImageOps

root=Path(__file__).resolve().parents[2]
source=(root/'scripts/presentation/HomeLayout3D.cs').read_text('utf-8')
layout=json.loads(re.search(r'Data = """\s*(.*?)\s*""";',source,re.S)[1])
parser=argparse.ArgumentParser();parser.add_argument('mode',choices=['plan','sheets']);args=parser.parse_args()
out=root/'outputs/home-v1';out.mkdir(parents=True,exist_ok=True)
fontpath='C:/Windows/Fonts/msyh.ttc'
font=ImageFont.truetype(fontpath,22);small=ImageFont.truetype(fontpath,16);title=ImageFont.truetype(fontpath,34)

if args.mode=='plan':
    image=Image.new('RGB',(1400,1500),'#eeeae1');d=ImageDraw.Draw(image)
    d.text((65,35),'主角的家  /  HOME V1',font=title,fill='#26382f')
    d.text((65,89),'12 × 9 m 单层住宅  ·  15 × 18 m 地块  ·  北 ↑  ·  单位：米',font=font,fill='#4e5b52')
    scale=62;ox=540;oz=515
    def xy(x,z):return ox+x*scale,oz+z*scale
    def rect(x,z,w,h,color,outline=None):d.rectangle((*xy(x,z),*xy(x+w,z+h)),fill=color,outline=outline,width=2)
    rect(-7.5,-6,15,18,'#d0d0bd','#46594e');rect(-6,-4.5,12,9,'#f7f3e9','#28382d')
    rect(-6,4.5,12,1.5,'#d7dcce')
    for w in layout['walls']:
        cursor=w['from']
        for u,width,lo,hi in sorted(w['openings'])+[[w['to'],0,0,0]]:
            if w['axis']=='x':rect(cursor,w['at']-.1,u-width/2-cursor,.2,'#465346')
            else:rect(w['at']-.1,cursor,.2,u-width/2-cursor,'#465346')
            if width and lo:
                a,b=(xy(u-width/2,w['at']),xy(u+width/2,w['at'])) if w['axis']=='x' else (xy(w['at'],u-width/2),xy(w['at'],u+width/2))
                d.line((a,b),fill='#3b929d',width=5)
            cursor=u+width/2
    for f in layout['furniture']:
        x,_,z=f['position'];w,_,h=f['size'];rect(x-w/2,z-h/2,w,h,'#c0ad90','#746953')
    for door in layout['doors']:
        x,_,z=door['position'];w=door['width']
        if door['axis']=='x':a,b=xy(x-w/2,z),xy(x+w/2,z)
        else:a,b=xy(x,z-w/2),xy(x,z+w/2)
        d.line((a,b),fill='#bc7840',width=4)
    for label,x,z in [('主角卧室',-4.1,3.75),('家人卧室',-4.1,-.55),('起居 / 餐厅',0,1.15),('厨房',3.9,3.65),('卫生间',3.95,-2.55),('门廊',0,5.4),('小院',0,8.5)]:
        px,py=xy(x,z);d.text((px,py),label,font=font,fill='#243b2f',anchor='mm')
    x,_,z=layout['spawn'];px,py=xy(x,z);d.ellipse((px-7,py-7,px+7,py+7),fill='#a44d37')
    d.text((1060,165),'空间约束',font=font,fill='#243b2f')
    for i,text in enumerate(['室内净高 3.2','主入口净宽 1.6','侧房门净宽 1.2','墙厚 0.2','全部地面同高','五处门 / 六组灯','室内门直接接起居厅','院门为封闭景观','红点：卧室出生点','蓝线：真实窗洞','棕线：可操作房门']):
        d.text((1060,215+i*42),text,font=small,fill='#45594b')
    d.text((65,1440),'共享来源：HomeLayout3D.Data  ·  独立漫游场景，不接醒后剧情或正式存档',font=font,fill='#45594b')
    image.save(out/'home-v1-floor-plan.png')
else:
    for folder in sorted((out/'validation').glob('*x*')):
        paths=sorted(folder.glob('*.png'))
        for start in range(0,len(paths),12):
            subset=paths[start:start+12];sheet=Image.new('RGB',(1600,3*335),'#e9e8e2');draw=ImageDraw.Draw(sheet)
            for i,path in enumerate(subset):
                im=Image.open(path);im=ImageOps.contain(im,(390,295));x=(i%4)*400;y=(i//4)*335
                sheet.paste(im,(x+(390-im.width)//2,y));draw.text((x+8,y+298),path.stem,font=small,fill='#283f32')
            sheet.save(out/f'contact-{folder.name}-{start//12+1}.jpg',quality=92)
print('HOME_REVIEW_'+args.mode.upper()+'_PASS')
