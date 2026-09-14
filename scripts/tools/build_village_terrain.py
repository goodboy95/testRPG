"""Offline Terrain3D sample inputs. init never overwrites authored data; check is read-only.
Run Python, then Godot --script scripts/tools/import_village_terrain.gd -- --terrain-init.
"""
from pathlib import Path
import argparse, hashlib, json, math, struct, re
import numpy as np
from PIL import Image, ImageEnhance
import village_geometry as geo

ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'art/terrain/village-sample-v1'
OUT=ROOT/'assets/generated/village/terrain-sample-v1'
IDS=['Grass001','Ground031','Gravel014','Ground037']
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()

def original_grid():
    raw=(ROOT/'assets/generated/village/3d/village-v2.glb').read_bytes()
    length=struct.unpack_from('<I',raw,12)[0]; gltf=json.loads(raw[20:20+length]); binary=raw[28+length:]
    node=next(n for n in gltf['nodes'] if n.get('name')=='Terrain')
    a=gltf['accessors'][gltf['meshes'][node['mesh']]['primitives'][0]['attributes']['POSITION']]
    view=gltf['bufferViews'][a['bufferView']]; start=view.get('byteOffset',0)+a.get('byteOffset',0)
    vertices=np.ndarray((a['count'],3),dtype='<f4',buffer=binary,offset=start,strides=(view.get('byteStride',12),4))
    grid=np.full((217,209),np.nan)
    for x,y,z in vertices: grid[round(float(z)+112),round(float(x)+104)]=y
    assert np.isfinite(grid).all()
    return grid

