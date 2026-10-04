"""Package actual engine captures as a local comparison/contact sheet and sampled video.
No source imagery is repainted; only labeled layout and video encoding are performed.
"""
import argparse,json
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import cv2
R=Path(__file__).resolve().parents[2]

def main():
    ap=argparse.ArgumentParser();ap.add_argument('current',type=Path);ap.add_argument('baseline',type=Path);args=ap.parse_args()
    out=R/'outputs/village-v8';font_path='C:/Windows/Fonts/msyh.ttc'
    font=ImageFont.truetype(font_path,24);small=ImageFont.truetype(font_path,18)
    names=[('home-front','主角家与院落'),('shop-front','杂货铺与广场'),('workshop-front','修理铺'),('tree-broad','代表树冠')]
    sheet=Image.new('RGB',(1600,4*480+70),'#eeeade');draw=ImageDraw.Draw(sheet)
    draw.text((24,15),'清新动画乡村 v8 · 实际 Godot 画面对照 · 等待视觉确认',font=font,fill='#36443e')
    for row,(name,title) in enumerate(names):
        for col,(folder,label) in enumerate([(args.baseline,'原正式 v6'),(args.current,'独立 v8 样板')]):
            path=folder/('1920-'+name+'.png')
            if not path.exists():path=folder/('1280-'+name+'.png')
            with Image.open(path) as im:sheet.paste(im.resize((800,450),Image.Resampling.LANCZOS),(col*800,row*480+100))
            draw.text((col*800+20,row*480+74),label+' · '+title,font=small,fill='#36443e')
    sheet.save(out/'v8-comparison.jpg',quality=93)
    frames=sorted((args.current/'walk-frames').glob('*.png'))
    report={'comparison':'v8-comparison.jpg','current':str(args.current),'baseline':str(args.baseline)}
    if frames:
        first=cv2.imread(str(frames[0]));h,w=first.shape[:2];target=out/'v8-sample-walk.mp4'
        writer=cv2.VideoWriter(str(target),cv2.VideoWriter_fourcc(*'mp4v'),15,(w,h));assert writer.isOpened()
        for p in frames:
            frame=cv2.imread(str(p));assert frame is not None and frame.shape[:2]==(h,w);writer.write(frame)
        writer.release();cap=cv2.VideoCapture(str(target));count=int(cap.get(cv2.CAP_PROP_FRAME_COUNT));assert count==len(frames);cap.release()
        report.update(video=target.name,frames=count,width=w,height=h,fps=15,note='Sampled walkthrough; PNG capture overhead is not performance evidence')
    (out/'review.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print(json.dumps(report))

if __name__=='__main__':main()
