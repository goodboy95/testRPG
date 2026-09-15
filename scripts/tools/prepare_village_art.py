"""Download selected CC0 source maps, then derive offline village art textures. No AI generation."""
import concurrent.futures,hashlib,json,urllib.request,math,random
from pathlib import Path
import numpy as np
from PIL import Image,ImageEnhance,ImageFilter,ImageDraw

ROOT=Path(__file__).resolve().parents[2]
SOURCE=ROOT/'art/sources/cc0/village-art-v3'
DEST=ROOT/'art/blender/village/v3-textures'
ASSETS={'bark':'chinese_hackberry_bark','brick':'brick_wall_10','roof':'grey_roof_tiles','fabric':'denim_fabric'}
def get(url):
    return urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'town-rpg offline asset preparation'}),timeout=90).read()
def sha(b):return hashlib.sha256(b).hexdigest()
def download(asset):
    folder=SOURCE/asset;folder.mkdir(parents=True,exist_ok=True)
    mp=folder/'files.json'
    if not mp.exists():mp.write_bytes(get('https://api.polyhaven.com/files/'+asset))
    meta=json.loads(mp.read_bytes());maps={}
    keys=['leaves_diff','leaves_alpha','leaves_nor_gl','leaves_rough'] if asset=='island_tree_01' else ['diff','nor_gl','rough']
    for key in keys:
        actual=key if key in meta else {'diff':'Diffuse','rough':'Rough'}[key]
        size='2k' if asset=='island_tree_01' else '1k';entry=meta[actual][size]['png'];p=folder/Path(entry['url']).name
        if not p.exists():p.write_bytes(get(entry['url']))
        raw=p.read_bytes();assert hashlib.md5(raw).hexdigest()==entry['md5'],p
        maps[key]={'file':p.relative_to(ROOT).as_posix(),'url':entry['url'],'sha256':sha(raw),'size':len(raw),'dimensions':Image.open(p).size}
    print('CC0_SOURCE',asset,flush=True)
    return {'id':asset,'assetUrl':'https://polyhaven.com/a/'+asset,'license':'CC0-1.0','maps':maps}
