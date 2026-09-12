"""Independent archive v2 production; reads ArchiveRoomLayout3D, preserves v1.
Blender --background --python scripts/tools/remodel_archive_v2.py -- graybox|build|export|check --root .
"""
from __future__ import annotations
import argparse, json, math, re, sys
from pathlib import Path
import bpy
from mathutils import Vector, Matrix
sys.path.insert(0,str(Path(__file__).resolve().parent))
import remodel_manor_v3 as mesh
import export_environment_cinematic_samples as exporter
from remodel_classroom_v6 import project_uv
SOURCE=Path('art/blender/dream_late_rooms/dream-archive-room-v2.blend')
GLB=Path('assets/generated/dream_escape/3d/dream-archive-room-v2.glb')
MANIFEST=GLB.with_name(GLB.stem+'-manifest.json')
LAYOUT=Path('scripts/presentation/ArchiveRoomLayout3D.cs')
REQUIRED=['ArchiveRoom','Room_Floor','Room_Ceiling','WindowGlass','Door_East_Hinge',
 'WestStacks','NorthStacks','SouthCabinet','RegistryTable','DrawerMatrix','RepairBench','RecorderBench',
 'TimelineTable','IndexTable','RouteTable','Registry_Solved','Drawer_Open','Drawer_Closed','Fuse_Repaired',
 'Recorder_Reel','Recorder_Reel_Right','Recorder_Crank','Recorder_Fuse','Recorder_Running',
 'Timeline_Sorted','Timeline_Unsorted','UnusedVaultState','Pickup_IndexStrip',
 *['Registry_Preview'+str(i) for i in range(4)],'RegistryRulePaper','ChoirRecordPaper','BroadcastRecordPaper',
 'ArtRecordPaper','TrackRecordPaper','FuseRoutePaper','RecorderBody','RoomFixtures']
def read_layout():
 s=(root/LAYOUT).read_text('utf-8')
 v={k:float(n) for k,n in re.findall(r'const float (\w+) = (-?[\d.]+)f;',s)}
 v.update({k:tuple(float(n.strip().removesuffix('f')) for n in ns.split(',')) for k,ns in re.findall(r'public static readonly Vector3 (\w+) = new\(([^)]+)\);',s)})
 return v
def box(name,p,size,material,bevel=.008):
 return mesh.box(name,p,size,material,0 if material in (plaster,floor) else bevel)
rod=mesh.rod
def group(name,parts,parent=None):return mesh.finish(parts,name,parent)
def anchor(name,p=(0,0,0)):return mesh.empty(name,p,room)
def state(name,parts,p=(0,0,0)):
 a=anchor(name,p);group(name+'_Mesh',parts,a);return a

