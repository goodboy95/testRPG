"""Export or read-only check of the saved, static Shen Yan v2 artwork.

Blender --background --factory-startup --python-exit-code 1 --python <this> -- export|check
Never regenerates geometry, edits the saved source, or produces a rig/LOD.
"""
import bpy, hashlib, json, math, struct, sys
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
SOURCE=ROOT/'art/blender/characters/shen-yan-v2/shen-yan-static-v2.blend'
DEST=ROOT/'assets/generated/characters/shen-yan-v2'
GLB=DEST/'shen-yan-static-v2.glb'
MANIFEST=DEST/'manifest.json'
MATERIALS={'Shen_SkinEyes','Shen_Wardrobe','Shen_HairBrows'}

def require(ok,message):
    if not ok: raise RuntimeError(message)

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def relative(path):return path.relative_to(ROOT).as_posix()

def inspect_source():
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    scene=bpy.context.scene
    require(scene.get('textures_baked'), 'Saved source must have finished PBR bake')
    objects=[o for o in bpy.data.collections['ShenYan_Artwork'].objects if o.type=='MESH' and o.get('export_static')]
    require(objects,'No static artwork')
    triangles=0;materials=set();lo=math.inf;hi=-math.inf
    for o in objects:
        require(not o.modifiers,'Unapplied modifier: '+o.name)
        require(o.data.uv_layers.active is not None,'Missing UV: '+o.name)
        require(not o.vertex_groups and not o.animation_data,'Static candidate contains rig data: '+o.name)
        o.data.calc_loop_triangles();triangles+=len(o.data.loop_triangles)
        for m in o.data.materials:materials.add(m.name)
        for v in o.data.vertices:
            p=o.matrix_world@v.co
            require(all(math.isfinite(c) for c in p),'Non-finite position')
            lo=min(lo,p.z);hi=max(hi,p.z)
    require(materials==MATERIALS,'Unexpected material set: '+str(materials))
    require(0<triangles<=32000,'Triangle budget exceeded: '+str(triangles))
    require(1.70<=hi-lo<=1.75,'Unexpected height')
    textures=[]
    for mname in sorted(materials):
        m=bpy.data.materials[mname]
        images=[n.image for n in m.node_tree.nodes if n.type=='TEX_IMAGE']
        require(len(images)==3,'Expected albedo, normal, ORM: '+mname)
        for image in images:
            path=Path(bpy.path.abspath(image.filepath))
            require(path.is_file(),'Missing texture '+str(path))
            require(max(image.size)<=2048,'Texture dimension budget')
            # Check actual data: an absent/flat map cannot masquerade as a bake.
            values=list(image.pixels[::max(4,(len(image.pixels)//16000)//4*4)])
            require(max(values)-min(values)>.004,'Texture has no variation: '+image.name)
            textures.append({'path':relative(path),'sha256':sha(path),'size':list(image.size),'colorSpace':image.colorspace_settings.name})
    require(len(textures)==9,'Expected nine maps')
    provenance_path=ROOT/'art/sources/cc0/npc-human-base-v1/provenance.json'
    provenance=json.loads(provenance_path.read_text(encoding='utf-8'))
    require(provenance['license']=='CC0','Unexpected anatomical base license')
    require(sha(ROOT/provenance['extractedSource'])==provenance['extractedSourceSha256'],'Anatomical source hash mismatch')
    return objects,{'triangles':triangles,'materials':sorted(materials),'heightMeters':round(hi-lo,6),'authoringObjects':len(objects),'textures':textures,'anatomicalBase':provenance}

def read_glb():
    raw=GLB.read_bytes();magic,version,length=struct.unpack_from('<4sII',raw)
    require(magic==b'glTF' and version==2 and length==len(raw),'Invalid GLB header')
    offset=12;document=None;binary=None
    while offset<len(raw):
        size,kind=struct.unpack_from('<II',raw,offset);offset+=8
        if kind==0x4E4F534A:document=json.loads(raw[offset:offset+size])
        elif kind==0x004E4942:binary=raw[offset:offset+size]
        offset+=size
    require(document is not None and binary is not None,'Missing GLB chunk')
    return document,binary

def verify_glb(expected):
    doc,blob=read_glb()
    require(not doc.get('skins') and not doc.get('animations'),'Static GLB must not contain skin or animations')
    require(len(doc['meshes'])==1,'Expected one merged static mesh')
    require(len(doc['materials'])==3,'Expected three materials')
    prims=doc['meshes'][0]['primitives'];require(len(prims)==3,'Expected three material surfaces')
    count=0
    def values(index):
        accessor=doc['accessors'][index];view=doc['bufferViews'][accessor['bufferView']]
        fmt={5126:'f',5125:'I',5123:'H',5121:'B'}[accessor['componentType']]
        size={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[accessor['type']]
        chunk=struct.calcsize('<'+fmt*size);stride=view.get('byteStride',chunk)
        start=view.get('byteOffset',0)+accessor.get('byteOffset',0)
        require(start+(accessor['count']-1)*stride+chunk<=view.get('byteOffset',0)+view['byteLength'],'Accessor outside view')
        return [struct.unpack_from('<'+fmt*size,blob,start+i*stride) for i in range(accessor['count'])]
    for p in prims:
        require(p.get('mode',4)==4,'Expected triangle primitives')
        attrs=p['attributes'];require(all(k in attrs for k in ['POSITION','NORMAL','TEXCOORD_0','TANGENT']),'Missing vertex channel')
        require(not any(k.startswith(('COLOR','JOINTS','WEIGHTS')) for k in attrs),'Unwanted vertex color or skin')
        pos=values(attrs['POSITION']);uv=values(attrs['TEXCOORD_0']);norm=values(attrs['NORMAL']);tangents=values(attrs['TANGENT']);indices=values(p['indices'])
        require(len(indices)%3==0,'Incomplete triangle');count+=len(indices)//3
        require(len(pos)==len(uv)==len(norm)==len(tangents),'Channel count mismatch')
        require(all(0<=i[0]<len(pos) for i in indices),'Invalid vertex index')
        require(all(math.isfinite(v) for channel in (pos,uv,norm,tangents) for row in channel for v in row),'Non-finite vertex data')
        require(all(-.001<=v<=1.001 for row in uv for v in row),'UV outside atlas')
        require(all(.98<sum(v*v for v in row)<1.02 for row in norm),'Non-unit normals')
    require(count==expected and count<=32000,'GLB/source triangle mismatch')
    require(len(doc['images'])==9,'Missing or duplicated embedded maps')
    for image in doc['images']:
        require('bufferView' in image and 'uri' not in image,'Image must be embedded')
        view=doc['bufferViews'][image['bufferView']]
        require(view['byteLength']>300,'Suspicious empty image')
    for m in doc['materials']:
        require(m.get('alphaMode','OPAQUE')=='OPAQUE','No blended hair/skin material allowed')
        require('normalTexture' in m and 'occlusionTexture' in m,'Missing PBR texture')
        require('baseColorTexture' in m['pbrMetallicRoughness'] and 'metallicRoughnessTexture' in m['pbrMetallicRoughness'],'Incomplete PBR material')
    # Import the real exported resource, rather than trusting only the JSON.
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(GLB))
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];require(len(meshes)==1,'Reimport did not produce one mesh')
    meshes[0].data.calc_loop_triangles();require(len(meshes[0].data.loop_triangles)==count,'Reimport changed triangles')
    require(not any(o.type=='ARMATURE' for o in bpy.context.scene.objects),'Reimport contains armature')
    return {'triangles':count,'meshInstances':1,'materialSurfaces':3,'embeddedImages':9,'reimported':True,'skins':0,'animations':0}

def main():
    args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
    require(args in [['export'],['check']],'Use export or check')
    before=sha(SOURCE);objects,info=inspect_source()
    if args==['export']:
        DEST.mkdir(parents=True,exist_ok=True)
        bpy.ops.object.select_all(action='DESELECT')
        for o in objects:o.select_set(True)
        bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join()
        obj=bpy.context.object;obj.name='ShenYanStatic_V2';obj.data.name='ShenYanStatic_V2'
        triangulate=obj.modifiers.new('Export triangulation','TRIANGULATE')
        bpy.ops.object.modifier_apply(modifier=triangulate.name)
        # Baked albedo already includes authoring pigment: exporting vertex colors
        # would multiply the pigment a second time in the engine.
        bpy.ops.export_scene.gltf(filepath=str(GLB),export_format='GLB',use_selection=True,
            export_animations=False,export_skins=False,export_morph=False,
            export_vertex_color='NONE',export_attributes=False,export_tangents=True,
            export_cameras=False,export_lights=False,export_extras=False,export_image_format='AUTO')
        result=verify_glb(info['triangles'])
        manifest={'schema':1,'assetId':'shen_yan.static_v2','characterId':'shen_yan','date':'2026-09-18',
            'runtimeReady':False,'visualApproval':False,'purpose':'Static art candidate; no animation or village adoption',
            'selfReview':'not_passed: facial character, hair silhouette and clothing finish need art rework',
            'reviewRecord':'doc/production/shen-yan-static-v2-validation.md',
            'authoringTool':'Blender '+bpy.app.version_string,'source':relative(SOURCE),'sourceSha256':before,
            'glb':relative(GLB),'glbSha256':sha(GLB),'sourceInspection':info,'exportInspection':result,
            'method':'CC0 anatomy adaptation and scripted mesh authoring; pigment/procedural surface baking; no high-poly sculpt or concept projection',
            'conceptRecord':'art/concepts/characters/shen-yan-v2/generation.json',
            'bakeRecord':'art/blender/characters/shen-yan-v2/bake-record.json'}
        MANIFEST.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    else:
        manifest=json.loads(MANIFEST.read_text(encoding='utf-8'))
        require(manifest['sourceSha256']==before and manifest['glbSha256']==sha(GLB),'Stale manifest hashes')
        require(not manifest['runtimeReady'] and not manifest['visualApproval'],'Static candidate must remain unapproved')
        require(info['textures']==manifest['sourceInspection']['textures'],'Stale texture hashes')
        require(info['anatomicalBase']==manifest['sourceInspection']['anatomicalBase'],'Stale provenance')
        result=verify_glb(info['triangles'])
    require(sha(SOURCE)==before,'Export/check modified authoritative source')
    print('SHEN_STATIC_'+args[0].upper()+'_PASS '+json.dumps(result))

if __name__=='__main__':main()
