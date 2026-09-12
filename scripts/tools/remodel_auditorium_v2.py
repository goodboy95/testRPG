"""Independent 22x16x7.5m auditorium v2. Blender --background --factory-startup
--python scripts/tools/remodel_auditorium_v2.py -- graybox|build|export|check --root .
The raised stage is scenic; shared layout keeps every interaction on ground level.
"""
from __future__ import annotations
import argparse,json,math,re,sys
from pathlib import Path
import bpy
from mathutils import Matrix,Vector
sys.path.insert(0,str(Path(__file__).resolve().parent))
import remodel_manor_v3 as mesh
import remodel_archive_v2 as common
import export_environment_cinematic_samples as exporter
from remodel_classroom_v6 import project_uv
SOURCE=Path('art/blender/dream_late_rooms/dream-auditorium-room-v2.blend')
GLB=Path('assets/generated/dream_escape/3d/dream-auditorium-room-v2.glb')
MANIFEST=GLB.with_name(GLB.stem+'-manifest.json')
LAYOUT=Path('scripts/presentation/AuditoriumLayout3D.cs')
REQUIRED=['AuditoriumRoom','Room_Floor','Room_Ceiling','WindowGlass','Door_South_Hinge','Stage','Proscenium','MainCurtain','StageScreen','StageExitClosed','Exit_Open',
 'Projector','FlyRail','LensTable','WeightTable','TapeReferenceTable','SpliceBench','MasterPlayer','LightConsole','SoundConsole','BroadcastConsole','LeftLever','RightSwitch','SideStand',
 *['Seat'+str(i).zfill(2) for i in range(1,33)],*['Line_Weight'+str(i) for i in range(4)],*['Projector_Disc'+str(i) for i in range(3)],
 'Projection_Clue','Projector_Lens','Projector_Solved','Tape_Order','Splice_Reel','Master_Tape','Master_Player_Light','Light_Ready','Sound_Ready','Broadcast_Ready','Left_Lever_Held',
 'Tape_Unspliced','Pickup_Counterweight','Pickup_Lens','Installed_Counterweight','Left_Handle','Right_Handle','FrontFramePlate','HandoffCircuitPlate','RoomFixtures']
def read_layout():
 s=(root/LAYOUT).read_text('utf-8');v={k:float(n) for k,n in re.findall(r'const float (\w+) = (-?[\d.]+)f;',s)}
 v.update({k:tuple(float(n.strip().removesuffix('f')) for n in ns.split(',')) for k,ns in re.findall(r'public static readonly Vector3 (\w+) = new\(([^)]+)\);',s)})
 return v
box=common.box;group=common.group;rod=mesh.rod;anchor=common.anchor;state=common.state