def run():
    SOURCE.mkdir(parents=True,exist_ok=True);DEST.mkdir(parents=True,exist_ok=True)
    (SOURCE/'.gdignore').touch()
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:records=list(pool.map(download,[*ASSETS.values(),'island_tree_01']))
    (SOURCE/'license.html').write_bytes(get('https://polyhaven.com/license'))
    mapping={r['id']:r for r in records}
    for key,asset in ASSETS.items():
        maps=mapping[asset]['maps']
        color=Image.open(ROOT/maps['diff']['file']).convert('RGB').resize((1024,1024),Image.Resampling.LANCZOS)
        if key in ('roof','fabric'):color=ImageEnhance.Color(color).enhance(.10)
        else:color=ImageEnhance.Color(color).enhance(.60)
        arr=np.asarray(color).astype(np.float32)/255
        targets={'roof':(.28,.31,.30),'brick':(.46,.40,.34),'bark':(.38,.35,.28),'fabric':(.75,.73,.66)}
        target=np.array(targets[key]);arr=np.clip((arr/np.maximum(arr.mean((0,1)),.01))**.65*target,0,1)
        Image.fromarray((arr*255).astype('uint8')).save(DEST/(key+'-color.png'))
        if key=='fabric':
            Image.fromarray((np.clip(arr*np.array([.43,.60,.48]),0,1)*255).astype('uint8')).save(DEST/'canopygreen-color.png')
        Image.open(ROOT/maps['nor_gl']['file']).convert('RGB').resize((1024,1024),Image.Resampling.LANCZOS).save(DEST/(key+'-normal.png'))
        Image.open(ROOT/maps['rough']['file']).convert('L').resize((1024,1024),Image.Resampling.LANCZOS).save(DEST/(key+'-rough.png'))
    maps=mapping['island_tree_01']['maps']
    leaf=Image.open(ROOT/maps['leaves_diff']['file']).convert('RGB');leaf=ImageEnhance.Color(leaf).enhance(.65)
    alpha=Image.open(ROOT/maps['leaves_alpha']['file']).convert('L');arr=np.asarray(leaf).astype(np.float32)/255;mask=np.asarray(alpha)>128
    arr=np.clip((arr/arr[mask].mean(0))**.7*np.array([.44,.55,.26]),0,1)
    leaf=Image.fromarray((arr*255).astype('uint8'));leaf.putalpha(alpha)
    leaf.save(DEST/'leaves-color.png')
    # Four original twig arrangements assembled from the adopted scanned leaves.
    # Use the clean second leaf; discard scanning markers elsewhere in the atlas.
    sourceLeaf=leaf.crop((int(.17*2048),int(.005*2048),int(.345*2048),int(.425*2048)))
    atlas=Image.new('RGBA',(2048,2048));rng=random.Random(371)
    for cell in range(4):
        tile=Image.new('RGBA',(1024,1024));draw=ImageDraw.Draw(tile)
        stem=[(510,930),(495+cell*7,700),(525-cell*8,420),(540,145)]
        draw.line(stem,fill=(93,85,51,255),width=8)
        for row in range(6):
            y=800-row*113
            for side in (-1,1):
                x=510+side*rng.randint(90,205);draw.line([(510,y+40),(x,y-60)],fill=(103,95,59,255),width=4)
                sprite=sourceLeaf.resize((rng.randint(100,135),rng.randint(210,285)),Image.Resampling.LANCZOS)
                sprite=ImageEnhance.Brightness(sprite).enhance(rng.uniform(.92,1.17)).rotate(side*rng.uniform(32,63),resample=Image.Resampling.BICUBIC,expand=True)
                tile.alpha_composite(sprite,(int(x-sprite.width/2),int(y-80-sprite.height/2)))
        atlas.alpha_composite(tile,((cell%2)*1024,(cell//2)*1024))
    atlas.save(DEST/'branches-color.png')
    old=ROOT/'art/sources/cc0/polyhaven/environment-cinematic-v1/painted_plaster_wall/painted_plaster_wall_diff_2k.jpg'
    plaster=Image.open(old).convert('RGB').resize((1024,1024),Image.Resampling.LANCZOS)
    arr=np.asarray(plaster).astype(np.float32)/255;arr=np.clip((arr/arr.mean((0,1)))**.50*np.array([.69,.69,.62]),0,1)
    Image.fromarray((arr*255).astype('uint8')).save(DEST/'plaster-color.png')
    for source,suffix in [('leaves_nor_gl','normal'),('leaves_rough','rough')]:
        Image.open(ROOT/maps[source]['file']).resize((1024,1024),Image.Resampling.LANCZOS).save(DEST/('leaves-'+suffix+'.png'))
    report={'date':'2026-09-14','provider':'Poly Haven','providerUrl':'https://polyhaven.com','licenseUrl':'https://polyhaven.com/license','tool':'Python Pillow and NumPy offline processing','aiGenerated':False,'assets':records,'postprocessing':['1K runtime PBR maps; 2K leaf color/alpha atlas','muted colors and compressed color contrast; preserved original CC0 maps','leaf alpha packed into color; no baked sun shadows'],'derived':{p.name:sha(p.read_bytes()) for p in DEST.glob('*.png')}}
    adopted=ROOT/'art/sources/cc0/polyhaven/environment-cinematic-v1'
    report['reusedSourceManifest']=(adopted/'source-manifest.json').relative_to(ROOT).as_posix()
    report['licenseSnapshot']={'file':(SOURCE/'license.html').relative_to(ROOT).as_posix(),'sha256':sha((SOURCE/'license.html').read_bytes())}
    report['reusedSources']=[{'file':p.relative_to(ROOT).as_posix(),'sha256':sha(p.read_bytes()),'dimensions':Image.open(p).size,'license':'CC0-1.0','assetUrl':'https://polyhaven.com/a/'+p.parent.name} for name in ['painted_plaster_wall','oak_veneer_01','terrazzo_tiles'] for p in sorted((adopted/name).glob('*.jpg'))]
    report['parameters']={'units':'metres','colorContrastExponent':{'leaves':.7,'plaster':.5},'leafColorTarget':[.44,.55,.26],'leafAtlasSize':[2048,2048],'leafAtlasCells':4,'leafSpriteSeed':371,'alphaCutoff':.45,'runtimeGeneration':False,'normalConvention':'OpenGL +Y','recipe':'scripts/tools/prepare_village_art.py','bakedSunShadows':False}
    report['postprocessing']+=['Four twig tiles assembled from one clean scanned leaf crop; stems drawn offline','Muted photographic plaster resampled to 1K; old normal/ORM maps retained','Runtime foliage uses branches-color.png; leaf source atlas and normal/rough intermediates are not runtime bindings']
    (SOURCE/'provenance.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print('VILLAGE_ART_TEXTURES_PASS',flush=True)
if __name__=='__main__':run()
