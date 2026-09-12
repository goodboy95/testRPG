"""Read shared room layout and write a dimensioned, review-only SVG floor plan."""
import argparse, html, re
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('room',choices=['archive','auditorium']);p.add_argument('--root',default='.');a=p.parse_args();root=Path(a.root).resolve()
name='ArchiveRoomLayout3D' if a.room=='archive' else 'AuditoriumLayout3D';s=(root/'scripts/presentation'/f'{name}.cs').read_text('utf-8')
nums={k:float(n) for k,n in re.findall(r'const float (\w+) = (-?[\d.]+)f;',s)}
v={k:tuple(float(n.strip().removesuffix('f')) for n in ns.split(',')) for k,ns in re.findall(r'public static readonly Vector3 (\w+) = new\(([^)]+)\);',s)}
w,d=nums['Width'],nums['Depth'];scale=48;left=90;top=95;pw=w*scale;ph=d*scale
parts=[f'<svg xmlns="http://www.w3.org/2000/svg" width="{pw+430}" height="{ph+190}" viewBox="0 0 {pw+430} {ph+190}">', '<rect width="100%" height="100%" fill="#f5f1e8"/>','<g font-family="Microsoft YaHei, sans-serif" fill="#293c3a">',f'<text x="{left}" y="38" font-size="24">{("档案与录音修复室" if a.room=="archive" else "旧学校礼堂")} · v2 布局</text>',f'<text x="{left}" y="64" font-size="13">{w:g} × {d:g} × {nums["Height"]:g} m · 北 ↑ · 所有交互位于地面层</text>',f'<rect x="{left}" y="{top}" width="{pw}" height="{ph}" fill="#e3dfd3" stroke="#293c3a" stroke-width="5"/>']
def rect(k,p,sz):
 x,y,z=p;ww,h,dd=sz;xx=left+(x+w/2-ww/2)*scale;zz=top+(z+d/2-dd/2)*scale
 parts.append(f'<rect x="{xx}" y="{zz}" width="{ww*scale}" height="{dd*scale}" fill="#9c9785" stroke="#525e52" stroke-width="1"/>')
 parts.append(f'<text x="{xx+ww*scale/2}" y="{zz+dd*scale/2}" text-anchor="middle" font-size="9">{html.escape(k)}</text>')
for k,pos in v.items():
 if k.endswith('Position') and k[:-8]+'Size' in v:rect(k[:-8],pos,v[k[:-8]+'Size'])
for k,pos in v.items():
 if re.fullmatch(r'Seat\d\d',k):rect(k,pos,v['SeatSize'])
markers=[]
for key,expr in re.findall(r'\["([^"]+)"\] = ([^\n]+)',s):
 expr=expr.rstrip(',').strip();m=re.match(r'new\(([^)]+)\)',expr)
 if m:pos=tuple(float(n.strip().removesuffix('f')) for n in m.group(1).split(','))
 elif expr in v:pos=v[expr]
 else:continue
 markers.append((key,pos))
for i,(key,(x,y,z)) in enumerate(markers,1):
 xx=left+(x+w/2)*scale;zz=top+(z+d/2)*scale
 parts.extend([f'<circle cx="{xx}" cy="{zz}" r="10" fill="#f3cc83" stroke="#624b25"/>',f'<text x="{xx}" y="{zz+3.5}" text-anchor="middle" font-size="10">{i}</text>',f'<text x="{left+pw+25}" y="{top+i*25}" font-size="10">{i:02d} {html.escape(key)}</text>'])
x,y,z=v['Entry'];parts.append(f'<circle cx="{left+(x+w/2)*scale}" cy="{top+(z+d/2)*scale}" r="8" fill="#327c70"/>')
parts.extend([f'<text x="{left}" y="{top+ph+36}" font-size="13">中央净通路 ≥ {nums["MainAisleWidth"]:g} m；次通路 ≥ {nums["SecondaryAisleWidth"]:g} m。家具灰块为共享布局碰撞边界。</text>',f'<text x="{left}" y="{top+ph+59}" font-size="11">绿点为入场安全点；数字为独立交互点。玩家胶囊通行与晓雨绕行由引擎专项另行验证。</text>','</g></svg>'])
out=root/'outputs/late-rooms-v2'/f'{a.room}-layout-v2.svg';out.parent.mkdir(parents=True,exist_ok=True);out.write_text('\n'.join(parts),'utf-8');print(out)
