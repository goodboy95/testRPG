"""Offline 12x10x4m music classroom, using MusicRoomLayout3D as layout authority.

Blender 5.2 --background --python this_file -- graybox|build|export|check --root .
Historical procedural source is preserved under art/blender/dream_music/history.
"""
from __future__ import annotations
import argparse
import json
import math
from pathlib import Path
import re
import sys
import bpy
from mathutils import Vector, Matrix

sys.path.insert(0, str(Path(__file__).resolve().parent))
import remodel_manor_v3 as mesh
import export_environment_cinematic_samples as exporter
from remodel_classroom_v6 import project_uv

SOURCE = Path('art/blender/dream_music/music-room-art-pass-v2.blend')
GLB = Path('assets/generated/dream_escape/3d/music-room-art-pass-v2.glb')
MANIFEST = GLB.with_name(GLB.stem + '-manifest.json')
LAYOUT = Path('scripts/presentation/MusicRoomLayout3D.cs')
REQUIRED = ['MusicRoom', 'Room_Floor', 'Room_Ceiling', 'Wall_West', 'WindowGlass',
            'Door_West_Hinge', 'EquipmentDoorHinge', 'EquipmentWalls', 'Piano', 'PianoBench',
            'CalibrationDesk', 'ScoreDesk', 'ToolsDesk', 'RollDesk', 'PartsTray', 'PhotoCabinet',
            'AcousticCabinet', 'EquipmentShelf', 'Chair01', 'Chair06', 'Score_Revealed',
            'Metronome_Weight', 'Metronome_Solved', 'Calibration_Solved', 'Roll_Installed',
            'Acoustic_Solved', 'Photo_Solved', 'Pickup_Mallet', 'Pickup_Cone', 'Pickup_Weight',
            'Pickup_Roll', 'Pickup_Wax', 'SideObservationPlate', 'TempoNoticeBoard', 'RollTemplateBoard',
            'FrontWavesBoard', 'Metronome', 'RoomFixtures']


def read_layout(root):
    source = (root / LAYOUT).read_text('utf-8')
    values = {k: float(n) for k, n in re.findall(r'const float (\w+) = (-?[\d.]+)f;', source)}
    values.update({k: tuple(float(n.strip().removesuffix('f')) for n in ns.split(','))
                   for k, ns in re.findall(r'public static readonly Vector3 (\w+) = new\(([^)]+)\);', source)})
    return values


def box(name, p, size, material, bevel=.008):
    return mesh.box(name, p, size, material, bevel)


def group(name, parts, parent=None):
    return mesh.finish(parts, name, parent)


def anchor(name, p=(0, 0, 0), parent=None):
    return mesh.empty(name, p, parent or room)


def board(name, p, width, height, facing='south', frame=True):
    x, y, z = p
    parts = []
    if facing == 'south':
        parts.append(box('PaperBacking', p, (width, height, .035), paper, .003))
        if frame:
            for dx in (-width/2-.022, width/2+.022):
                parts.append(box('FrameStile', (x+dx, y, z+.008), (.045, height+.09, .065), wood))
            for dy in (-height/2-.022, height/2+.022):
                parts.append(box('FrameRail', (x, y+dy, z+.008), (width+.09, .045, .065), wood))
    else:
        parts.append(box('PaperBacking', p, (.035, height, width), paper, .003))
        if frame:
            for dz in (-width/2-.022, width/2+.022):
                parts.append(box('FrameStile', (x+.008, y, z+dz), (.065, height+.09, .045), wood))
            for dy in (-height/2-.022, height/2+.022):
                parts.append(box('FrameRail', (x+.008, y+dy, z), (.065, .045, width+.09), wood))
    return group(name, parts)


