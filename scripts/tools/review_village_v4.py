"""Local-only comparison sheets and validated movement clips. Does not edit game assets."""
from pathlib import Path
import sys,json
from PIL import Image,ImageDraw,ImageFont
import cv2
R=Path(__file__).resolve().parents[2];O=R/'outputs/village-v4'
def sheets(sample,baseline):
 font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',26)
 for resolution in ['1280x720','1600x900','1920x1080']:
  canvas=Image.new('RGB',(1920,4*590),'#eeeae0');draw=ImageDraw.Draw(canvas)
  for row,name in enumerate(['overview','lane','shop','station']):
   for col,(folder,label) in enumerate([(baseline,'原布局 v2'),(sample,'紧凑布局 v4')]):
    im=Image.open(O/folder/resolution/(name+'.png')).convert('RGB');im.thumbnail((960,540))
    draw.text((col*960+14,row*590+8),label+' · '+name,fill='#263d35',font=font);canvas.paste(im,(col*960,row*590+46))
  canvas.save(O/('compare-'+resolution+'.jpg'),quality=92)
 hero=Image.new('RGB',(1600,1850),'#eeeae0');draw=ImageDraw.Draw(hero);draw.text((25,15),'村庄 v4 · 更紧凑的日常路线',font=font,fill='#263d35')
 for i,name in enumerate(['overview','shop','lane']):
  im=Image.open(O/sample/'1920x1080'/(name+'.png')).convert('RGB');im.thumbnail((1580,580));hero.paste(im,((1600-im.width)//2,55+i*595))
 hero.save(O/'village-v4-preview.jpg',quality=93)
def videos(sample):
 records=[]
 for size in ['1280x720','1600x900','1920x1080']:
  folder=O/sample/size/'frames';files=sorted(folder.glob('*.png'));assert len(files)==180
  w,h=map(int,size.split('x'));dest=O/sample/('movement-'+size+'.mp4');writer=cv2.VideoWriter(str(dest),cv2.VideoWriter_fourcc(*'mp4v'),30,(w,h))
  for p in files:
   frame=cv2.imread(str(p));assert frame.shape[:2]==(h,w);writer.write(frame)
  writer.release();cap=cv2.VideoCapture(str(dest));count=0
  while True:
   ok,frame=cap.read()
   if not ok:break
   assert frame.shape[:2]==(h,w);count+=1
  cap.release();assert count==180;records.append({'path':str(dest.relative_to(R)),'decodedFrames':count,'fps':30,'size':[w,h]})
 (O/sample/'videos.json').write_text(json.dumps(records,indent=2));print('V4_VIDEOS_PASS',len(records))
if __name__=='__main__':
 if sys.argv[1]=='sheets':sheets(sys.argv[2],sys.argv[3])
 else:videos(sys.argv[2])
