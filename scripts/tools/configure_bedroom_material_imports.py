"""Set Compatibility-compatible imports for the ten A/B atlas textures only."""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
DIRECTORY = ROOT / 'assets/generated/bedroom-material-v1'

def main():
    names = [f'bedroom-material-v1_{kind}-{channel}.png' for kind in ('desk','wardrobe') for channel in ('color','normal','orm')]
    names += [f'{kind}-old-{channel}.png' for kind in ('desk','wardrobe') for channel in ('color','orm')]
    names += ['uv-checker.png']
    for name in names:
        path = DIRECTORY / (name+'.import')
        if not path.exists(): raise RuntimeError('Import with Godot before configuring: '+name)
        content = path.read_text(encoding='utf8')
        for key,value in [('compress/mode','2'),('compress/high_quality','false'),('compress/normal_map','1' if '-normal.' in name else '2'),('mipmaps/generate','true')]:
            content,count = re.subn(r'^'+re.escape(key)+r'=.*$',key+'='+value,content,flags=re.M)
            if count!=1: raise RuntimeError('Missing import setting '+key+' in '+name)
        path.write_text(content,encoding='utf8')
    print('BEDROOM_MATERIAL_IMPORT_SETTINGS_PASS count='+str(len(names)))

if __name__=='__main__': main()