def architecture():
    w, d, h = v['Width'], v['Depth'], v['Height']
    slab=box('FloorSlab', (0, -.1, 0), (w, .2, d), floor, .003)
    for uv in slab.data.uv_layers.active.data:uv.uv*=.25
    group('Room_Floor', [slab])
    group('Room_Ceiling', [box('CeilingSlab', (0, h+.1, 0), (w, .2, d), plaster, .003)])
    t=v['WallThickness'];west=-w/2;east=w/2;north=-d/2;south=d/2
    for name, p, size in [('Wall_North', (0, h/2, north-t/2), (w+2*t, h, t)),
                          ('Wall_South', (0, h/2, south+t/2), (w+2*t, h, t)),
                          ('Wall_East', (east+t/2, h/2, 0), (t, h, d))]:
        group(name, [box(name, p, size, plaster, .003)])
    # True window openings; the west exit retains its existing facing.
    openings = [(-3.45, 1.45, 1.25, 3.15), (-1.45, 1.45, 1.25, 3.15),
                (.55, 1.45, 1.25, 3.15), (v['ExitDoor'][2], v['DoorWidth'], 0, v['DoorHeight'])]
    parts, frames, panes = [], [], []
    cursor = -5
    for center, width, low, high in openings:
        a, b = center-width/2, center+width/2
        if a > cursor: parts.append(box('WestPier', (-6.1, 2, (cursor+a)/2), (.2, 4, a-cursor), plaster))
        if low > 0: parts.append(box('WestSillWall', (-6.1, low/2, center), (.2, low, width), plaster))
        parts.append(box('WestHeader', (-6.1, (high+4)/2, center), (.2, 4-high, width), plaster))
        cursor = b
        if low == 0: continue
        for dz in (-width/2, 0, width/2):
            frames.append(box('WindowStile', (-5.99, (low+high)/2, center+dz), (.14, high-low+.09, .065), teal))
        for y in (low, (low+high)/2, high):
            frames.append(box('WindowRail', (-5.99, y, center), (.14, .07, width+.08), teal))
        frames.append(box('WindowSill', (-5.87, low-.06, center), (.4, .1, width+.24), wood))
        panes.append(box('GlassPane', (-6.025, (low+high)/2, center), (.02, high-low-.06, width-.04), glass, .001))
    parts.append(box('WestEndPier', (-6.1, 2, (cursor+5)/2), (.2, 4, 5-cursor), plaster))
    group('Wall_West', parts); group('WindowFrames', frames); group('WindowGlass', panes)
    skirt=[]
    for p,size in [((0,.58,-4.985),(12,1.16,.025)),((0,.58,4.985),(12,1.16,.025)),
                   ((5.985,.58,0),(.025,1.16,10)),((-5.985,.58,-1.075),(.025,1.16,7.85)),
                   ((-5.985,.58,4.625),(.025,1.16,.75))]:
        skirt.append(box('PaintedDado',p,size,teal,.002))
    for p,size in [((0,.065,-4.96),(12,.13,.045)),((0,.065,4.96),(12,.13,.045)),
                   ((5.96,.065,0),(.045,.13,10)),((-5.96,.065,-1.075),(.045,.13,7.85)),
                   ((-5.96,.065,4.625),(.045,.13,.75))]:
        skirt.append(box('Skirting',p,size,wood))
    group('WallSkirting',skirt)
    # The equipment room is a real 3.2m square, not a solid furniture obstacle.
    ew,es=v['EquipmentWest'],v['EquipmentSouth'];dx=v['EquipmentDoor'][0];dw=v['DoorWidth'];dh=v['DoorHeight']
    left,right=dx-dw/2,dx+dw/2
    parts=[box('PartitionWest',(ew,h/2,(north+es)/2),(.18,h,es-north),plaster),
           box('PartitionSouthLeft',((ew+left)/2,h/2,es),(left-ew,h,.18),plaster),
           box('PartitionSouthRight',((right+east)/2,h/2,es),(east-right,h,.18),plaster),
           box('PartitionLintel',(dx,(dh+h)/2,es),(dw,h-dh,.18),plaster),
           box('PartitionDado',(ew-.107,.6,(north+es)/2),(.035,1.2,es-north),teal)]
    group('EquipmentWalls',parts)


def doors():
    # Exit leaf is authored along +Z, equipment leaf along -X. Local pivots are stable.
    x,_,z=v['ExitHinge']; h=v['DoorHeight']; w=v['DoorWidth']
    hinge=anchor('Door_West_Hinge',v['ExitHinge'])
    parts=[box('ExitLeaf',(x,h/2,z+w/2),(.09,h,w-.025),wood)]
    for y in (.48,1.4,2.23):parts.append(box('ExitPanel',(x+.055,y,z+w/2),(.035,.56,w-.25),teal))
    parts.append(mesh.rod('Handle',(x+.14,1.04,z+1.08),(x+.14,1.04,z+1.26),.018,brass))
    group('ExitLeaf',parts,hinge)
    parts=[]
    for zz in (z-.055,z+w+.055):parts.append(box('ExitJamb',(x+.035,h/2,zz),(.2,h+.1,.11),wood))
    parts.append(box('ExitLintel',(x+.035,h+.055,z+w/2),(.2,.11,w+.22),wood))
    group('ExitJoinery',parts)
    x,_,z=v['EquipmentHinge']; hinge=anchor('EquipmentDoorHinge',v['EquipmentHinge'])
    parts=[]
    # Small observation pane with an opaque lower panel and upper header.
    parts.extend([box('DoorLower',(x-w/2,.61,z),(w-.02,1.22,.09),teal),
                  box('DoorHeader',(x-w/2,2.22,z),(w-.02,.66,.09),teal)])
    for xx in (x-w+.09,x-.09):parts.append(box('DoorStile',(xx,1.55,z),(.18,.7,.09),wood))
    group('EquipmentDoorGlass',[box('ObservationPane',(x-w/2,1.55,z),(w-.36,.66,.025),glass,.001)],hinge)
    for yy in (1.2,1.9):parts.append(box('DoorWindowRail',(x-w/2,yy,z-.01),(w-.17,.07,.12),wood))
    for side in (-1,1):
        parts.append(box('LockPlate',(x-w+.22,1.03,z+side*.065),(.1,.23,.02),brass))
        parts.append(mesh.rod('DoorLever',(x-w+.23,1.03,z+side*.10),(x-w+.4,1.03,z+side*.10),.014,brass))
    group('EquipmentDoorLeaf',parts,hinge)
    for y in (.35,1.25,2.25):
        group('EquipmentHingeHardware'+str(y),[mesh.rod('HingePin',(x,y-.08,z),(x,y+.08,z),.027,iron)],hinge)
    group('EquipmentDoorFrame',[
        box('JambL',(x-w-.055,h/2,z),(.11,h+.1,.23),wood),
        box('JambR',(x+.055,h/2,z),(.11,h+.1,.23),wood),
        box('Lintel',(x-w/2,h+.055,z),(w+.22,.11,.23),wood)])
    anchor('DoorNumberPlate',(4.36,1.04,-1.865),hinge)
    group('EquipmentNumbersBacking',[box('InnerPlaque',(4.35,1.04,-1.858),(1.08,.24,.028),paper,.004)],hinge)
    group('EquipmentLockControls',[box('LockPlate',(4.35,1.02,-1.73),(.72,.19,.035),iron),
        *[box('LockKey',(4.08+i*.18,1.02,-1.70),(.13,.13,.03),brass,.01) for i in range(4)]],hinge)
    board('EquipmentNameBoard',(4.35,2.92,-1.67),1.1,.24)
    board('RoomNameBoard',(-5.94,2.86,3.55),1.15,.26,'east')


