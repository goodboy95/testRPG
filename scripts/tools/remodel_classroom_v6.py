"""Produce the approved twelve-desk classroom from the preserved v5 source.

Blender --background --python this_file -- graybox|build|export|check --root .
Layout positions come from ClassroomLayout3D.cs. Never writes the v5 Blend.
"""
from __future__ import annotations
import argparse
import json
import math
from pathlib import Path
import re
import sys
import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_classroom_art_pass_v4 as geo
import export_environment_cinematic_samples as exporter
from expand_environment_cinematic_full import MeshAccumulator

SOURCE = Path('art/blender/dream_classroom/classroom-art-pass-v6.blend')
GLB = Path('assets/generated/dream_escape/3d/classroom-art-pass-v6.glb')
MANIFEST = GLB.with_name(GLB.stem + '-manifest.json')
SEED = SOURCE.with_name('classroom-art-pass-v5.blend')


def layout(root):
    source = (root / 'scripts/presentation/ClassroomLayout3D.cs').read_text('utf-8')
    def array(name):
        return [float(n.rstrip('f')) for n in re.search(name + r' = \[([^]]+)\]', source)[1].split(',')]
    return [(x, z) for z in array('DeskRows') for x in array('DeskColumns')]


def dimension(name):
    source=(root/'scripts/presentation/ClassroomLayout3D.cs').read_text('utf-8')
    return float(re.search(name+r' = ([0-9.]+)f;',source)[1])


def remove(objects):
    for obj in set(objects):
        if obj.name in bpy.data.objects:
            bpy.data.objects.remove(obj, do_unlink=True)


def drop_roots(prefixes):
    selected = [o for o in bpy.data.objects if o.name.startswith(prefixes)]
    remove(o for parent in selected for o in exporter.descendants(parent))


def project_uv(obj, density=1.0):
    mesh = obj.data
    uv = mesh.uv_layers.active or mesh.uv_layers.new(name='UVMap')
    for face in mesh.polygons:
        normal = face.normal
        axis = max(range(3), key=lambda k: abs(normal[k]))
        axes = [(1, 2), (0, 2), (0, 1)][axis]
        for loop in face.loop_indices:
            co = obj.matrix_world @ mesh.vertices[mesh.loops[loop].vertex_index].co
            uv.data[loop].uv = (co[axes[0]] * density, co[axes[1]] * density)


def finish(parts, name, parent=None):
    obj = geo.join(parts, name, parent)
    project_uv(obj)
    return obj


def box(name, pos, size, mat, parent=None, bevel=.008):
    return geo.box(name, pos, size, collection, mat, bevel, parent)


def rod(name, start, end, radius, mat, parent=None):
    acc = MeshAccumulator()
    def convert(p): return (p[0], -p[2], p[1])
    acc.add_rod(convert(start), convert(end), radius, 12)
    obj = acc.finish(name, mat, collection, parent, bevel=0)
    return obj


