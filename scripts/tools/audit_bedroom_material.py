"""Read-only source/GLB/texture checks. Optional JSON receipt goes to ignored outputs."""
from pathlib import Path
import json, struct, hashlib, sys
from itertools import product
import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[2]
DIRECTORY=ROOT/'assets/generated/bedroom-material-v1'

def read_glb(path):
    blob=path.read_bytes(); size=struct.unpack_from('<I',blob,12)[0]
    doc=json.loads(blob[20:20+size]); binary=blob[28+size:]
    def accessor(index):
        a=doc['accessors'][index];v=doc['bufferViews'][a['bufferView']]
        dtype={5126:'<f4',5125:'<u4',5123:'<u2',5121:'u1'}[a['componentType']]
        dim={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']]
        offset=v.get('byteOffset',0)+a.get('byteOffset',0)
        return np.frombuffer(binary,dtype=dtype,count=a['count']*dim,offset=offset).reshape(a['count'],dim)
    points=[]; count=0; vertices=0; furniture={}
    def walk(index,parent):
        nonlocal count,vertices
        node=doc['nodes'][index]
        if 'matrix' in node: local=np.array(node['matrix']).reshape(4,4).T
        else:
            local=np.eye(4);local[:3,3]=node.get('translation',[0,0,0])
            x,y,z,w=node.get('rotation',[0,0,0,1])
            local[:3,:3]=np.array([[1-2*y*y-2*z*z,2*x*y-2*z*w,2*x*z+2*y*w],
                [2*x*y+2*z*w,1-2*x*x-2*z*z,2*y*z-2*x*w],[2*x*z-2*y*w,2*y*z+2*x*w,1-2*x*x-2*y*y]])@np.diag(node.get('scale',[1,1,1]))
        matrix=parent@local
        if 'mesh' in node:
            for p in doc['meshes'][node['mesh']]['primitives']:
                a=accessor(p['attributes']['POSITION']);world=a@matrix[:3,:3].T+matrix[:3,3]
                points.append(world);vertices+=len(a);count+=len(accessor(p['indices']))//3
                if doc['materials'][p['material']]['name'] in ('DeskRefined','WardrobeRefined'):
                    assert 'TEXCOORD_0' in p['attributes']
                    assert 'TEXCOORD_1' not in p['attributes'],'Auxiliary source UV leaked into GLB'
                    furniture[doc['materials'][p['material']]['name']]={'min':world.min(axis=0).tolist(),'max':world.max(axis=0).tolist()}
        for child in node.get('children',[]):walk(child,matrix)
    for node in doc['scenes'][doc.get('scene',0)]['nodes']:walk(node,np.eye(4))
    points=np.unique(np.concatenate(points),axis=0)
    quantized=np.unique(np.rint(points*1000).astype(np.int64),axis=0)
    return doc,{'triangles':count,'exportVertices':vertices,'uniquePositionsAt1mm':len(quantized),
        'positionSha256At1mm':hashlib.sha256(quantized.tobytes()).hexdigest(),'furnitureBounds':furniture},points

def max_distance(source,destination):
    buckets={}
    for p in destination:buckets.setdefault(tuple(np.rint(p*1000).astype(int)),[]).append(p)
    maximum=0
    for p in source:
        key=tuple(np.rint(p*1000).astype(int));values=buckets.get(key,[])
        distance=min((float(np.max(np.abs(p-q))) for q in values),default=1)
        if distance>1e-5:
            for delta in product((-1,0,1),repeat=3):
                for q in buckets.get(tuple(k+d for k,d in zip(key,delta)),[]):distance=min(distance,float(np.max(np.abs(p-q))))
        maximum=max(maximum,distance)
        assert distance<1e-5,('World vertex moved',p,distance)
    return maximum

def main():
    old,oldgeometry,oldpoints=read_glb(ROOT/'assets/generated/bedroom-target-v2/bedroom-target-v2.glb')
    new,newgeometry,newpoints=read_glb(DIRECTORY/'bedroom-material-v1.glb')
    assert oldgeometry['triangles']==newgeometry['triangles']==454362
    geometryDistance=max(max_distance(oldpoints,newpoints),max_distance(newpoints,oldpoints))
    desk=newgeometry['furnitureBounds']['DeskRefined']
    assert desk['min'][2]<-1.139 and desk['max'][2]>.539 and desk['max'][1]>.779,'Complete desktop not included'
    textures={}
    for kind in ('desk','wardrobe'):
        for suffix in ('color','normal','orm','old-color','old-orm'):
            name=('' if suffix.startswith('old-') else 'bedroom-material-v1_')+kind+'-'+suffix+'.png'
            path=DIRECTORY/name;img=Image.open(path).convert('RGB');assert img.size==(2048,2048)
            a=np.asarray(img)
            source=ROOT/'art/blender/bedroom-material-v1/textures'/f'{kind}-{suffix}.png'
            assert np.array_equal(a,np.asarray(Image.open(source).convert('RGB'))), 'Exported atlas differs from saved-source PNG: '+name
            if suffix.endswith('orm'):assert np.all(a[:,:,0]==255) and np.all(a[:,:,2]==0),'ORM AO/metallic not neutral'
            settings=path.with_suffix('.png.import').read_text(encoding='utf8')
            assert 'compress/mode=2' in settings and 'compress/high_quality=false' in settings and 'mipmaps/generate=true' in settings
            textures[name]={'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'size':list(img.size)}
        m=next(m for m in new['materials'] if m['name']==kind.title()+'Refined')
        p=m['pbrMetallicRoughness'];assert all(k in p for k in ('baseColorTexture','metallicRoughnessTexture')) and 'normalTexture' in m
        assert p.get('roughnessFactor',1)==1 and p.get('metallicFactor',1)==1
    source=ROOT/'art/blender/bedroom-material-v1/bedroom-material-v1.blend'
    manifest=json.loads((DIRECTORY/'bedroom-material-v1-manifest.json').read_text(encoding='utf8'))
    assert manifest['sourceSha256']==hashlib.sha256(source.read_bytes()).hexdigest()
    bake=json.loads((DIRECTORY/'bake-record.json').read_text(encoding='utf-8-sig'))
    glb_hash=hashlib.sha256((DIRECTORY/'bedroom-material-v1.glb').read_bytes()).hexdigest().upper()
    assert bake['tool'].startswith('4.6.3.stable.mono.')
    assert bake['sourceGlbSha256']==bake['preparation']['sourceSha256']==glb_hash
    assert bake['preparation']==json.loads((DIRECTORY/'unwrap-cache.json').read_text(encoding='utf8'))
    for output in bake['outputs']:
        assert output['sha256']==hashlib.sha256((DIRECTORY/output['name']).read_bytes()).hexdigest().upper()
    assert 'importer="2d_array_texture"' in (DIRECTORY/'bedroom.exr.import').read_text(encoding='utf8')
    receipt={'status':'PASS','oldGeometry':oldgeometry,'newGeometry':newgeometry,'maxWorldVertexDeltaMetres':geometryDistance,'textures':textures,
        'sourceTriangles':manifest['sourceTriangles'],'packedAtlasExportMatches':True,'bakeSourceMatches':True,
        'baseBlendUnchanged':hashlib.sha256((ROOT/'art/blender/bedroom-target-v2/bedroom-target-v2.blend').read_bytes()).hexdigest()==json.loads((ROOT/'art/blender/bedroom-material-v1/provenance.json').read_text(encoding='utf8'))['baseSourceSha256']}
    assert receipt['baseBlendUnchanged']
    if len(sys.argv)>1:(ROOT/sys.argv[1]).write_text(json.dumps(receipt,indent=2),encoding='utf8')
    print('BEDROOM_MATERIAL_ASSET_AUDIT_PASS',json.dumps({'oldGeometry':oldgeometry,'newGeometry':newgeometry}))

if __name__=='__main__':main()
