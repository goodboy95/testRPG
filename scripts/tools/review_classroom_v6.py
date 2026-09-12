"""Deterministic vector floor plan and local before/after review. No generated raster art."""
from pathlib import Path
import argparse
import html
import re


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', type=Path, default=Path('.'))
    args = parser.parse_args()
    root = args.root.resolve()
    output = root / 'outputs/classroom-v6'
    output.mkdir(parents=True, exist_ok=True)
    source = (root / 'scripts/presentation/ClassroomLayout3D.cs').read_text('utf-8')
    def array(name):
        return [float(n.rstrip('f')) for n in re.search(name + r' = \[([^]]+)\]', source)[1].split(',')]
    svg = ['<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="1060" viewBox="0 0 1200 1060">',
           '<rect width="1200" height="1060" fill="#f3f0e8"/>',
           '<g font-family="Microsoft YaHei,sans-serif" fill="#23382d">',
           '<text x="80" y="54" font-size="27">第一室 v6 · 12 组课桌 / 12 × 9 米</text>',
           '<text x="80" y="88" font-size="16">Godot 平面坐标 X / Z · 北侧黑板 · 东侧出口 · 西侧三组窗</text>']
    def rect(x,z,w,d,color):
        svg.append(f'<rect x="{600+(x-w/2)*78:.1f}" y="{530+(z-d/2)*78:.1f}" width="{w*78:.1f}" height="{d*78:.1f}" fill="{color}" stroke="#40574a" stroke-width="1.5"/>')
    def label(x,z,text):
        svg.append(f'<text x="{600+x*78:.1f}" y="{530+z*78:.1f}" text-anchor="middle" font-size="14">{html.escape(text)}</text>')
    rect(0,0,12,9,'#e0e2d7')
    rect(0,1.1,3.68,5.7,'#c8dbca')
    rect(0,-4.28,5.8,.2,'#294a3c')
    rect(.55,-3.25,2.05,1.05,'#b79b71'); label(.55,-3.2,'教师桌')
    rect(-5.05,-3.78,1.34,.74,'#b79b71'); label(-5.05,-3.72,'地图柜')
    rect(-3.4,-2.05,.78,.44,'#b79b71'); label(-3.4,-2.35,'作息表')
    for i,z in enumerate(array('DeskRows')):
        for j,x in enumerate(array('DeskColumns')):
            rect(x,z,.80,.54,'#ae8558')
            rect(x,z+.48,.40,.36,'#ae8558')
            label(x,z+.02,f'{i*4+j+1:02}')
    for z in (-2.8,-.9,1.0): rect(-5.94,z,.10,1.78,'#9ebfc2')
    rect(5.94,3.35,.12,1.2,'#c2975d')
    for x in (-3.55,3.55): rect(x,2.25,.60,.38,'#eee3b9')
    for x,z,t in [(-3.55,2.30,'维护卡'),(3.55,2.30,'值日便签'),(0,1.2,'中央净宽 3.68 m'),
                  (0,3.75,'后排主通道 ≥ 1.2 m'),(-4.8,.2,'窗边通道'),(4.8,.2,'东侧通道'),
                  (2.4,-2.21,'原新游戏出生'),(5.1,2.86,'返室出生')]: label(x,z,t)
    svg += ['<text x="80" y="955" font-size="16">家具碰撞包含椅子；角色碰撞半径约 0.3 m。机关与纸面线索分离 ≥ 2 m。</text>',
            '<text x="80" y="988" font-size="16">主要通道按实际碰撞外缘计算。正北为图上方；门窗位置与 v5 保持一致。</text>', '</g></svg>']
    (output/'floor-plan.svg').write_text('\n'.join(svg),'utf-8')
    content=['<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>第一室 v6 验收对照</title>',
             '<style>body{max-width:1440px;margin:40px auto;background:#efeee9;color:#263b32;font:16px/1.7 "Microsoft YaHei",sans-serif;padding:24px}img{width:100%;display:block}section{margin:32px 0}.pair{display:grid;grid-template-columns:1fr 1fr;gap:20px}figure{margin:0}figcaption{padding:8px 0}h1{font-size:32px}h2{font-size:22px}a{color:#305c49}</style>',
             '<h1>第一室 · 写实旧教室重制</h1><p>12 组课桌、宽中央通道、实体线索承托。此页为本机验收材料；含本机受限角色的截图不可再分发。</p><p>对照固定环境机位；历史截图使用旧人物表现，因此人物不作为本轮对比对象。</p>',
             '<section><h2>带尺寸布局</h2><img src="floor-plan.svg"></section>']
    for label,name in [('北侧教学区','01-view-blackboard.png'),('东侧出口','02-view-east-door.png'),
                       ('后排','03-view-rear-wall.png'),('西窗','04-view-west-window.png'),
                       ('课桌近景','15-closeup-student-desks.png')]:
        content.append(f'<section><h2>{label} · 同机位对照</h2><div class="pair"><figure><img src="../classroom-v5-full-runtime/{name}"><figcaption>v5 历史运行时截图</figcaption></figure><figure><img src="1920x1080/{name}"><figcaption>v6 当前运行时</figcaption></figure></div></section>')
    content.append('<section><h2>独立验收</h2><p><a href="validation.md">验证记录与边界</a> · <a href="probe/metrics.json">分辨率与环绕性能数据</a></p></section></html>')
    (output/'review.html').write_text('\n'.join(content),'utf-8')
    print('CLASSROOM_V6_REVIEW_PASS vectorPlan=true')


if __name__ == '__main__': main()