def material(name, color, roughness=.8, metallic=0, asset=None, normal_strength=.25, diffuse=True):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.node_tree.nodes.clear()
    bsdf = mat.node_tree.nodes.new('ShaderNodeBsdfPrincipled')
    output = mat.node_tree.nodes.new('ShaderNodeOutputMaterial')
    mat.node_tree.links.new(bsdf.outputs['BSDF'], output.inputs['Surface'])
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Roughness'].default_value = roughness
    bsdf.inputs['Metallic'].default_value = metallic
    if asset:
        directory = root / 'art/sources/cc0/polyhaven/environment-cinematic-v1' / asset
        for suffix, socket in [('diff', 'Base Color'), ('nor_gl', None)]:
            if socket and not diffuse: continue
            paths = list(directory.glob(f'*_{suffix}_2k.*'))
            if not paths: continue
            image = bpy.data.images.load(str(paths[0]), check_existing=True)
            node = mat.node_tree.nodes.new('ShaderNodeTexImage')
            node.image = image
            if socket:
                mat.node_tree.links.new(node.outputs['Color'], bsdf.inputs[socket])
            else:
                image.colorspace_settings.name = 'Non-Color'
                bump = mat.node_tree.nodes.new('ShaderNodeNormalMap')
                bump.inputs['Strength'].default_value = normal_strength
                mat.node_tree.links.new(node.outputs['Color'], bump.inputs['Color'])
                mat.node_tree.links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
        arm_paths=list(directory.glob('*_arm_2k.*'))
        if arm_paths:
            orm=mat.node_tree.nodes.new('ShaderNodeTexImage')
            orm.image=bpy.data.images.load(str(arm_paths[0]),check_existing=True)
            orm.image.colorspace_settings.name='Non-Color'
            channels=mat.node_tree.nodes.new('ShaderNodeSeparateColor')
            mat.node_tree.links.new(orm.outputs['Color'],channels.inputs['Color'])
            mat.node_tree.links.new(channels.outputs['Green'],bsdf.inputs['Roughness'])
            mat.node_tree.links.new(channels.outputs['Blue'],bsdf.inputs['Metallic'])
    return mat


def make_desk(gray):
    parts = [box('Desktop', (0,dimension('DeskTopHeight')-.018,0), (dimension('DeskWidth'),.036,.54), wood, bevel=.016),
             box('ChairSeat', (0,dimension('ChairSeatHeight')-.014,.48), (.40,.028,.36), wood, bevel=.014),
             box('ChairBack', (0,.715,.66), (.42,.17,.028), wood, bevel=.014)]
    if gray:
        parts += [box('GrayDeskSupport', (0,.35,0), (.65,.7,.4), metal)]
    else:
        parts += [box('DeskShelf', (0,.605,.015), (.69,.025,.43), wood),
                  box('DeskBack', (0,.655,-.21), (.69,.085,.022), wood)]
        for x in (-.345,.345):
            parts.append(box('DeskSide', (x,.655,.015), (.024,.085,.43), wood))
            for z in (-.21,.21):
                parts.append(rod('DeskLeg', (x,.025,z), (x,.705,z), .018, metal))
                parts.append(box('DeskFoot', (x,.019,z), (.048,.038,.048), rubber, bevel=.008))
            parts.append(rod('DeskBrace', (x,.28,-.21), (x,.28,.21), .013, metal))
            parts.append(rod('TopRail', (x,.70,-.22), (x,.70,.22), .017, metal))
        parts.append(rod('CrossBrace', (-.345,.32,-.21), (.345,.32,-.21), .014, metal))
        for x in (-.165,.165):
            for z in (.35,.61):
                parts.append(rod('ChairLeg', (x,.025,z+.015), (x,.413,z), .013, metal))
                parts.append(box('ChairFoot', (x,.018,z+.015), (.038,.036,.04), rubber, bevel=.005))
            parts.append(rod('ChairBackPost', (x,.39,.61), (x,.79,.66), .012, metal))
            parts.append(rod('ChairBrace', (x,.2,.36), (x,.2,.625), .01, metal))
            for h in (.685,.745):
                parts.append(rod('BackRivet', (x,h,.638), (x,h,.646), .008, metal))
        # Two bent side hooks; small fittings are geometry, not painted dark marks.
        for x in (-.38,.38):
            parts.append(rod('HookStem', (x,.66,.1), (x,.60,.1), .008, metal))
            parts.append(rod('HookReturn', (x,.60,.1), (x,.60,.145), .008, metal))
    desk = finish(parts, 'StudentDeskArt_01')
    bpy.context.scene.cursor.location = (0,0,0)
    bpy.context.view_layer.objects.active = desk
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    return desk