def architecture():
 w,d,h=v['Width'],v['Depth'],v['Height']
 group('Room_Floor',[box('Floor',(0,-.1,0),(w,.2,d),floor)])
 group('Room_Ceiling',[box('Ceiling',(0,h+.1,0),(w+.4,.2,d+.4),plaster)])
 group('Wall_North',[box('North',(0,h/2,-8.1),(w+.4,h,.2),plaster)])
 group('Wall_South',[box('SouthWest',(-5.95,h/2,8.1),(10.1,h,.2),plaster),box('SouthEast',(5.95,h/2,8.1),(10.1,h,.2),plaster),box('SouthHeader',(0,5.15,8.1),(1.8,4.7,.2),plaster)])
 frames=[];panes=[];dado=[];pilasters=[]
 for side in (-1,1):
  parts=[];cursor=-8;x=side*11.1
  for z in (-5.3,-1.6,2.1,5.8):
   ww=2.25;low=3.4;high=6.7;a=z-ww/2;b=z+ww/2
   if a>cursor:parts.append(box('Pier',(x,h/2,(cursor+a)/2),(.2,h,a-cursor),plaster))
   parts.extend([box('SillWall',(x,low/2,z),(.2,low,ww),plaster),box('Header',(x,(h+high)/2,z),(.2,h-high,ww),plaster)])
   for zz in (a,z,b):frames.append(box('Mullion',(side*10.99,(low+high)/2,zz),(.14,high-low+.1,.065),wood))
   for yy in (low,5.35,high):frames.append(box('Transom',(side*10.99,yy,z),(.14,.07,ww+.1),wood))
   frames.append(box('Sill',(side*10.89,low-.06,z),(.38,.12,ww+.24),wood))
   panes.append(box('ClerestoryPane',(side*11.025,(low+high)/2,z),(.02,high-low-.04,ww-.03),glass,0));cursor=b
  parts.append(box('EndPier',(x,h/2,(cursor+8)/2),(.2,h,8-cursor),plaster));group('Wall_West' if side<0 else 'Wall_East',parts)
  dado.extend([box('AcousticDado',(side*10.98,1.1,0),(.04,2.2,16),paint),box('DadoRail',(side*10.94,2.22,0),(.09,.08,16),wood),box('Skirting',(side*10.95,.1,0),(.08,.2,16),wood)])
  for z in (-7.25,-3.45,.25,3.95,7.7):
   pilasters.extend([box('Pilaster',(side*10.9,3.65,z),(.2,7.3,.21),paint),box('Capital',(side*10.82,7.08,z),(.35,.22,.42),wood)])
 for x,ww in [(-5.95,10.1),(5.95,10.1)]:dado.extend([box('RearDado',(x,1.1,7.98),(ww,2.2,.04),paint),box('RearRail',(x,2.22,7.94),(ww,.08,.09),wood),box('RearSkirting',(x,.1,7.95),(ww,.2,.08),wood)])
 group('WindowFrames',frames);group('WindowGlass',panes);group('WallSkirting',dado);group('WallPilasters',pilasters)
 beams=[]
 for z in (-6,-2,2,6):beams.extend([box('CeilingBeam',(0,7.22,z),(22,.35,.22),wood),box('BeamSoffit',(0,7.015,z),(22,.065,.31),paint)])
 group('CeilingBeams',beams)
 x,y,z=v['ExitHinge'];dw=v['DoorWidth'];dh=v['DoorHeight'];hinge=anchor('Door_South_Hinge',(x,y,z))
 parts=[box('Leaf',(x+dw/2,dh/2,z),(dw-.025,dh,.095),wood,0)]
 for yy in (.6,1.55,2.38):parts.append(box('DoorPanel',(x+dw/2,yy,z-.057),(dw-.23,.55,.04),paint))
 parts.extend([rod('PushRail',(-.6,1.05,7.88),(.6,1.05,7.88),.026,brass),box('DoorNumber',(0,2.4,7.868),(.42,.19,.02),paper)])
 group('EntranceLeaf',parts,hinge)
 group('EntranceJoinery',[box('Jamb',(xx,dh/2,z),(.13,dh+.13,.23),wood) for xx in (-.965,.965)]+[box('Header',(0,dh+.065,z),(2.06,.13,.23),wood)])
 group('EntranceRebate',[box('Stop',(xx,dh/2,8.06),(.10,dh,.06),wood,0) for xx in (-.865,.865)]+[box('HeadStop',(0,dh-.025,8.06),(1.8,.08,.06),wood,0)])

def curtain(name,width,height,top,z,parent=None):
 if gray:return group(name,[box('Curtain',(0,top-height/2,z),(width,height,.2),fabric)],parent)
 vertices=[];faces=[];columns=112;rows=10
 for back in (0,1):
  for r in range(rows+1):
   y=top-height*r/rows
   for c in range(columns+1):
    x=-width/2+width*c/columns;zz=z+.10*math.cos(c*math.tau/7)+.014*math.sin(r*1.4+c*.5)+back*.015
    vertices.append((x,-zz,y))
 layer=(columns+1)*(rows+1)
 for side in range(2):
  for r in range(rows):
   for c in range(columns):
    a=side*layer+r*(columns+1)+c;faces.append((a,a+1,a+columns+2,a+columns+1) if side==0 else (a+1,a,a+columns+1,a+columns+2))
 data=bpy.data.meshes.new(name);data.from_pydata(vertices,[],faces);data.materials.append(fabric);obj=bpy.data.objects.new(name,data);mesh.collection.objects.link(obj);project_uv(obj)
 for face in data.polygons:face.use_smooth=True
 return group(name,[obj],parent)