def table(name):
    x,y,z=v[name+'Position']; w,h,d=v[name+'Size']
    parts=[box('Top',(x,h-.035,z),(w,.07,d),wood,.015)]
    for dx in (-w/2+.08,w/2-.08):
        for dz in (-d/2+.08,d/2-.08):parts.append(box('Leg',(x+dx,(h-.07)/2,z+dz),(.065,h-.07,.065),iron))
    for dz in (-d/2+.09,d/2-.09):parts.append(box('Apron',(x,h-.16,z+dz),(w-.1,.17,.035),wood))
    if not gray:
        for dx in (-w/2+.08,w/2-.08):
            parts.append(mesh.rod('Brace',(x+dx,.19,z-d/2+.07),(x+dx,.19,z+d/2-.07),.018,iron))
        for dx in (-w*.37,w*.32):
            parts.append(box('WornTableEdge',(x+dx,h+.0005,z+d/2-.012),(.13,.0015,.012),wear,.001))
    return group(name,parts)


def turn(obj, pivot, axis, angle):
    p=Vector((pivot[0],-pivot[2],pivot[1]))
    obj.matrix_world=Matrix.Translation(p) @ Matrix.Rotation(math.radians(angle),4,axis) @ Matrix.Translation(-p) @ obj.matrix_world
    return obj


def piano():
    x,_,z=v['PianoPosition'];parts=[]
    parts.extend([box('RearSoundboard',(x,.71,z+.34),(1.46,1.1,.10),wood),
        box('LowerPanel',(x,.38,z+.22),(1.39,.57,.12),ebony),
        box('UpperPanel',(x,1.01,z+.12),(1.4,.45,.15),ebony),
        box('TopLid',(x,1.27,z+.08),(1.62,.06,.64),ebony,.015),
        box('Keybed',(x,.735,z-.17),(1.6,.09,.44),ebony),
        box('FallboardRail',(x,.845,z-.035),(1.38,.08,.065),wood)])
    for dx in (-.755,.755):
        parts.append(box('Cheek',(x+dx,.79,z+.02),(.11,.96,.68),ebony,.012))
        parts.append(box('Foot',(x+dx,.075,z+.02),(.13,.15,.7),ebony,.013))
        parts.append(box('Console',(x+dx,.38,z-.26),(.08,.5,.1),ebony))
        for zz in (z-.24,z+.27):parts.append(mesh.rod('Caster',(x+dx-.035,.035,zz),(x+dx+.035,.035,zz),.035,brass))
    key_width=1.23/52
    for i in range(52):
        px=x-.615+(i+.5)*key_width
        parts.append(box('IvoryKey',(px,.795,z-.22),(key_width-.001,.045,.23),ivory,.001))
        # Black key gaps follow the repeating diatonic pattern, not decorative stripes.
        if i<51 and i%7 in (0,2,3,5,6):
            parts.append(box('BlackKey',(px+key_width/2,.835,z-.155),(.0135,.044,.145),rubber,.001))
    for dx in (-.07,0,.07):
        parts.append(mesh.rod('PedalStem',(x+dx,.09,z+.05),(x+dx,.07,z-.30),.013,brass))
        parts.append(box('PedalToe',(x+dx,.074,z-.31),(.043,.021,.08),brass,.008))
    for y in (.17,.61,1.18):
        parts.append(box('PanelBead',(x,y,z-.003),(1.36,.025,.018),wood,.004))
    # Raised music rest has a real ledge and support, kept within the piano's footprint.
    parts.extend([box('MusicRest',(x,.98,z-.035),(.76,.25,.035),ebony),
                  box('ScoreLedge',(x,.865,z-.08),(.80,.035,.1),wood)])
    group('Piano',parts)
    bx,_,bz=v['BenchPosition'];parts=[box('BenchSeat',(bx,.49,bz),(.9,.06,.46),wood,.018)]
    for dx in (-.36,.36):
        for dz in (-.15,.15):parts.append(box('BenchLeg',(bx+dx,.24,bz+dz),(.055,.48,.055),ebony))
    for dz in (-.17,.17):parts.append(box('BenchApron',(bx,.405,bz+dz),(.82,.1,.04),ebony))
    group('PianoBench',parts)


