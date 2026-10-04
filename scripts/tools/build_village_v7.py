"""Derive streaming resources from the saved v6, never run historical builders.

init: save an independent editable source (refuses overwrite).
export: export the saved v7 source. check: validate hashes and GLB costs, read only.
LOD0/1 textures are retained. LOD2/3 bake base colour to vertex colours; leaf
alpha cards keep their existing CC0 texture. Collision stays in VillageLayout3D.
"""
import hashlib, json, math, struct, sys
from pathlib import Path
import bpy
import bmesh
import numpy as np
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'art/blender/village/village-v7.blend'
BASE = ROOT / 'art/blender/village/village-v6.blend'
DEST = ROOT / 'assets/generated/village/stream-v7'
LAYOUT = ROOT / 'assets/generated/village/layout-v4.json'
DISTRICTS = ['home', 'west', 'plaza', 'station', 'east_south', 'east_north', 'creek']
LOD_TRIANGLE_LIMITS = (65000, 60000, 30000, 18000)
LOD_SURFACE_LIMITS = (24, 24, 2, 2)


def validate_units(units):
    """Fail export/check when an edited source breaks the authored per-unit contract."""
    assert len({u['id'] for u in units}) == len(units), 'Duplicate render unit'
    nodes = [lod['node'] for u in units for lod in u['lods']]
    assert len(set(nodes)) == len(nodes), 'Duplicate LOD node'
    layout = json.loads(LAYOUT.read_text(encoding='utf-8'))
    assert {u['id'] for u in units if u['category'] == 'building'} == {
        b['id'] for b in layout['geometry']['buildings']}, 'Building collision/visual coverage differs'
    for u in units:
        assert len(u['lods']) == 4, u['id']
        assert len(u['bounds']) == 6 and all(math.isfinite(v) for v in u['bounds']), u['id']
        for level, lod in enumerate(u['lods']):
            assert lod['node'] == u['id'] + '_lod' + str(level), lod['node']
            assert 0 < lod['triangles'] <= LOD_TRIANGLE_LIMITS[level], (u['id'], level, 'triangles')
            assert 0 < lod['surfaces'] <= LOD_SURFACE_LIMITS[level], (u['id'], level, 'surfaces')
            assert 0 <= lod['screenFraction'] <= 1, (u['id'], level, 'screenFraction')

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def district(x, z):
    if 28 <= x <= 60: return 'creek'
    if x > 28: return 'east_south' if z >= 25 else 'east_north'
    if z < -30: return 'station'
    if x < -34: return 'home' if z >= 29 else 'west'
    return 'plaza'

def glb_data(path):
    raw = path.read_bytes()
    n = struct.unpack_from('<I', raw, 12)[0]
    return json.loads(raw[20:20+n])

