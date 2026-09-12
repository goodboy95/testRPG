"""Build the approved wide foyer, short hall and enclosed scenic stair.

Blender 5.2 --background --python this_file -- graybox|build|export|check --root .
Reads MansionLayout3D.cs; v1/v2 sources and the historical exterior stay untouched.
"""
from __future__ import annotations
import argparse
import hashlib
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
from remodel_classroom_v6 import project_uv

SOURCE = Path('art/blender/dream_manor/dream-manor-hall-v3.blend')
GLB = Path('assets/generated/dream_escape/3d/dream-manor-hall-v3.glb')
MANIFEST = GLB.with_name(GLB.stem + '-manifest.json')
LAYOUT = Path('scripts/presentation/MansionLayout3D.cs')
REQUIRED = ['DreamManorHall', 'Room_Floor', 'Room_Ceiling', 'V3_PortalJoinery',
            'Door_Front_Root', 'Door_Front_Hinge_L', 'Door_Front_Hinge_R',
            'Door_Room1_Root', 'Door_Room1_Hinge', 'Door_Room2_Root', 'Door_Room2_Hinge',
            'Door_Room3_Root', 'Door_Room3_Hinge', 'Door_Room4_Root',
            'Door_Room4_Hinge_L', 'Door_Room4_Hinge_R', 'ReservedModule_A', 'ReservedModule_B',
            'V3_StairStructure', 'V3_StairScreen', 'V3_Fireplace', 'V3_ChairSouth',
            'V3_ChairNorth', 'V3_ConsoleTable', 'V3_FoyerBench', 'V3_FrontSidelightGlass']


def read_layout(root):
    source = (root / LAYOUT).read_text('utf-8')
    numbers = {key: float(value) for key, value in re.findall(r'const float (\w+) = (-?[\d.]+)f;', source)}
    vectors = {key: tuple(float(n.strip().removesuffix('f')) for n in value.split(','))
               for key, value in re.findall(r'public static readonly Vector3 (\w+) = new\(([^)]+)\);', source)}
    return {**numbers, **vectors}


def empty(name, position=(0, 0, 0), parent=None):
    obj = geo.empty(name, collection, position)
    if parent is not None:
        bpy.context.view_layer.update()
        geo.parent_keep_world(obj, parent)
    return obj


def box(name, position, size, mat, bevel=.008):
    obj=geo.box(name, position, size, collection, mat, 0 if gray else bevel)
    if mat == wood:
        uv=obj.data.uv_layers.active
        lengths=(size[0],size[2],size[1])
        for face in obj.data.polygons:
            normal_axis=max(range(3),key=lambda a:abs(face.normal[a]))
            axes=sorted((a for a in range(3) if a!=normal_axis),key=lambda a:lengths[a],reverse=True)
            for loop in face.loop_indices:
                co=obj.data.vertices[obj.data.loops[loop].vertex_index].co
                uv.data[loop].uv=(co[axes[0]],co[axes[1]])
    else:
        project_uv(obj)
    return obj


def rod(name, start, end, radius, mat, vertices=16):
    acc = MeshAccumulator()
    acc.add_rod((start[0], -start[2], start[1]), (end[0], -end[2], end[1]), radius, vertices)
    obj=acc.finish(name, mat, collection, bevel=0)
    project_uv(obj)
    if mat==wood:
        a=Vector((start[0],-start[2],start[1]));direction=Vector((end[0],-end[2],end[1]))-a
        direction.normalize()
        helper=Vector((0,0,1)) if abs(direction.z)<.9 else Vector((1,0,0))
        transverse=direction.cross(helper).normalized()
        for loop in obj.data.loops:
            co=obj.data.vertices[loop.vertex_index].co-a
            obj.data.uv_layers.active.data[loop.index].uv=(co.dot(direction),co.dot(transverse))
    return obj


def finish(parts, name, parent=None, density=1):
    for part in parts:
        if not part.data.uv_layers:project_uv(part)
        part.data.uv_layers.active_index=0
        part.data.uv_layers[0].active_render=True
        part.data.uv_layers[0].name='UVMap'
    if len(parts) == 1:
        obj = parts[0]
        obj.name = name
        bpy.context.view_layer.update()
        geo.parent_keep_world(obj, parent or hall_root)
    else:
        obj = geo.join(parts, name, parent or hall_root)
    # Preserve per-component grain direction and authored painting UVs when joining.
    while len(obj.data.uv_layers)>1:obj.data.uv_layers.remove(obj.data.uv_layers[-1])
    obj.data.uv_layers.active_index=0
    obj.data.uv_layers[0].active_render=True
    if density!=1:
        for uv in obj.data.uv_layers[0].data:uv.uv*=density
    return obj


def lathe(name, position, profile, mat, segments=32):
    """Real hollow/turned profiles; dimensions and profile coordinates are metres."""
    vertices, faces = [], []
    for radius, h in profile:
        for j in range(segments):
            angle = 2 * math.pi * j / segments
            vertices.append((position[0] + radius * math.cos(angle),
                             -position[2] + radius * math.sin(angle), position[1] + h))
    for ring in range(len(profile)-1):
        for j in range(segments):
            a, b = ring*segments+j, ring*segments+(j+1)%segments
            faces.append((a, b, b+segments, a+segments))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(mat)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    for face in mesh.polygons:
        face.use_smooth = True
    return obj


