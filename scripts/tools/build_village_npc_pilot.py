"""Original procedural pilot characters. Blender: init/export/check, never overwrite a saved source in init."""
import hashlib
import json
import pathlib
import sys

import bpy

ROOT = pathlib.Path(__file__).resolve().parents[2]
OUT = ROOT / 'assets/generated/village/npc-pilot'
SOURCE = ROOT / 'art/blender/village/npc-pilot.blend'


def material():
    mat = bpy.data.materials.new('NpcPilot_VertexColors')
    mat.use_nodes = True
    tree = mat.node_tree
    color = tree.nodes.new('ShaderNodeVertexColor')
    color.layer_name = 'Color'
    shader = tree.nodes.get('Principled BSDF')
    tree.links.new(color.outputs['Color'], shader.inputs['Base Color'])
    shader.inputs['Roughness'].default_value = .9
    return mat


def make_character(name, mat):
    qiao = name == 'aunt_qiao'
    arm = bpy.data.armatures.new(name + '_bones')
    rig = bpy.data.objects.new(name, arm)
    bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    joints = {'root': ((0, 0, .85), (0, 0, 1.12)),
              'head': ((0, 0, 1.36), (0, 0, 1.6)),
              'arm_l': ((-.25, 0, 1.29), (-.29, 0, .9)),
              'arm_r': ((.25, 0, 1.29), (.29, 0, .9)),
              'leg_l': ((-.12, 0, .83), (-.12, 0, .14)),
              'leg_r': ((.12, 0, .83), (.12, 0, .14))}
    for bone_name, (head, tail) in joints.items():
        bone = arm.edit_bones.new(bone_name)
        bone.head, bone.tail = head, tail
        if bone_name != 'root':
            bone.parent = arm.edit_bones['root']
    bpy.ops.object.mode_set(mode='OBJECT')
    skin, hair = (.64, .41, .29, 1), (.045, .032, .026, 1)
    shirt = (.24, .36, .29, 1) if qiao else (.5, .23, .13, 1)
    pants = (.07, .095, .12, 1)
    for lod in (0, 1):
        pieces = []

        def piece(label, center, scale, color, bone='root', sphere=False):
            bpy.ops.object.select_all(action='DESELECT')
            if sphere:
                bpy.ops.mesh.primitive_uv_sphere_add(segments=12 if lod == 0 else 8,
                                                   ring_count=8 if lod == 0 else 4, location=center)
            else:
                bpy.ops.mesh.primitive_cube_add(size=2, location=center)
            obj = bpy.context.object
            obj.name = label
            obj.scale = scale
            bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
            obj.data.materials.append(mat)
            attr = obj.data.color_attributes.new(name='Color', type='FLOAT_COLOR', domain='CORNER')
            for entry in attr.data:
                entry.color = color
            group = obj.vertex_groups.new(name=bone)
            group.add(list(range(len(obj.data.vertices))), 1, 'REPLACE')
            pieces.append(obj)

        piece('shirt', (0, 0, 1.08), (.245, .14, .28), shirt, sphere=True)
        piece('neck', (0, 0, 1.39), (.065, .07, .075), skin, 'head')
        piece('face', (0, -.025, 1.55), (.13, .12, .165), skin, 'head', True)
        piece('hair', (0, .017, 1.64), (.137, .126, .09), hair, 'head', True)
        if qiao:
            piece('bun', (0, .135, 1.62), (.07, .07, .075), hair, 'head', True)
            piece('apron', (0, -.146, 1.015), (.19, .018, .22), (.64, .56, .4, 1))
            piece('apron_pocket', (0, -.17, .95), (.08, .008, .055), (.48, .41, .28, 1))
        else:
            piece('collar', (0, -.135, 1.29), (.065, .02, .07), (.78, .71, .57, 1))
        for sign, side in ((-1, 'l'), (1, 'r')):
            piece('sleeve', (sign * .28, 0, 1.16), (.072, .09, .17), shirt, 'arm_' + side, True)
            piece('hand', (sign * .29, -.015, .93), (.05, .055, .085), skin, 'arm_' + side, True)
            piece('trousers', (sign * .12, 0, .46), (.095, .115, .35), pants, 'leg_' + side)
            piece('shoe', (sign * .12, -.055, .085), (.1, .18, .085), (.04, .033, .027, 1), 'leg_' + side)
            piece('eye', (sign * .046, -.143, 1.565), (.012, .008, .014), hair, 'head', True)
        bpy.ops.object.select_all(action='DESELECT')
        for obj in pieces:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = pieces[0]
        bpy.ops.object.join()
        mesh = bpy.context.object
        mesh.name = f'{name}_LOD{lod}'
        mesh.parent = rig
        modifier = mesh.modifiers.new('Skin', 'ARMATURE')
        modifier.object = rig
        mesh.data.calc_loop_triangles()
        assert len(mesh.data.loop_triangles) <= (8000 if lod == 0 else 2000)
    rig.select_set(False)