def generate():
    OUT.mkdir(parents=True,exist_ok=True); ART.mkdir(parents=True,exist_ok=True)
    if (OUT/'manifest.json').exists(): raise RuntimeError('Already initialized. Preserve edited data; use check / backup instead.')
    v=geo.read(ROOT); ex=json.loads(re.search(r'public const string Data = """\s*(.*?)\s*""";', (ROOT/'scripts/presentation/VillageExteriorLayout3D.cs').read_text('utf-8'),re.S)[1])
    axis=np.arange(512)*.5-128; x,z=np.meshgrid(axis,axis)
    # Read actual GLB vertices, then interpolate its original a-c-b / b-c-d triangles.
    g=original_grid(); cx=np.clip(x,-104,104);cz=np.clip(z,-112,104)
    ix=np.minimum(np.floor(cx+104).astype(int),207); iz=np.minimum(np.floor(cz+112).astype(int),215)
    u=cx+104-ix;w=cz+112-iz
    base=np.where(u+w<=1,g[iz,ix]+(g[iz,ix+1]-g[iz,ix])*u+(g[iz+1,ix]-g[iz,ix])*w,
                  g[iz+1,ix+1]+(g[iz+1,ix]-g[iz+1,ix+1])*(1-u)+(g[iz,ix+1]-g[iz+1,ix+1])*(1-w))
    road=np.full_like(x,1e4); protected=np.zeros(x.shape,dtype=bool)
    for r in v['roads']:
        for a,b in zip(r['points'],r['points'][1:]):
            dx=b[0]-a[0];dz=b[1]-a[1];t=np.clip(((x-a[0])*dx+(z-a[1])*dz)/(dx*dx+dz*dz),0,1)
            road=np.minimum(road,np.hypot(x-a[0]-t*dx,z-a[1]-t*dz)-r['width']/2)
    protected |= road<.75
    for b in v['buildings']:
        angle=math.radians(b['yaw']);lx=(x-b['position'][0])*math.cos(angle)-(z-b['position'][2])*math.sin(angle);lz=(x-b['position'][0])*math.sin(angle)+(z-b['position'][2])*math.cos(angle)
        a,c,ww,dd=geo.pad(b); protected |= (lx>a-1)&(lx<a+ww+1)&(lz>c-1)&(lz<c+dd+1)
    # Existing plaza surfaces and first-batch paving/fixtures, including feet and standing zones.
    protected |= (x>-23)&(x<2)&(z>1)&(z<16)
    for p in ex['props']:
        protected |= (abs(x-p['position'][0])<p['size'][0]/2+.75)&(abs(z-p['position'][2])<p['size'][2]/2+.75)
    for b in v['bridges']:
        protected |= (abs(z-b['z'])<b['width']/2+2)&(abs(x-geo.stream(b['z']))<b['halfSpan']+4)
    for tx,tz,*_ in geo.trees(v):protected |= np.hypot(x-tx,z-tz)<1.25
    for tx,tz in v['utilityPoles']:protected |= np.hypot(x-tx,z-tz)<.75
    # Preserve bank wall foundations while sculpting the outside soil and river-facing slope.
    bankdistance=abs(x-(49+4*np.sin(z/38)))
    protected |= abs(bankdistance-8.7)<.75
    def mask(a,b,c,d,fade=2):
        t=np.clip(np.minimum.reduce([x-a,b-x,z-c,d-z])/fade,0,1);return t*t*(3-2*t)
    home=mask(-74,-39,49,62);plaza=mask(-25,3,0,21);bank=mask(33,49,5,31)
    region=np.maximum.reduce([home,plaza,bank]); region[(x<-104)|(x>104)|(z<-112)|(z>104)]=0
    height=base+region*np.where(protected,0, .065*np.sin(x*.67)*np.cos(z*.48)-.065*np.exp(-((road-1.1)/.65)**2))
    height+=bank*np.where(protected,0,.12*np.sin(z*.65)*np.exp(-((bankdistance-6.8)/1.8)**2))
    # Texture controls: base/overlay/blend, no holes, no autoshader.
    overlay=np.ones(x.shape,dtype=np.uint32)
    edge=np.clip(1-road/2.8,0,1);blend=region*edge*.88
    gravel=(road<.8)&(road>-.2);overlay[gravel]=2
    shore=bank>0;overlay[shore]=3;blend[shore]=np.maximum(blend[shore],bank[shore]*np.clip((12-bankdistance[shore])/6,0,1))
    blend=np.clip(blend*(.84+.16*np.sin(x*1.4+np.sin(z*1.1))),0,1)
    control=(overlay<<22)|(np.rint(blend*255).astype(np.uint32)<<14)
    colour=np.ones((*x.shape,4),dtype=np.uint8)*255
    macro=(.94+.035*np.sin(x*.09)*np.cos(z*.11)-bank*.06*np.clip((9-bankdistance)/5,0,1))
    colour[:,:,:3]=(macro[:,:,None]*255).astype(np.uint8);colour[:,:,3]=128
    for name,data in [('height',height.astype('<f4')),('baseline',base.astype('<f4')),('control',control.astype('<u4')),('color',colour),('protected',protected.astype('uint8'))]:
        (ART/(name+'.bin')).write_bytes(data.tobytes())
    # Shared low-detail assets, fixed seeded placement, no runtime scattering.
    rng=np.random.default_rng(914); props=[]
    for _ in range(4200):
        px=float(rng.uniform(-75,50));pz=float(rng.uniform(1,63));i=int((px+128)*2);j=int((pz+128)*2)
        if region[j,i]<.35 or protected[j,i] or road[j,i]<.7 or bankdistance[j,i]<8.9:continue
        if rng.random()>.42:continue
        props.append(dict(x=px,z=pz,y=float(height[j,i]),yaw=float(rng.uniform(0,6.283)),scale=float(rng.uniform(.7,1.25)),kind='stone' if rng.random()<.2 else 'grass'))
    (OUT/'details.json').write_text(json.dumps(props,indent=2))
    for name in IDS:
        src=ART/'sources'/name
        def load(s):return Image.open(src/(name+'_1K-PNG_'+s+'.png')).resize((1024,1024),Image.Resampling.LANCZOS)
        col=ImageEnhance.Color(load('Color').convert('RGB')).enhance(.68 if name=='Grass001' else .8)
        col=ImageEnhance.Brightness(col).enhance(1.07 if name=='Grass001' else .92)
        col.putalpha(load('Displacement').convert('L'));col.save(OUT/(name+'-albedo-height.png'))
        normal=load('NormalGL').convert('RGB');normal.putalpha(load('Roughness').convert('L'));normal.save(OUT/(name+'-normal-roughness.png'))
    manifest=dict(version=1,status='independent-sample',spacing=.5,regionSize=256,regions=4,origin=[-128,-128],dimensions=[512,512],
      baselineGlbSha256=sha(ROOT/'assets/generated/village/3d/village-v2.glb'),protectedSamples=int(protected.sum()),maxProtectedDelta=float(abs(height-base)[protected].max()),
      editedSamples=int(np.count_nonzero(height!=base)),maximumHeightDelta=float(abs(height-base).max()),details=len(props),
      textureIds=IDS,textureBytesEstimate=4*2*1024*1024*4*4/3,inputs={p.name:sha(p) for p in ART.glob('*.bin')},
      regionsDescription=['home gate and 30m lane','plaza edge','south bridge west bank, 20m north'],
      note='Four regions migrate the base. Only three zones are art samples. Outside zones grass shading changes; geometry retained.')
    (OUT/'manifest.json').write_text(json.dumps(manifest,indent=2));print(json.dumps(manifest))

def check():
    m=json.loads((OUT/'manifest.json').read_text());assert sha(ROOT/'assets/generated/village/3d/village-v2.glb')==m['baselineGlbSha256']
    for n,h in m['inputs'].items():assert sha(ART/n)==h,n
    b=np.fromfile(ART/'baseline.bin','<f4');h=np.fromfile(ART/'height.bin','<f4');p=np.fromfile(ART/'protected.bin','uint8').astype(bool)
    assert np.max(abs(b[p]-h[p]))<=.01;assert np.isfinite(h).all()
    print('VILLAGE_TERRAIN_INPUT_CHECK_PASS protected=',p.sum())
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('action',choices=['init','check']);a=parser.parse_args()
    generate() if a.action=='init' else check()
