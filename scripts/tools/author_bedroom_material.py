"""Derive once from saved v2; subsequent changes use saved-source export only.

Blender --background --python this_file -- derive|export|check
No room reconstruction, simulation, or character data is involved.
"""
import bpy, math, json, hashlib, sys
from pathlib import Path
from mathutils import Vector, Matrix
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'art/blender/bedroom-material-v1'
DEST = ROOT / 'assets/generated/bedroom-material-v1'
OLD = ROOT / 'art/blender/bedroom-target-v2/bedroom-target-v2.blend'
SAVED = SOURCE / 'bedroom-material-v1.blend'
N = 2048

def node(mat, kind):
    return next(n for n in mat.node_tree.nodes if n.type == kind)

def triangles():
    return sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in bpy.data.objects if o.type == 'MESH')

def components(mesh):
    adjacency = [[] for _ in mesh.vertices]
    for e in mesh.edges:
        a, b = e.vertices; adjacency[a].append(b); adjacency[b].append(a)
    remaining = set(range(len(mesh.vertices)))
    while remaining:
        seed = min(remaining); remaining.remove(seed); group = {seed}; stack = [seed]
        while stack:
            for v in adjacency[stack.pop()]:
                if v in remaining: remaining.remove(v); group.add(v); stack.append(v)
        yield group

def subset(o, indices, name):
    """Copy faces, UVs and weighted corner normals without moving any surface."""
    old = o.data; polygons = [old.polygons[i] for i in sorted(indices)]
    ids = sorted({v for p in polygons for v in p.vertices}); lookup = {v: i for i, v in enumerate(ids)}
    vertices = [tuple(o.matrix_world @ old.vertices[v].co) for v in ids]
    faces = [[lookup[v] for v in p.vertices] for p in polygons]
    mesh = bpy.data.meshes.new(name); mesh.from_pydata(vertices, [], faces); mesh.update()
    for m in old.materials: mesh.materials.append(m)
    uv = mesh.uv_layers.new(name='UVMap')
    normals = []; basis = o.matrix_world.to_3x3().inverted().transposed()
    for out, original in zip(mesh.polygons, polygons):
        out.material_index = original.material_index; out.use_smooth = original.use_smooth
        for a, b in zip(out.loop_indices, original.loop_indices):
            uv.data[a].uv = old.uv_layers.active.data[b].uv
            normals.append(tuple((basis @ old.corner_normals[b].vector).normalized()))
    mesh.normals_split_custom_set(normals)
    return mesh