def pbr(name, color, roughness=.8, metallic=0, asset=None, diffuse=True, normal_strength=.22, emission=0, tint=None):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.diffuse_color = (*color, 1)
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Roughness'].default_value = roughness
    bsdf.inputs['Metallic'].default_value = metallic
    if emission:
        bsdf.inputs['Emission Color'].default_value = (*color, 1)
        bsdf.inputs['Emission Strength'].default_value = emission
    if asset and not gray:
        directory = root / 'art/sources/cc0/polyhaven/environment-cinematic-v1' / asset
        def texture(suffix):
            paths = sorted(directory.glob(f'*_{suffix}_2k.*'))
            if not paths:
                raise RuntimeError(f'Missing adopted CC0 map: {asset}/{suffix}')
            node = mat.node_tree.nodes.new('ShaderNodeTexImage')
            node.image = bpy.data.images.load(str(paths[0]), check_existing=True)
            if suffix != 'diff': node.image.colorspace_settings.name = 'Non-Color'
            return node
        if diffuse:
            source=texture('diff').outputs['Color']
            if tint:
                multiply=mat.node_tree.nodes.new('ShaderNodeMix')
                multiply.data_type='RGBA';multiply.blend_type='MULTIPLY'
                multiply.inputs[0].default_value=1
                multiply.inputs[7].default_value=(*tint,1)
                mat.node_tree.links.new(source,multiply.inputs[6])
                source=multiply.outputs[2]
            mat.node_tree.links.new(source, bsdf.inputs['Base Color'])
        normal = mat.node_tree.nodes.new('ShaderNodeNormalMap')
        normal.inputs['Strength'].default_value = normal_strength
        mat.node_tree.links.new(texture('nor_gl').outputs['Color'], normal.inputs['Color'])
        mat.node_tree.links.new(normal.outputs['Normal'], bsdf.inputs['Normal'])
        channels = mat.node_tree.nodes.new('ShaderNodeSeparateColor')
        mat.node_tree.links.new(texture('arm').outputs['Color'], channels.inputs['Color'])
        mat.node_tree.links.new(channels.outputs['Green'], bsdf.inputs['Roughness'])
        mat.node_tree.links.new(channels.outputs['Blue'], bsdf.inputs['Metallic'])
    return mat


def wall_segments(name, axis, fixed, start, end, height, openings):
    """A continuous wall with real openings. axis x spans X, axis z spans Z."""
    parts = []
    def segment(a, b, bottom, top):
        if b-a <= .001 or top-bottom <= .001: return
        position = ((a+b)/2, (top+bottom)/2, fixed) if axis == 'x' else (fixed, (top+bottom)/2, (a+b)/2)
        size = (b-a, top-bottom, v['WallThickness']) if axis == 'x' else (v['WallThickness'], top-bottom, b-a)
        parts.append(box('PlasterPier', position, size, plaster, .004))
    cursor = start
    for center, width, opening_height in sorted(openings):
        segment(cursor, center-width/2, 0, height)
        segment(center-width/2, center+width/2, opening_height, height)
        cursor = center+width/2
    segment(cursor, end, 0, height)
    finish(parts, name, density=.7)


def wainscot(name, axis, fixed, start, end, openings=()):
    parts = []
    def b(n, u, h, width, height, depth, offset=0, mat=None):
        position = (u, h, fixed+offset) if axis == 'x' else (fixed+offset, h, u)
        size = (width, height, depth) if axis == 'x' else (depth, height, width)
        parts.append(box(n, position, size, mat or wood, .004))
    spans, cursor = [], start
    for center, width in sorted(openings):
        spans.append((cursor, center-width/2-.12))
        cursor = center+width/2+.12
    spans.append((cursor, end))
    # Put the frame slightly into the room, with panels recessed behind it.
    inward = 1 if fixed < 0 else -1
    for a, b_end in spans:
        if b_end-a < .15: continue
        count = max(1, round((b_end-a)/.92))
        pitch = (b_end-a)/count
        b('Skirting', (a+b_end)/2, .10, b_end-a, .20, .085)
        b('ChairRail', (a+b_end)/2, 1.22, b_end-a, .075, .09)
        b('TopCap', (a+b_end)/2, 1.285, b_end-a, .035, .12)
        for i in range(count):
            center = a+(i+.5)*pitch
            b('RecessedPanel', center, .72, pitch-.11, .93, .025, -inward*.022)
            for h in (.26, 1.15): b('PanelMoulding', center, h, pitch-.16, .035, .04, inward*.016)
            for edge in (-1, 1):
                b('Stile', center+edge*(pitch-.07)/2, .72, .07, 1, .075)
                if not gray: b('PanelBead', center+edge*(pitch-.16)/2, .72, .026, .89, .04, inward*.016)
    finish(parts, name)