def cabinets():
    # Photo lightbox has a glazed, framed paper support above a timber cupboard.
    x,_,z=v['PhotoCabinetPosition'];parts=[]
    parts.extend([box('Cupboard',(x,.54,z),(.58,1.02,1.1),wood),
                  box('LightboxCase',(x,1.36,z),(.52,.96,1.1),teal)])
    for zz in (z-.49,z+.49):parts.append(box('LightboxRail',(x+.31,1.36,zz),(.065,.92,.065),wood))
    for y in (.91,1.81):parts.append(box('LightboxRail',(x+.31,y,z),(.065,.06,1.04),wood))
    parts.append(box('Diffuser',(x+.291,1.36,z),(.025,.8,.9),ivory))
    # Opaque stored negative sheets: the readable photograph remains in its existing puzzle diagram.
    for zz in (z-.23,z+.23):
        parts.append(box('NegativeSleeve',(x+.308,1.37,zz),(.009,.42,.33),film,.001))
        for y in (1.15,1.59):parts.append(box('FilmClip',(x+.325,y,zz),(.018,.035,.06),brass,.003))
    for zz in (z-.28,z+.28):
        parts.append(box('CupboardPanel',(x+.302,.51,zz),(.028,.74,.49),wood))
        parts.append(mesh.rod('Pull',(x+.334,.56,zz-.06),(x+.334,.56,zz+.06),.012,brass))
    group('PhotoCabinet',parts)
    group('PhotoGlass',[box('LightboxPane',(x+.339,1.36,z),(.01,.79,.9),glass,.001)])
    # Open storage rack, shelf surfaces correspond to the wax-paper pickup height.
    x,_,z=v['EquipmentShelfPosition'];w,h,d=v['EquipmentShelfSize'];parts=[]
    for zz in (z-d/2+.045,z+d/2-.045):
        for xx in (x-w/2+.04,x+w/2-.04):parts.append(box('ShelfUpright',(xx,h/2,zz),(.045,h,.045),iron))
    for y in (.12,.78,1.42,2.10):parts.append(box('ShelfBoard',(x,y,z),(w,.045,d),wood))
    parts.append(box('ShelfBack',(x+w/2-.018,h/2,z),(.035,h,d),teal))
    group('EquipmentShelf',parts)
    parts=[]
    for zz in (-4.32,-3.66,-3.0):
        parts.extend([box('InstrumentCase',(5.59,1.69,zz),(.42,.44,.44),film,.025),
                      box('CaseStrap',(5.366,1.69,zz),(.015,.44,.045),brass,.003)])
    group('StoredInstrumentCases',parts)
    x,_,z=v['AcousticCabinetPosition'];parts=[]
    parts.extend([box('ResonatorBack',(x+.29,1.2,z),(.12,2.2,2.55),wood),
                  box('ResonatorFront',(x-.31,1.22,z),(.065,1.38,2.45),teal)])
    for zz in (z-1.26,z+1.26):parts.append(box('CabinetSide',(x,1.15,zz),(.7,2.3,.075),wood))
    for y in (.08,.58,2.25):parts.append(box('CabinetShelf',(x,y,z),(.7,.075,2.6),wood))
    for i in range(4):
        zz=z-.9+i*.6
        parts.append(mesh.rod('SoundCup',(x-.29,1.25,zz),(x-.35,1.25,zz),.18,brass,32))
        parts.append(mesh.rod('SoundMembrane',(x-.35,1.25,zz),(x-.372,1.25,zz),.14,film,32))
        for yy in (.95,1.55):parts.append(mesh.rod('MountScrew',(x-.343,yy,zz),(x-.36,yy,zz),.014,brass,12))
    group('AcousticCabinet',parts)