def separate():
    targets = {'desk': [], 'wardrobe': []}; record = []
    for o in list(bpy.data.objects):
        if o.type != 'MESH' or o.name not in ('Furniture_Wood', 'Furniture_WoodEdge', 'Furniture_WoodDark'): continue
        selected = set()
        for group in components(o.data):
            points = [o.matrix_world @ o.data.vertices[v].co for v in group]
            gd = [(p.x, p.z, -p.y) for p in points]
            lo = [min(p[i] for p in gd) for i in range(3)]; hi = [max(p[i] for p in gd) for i in range(3)]
            kind = None
            if lo[0] > 1.32 and hi[0] < 1.99 and lo[1] > -.01 and hi[1] < .81 and lo[2] > -1.16 and hi[2] < .59: kind = 'desk'
            if lo[0] > -1.01 and hi[0] < .26 and lo[1] > .02 and hi[1] < 2.23 and lo[2] > -2.28 and hi[2] < -1.59: kind = 'wardrobe'
            if not kind: continue
            faces = {p.index for p in o.data.polygons if p.vertices[0] in group}
            mesh = subset(o, faces, kind + '-board')
            targets[kind].append(mesh); selected.update(faces)
            record.append({'kind': kind, 'sourceMesh': o.name, 'minGodot': lo, 'maxGodot': hi, 'faces': len(faces)})
        if selected:
            mesh = subset(o, set(range(len(o.data.polygons))) - selected, o.name + '-remaining')
            o.data = mesh; o.matrix_world = Matrix.Identity(4)
    objects = {}
    for kind, meshes in targets.items():
        if len(meshes) < 10: raise RuntimeError('Incomplete furniture grouping: ' + kind)
        verts = []; faces = []; olduv = []; normals = []; smooth = []; material_ids = []; materials = []; board_ids = []
        for board, mesh in enumerate(meshes):
            start = len(verts); verts.extend(tuple(v.co) for v in mesh.vertices)
            slots = []
            for m in mesh.materials:
                if m not in materials: materials.append(m)
                slots.append(materials.index(m))
            for p in mesh.polygons:
                faces.append([start + v for v in p.vertices]); board_ids.append(board)
                smooth.append(p.use_smooth); material_ids.append(slots[p.material_index])
                for li in p.loop_indices:
                    olduv.append(tuple(mesh.uv_layers.active.data[li].uv)); normals.append(tuple(mesh.corner_normals[li].vector))
        mesh = bpy.data.meshes.new('Furniture_' + kind.title()); mesh.from_pydata(verts, [], faces); mesh.update()
        for m in materials: mesh.materials.append(m)
        mesh.normals_split_custom_set(normals)
        for layername in ('UVMap', 'OriginalUV', 'WoodSurface'):
            uv = mesh.uv_layers.new(name=layername)
            for li, p in enumerate(olduv): uv.data[li].uv = p
        for p, mi, sm in zip(mesh.polygons, material_ids, smooth): p.material_index = mi; p.use_smooth = sm
        attr = mesh.attributes.new('wood_board', 'INT', 'FACE')
        for d, v in zip(attr.data, board_ids): d.value = v
        obj = bpy.data.objects.new(mesh.name, mesh); bpy.context.collection.objects.link(obj); objects[kind] = obj
    return objects, record

def atlas(o, kind):
    mesh = o.data; boards = mesh.attributes['wood_board'].data
    boardverts = {}; charts = {}
    for p in mesh.polygons:
        board = boards[p.index].value
        boardverts.setdefault(board, set()).update(p.vertices)
        axis = max(range(3), key=lambda a: abs(p.normal[a])); sign = 1 if p.normal[axis] > 0 else -1
        charts.setdefault((board, axis, sign), []).append(p)
    boxes = {}
    for board, ids in boardverts.items():
        lo = [min(mesh.vertices[i].co[a] for i in ids) for a in range(3)]
        hi = [max(mesh.vertices[i].co[a] for i in ids) for a in range(3)]
        boxes[board] = (lo, hi, max(range(3), key=lambda a: hi[a]-lo[a]))
    specs = []
    for key, polygons in charts.items():
        board, axis, sign = key; lo, hi, main = boxes[board]
        axes = [a for a in range(3) if a != axis]
        long = main if main in axes else axes[1]; short = next(a for a in axes if a != long)
        density = 256 if axis == 2 and sign < 0 else 512
        if kind == 'wardrobe':
            outer_side = axis == 0 and hi[2]-lo[2] > 1.8 and hi[1]-lo[1] > .4
            density = 448 if axis == 1 and sign < 0 or outer_side else 224
        w = max(2, math.ceil((hi[short]-lo[short])*density)); h = max(2, math.ceil((hi[long]-lo[long])*density))
        specs.append((w+32, h+32, key, short, long, density, w, h))
    # Atlas islands may rotate; the separate physical WoodSurface UV keeps grain along the board.
    free = [(0, 0, N, N)]; result = []
    print('ATLAS_AREA',kind,len(specs),sum(s[0]*s[1] for s in specs),flush=True)
    for rw, rh, key, short, long, density, w, h in sorted(specs, key=lambda s: (-max(s[0], s[1]), -s[0]*s[1], s[2])):
        choices = [(min(fw-a, fh-b), fw*fh-a*b, x, y, rotated) for x, y, fw, fh in free
                   for a,b,rotated in ((rw,rh,False),(rh,rw,True)) if fw >= a and fh >= b]
        if not choices: raise RuntimeError(f'Atlas exceeds 2048: {kind} rect={rw,rh,key}, placed={len(result)}')
        _, _, x, y, rotated = min(choices)
        if rotated: rw,rh=rh,rw
        nextfree = []
        for fx, fy, fw, fh in free:
            if x >= fx+fw or x+rw <= fx or y >= fy+fh or y+rh <= fy: nextfree.append((fx,fy,fw,fh)); continue
            if x > fx: nextfree.append((fx,fy,x-fx,fh))
            if x+rw < fx+fw: nextfree.append((x+rw,fy,fx+fw-x-rw,fh))
            if y > fy: nextfree.append((fx,fy,fw,y-fy))
            if y+rh < fy+fh: nextfree.append((fx,y+rh,fw,fy+fh-y-rh))
        free = [f for i,f in enumerate(nextfree) if not any(i != j and f[0] >= g[0] and f[1] >= g[1] and f[0]+f[2] <= g[0]+g[2] and f[1]+f[3] <= g[1]+g[3] and (f != g or j < i) for j,g in enumerate(nextfree))]
        board = key[0]; lo, hi, main = boxes[board]
        for p in charts[key]:
            for li in p.loop_indices:
                co = mesh.vertices[mesh.loops[li].vertex_index].co
                u=(co[short]-lo[short])*density;v=(co[long]-lo[long])*density
                mesh.uv_layers['UVMap'].data[li].uv = ((x+16+(v if rotated else u))/N, (y+16+(u if rotated else v))/N)
                # The upstream scan covers 1.8 m; deterministic board offsets avoid repeated grain.
                mesh.uv_layers['WoodSurface'].data[li].uv = ((co[short]-lo[short])/1.8 + board*.137, (co[long]-lo[long])/1.8 + board*.271)
        result.append({'board': board, 'faceAxis': key[1], 'faceSign': key[2], 'density': density, 'rotated':rotated,'rect': [x,y,rw,rh]})
    mesh.uv_layers.active = mesh.uv_layers['UVMap']; mesh.uv_layers['UVMap'].active_render = True
    return result

