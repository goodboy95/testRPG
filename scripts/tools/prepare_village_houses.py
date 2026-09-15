"""Derive three shared 1K colour maps from adopted CC0 sources. Never edits input assets."""
from pathlib import Path
import hashlib,json
import numpy as np
from PIL import Image,ImageFilter
R=Path(__file__).resolve().parents[2];D=R/'art/blender/village/v5-textures'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def run():
    if D.exists():raise RuntimeError('Saved v5 textures exist; explicitly edit them, do not reinitialize.')
    D.mkdir(parents=True)
    inputs={
      'plaster':R/'art/sources/cc0/polyhaven/environment-cinematic-v1/painted_plaster_wall/painted_plaster_wall_diff_2k.jpg',
      'wood':next((R/'art/sources/cc0/polyhaven/environment-cinematic-v1/oak_veneer_01').glob('*diff_2k.*'))}
    for name,p in inputs.items():
        im=Image.open(p).convert('RGB').resize((1024,1024),Image.Resampling.LANCZOS)
        a=np.asarray(im).astype(float)/255;mean=a.mean((0,1))
        target=np.array([.84,.83,.79] if name=='plaster' else [.49,.37,.25])
        a=np.clip((a/mean)**(.48 if name=='plaster' else .7)*target,0,1)
        Image.fromarray((a*255).astype('uint8')).save(D/(name+'-color.png'))
    # Shared physical repair surface, not a painted task mark or a fixed lighting shadow.
    a=np.asarray(Image.open(D/'plaster-color.png')).astype(float)
    a=np.clip(a*.95+9,0,255);Image.fromarray(a.astype('uint8')).save(D/'repair-color.png')
    record={'date':'2026-09-15','tool':'Python Pillow / NumPy offline derivation','aiGenerated':False,
      'license':'CC0-1.0','licenseRecord':'art/sources/cc0/village-art-v3/license.html',
      'reusedManifest':'art/sources/cc0/village-art-v3/provenance.json',
      'recipe':'scripts/tools/prepare_village_houses.py','processing':['1K colour, contrast compression .48 plaster / .70 timber','No baked illumination; shared material factors provide per-house colours','Existing brick/roof/stone normal and roughness maps reused unchanged'],
      'inputs':[{'path':str(p.relative_to(R)),'sha256':sha(p),'assetUrl':'https://polyhaven.com/a/'+p.parent.name,'dimensions':Image.open(p).size} for p in inputs.values()],
      'outputs':[{'path':str(p.relative_to(R)),'sha256':sha(p),'dimensions':Image.open(p).size} for p in D.glob('*.png')]}
    (D/'provenance.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
if __name__=='__main__':run()
