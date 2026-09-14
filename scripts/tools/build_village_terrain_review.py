"""Local terrain screenshots and videos. Does not modify game assets or source files."""
from pathlib import Path
import json,sys
import cv2
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'outputs/village-terrain'
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
def checked(s):
 p=Path(s).resolve()
 if not p.is_relative_to(OUT):raise ValueError('Use outputs/village-terrain')
 return p
def sheets(sample,baseline):
 for width in [1280,1600,1920]:
  for zone,label in [('home','家门外道路'),('plaza','广场边缘'),('bank','南桥西侧溪岸')]:
   sheet=Image.new('RGB',(1280,1520),'#eeeae0');d=ImageDraw.Draw(sheet)
   for row,(run,title) in enumerate([(baseline,'原村庄 v2'),(sample,'Terrain3D 样板')]):
    d.text((15,row*760+5),f'{label} · {title} · {width}×{width*9//16}',font=font,fill='#29443d')
    sheet.paste(Image.open(run/str(width)/(zone+'.png')).resize((1280,720)),(0,row*760+40))
   sheet.save(OUT/f'compare-{zone}-{width}.jpg',quality=94)
 hero=Image.new('RGB',(1280,1200),'#eeeae0');d=ImageDraw.Draw(hero)
 for row,(zone,label) in enumerate([('home','家门外道路'),('plaza','广场边缘'),('bank','南桥西侧溪岸')]):
  d.text((15,row*400+4),label+' · 独立地形样板',font=font,fill='#29443d')
  hero.paste(Image.open(sample/'1280'/(zone+'.png')).resize((640,360)),(0,row*400+32))
  hero.paste(Image.open(sample/'1280'/(zone+'-detail.png')).resize((640,360)),(640,row*400+32))
 hero.save(OUT/'terrain-sample-preview.jpg',quality=94)
def videos(run):
 results=[]
 for width in [1280,1600,1920]:
  height=width*9//16
  for zone in ['home','plaza','bank']:
   frames=sorted((run/str(width)/'frames').glob(zone+'-*.jpg'));assert len(frames)==120
   p=run/f'{zone}-{width}x{height}.mp4';w=cv2.VideoWriter(str(p),cv2.VideoWriter_fourcc(*'mp4v'),30,(width,height));assert w.isOpened()
   for f in frames:
    im=cv2.imread(str(f));assert im.shape[:2]==(height,width);w.write(im)
   w.release();reader=cv2.VideoCapture(str(p));count=0
   while True:
    ok,im=reader.read()
    if not ok:break
    assert im.shape[:2]==(height,width);count+=1
   reader.release();assert count==120
   results.append(dict(file=str(p),frames=count,fps=30,seconds=4,bytes=p.stat().st_size))
 (run/'encoded-videos.json').write_text(json.dumps(results,indent=2));print(json.dumps(results))
def plan():
 import sys
 sys.path.insert(0,str(ROOT/'scripts/tools'));import village_geometry as geo
 v=geo.read(ROOT);im=Image.new('RGB',(1100,1100),'#eeeae0');d=ImageDraw.Draw(im)
 def p(x,z):return (int((x+110)*4.6),int((z+118)*4.6))
 for r in v['roads']:d.line([p(*a) for a in r['points']],fill='#aaa69a',width=int(r['width']*4.6))
 for b in v['buildings']:
  x,_,z=b['position'];ww,_,dd=b['size'];d.rectangle([p(x-ww/2,z-dd/2),p(x+ww/2,z+dd/2)],fill='#ada28c')
 for z in range(-112,104):d.line([p(geo.stream(z),z),p(geo.stream(z+1),z+1)],fill='#7aa5a1',width=45)
 for label,rect in [('家门外 · 35×13m',(-74,49,-39,62)),('广场边缘 · 28×21m',(-25,0,3,21)),('西溪岸 · 16×26m',(33,5,49,31))]:
  a,b,c,e=rect;d.rectangle([p(a,b),p(c,e)],outline='#a35a38',width=3);d.text(p(a,e+1),label,font=font,fill='#733f27')
 d.text((25,12),'地形样板范围 · 北向上 · 米 · 原布局保持',font=font,fill='#29443d')
 d.line([(30,1040),(122,1040)],fill='#29443d',width=3);d.text((30,1047),'20m',font=font,fill='#29443d')
 im.save(OUT/'terrain-sample-plan.png')
if __name__=='__main__':
 if sys.argv[1]=='sheets':sheets(checked(sys.argv[2]),checked(sys.argv[3]));plan()
 elif sys.argv[1]=='videos':videos(checked(sys.argv[2]))
