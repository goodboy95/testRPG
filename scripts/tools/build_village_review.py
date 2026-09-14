"""Metric site plan and local screenshot contact sheets. No generated character art."""
import argparse
import math
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont,ImageOps
import village_geometry as g

root=Path(__file__).resolve().parents[2];v=g.read(root)
parser=argparse.ArgumentParser();parser.add_argument('mode',choices=['plan','sheets']);args=parser.parse_args()
out=root/'outputs/village-v1';out.mkdir(parents=True,exist_ok=True)
fontpath='C:/Windows/Fonts/msyh.ttc';font=ImageFont.truetype(fontpath,22);small=ImageFont.truetype(fontpath,16);title=ImageFont.truetype(fontpath,38)
if args.mode=='plan':
    im=Image.new('RGB',(1760,1510),'#eeeae1');d=ImageDraw.Draw(im)
    d.text((55,35),'溪谷村庄  /  VILLAGE V1',font=title,fill='#293f38')
    d.text((55,97),'24 栋建筑 · 18 栋住宅 · 2 座桥 · 生活中心与两条回环',font=font,fill='#586558')
    scale=5.35;ox=620;oz=770
    def xy(x,z):return ox+x*scale,oz+z*scale
    d.rectangle((*xy(-104,-112),*xy(104,104)),fill='#c9d0b5',outline='#6a7964',width=2)
    for z in range(-110,104,10):
        d.line((xy(-104,z),xy(104,z)),fill='#bdc6aa',width=1)
    left=[xy(g.stream(z)-5,z) for z in range(-112,105,2)];right=[xy(g.stream(z)+5,z) for z in range(104,-113,-2)]
    d.polygon(left+right,fill='#799f9f')
    for r in v['roads']:
        d.line([xy(*p) for p in r['points']],fill='#f5eedc',width=round(r['width']*scale),joint='curve')
    for x,z,h,variant in g.trees(v):
        px,py=xy(x,z);rad=h/2*scale;d.ellipse((px-rad,py-rad,px+rad,py+rad),fill='#819567',outline='#6e875f',width=1)
    for b in v['buildings']:
        a,z,w,depth=g.pad(b);d.polygon([xy(*g.world(b,x,zz)) for x,zz in [(a,z),(a+w,z),(a+w,z+depth),(a,z+depth)]],fill='#d9d1b8',outline='#9b9f83')
        w,_,depth=b['size'];pts=[xy(*g.world(b,x,z)) for x,z in [(-w/2,-depth/2),(w/2,-depth/2),(w/2,depth/2),(-w/2,depth/2)]]
        d.polygon(pts,fill='#526867' if b['kind'] in ('house','home') else '#ac7860',outline='#48594f',width=2)
        label={'home':'家','shop':'杂货铺','clinic':'诊所','workshop':'修理铺','community':'活动室','station':'旧站','rail_shed':'附屋'}.get(b['id'],b['id'].removeprefix('house'))
        d.text(xy(b['position'][0],b['position'][2]),label,font=small,fill='#fff4db',anchor='mm')
    for b in v['bridges']:
        x=g.stream(b['z']);d.rectangle((*xy(x-b['halfSpan'],b['z']-b['width']/2),*xy(x+b['halfSpan'],b['z']+b['width']/2)),fill='#a6a38c',outline='#596d66',width=2)
    for r,color in zip(v['routes'],['#a86137','#397f81','#936149']):
        d.line([xy(*p) for p in r['points']],fill=color,width=3,joint='curve')
    px,py=xy(-56,49);d.ellipse((px-6,py-6,px+6,py+6),fill='#c86035')
    d.line((xy(-46,-101),xy(17,-101)),fill='#555e53',width=5)
    for x in range(-46,18,2):d.line((xy(x,-102),xy(x,-100)),fill='#626659',width=2)
    d.text((1230,185),'空间与游玩',font=font,fill='#293f38')
    lines=['北 ↑  /  坐标单位：米','西南：主角家与居住巷','中部：杂货铺与小广场','东侧：溪岸双桥回环','北侧：旧站与缓坡回环','棕线：家 → 杂货铺','青线：溪岸回环','深棕线：旧站回环','主路 4 m，巷道 2.5 m','院地平整，路面连续缓坡','住宅室内从正门转场','其他室内暂不开放','首版没有居民与剧情事件']
    for i,line in enumerate(lines):d.text((1230,239+i*46),line,font=small,fill='#4b5e51')
    daily=next(r for r in v['routes'] if r['id']=='home_shop')
    d.text((1230,905),f'家到杂货铺：{g.route_length(daily):.1f} m',font=font,fill='#293f38')
    d.text((1230,943),f'理想慢跑：{g.route_length(daily)/3.1:.1f} s',font=small,fill='#4b5e51')
    x,y=1230,1060;d.line((x,y,x+20*scale,y),fill='#293f38',width=4)
    for xx in (x,x+10*scale,x+20*scale):d.line((xx,y-7,xx,y+7),fill='#293f38',width=2)
    d.text((x,y+12),'0             10            20 m',font=small,fill='#293f38')
    d.text((55,1420),'共享布局：VillageLayout3D.Data  ·  山体与更远道路作为景观边界  ·  正式剧情与存档不变',font=font,fill='#4b5e51')
    im.save(out/'village-v1-site-plan.png')
else:
    for folder in sorted((out/'validation').glob('*x*')):
        paths=sorted(folder.glob('*.png'))
        for start in range(0,len(paths),9):
            subset=paths[start:start+9];im=Image.new('RGB',(1536,3*330),'#e9e8e2');d=ImageDraw.Draw(im)
            for i,path in enumerate(subset):
                thumb=ImageOps.contain(Image.open(path),(500,290));x=(i%3)*512;y=(i//3)*330
                im.paste(thumb,(x+(500-thumb.width)//2,y));d.text((x+8,y+295),path.stem,font=small,fill='#283f32')
            im.save(out/f'contact-{folder.name}-{start//9+1}.jpg',quality=93)
print('VILLAGE_REVIEW_'+args.mode.upper()+'_PASS')