def architecture():
    fw, fd, fh, hw, hd, hh = (v[k] for k in ('FoyerWidth','FoyerDepth','FoyerHeight','HallWidth','HallDepth','HallHeight'))
    floor_root=empty('Room_Floor',parent=hall_root)
    finish([box('FoyerFloor', (0,-.10,-fd/2),(fw,.2,fd),floor,0)],'V3_FoyerFloor',floor_root,density=.8)
    finish([box('HallFloor',(0,-.10,hd/2),(hw,.2,hd),floor,0)],'V3_HallFloor',floor_root,density=.8)
    finish([box('FoyerCeiling',(0,fh+.1,-fd/2),(fw+.2,.2,fd+.2),plaster,0),
            box('HallCeiling',(0,hh+.1,hd/2),(hw+.2,.2,hd+.2),plaster,0)], 'Room_Ceiling', density=.7)
    wall_segments('V3_WallFoyerWest','z',-fw/2-.1,-fd,0,fh,[])
    wall_segments('V3_WallFoyerEast','z',fw/2+.1,-fd,0,fh,[])
    single = v['SingleDoorWidth']+.12
    wall_segments('V3_WallHallWest','z',-hw/2-.1,0,hd,hh,
                  [(v['RoomOneDoor'][2],single,v['SingleDoorHeight']+.06),
                   (v['RoomThreeDoor'][2],single,v['SingleDoorHeight']+.06)])
    wall_segments('V3_WallHallEast','z',hw/2+.1,0,hd,hh,[(v['RoomTwoDoor'][2],single,v['SingleDoorHeight']+.06)])
    wall_segments('V3_WallSouth','x',-fd-.1,-fw/2,fw/2,fh,[(0,4.05,v['FrontDoorHeight']+.08)])
    wall_segments('V3_WallNorth','x',hd+.1,-hw/2,hw/2,hh,[(0,v['DoubleDoorWidth']+.12,v['DoubleDoorHeight']+.08)])
    shoulder=(fw-hw)/2
    finish([box('ShoulderW',(-(hw+shoulder)/2,fh/2,.1),(shoulder,fh,.2),plaster),
            box('ShoulderE',((hw+shoulder)/2,fh/2,.1),(shoulder,fh,.2),plaster),
            box('PortalSpandrel',(0,(hh+fh)/2,.1),(hw,fh-hh,.2),plaster)],'V3_TransitionWall')
    trim=[]
    for x in (-hw/2+.04,hw/2-.04):
        trim += [box('PortalPost',(x,hh/2,-.09),(.16,hh,.24),wood),
                 box('PortalCapital',(x,hh-.22,-.09),(.25,.18,.30),wood)]
    trim.append(box('PortalLintel',(0,hh-.08,-.08),(hw,.22,.30),wood))
    for x in (-fw/2+.04,fw/2-.04): trim.append(box('FoyerCornice',(x,fh-.12,-fd/2),(.15,.16,fd),wood))
    for z in (-fd+.04,-.1): trim.append(box('FoyerCornice',(0,fh-.12,z),(fw,.16,.15),wood))
    for x in (-hw/2+.04,hw/2-.04): trim.append(box('HallCornice',(x,hh-.12,hd/2),(.15,.16,hd),wood))
    for z in (0,4,8,12): trim.append(box('CeilingBeam',(0,hh-.16,z),(hw,.22,.18),wood))
    for x in (-2.2,0,2.2): trim.append(box('CeilingFoyerBeam',(x,fh-.12,-fd/2),(.16,.22,fd),wood))
    finish(trim,'V3_PortalJoinery')
    wainscot('V3_WainscotFoyerWest','z',-fw/2+.04,-fd,0)
    wainscot('V3_WainscotFoyerEast','z',fw/2-.04,-fd,0)
    wainscot('V3_WainscotSouth','x',-fd+.04,-fw/2,fw/2,[(0,4.05)])
    wainscot('V3_WainscotHallWest','z',-hw/2+.04,0,hd,[(v['RoomOneDoor'][2],single),(v['RoomThreeDoor'][2],single)])
    wainscot('V3_WainscotHallEast','z',hw/2-.04,0,hd,[(v['RoomTwoDoor'][2],single)])
    wainscot('V3_WainscotNorth','x',hd-.04,-hw/2,hw/2,[(0,v['DoubleDoorWidth']+.12)])
    for label,z in [('A',5.1),('B',9.5)]:
        module=empty('ReservedModule_'+label,parent=hall_root)
        # Flat plaster bay, no lintel or doorlike recess.
        finish([box('PlainWallBay',(hw/2-.006,2.1,z),(.012,1.45,1.25),plaster,0)],'V3_ReservedWall_'+label,module,.7)