def costs(path):
    d = glb_data(path)
    result = {}
    for node in d['nodes']:
        if 'mesh' not in node: continue
        mesh = d['meshes'][node['mesh']]
        ps = mesh['primitives']
        result[node['name']] = dict(triangles=sum(d['accessors'][p['indices']]['count']//3 for p in ps),
            surfaces=len(ps), materials=sorted(set(d['materials'][p['material']]['name'] for p in ps)))
    return result

def share_textures(path):
    """External shared images avoid one GPU texture copy per district import."""
    raw=path.read_bytes();n=struct.unpack_from('<I',raw,12)[0];d=json.loads(raw[20:20+n]);binary=raw[28+n:]
    image_views=set();known={}
    # Prefer the adopted VRAM-compressed image imports, not historical uncompressed v1.
    for p in sorted((ROOT/'assets/generated/village/3d').iterdir(),key=lambda p:p.name,reverse=True):
        if p.suffix.lower() in ['.png','.jpg','.jpeg']:known.setdefault(sha(p),p)
    for img in d.get('images',[]):
        if 'bufferView' in img:
            index=img.pop('bufferView');image_views.add(index);v=d['bufferViews'][index]
            data=binary[v.get('byteOffset',0):v.get('byteOffset',0)+v['byteLength']]
        else:data=(path.parent/img['uri']).read_bytes()
        digest=hashlib.sha256(data).hexdigest()
        target=known.get(digest)
        if target is None:
            target=DEST/'textures'/(digest+('.jpg' if img.get('mimeType')=='image/jpeg' else '.png'))
            target.parent.mkdir(exist_ok=True)
            if not target.exists():target.write_bytes(data)
        import os
        img['uri']=os.path.relpath(target,path.parent).replace('\\','/');img.pop('mimeType',None)
    views=[];mapping={};payload=bytearray()
    for i,v in enumerate(d['bufferViews']):
        if i in image_views:continue
        mapping[i]=len(views);start=v.get('byteOffset',0);new=dict(v);new['byteOffset']=len(payload)
        payload.extend(binary[start:start+v['byteLength']]);payload.extend(b'\0'*((-len(payload))%4));views.append(new)
    for a in d['accessors']:
        if 'bufferView' in a:a['bufferView']=mapping[a['bufferView']]
        if 'sparse' in a:
            for field in ['indices','values']:a['sparse'][field]['bufferView']=mapping[a['sparse'][field]['bufferView']]
    d['bufferViews']=views;d['buffers'][0]['byteLength']=len(payload)
    j=json.dumps(d,separators=(',',':')).encode();j+=b' '*((-len(j))%4)
    path.write_bytes(struct.pack('<IIIII',0x46546C67,2,28+len(j)+len(payload),len(j),0x4E4F534A)+j+struct.pack('<II',len(payload),0x004E4942)+payload)

def export_glb(objects, path):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects: o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.gltf(filepath=str(path), export_format='GLB', use_selection=True,
        export_animations=False, export_cameras=False, export_lights=False,
        export_yup=True, export_apply=True, export_extras=False)
    raw = path.read_bytes(); n = struct.unpack_from('<I', raw, 12)[0]; d = json.loads(raw[20:20+n])
    for m in d.get('materials', []):
        if m['name'] == 'V3_Leaves': m.update(alphaMode='MASK', alphaCutoff=.45, doubleSided=True)
    j = json.dumps(d, separators=(',', ':')).encode(); j += b' '*((-len(j))%4)
    tail = raw[20+n:]
    path.write_bytes(struct.pack('<IIIII', 0x46546C67, 2, 20+len(j)+len(tail), len(j), 0x4E4F534A)+j+tail)
    share_textures(path)
    # Explicit LOD meshes are selected by the budget controller, never simplified twice.
    path.with_suffix('.glb.import').write_text('[remap]\nimporter="scene"\nimporter_version=1\ntype="PackedScene"\n\n[deps]\nsource_file="res://'+path.relative_to(ROOT).as_posix()+'"\n\n[params]\nmeshes/generate_lods=false\nmeshes/create_shadow_meshes=true\nmaterials/extract=0\nanimation/import=false\n', encoding='utf-8')

images = {}
def colour(material, uv):
    if material is None: return (.6, .6, .6, 1)
    bsdf = next((n for n in material.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None) if material.use_nodes else None
    if bsdf is None: return tuple(material.diffuse_color)
    socket = bsdf.inputs['Base Color']
    if not socket.is_linked: return tuple(socket.default_value)
    tex = socket.links[0].from_node
    if tex.type != 'TEX_IMAGE' or tex.image is None: return tuple(material.diffuse_color)
    img = tex.image
    if img.name not in images:
        w, h = img.size; data = np.empty(w*h*4, dtype=np.float32); img.pixels.foreach_get(data)
        images[img.name] = data.reshape(h, w, 4)
    data = images[img.name]; h, w, _ = data.shape
    # Image pixels are linear; glTF COLOR_0 is linear as well.
    return (*data[int(uv[1]%1*h)%h, int(uv[0]%1*w)%w, :3], 1)

def proxy_material():
    mat = bpy.data.materials.new('V7_ProxyColour'); mat.use_nodes = True
    bsdf = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Roughness'].default_value = .93
    node = mat.node_tree.nodes.new('ShaderNodeVertexColor'); node.layer_name = 'ProxyColour'
    mat.node_tree.links.new(node.outputs['Color'], bsdf.inputs['Base Color'])
    return mat

def simplify(obj, lod, proxy, category):
    bpy.context.view_layer.objects.active = obj
    if lod >= 2 and category == 'building':
        # Replace overlapping tile sheets by a closed envelope at the SAME eaves/ridge.
        # Lower porch roofs remain separate, avoiding a convex wedge across the facade.
        mesh=obj.data;roof_indices={i for i,m in enumerate(mesh.materials) if m and 'roof' in m.name.lower()}
        faces=[p for p in mesh.polygons if p.material_index in roof_indices]
        if faces:
            top=max(mesh.vertices[v].co.z for f in faces for v in f.vertices)
            chosen=[f for f in faces if all(mesh.vertices[v].co.z>=top-1.60 for v in f.vertices)]
            if len(chosen)>24:
                selected={f.index for f in chosen};coords={tuple(mesh.vertices[v].co) for f in chosen for v in f.vertices}
                material=mesh.materials[max(roof_indices,key=lambda i:sum(f.area for f in chosen if f.material_index==i))]
                hull=bmesh.new()
                for c in coords:hull.verts.new(c)
                bmesh.ops.convex_hull(hull,input=list(hull.verts),use_existing_faces=False)
                shell=bpy.data.meshes.new('roof_envelope');hull.to_mesh(shell);hull.free();shell.materials.append(material)
                bm=bmesh.new();bm.from_mesh(mesh);bm.faces.ensure_lookup_table()
                bmesh.ops.delete(bm,geom=[bm.faces[i] for i in selected],context='FACES');bm.to_mesh(mesh);bm.free()
                piece=bpy.data.objects.new('roof_envelope',shell);bpy.context.collection.objects.link(piece)
                bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);piece.select_set(True)
                bpy.context.view_layer.objects.active=obj;bpy.ops.object.join()
    if lod:
        # Planar reduction preserves doors, windows, disconnected alpha cards and road
        # boundaries. Global collapse ratios destroy thin boxes and cause triangular holes.
        mod = obj.modifiers.new('LOD planar reduction', 'DECIMATE');mod.decimate_type='DISSOLVE'
        mod.angle_limit=math.radians([0,3,12,22][lod]);mod.delimit={'MATERIAL'}
        if category=='ground':mod.angle_limit=math.radians(.001)
        mod.use_dissolve_boundaries=False
        bpy.ops.object.modifier_apply(modifier=mod.name)
    if lod >= 2 and category == 'building':
        # Remove entire small disconnected fittings, never individual triangles of a
        # window/door/wall. Tiled roof sheets are exempt (there is no hidden base roof).
        bm=bmesh.new();bm.from_mesh(obj.data);remaining=set(bm.verts);discard=[]
        while remaining:
            seed=remaining.pop();component={seed};stack=[seed]
            while stack:
                v=stack.pop()
                for e in v.link_edges:
                    n=e.other_vert(v)
                    if n in remaining:remaining.remove(n);component.add(n);stack.append(n)
            fs={f for v in component for f in v.link_faces}
            roof=any('roof' in obj.data.materials[f.material_index].name.lower() for f in fs if f.material_index<len(obj.data.materials))
            size=Vector([max(v.co[i] for v in component)-min(v.co[i] for v in component) for i in range(3)]).length
            if not roof and size < (.38 if lod==2 else 1.05):discard.extend(component)
        bmesh.ops.delete(bm,geom=discard,context='VERTS');bm.to_mesh(obj.data);bm.free()
    if lod >= 2 and category == 'tree':
        # Keep complete, spatially distributed alpha cards; simplify only opaque wood.
        # This gives distant crowns a geometric cluster proxy without half-triangle leaves.
        bm=bmesh.new();bm.from_mesh(obj.data)
        leaf_faces=[f for f in bm.faces if obj.data.materials[f.material_index].name=='V3_Leaves']
        remaining=set(v for f in leaf_faces for v in f.verts);cards=[]
        while remaining:
            seed=remaining.pop();component={seed};stack=[seed]
            while stack:
                v=stack.pop()
                for e in v.link_edges:
                    n=e.other_vert(v)
                    if n in remaining:remaining.remove(n);component.add(n);stack.append(n)
            cards.append(component)
        cards.sort(key=lambda vs:tuple(round(sum(v.co[i] for v in vs)/len(vs),4) for i in range(3)))
        stride=2 if lod==2 else 4
        discard=[v for i,vs in enumerate(cards) if i%stride for v in vs]
        bmesh.ops.delete(bm,geom=discard,context='VERTS');bm.to_mesh(obj.data);bm.free()
        # Physically separate leaves: collapse weights are only a preference, not a lock.
        wood=obj.copy();wood.data=obj.data.copy();bpy.context.collection.objects.link(wood)
        for part,remove_leaf in [(wood,True),(obj,False)]:
            bm=bmesh.new();bm.from_mesh(part.data)
            drop=[f for f in bm.faces if (part.data.materials[f.material_index].name=='V3_Leaves')==remove_leaf]
            bmesh.ops.delete(bm,geom=drop,context='FACES');bm.to_mesh(part.data);bm.free()
        bpy.context.view_layer.objects.active=wood
        mod=wood.modifiers.new('Wood reduction','DECIMATE');mod.ratio=.35 if lod==2 else .18
        bpy.ops.object.modifier_apply(modifier=mod.name)
        bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);wood.select_set(True)
        bpy.context.view_layer.objects.active=obj;bpy.ops.object.join()
    mesh = obj.data
    if lod >= 2:
        uv = mesh.uv_layers.active
        layer = mesh.color_attributes.new(name='ProxyColour', type='FLOAT_COLOR', domain='CORNER')
        old = list(mesh.materials)
        leaves = next((m for m in old if m and m.name == 'V3_Leaves'), None)
        indices = []
        for face in mesh.polygons:
            mat = old[face.material_index] if face.material_index < len(old) else None
            is_leaf = mat == leaves and leaves is not None
            indices.append(1 if is_leaf else 0)
            for i in face.loop_indices:
                layer.data[i].color = (1,1,1,1) if is_leaf else colour(mat, uv.data[i].uv if uv else (0,0))
        mesh.materials.clear(); mesh.materials.append(proxy)
        if leaves: mesh.materials.append(leaves)
        for f, i in zip(mesh.polygons, indices): f.material_index = i
    mesh.validate(clean_customdata=False);mesh.update()
    # Empty material slots otherwise create misleading slot budgets.
    used = {f.material_index for f in mesh.polygons}
    for i in reversed(range(len(mesh.materials))):
        if i not in used: mesh.materials.pop(index=i)