def instruments():
    parts=[]
    # Woodblock/wood fish: turned body, visible striking slot and felt base.
    parts.append(mesh.lathe('WoodFish',(.82,.825,-3.37),[(.04,0),(.14,.025),(.19,.12),(.16,.22),(.06,.255),(.01,.26)],wood,32))
    parts.append(box('WoodFishSlit',(.82,1.064,-3.35),(.23,.006,.033),rubber,.003))
    parts.append(box('FeltPad',(.82,.81,-3.37),(.42,.012,.34),film,.025))
    # Suspended triangle with an open corner, wire tie, and a steel support.
    parts.append(mesh.rod('TriangleStand',(1.40,.81,-3.48),(1.40,1.53,-3.48),.012,iron))
    parts.append(mesh.rod('TriangleStandFoot',(1.18,.817,-3.48),(1.62,.817,-3.48),.018,iron))
    parts.append(mesh.rod('TriangleArm',(1.40,1.51,-3.48),(1.40,1.51,-3.20),.01,iron))
    a,b,c=(1.40,1.44,-3.20),(1.24,1.1,-3.20),(1.56,1.1,-3.20)
    parts.extend([mesh.rod('TriangleLeft',a,b,.009,silver),mesh.rod('TriangleBase',b,(1.52,1.1,-3.20),.009,silver),
                  mesh.rod('TriangleRight',c,a,.009,silver),mesh.rod('TriangleTie',(1.4,1.53,-3.2),a,.003,film,8)])
    parts.append(mesh.lathe('Handbell',(2.03,.81,-3.4),[(.125,0),(.14,.018),(.115,.06),(.066,.20),(.028,.255),(.026,.28)],brass,32))
    parts.append(mesh.rod('BellHandle',(2.03,1.08,-3.4),(2.03,1.3,-3.4),.023,wood))
    parts.append(mesh.lathe('BellFinial',(2.03,1.3,-3.4),[(.023,0),(.035,.022),(.015,.042)],wood,20))
    group('CalibrationInstruments',parts)
    # Four physical leads run to the side receiver, with the piano lead following the room edge.
    parts=[]
    for i in range(4):
        z=-3.35-i*.10
        parts.append(mesh.rod('ReceiverTube',(2.33,.91,z),(2.91,.91,z),.012,brass))
        parts.append(mesh.rod('ReceiverSocket',(2.92,.91,z),(2.95,.91,z),.032,iron))
    parts.append(box('ReceiverHousing',(2.66,.94,-3.51),(.1,.22,.48),iron))
    path=[(2.66,.94,-3.65),(2.66,2.78,-3.65),(2.66,2.78,-1.66),
          (5.93,2.78,-1.66),(5.93,.13,-1.66),(5.93,.13,4.9),(.6,.13,4.9)]
    parts.extend(mesh.rod('PianoChannel',a,b,.011,iron) for a,b in zip(path,path[1:]))
    for x in (3.1,3.7,4.3,4.9,5.5):
        parts.append(box('ConduitClip',(x,2.78,-1.679),(.045,.055,.045),brass,.003))
    group('AcousticConnections',parts)


def school_details():
    parts=[]
    # Teaching staff is intentionally empty, so decoration cannot disclose a puzzle sequence.
    parts.append(box('Blackboard',(-3.0,2.3,-4.91),(4.5,1.16,.075),film))
    for xx in (-5.31,-.69):parts.append(box('BoardStile',(xx,2.3,-4.85),(.1,1.3,.1),wood))
    for y in (1.66,2.94):parts.append(box('BoardRail',(-3, y,-4.85),(4.72,.09,.1),wood))
    for y in (2.02,2.13,2.24,2.35,2.46):parts.append(box('StaffRule',(-3, y,-4.864),(3.8,.009,.004),ivory,.001))
    parts.append(box('ChalkTray',(-3,1.65,-4.79),(4.65,.055,.2),wood))
    group('TeachingBlackboard',parts)
    parts=[]
    # Clamp-on music stands remain inside the chair footprints and add no hidden walking obstacles.
    for i in (1,4):
        x,_,z=v[f'Chair{i:02}']
        parts.append(mesh.rod('StandStem',(x-.22,.32,z-.20),(x-.22,1.18,z-.20),.011,iron))
        parts.append(box('MusicStand',(x,1.16,z-.24),(.44,.3,.025),iron,.008))
        parts.append(box('StandLedge',(x,1.02,z-.28),(.46,.024,.08),iron))
    group('RehearsalMusicStands',parts)
    parts=[]
    for x,z in [(-2.7,-2.6),(.6,-2.6),(-2.7,1.45),(.6,1.45)]:
        parts.append(box('FluorescentHousing',(x,3.78,z),(1.35,.16,.28),teal,.015))
        for dz in (-.065,.065):parts.append(mesh.rod('Tube',(x-.57,3.685,z+dz),(x+.57,3.685,z+dz),.023,lamp,16))
        for dx in (-.45,.45):parts.append(mesh.rod('Suspension',(x+dx,3.84,z),(x+dx,3.99,z),.008,iron))
    parts.append(mesh.rod('PendantStem',(4.35,3.97,-3.4),(4.35,3.62,-3.4),.012,iron))
    parts.append(mesh.lathe('EquipmentShade',(4.35,3.42,-3.4),[(.25,0),(.25,.02),(.17,.13),(.07,.20)],teal,32))
    parts.append(mesh.lathe('EquipmentBulb',(4.35,3.40,-3.4),[(.055,0),(.07,.06),(.035,.13)],lamp,24))
    parts.append(mesh.rod('FanStem',(.1,3.97,-.4),(.1,3.42,-.4),.026,iron))
    parts.append(mesh.lathe('FanMotor',(.1,3.32,-.4),[(.1,0),(.16,.045),(.16,.13),(.09,.17)],teal,32))
    for angle in (0,120,240):
        blade=box('FanBlade',(.48,3.34,-.4),(.68,.024,.18),wood,.035)
        parts.append(turn(blade,(.1,3.34,-.4),'Z',angle))
    group('RoomFixtures',parts)
    parts=[]
    # Brass task lamp, the runtime point light sits inside its shade.
    parts.append(mesh.lathe('LampBase',(-.55,1.3,4.72),[(.105,0),(.11,.025),(.06,.06)],brass,24))
    parts.append(mesh.rod('LampStem',(-.55,1.34,4.72),(-.55,1.91,4.72),.014,brass))
    parts.append(mesh.rod('LampNeck',(-.55,1.91,4.72),(-.4,2.05,4.25),.012,brass))
    parts.append(mesh.lathe('LampShade',(-.4,1.99,4.25),[(.16,0),(.15,.035),(.065,.16),(.025,.18)],teal,24))
    group('PianoTaskLamp',parts)
    # Localized papers are supported on tabletop easels and wall frames.
    parts=[]
    for x,z,width in [(-4.35,-4.25,1.05),(1.45,-3.6,1.5)]:
        for dx in (-width*.35,width*.35):
            parts.append(mesh.rod('EaselSupport',(x+dx,.8,z+.18),(x+dx,1.18,z),.013,brass))
        parts.append(box('PaperLedge',(x,.82,z+.015),(width+.08,.035,.12),wood))
    for dx in (-.5,.5):parts.append(mesh.rod('RollStand',(3.15+dx,.8,4.38),(3.15+dx,1.24,4.18),.012,brass))
    parts.append(box('RollStandLedge',(3.15,.86,4.17),(1.28,.035,.13),wood))
    group('DocumentSupports',parts)