def texture_node(mat, path, uvname, noncolor=False):
    nodes = mat.node_tree.nodes; uv = nodes.new('ShaderNodeUVMap'); uv.uv_map = uvname
    tex = nodes.new('ShaderNodeTexImage'); tex.image = bpy.data.images.load(str(path), check_existing=True)
    tex.image.colorspace_settings.name = 'Non-Color' if noncolor else 'sRGB'
    mat.node_tree.links.new(uv.outputs['UV'], tex.inputs['Vector']); return tex

def wood_material(kind):
    m = bpy.data.materials.new('Wood bake input ' + kind); m.use_nodes = True
    bs = node(m, 'BSDF_PRINCIPLED'); bs.inputs['Metallic'].default_value = 0
    color = texture_node(m, SOURCE/'inputs'/f'{kind}-color.png', 'WoodSurface')
    m.node_tree.links.new(color.outputs['Color'], bs.inputs['Base Color'])
    arm = texture_node(m, SOURCE/'inputs'/f'{kind}-orm.png', 'WoodSurface', True)
    separate = m.node_tree.nodes.new('ShaderNodeSeparateColor'); m.node_tree.links.new(arm.outputs['Color'], separate.inputs[0])
    m.node_tree.links.new(separate.outputs['Green'], bs.inputs['Roughness'])
    tex = texture_node(m, SOURCE/'inputs/wood-normal.png', 'WoodSurface', True)
    normal = m.node_tree.nodes.new('ShaderNodeNormalMap'); normal.uv_map = 'WoodSurface'; normal.inputs['Strength'].default_value = .15
    m.node_tree.links.new(tex.outputs['Color'], normal.inputs['Color']); m.node_tree.links.new(normal.outputs['Normal'], bs.inputs['Normal'])
    return m