def architecture():
 w,d,h=v['Width'],v['Depth'],v['Height']
 group('Room_Floor',[box('Floor',(0,-.1,0),(w,.2,d),floor)])
 group('Room_Ceiling',[box('Ceiling',(0,h+.1,0),(w+.4,.2,d+.4),plaster)])
 group('Wall_North',[box('North',(0,h/2,-6.1),(14.4,h,.2),plaster)])
 group('Wall_West',[box('West',(-7.1,h/2,0),(.2,h,12),plaster)])
 group('Wall_East',[box('EastNorth',(7.1,h/2,-1.3),(.2,h,9.4),plaster),
  box('EastSouth',(7.1,h/2,5.5),(.2,h,1),plaster),box('EastHeader',(7.1,3.4,4.2),(.2,1.6,1.6),plaster)])
 # Real south window openings, opaque piers, separate panes and sill joinery.
 parts=[];frames=[];panes=[];cursor=-7
 for x in (-3,0,3,5.3):
  width=1.65;low=1.45;high=3.45;a=x-width/2;b=x+width/2
  if a>cursor:parts.append(box('Pier',((a+cursor)/2,h/2,6.1),(a-cursor,h,.2),plaster))
  parts.extend([box('SillWall',(x,low/2,6.1),(width,low,.2),plaster),box('Header',(x,(h+high)/2,6.1),(width,h-high,.2),plaster)])
  for xx in (a,x,b):frames.append(box('Stile',(xx,(low+high)/2,5.99),(.06,high-low+.08,.14),paint))
  for y in (low,2.45,high):frames.append(box('Rail',(x,y,5.99),(width+.08,.07,.14),paint))
  frames.append(box('Sill',(x,low-.06,5.89),(width+.22,.1,.38),wood))
  panes.append(box('Pane',(x,(low+high)/2,6.025),(width-.03,high-low-.04,.02),glass,.001));cursor=b
 parts.append(box('EndPier',((cursor+7)/2,h/2,6.1),(7-cursor,h,.2),plaster))
 group('Wall_South',parts);group('WindowFrames',frames);group('WindowGlass',panes)
 parts=[]
 for p,s in [((0,.58,-5.99),(14,1.16,.035)),((0,.58,5.99),(14,1.16,.035)),((-6.99,.58,0),(.035,1.16,12)),((6.99,.58,-1.3),(.035,1.16,9.4)),((6.99,.58,5.5),(.035,1.16,1))]:parts.append(box('Dado',p,s,paint))
 for p,s in [((0,.07,-5.95),(14,.14,.055)),((0,.07,5.95),(14,.14,.055)),((-6.95,.07,0),(.055,.14,12)),((6.95,.07,-1.3),(.055,.14,9.4)),((6.95,.07,5.5),(.055,.14,1))]:parts.append(box('Skirting',p,s,wood))
 group('WallSkirting',parts)
 x,y,z=v['ExitHinge'];dw=v['DoorWidth'];dh=v['DoorHeight']
 hinge=anchor('Door_East_Hinge',(x,y,z));parts=[box('Leaf',(x,dh/2,z+dw/2),(.095,dh,dw-.025),wood)]
 for yy in (.52,1.43,2.25):parts.append(box('RaisedPanel',(x-.055,yy,z+dw/2),(.04,.54,dw-.25),paint))
 parts.extend([box('Escutcheon',(x-.078,1.08,z+1.32),(.03,.23,.11),brass),rod('Handle',(x-.14,1.07,z+1.18),(x-.14,1.07,z+1.4),.016,brass)])
 group('DoorLeaf',parts,hinge)
 group('DoorRebate',[box('Stop',(7.06,dh/2,zz),(.06,dh,.1),wood,0) for zz in (z+.035,z+dw-.035)]+[box('HeadStop',(7.06,dh-.025,z+dw/2),(.06,.08,dw),wood,0)])
 group('DoorFrame',[box('Jamb',(x,dh/2,zz),(.22,dh+.14,.12),wood) for zz in (z-.06,z+dw+.06)]+[box('Header',(x,dh+.07,z+dw/2),(.22,.14,dw+.24),wood)])

def table(name):
 x,y,z=v[name+'Position'];w,h,d=v[name+'Size'];parts=[box('Top',(x,h-.035,z),(w,.07,d),wood,.016)]
 for xx in (-w/2+.08,w/2-.08):
  for zz in (-d/2+.08,d/2-.08):parts.append(box('Leg',(x+xx,(h-.07)/2,z+zz),(.07,h-.07,.07),iron))
 for zz in (-d/2+.08,d/2-.08):parts.append(box('Apron',(x,h-.16,z+zz),(w-.1,.18,.04),wood))
 group(name,parts)

def cabinet(name,west=False,drawers=False):
 x,y,z=v[name+'Position'];w,h,d=v[name+'Size'];length=d if west else w;depth=w if west else d
 def p(a,b,c):return (x+c,b,z+a) if west else (x+a,b,z+c)
 def s(a,b,c):return (c,b,a) if west else (a,b,c)
 parts=[box('Back',p(0,h/2,-depth/2+.025),s(length,h,.05),paint)]
 for a in (-length/2+.035,length/2-.035):parts.append(box('Side',p(a,h/2,0),s(.07,h,depth),wood))
 rows=4;cols=max(2,round(length/.72));cw=(length-.14)/cols;rh=(h-.12)/rows
 for row in range(rows+1):parts.append(box('Shelf',p(0,.05+row*rh,0),s(length,.045,depth),wood))
 for col in range(1,cols):parts.append(box('Upright',p(-length/2+.07+col*cw,h/2,0),s(.035,h,depth),wood))
 group(name,parts)
 if gray:return
 contents=[]
 for row in range(rows):
  for col in range(cols):
   a=-length/2+.07+(col+.5)*cw;b=.09+row*rh
   if drawers:
    contents.extend([box('DrawerFront',p(a,b+rh/2,depth/2-.03),s(cw-.055,rh-.055,.055),wood),
      rod('Pull',p(a-.1,b+rh/2,depth/2+.017),p(a+.1,b+rh/2,depth/2+.017),.012,brass),
      box('Label',p(a,b+rh*.72,depth/2+.003),s(.13,.055,.005),paper,.001)])
   else:
    for k in range(3):
     aa=a+(k-1)*cw*.26;hh=rh*(.66+.06*((row+col+k)%3))
     contents.extend([box('FileBox',p(aa,b+hh/2,.02),s(cw*.22,hh,depth*.69),paper,.006),box('FileLabel',p(aa,b+hh*.64,depth*.38),s(cw*.15,.10,.005),ink,.001)])
 group(name+'_Contents',contents)