def door(prefix, center, width, height, facing, double=False):
    """Build in local X/Y with front +Z, then rotate the complete frame into the wall."""
    parent=empty(prefix+'_Root',parent=hall_root)
    pieces=[]
    for x in (-width/2-.075,width/2+.075):
        pieces += [box('Jamb',(x,height/2,0),(.15,height+.1,.18),wood),
                   box('DoorStop',(x,height/2,.105),(.055,height,.04),wood)]
    pieces += [box('Lintel',(0,height+.06,0),(width+.3,.16,.20),wood),
               box('Crown',(0,height+.16,.01),(width+.40,.06,.24),wood),
               box('Threshold',(0,.016,0),(width+.18,.032,.28),stone)]
    finish(pieces,prefix+'_Frame',parent)
    finish([box('VestibuleBack',(0,height/2,-.6),(width+.14,height,.08),iron),
            box('VestibuleFloor',(0,.015,-.3),(width+.12,.03,.6),stone),
            box('VestibuleTop',(0,height,-.3),(width+.12,.06,.6),wood),
            box('VestibuleLeft',(-width/2-.04,height/2,-.3),(.06,height,.6),wood),
            box('VestibuleRight',(width/2+.04,height/2,-.3),(.06,height,.6),wood)],prefix+'_DarkVestibule',parent)
    leaf_count=2 if double else 1
    for i in range(leaf_count):
        left = i==0 if double or facing != -90 else False
        hinge_x=-width/2 if left else width/2
        label=('_L' if left else '_R') if double else ''
        hinge=empty(prefix+'_Hinge'+label,(hinge_x,0,0),parent)
        leaf_width=width/leaf_count-.012
        center_x=hinge_x+(leaf_width/2 if left else -leaf_width/2)
        parts=[box('LeafCore',(center_x,height/2,0),(leaf_width,height-.035,.075),wood)]
        for side in (-1,1):
            for x in (center_x-leaf_width/2+.07,center_x+leaf_width/2-.07):
                parts.append(box('LeafStile',(x,height/2,side*.052),(.14,height-.04,.045),wood))
            for y in (.13,.73,1.40,height-.13):
                parts.append(box('LeafRail',(center_x,y,side*.052),(leaf_width-.15,.14,.045),wood))
            for bottom,top in [(.22,.64),(.82,1.31),(1.49,height-.22)]:
                parts.append(box('InsetPanel',(center_x,(bottom+top)/2,side*.041),(leaf_width-.29,top-bottom,.018),wood))
                if not gray:
                    for y in (bottom,top): parts.append(box('PanelBeading',(center_x,y,side*.073),(leaf_width-.27,.025,.028),wood))
                    for x in (center_x-(leaf_width-.27)/2,center_x+(leaf_width-.27)/2):
                        parts.append(box('PanelBeading',(x,(bottom+top)/2,side*.073),(.025,top-bottom,.028),wood))
        if not gray:
            for y in (.30,height/2,height-.30):
                parts.append(rod('HingeBarrel',(hinge_x,y-.06,.005),(hinge_x,y+.06,.005),.023,brass,24))
            handle_x=center_x+(leaf_width/2-.18)*(1 if left else -1)
            parts += [box('Escutcheon',(handle_x,1.03,.085),(.09,.25,.02),brass,.013),
                      rod('HandleStem',(handle_x,1.10,.09),(handle_x,1.10,.145),.019,brass,24),
                      rod('Lever',(handle_x,1.10,.145),(handle_x+(-.10 if left else .10),1.10,.145),.016,brass,24),
                      box('Keyhole',(handle_x,.97,.098),(.012,.027,.004),iron,.004)]
        finish(parts,prefix+'_Leaf'+label,hinge)
    # Position only after every component is parented in local coordinates.
    parent.location=(center[0],-center[2],center[1])
    parent.rotation_euler.z=math.radians(facing)
    return parent


def doors():
    door('Door_Front',v['FrontDoor'],v['FrontDoorWidth'],v['FrontDoorHeight'],0,True)
    door('Door_Room1',v['RoomOneDoor'],v['SingleDoorWidth'],v['SingleDoorHeight'],90)
    door('Door_Room2',v['RoomTwoDoor'],v['SingleDoorWidth'],v['SingleDoorHeight'],-90)
    door('Door_Room3',v['RoomThreeDoor'],v['SingleDoorWidth'],v['SingleDoorHeight'],90)
    door('Door_Room4',v['RoomFourDoor'],v['DoubleDoorWidth'],v['DoubleDoorHeight'],180,True)
    frame, panes = [], []
    h=v['FrontDoorHeight']
    z=v['FrontDoor'][2]
    for side in (-1,1):
        x=side*1.695
        panes.append(box('SidelightPane',(x,h/2,z),(.53,h-.08,.035),glass,.004))
        for edge in (-1,1): frame.append(box('SidelightStile',(x+edge*.29,h/2,z+.025),(.055,h,.08),wood))
        for y in (.04,h/3,2*h/3,h-.02): frame.append(box('SidelightRail',(x,y,z+.025),(.6,.045,.08),wood))
        for dx in (-.14,.14): frame.append(box('SidelightLeading',(x+dx,h/2,z+.05),(.013,h-.08,.018),brass,.002))
    finish(panes,'V3_FrontSidelightGlass')
    finish(frame,'V3_FrontSidelightFrame')


def fireplace():
    x,y,z=v['FireplacePosition']
    if gray:
        finish([box('FireplaceBlock',(x,y,z),v['FireplaceSize'],stone)],'V3_Fireplace'); return
    parts=[]
    def b(n,offset,size,mat=stone):
        parts.append(box(n,(x+offset[0],offset[1],z+offset[2]),size,mat,.012))
    b('Hearth',(.06,.075,0),(.82,.15,2.18))
    b('Fireback',(-.31,.72,0),(.07,1.2,1.3),iron)
    for side in (-1,1):
        b('StonePier',(.02,.77,side*.72),(.63,1.4,.28))
        b('TimberPilaster',(.18,1.18,side*.93),(.22,2.12,.20),wood)
        for h in (.18,1.52,2.06): b('PilasterCollar',(.22,h,side*.93),(.27,.10,.25),wood)
    b('FireLintel',(.02,1.47,0),(.65,.25,1.55))
    b('Mantel',(.06,1.69,0),(.85,.14,2.15),wood)
    b('Overmantel',(-.17,1.98,0),(.23,.48,1.6),wood)
    b('OvermantelInset',(-.03,1.98,0),(.04,.31,1.32),stone)
    for dz in (-.49,-.25,0,.25,.49):
        parts.append(rod('GrateBar',(x+.27,.23,z+dz),(x+.27,.59,z+dz),.016,iron))
    parts += [rod('GrateTop',(x+.27,.59,z-.53),(x+.27,.59,z+.53),.023,iron),
              rod('ColdLog',(x-.02,.23,z-.48),(x+.1,.26,z+.42),.085,wood,12)]
    finish(parts,'V3_Fireplace')


