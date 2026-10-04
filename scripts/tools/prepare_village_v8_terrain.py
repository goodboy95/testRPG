"""Original low-frequency pigment maps. Preserves Terrain3D v4 heights/control maps."""
from pathlib import Path
import json, math, struct, zlib
R=Path(__file__).resolve().parents[2]
D=R/'assets/generated/village/terrain-v8'

def png(path,pixels,n):
    def chunk(t,b):return struct.pack('>I',len(b))+t+b+struct.pack('>I',zlib.crc32(t+b)&0xffffffff)
    data=b''.join(b'\0'+bytes(pixels[y*n*4:(y+1)*n*4]) for y in range(n))
    path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',n,n,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(data))+chunk(b'IEND',b''))

def main():
    D.mkdir(exist_ok=True)
    source=R/'assets/generated/village/terrain-sample-v1'
    assets=(source/'terrain-assets.tres').read_text()
    for name,col in [('Grass001',(143,165,106)),('Ground031',(174,160,124)),('Gravel014',(184,180,152)),('Ground037',(158,155,115))]:
        n=256;pixels=[]
        for y in range(n):
            for x in range(n):
                u=x/n*math.tau;v=y/n*math.tau
                a=4*math.sin(u+math.sin(v))+3*math.cos(v*2+.6*math.sin(u*2))+1.2*math.sin(u*9+v*5)
                pixels.extend([int(c+a) for c in col]+[128])
        png(D/(name+'-albedo-height.png'),pixels,n)
        png(D/(name+'-normal-roughness.png'),[128,128,255,250]*(n*n),n)
        for suffix in ('-albedo-height.png','-normal-roughness.png'):
            target=D/(name+suffix+'.import')
            if target.exists():
                target.write_text(target.read_text().replace('mipmaps/generate=false','mipmaps/generate=true').replace('detect_3d/compress_to=1','detect_3d/compress_to=0').replace('compress/normal_map=0','compress/normal_map=2'))
    assets=assets.replace('terrain-sample-v1/','terrain-v8/').replace('uv_scale = 0.5','uv_scale = 0.22')
    (D/'terrain-assets.tres').write_text(assets)
    (D/'terrain-material.tres').write_text((source/'terrain-material.tres').read_text())
    scene=(R/'assets/generated/village/terrain-v4/terrain.tscn').read_text().replace('terrain-sample-v1/','terrain-v8/')
    (D/'terrain.tscn').write_text(scene)
    (D/'provenance.json').write_text(json.dumps(dict(method='Original mathematical pigment maps',tool='Python stdlib',seed='deterministic sine fields',heightSource='terrain-v4/data unchanged',normal='flat',generatedModel=None),indent=2))

if __name__=='__main__':main()