def bake(o, name, channel):
    image = bpy.data.images.get(name) or bpy.data.images.new(name, width=N, height=N, alpha=False)
    image.colorspace_settings.name = 'sRGB' if channel == 'color' else 'Non-Color'
    restores = []
    for mat in o.data.materials:
        nodes = mat.node_tree.nodes; output = node(mat, 'OUTPUT_MATERIAL'); bs = node(mat, 'BSDF_PRINCIPLED')
        target = nodes.new('ShaderNodeTexImage'); target.image = image; nodes.active = target
        if channel == 'roughness':
            original = output.inputs['Surface'].links[0].from_socket
            emit = nodes.new('ShaderNodeEmission'); source = bs.inputs['Roughness']
            if source.is_linked: mat.node_tree.links.new(source.links[0].from_socket, emit.inputs['Color'])
            else: emit.inputs['Color'].default_value = (source.default_value,)*3+(1,)
            mat.node_tree.links.new(emit.outputs[0], output.inputs['Surface']); restores.append((mat, output, original, emit, target))
        else: restores.append((mat, output, None, None, target))
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    scene = bpy.context.scene; scene.render.engine = 'CYCLES'; scene.cycles.samples = 1
    scene.render.bake.margin = 16; scene.render.bake.use_pass_direct = False; scene.render.bake.use_pass_indirect = False
    scene.render.bake.use_pass_color = True; scene.render.bake.normal_space = 'TANGENT'; scene.render.bake.use_clear = True
    bpy.ops.object.bake(type={'color':'DIFFUSE','roughness':'EMIT','normal':'NORMAL'}[channel])
    image.filepath_raw = str(SOURCE/'textures'/f'{name}.png'); image.file_format='PNG'; image.save()
    # Repack updated bytes: pack() alone keeps a previously packed, stale image.
    if image.packed_file: image.unpack(method='REMOVE')
    image.pack()
    for mat, output, socket, emit, target in restores:
        if socket: mat.node_tree.links.new(socket, output.inputs['Surface']); mat.node_tree.nodes.remove(emit)
        mat.node_tree.nodes.remove(target)
    print('WOOD_BAKE_PASS', name, flush=True); return image

def packed_rough(image, name):
    values = np.empty(N*N*4, dtype=np.float32); image.pixels.foreach_get(values); values = values.reshape(N,N,4)
    pixels = np.empty_like(values); pixels[:,:,0] = 1; pixels[:,:,1] = values[:,:,0]; pixels[:,:,2] = 0; pixels[:,:,3] = 1
    packed = bpy.data.images.get(name) or bpy.data.images.new(name,width=N,height=N,alpha=False); packed.colorspace_settings.name='Non-Color'
    packed.pixels.foreach_set(pixels.ravel()); packed.filepath_raw=str(SOURCE/'textures'/f'{name}.png'); packed.file_format='PNG'; packed.save()
    if packed.packed_file: packed.unpack(method='REMOVE')
    packed.pack(); return packed

def final_material(name, color, orm, normal=None):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name); m.use_nodes=True; m.use_backface_culling=True
    m.node_tree.nodes.clear()
    bs=m.node_tree.nodes.new('ShaderNodeBsdfPrincipled');output=m.node_tree.nodes.new('ShaderNodeOutputMaterial')
    m.node_tree.links.new(bs.outputs[0],output.inputs['Surface'])
    bs = node(m, 'BSDF_PRINCIPLED'); bs.inputs['Metallic'].default_value=0
    for image, socket in [(color,'Base Color')]:
        tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image;m.node_tree.links.new(tex.outputs['Color'],bs.inputs[socket])
    tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=orm
    split=m.node_tree.nodes.new('ShaderNodeSeparateColor');m.node_tree.links.new(tex.outputs['Color'],split.inputs[0])
    m.node_tree.links.new(split.outputs['Green'],bs.inputs['Roughness']);m.node_tree.links.new(split.outputs['Blue'],bs.inputs['Metallic'])
    if normal:
        tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=normal
        n=m.node_tree.nodes.new('ShaderNodeNormalMap');n.uv_map='UVMap';m.node_tree.links.new(tex.outputs['Color'],n.inputs['Color']);m.node_tree.links.new(n.outputs[0],bs.inputs['Normal'])
    return m