def waveform_plates():
    traces=[]
    for row in range(3):
        points=[]
        for n in range(49):
            t=n/48
            if row==0:height=.1*(math.exp(-((t-.25)/.045)**2)+math.exp(-((t-.61)/.045)**2))
            elif row==1:height=sum(a*math.exp(-((t-c)/.07)**2) for a,c in [(0.035,.2),(.065,.47),(.1,.75)])
            else:height=.12*math.exp(-((t-.27)/.065)**2)
            points.append((3.72+t*.67,1.75-row*.2+height,-4.853))
        for a,b in zip(points,points[1:]):traces.append(mesh.rod('FrontWaveTrace',a,b,.0023,iron,8))
    group('FrontWaveTraces',traces)
    points=[]
    for n in range(49):
        t=n/48
        value=.115*math.exp(-((t-.25)/.075)**2)+(.05*math.exp(-(t-.31)*1.8) if t>.31 else 0)
        points.append((2.943,1.38+value,-3.58-t*.54))
    group('SideWaveTrace',[mesh.rod('TailTrace',a,b,.0024,iron,8) for a,b in zip(points,points[1:])])


def furniture():
    for name in ['PartsTray','RollDesk','ScoreDesk','ToolsDesk','CalibrationDesk']:table(name)
    # Initial structure has the exact footprint of the detailed instrument/cabinets.
    if gray:
        for name in ['Piano','PianoBench','PhotoCabinet','AcousticCabinet','EquipmentShelf']:
            key='Bench' if name=='PianoBench' else name
            group(name,[box(name+'Body',v[key+'Position'],v[key+'Size'],wood)])
    else:piano();cabinets()
    for i in range(1,7):
        x,y,z=v[f'Chair{i:02}'];parts=[]
        parts.append(box('Seat',(x,.45,z),(.52,.055,.52),wood,.014))
        parts.append(box('Back',(x,.73,z+.25),(.52,.23,.045),wood,.016))
        for dx in (-.23,.23):
            parts.append(mesh.rod('FrontLeg',(x+dx,.035,z-.22),(x+dx,.46,z-.22),.021,iron))
            parts.append(mesh.rod('BackLeg',(x+dx,.035,z+.22),(x+dx,.82,z+.25),.021,iron))
            parts.append(mesh.rod('SideBrace',(x+dx,.24,z-.22),(x+dx,.24,z+.22),.014,iron))
        if not gray:
            for dx in (-.19,.19):
                parts.append(mesh.rod('SeatRivet',(x+dx,.469,z-.16),(x+dx,.482,z-.16),.008,brass,12))
                parts.append(mesh.rod('BackRivet',(x+dx,.74,z+.219),(x+dx,.74,z+.228),.008,brass,12))
            for dx in (-.17,.12):parts.append(box('SeatEdgeWear',(x+dx,.478,z-.249),(.065,.0015,.016),wear,.001))
        group(f'Chair{i:02}',parts)
    if not gray:instruments()