def chair(name, position):
    x,_,z=position
    parts=[box('SeatCushion',(x-.02,.47,z),(.74,.18,.82),fabric,.055),
           box('BackCushion',(x+.40,.80,z),(.18,.50,.82),fabric,.055)]
    if not gray:
        for dx in (-.38,.38):
            for dz in (-.39,.39):
                parts += [box('ChairLeg',(x+dx,.24,z+dz),(.075,.44,.075),wood,.014),
                          box('LegCollar',(x+dx,.08,z+dz),(.09,.05,.09),wood,.009)]
        for dz in (-.45,.45):
            parts += [box('SeatSideRail',(x,.36,z+dz),(.95,.13,.08),wood),
                      box('ArmPad',(x,.73,z+dz),(.91,.09,.115),wood,.025),
                      box('ArmPost',(x-.38,.58,z+dz),(.07,.29,.07),wood),
                      box('BackPost',(x+.46,.62,z+dz),(.075,1.03,.075),wood)]
        parts.append(box('BackCrest',(x+.46,1.09,z),(.13,.09,.98),wood,.022))
        for dz in (-.28,0,.28):
            parts.append(box('BackSlat',(x+.5,.79,z+dz),(.04,.47,.065),wood))
        parts.append(box('FrontApron',(x-.43,.36,z),(.065,.14,.85),wood))
    else:
        parts.append(box('ChairSupport',(x,.2,z),(.75,.4,.76),wood))
    finish(parts,name)


def furniture():
    fireplace()
    chair('V3_ChairSouth',v['ChairSouthPosition'])
    chair('V3_ChairNorth',v['ChairNorthPosition'])
    x,_,z=v['SideTablePosition']
    parts=[box('TableTop',(x,.615,z),(.65,.05,.65),wood,.018)]
    for dx in (-.24,.24):
        for dz in (-.24,.24): parts.append(box('TableLeg',(x+dx,.29,z+dz),(.055,.58,.055),wood,.008))
    parts.append(box('LowerShelf',(x,.20,z),(.52,.035,.52),wood))
    finish(parts,'V3_SideTable')
    x,_,z=v['ConsolePosition']
    parts=[box('ConsoleTop',(x,.93,z),(1.8,.06,.62),wood,.02),
           box('ConsoleApron',(x,.77,z),(1.67,.25,.51),wood)]
    for dx in (-.77,.77):
        for dz in (-.22,.22): parts.append(box('ConsoleLeg',(x+dx,.39,z+dz),(.085,.76,.085),wood,.012))
    for dx in (-.43,.43):
        parts += [box('DrawerFront',(x+dx,.79,z+.274),(.75,.18,.024),wood),
                  rod('DrawerPull',(x+dx-.075,.79,z+.31),(x+dx+.075,.79,z+.31),.013,brass)]
    finish(parts,'V3_ConsoleTable')
    x,_,z=v['BenchPosition']
    parts=[box('BenchSeat',(x,.47,z),(.58,.14,1.78),fabric,.032),
           box('BenchBack',(x+.24,.82,z),(.065,.57,1.8),wood)]
    for dz in (-.76,.76):
        for dx in (-.23,.23): parts.append(box('BenchLeg',(x+dx,.23,z+dz),(.07,.46,.07),wood))
    for dz in (-.82,.82): parts.append(box('BenchArm',(x,.70,z+dz),(.61,.07,.085),wood))
    finish(parts,'V3_FoyerBench')
    if not gray:
        x,_,z=v['ConsolePosition']
        finish([lathe('CeramicVase',(x+.46,.965,z),[(.0,0),(.085,0),(.11,.09),(.115,.22),(.065,.31),(.046,.34),(.037,.34),(.045,.30),(.10,.21)],ceramic),
                box('Book',(x-.48,.995,z+.02),(.31,.06,.24),paper,.003),
                box('BookCover',(x-.48,1.03,z+.02),(.32,.012,.25),fabric,.003)],'V3_ConsoleProps')