def derive():
    if SAVED.exists(): raise RuntimeError('Refusing to overwrite saved material source; use export after editing.')
    (SOURCE/'textures').mkdir(parents=True, exist_ok=True); bpy.ops.wm.open_mainfile(filepath=str(OLD))
    before=triangles(); objects, boards=separate(); layouts={}
    for kind,o in objects.items():
        layouts[kind]=atlas(o,kind)
        # Bind the original image explicitly to the retained UV before baking it into the new atlas.
        for i,m in enumerate(list(o.data.materials)):
            m=m.copy();o.data.materials[i]=m
            for tex in list(m.node_tree.nodes):
                if tex.type=='TEX_IMAGE':
                    uv=m.node_tree.nodes.new('ShaderNodeUVMap');uv.uv_map='OriginalUV';m.node_tree.links.new(uv.outputs[0],tex.inputs['Vector'])
        oldcolor=bake(o,kind+'-old-color','color');oldrough=bake(o,kind+'-old-roughness','roughness')
        oldorm=packed_rough(oldrough,kind+'-old-orm')
        baseline=final_material(kind.title()+'Baseline',oldcolor,oldorm);baseline.use_fake_user=True
        m=wood_material(kind);o.data.materials.clear();o.data.materials.append(m)
        for p in o.data.polygons:p.material_index=0
        color=bake(o,kind+'-color','color');rough=bake(o,kind+'-roughness','roughness');normal=bake(o,kind+'-normal','normal')
        orm=packed_rough(rough,kind+'-orm');m=final_material(kind.title()+'Refined',color,orm,normal)
        o.data.materials.clear();o.data.materials.append(m)
        for p in o.data.polygons:p.material_index=0
    if triangles()!=before: raise RuntimeError('Geometry changed during material extraction')
    record={'date':'2026-10-06','tool':'Blender '+bpy.app.version_string+' / Cycles texture bake',
        'baseSource':str(OLD.relative_to(ROOT)),'baseSourceSha256':hashlib.sha256(OLD.read_bytes()).hexdigest(),
        'trianglesBefore':before,'trianglesAfter':triangles(),'boards':boards,'atlasLayouts':layouts,
        'oldMaterialBake':'Diffuse color only on OriginalUV; old roughness baked independently; no lighting',
        'materialBake':'CC0 oak_veneer_01, source dimension 1.8m; tangent normal strength 0.15; ORM R=1,G=roughness,B=0',
        'newAIImages':False,'characters':False,'formalAdoption':False,'visualApproval':False}
    (SOURCE/'provenance.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf8')
    bpy.ops.wm.save_as_mainfile(filepath=str(SAVED));export()

def export():
    bpy.ops.wm.open_mainfile(filepath=str(SAVED));DEST.mkdir(parents=True,exist_ok=True)
    source_triangles=triangles()
    # Aux UVs are retained only in Blender for reproducible baking. Godot reserves UV2 for GI.
    for o in bpy.data.objects:
        if o.type=='MESH':
            for uv in list(o.data.uv_layers):
                if uv.name!='UVMap':o.data.uv_layers.remove(uv)
    bpy.ops.export_scene.gltf(filepath=str(DEST/'bedroom-material-v1.glb'),export_format='GLB',export_yup=True,
        export_animations=False,export_cameras=False,export_lights=False,export_materials='EXPORT',export_image_format='AUTO')
    for name in ('desk-old-color','desk-old-orm','wardrobe-old-color','wardrobe-old-orm'):
        (DEST/(name+'.png')).write_bytes((SOURCE/'textures'/(name+'.png')).read_bytes())
    if (SOURCE/'inputs/uv-checker.png').exists():
        (DEST/'uv-checker.png').write_bytes((SOURCE/'inputs/uv-checker.png').read_bytes())
    report={'source':str(SAVED.relative_to(ROOT)),'sourceSha256':hashlib.sha256(SAVED.read_bytes()).hexdigest(),
        'triangles':triangles(),'sourceTriangles':source_triangles,'meshObjects':sum(o.type=='MESH' for o in bpy.data.objects),
        'vertices':sum(len(o.data.vertices) for o in bpy.data.objects if o.type=='MESH'),
        'baseTriangles':454362,'characters':0,'runtimeClothSimulation':False,'formalAdoption':False,'visualApproval':False}
    (DEST/'bedroom-material-v1-manifest.json').write_text(json.dumps(report,indent=2),encoding='utf8')
    print('BEDROOM_MATERIAL_EXPORT_PASS',json.dumps(report),flush=True)