def mechanisms():
    board('TempoNoticeBoard',(-5.9,1.65,2),1.03,.65,'east')
    board('RollTemplateBoard',(-5.9,1.7,-2.15),1.02,.68,'east')
    board('FrontWavesBoard',(4.25,1.65,-4.88),1.6,.9)
    board('SideObservationPlate',(2.915,1.45,-3.85),.65,.6,'east')
    board('ScorePaper',(-4.35,1.07,-4.25),1.05,.48)
    board('ChannelPlate',(1.45,1.1,-3.6),1.5,.36)
    for name,p in [('Score_Revealed',(-4.35,1.07,-4.218)),
                   ('Metronome_Solved',(-.5,1.35,4.12)),('Calibration_Solved',(1.45,1.25,-3.6)),
                   ('Roll_Installed',(3.2,.92,4.18)),('Acoustic_Solved',(5.16,2.1,1.25)),
                   ('Photo_Solved',(-5.235,1.15,-.45))]:
        parent=anchor(name)
        if name=='Roll_Installed' and not gray:
            group('RepairedPaperRoll',[box('PaperSheet',(3.15,1.1,4.18),(1.22,.44,.045),paper,.003)],parent)
        elif name=='Photo_Solved' and not gray:
            group('PhotoStatusLamp',[box('StatusLight',(-5.224,1.78,-.45),(.018,.035,.68),lamp,.003)],parent)
        elif name=='Acoustic_Solved' and not gray:
            group('AcousticStatusLamp',[box('StatusLight',(5.18,2.15,1.25),(.022,.065,.32),lamp,.004)],parent)
        else:group(name+'_Detail',[box(name+'_Detail',p,(.025,.025,.012),brass,.002)],parent)
    parent=anchor('Metronome')
    if gray:body=box('Body',(-.48,1.49,4.43),(.27,.38,.23),wood)
    else:
        vertices=[]
        for y,wx,wz in [(1.3,.145,.125),(1.68,.053,.056)]:
            for dx,dz in [(-1,-1),(1,-1),(1,1),(-1,1)]:vertices.append((-.48+dx*wx,-(4.43+dz*wz),y))
        data=bpy.data.meshes.new('MetronomeCase');data.from_pydata(vertices,[],[(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)])
        data.materials.append(wood);body=bpy.data.objects.new('MetronomeCase',data);mesh.collection.objects.link(body)
    group('MetronomeBody',[body,mesh.rod('Pendulum',(-.48,1.32,4.30),(-.48,1.68,4.30),.007,brass),
        box('BeatPlaque',(-.48,1.37,4.298),(.23,.1,.012),paper,.003)],parent)
    parent=anchor('Metronome_Weight')
    group('InstalledWeight',[box('Slider',(-.48,1.51,4.295),(.055,.048,.032),brass)],parent)
    for name,p,size,mat in [
        ('Pickup_Mallet',(-2.4,.845,-4.2),(.5,.08,.13),wood),
        ('Pickup_Cone',(-.65,.92,-4.2),(.3,.2,.3),brass),
        ('Pickup_Weight',(-1.65,.85,4.4),(.12,.08,.08),brass),
        ('Pickup_Roll',(2.2,.85,4.2),(.65,.07,.28),paper),
        ('Pickup_Wax',(5.5,.83,-3.45),(.26,.08,.22),paper)]:
        parent=anchor(name)
        if not gray and name=='Pickup_Mallet':
            parts=[mesh.rod('MalletHandle',(-2.64,.855,-4.2),(-2.16,.855,-4.2),.012,wood),
                   mesh.rod('RubberHead',(-2.16,.855,-4.275),(-2.16,.855,-4.125),.045,rubber,24)]
        elif not gray and name=='Pickup_Cone':
            parts=[mesh.lathe('ListeningCone',(-.65,.82,-4.2),[(.045,0),(.045,.06),(.1,.21),(.14,.26),(.132,.267),(.091,.21),(.035,.061),(.035,0)],brass,32)]
        elif not gray and name=='Pickup_Roll':
            parts=[box('FoldedPaper',(2.2,.82,4.2),(.64,.015,.29),paper,.003),
                   mesh.rod('RolledEdge',(1.90,.89,4.2),(2.50,.89,4.2),.061,paper,32)]
        elif not gray and name=='Pickup_Wax':
            parts=[box('WaxWrapper',(5.5,.83,-3.45),(.28,.04,.23),paper,.012),
                   box('WaxBlock',(5.5,.868,-3.45),(.16,.045,.14),ivory,.014)]
        else:parts=[box(name+'_Mesh',p,size,mat)]
        group(name+'_Mesh',parts,parent)
    if gray:group('RoomFixtures',[box('CeilingStrip',(0,3.77,0),(1.3,.15,.24),lamp)])
    else:school_details();waveform_plates()


def configure_export():
    exporter.SPECS['music']={'source':SOURCE,'glb':GLB,'manifest':MANIFEST,
        'baseline':'art/blender/dream_music/history/music-room-procedural-v1.cs.txt',
        'required':REQUIRED,'maxTriangles':180000,'maxVisibleMeshes':220,'maxMaterials':24,
        'maxBytes':60*1024*1024,'actualTextureMemory':True}


def manifest_and_provenance():
    path=root/MANIFEST; data=json.loads(path.read_text('utf-8'))
    data.update(layoutVersion=2,layoutSource=LAYOUT.as_posix(),layoutSha256=exporter.sha256(root/LAYOUT),
        layout=v,graybox=gray,visualAcceptance='pending-human-review',
        lightingDecision='Color ambient, west daylight, practical task lamps, two ReflectionProbes; no SSAO or GI dependency')
    used={'oak_veneer_01','painted_plaster_wall','terrazzo_tiles'}
    data['sourceAssets']=[a for a in data['sourceAssets'] if a['id'] in used] if not gray else []
    path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n','utf-8')
    if gray:return
    record={'id':'dream_music_room_runtime_v2','sourceFile':SOURCE.as_posix(),
        'gameFile':GLB.as_posix().removeprefix('assets/generated/'),'status':'adopted-runtime-default',
        'generator':'Blender Python offline procedural modeling','model':'Blender 5.2.0 LTS','date':'2026-09-09',
        'script':'scripts/tools/remodel_music_v2.py',
        'prompt':'Approved 12x10x4m old school music classroom; six timber and steel chairs, real upright piano, four-instrument calibration, 3.2m equipment room with traversable unlocked door, school joinery and readable west daylight.',
        'referenceImages':['outputs/music-room-3d-review/01-entry-overview.png'],
        'styleReferences':['doc/engineering/classroom-remodel-v6.md'],
        'parameters':{'layoutSource':LAYOUT.as_posix(),'layoutVersion':2,'seed':0,'runtimeGeneration':False},
        'postprocessing':['per-component metric UVs','applied bevels','packed adopted CC0 PBR textures',
                          'GLB with stable state anchors; no characters, animations, cameras or lights'],
        'sourceSha256':data['sourceSha256'],'runtimeGlbSha256':data['runtimeGlbSha256'],'sourceAssets':data['sourceAssets']}
    path=root/'assets/generated/provenance.json';text=path.read_text('utf-8');formatted=json.dumps(record,ensure_ascii=False,indent=2)
    needle='"id": "'+record['id']+'"'
    if needle in text:
        start=text.rfind('{',0,text.index(needle));_,length=json.JSONDecoder().raw_decode(text[start:])
        text=text[:start]+formatted+text[start+length:]
    else:
        end=text.rfind(']');text=text[:end].rstrip()+',\n'+formatted+'\n'+text[end:]
    json.loads(text);path.write_text(text,'utf-8')