def document(name,p,w=.58,d=.4,facing='flat'):
 x,y,z=p
 def pp(a,b,c):return (x+a,y+b,z+c) if facing=='flat' else ((x+b,y-c,z-a) if facing=='east' else (x+a,y-c,z+(-b if facing=='north' else b)))
 def ss(a,b,c):return (a,b,c) if facing=='flat' else ((b,c,a) if facing=='east' else (a,c,b))
 parts=[box('Clipboard',pp(0,0,0),ss(w+.055,.025,d+.055),wood,.004),box('Paper',pp(0,.016,0),ss(w,.003,d),paper,.001)]
 if not gray:
  for i in range(7):parts.append(box('PrintedRule',pp(0,.018,-d*.34+i*d*.1),ss(w*(.62 if i%3 else .8),.0015,.006),ink,.0005))
  parts.append(box('Clip',pp(0,.026,-d*.47),ss(.13,.016,.04),brass,.002))
 group(name,parts)

def reel(name,p):
 x,y,z=p;parts=[]
 # Reel face looks west; rotate around its axle X in Godot.
 for xx in (-.027,.027):
  parts.append(rod('Rim',(x+xx-.008,y,z),(x+xx+.008,y,z),.235,silver,40))
  for a in range(6):
   angle=a*math.tau/6
   parts.append(rod('Vent',(x+xx-.01,y+.145*math.cos(angle),z+.145*math.sin(angle)),(x+xx-.016,y+.145*math.cos(angle),z+.145*math.sin(angle)),.049,iron,16))
 parts.append(rod('Hub',(x-.05,y,z),(x+.05,y,z),.045,brass,24))
 return state(name,parts,p)

def mechanisms():
 document('RegistryRulePaper',(-4.8,1.45,4.88),.67,.5,'north')
 for name,z in [('ChoirRecordPaper',1.4),('BroadcastRecordPaper',-1),('ArtRecordPaper',-3.8)]:document(name,(-6.035,1.45,z),.62,.54,'east')
 document('TrackRecordPaper',(-.7,1.45,-5.055),.65,.5,'south')
 document('FuseRoutePaper',(2.7,.815,-5.35),.85,.52)
 document('RegistryLedger',(-2.5,.815,-.1),.8,.55)
 state('Registry_Solved',[box('IndexCard',(-2.5,.848,.39),(.55,.012,.14),paper,.002)])
 for i in range(4):state('Registry_Preview'+str(i),[box('PlacedCard',(-3.2+i*.46,.835,.15),(.3,.008,.2),paper,.002)])
 for name,z in [('Drawer_Closed',-2.78),('Drawer_Open',-2.43)]:
  state(name,[box('DrawerFront',(-2.5,.91,z),(1.25,.27,.055),wood),box('DrawerBase',(-2.5,.775,z-.24),(1.22,.04,.5),wood),rod('DrawerHandle',(-2.65,.91,z+.05),(-2.35,.91,z+.05),.014,brass)])
 state('Pickup_IndexStrip',[box('FoldedIndex',(-3.1,.826,3.3),(.68,.012,.21),paper,.002),rod('RolledEnd',(-3.44,.85,3.2),(-3.44,.85,3.4),.032,paper,16)])
 parts=[box('FuseMat',(5.65,.812,-2.4),(1.1,.018,1.1),rubber),box('CeramicCarrier',(5.6,.87,-2.4),(.28,.09,.51),ivory)]
 for z in (-2.58,-2.22):parts.append(box('FuseClip',(5.6,.94,z),(.12,.05,.075),brass))
 if not gray:
  parts.extend([rod('Screwdriver',(5.99,.845,-2.78),(5.99,.845,-2.3),.012,silver),rod('Handle',(5.99,.845,-2.88),(5.99,.845,-2.68),.029,rubber)])
 group('FuseWorkbench',parts)
 state('Fuse_Repaired',[rod('CeramicFuse',(5.6,.94,-2.56),(5.6,.94,-2.24),.025,ivory,24),rod('FuseEnd',(5.6,.94,-2.575),(5.6,.94,-2.52),.03,brass)])
 group('RecorderBody',[box('Case',(5.65,1.055,1),(.54,.48,1.28),paint,.035),box('Face',(5.36,1.08,1),(.045,.44,1.18),iron),
  box('VuMeter',(5.329,.985,.93),(.016,.085,.18),ivory,.004),box('Speaker',(5.329,.98,1.25),(.016,.09,.27),rubber,.004)])
 reel('Recorder_Reel',(5.28,1.34,.67));reel('Recorder_Reel_Right',(5.28,1.34,1.33))
 state('Recorder_Crank',[rod('CrankAxle',(5.35,1.03,.48),(5.20,1.03,.48),.022,silver),rod('CrankArm',(5.20,1.03,.48),(5.20,.88,.48),.016,brass),rod('Grip',(5.20,.88,.48),(5.10,.88,.48),.025,rubber)])
 state('Recorder_Fuse',[rod('InstalledFuse',(5.32,.92,1.51),(5.32,1.12,1.51),.022,ivory)])
 state('Recorder_Running',[box('Indicator',(5.327,.94,.74),(.008,.018,.025),lamp,.002)])
 for name,sorted_ in [('Timeline_Unsorted',False),('Timeline_Sorted',True)]:
  parts=[]
  for i in range(4):
   z=5.32 if sorted_ else 5.25+(i%2)*.17
   parts.append(box('Receipt',(-.9+i*.6,.815+i*.001,z),(.46,.007,.34),paper,.002))
   if not gray:
    for j in range(5):parts.append(box('ReceiptRule',(-.9+i*.6,.82+i*.001,z-.11+j*.05),(.33,.001,.005),ink,.0005))
  state(name,parts)
 # Empty compatibility anchor; no obsolete vault or answer decoration.
 state('UnusedVaultState',[box('HiddenState',(0,-1,0),(.001,.001,.001),iron,0)])
 parts=[]
 for x in (-3,3):
  for z in (-3,2):
   parts.extend([rod('PendantStem',(x,4.19,z),(x,3.65,z),.018,iron),box('Housing',(x,3.61,z),(1.2,.11,.28),paint),box('Diffuser',(x,3.547,z),(1.08,.018,.22),lamp,.005)])
 for x,z in [(5.9,-2.8),(5.9,1.7)]:
  parts.extend([rod('TaskLampStem',(x,.81,z),(x,1.43,z),.019,iron),rod('TaskLampArm',(x,1.43,z),(x-.4,1.35,z),.019,iron),box('TaskLampShade',(x-.4,1.33,z),(.26,.08,.18),paint)])
 group('RoomFixtures',parts)