def check():
    bpy.ops.wm.open_mainfile(filepath=str(SAVED))
    record=json.loads((SOURCE/'provenance.json').read_text(encoding='utf8'))
    assert triangles()==record['trianglesBefore']==record['trianglesAfter']
    assert not any(o.type=='ARMATURE' for o in bpy.data.objects)
    assert not any(m.type=='CLOTH' for o in bpy.data.objects for m in o.modifiers)
    for kind in ('Desk','Wardrobe'):
        o=bpy.data.objects['Furniture_'+kind]
        assert len(o.data.materials)==1 and o.data.materials[0].name==kind+'Refined'
        assert 'OriginalUV' in o.data.uv_layers and 'WoodSurface' in o.data.uv_layers
        for tex in o.data.materials[0].node_tree.nodes:
            if tex.type=='TEX_IMAGE':
                expected='sRGB' if tex.image.name.endswith('-color') else 'Non-Color'
                assert tex.image.colorspace_settings.name==expected, (tex.image.name,expected)
        rectangles=[c['rect'] for c in record['atlasLayouts'][kind.lower()]]
        for i,(x,y,w,h) in enumerate(rectangles):
            assert x>=0 and y>=0 and x+w<=N and y+h<=N
            for a,b,c,d in rectangles[i+1:]:
                assert x+w<=a or a+c<=x or y+h<=b or b+d<=y, 'Overlapping atlas islands'
    for image in bpy.data.images:
        path=SOURCE/'textures'/(image.name+'.png')
        if path.exists():
            assert image.packed_file and bytes(image.packed_file.data)==path.read_bytes(), 'Stale packed atlas: '+image.name
    print('BEDROOM_MATERIAL_SOURCE_CHECK_PASS')

def refresh_packed():
    """Saved-source repair only: repack atlas PNGs after rebaking reused images."""
    bpy.ops.wm.open_mainfile(filepath=str(SAVED))
    revision=SOURCE/'revisions/before-repack.blend';revision.parent.mkdir(exist_ok=True)
    if revision.exists(): raise RuntimeError('Repack repair already recorded; edit saved source instead.')
    revision.write_bytes(SAVED.read_bytes())
    for image in bpy.data.images:
        path=SOURCE/'textures'/(image.name+'.png')
        if not path.exists():continue
        if image.packed_file:image.unpack(method='REMOVE')
        image.filepath=str(path);image.reload();image.pack()
        assert bytes(image.packed_file.data)==path.read_bytes(), image.name
        print('PACKED_ATLAS_REFRESHED',image.name,flush=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(SAVED))