def desks(gray):
    drop_roots(('StudentDeskGroup_', 'FULL_Classroom_Desk', 'V5_Desk'))
    reference = make_desk(gray)
    for index, (x,z) in enumerate(layout(root), 1):
        parent = geo.empty(f'StudentDeskGroup_{index:02}', collection, (x,0,z))
        obj = reference if index == 1 else bpy.data.objects.new(f'StudentDeskArt_{index:02}', reference.data)
        if index != 1: collection.objects.link(obj)
        obj.parent = parent
        obj.location = (0,0,0)
    if not gray:
        papers=[]
        for index in (2,5,7,10):
            x,z=layout(root)[index-1]
            papers.append(box('ExerciseBook', (x+.06,.765,z-.03), (.24,.024,.18), paper, bevel=.003))
        finish(papers,'V6_DeskExerciseBooks')


def furnishings():
    # Replace large solid teacher-desk body with a panel-and-pedestal construction.
    for state in ('Closed','Open'):
        parent=bpy.data.objects['TeacherDesk_'+state]
        remove(list(exporter.descendants(parent)-{parent}))
        pieces=[]
        def b(n,p,s,m=wood): pieces.append(box(n,p,s,m,bevel=.012))
        b('Top',(.55,.90,-3.25),(2.02,.055,.92))
        for x in (-.35,1.45): b('Side',(x,.45,-3.25),(.065,.85,.77))
        b('Modesty',(.55,.52,-3.60),(1.77,.53,.045))
        for x in (-.25,1.35):
            b('Pedestal',(x,.42,-3.27),(.35,.72,.7))
            for h in (.3,.56):
                b('DrawerFace',(x,h,-2.90),(.305,.20,.027))
                b('Handle',(x,h,-2.868),(.13,.022,.035),metal)
        pull=.28 if state=='Open' else 0
        b('CenterDrawer',(.55,.69,-2.9+pull),(.9,.22,.035))
        b('DrawerBottom',(.55,.586,-3.1+pull),(.88,.025,.43))
        for x in (.10,1.0): b('DrawerWall',(x,.675,-3.1+pull),(.022,.16,.43))
        b('LockPlate',(.55,.70,-2.873+pull),(.36,.135,.02),metal)
        for x in (.44,.55,.66):
            pieces.append(geo.cylinder('LockWheel',(x,.70,-2.852+pull),.037,.034,collection,brass,vertices=24,wall=True))
        b('Programme',(.20,.937,-3.24),(.34,.012,.24),paper)
        finish(pieces,'V6_TeacherDesk_'+state,parent)
    for state in ('Closed','Open'):
        parent=bpy.data.objects['MapCabinet_'+state]
        body=bpy.data.objects.get('MapCabinet_'+state+'_Body')
        if body: remove([body])
        panels=[box('Back',(-5.05,1.16,-4.08),(1.32,2.25,.035),wood)]
        for x in (-5.68,-4.42): panels.append(box('Stile',(x,1.16,-3.78),(.06,2.28,.66),wood))
        for h in (.08,1.0,2.27): panels.append(box('Shelf',(-5.05,h,-3.78),(1.28,.055,.68),wood))
        finish(panels,'V6_MapCabinet_'+state,parent)
    # The schedule gets a real floor-standing, stable notice stand.
    stand=[box('ScheduleBoard',(-3.40,1.12,-2.05),(.76,.52,.055),wood),
           box('SchedulePost',(-3.40,.48,-2.05),(.055,.92,.055),metal),
           box('ScheduleFoot',(-3.40,.025,-2.05),(.70,.05,.42),metal)]
    finish(stand,'V6_ScheduleStand')