def build(version=7, authored_smooth=False):
    assert SOURCE.exists(), 'Run init once; subsequently edit the saved v7 source.'
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE), load_ui=False)
    DEST.mkdir(parents=True, exist_ok=True)
    layout = json.loads(LAYOUT.read_text())
    buildings = layout['geometry']['buildings']
    groups = {}; originals = [];tree_sources={};tree_lods={}
    # Only the saved village collection, excluding lookdev/cameras/characters.
    collection = bpy.data.collections.get('Village')
    if collection is None:
        candidates = [c for c in bpy.data.collections if any(o.name == 'Terrain' for o in c.all_objects)]
        assert candidates, 'Village collection missing'
        collection = candidates[0]
    for obj in list(collection.all_objects):
        if obj.type != 'MESH' or obj.name == 'Terrain': continue
        originals.append(obj)
        lineage = []; parent = obj
        while parent:
            lineage.append(parent.name); parent = parent.parent
        building = next((b for b in buildings if 'Building_'+b['id'] in lineage or b['id']=='home' and 'HomeExterior' in lineage), None)
        # Group props and signs with their nearest building; long roads/banks are split below.
        points = [obj.matrix_world @ Vector(v) for v in obj.bound_box]
        center = sum(points, Vector())/8; x,z = center.x,-center.y
        category = 'tree' if obj.name.startswith('Tree_') or obj.name == 'PlazaTree' else 'building' if building else 'detail'
        if category == 'tree':
            uid = obj.name.lower(); area = district(x,z);tree_sources[uid]=(obj.data.name,obj.matrix_world.copy())
        elif building:
            uid = building['id']; area = district(building['position'][0],building['position'][2])
        elif obj.name.startswith(('Road', 'Stream', 'Bridge', 'Utility', 'OldRail', 'Scenic', 'VillageSquare')):
            # Spatially split huge bounds, making distant road/wire geometry independently cullable.
            uid = 'ground'; area = None; category = 'ground'
        else:
            near = min(buildings, key=lambda b: (x-b['position'][0])**2+(z-b['position'][2])**2)
            uid = near['id']; area = district(near['position'][0],near['position'][2]); category = 'building'
        # Copy evaluated triangles in world space; no transform or collision coordinate changes.
        mesh = bpy.data.meshes.new_from_object(obj.evaluated_get(bpy.context.evaluated_depsgraph_get()))
        if obj.name == 'ScenicMountainTerrain':
            # Distant inaccessible backdrop only, independent of the walkable Terrain3D.
            temp=bpy.data.objects.new('backdrop_reduction',mesh);bpy.context.collection.objects.link(temp)
            bpy.context.view_layer.objects.active=temp
            mod=temp.modifiers.new('Backdrop', 'DECIMATE');mod.ratio=.08
            bpy.ops.object.modifier_apply(modifier=mod.name);mesh=temp.data.copy();bpy.data.objects.remove(temp,do_unlink=True)
        mesh.transform(obj.matrix_world); mesh.calc_loop_triangles()
        bins = {}
        for tri in mesh.loop_triangles:
            c = sum((mesh.vertices[i].co for i in tri.vertices), Vector())/3
            key = (area or district(c.x,-c.y), uid if area else 'ground_'+district(c.x,-c.y), category)
            bins.setdefault(key, []).append(tri)
        for key, triangles in bins.items():
            vs=[]; fs=[]; uvs=[]; mis=[]; smooth=[]
            for tri in triangles:
                start=len(vs); vs.extend(tuple(mesh.vertices[i].co) for i in tri.vertices); fs.append((start,start+1,start+2));mis.append(tri.material_index)
                smooth.append(authored_smooth and mesh.polygons[tri.polygon_index].use_smooth)
                uvs.extend(tuple(mesh.uv_layers.active.data[i].uv) if mesh.uv_layers.active else (0,0) for i in tri.loops)
            data=bpy.data.meshes.new('part');data.from_pydata(vs,[],fs)
            for m in mesh.materials: data.materials.append(m)
            uv=data.uv_layers.new(name='UVMap')
            for i,p in enumerate(uvs):uv.data[i].uv=p
            for f,i,s in zip(data.polygons,mis,smooth):f.material_index=i;f.use_smooth=s
            part=bpy.data.objects.new('part',data);bpy.context.collection.objects.link(part)
            groups.setdefault(key,[]).append(part)
        bpy.data.meshes.remove(mesh)
    assert any(key[1]=='home' for key in groups), 'Home missing from source collection'
    proxy=proxy_material(); units=[]; bundles={}; derived=[]
    for (area,uid,category), parts in sorted(groups.items()):
        bpy.ops.object.select_all(action='DESELECT')
        for o in parts:o.select_set(True)
        bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();base=parts[0]
        # Weld split loop vertices before decimation, retaining UV/material seams.
        bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.remove_doubles(threshold=.00001);bpy.ops.object.mode_set(mode='OBJECT')
        coords=[v.co for v in base.data.vertices]
        lo=[min(v[i] for v in coords) for i in range(3)]; hi=[max(v[i] for v in coords) for i in range(3)]
        unit=dict(id=uid,district=area,category=category,important=uid in ['home','shop','workshop','plazatree'],
                  bounds=[lo[0],lo[2],-hi[1],hi[0]-lo[0],hi[2]-lo[2],hi[1]-lo[1]],lods=[])
        if category=='tree':unit['sharedPrototype']=tree_sources[uid][0]
        for lod in range(4):
            obj=base.copy();obj.data=base.data.copy();bpy.context.collection.objects.link(obj);obj.name=uid+'_lod'+str(lod)
            simplify(obj,lod,proxy,category);derived.append(obj)
            if category=='tree':
                prototype,transform=tree_sources[uid];key=(prototype,lod)
                if key not in tree_lods:
                    obj.data.transform(transform.inverted());tree_lods[key]=obj.data
                else:obj.data=tree_lods[key]
                obj.matrix_world=transform
            bundle=area+('_detail' if lod<2 else '_proxy');bundles.setdefault(bundle,[]).append(obj)
            unit['lods'].append(dict(node=obj.name,bundle=bundle,screenFraction=[.30,.12,.035,0][lod]))
        bpy.data.objects.remove(base,do_unlink=True);units.append(unit)
    files=[]
    for name,objects in bundles.items():
        path=DEST/(name+'.glb');export_glb(objects,path);actual=costs(path)
        for u in units:
            for lod in u['lods']:
                if lod['bundle']==name:lod.update(actual[lod['node']])
        files.append(dict(id=name,path='res://'+path.relative_to(ROOT).as_posix(),sha256=sha(path)))
    validate_units(units)
    manifest=dict(version=version,status='independent-preview',sourceSha256=sha(SOURCE),baselineSha256=sha(BASE),layoutSha256=sha(LAYOUT),
        tool='Blender '+bpy.app.version_string,source='saved v6 derivative; CC0 materials reused; no characters',
        collisionSource='VillageLayoutProfile3D + repaired terrain-v4; visual meshes have no collision',
        districts=DISTRICTS,bundles=files,units=units)
    (DEST/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    print('V'+str(version)+'_EXPORT_PASS units='+str(len(units))+' bundles='+str(len(files)),flush=True)

def check():
    m=json.loads((DEST/'manifest.json').read_text())
    for p,key in [(SOURCE,'sourceSha256'),(BASE,'baselineSha256'),(LAYOUT,'layoutSha256')]:assert sha(p)==m[key],str(p)
    assert len(m['districts'])==7 and len({u['id'] for u in m['units']})==len(m['units'])
    validate_units(m['units'])
    actual={}
    for b in m['bundles']:
        p=ROOT/b['path'].removeprefix('res://');assert sha(p)==b['sha256'];actual[b['id']]=costs(p)
    for u in m['units']:
        assert len(u['bounds'])==6 and all(math.isfinite(v) for v in u['bounds'])
        for level,lod in enumerate(u['lods']):
            c=actual[lod['bundle']][lod['node']]
            assert all(c[k]==lod[k] for k in c),(u['id'],level)
            assert lod['triangles']>0 and (level<2 or lod['surfaces']<=2)
    print('V7_CHECK_PASS readOnly=true units='+str(len(m['units'])),flush=True)

if __name__=='__main__':
    mode=sys.argv[sys.argv.index('--')+1]
    if mode=='init':
        assert not SOURCE.exists(),'Saved v7 already exists; use export after editing.'
        bpy.ops.wm.open_mainfile(filepath=str(BASE),load_ui=False)
        bpy.context.scene['v7BaselineSha256']=sha(BASE)
        bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE));build()
    elif mode=='export':build()
    elif mode=='check':check()
    elif mode=='pack':
        m=json.loads((DEST/'manifest.json').read_text())
        for b in m['bundles']:
            path=ROOT/b['path'].removeprefix('res://');share_textures(path);b['sha256']=sha(path)
        (DEST/'manifest.json').write_text(json.dumps(m,indent=2),encoding='utf-8')
    else:raise ValueError(mode)
