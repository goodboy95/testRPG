"""Metric exterior plans and same-camera local comparison sheets."""
import argparse
import json
from pathlib import Path
import re
from PIL import Image,ImageDraw,ImageFont,ImageOps

root=Path(__file__).resolve().parents[2]
layout=json.loads(re.search(r'public const string Data = """\s*(.*?)\s*""";',
    (root/'scripts/presentation/VillageExteriorLayout3D.cs').read_text('utf-8'),re.S)[1])
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',16)
title=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',32)
out=root/'outputs/village-v2';out.mkdir(parents=True,exist_ok=True)
parser=argparse.ArgumentParser();parser.add_argument('mode',choices=['plan','sheets']);args=parser.parse_args()
if args.mode=='plan':
    image=Image.new('RGB',(1800,1000),'#eeeae1');draw=ImageDraw.Draw(image)
    draw.text((35,25),'村庄 v2 / 第一批外景平面 · 单位：米 · 北向上',font=title,fill='#253d35')
    labels={'VillageDesk':'办事桌','VillageChair':'椅','PaperCabinet':'纸笔柜','CollectionBox':'收件箱','Noticeboard':'公告牌','PlazaBench':'长凳',
        'DeliveryTable':'交货台','DryGoods':'米面','EmptyCrates':'空筐','Handcart':'推车','ShopBench':'长凳','ShopFridge':'冰柜',
        'WorkTable':'工作台','ToolBoard':'挂板','RepairCabinet':'柜','PendingStool':'待修凳','ReadyStool':'待领凳','TimberRack':'木料','SortingBins':'分类箱'}
    for idx,(name,rect) in enumerate([('广场',[-23,4,14,13]),('杂货铺',[-12,-1,16,11]),('修理铺',[-31,-18,16,12])]):
        left=30+idx*590;top=115;scale=33
        def xy(x,z):return left+20+(x-rect[0])*scale,top+30+(z-rect[1])*scale
        draw.text((left+10,85),name,font=font,fill='#253d35')
        draw.rectangle((left,top,left+570,top+665),outline='#bdc5ba',width=2)
        if idx:
            bx,bz,bw=(-10,1.5,12) if idx==1 else (-29,-16,12)
            draw.rectangle((*xy(bx,bz-2),*xy(bx+bw,bz)),fill='#7d8e87')
            draw.text(xy(bx+.2,bz-1.2),'建筑前墙',font=small,fill='white')
        for zone in layout['zones']:
            x,z,w,d=zone['rect']
            if not rect[0]<=x<rect[0]+rect[2] or not rect[1]<=z<rect[1]+rect[3]:continue
            draw.rectangle((*xy(x,z),*xy(x+w,z+d)),fill='#d3dfc0',outline='#577750',width=2)
            draw.text(xy(x+.1,z+d/2),f'{w:g}×{d:g} 净空',font=small,fill='#344f31')
        for p in layout['props']:
            x,_,z=p['position'];w,_,d=p['size']
            if p['kind']=='overhead' or not rect[0]<=x<rect[0]+rect[2] or not rect[1]<=z<rect[1]+rect[3]:continue
            draw.rectangle((*xy(x-w/2,z-d/2),*xy(x+w/2,z+d/2)),fill='#ae8868',outline='#665347')
            if p['id'] in labels:draw.text(xy(x-w/2,z+d/2+.03),labels[p['id']],font=small,fill='#433e33')
        if idx==0:
            draw.ellipse((*xy(-13.3,8.7),*xy(-10.7,11.3)),fill='#8eac77');draw.text(xy(-12.8,9.5),'大树',font=small,fill='#29422e')
        draw.text((left+20,800),'棕：物件占地   绿：独立站位 / 工作净空',font=small,fill='#4e5c50')
        draw.line((left+20,850,left+20+5*scale,850),fill='#344b40',width=3)
        draw.text((left+20,865),'0                          5 m',font=small,fill='#344b40')
    draw.text((35,948),'平面来自 Blender / Godot 共用布局；家具占地不计入净空。仅静态外景，不接任务。',font=font,fill='#445c50')
    image.save(out/'village-v2-first-batch-plan.png')
else:
    for name in ['05-shop','06-plaza-tree','09-workshop']:
        old=root/f'outputs/village-v1-regression/validation/1920x1080/{name}.png'
        new=out/f'validation/1920x1080/{name}.png'
        if not old.exists() or not new.exists():continue
        canvas=Image.new('RGB',(1920,650),'#eeeae1');d=ImageDraw.Draw(canvas)
        for i,(path,label) in enumerate([(old,'v1 / 本轮复拍'),(new,'v2 / 第一批外景')]):
            thumb=ImageOps.contain(Image.open(path),(950,535));canvas.paste(thumb,(i*960,65));d.text((i*960+20,15),label,font=font,fill='#2d4339')
        canvas.save(out/f'compare-{name}.jpg',quality=93)
    for res in ['1280x720','1600x900','1920x1080']:
        paths=sorted((out/'validation'/res).glob('*.png'))
        for start in range(0,len(paths),9):
            canvas=Image.new('RGB',(1536,990),'#eeeae1');d=ImageDraw.Draw(canvas)
            for i,path in enumerate(paths[start:start+9]):
                thumb=ImageOps.contain(Image.open(path),(500,285));x=i%3*512;y=i//3*330
                canvas.paste(thumb,(x,y));d.text((x+5,y+290),path.stem,font=small,fill='#2d4339')
            canvas.save(out/f'contact-{res}-{start//9+1}.jpg',quality=92)
print('VILLAGE_V2_REVIEW_PASS '+args.mode)