def stage():
 x,y,z=v['StagePosition'];w,h,d=v['StageSize']
 group('Stage',[box('Plinth',(x,h/2,z),(w,h,d),wood,.006),box('StageNosing',(0,h-.035,-4.04),(14,.07,.08),brass,.003)])
 parts=[]
 for x in (-6.78,6.78):
  parts.extend([box('PortalColumn',(x,3.45,-4.13),(.44,6.9,.48),wood),box('ColumnPanel',(x,3.65,-3.87),(.28,5.3,.025),paint),box('ColumnFoot',(x,.7,-4.13),(.59,1.4,.57),wood)])
 parts.extend([box('PortalLintel',(0,6.85,-4.13),(14,.48,.55),wood),box('Cornice',(0,7.13,-4.11),(14.3,.13,.71),paint),box('Valance',(0,6.50,-4.15),(13.2,.48,.23),fabric)])
 group('Proscenium',parts)
 root_curtain=anchor('MainCurtain',v['CurtainTop']);curtain('CurtainPleats',13.1,5.55,6.7,-4.4,root_curtain)
 group('StageScreen',[box('Screen',(0,4.98,-7.85),(10.4,2.6,.05),paper),box('ScreenTop',(0,6.34,-7.81),(10.6,.07,.07),iron),box('ScreenBottom',(0,3.62,-7.81),(10.6,.07,.07),iron)])
 # Stage exit is scenery; completion is still the existing ExitOpened page.
 closed=anchor('StageExitClosed');opened=anchor('Exit_Open');parts=[]
 for side in (-1,1):
  leaf=box('BackstageLeaf',(side*.6,2.45,-7.79),(1.18,2.7,.1),paint)
  group('ClosedLeaf'+str(side),[leaf,box('PushBar',(side*.6,2.25,-7.72),(.8,.055,.065),brass)],closed)
  hinge=mesh.empty('OpenLeafHinge'+str(side),(side*1.2,1.1,-7.79),opened)
  group('OpenLeaf'+str(side),[box('OpenLeaf',(side*.6,2.45,-7.79),(1.18,2.7,.1),paint),box('PushBar',(side*.6,2.25,-7.72),(.8,.055,.065),brass)],hinge)
  hinge.rotation_euler.z=math.radians(-side*65)
 group('StageExitFrame',[box('ExitJamb',(x,2.45,-7.74),(.12,2.82,.23),wood) for x in (-1.26,1.26)]+[box('ExitHeader',(0,3.88,-7.74),(2.64,.12,.23),wood),box('ExitDarkness',(0,2.45,-7.925),(2.4,2.7,.03),ink)])
 # Front observation is a real plate fixed to the stage apron. It carries no solution sequence.
 common.document('FrontFramePlate',(0,.65,-3.965),1.05,.43,'south')
 x,y,z=v['SideStandPosition'];w,h,d=v['SideStandSize']
 group('SideStand',[box('StandBase',(x,.06,z),(w,.12,d),iron),box('Upright',(x,.85,z),(.055,1.6,.055),iron)])
 common.document('Projection_Clue',(-7.23,1.36,-7.1),.66,.48,'east')
 # The plate faces west, towards the side corridor.
 plate=bpy.data.objects['Projection_Clue'];pivot=Vector((-7.23,7.1,1.36));plate.matrix_world=Matrix.Translation(pivot)@Matrix.Rotation(math.pi,4,'Z')@Matrix.Translation(-pivot)@plate.matrix_world


def seats():
 if gray:parts=[box('SeatEnvelope',(0,.475,0),v['SeatSize'],wood,0)]
 else:
  parts=[box('SeatCushion',(0,.475,-.035),(.54,.11,.50),fabric,.035),box('BackShell',(0,.725,.28),(.55,.43,.075),wood,.028),box('BackCushion',(0,.735,.229),(.49,.36,.065),fabric,.026)]
  for x in (-.23,.23):
   for z in (-.25,.25):parts.append(rod('SeatLeg',(x,.03,z),(x,.45,z),.023,iron))
   parts.append(rod('LowerBrace',(x,.22,-.25),(x,.22,.25),.015,iron))
  for x in (-.287,.287):
   parts.append(box('ArmRest',(x,.62,-.025),(.044,.05,.55),wood,.014))
   for z in (-.18,.19):parts.append(rod('ArmSupport',(x,.40,z),(x,.6,z),.012,iron))
 prototype=group('Seat01',parts);bpy.context.view_layer.update();prototype.data.transform(prototype.matrix_world);prototype.matrix_world=Matrix.Identity(4)
 for i in range(1,33):
  obj=prototype if i==1 else prototype.copy()
  if i!=1:mesh.collection.objects.link(obj)
  obj.name=f'Seat{i:02}';x,y,z=v[f'Seat{i:02}'];obj.location=(x,-z,0)


