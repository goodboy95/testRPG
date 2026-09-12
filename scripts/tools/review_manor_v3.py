"""Create the local vector plan and runtime screenshot review (no raster editing)."""
from __future__ import annotations
import argparse
import html
import json
from pathlib import Path
import re


def read_layout(root):
    source=(root/'scripts/presentation/MansionLayout3D.cs').read_text('utf-8')
    result={key:float(value) for key,value in re.findall(r'const float (\w+) = (-?[\d.]+)f;',source)}
    result.update({key:tuple(float(n.strip().removesuffix('f')) for n in value.split(','))
        for key,value in re.findall(r'public static readonly Vector3 (\w+) = new\(([^)]+)\);',source)})
    return result


def floor_plan(root,output):
    v=read_layout(root);scale=29
    def p(x,z):return 230+x*scale,48+(12-z)*scale
    shapes=['<svg xmlns="http://www.w3.org/2000/svg" width="1040" height="740" viewBox="0 0 1040 740">',
        '<rect width="1040" height="740" fill="#f6f2e9"/>',
        '<style>text{font-family:"Microsoft YaHei",sans-serif;fill:#34312d}.small{font-size:12px}.label{font-size:14px}.heading{font-size:23px;font-weight:600}</style>']
    fw,fd,hw,hd=v['FoyerWidth'],v['FoyerDepth'],v['HallWidth'],v['HallDepth']
    outline=[(-hw/2,hd),(hw/2,hd),(hw/2,0),(fw/2,0),(fw/2,-fd),(-fw/2,-fd),(-fw/2,0),(-hw/2,0)]
    shapes.append('<polygon points="'+' '.join(f'{p(x,z)[0]},{p(x,z)[1]}' for x,z in outline)+'" fill="#e9e3d7" stroke="#4d463c" stroke-width="4"/>')
    def rect(x,z,w,d,fill,stroke=None):
        px,py=p(x-w/2,z+d/2)
        shapes.append(f'<rect x="{px}" y="{py}" width="{w*scale}" height="{d*scale}" fill="{fill}"'+(f' stroke="{stroke}"' if stroke else '')+'/>')
    rect(0,2,3,18.6,'#d9e3d1')
    rect(0,-4.5,2.5,4.6,'#8a6253');rect(0,6,1.75,10.3,'#8a6253')
    for prefix,label in [('Fireplace','壁炉'),('ChairSouth','扶手椅'),('ChairNorth','扶手椅'),('SideTable','边桌'),('Console','玄关柜'),('Bench','长凳'),('Stair','封闭楼梯')]:
        x,_,z=v[prefix+'Position'];w,_,d=v['ChairSize' if prefix.startswith('Chair') else prefix+'Size']
        rect(x,z,w,d,'#c4af8d','#665843')
        px,py=p(x,z)
        if prefix=='Stair':
            for i in range(13):
                _,sy=p(x,z-d/2+.8+i*.25)
                shapes.append(f'<path d="M {px-w*scale/2} {sy} h {w*scale}" stroke="#8d785b" stroke-width="1"/>')
            rect(x,z-d/2+.03,w,.10,'#8a4d3e')
        shapes.append(f'<text x="{px}" y="{py+4}" text-anchor="middle" class="small">{label}</text>')
    for key,label,width in [('FrontDoor','大门·锁闭',v['FrontDoorWidth']),('RoomOneDoor','第一室',v['SingleDoorWidth']),('RoomTwoDoor','第二室',v['SingleDoorWidth']),('RoomThreeDoor','第三室',v['SingleDoorWidth']),('RoomFourDoor','第四室',v['DoubleDoorWidth'])]:
        x,_,z=v[key];px,py=p(x,z)
        if key in ('FrontDoor','RoomFourDoor'):
            rect(x,z,width,.16,'#78563a')
            shapes.append(f'<text x="{px}" y="{py+(-12 if key=="RoomFourDoor" else 24)}" text-anchor="middle" class="label">{label}</text>')
        else:
            rect(x,z,.16,width,'#78563a')
            tx=px-15 if x<0 else px+15
            anchor='end' if x<0 else 'start'
            shapes.append(f'<text x="{tx}" y="{py+4}" text-anchor="{anchor}" class="label">{label}</text>')
    for key in ('RoomOneEntry','RoomTwoEntry','RoomThreeEntry','RoomFourEntry'):
        x,_,z=v[key];px,py=p(x,z)
        shapes.append(f'<circle cx="{px}" cy="{py}" r="4" fill="#446b80"/>')
    for width,z in [(hw,hd+.85),(fw,-fd-1.4)]:
        x,y=p(-width/2,z)
        shapes.append(f'<path d="M {x} {y-4} v 8 m 0 -4 h {width*scale} m 0 -4 v 8" fill="none" stroke="#665843"/>')
        shapes.append(f'<text x="{x+width*scale/2}" y="{y-7}" text-anchor="middle" class="small">{width:g} m</text>')
    for depth,z in [(hd,hd),(fd,0)]:
        _,y=p(0,z)
        shapes.append(f'<path d="M 431 {y} h 8 m -4 0 v {depth*scale} m -4 0 h 8" fill="none" stroke="#665843"/>')
        shapes.append(f'<text x="444" y="{y+depth*scale/2}" class="small">{depth:g} m</text>')
    shapes += ['<text x="500" y="75" class="heading">洋房大厅 v3 · 带尺寸俯视图</text>',
        '<text x="500" y="110" class="label">北 ↑　世界坐标：X 横向，Z 向北，Y 向上</text>']
    lines=['前厅：10 × 8 m，净高 6.6 m','内廊：7 × 12 m，净高 4.2 m',
        '绿色区：3 m 连续中央通路','蓝点：四室返厅安全出生点','楼梯占地：2.4 × 4.6 m，平台高 4.2 m',
        '楼梯整体禁入；南侧固定木格屏无交互',
        '四室依次解锁；大门永久关闭','扩展墙段为普通实墙，无门型暗示',
        '家具色块表示对应模型／碰撞占地','地毯与画框不改变通行和剧情状态']
    for i,line in enumerate(lines):shapes.append(f'<text x="500" y="{155+i*31}" class="label">{html.escape(line)}</text>')
    shapes.append('<text x="500" y="540" class="small">本图由 MansionLayout3D 同源数据生成。</text>')
    shapes.append('<text x="500" y="563" class="small">实际胶囊通行、旧档恢复与镜头检查另见验收报告。</text>')
    shapes.append('<text x="500" y="606" class="small">旧外观 v1 为历史资产，不代表此内部布局。</text>')
    shapes.append('</svg>')
    (output/'floor-plan.svg').write_text('\n'.join(shapes),'utf-8')