def export_assets():
    OUT.mkdir(parents=True, exist_ok=True)
    records = []
    for name in ('aunt_qiao', 'shen_yan'):
        rig = bpy.data.objects[name]
        bpy.ops.object.select_all(action='DESELECT')
        rig.select_set(True)
        meshes = [obj for obj in rig.children if obj.type == 'MESH']
        for obj in meshes:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = rig
        path = OUT / (name + '.glb')
        bpy.ops.export_scene.gltf(filepath=str(path), export_format='GLB', use_selection=True,
                                  export_animations=False, export_yup=True)
        costs = []
        for obj in meshes:
            obj.data.calc_loop_triangles()
            costs.append({'name': obj.name, 'triangles': len(obj.data.loop_triangles),
                          'surfaces': len(obj.data.materials)})
        records.append({'id': name, 'path': 'res://' + str(path.relative_to(ROOT)).replace('\\', '/'),
                        'sha256': hashlib.sha256(path.read_bytes()).hexdigest(), 'lods': costs})
    manifest = {'status': 'independent-npc-pilot', 'runtimeDefault': False,
                'source': str(SOURCE.relative_to(ROOT)).replace('\\', '/'),
                'sourceSha256': hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
                'provenance': {'tool': 'Blender ' + bpy.app.version_string,
                               'method': 'Original deterministic geometry and six-bone rigid weights; runtime procedural poses',
                               'date': '2026-09-16', 'externalAssets': [], 'aiImages': False}, 'characters': records}
    (OUT / 'manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')


def check():
    manifest = json.loads((OUT / 'manifest.json').read_text(encoding='utf-8'))
    assert hashlib.sha256(SOURCE.read_bytes()).hexdigest() == manifest['sourceSha256']
    for entry in manifest['characters']:
        path = ROOT / entry['path'][6:]
        assert hashlib.sha256(path.read_bytes()).hexdigest() == entry['sha256']
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.gltf(filepath=str(path))
        meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH' and o.name.startswith(entry['id'] + '_LOD')]
        assert len(meshes) == 2
        for obj in meshes:
            obj.data.calc_loop_triangles()
            record = next(l for l in entry['lods'] if l['name'] == obj.name)
            assert len(obj.data.loop_triangles) == record['triangles']
            assert record['triangles'] <= (8000 if obj.name.endswith('LOD0') else 2000)
            assert len(obj.data.materials) <= 2
            assert obj.vertex_groups and any(m.type == 'ARMATURE' for m in obj.modifiers)
        assert len(next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE').data.bones) == 6
    print('VILLAGE_NPC_ASSET_CHECK_PASS characters=2 lods=4 readOnly=true')


args = sys.argv[sys.argv.index('--') + 1:]
mode = args[0]
if mode == 'init':
    if SOURCE.exists():
        raise RuntimeError('Source exists; use export after editing the saved source.')
    bpy.ops.wm.read_factory_settings(use_empty=True)
    shared = material()
    for name in ('aunt_qiao', 'shen_yan'):
        make_character(name, shared)
    SOURCE.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
    export_assets()
elif mode == 'export':
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    export_assets()
elif mode == 'check':
    check()
else:
    raise ValueError(mode)