def build():
    global room,wood,teal,plaster,floor,brass,iron,paper,glass,lamp,ebony,ivory,rubber,film,silver,wear
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mesh.root=root;mesh.gray=gray;mesh.collection=mesh.geo.collection('MusicRoomV2')
    room=mesh.empty('MusicRoom');mesh.hall_root=room
    wood=mesh.pbr('M_MusicV2_Wood',(.3,.17,.08),.72,asset='oak_veneer_01',tint=(.5,.32,.18),normal_strength=.23)
    mesh.wood=wood
    plaster=mesh.pbr('M_MusicV2_Plaster',(.65,.64,.59),.91,asset='painted_plaster_wall',normal_strength=.18)
    floor=mesh.pbr('M_MusicV2_Terrazzo',(.4,.38,.34),.85,asset='terrazzo_tiles',normal_strength=.22)
    teal=mesh.pbr('M_MusicV2_GreenPaint',(.19,.29,.24),.84,asset='painted_plaster_wall',diffuse=False,normal_strength=.12)
    brass=mesh.pbr('M_MusicV2_Brass',(.4,.28,.1),.43,.68)
    iron=mesh.pbr('M_MusicV2_Steel',(.055,.065,.06),.6,.45)
    paper=mesh.pbr('M_MusicV2_Paper',(.69,.65,.52),.95)
    glass=mesh.pbr('M_MusicV2_Glass',(.49,.61,.58),.21)
    bsdf=glass.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Alpha'].default_value=.22
    glass.surface_render_method='DITHERED';glass.diffuse_color=(.49,.61,.58,.22)
    lamp=mesh.pbr('M_MusicV2_Lamp',(.84,.75,.56),.4,emission=.35)
    ebony=mesh.pbr('M_MusicV2_PianoFinish',(.06,.038,.02),.38,asset='oak_veneer_01',tint=(.095,.065,.036),normal_strength=.10)
    ivory=mesh.pbr('M_MusicV2_Ivory',(.80,.79,.68),.48)
    rubber=mesh.pbr('M_MusicV2_Rubber',(.02,.025,.023),.87)
    film=mesh.pbr('M_MusicV2_DarkGreen',(.055,.085,.065),.83)
    silver=mesh.pbr('M_MusicV2_Nickel',(.54,.55,.51),.3,.8)
    wear=mesh.pbr('M_MusicV2_WornEdge',(.4,.29,.17),.92)
    architecture();doors();furniture();mechanisms()
    bpy.context.view_layer.update()
    for obj in exporter.export_objects('music'):
        if obj.type!='MESH':continue
        if not obj.data.uv_layers:project_uv(obj)
        bpy.context.view_layer.objects.active=obj
        for modifier in list(obj.modifiers):bpy.ops.object.modifier_apply(modifier=modifier.name)
    for im in bpy.data.images:
        if im.source=='FILE':im.pack()
    for key,value in {'sampleOnly':False,'runtimeDefault':True,'fullScene':True,'visualDirectionApproved':True,
        'visualAcceptance':'pending-human-review','layoutVersion':2,'graybox':gray}.items():bpy.context.scene[key]=value
    bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
    (root/SOURCE).parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(root/SOURCE))
    exporter.run('music','export',root);manifest_and_provenance()


def check():
    exporter.run('music','check',root)
    data=json.loads((root/MANIFEST).read_text('utf-8'))
    if data['layoutSha256']!=exporter.sha256(root/LAYOUT):raise RuntimeError('Music layout hash is stale')
    if data['geometry']!=exporter.summarize('music'):raise RuntimeError('Music geometry manifest is stale')
    if data['textureMemoryEstimateMiB']!=exporter.texture_memory_mib('music'):raise RuntimeError('Music texture estimate is stale')
    print('MUSIC_V2_CHECK_PASS room=12x10x4 equipment=3.2x3.2 readOnly=true')


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('mode',choices=['graybox','build','export','check'])
    parser.add_argument('--root',required=True)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    root=Path(args.root).resolve();v=read_layout(root);gray=args.mode=='graybox';configure_export()
    if args.mode in ('graybox','build'):build()
    elif args.mode=='export':exporter.run('music','export',root);manifest_and_provenance()
    else:check()