def review(root,output):
    manifest=json.loads((root/'assets/generated/dream_escape/3d/dream-manor-hall-v3-manifest.json').read_text('utf-8'))
    g=manifest['geometry']
    shots=[('01-foyer-front-door.png','前厅大门'),('02-foyer-reverse.png','前厅与内廊'),
           ('03-hall-looking-north.png','内廊轴线'),('04-hall-looking-south.png','从内廊返回前厅'),
           ('05-stair-enclosure.png','封闭楼梯'),('06-fireplace.png','壁炉结构'),('07-seating.png','座位区'),
           ('08-console.png','玄关柜'),('09-room-one.png','第一室门'),('10-room-two.png','第二室门'),
           ('11-room-three.png','第三室门'),('12-room-four.png','第四室双开门'),('13-bench.png','内廊长凳'),
           ('14-room-one-open.png','单扇门开启'),('15-room-four-open.png','双扇门开启'),
           ('16-collision-foyer.png','前厅碰撞'),('17-collision-hall.png','内廊碰撞')]
    figures=''.join(f'<figure><a href="probe/1920x1080/{file}"><img loading="lazy" src="probe/1920x1080/{file}" alt="{label}"></a><figcaption>{label}</figcaption></figure>' for file,label in shots)
    other_sizes=''.join('<details><summary>'+size+' · 17 张实机图</summary><div class="grid">'+
        ''.join(f'<figure><a href="probe/{size}/{file}"><img loading="lazy" src="probe/{size}/{file}" alt="{label}"></a><figcaption>{label}</figcaption></figure>' for file,label in shots)+
        '</div></details>' for size in ('1280x720','1600x900'))
    content=f'''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>洋房大厅 v3 · 实机评审</title><style>
body{{margin:0;background:#eee9df;color:#302d29;font:16px/1.65 "Microsoft YaHei",sans-serif}}main{{max-width:1240px;margin:auto;padding:42px 28px}}h1{{font-size:32px;margin:0}}h2{{font-size:23px;margin-top:48px}}p{{max-width:980px}}a{{color:#665039}}img{{display:block;max-width:100%;height:auto}}figure{{margin:0 0 25px}}figcaption{{padding:9px 0;color:#5f554a}}.pair,.grid{{display:grid;grid-template-columns:1fr 1fr;gap:22px}}.meta{{border-top:1px solid #c8bdab;padding-top:18px}}@media(max-width:800px){{.pair,.grid{{grid-template-columns:1fr}}}}
</style><main><h1>洋房大厅 v3</h1><p>宽前厅、短内廊与封闭楼梯。以下为 Godot 4.6.3 Compatibility 实机截图。</p>
<p><a href="validation.md">实际验收记录</a> · <a href="floor-plan.svg">带尺寸俯视图</a> · <a href="../../art/blender/dream_manor/dream-manor-hall-v3.blend">Blender 源</a> · <a href="../../assets/generated/dream_escape/3d/dream-manor-hall-v3.glb">GLB</a></p>
<p class="meta">{g['triangleCount']:,} 三角面 · {g['maximumVisibleMeshCount']} 可见网格 · {g['materialCount']} 材质 · {manifest['runtimeGlbBytes']/1048576:.2f} MiB GLB · {manifest['textureMemoryEstimateMiB']:.1f} MiB 纹理估计</p>
<figure><img src="probe/1920x1080/02-foyer-reverse.png" alt="重建后的前厅与内廊"></figure>
<h2>布局</h2><img src="floor-plan.svg" alt="大厅 v3 尺寸与家具占地">
<h2>历史 v2 与重建 v3</h2><p>布局已改变，下列为相同观察主题的对照，非完全相同机位。历史人物为 billboard；v3 的本机角色不属于环境资产。</p>
<div class="pair"><figure><img src="../dream-manor-v2-full-runtime/01-foyer-front-door.png" alt="历史 v2 前厅"><figcaption>历史 v2 · 前厅</figcaption></figure><figure><img src="probe/1920x1080/01-foyer-front-door.png" alt="v3 前厅"><figcaption>v3 · 前厅</figcaption></figure>
<figure><img src="../dream-manor-v2-full-runtime/02-hall-looking-north.png" alt="历史 v2 内廊"><figcaption>历史 v2 · 内廊</figcaption></figure><figure><img src="probe/1920x1080/03-hall-looking-north.png" alt="v3 内廊"><figcaption>v3 · 内廊</figcaption></figure></div>
<h2>完整 1080p 实机检查</h2><div class="grid">{figures}</div>
<h2>其余分辨率与同行检查</h2>{other_sizes}<figure><img loading="lazy" src="probe/18-companion-return.png" alt="晓雨跟随返回前厅"><figcaption>晓雨跟随环路返回前厅</figcaption></figure>
<h2>分辨率与边界</h2><p><a href="probe/1280x720/02-foyer-reverse.png">1280×720</a> · <a href="probe/1600x900/02-foyer-reverse.png">1600×900</a> · <a href="probe/1920x1080/02-foyer-reverse.png">1920×1080</a> · <a href="probe/metrics.json">引擎测量数据</a> · <a href="../../assets/generated/dream_escape/3d/dream-manor-hall-v3-manifest.json">资源与来源哈希清单</a></p>
<p>自动化与截图检查不替代人工盲测、真实手柄、物理鼠标或目标硬件验收。含本机 mc_model 的截图仅限本机非商业查看，不提交、分发或导出。旧外观 v1 未随本轮重建。</p></main></html>'''
    (output/'review.html').write_text(content,'utf-8')


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--root',required=True);args=parser.parse_args()
    root=Path(args.root).resolve();output=root/'outputs/manor-v3';output.mkdir(parents=True,exist_ok=True)
    floor_plan(root,output);review(root,output)
    print('MANOR_V3_REVIEW_PASS vectorPlan=true reviewHtml=true')
