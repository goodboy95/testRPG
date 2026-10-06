"""Prepare restrained CC0 wood inputs; never edits upstream or saved Blender sources."""
from pathlib import Path
import hashlib, json
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'art/sources/cc0/polyhaven/environment-cinematic-v1/oak_veneer_01'
DEST = ROOT / 'art/blender/bedroom-material-v1/inputs'

def main():
    DEST.mkdir(parents=True, exist_ok=True)
    rgb = np.asarray(Image.open(SOURCE / 'oak_veneer_01_diff_2k.jpg').convert('RGB'), dtype=np.float32) / 255
    # Preserve the photographed grain, reduce contrast, and choose a modest warm finish.
    mean = rgb.mean(axis=(0, 1))
    for name, colour, roughness in [('desk', 'b69570', .45), ('wardrobe', 'b39169', .55)]:
        target = np.array([int(colour[i:i+2], 16)/255 for i in (0, 2, 4)])
        color = np.clip(target + (rgb - mean) * .5, 0, 1)
        Image.fromarray(np.rint(color * 255).astype('uint8')).save(DEST / (name + '-color.png'))
        arm = np.asarray(Image.open(SOURCE / 'oak_veneer_01_arm_2k.jpg').convert('RGB'), dtype=np.float32) / 255
        rough = arm[:, :, 1]
        rough = np.clip(roughness + (rough - rough.mean()) * .23, roughness - .07, roughness + .07)
        packed = np.stack((np.ones_like(rough), rough, np.zeros_like(rough)), axis=2)
        Image.fromarray(np.rint(packed * 255).astype('uint8')).save(DEST / (name + '-orm.png'))
    Image.open(SOURCE / 'oak_veneer_01_nor_gl_2k.jpg').save(DEST / 'wood-normal.png')
    y,x=np.indices((2048,2048));checks=((x//64+y//64)%2)*80+100
    Image.fromarray(np.repeat(checks[:,:,None],3,axis=2).astype('uint8')).save(DEST/'uv-checker.png')
    record = {'provider': 'Poly Haven', 'asset': 'oak_veneer_01', 'license': 'CC0-1.0',
              'assetUrl': 'https://polyhaven.com/a/oak_veneer_01', 'date': '2026-10-06',
              'processing': 'Pillow/NumPy; sRGB contrast 50%, warm mean; independent upstream roughness and GL normal',
              'newAIImages': False, 'sourceHashes': {p.name: hashlib.sha256(p.read_bytes()).hexdigest()
                  for p in SOURCE.glob('*.jpg')}}
    (DEST / 'processing.json').write_text(json.dumps(record, ensure_ascii=False, indent=2), encoding='utf8')
    print('BEDROOM_WOOD_INPUTS_PASS')

if __name__ == '__main__': main()