def cabinet(name,height=1):
 x,y,z=v[name+'Position'];w,_,d=v[name+'Size'];parts=[box('Case',(x,height*.47,z),(w-.06,height*.94,d-.06),paint,.015),box('Plinth',(x,.05,z),(w,.1,d),iron),box('Top',(x,height-.025,z),(w,.05,d),wood)]
 if not gray:
  for k in range(5):
   xx=x-w*.34+k*w*.17
   parts.extend([box('FaderSlot',(xx,height+.003,z-.13),(.026,.007,.28),ink,.002),box('Fader',(xx,height+.017,z-.16+(k%3)*.07),(.065,.025,.045),silver,.005),rod('Dial',(xx,height+.01,z+.2),(xx,height+.042,z+.2),.028,brass,20)])
  parts.append(box('Meter',(x,height+.006,z+.34),(w*.63,.01,.12),ivory,.003))
 group(name,parts)

def apparatus():
 for name in ['LensTable','WeightTable','TapeReferenceTable','SpliceBench']:common.table(name)
 for name in ['LightConsole','SoundConsole','BroadcastConsole']:cabinet(name)
 cabinet('MasterPlayer',.90)
 # Projector pedestal, side-changing shadow discs and an installable optical barrel.
 group('Projector',[box('Pedestal',(-9.7,.43,-4.8),(1.45,.86,1.2),paint),box('Deck',(-9.7,.88,-4.8),(1.6,.07,1.4),wood),box('OpticalCase',(-9.62,1.19,-4.8),(.72,.56,1.13),iron,.025)])
 for i in range(3):
  x,y,z=v[f'Disc{i+1:02}'];parts=[rod('ShadowDisc',(x-.02,y,z),(x+.02,y,z),.177,brass,32),rod('Hub',(x-.035,y,z),(x+.035,y,z),.033,iron)]
  if not gray:
   for k in range(3):
    a=k*math.tau/3;parts.append(box('ImageWindow',(x+.025,y+.10*math.cos(a),z+.10*math.sin(a)),(.008,.055,.055),ink,.003))
  state('Projector_Disc'+str(i),parts,(x,y,z))
 state('Projector_Lens',[rod('LensBarrel',(-9.20,1.18,-4.8),(-8.80,1.18,-4.8),.135,iron,32),rod('LensRing',(-8.83,1.18,-4.8),(-8.78,1.18,-4.8),.149,brass,32),rod('Optic',(-8.777,1.18,-4.8),(-8.773,1.18,-4.8),.119,glass,32)])
 state('Pickup_Lens',[rod('LooseLens',(-9.84,.96,.5),(-9.55,.96,.5),.133,iron,32),rod('LensRim',(-9.55,.96,.5),(-9.5,.96,.5),.145,brass,32)])
 # Projection is revealed only after alignment; the unsolved front and side observations stay independent.
 shapes=[]
 for x in (-3,0,3):
  shapes.append(box('ImageFrame',(x,5.05,-7.81),(2.0,1.7,.018),ivory,.004))
 for xx in (-3.55,-3,-2.45):shapes.append(box('WindowBar',(xx,5.05,-7.79),(.05,1.45,.012),ink,.002))
 for yy in (4.34,5.05,5.76):shapes.append(box('WindowBar',(-3,yy,-7.79),(1.15,.05,.012),ink,.002))
 shapes.extend([rod('FigureHead',(0,5.46,-7.8),(0,5.46,-7.775),.2,ink,24),box('FigureBody',(0,4.91,-7.78),(.39,.62,.025),ink,.05),box('BellBody',(3,4.96,-7.78),(.72,.8,.025),ink,.15),rod('BellClapper',(3,4.5,-7.8),(3,4.5,-7.775),.1,ink,20)])
 state('Projector_Solved',shapes)
 # Four vertical rope lanes, sheaves, cleats and travelling iron weights.
 x,y,z=v['FlyRailPosition'];w,h,d=v['FlyRailSize'];parts=[]
 for zz in (z-d/2+.08,z+d/2-.08):parts.append(box('SteelUpright',(x,h/2,zz),(.12,h,.12),iron))
 for yy in (.15,h-.1):parts.append(box('RailCrossbeam',(x,yy,z),(.16,.15,d),iron))
 for i in range(4):
  xx,yy,zz=v[f'Weight{i+1:02}'];parts.extend([rod('Rope',(xx,1,zz),(xx,6.15,zz),.014,rope),rod('ReturnRope',(xx+.22,1,zz),(xx+.22,6.15,zz),.014,rope),rod('Sheave',(xx-.04,6.12,zz),(xx+.26,6.12,zz),.15,brass,28),box('RopeLock',(xx,.92,zz),(.16,.19,.16),iron)])
  for level in range(4):parts.append(box('Notch',(xx+.08,v['WeightMinimumHeight']+level*v['WeightStep'],zz+.17),(.03,.022,.11),ivory,.001))
  state('Line_Weight'+str(i),[box('WeightCarrier',(xx,yy,zz),(.23,.40,.32),iron,.012),box('IdentificationPlate',(xx-.124,yy,zz),(.008,.06,.12),brass,.002)],(xx,yy,zz))
 group('FlyRail',parts)
 state('Pickup_Counterweight',[box('LooseWeight',(9.7,.93,.5),(.36,.24,.32),iron,.015),rod('LiftEye',(9.7,1.05,.5),(9.7,1.16,.5),.055,brass,24)])
 added=state('Installed_Counterweight',[box('AddedWeight',(9.3,.8,-6.75),(.24,.28,.32),iron,.012)],(9.3,.8,-6.75));bpy.context.view_layer.update();mesh.geo.parent_keep_world(added,bpy.data.objects['Line_Weight0'])
 common.document('Tape_Order',(-9.7,.815,3.1),1.1,.7)
 group('SpliceTools',[box('CuttingMat',(9.7,.815,3.1),(1.24,.02,.82),rubber),box('SpliceBlock',(9.5,.865,3.1),(.38,.075,.35),silver),rod('SpliceKnife',(10.04,.845,2.84),(10.04,.845,3.24),.014,silver),rod('KnifeGrip',(10.04,.845,3.24),(10.04,.845,3.4),.023,wood)])
 parts=[]
 for i in range(3):parts.extend([box('LooseTape',(9.3+i*.38,.839,3.2+(i%2)*.09),(.23,.004,.47),paper,.001),box('TapeStripe',(9.3+i*.38,.844,3.2+(i%2)*.09),(.08,.003,.38),ink,.001)])
 state('Tape_Unspliced',parts)
 # Completed reel lies on the table until installed in the master machine.
 state('Splice_Reel',[rod('SplicedReel',(9.7,.84,3.1),(9.7,.92,3.1),.29,brass,32),rod('TapeCore',(9.7,.93,3.1),(9.7,.95,3.1),.075,iron,24)])
 group('MasterRecorderBody',[box('MasterCase',(9.65,1.14,5.8),(.74,.48,.84),paint),box('MasterFace',(9.25,1.14,5.8),(.05,.46,.82),iron),box('MasterMeter',(9.219,1.02,5.65),(.014,.09,.19),ivory),box('MasterSpeaker',(9.219,1.02,5.99),(.014,.1,.24),rubber)])
 common.reel('Master_Tape',v['MasterReel'])
 state('Master_Player_Light',[box('PlaybackLamp',(9.214,1.0,5.47),(.008,.025,.025),lamp,.002)])
 for rootname,cab in [('Light_Ready','LightConsole'),('Sound_Ready','SoundConsole'),('Broadcast_Ready','BroadcastConsole')]:
  xx,_,zz=v[cab+'Position'];state(rootname,[box('StatusLamp',(xx+.65,1.021,zz-.35),(.06,.018,.045),lamp,.004),box('SwitchRaised',(xx+.65,1.04,zz-.18),(.035,.04,.065),brass,.003)])
 common.document('HandoffCircuitPlate',(-6,1.03,6.9),1.05,.55)
 for name,hand in [('LeftLever','Left_Handle'),('RightSwitch','Right_Handle')]:
  xx,_,zz=v[name+'Position'];group(name,[box('Base',(xx,.07,zz),(.55,.14,.60),iron),box('Stand',(xx,.57,zz),(.20,1,.22),paint),box('ControlBox',(xx,1.12,zz),(.44,.33,.39),wood)])
  pivot=(xx,1.16,zz);state(hand,[rod('HandleShaft',(xx,1.16,zz),(xx,1.16,zz-.28),.022,brass),rod('HandleGrip',(xx-.1,1.16,zz-.28),(xx+.1,1.16,zz-.28),.028,rubber)],pivot)
 state('Left_Lever_Held',[box('HeldIndicator',(-8.6,1.294,-1.8),(.035,.009,.035),lamp,.002)])
 parts=[]
 for x in (-7,0,7):
  for z in (-2,4):parts.extend([rod('PendantStem',(x,7.5,z),(x,6.3,z),.018,iron),box('PendantHousing',(x,6.25,z),(1.0,.15,.34),paint),box('PendantDiffuser',(x,6.165,z),(.88,.018,.27),lamp,.004)])
 for x in (-5,0,5):parts.extend([rod('LightingBar',(x-.4,6.9,-3.8),(x+.4,6.9,-3.8),.025,iron),rod('SpotHousing',(x,6.6,-3.85),(x,6.6,-3.55),.12,iron,24),rod('SpotLens',(x,6.6,-3.54),(x,6.6,-3.53),.104,glass,24)])
 group('RoomFixtures',parts)