def configure():
 exporter.SPECS['archive']={'source':SOURCE,'glb':GLB,'manifest':MANIFEST,'baseline':'art/blender/dream_late_rooms/dream-archive-room-v1.blend','required':REQUIRED,'maxTriangles':180000,'maxVisibleMeshes':220,'maxMaterials':24,'maxBytes':60*1024*1024,'actualTextureMemory':True}
def metadata():
 p=root/MANIFEST;data=json.loads(p.read_text('utf-8'));data.update(layoutVersion=2,layoutSource=LAYOUT.as_posix(),layoutSha256=exporter.sha256(root/LAYOUT),layout=v,graybox=gray,visualAcceptance='pending-human-review',lightingDecision='Color ambient, south window light and practical task lights; no SSAO')
 data['sourceAssets']=[a for a in data['sourceAssets'] if a['id'] in {'oak_veneer_01','painted_plaster_wall','terrazzo_tiles'}] if not gray else []
 p.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n','utf-8')
 if gray:return
 record={'id':'dream_archive_room_runtime_v2','sourceFile':SOURCE.as_posix(),'gameFile':GLB.as_posix().removeprefix('assets/generated/'),'status':'adopted-runtime-default','generator':'Blender Python offline procedural modeling','model':bpy.app.version_string,'date':'2026-09-10','script':'scripts/tools/remodel_archive_v2.py','prompt':'Approved 14x12x4.2m school archive and recording repair room; west and north filing, central retrieval table, east repair and reel recorder, south timeline, real joinery and supported documents. Preserve 13 interaction IDs.','referenceImages':['outputs/late-rooms-v2/before'],'parameters':{'layoutSource':LAYOUT.as_posix(),'layoutVersion':2,'seed':0,'runtimeGeneration':False},'postprocessing':['metric UVs','applied bevels','packed adopted CC0 textures','stable physical state anchors; no character or animation data'],'sourceSha256':data['sourceSha256'],'runtimeGlbSha256':data['runtimeGlbSha256'],'sourceAssets':data['sourceAssets']}
 p=root/'assets/generated/provenance.json';s=p.read_text('utf-8');formatted=json.dumps(record,ensure_ascii=False,indent=2);needle='"id": "'+record['id']+'"'
 if needle in s:
  start=s.rfind('{',0,s.index(needle));_,n=json.JSONDecoder().raw_decode(s[start:]);s=s[:start]+formatted+s[start+n:]
 else:
  end=s.rfind(']');s=s[:end].rstrip()+',\n'+formatted+'\n'+s[end:]
 json.loads(s);p.write_text(s,'utf-8')