def stairs():
    x,_,z=v['StairPosition']
    width,_,depth=v['StairSize']
    left,right=x-width/2,x+width/2
    south,north=z-depth/2,z+depth/2
    if gray:
        finish([box('StairBlock',(x,2.1,z),(width,4.2,depth),wood)],'V3_StairStructure')
        finish([box('ScreenBlock',(x,1.25,south-.015),(width,2.5,.10),wood)],'V3_StairScreen')
        return
    parts=[]
    run=3.0
    foot=south+.8
    tread=run/12
    for flight in range(2):
        cx=left+.575 if flight==0 else right-.575
        for i in range(12):
            height=(i+1)*.175+flight*2.1
            cz=foot+(i+.5)*tread if flight==0 else foot+run-(i+.5)*tread
            parts += [box('StairTread',(cx,height-.025,cz),(1.12,.05,tread+.025),wood,.012),
                      box('StairRiser',(cx,height-.11,cz-tread/2 if flight==0 else cz+tread/2),(1.08,.17,.028),wood,.004)]
        for side in (-1,1):
            sx=cx+side*.535
            start=(sx,.025+flight*2.1,foot) if flight==0 else (sx,2.1,foot+run)
            end=(sx,2.1+flight*2.1,foot+run) if flight==0 else (sx,4.2,foot)
            parts.append(rod('StairStringer',start,end,.075,wood,8))
            parts.append(rod('SlopingHandrail',(start[0],start[1]+.94,start[2]),(end[0],end[1]+.94,end[2]),.041,wood,20))
            for i in range(13):
                t=i/12
                point=tuple(start[j]+(end[j]-start[j])*t for j in range(3))
                parts.append(rod('StairBaluster',(point[0],point[1]+.05,point[2]),(point[0],point[1]+.91,point[2]),.021,wood,12))
    parts += [box('HalfLanding',(x,2.04,north-.4),(width,.12,.8),wood),
              box('TopLanding',(right-.575,4.14,south+.4),(1.15,.12,.8),wood),
              box('LandingSupport',(x,1.015,north-.4),(width,2.03,.78),wood)]
    # Closed side follows the pitch of the lower flight; it never buries the lower treads.
    mesh=bpy.data.meshes.new('SlopingStairPanel')
    profile=[(foot,.015),(foot+run,.015),(foot+run,2.035)]
    vertices=[(px,-pz,py) for px in (left+.015,left+.095) for pz,py in profile]
    mesh.from_pydata(vertices,[],[(0,2,1),(3,4,5),(0,1,4,3),(1,2,5,4),(2,0,3,5)])
    mesh.materials.append(wood);mesh.update()
    obj=bpy.data.objects.new('SlopingStairPanel',mesh);collection.objects.link(obj)
    project_uv(obj);parts.append(obj)
    for h in (.13,1.93):parts.append(box('LandingPanelRail',(left+.012,h,north-.4),(.035,.08,.74),wood))
    for px in (left+.03,right-.03):
        parts += [box('LandingNewel',(px,2.66,north-.05),(.12,1.16,.12),wood,.018),
                  rod('LandingRail',(px,3.13,north-.05),(px,3.13,north-.7),.042,wood)]
    parts.append(rod('LandingBackRail',(left,3.13,north-.04),(right,3.13,north-.04),.043,wood))
    for i in range(13):
        px=left+i*width/12
        parts.append(rod('LandingBaluster',(px,2.12,north-.04),(px,3.12,north-.04),.02,wood,12))
    finish(parts,'V3_StairStructure')
    screen=[]
    for px in (left,x,right):
        screen.append(box('ScreenPost',(px,1.31,south-.012),(.085,2.62,.085),wood,.012))
    for h in (.12,.82,1.62,2.57):
        screen.append(box('ScreenRail',(x,h,south-.012),(width,.065,.075),wood))
    for i in range(1,20):
        px=left+i*width/20
        screen.append(box('ScreenLattice',(px,1.32,south-.012),(.026,2.45,.042),wood,.003))
    finish(screen,'V3_StairScreen')


def make_rug(name, center, size):
    x,z=center; width,length=size
    parts=[box('RugBacking',(x,.009,z),(width,.018,length),rug_dark,.002),
           box('WovenField',(x,.019,z),(width-.21,.008,length-.21),rug,.001)]
    for inset,thickness,mat in [(.12,.025,rug_gold),(.20,.02,rug_dark)]:
        for side in (-1,1):
            parts.append(box('RugBorder',(x+side*(width/2-inset),.025,z),(thickness,.005,length-2*inset),mat,.001))
            parts.append(box('RugBorder',(x,.025,z+side*(length/2-inset)),(width-2*inset,.005,thickness),mat,.001))
    if not gray:
        for i in range(max(1,int(length/1.2))):
            cz=z-length/2+.65+i*1.15
            if cz>z+length/2-.4: break
            # Restrained lozenge motifs are flat woven inlays, not noisy plaid.
            diamond=box('WovenMedallion',(x,.025,cz),(.28,.004,.28),rug_gold,.001)
            diamond.rotation_euler.z=math.pi/4
            parts.append(diamond)
    finish(parts,name,density=3)


def sconce(name, position, yaw=0):
    parent=empty(name,parent=hall_root)
    parts=[box('Backplate',(0,0,0),(.12,.30,.035),brass,.04),
           rod('Stem',(0,-.035,.02),(0,-.035,.17),.018,brass,24),
           rod('Upright',(0,-.035,.17),(0,.08,.17),.018,brass,24),
           lathe('ShadeFoot',(0,.06,.17),[(.0,0),(.063,0),(.068,.035),(.045,.05)],brass),
           lathe('MilkGlass',(0,.10,.17),[(.043,0),(.058,.03),(.105,.23),(.109,.25),(.101,.25),(.097,.23),(.049,.03),(.038,0)],lamp,40)]
    finish(parts,name+'_Fixture',parent)
    parent.location=(position[0],-position[2],position[1])
    parent.rotation_euler.z=math.radians(yaw)


