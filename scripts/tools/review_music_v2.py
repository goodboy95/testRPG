"""Local music-v2 review, vector floor plan and capture inventory; no raster editing."""
from __future__ import annotations
import argparse
import html
import json
from pathlib import Path
import struct


def run(root):
    out=root/'outputs/music-v2';out.mkdir(parents=True,exist_ok=True)
    manifest=json.loads((root/'assets/generated/dream_escape/3d/music-room-art-pass-v2-manifest.json').read_text('utf-8'))
    v=manifest['layout'];s=44
    def xy(x,z):return 320+x*s,300+z*s
    svg=['<svg xmlns="http://www.w3.org/2000/svg" width="1080" height="620" viewBox="0 0 1080 620">',
         '<rect width="1080" height="620" fill="#f3efe5"/><style>text{font-family:Microsoft YaHei,sans-serif;fill:#343d35;font-size:13px}</style>']
    def rect(x,z,w,d,fill,stroke='#6e7367'):
        px,py=xy(x-w/2,z-d/2);svg.append(f'<rect x="{px}" y="{py}" width="{w*s}" height="{d*s}" fill="{fill}" stroke="{stroke}"/>')
    def label(x,z,text):
        px,py=xy(x,z);svg.append(f'<text x="{px}" y="{py+4}" text-anchor="middle">{text}</text>')
    rect(0,0,v['Width'],v['Depth'],'#e7dfce');rect(0,.15,3,5.1,'#c6d8bf','#a5bc9b')
    rect(4.4,-3.4,3.2,3.2,'#d4dfd9');rect(2.8,-3.4,.18,3.2,'#657569')
    rect(3.225,-1.8,.85,.18,'#657569');rect(5.525,-1.8,.95,.18,'#657569')
    rect(4.35,-1.8,1.4,.07,'#b68c53');rect(5.05,-2.5,.07,1.4,'#af8d69')
    rect(-6,3.55,.08,1.4,'#b68c53')
    for z in (-3.45,-1.45,.55):rect(-6,z,.08,1.45,'#99c6cd')
    names=[('Piano','钢琴'),('Bench','琴凳'),('PartsTray','配件'),('RollDesk','纸带'),('ScoreDesk','拓显'),
           ('ToolsDesk','工具'),('CalibrationDesk','校准'),('PhotoCabinet','灯箱'),('AcousticCabinet','声学柜'),('EquipmentShelf','器材架')]
    for key,text in names:
        x,_,z=v[key+'Position'];w,_,d=v[key+'Size'];rect(x,z,w,d,'#c1a17a');label(x,z,text)
    for i in range(1,7):
        x,_,z=v[f'Chair{i:02}'];w,_,d=v['ChairSize'];rect(x,z,w,d,'#ba9c71');label(x,z,str(i))
    for key,text in [('Entry','入场'),('BeforeJoin','隔门站位'),('SideObservation','观察位')]:
        x,_,z=v[key];px,py=xy(x,z);svg.append(f'<circle cx="{px}" cy="{py}" r="4" fill="#36758b"/>');label(x,z+.3,text)
    label(0,-5.5,'12 m　北 ↑');label(0,.4,'3 m 净通路');label(0,5.6,'南侧钢琴区 · 净高 4 m')
    svg.append('<text x="645" y="88" style="font-size:25px;font-weight:bold">音乐教室 v2 · 俯视布局</text>')
    lines=['12 × 10 × 4 m，东北器材间 3.2 × 3.2 m','主路 ≥ 3 m，主要西侧路线约 1.23 m',
           '六把木面钢架椅；琴凳只是陈设','器材间门洞 1.4 m，解锁后实际可进入','31 个碰撞定义，开／关门扇互斥',
           '18 个交互 ID；三组资料距机关 ≥ 2 m','西窗真实开洞，四墙与天花板完整',
           '蓝点为安全入场和晓雨站位','布局来源：MusicRoomLayout3D.cs','图片不代表新增谜题或存档字段']
    for i,line in enumerate(lines):svg.append(f'<text x="645" y="{134+i*32}">{line}</text>')
    svg.append('</svg>');(out/'floor-plan.svg').write_text('\n'.join(svg),'utf-8')
    captures=[]
    for path in sorted((out/'probe').glob('*/*.png'))+sorted((out/'ui').glob('*/*.png')):
        if path.parent.name=='1280x720-1.2':continue # exploratory capture, not an accepted font preset
        with path.open('rb') as f:
            header=f.read(24)
        if header[:8]!=b'\x89PNG\r\n\x1a\n':raise ValueError(path)
        width,height=struct.unpack('>II',header[16:24])
        expected=int(path.parent.name.split('x')[0].split('-')[0])
        if (width,height)!=(expected,expected*9//16):raise ValueError(f'Wrong capture size: {path}')
        captures.append({'path':path.relative_to(out).as_posix(),'width':width,'height':height})
    (out/'capture-audit.json').write_text(json.dumps(captures,ensure_ascii=False,indent=2)+'\n','utf-8')
    def figures(folder):
        return ''.join(f'<figure><a href="{p.relative_to(out).as_posix()}"><img loading="lazy" src="{p.relative_to(out).as_posix()}" alt="{html.escape(p.stem)}"></a><figcaption>{html.escape(p.stem)}</figcaption></figure>' for p in sorted(folder.glob('*.png')))
    g=manifest['geometry']
    page=f'''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>第二室音乐教室 v2 · 本机评审</title><style>body{{margin:0;background:#f3efe5;color:#303c34;font:16px/1.7 "Microsoft YaHei",sans-serif}}main{{max-width:1250px;margin:auto;padding:35px 25px}}h1{{font-size:34px}}h2{{margin-top:45px}}a{{color:#476451}}img{{max-width:100%;display:block}}.grid{{display:grid;grid-template-columns:1fr 1fr;gap:22px}}figure{{margin:0 0 18px}}figcaption{{font-size:13px;padding:6px 0}}summary{{cursor:pointer;padding:14px;background:#e1e6da}}@media(max-width:800px){{.grid{{grid-template-columns:1fr}}}}</style>
<main><h1>第二室 · 旧学校音乐教室 v2</h1><p>12×10×4 米、可进入器材间、六把排练椅和真实乐器。18 个交互 ID、谜题、剧情与内容版本 6 保持原约定。</p>
<p><a href="validation.md">验收记录</a> · <a href="floor-plan.svg">尺寸布局</a> · <a href="../../art/blender/dream_music/music-room-art-pass-v2.blend">Blender 源</a> · <a href="../../assets/generated/dream_escape/3d/music-room-art-pass-v2.glb">GLB</a></p>
<p>{g['triangleCount']:,} 三角面 · {g['maximumVisibleMeshCount']} 网格 · {g['materialCount']} 材质 · {manifest['runtimeGlbBytes']/1048576:.2f} MiB GLB · {manifest['textureMemoryEstimateMiB']:.0f} MiB 纹理估计</p>
<img src="probe/1920x1080/02-teaching-and-windows.png" alt="当前音乐室西窗和教学区">
<h2>同源布局</h2><img src="floor-plan.svg" alt="音乐室布局图">
<h2>历史 v1 与当前 v2</h2><p>空间与机位已变化，以下比较相同观察主题。历史截图不能用于确认当前布置。</p><div class="grid"><figure><img src="../music-room-3d-review/01-entry-overview.png"><figcaption>历史程序化 v1 · 入口</figcaption></figure><figure><img src="probe/1920x1080/01-entry-overview.png"><figcaption>当前 Blender v2 · 入口</figcaption></figure></div>
<h2>当前 1080p 场景与状态</h2><div class="grid">{figures(out/'probe/1920x1080')}</div>'''
    for size in ('1280x720','1600x900'):page+=f'<details><summary>{size} · 场景与状态</summary><div class="grid">{figures(out/"probe"/size)}</div></details>'
    page+='<h2>三档字号 × 三档分辨率</h2>'
    for width in (1280,1600,1920):
        for scale in ('1','1.25','1.5'):
            page+=f'<details><summary>{width} × {width*9//16} · 字号 {float(scale)*100:g}%</summary><div class="grid">{figures(out/"ui"/f"{width}-{scale}")}</div></details>'
    page+='<h2>验收边界</h2><p>上述为 Godot 4.6.3 Compatibility 本机渲染、合成输入和真实胶囊 sweep 检查。音频使用 Dummy 驱动，未据此验收听感。玩家盲测、人工视觉终验、真实 Xbox/raw delta、GTX 1660 与 Windows 导出仍待进行。</p><p>角色沿用本机测试资源；含角色图只限本机，禁止提交、分发及导出。23 号板面特写隐藏人物；24 号临时关闭门叶检查内侧编号，属于美术检查。常规游戏截图保留人物和真实遮挡。</p></main></html>'
    (out/'review.html').write_text(page,'utf-8')
    print(f'MUSIC_V2_REVIEW_PASS captures={len(captures)}')


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--root',default='.');args=parser.parse_args();run(Path(args.root).resolve())