def architecture():
    # Keep shell geometry and openings; replace finishes and oversize surface relief.
    replacements={'M_Cinematic_Classroom_Plaster':plaster,'M_Cinematic_Classroom_Teal':green,
                  'M_Cinematic_Classroom_Floor':floor,'M_Cinematic_Classroom_Wood':wood,
                  'M_Cinematic_Classroom_Darkwood':wood,'M_Cinematic_Classroom_Metal':metal}
    for obj in exporter.export_objects('classroom'):
        if obj.type!='MESH': continue
        for slot in obj.material_slots:
            if slot.material and slot.material.name in replacements: slot.material=replacements[slot.material.name]
        if obj.name.startswith(('Wall_', 'V4_Floor','V4_Ceiling')):
            project_uv(obj,.65)
        elif not obj.data.uv_layers:
            project_uv(obj)
    # Repair the inherited shell seams: the old west south pier left a 45 cm gap,
    # and the east north pier extended across half of the doorway.
    for side,segments in [('West',[('North',-4.125,.75),('South',3.225,2.55)]),
                          ('East',[('North',-.875,7.25),('South',4.225,.55)])]:
        parent=bpy.data.objects['Wall_'+side]
        x=-6 if side=='West' else 6
        for name,z,depth in segments:
            for part,h,height,mat in [('Lower',.7,1.4,green),('Upper',2.7,2.6,plaster)]:
                old=bpy.data.objects.get(f'Wall_{side}_{name}_{part}')
                if old: remove([old])
                obj=box(f'Wall_{side}_{name}_{part}',(x,h,z),(.2,height,depth),mat,parent)
                project_uv(obj,.65)
    remove([bpy.data.objects['Wall_East_Lintel']])
    box('Wall_East_Lintel',(6,3.25,3.35),(.2,1.5,1.2),plaster,bpy.data.objects['Wall_East'])
    drop_roots(('CeilingFan','Fluorescent','FULL_Classroom_Ceiling','V5_Ceiling'))
    trim=[]
    for z in (-4.37,4.37):
        trim.append(box('DadoRail',(0,1.405,z),(11.72,.035,.036),green))
        trim.append(box('CeilingCove',(0,3.94,z),(11.75,.06,.10),plaster))
    for x in (-5.87,5.87):
        # East rail stops before the exit, west rail follows solid wall segments.
        for z,length in ((-3.87,.90),(3.1,2.15)) if x<0 else ((-.9,7.0),(4.25,.28)):
            trim.append(box('DadoRail',(x,1.405,z),(.035,.035,length),green))
        trim.append(box('CeilingCove',(x,3.94,0),(.10,.06,8.7),plaster))
    for z in (-2.8,-.9,1.0):
        trim.append(box('WindowSill',(-5.79,.81,z),(.33,.065,1.83),wood,bevel=.014))
    finish(trim,'V6_ArchitecturalTrim')
    fixtures=[]
    for x in (-2.9,2.9):
        for z in (-1.8,1.7):
            fixtures.append(box('LampHousing',(x,3.80,z),(1.30,.07,.22),metal))
            for dz in (-.058,.058):
                fixtures.append(rod('Tube',(x-.59,3.76,z+dz),(x+.59,3.76,z+dz),.014,plaster))
            for dx in (-.42,.42): fixtures.append(rod('Hanger',(x+dx,3.85,z),(x+dx,3.99,z),.009,metal))
    for x in (-2.7,2.7):
        fixtures.append(rod('FanStem',(x,3.48,.35),(x,3.99,.35),.032,metal))
        fixtures.append(geo.cylinder('FanMotor',(x,3.50,.35),.135,.14,collection,metal,vertices=32))
        for angle in (0,math.tau/3,math.tau*2/3):
            acc=MeshAccumulator()
            # Tapered, swept blades with thickness, pitched by a few degrees.
            verts=[]
            for h in (-.009,.009):
                for r,t in ((.10,-.045),(.70,.015),(.79,.15),(.22,.09)):
                    verts.append((x+r*math.cos(angle)-t*math.sin(angle),
                                  -.35+r*math.sin(angle)+t*math.cos(angle),3.43+h+.04*r))
            acc._append([Vector(v) for v in verts],[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)])
            fixtures.append(acc.finish('FanBlade',metal,collection,bevel=.004))
    finish(fixtures,'V6_CeilingFixtures')
    # Window panes admit a softly luminous overcast exterior, with no scenery reveal.
    glass=bpy.data.materials.get('M_DirtyGlass')
    if glass:
        bsdf=next(n for n in glass.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
        for name in ('Base Color','Emission Color'):
            for link in list(bsdf.inputs[name].links): glass.node_tree.links.remove(link)
        bsdf.inputs['Base Color'].default_value=(.56,.64,.59,1)
        bsdf.inputs['Emission Color'].default_value=(.40,.48,.42,1)
        bsdf.inputs['Emission Strength'].default_value=.35
        bsdf.inputs['Roughness'].default_value=.65


def clock_and_roster():
    drop_roots(('V5_ClockMount','V5_ClockTick_'))
    for state in ('Missing','HandOnly','KeyOnly','Ready','Restored'):
        parent=bpy.data.objects['Clock_'+state]
        remove(list(exporter.descendants(parent)-{parent}))
        acc=MeshAccumulator()
        acc.add_torus((3.55,4.27,2.66),.365,.029,(0,1,0),48,8)
        rim=acc.finish('Rim',metal,collection,bevel=0)
        pieces=[rim,geo.cylinder('Face',(3.55,2.66,-4.28),.352,.022,collection,paper,vertices=64,wall=True)]
        for i in range(12):
            a=i*math.tau/12
            pieces.append(rod('Tick',(3.55+.303*math.sin(a),2.66+.303*math.cos(a),-4.255),
                              (3.55+.326*math.sin(a),2.66+.326*math.cos(a),-4.255),.007,metal))
        pieces.append(rod('Minute',(3.55,2.66,-4.23),(3.55,2.93,-4.23),.011,metal))
        if state in ('HandOnly','Ready','Restored'):
            angle=7*math.tau/12 if state=='Restored' else math.pi
            pieces.append(rod('Hour',(3.55,2.66,-4.21),(3.55+.18*math.sin(angle),2.66+.18*math.cos(angle),-4.21),.014,metal))
        if state in ('KeyOnly','Ready','Restored'):
            pieces.append(box('WindingKey',(3.93,2.56,-4.27),(.11,.055,.035),brass))
        finish(pieces,'V6_Clock_'+state,parent)
    # Reinforce the paper's physical backing and slot construction in each state.
    for state in ('Missing','Inserted','Aligned'):
        parent=bpy.data.objects['Roster_'+state]
        pieces=[box('RosterBacking',(4.75,2.2,-4.36),(1.02,1.35,.04),wood)]
        for x in (4.25,5.25): pieces.append(box('SliderRail',(x,2.2,-4.255),(.025,1.30,.055),metal))
        for x in (4.44,5.06): pieces.append(box('PaperClip',(x,2.79,-4.255),(.08,.10,.02),metal))
        finish(pieces,'V6_RosterMount_'+state,parent)
    drop_roots(('BlackboardSymbol',))


def windows_and_door():
    drop_roots(('Curtain_','FULL_Classroom_CurtainFolds','FULL_Classroom_WindowHardware'))
    fabric=material('M_V6_Linen',(.58,.55,.45),.97)
    for z in (-3.55,1.75):
        acc=MeshAccumulator()
        vertices=[]
        for row in range(9):
            for column in range(33):
                t=column/32
                h=2.72-row/8*1.83+.025*math.sin(t*math.pi*5)*row/8
                vertices.append(Vector((-5.64+.047*math.cos(t*math.tau*4),-(z+(t-.5)*.42),h)))
        faces=[]
        for row in range(8):
            for column in range(32):
                i=row*33+column
                faces.append((i,i+1,i+34,i+33))
        acc._append(vertices,faces)
        curtain=acc.finish('V6_Curtain',fabric,collection,bevel=0)
        for polygon in curtain.data.polygons: polygon.use_smooth=True
        modifier=curtain.modifiers.new('FabricThickness','SOLIDIFY')
        modifier.thickness=.004
        project_uv(curtain,2)
    drop_roots(('FULL_Classroom_DoorMoulding','V5_Door'))
    frame=[]
    for z in (2.75,3.95):
        frame.append(box('DoorJamb',(5.86,1.23,z),(.22,2.46,.09),wood))
        frame.append(box('DoorArchitrave',(5.73,1.24,z),(.06,2.48,.12),wood))
    frame.append(box('DoorHead',(5.84,2.45,3.35),(.25,.18,1.29),wood))
    frame.append(box('DoorHeadTrim',(5.70,2.52,3.35),(.06,.08,1.40),wood))
    finish(frame,'V6_DoorFrame',bpy.data.objects['Door_East'])
    # Three real sliding latch tracks; moving handles remain under Core-derived roots.
    for state in ('Closed','Open'):
        parent=bpy.data.objects['WindowCache_'+state]
        remove([o for o in parent.children if any(s in o.name for s in ('Latch','Scratch','Drawer'))])
        pieces=[]
        heights=(2.18,1.22,1.70) if state=='Open' else (1.70,1.70,1.70)
        for z,h in zip((-2.8,-.9,1.0),heights):
            pieces.append(box('LatchTrack',(-5.62,1.72,z),(.035,1.20,.09),metal))
            pieces.append(box('LatchBlock',(-5.58,h,z),(.07,.13,.12),brass))
            pieces.append(rod('LatchLever',(-5.54,h,z),(-5.40,h,z),.018,brass))
            for end in (1.16,2.28):
                pieces.append(rod('TrackScrew',(-5.59,end,z),(-5.58,end,z),.014,metal))
        front=-5.18 if state=='Open' else -5.57
        pieces += [box('CacheFront',(front,.55,-.9),(.045,.23,1.15),wood),
                   box('CacheBase',((front-5.78)/2,.435,-.9),(front+5.78,.035,1.10),wood)]
        for z in (-1.45,-.35):
            pieces.append(box('CacheSide',((front-5.78)/2,.53,z),(front+5.78,.17,.025),wood))
        pieces.append(rod('CachePull',(front+.035,.57,-1.0),(front+.035,.57,-.8),.012,metal))
        finish(pieces,'V6_WindowCache_'+state,parent)
    # Joinery is attached to the existing hinge so it follows both transition directions.
    hinge=bpy.data.objects['Door_East_Hinge']
    remove(list(exporter.descendants(hinge)-{hinge}))
    pieces=[]
    # Build in world coordinates, then preserve world pose while parenting.
    for z in (2.88,3.82): pieces.append(box('DoorStile',(5.86,1.18,z),(.09,2.36,.08),wood))
    for h,depth in ((.08,.12),(1.17,.12),(2.30,.12)):
        pieces.append(box('DoorRail',(5.86,h,3.35),(.09,depth,.94),wood))
    pieces.append(box('LowerPanel',(5.88,.63,3.35),(.055,.93,.81),wood))
    glass=bpy.data.materials['M_DirtyGlass']
    pieces.append(box('DoorGlass',(5.88,1.73,3.35),(.024,.96,.80),glass))
    for z in (2.96,3.74):
        pieces.append(box('GlazingBead',(5.815,1.73,z),(.025,1.01,.025),wood))
    for h in (1.24,2.22): pieces.append(box('GlazingBead',(5.815,h,3.35),(.025,.025,.81),wood))
    for h in (.30,1.18,2.06):
        pieces.append(rod('HingePin',(5.80,h-.065,3.85),(5.80,h+.065,3.85),.021,brass))
    pieces.append(box('HandlePlate',(5.80,1.03,2.98),(.025,.19,.07),brass))
    pieces.append(rod('HandleStem',(5.76,1.06,2.98),(5.68,1.06,2.98),.019,brass))
    pieces.append(rod('HandleGrip',(5.68,1.06,2.98),(5.68,1.06,3.12),.019,brass))
    finish(pieces,'DoorV4_Leaf',hinge)


def configure_export():
    spec=exporter.SPECS['classroom']
    spec.update(source=SOURCE,glb=GLB,manifest=MANIFEST,baseline=SEED.as_posix().replace('art/blender/dream_classroom/','assets/generated/dream_escape/3d/').replace('.blend','.glb'))
    spec['required']=['Room_Ceiling','Door_East_Hinge','Blackboard_Blank','Blackboard_Revealed',
                      'TeacherDesk_Open','Clock_Restored','Roster_Aligned','StudentDeskGroup_12',
                      'V6_ScheduleStand','V6_CeilingFixtures','V6_ArchitecturalTrim']
    # Exporter uses metadata to validate the expected desk count, historical v5 remains 18.
    spec['deskCount']=12


def record_provenance():
    manifest=json.loads((root/MANIFEST).read_text('utf-8'))
    record={
        'id':'dream_classroom_cinematic_runtime_v6',
        'sourceFile':SOURCE.as_posix(), 'gameFile':GLB.as_posix().removeprefix('assets/generated/'),
        'status':'adopted-runtime-default',
        'generator':'Blender source-preserving remodeling, shared meshes, physical-size UV projection and glTF 2.0 export',
        'model':'Blender 5.2.0 LTS', 'generatedAt':'2026-09-09',
        'referenceImages':['art/concepts/dream_classroom/classroom-cinematic-target-v5.png',
                           'outputs/classroom-v5-full-runtime/01-view-blackboard.png'],
        'referenceSources':[SEED.as_posix(),'scripts/presentation/ClassroomLayout3D.cs',
                            'art/sources/cc0/polyhaven/environment-cinematic-v1/source-manifest.json'],
        'license':'Project-authored geometry and adopted Poly Haven CC0 materials; no purchased assets or local mc_model content',
        'parameters':{'roomMetres':[12,9,4],'desks':12,'desktopMetres':.75,'chairSeatMetres':.44,
                      'renderer':'Godot 4.6.3 Compatibility','script':'scripts/tools/remodel_classroom_v6.py'},
        'sourceSha256':manifest['sourceSha256'],'runtimeGlbSha256':manifest['runtimeGlbSha256'],
        'postprocessing':['Preserved the v5 source; rebuilt desks, teacher desk, cabinet panels, door, latches, clock, notice stand and ceiling fixtures in v6',
                          'Packed adopted materials, projected UVs in metre scale, linked twelve desk instances and excluded Lookdev characters/lights/cameras',
                          'Godot owns collision, clues, state selection, neutral fill and the two reflection probes; no runtime generation'],
        'prompt':'Authoring specification approved 2026-09-09: realistic old East Asian classroom with twelve wood-and-steel desks, pale plaster, faded green dado, terrazzo floor and gentle west-window daylight. Preserve the 12x9x4 metre shell, east door, three west window bays and all current puzzle semantics; keep central aisle >=2.4m and side/rear main aisles >=1.2m.'
    }
    path=root/'assets/generated/provenance.json'
    text=path.read_text('utf-8')
    encoded='\n'.join('    '+line for line in json.dumps(record,ensure_ascii=False,indent=2).splitlines())
    marker='"id": "'+record['id']+'"'
    if marker in text:
        start=text.rfind('    {',0,text.index(marker))
        _,length=json.JSONDecoder().raw_decode(text[start+4:])
        text=text[:start]+encoded+text[start+4+length:]
    else:
        end=text.rfind('\n  ]')
        assert end>=0
        text=text[:end]+',\n'+encoded+text[end:]
    json.loads(text)
    path.write_text(text,'utf-8')


def check_layout():
    positions=layout(root)
    assert len(positions)==12
    for i,(x,z) in enumerate(positions,1):
        obj=bpy.data.objects[f'StudentDeskGroup_{i:02}']
        assert (obj.location-Vector((x,-z,0))).length<.0001
    assert min(abs(x) for x,z in positions)*2-.82>=2.4
    assert 5.7-max(abs(x) for x,z in positions)-.41>=1.2
    assert 4.2-max(z for x,z in positions)-.71>=1.2
    meshes=[bpy.data.objects[f'StudentDeskArt_{i:02}'].data for i in range(1,13)]
    assert len({m.as_pointer() for m in meshes})==1


def main():
    global root,collection,wood,metal,rubber,paper,brass,plaster,green,floor
    parser=argparse.ArgumentParser()
    parser.add_argument('mode',choices=['graybox','build','export','check'])
    parser.add_argument('--root',type=Path,required=True)
    opts=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    root=opts.root.resolve()
    configure_export()
    if opts.mode in ('export','check'):
        bpy.ops.wm.open_mainfile(filepath=str(root/SOURCE),load_ui=False)
        check_layout()
        exporter.run('classroom',opts.mode,root)
        if opts.mode=='export': record_provenance()
        print('CLASSROOM_V6_CHECK_PASS desks=12 centralAisle=3.68 sideAisle=1.74 rearAisle=1.24')
        return
    seed_hash=exporter.sha256(root/SEED)
    bpy.ops.wm.open_mainfile(filepath=str(root/SEED),load_ui=False)
    collection=geo.collection('ClassroomV6')
    wood=material('M_V6_Oak',(.38,.27,.15),.78,asset='oak_veneer_01',normal_strength=.18)
    metal=material('M_V6_PaintedSteel',(.105,.135,.12),.72,.18)
    rubber=material('M_V6_Rubber',(.04,.043,.038),.95)
    paper=bpy.data.materials['M_Cinematic_AgedPaper']
    brass=bpy.data.materials['M_Cinematic_AgedBrass']
    plaster=material('M_V6_Plaster',(.66,.65,.58),.93,asset='painted_plaster_wall',normal_strength=.12)
    green=material('M_V6_GreenPaint',(.23,.32,.245),.86,asset='painted_plaster_wall',normal_strength=.10,diffuse=False)
    floor=material('M_V6_Terrazzo',(.40,.41,.36),.79,asset='terrazzo_tiles',normal_strength=.18)
    desks(opts.mode=='graybox')
    furnishings()
    if opts.mode=='build':
        # glTF also exports legacy curve cables. Convert them explicitly so UVs,
        # adopted material replacement and the geometry budget include them.
        for obj in list(exporter.export_objects('classroom')):
            if obj.type not in ('CURVE','FONT','SURFACE'): continue
            bpy.ops.object.select_all(action='DESELECT')
            obj.hide_set(False)
            obj.select_set(True)
            bpy.context.view_layer.objects.active=obj
            bpy.ops.object.convert(target='MESH')
        architecture()
        clock_and_roster()
        windows_and_door()
    bpy.context.view_layer.update()
    check_layout()
    bpy.context.scene['studentDeskSharedMeshCount']=12
    bpy.context.scene['visualAcceptance']='pending-runtime-review'
    bpy.context.scene['remodelVersion']=6
    bpy.context.scene['sourceSeedSha256']=seed_hash
    # Freeze modifier-generated UV interpolation in the authority source. Otherwise
    # bevel UVs can vary by one float ULP across processes during export_apply.
    for obj in exporter.export_objects('classroom'):
        if obj.type!='MESH' or not obj.modifiers: continue
        bpy.ops.object.select_all(action='DESELECT')
        obj.hide_set(False)
        obj.select_set(True)
        bpy.context.view_layer.objects.active=obj
        for modifier in list(obj.modifiers): bpy.ops.object.modifier_apply(modifier=modifier.name)
    # Pack adopted source images: Blender remains the editable, self-contained source.
    bpy.ops.file.pack_all()
    destination=root/(SOURCE if opts.mode=='build' else Path('outputs/classroom-v6/graybox.blend'))
    destination.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(destination))
    if opts.mode=='graybox':
        exporter.export('classroom',root/'outputs/classroom-v6/graybox.glb')
    else:
        exporter.run('classroom','export',root)
        record_provenance()
    assert exporter.sha256(root/SEED)==seed_hash,'Historical v5 source changed'
    print('CLASSROOM_V6_BUILD_PASS mode='+opts.mode)


if __name__=='__main__': main()