def decorations():
    make_rug('V3_FoyerRug',(0,-4.5),(2.5,4.6))
    make_rug('V3_HallRunner',(0,6),(1.75,10.3))
    for i,(p,yaw) in enumerate([
        ((-2.45,2.25,-7.94),0),((2.45,2.25,-7.94),0),
        ((-4.94,2.55,-1.3),90),((4.94,3.25,-3.3),-90),
        ((-3.44,2.55,5.5),90),((3.44,2.55,5.4),-90),
        ((-3.44,2.55,10.5),90),((3.44,2.55,10.4),-90)]):
        sconce(f'V3_Sconce{i+1:02}',p,yaw)
    for name,z,h,ceiling in [('Foyer',-3.8,4.8,v['FoyerHeight']),('Hall',6,3.35,v['HallHeight'])]:
        parts=[rod('Suspension',(0,h+.22,z),(0,ceiling,z),.018,iron),
               lathe('CeilingRose',(0,ceiling-.04,z),[(0,0),(.13,0),(.13,.04),(0,.04)],brass),
               lathe('PendantShade',(0,h-.18,z),[(.16,0),(.25,.05),(.28,.29),(.22,.38),(.20,.38),(.255,.29),(.23,.06),(.14,0)],lamp,40)]
        for angle in range(0,360,90):
            a=math.radians(angle)
            parts.append(rod('PendantFrame',(.25*math.cos(a),h-.13,z+.25*math.sin(a)),
                             (.22*math.cos(a),h+.2,z+.22*math.sin(a)),.012,brass))
        finish(parts,'V3_'+name+'Pendant')
    if not gray:
        for i,(x,z) in enumerate([(-3.43,6.9),(3.43,8.15),(-4.93,-6.6)]):
            inward=1 if x<0 else -1
            parts=[box('PictureBacking',(x,2.17,z),(.04,.79,1.0),paper),
                   box('PictureMat',(x+inward*.025,2.17,z),(.012,.67,.89),paper)]
            for h in (1.75,2.59):parts.append(box('PictureFrame',(x+inward*.04,h,z),(.075,.055,1.08),wood))
            for dz in (-.51,.51):parts.append(box('PictureFrame',(x+inward*.04,2.17,z+dz),(.075,.84,.055),wood))
            # Reuse the existing decorative sample image unchanged, with explicit 0..1 UVs.
            px=x+inward*.039
            vertices=[(px,-(z-.39),2.17-.2925),(px,-(z+.39),2.17-.2925),
                      (px,-(z+.39),2.17+.2925),(px,-(z-.39),2.17+.2925)]
            mesh=bpy.data.meshes.new('LandscapeCanvas')
            mesh.from_pydata(vertices,[],[(0,1,2,3)] if x>0 else [(3,2,1,0)])
            mesh.materials.append(painting)
            uv=mesh.uv_layers.new(name='UVMap')
            coordinates=[(0,0),(1,0),(1,1),(0,1)]
            for loop in mesh.loops:uv.data[loop.index].uv=coordinates[loop.vertex_index]
            obj=bpy.data.objects.new('LandscapeCanvas',mesh);collection.objects.link(obj);parts.append(obj)
            finish(parts,f'V3_Gallery{i+1:02}')


def configure_export():
    exporter.SPECS['manor'].update(source=SOURCE,glb=GLB,manifest=MANIFEST,
        baseline='assets/generated/dream_escape/3d/dream-manor-hall-v2.glb',
        required=REQUIRED,actualTextureMemory=True)


def add_manifest_layout():
    path=root/MANIFEST
    data=json.loads(path.read_text('utf-8'))
    data['layoutVersion']=3
    data['layoutSource']=LAYOUT.as_posix()
    data['layoutSha256']=exporter.sha256(root/LAYOUT)
    data['layout']=v
    data['visualAcceptance']='pending-human-review'
    data['lightingDecision']='Color ambient with zero sky contribution, warm practical lights, cool neutral fill, two ReflectionProbes; no SSAO or LightmapGI'
    data['exteriorCompatibility']='Historical exterior v1 is not dimensionally representative of interior v3 and is not loaded.'
    path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n','utf-8')


def record_provenance():
    path=root/'assets/generated/provenance.json'
    text=path.read_text('utf-8')
    manifest=json.loads((root/MANIFEST).read_text('utf-8'))
    record={'id':'dream_manor_hall_runtime_v3','sourceFile':SOURCE.as_posix(),
        'gameFile':GLB.as_posix().removeprefix('assets/generated/'),'status':'adopted-runtime-default',
        'generator':'Blender Python procedural offline modeling','model':'Blender 5.2.0 LTS',
        'date':'2026-09-09','script':'scripts/tools/remodel_manor_v3.py',
        'prompt':'Approved 10x8x6.6m foyer, 7x12x4.2m hall, closed scenic return stair, Arts and Crafts old house, warm lamps and readable cool shadows. Four rooms in unchanged story order; front door remains locked.',
        'referenceImages':['art/concepts/dream_manor/manor-foyer-cinematic-target-v2.png'],
        'reusedDecorativeImage':{'file':'art/blender/dream_manor/textures-v3/landscape-painting.png',
            'sha256':exporter.sha256(root/'art/blender/dream_manor/textures-v3/landscape-painting.png'),
            'origin':'Existing offline sample from build_environment_material_textures.py, landscape(), random seed 10103. Copied unchanged; no new raster generation.'},
        'parameters':{'layoutSource':LAYOUT.as_posix(),'layoutVersion':3,'seed':0,'realtimeGeneration':False},
        'postprocessing':['metric planar UV projection','applied mesh modifiers','packed CC0 PBR textures','GLB export without characters, cameras or lights'],
        'sourceSha256':manifest['sourceSha256'],'runtimeGlbSha256':manifest['runtimeGlbSha256'],
        'sourceAssets':manifest['sourceAssets']}
    data=json.loads(text)
    # Preserve unrelated records and their formatting; replace/append only this record.
    records=data if isinstance(data,list) else data['assets']
    existing=next((r for r in records if r.get('id')==record['id']),None)
    formatted=json.dumps(record,ensure_ascii=False,indent=2)
    if existing:
        start=text.rfind('{',0,text.index('"id": "'+record['id']+'"'))
        # Find the enclosing object with the JSON decoder, not a brace regex.
        _,length=json.JSONDecoder().raw_decode(text[start:])
        text=text[:start]+formatted+text[start+length:]
    else:
        end=text.rfind(']')
        text=text[:end].rstrip()+',\n'+formatted+'\n'+text[end:]
    json.loads(text)
    path.write_text(text,'utf-8')


