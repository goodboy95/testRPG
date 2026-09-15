"""Local review sheets and native-resolution videos. Outputs are never game resources."""
import argparse,json
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import cv2
ROOT=Path(__file__).resolve().parents[2]/'outputs/village-art-v3'
FONT='C:/Windows/Fonts/msyh.ttc'
def label(image,text,xy,size=25):ImageDraw.Draw(image).text(xy,text,font=ImageFont.truetype(FONT,size),fill=(236,233,216))
def sheets(sample,baseline):
    for width in (1280,1600,1920):
        for name in ('tree-profile','shop-side','workshop-side'):
            out=Image.new('RGB',(1280,1520),(26,36,34));label(out,'树木与铺面 · 同机位对照',(28,18),30)
            for i,(folder,title) in enumerate([(baseline,'现有 v2'),(sample,'v3 独立美术样板')]):
                label(out,title,(28,70+i*700),23);im=Image.open(folder/str(width)/(name+'.png')).convert('RGB').resize((1280,720),Image.Resampling.LANCZOS)
                # Preserve the full frame and aspect ratio in each comparison row.
                im.thumbnail((1280,640),Image.Resampling.LANCZOS);out.paste(im,((1280-im.width)//2,108+i*700))
            out.save(ROOT/f'compare-{name}-{width}.jpg',quality=94)
    hero=Image.new('RGB',(1280,1200),(26,36,34));label(hero,'村庄 v3 · 树木与铺面样板',(26,14),30)
    for i,name in enumerate(('tree-profile','shop-side','workshop-side')):
        im=Image.open(sample/'1920'/(name+'.png')).convert('RGB');im.thumbnail((640,360),Image.Resampling.LANCZOS)
        if i==0:
            im=Image.open(sample/'1920'/(name+'.png')).convert('RGB').resize((1280,720));hero.paste(im,(0,65))
        else:hero.paste(im,((i-1)*640,810))
    hero.save(ROOT/'village-art-preview.jpg',quality=94)
    for width in (1280,1600,1920):
        shots=list(sorted((sample/str(width)).glob('*.png')));sheet=Image.new('RGB',(1280,((len(shots)+1)//2)*390),(26,36,34))
        for i,p in enumerate(shots):
            im=Image.open(p).convert('RGB').resize((640,360));x=i%2*640;y=i//2*390;label(sheet,p.stem,(x+10,y+4),18);sheet.paste(im,(x,y+28))
        sheet.save(ROOT/f'contact-{width}.jpg',quality=91)
    print('ART_REVIEW_SHEETS_PASS')
def videos(folder):
    records=[]
    for width in (1280,1600,1920):
        for zone in ('plaza','shop','workshop'):
            frames=sorted((folder/str(width)/'frames').glob(zone+'-*.jpg'))
            if not frames:continue
            assert len(frames)==120
            dest=folder/f'{zone}-{width}x{width*9//16}.mp4';writer=cv2.VideoWriter(str(dest),cv2.VideoWriter_fourcc(*'mp4v'),30,(width,width*9//16));assert writer.isOpened()
            for f in frames:
                a=cv2.imread(str(f));assert a.shape[:2]==(width*9//16,width);writer.write(a)
            writer.release();cap=cv2.VideoCapture(str(dest));count=0
            while True:
                ok,a=cap.read()
                if not ok:break
                count+=1
            cap.release();assert count==120
            records.append({'file':str(dest.relative_to(ROOT)),'frames':count,'fps':30,'seconds':4,'bytes':dest.stat().st_size})
    assert len(records) in (3,9)
    (folder/'encoded-videos.json').write_text(json.dumps(records,indent=2));print('ART_VIDEO_PASS',len(records))
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('mode',choices=['sheets','videos']);parser.add_argument('sample');parser.add_argument('baseline',nargs='?');a=parser.parse_args()
    sample=(ROOT/a.sample).resolve();assert sample.is_relative_to(ROOT.resolve())
    if a.mode=='videos':videos(sample)
    else:
        baseline=(ROOT/a.baseline).resolve();assert baseline.is_relative_to(ROOT.resolve());sheets(sample,baseline)