def configure():
 exporter.SPECS['auditorium']={'source':SOURCE,'glb':GLB,'manifest':MANIFEST,'baseline':'art/blender/dream_late_rooms/dream-auditorium-v1.blend','required':REQUIRED,'maxTriangles':260000,'maxVisibleMeshes':320,'maxMaterials':32,'maxBytes':80*1024*1024,'actualTextureMemory':True}
def metadata():
 p=root/MANIFEST;data=json.loads(p.read_text('utf-8'));data.update(layoutVersion=2,layoutSource=LAYOUT.as_posix(),layoutSha256=exporter.sha256(root/LAYOUT),layout=v,graybox=gray,visualAcceptance='pending-human-review',lightingDecision='Color ambient, west clerestory and practical stage/console lighting; no SSAO',seating={'instances':32,'sharedMesh':True,'rowsPerBank':4,'seatsPerRow':4},stage='Scenic raised platform; all interaction and cooperation anchors are on Y=0')
 data['sourceAssets']=[a for a in data['sourceAssets'] if a['id'] in {'oak_veneer_01','painted_plaster_wall','terrazzo_tiles','fabric_pattern_07'}] if not gray else []
 p.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n','utf-8')
 if gray:return
 record={'id':'dream_auditorium_runtime_v2','sourceFile':SOURCE.as_posix(),'gameFile':GLB.as_posix().removeprefix('assets/generated/'),'status':'adopted-runtime-default','generator':'Blender Python offline procedural modeling','model':bpy.app.version_string,'date':'2026-09-10','script':'scripts/tools/remodel_auditorium_v2.py','prompt':'Approved 22x16x7.5m old school auditorium, 32 linked upholstered seats, continuous ground-level central and side work aisles, scenic raised stage with pleated curtain, physical fly rail, shadow discs, tape splicing and cooperation controls. Preserve 15 interaction IDs.','referenceImages':['outputs/late-rooms-v2/before/auditorium'],'parameters':{'layoutSource':LAYOUT.as_posix(),'layoutVersion':2,'seed':0,'runtimeGeneration':False},'postprocessing':['metric UVs','applied bevels','32 seats share one mesh','packed adopted CC0 PBR textures','stable physical state anchors; no character or animation data'],'sourceSha256':data['sourceSha256'],'runtimeGlbSha256':data['runtimeGlbSha256'],'sourceAssets':data['sourceAssets']}
 p=root/'assets/generated/provenance.json';s=p.read_text('utf-8');formatted=json.dumps(record,ensure_ascii=False,indent=2);needle='"id": "'+record['id']+'"'
 if needle in s:
  start=s.rfind('{',0,s.index(needle));_,n=json.JSONDecoder().raw_decode(s[start:]);s=s[:start]+formatted+s[start+n:]
 else:
  end=s.rfind(']');s=s[:end].rstrip()+',\n'+formatted+'\n'+s[end:]
 json.loads(s);p.write_text(s,'utf-8')