def build():
 global room,wood,paint,plaster,floor,brass,iron,paper,glass,lamp,ink,silver,ivory,rubber
 bpy.ops.wm.read_factory_settings(use_empty=True);mesh.root=root;mesh.gray=gray;mesh.collection=mesh.geo.collection('ArchiveRoomV2');room=mesh.empty('ArchiveRoom');mesh.hall_root=room
 wood=mesh.pbr('ArchiveV2_Oak',(.3,.2,.11),.76,asset='oak_veneer_01',tint=(.48,.32,.2));mesh.wood=wood
 plaster=mesh.pbr('ArchiveV2_Plaster',(.67,.65,.59),.93,asset='painted_plaster_wall',normal_strength=.17)
 floor=mesh.pbr('ArchiveV2_Terrazzo',(.43,.4,.36),.86,asset='terrazzo_tiles',normal_strength=.2)
 paint=mesh.pbr('ArchiveV2_Sage',(.23,.29,.26),.8,asset='painted_plaster_wall',diffuse=False,normal_strength=.1)
 brass=mesh.pbr('ArchiveV2_Brass',(.43,.31,.14),.45,.7);iron=mesh.pbr('ArchiveV2_Iron',(.055,.065,.06),.55,.5)
 paper=mesh.pbr('ArchiveV2_Paper',(.77,.72,.59),.96);ink=mesh.pbr('ArchiveV2_Ink',(.12,.15,.13),.95)
 silver=mesh.pbr('ArchiveV2_Nickel',(.54,.55,.52),.3,.8);ivory=mesh.pbr('ArchiveV2_Ceramic',(.78,.78,.69),.5);rubber=mesh.pbr('ArchiveV2_Rubber',(.021,.026,.023),.91)
 glass=mesh.pbr('ArchiveV2_Glass',(.47,.61,.59),.2);glass.node_tree.nodes.get('Principled BSDF').inputs['Alpha'].default_value=.2;glass.surface_render_method='DITHERED';glass.diffuse_color=(.47,.61,.59,.2)
 lamp=mesh.pbr('ArchiveV2_Lamp',(.86,.76,.52),.4,emission=.4)
 architecture()
 for name in ['RegistryTable','RepairBench','RecorderBench','TimelineTable','IndexTable','RouteTable']:table(name)
 cabinet('WestStacks',west=True);cabinet('NorthStacks');cabinet('SouthCabinet',drawers=True);cabinet('DrawerMatrix')
 mechanisms();bpy.context.view_layer.update()
 for obj in exporter.export_objects('archive'):
  if obj.type!='MESH':continue
  if not obj.data.uv_layers:project_uv(obj)
  bpy.context.view_layer.objects.active=obj
  for modifier in list(obj.modifiers):bpy.ops.object.modifier_apply(modifier=modifier.name)
 for im in bpy.data.images:
  if im.source=='FILE':im.pack()
 for k,value in {'sampleOnly':False,'runtimeDefault':True,'fullScene':True,'visualDirectionApproved':True,'visualAcceptance':'pending-human-review','layoutVersion':2,'graybox':gray}.items():bpy.context.scene[k]=value
 bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
 (root/SOURCE).parent.mkdir(parents=True,exist_ok=True);bpy.ops.wm.save_as_mainfile(filepath=str(root/SOURCE));exporter.run('archive','export',root);metadata()
def check():
 exporter.run('archive','check',root);data=json.loads((root/MANIFEST).read_text('utf-8'))
 if data['layoutSha256']!=exporter.sha256(root/LAYOUT):raise RuntimeError('Archive layout hash stale')
 if data['geometry']!=exporter.summarize('archive'):raise RuntimeError('Archive geometry manifest stale')
 print('ARCHIVE_V2_CHECK_PASS room=14x12x4.2 readOnly=true')
if __name__=='__main__':
 parser=argparse.ArgumentParser();parser.add_argument('mode',choices=['graybox','build','export','check']);parser.add_argument('--root',required=True);args=parser.parse_args(sys.argv[sys.argv.index('--')+1:]);root=Path(args.root).resolve();v=read_layout();gray=args.mode=='graybox';configure()
 if args.mode in ('graybox','build'):build()
 elif args.mode=='export':exporter.run('archive','export',root);metadata()
 else:check()