def build():
    global collection,hall_root,wood,plaster,floor,stone,brass,iron,fabric,glass,lamp,paper,ceramic,rug,rug_dark,rug_gold,painting
    bpy.ops.wm.read_factory_settings(use_empty=True)
    collection=geo.collection('ManorV3')
    geo.collection('CinematicSampleDetails');geo.collection('CinematicFullDetails');geo.collection('Lookdev')
    hall_root=empty('DreamManorHall')
    wood=pbr('M_V3_Manor_Wood',(.12,.065,.032),.72,asset='black_oak_veneer',tint=(.34,.17,.068),normal_strength=.30)
    plaster=pbr('M_V3_Manor_Plaster',(.48,.40,.29),.88,asset='beige_wall_001',normal_strength=.18)
    floor=pbr('M_V3_Manor_Floor',(.28,.25,.20),.73,asset='old_mosaic_floor',normal_strength=.28)
    stone=pbr('M_V3_Manor_Stone',(.23,.22,.19),.82)
    brass=pbr('M_V3_Manor_Brass',(.38,.23,.085),.38,.72)
    iron=pbr('M_V3_Manor_Iron',(.027,.030,.028),.68,.35)
    fabric=pbr('M_V3_Manor_Upholstery',(.14,.17,.12),.93,asset='fabric_pattern_07',diffuse=False,normal_strength=.18)
    rug=pbr('M_V3_Manor_Rug',(.16,.075,.056),.94,asset='fabric_pattern_07',diffuse=False,normal_strength=.16)
    rug_dark=pbr('M_V3_Manor_RugBorder',(.07,.052,.035),.94)
    rug_gold=pbr('M_V3_Manor_RugInlay',(.25,.20,.12),.94)
    glass=pbr('M_V3_Manor_Glass',(.51,.44,.27),.25,emission=.25)
    lamp=pbr('M_V3_Manor_LampGlass',(.65,.51,.31),.40,emission=.55)
    paper=pbr('M_V3_Manor_Paper',(.34,.29,.20),.94)
    ceramic=pbr('M_V3_Manor_Ceramic',(.15,.24,.22),.27)
    painting=pbr('M_V3_Manor_Painting',(.23,.25,.20),.94)
    if not gray:
        node=painting.node_tree.nodes.new('ShaderNodeTexImage')
        node.image=bpy.data.images.load(str(root/'art/blender/dream_manor/textures-v3/landscape-painting.png'))
        painting.node_tree.links.new(node.outputs['Color'],painting.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
    architecture();doors();furniture();stairs();decorations()
    bpy.context.view_layer.update()
    for obj in exporter.export_objects('manor'):
        if obj.type!='MESH':continue
        if not obj.data.uv_layers:project_uv(obj)
        bpy.context.view_layer.objects.active=obj
        for modifier in list(obj.modifiers):bpy.ops.object.modifier_apply(modifier=modifier.name)
    for image in bpy.data.images:
        if image.source=='FILE':image.pack()
    scene=bpy.context.scene
    for key,value in {'sampleOnly':False,'runtimeDefault':True,'fullScene':True,'visualDirectionApproved':True,
                      'visualAcceptance':'pending-human-review','layoutVersion':3,'graybox':gray}.items():scene[key]=value
    scene.unit_settings.system='METRIC'
    scene.unit_settings.scale_length=1
    (root/SOURCE).parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(root/SOURCE))
    exporter.run('manor','export',root)
    add_manifest_layout()
    if not gray:record_provenance()


def check():
    exporter.run('manor','check',root)
    data=json.loads((root/MANIFEST).read_text('utf-8'))
    if data.get('layoutSha256')!=exporter.sha256(root/LAYOUT) or data.get('layout')!={k:list(val) if isinstance(val,tuple) else val for k,val in v.items()}:
        raise RuntimeError('Manor layout manifest is stale')
    if data['geometry']!=exporter.summarize('manor'):
        raise RuntimeError('Manor geometry manifest is stale')
    if data['textureMemoryEstimateMiB']!=exporter.texture_memory_mib('manor'):
        raise RuntimeError('Manor texture memory estimate is stale')
    print('MANOR_V3_CHECK_PASS foyer=10x8x6.6 hall=7x12x4.2 stair=closed readOnly=true')


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('mode',choices=['graybox','build','export','check'])
    parser.add_argument('--root',required=True)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    root=Path(args.root).resolve();v=read_layout(root);gray=args.mode=='graybox'
    configure_export()
    if args.mode in ('graybox','build'):build()
    elif args.mode=='export':
        exporter.run('manor','export',root);add_manifest_layout();record_provenance()
    else:check()