def build():
 global room,wood,paint,plaster,floor,fabric,brass,iron,paper,glass,lamp,ink,silver,ivory,rubber,rope
 bpy.ops.wm.read_factory_settings(use_empty=True);mesh.root=root;mesh.gray=gray;mesh.collection=mesh.geo.collection('AuditoriumRoomV2');room=mesh.empty('AuditoriumRoom');mesh.hall_root=room
 wood=mesh.pbr('AuditoriumV2_Oak',(.28,.16,.09),.73,asset='oak_veneer_01',tint=(.45,.28,.16));mesh.wood=wood
 plaster=mesh.pbr('AuditoriumV2_Plaster',(.63,.61,.56),.93,asset='painted_plaster_wall',normal_strength=.17)
 floor=mesh.pbr('AuditoriumV2_Terrazzo',(.32,.31,.28),.85,asset='terrazzo_tiles',normal_strength=.17)
 paint=mesh.pbr('AuditoriumV2_Green',(.17,.23,.22),.85,asset='painted_plaster_wall',diffuse=False,normal_strength=.1)
 fabric=mesh.pbr('AuditoriumV2_WineCloth',(.24,.075,.085),.95,asset='fabric_pattern_07',diffuse=False,normal_strength=.15)
 brass=mesh.pbr('AuditoriumV2_Brass',(.4,.28,.1),.46,.7);iron=mesh.pbr('AuditoriumV2_Iron',(.035,.045,.047),.58,.5)
 paper=mesh.pbr('AuditoriumV2_Paper',(.72,.68,.56),.96);ink=mesh.pbr('AuditoriumV2_Ink',(.065,.075,.073),.95)
 silver=mesh.pbr('AuditoriumV2_Nickel',(.5,.51,.5),.35,.8);ivory=mesh.pbr('AuditoriumV2_Ivory',(.72,.74,.66),.5);rubber=mesh.pbr('AuditoriumV2_Rubber',(.018,.022,.021),.9)
 rope=mesh.pbr('AuditoriumV2_Rope',(.3,.25,.17),.94)
 glass=mesh.pbr('AuditoriumV2_Glass',(.44,.56,.61),.2);glass.node_tree.nodes.get('Principled BSDF').inputs['Alpha'].default_value=.19;glass.surface_render_method='DITHERED';glass.diffuse_color=(.44,.56,.61,.19)
 lamp=mesh.pbr('AuditoriumV2_Lamp',(.87,.76,.53),.4,emission=.35)
 for key in ['root','gray','v','room','wood','paint','plaster','floor','brass','iron','paper','glass','lamp','ink','silver','ivory','rubber']:setattr(common,key,globals()[key])
 architecture();stage();seats();apparatus();bpy.context.view_layer.update()
 for obj in exporter.export_objects('auditorium'):
  if obj.type!='MESH':continue
  if not obj.data.uv_layers:project_uv(obj)
  bpy.context.view_layer.objects.active=obj
  for modifier in list(obj.modifiers):bpy.ops.object.modifier_apply(modifier=modifier.name)
 for im in bpy.data.images:
  if im.source=='FILE':im.pack()
 for k,value in {'sampleOnly':False,'runtimeDefault':True,'fullScene':True,'visualDirectionApproved':True,'visualAcceptance':'pending-human-review','layoutVersion':2,'graybox':gray}.items():bpy.context.scene[k]=value
 bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
 (root/SOURCE).parent.mkdir(parents=True,exist_ok=True);bpy.ops.wm.save_as_mainfile(filepath=str(root/SOURCE));exporter.run('auditorium','export',root);metadata()
def check():
 exporter.run('auditorium','check',root);data=json.loads((root/MANIFEST).read_text('utf-8'))
 if data['layoutSha256']!=exporter.sha256(root/LAYOUT):raise RuntimeError('Auditorium layout hash stale')
 if data['geometry']!=exporter.summarize('auditorium'):raise RuntimeError('Auditorium geometry manifest stale')
 seats=[bpy.data.objects[f'Seat{i:02}'] for i in range(1,33)]
 if len({o.data.as_pointer() for o in seats})!=1:raise RuntimeError('Seating is not linked')
 print('AUDITORIUM_V2_CHECK_PASS room=22x16x7.5 seats=32 sharedMesh=true readOnly=true')
if __name__=='__main__':
 parser=argparse.ArgumentParser();parser.add_argument('mode',choices=['graybox','build','export','check']);parser.add_argument('--root',required=True);args=parser.parse_args(sys.argv[sys.argv.index('--')+1:]);root=Path(args.root).resolve();v=read_layout();gray=args.mode=='graybox';configure()
 if args.mode in ('graybox','build'):build()
 elif args.mode=='export':exporter.run('auditorium','export',root);metadata()
 else:check()