def edit_desk():
    """Edit the saved derivative, retaining it first; never reload or reconstruct from v2."""
    bpy.ops.wm.open_mainfile(filepath=str(SAVED))
    revision=SOURCE/'revisions/before-desktop.blend';revision.parent.mkdir(exist_ok=True)
    if revision.exists():
        if hashlib.sha256(revision.read_bytes()).digest()!=hashlib.sha256(SAVED.read_bytes()).digest():
            raise RuntimeError('Desktop source edit already saved; edit the saved source instead.')
    else:revision.write_bytes(SAVED.read_bytes())
    print('DESKTOP_EDIT_BEGIN_SAVED_SOURCE',flush=True)
    record=json.loads((SOURCE/'provenance.json').read_text(encoding='utf8'))
    o=bpy.data.objects['Furniture_Desk'];oldmaterials=[r['sourceMesh'].removeprefix('Furniture_') for r in record['boards'] if r['kind']=='desk']
    source=bpy.data.objects['Furniture_Wood'];selected=set()
    for group in components(source.data):
        gd=[(p.x,p.z,-p.y) for p in (source.matrix_world@source.data.vertices[v].co for v in group)]
        lo=[min(p[i] for p in gd) for i in range(3)];hi=[max(p[i] for p in gd) for i in range(3)]
        if lo[0]>1.32 and hi[0]<1.99 and lo[1]>.70 and hi[1]<.81 and lo[2]>-1.16 and hi[2]<.59:
            selected.update(p.index for p in source.data.polygons if p.vertices[0] in group)
            record['boards'].append({'kind':'desk','sourceMesh':'Furniture_Wood','minGodot':lo,'maxGodot':hi,'faces':len(selected)})
    if not selected:raise RuntimeError('Expected saved-source desktop component missing')
    print('DESKTOP_FACES',len(selected),flush=True)
    topmesh=subset(source,selected,'DesktopSource');top=bpy.data.objects.new('DesktopSource',topmesh);bpy.context.collection.objects.link(top)
    for name in ('OriginalUV','WoodSurface'):
        layer=topmesh.uv_layers.new(name=name)
        for a,b in zip(layer.data,topmesh.uv_layers['UVMap'].data):a.uv=b.uv
    board=topmesh.attributes.new('wood_board','INT','FACE')
    for d in board.data:d.value=len(oldmaterials)
    oldmaterials.append('Wood')
    source.data=subset(source,set(range(len(source.data.polygons)))-selected,'Furniture_Wood-remaining');source.matrix_world=Matrix.Identity(4)
    print('DESKTOP_JOIN_BEGIN',flush=True)
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);top.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.join()
    print('DESKTOP_JOIN_COMPLETE',flush=True)
    slots=[]
    for name in ('Wood','WoodEdge','WoodDark'):
        m=bpy.data.materials[name].copy()
        for tex in list(m.node_tree.nodes):
            if tex.type=='TEX_IMAGE':
                uv=m.node_tree.nodes.new('ShaderNodeUVMap');uv.uv_map='OriginalUV';m.node_tree.links.new(uv.outputs[0],tex.inputs['Vector'])
        slots.append(m)
    o.data.materials.clear()
    for m in slots:o.data.materials.append(m)
    for p in o.data.polygons:p.material_index=('Wood','WoodEdge','WoodDark').index(oldmaterials[o.data.attributes['wood_board'].data[p.index].value])
    record['atlasLayouts']['desk']=atlas(o,'desk')
    color=bake(o,'desk-old-color','color');rough=bake(o,'desk-old-roughness','roughness');orm=packed_rough(rough,'desk-old-orm')
    final_material('DeskBaseline',color,orm).use_fake_user=True
    o.data.materials.clear();o.data.materials.append(wood_material('desk'))
    for p in o.data.polygons:p.material_index=0
    color=bake(o,'desk-color','color');rough=bake(o,'desk-roughness','roughness');normal=bake(o,'desk-normal','normal');orm=packed_rough(rough,'desk-orm')
    o.data.materials.clear();o.data.materials.append(final_material('DeskRefined',color,orm,normal))
    for p in o.data.polygons:p.material_index=0
    assert triangles()==record['trianglesBefore']
    record['desktopEdit']='Saved-source edit; original complete v2 and first derivative retained; desktop component added to Desk'
    (SOURCE/'provenance.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf8')
    bpy.ops.wm.save_as_mainfile(filepath=str(SAVED));export()

if __name__=='__main__':
    command=sys.argv[sys.argv.index('--')+1]
    {'derive':derive,'export':export,'check':check,'edit-desk':edit_desk,'refresh-packed':refresh_packed}[command]()
