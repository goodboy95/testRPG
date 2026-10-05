"""Saved-source bedroom target v2. Blender --python this_file -- gray|init|export|check.

Both initializers refuse existing saved sources. Export loads the saved source,
never rebuilds it. Coordinates in helpers are Godot metres (X, up, Z).
"""
import bpy, bmesh, math, random, json, hashlib, sys, shutil
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'art/blender/bedroom-target-v2'
DEST = ROOT / 'assets/generated/bedroom-target-v2'
M = {}

def xyz(p): return (p[0], -p[2], p[1])
def rgba(s):
    rgb = [int(s[i:i+2],16)/255 for i in (0,2,4)]
    return (*[v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in rgb],1)

def mat(name,color,rough=.8,metal=0):
    m=bpy.data.materials.new(name);m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF')
    bs.inputs['Base Color'].default_value=rgba(color)
    bs.inputs['Roughness'].default_value=rough
    bs.inputs['Metallic'].default_value=metal
    m.diffuse_color=rgba(color);M[name]=m
    return m

def finish(o,name,material):
    o.name=name
    if material:o.data.materials.append(M[material])
    return o

def box(name,p,size,material,bevel=.008):
    bpy.ops.mesh.primitive_cube_add(size=1,location=xyz(p));o=bpy.context.object
    o.dimensions=(size[0],size[2],size[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    finish(o,name,material)
    if bevel:
        b=o.modifiers.new('Furniture edge radius','BEVEL');b.width=bevel;b.segments=3
        bpy.ops.object.modifier_apply(modifier=b.name)
        n=o.modifiers.new('Board corner normals','WEIGHTED_NORMAL');n.keep_sharp=True
        bpy.ops.object.modifier_apply(modifier=n.name)
    return o

def sphere(name,p,size,material,segments=24):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=12,location=xyz(p));o=bpy.context.object
    o.scale=(size[0],size[2],size[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    finish(o,name,material)
    for poly in o.data.polygons:poly.use_smooth=True
    return o

def cylinder(name,a,b,r,material,vertices=16):
    a,b=Vector(xyz(a)),Vector(xyz(b));d=b-a
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=d.length,location=(a+b)/2)
    o=bpy.context.object;o.rotation_euler=d.to_track_quat('Z','Y').to_euler();finish(o,name,material)
    for poly in o.data.polygons:poly.use_smooth=True
    return o

def surface(name,verts,faces,material):
    mesh=bpy.data.meshes.new(name);mesh.from_pydata([xyz(v) for v in verts],[],faces);mesh.update()
    o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);finish(o,name,material)
    return o

def palette():
    for n,c,r,m in [('Plaster','dedbd2',.94,0),('Ceiling','e5e1d7',.94,0),('Floor','90949b',.43,0),
      ('Grout','777d84',.93,0),('Wood','b18960',.56,0),('WoodEdge','ad835b',.59,0),
      ('WoodDark','765a40',.68,0),('Blue','637595',.88,0),('Linen','e5dfce',.93,0),
      ('DarkMetal','454c50',.34,.7),('Window','656c6d',.32,.35),('Glass','b9d4db',.12,0),
      ('Paper','efe7d3',.87,0),('Green','567254',.7,0),('Leaf','617e43',.83,0),
      ('LeafLight','849c58',.87,0),('LeafDark','486944',.83,0),('Bag','394554',.91,0),
      ('Box','919084',.89,0),('Rug','b0a08a',.93,0),('Ceramic','dfdfd2',.38,0),
      ('Roof','62717b',.78,0),('House','d9d5c6',.88,0),('Hill','849e94',.94,0),
      ('HillFar','9bb0b3',.97,0),('Shoe','414750',.76,0),('Rubber','d7d5c8',.89,0),
      ('BookBlue','426b7c',.76,0),('BookOchre','b29c6b',.76,0)]:mat(n,c,r,m)
    bs=M['Glass'].node_tree.nodes.get('Principled BSDF')
    bs.inputs['Alpha'].default_value=.12
    M['Glass'].surface_render_method='DITHERED'
    M['Glass'].diffuse_color=(*rgba('b9d4db')[:3],.12)

def architecture(gray=False):
    box('FloorSlab',(0,-.075,0),(4.44,.15,4.84),'Grout')
    if gray:box('FloorFinish',(0,-.002,0),(4.2,.012,4.6),'Floor')
    else:
        for ix in range(7):
            for iz in range(8):
                width=.596;depth=.571
                box('FloorTile',(-1.8+ix*.6,.004,-2.0125+iz*.575),(width,.016,depth),'Floor',.003)
    box('Wall_Back',(0,1.45,-2.39),(4.44,2.9,.18),'Plaster')
    box('Wall_Left',(-2.19,1.45,0),(.18,2.9,4.6),'Plaster')
    # Right window: z [-1.55,.80], y [.98,2.63].
    box('Wall_Right_Sill',(2.19,.49,0),(.18,.98,4.6),'Plaster')
    box('Wall_Right_Lintel',(2.19,2.765,0),(.18,.27,4.6),'Plaster')
    box('Wall_Right_Back',(2.19,1.805,-1.925),(.18,1.65,.75),'Plaster')
    box('Wall_Right_Front',(2.19,1.805,1.55),(.18,1.65,1.50),'Plaster')
    # Front doorway x [-2.02,-.82].
    box('Wall_Front_Right',(.64,1.45,2.39),(2.92,2.9,.18),'Plaster')
    box('Wall_Front_Left',(-2.06,1.45,2.39),(.08,2.9,.18),'Plaster')
    box('Wall_Door_Lintel',(-1.42,2.60,2.39),(1.2,.60,.18),'Plaster')
    box('Ceiling',(0,2.96,0),(4.44,.12,4.84),'Ceiling')
    box('HallLanding',(-1.4,-.05,2.85),(1.6,.10,1.2),'Floor')
    if gray:return
    for p,s in [((0,.065,-2.285),(4.2,.13,.035)),((-2.085,.065,0),(.035,.13,4.6)),
                ((2.085,.065,0),(.035,.13,4.6)),((.64,.065,2.285),(2.92,.13,.035))]:box('Skirting',p,s,'Floor',.004)
    for z in (-1.55,.8):box('WindowJamb',(2.1,1.805,z),(.16,1.70,.065),'Window',.006)
    for y in (.98,2.63):box('WindowRail',(2.1,y,-.375),(.16,.065,2.4),'Window',.006)
    box('WindowMullion',(2.06,1.80,-.39),(.08,1.61,.05),'Window',.006)
    box('WindowCrossbar',(2.06,2.20,-.375),(.08,.045,2.29),'Window',.005)
    box('WindowSill',(2.02,.955,-.375),(.32,.06,2.53),'Ceramic',.012)
    for z in (-.95,.21):box('WindowGlass',(2.145,1.80,z),(.008,1.56,1.09),'Glass',0)
    for z in (-.47,-.30):cylinder('WindowLatch',(2.003,1.55,z),(2.003,1.72,z),.012,'DarkMetal')
    for x in (-2.02,-.82):box('DoorCasing',(x,1.16,2.265),(.085,2.32,.09),'WoodEdge')
    box('DoorCasingTop',(-1.42,2.315,2.265),(1.28,.085,.09),'WoodEdge')
    pivot=bpy.data.objects.new('BedroomDoorPivot',None);bpy.context.collection.objects.link(pivot);pivot.location=xyz((-2.0,0,2.31))
    door=box('DynamicDoor',(-1.46,1.115,2.31),(1.08,2.23,.045),'Wood',.009)
    for yy,h in ((.61,.7),(1.65,.66)):
        panel=box('DynamicDoorPanel',(-1.46,yy,2.278),(.83,h,.024),'WoodEdge',.01)
        panel.parent=pivot;panel.matrix_parent_inverse=pivot.matrix_world.inverted()
    handle=cylinder('DynamicDoorHandle',(-.98,1.0,2.225),(-1.12,1.0,2.225),.012,'DarkMetal')
    for o in (door,handle):o.parent=pivot;o.matrix_parent_inverse=pivot.matrix_world.inverted()
    pivot.rotation_euler.z=math.radians(97)

def gray_furniture():
    box('BedGray',(-1.34,.39,-.35),(1.32,.78,2.10),'Blue',.03)
    box('WardrobeGray',(-.38,1.09,-1.97),(1.17,2.18,.58),'Wood')
    box('DeskGray',(1.67,.74,-.3),(.63,.07,1.6),'Wood')
    box('DeskPedestalGray',(1.67,.35,.32),(.54,.70,.34),'Wood')
    box('ChairGray',(.95,.46,-.24),(.43,.08,.44),'Wood')

def handle(name,p,axis='y',length=.14):
    x,y,z=p
    if axis=='y':a=(x,y-length/2,z);b=(x,y+length/2,z)
    else:a=(x,y,z-length/2);b=(x,y,z+length/2)
    cylinder(name,a,b,.009,'DarkMetal',12)
    for q in (a,b):cylinder(name+'Foot',q,(q[0],q[1],q[2]-.021),.007,'DarkMetal',10)

def detailed_furniture():
    # Bed: individual boards, rails, fasteners and a rounded mattress.
    for x in (-1.94,-.74):
        for z in (-1.365,.665):
            box('BedPost',(x,.38,z),(.075,.76,.075),'WoodEdge',.013)
            for y in (.25,.59):
                sphere('BedBolt',(x+.04,y,z),(.007,.007,.007),'DarkMetal',12)
        box('BedSideRail',(x,.36,-.35),(.055,.14,2.05),'Wood')
    for z in (-1.38,.69):box('BedEndRail',(-1.34,.36,z),(1.28,.14,.065),'Wood')
    for i in range(10):box('BedSlat',(-1.34,.398,-1.22+i*.185),(1.22,.025,.075),'WoodDark',.003)
    box('HeadboardTop',(-1.34,.85,-1.395),(1.29,.045,.065),'WoodEdge')
    for y in (.57,.70):box('HeadboardBoard',(-1.34,y,-1.398),(1.21,.115,.04),'Wood')
    mattress=box('Mattress',(-1.34,.495,-.35),(1.22,.20,2.04),'Linen',.07)
    mattress.modifiers.new('Cloth collision mattress','COLLISION')
    mattress.collision.thickness_outer=.012
    # The fitted sheet edge follows the mattress perimeter, not a flat plane.
    for x in (-1.929,-.751):cylinder('SheetPiping',(x,.456,-1.30),(x,.456,.60),.005,'Linen',10)
    # Tall wardrobe with independently constructed doors and two lower drawers.
    cx=-.38
    for x in (cx-.55,cx+.55):box('WardrobeSide',(x,1.11,-1.97),(.06,2.14,.57),'Wood')
    box('WardrobeBack',(cx,1.11,-2.242),(1.08,2.11,.022),'WoodDark',.002)
    for y in (.08,2.17):box('WardrobeCap',(cx,y,-1.97),(1.20,.065,.62),'WoodEdge')
    for x in (cx-.283,cx+.283):
        box('WardrobeDoor',(x,1.37,-1.666),(.550,1.50,.034),'Wood',.006)
        # Recessed perimeter trim; a dark 6 mm central gap remains visible.
        for xx in (x-.255,x+.255):box('WardrobeDoorStile',(xx,1.37,-1.642),(.018,1.445,.013),'WoodEdge',.003)
        for yy in (.65,2.09):box('WardrobeDoorRail',(x,yy,-1.642),(.51,.018,.013),'WoodEdge',.003)
    for y in (.29,.505):
        box('WardrobeDrawer',(cx,y,-1.656),(1.07,.193,.041),'Wood')
        cylinder('WardrobeDrawerHandle',(cx-.075,y,-1.615),(cx+.075,y,-1.615),.008,'DarkMetal')
    for x in (cx-.06,cx+.06):handle('WardrobeHandle',(x,1.23,-1.603))
    # Writing desk: longitudinal under the right window.
    box('DeskTop',(1.66,.753,-.30),(.65,.055,1.68),'Wood',.013)
    box('DeskApron',(1.66,.676,-1.045),(.54,.11,.035),'Wood')
    for x in (1.43,1.90):
        for z in (-1.04,.46):box('DeskLeg',(x,.348,z),(.05,.697,.05),'WoodEdge')
    for x in (1.42,1.91):box('DeskPedestalSide',(x,.383,.272),(.035,.68,.44),'Wood')
    for y in (.065,.265,.468):
        box('DeskDrawer',(1.397,y+.09,.275),(.035,.176,.399),'Wood',.005)
        cylinder('DeskDrawerPull',(1.369,y+.09,.22),(1.369,y+.09,.33),.008,'DarkMetal')
    box('DeskPedestalBack',(1.926,.38,.275),(.02,.69,.44),'WoodDark')
    # Simple sturdy chair with slightly splayed legs and tilted backboards.
    box('ChairSeat',(.95,.465,-.24),(.47,.045,.48),'Wood',.012)
    for x in (.765,1.135):
        for z in (-.42,-.06):
            cylinder('ChairLeg',(x,.03,z),(x+(.02 if x<.95 else -.02),.455,z+(.018 if z<-.24 else -.018)),.023,'WoodEdge')
        cylinder('ChairSideBrace',(x,.20,-.43),(x,.20,-.035),.014,'WoodEdge')
    for z in (-.425,-.055):cylinder('ChairCrossBrace',(.765,.22,z),(1.135,.22,z),.012,'WoodDark')
    for z in (-.438,-.042):
        cylinder('ChairBackStile',(.755,.455,z),(.70,.935,z),.022,'WoodEdge')
    box('ChairBack',(.727,.83,-.24),(.043,.16,.38),'Wood',.012)
    pillow_mesh('ChairCushion',(.95,.507,-.24),(.41,.055,.40),'Blue',16)
    # Small bedside table in the foreground; keep the entry aisle clear.
    box('BedsideTop',(-1.77,.565,1.35),(.53,.036,.53),'Wood',.012)
    for x in (-1.99,-1.55):
        for z in (1.13,1.57):box('BedsideLeg',(x,.27,z),(.043,.54,.043),'WoodEdge')
    box('BedsideDrawer',(-1.77,.46,1.59),(.45,.13,.028),'Wood')
    handle('BedsideHandle',(-1.77,.46,1.616),'z',.10)
    box('BedsideShelf',(-1.77,.18,1.35),(.45,.027,.45),'Wood')

def pillow_mesh(name,p,size,material,resolution=32):
    w,h,d=size;verts=[];faces=[];N=resolution
    for side in (1,-1):
        for i in range(N+1):
            u=i/N*2-1
            for j in range(N+1):
                v=j/N*2-1
                loft=(max(0,1-u*u)*max(0,1-v*v))**.44
                crease=.009*math.sin(17*u+3*v)*(abs(v)**10)+.006*math.sin(19*v-2*u)*(abs(u)**10)
                x=p[0]+u*w/2*(1-.025*abs(v)**5)
                z=p[2]+v*d/2*(1-.055*abs(u)**5)
                y=p[1]+side*(.012+h*.48*loft)+crease
                verts.append((x,y,z))
    layer=(N+1)**2
    for side in range(2):
        for i in range(N):
            for j in range(N):
                a=side*layer+i*(N+1)+j;f=(a,a+1,a+N+2,a+N+1)
                faces.append(f if side==0 else tuple(reversed(f)))
    boundary=[i*(N+1) for i in range(N+1)]+[N*(N+1)+j for j in range(1,N+1)]+[i*(N+1)+N for i in range(N-1,-1,-1)]+[j for j in range(N-1,0,-1)]
    for a,b in zip(boundary,boundary[1:]+boundary[:1]):faces.append((a,b,b+layer,a+layer))
    o=surface(name,verts,faces,material)
    for poly in o.data.polygons:poly.use_smooth=True
    return o

def simulate_duvet():
    # Actual offline cloth solve over the rounded mattress, saved as a static mesh.
    N,K=62,78;verts=[];faces=[]
    rng=random.Random(52)
    folds=[(rng.uniform(-.8,.8),rng.uniform(-.8,.8),rng.uniform(.035,.085),rng.uniform(.15,.32)) for _ in range(7)]
    for i in range(N+1):
        u=i/N*2-1
        for j in range(K+1):
            v=j/K*2-1
            crest=sum(a*math.exp(-((u-x)**2+(v-z)**2)/(s*s)) for x,z,a,s in folds)
            x=-1.34+u*(.72 if u<0 else .89)
            z=.10+v*.84
            verts.append((x,.74+crest+.018*math.cos(v*8+u*2),z))
    for i in range(N):
        for j in range(K):
            a=i*(K+1)+j;faces.append((a,a+1,a+K+2,a+K+1))
    cloth=surface('Duvet_OfflineCloth',verts,faces,'Blue')
    bpy.context.view_layer.objects.active=cloth
    mod=cloth.modifiers.new('Offline cloth drape','CLOTH')
    s=mod.settings;s.quality=8;s.mass=.32;s.tension_stiffness=18;s.compression_stiffness=18
    s.shear_stiffness=10;s.bending_stiffness=.7;s.air_damping=3
    mod.collision_settings.use_collision=True;mod.collision_settings.distance_min=.014
    mod.collision_settings.use_self_collision=True;mod.collision_settings.self_distance_min=.011
    mod.point_cache.frame_start=1;mod.point_cache.frame_end=55
    bpy.context.scene.frame_end=55
    for frame in range(1,56):
        bpy.context.scene.frame_set(frame);bpy.context.view_layer.update()
        if frame%10==0:print('BEDROOM_CLOTH_FRAME',frame,flush=True)
    bpy.ops.object.modifier_apply(modifier=mod.name)
    # Broad asymmetric gathering around two hand-pulled folds. These local
    # corrections preserve the collision solve and avoid a regular wavy sheet.
    for vertex in cloth.data.vertices:
        x,z,y=vertex.co.x,-vertex.co.y,vertex.co.z
        if y>.54:
            vertex.co.z+=.042*math.exp(-((x+1.27)/.22)**2-((z-.02)/.55)**2)
            vertex.co.z+=.024*math.exp(-((x+.92)/.13)**2-((z-.49)/.40)**2)
    sub=cloth.modifiers.new('Duvet surface refinement','SUBSURF');sub.levels=1
    bpy.ops.object.modifier_apply(modifier=sub.name)
    thick=cloth.modifiers.new('Filled duvet thickness','SOLIDIFY');thick.thickness=.029;thick.offset=0
    bpy.ops.object.modifier_apply(modifier=thick.name)
    for poly in cloth.data.polygons:poly.use_smooth=True
    # Record the applied simulation; the runtime asset contains no solver.
    (SOURCE/'cloth-production.json').write_text(json.dumps({'solver':'Blender 5.2 cloth','frames':55,
        'grid':[N,K],'quality':8,'collision':'rounded mattress','selfCollision':True,
        'postprocess':'two local broad loft corrections, subdivision 1, thickness 0.029m',
        'runtimeSimulation':False},indent=2),encoding='utf8')

def curtain(name,z0,width,phase):
    # Hanging pleats widen slightly toward the hem; continuous double-sided fabric.
    N,K=32,44;verts=[];faces=[]
    for i in range(N+1):
        u=i/N
        for j in range(K+1):
            v=j/K
            z=z0+(u-.5)*width*(.92+.08*v)+.012*math.sin(v*5+u*3)
            x=1.93-.038*math.cos(u*math.pi*10+phase)-.008*math.sin(v*7+u*8)
            y=2.69-v*1.70+.018*math.cos(u*math.pi*10)*v**5
            verts.append((x,y,z))
    for i in range(N):
        for j in range(K):
            a=i*(K+1)+j;faces.append((a,a+K+1,a+K+2,a+1))
    o=surface(name,verts,faces,'Linen');bpy.context.view_layer.objects.active=o
    solid=o.modifiers.new('Curtain fabric thickness','SOLIDIFY');solid.thickness=.0025
    bpy.ops.object.modifier_apply(modifier=solid.name)
    for p in o.data.polygons:p.use_smooth=True
    for z in [z0-width*.42+i*width*.84/5 for i in range(6)]:
        sphere('CurtainEyelet',(1.93,2.7,z),(.022,.022,.009),'Ceramic',12)

def soft_furnishings():
    pillow_mesh('BedPillow',(-1.34,.656,-1.02),(.84,.15,.47),'Blue')
    simulate_duvet()
    curtain('CurtainBack',-1.43,.41,.1);curtain('CurtainFront',.70,.35,.9)
    cylinder('CurtainRod',(1.94,2.74,-1.75),(1.94,2.74,1.01),.013,'DarkMetal')
    for z in (-1.76,1.02):sphere('CurtainRodFinial',(1.94,2.74,z),(.024,.024,.024),'WoodDark',12)
    box('WovenRug',(-.55,.021,1.62),(1.12,.016,.72),'Rug',.012)
    for x in (-1.13,.03):
        for i in range(22):cylinder('RugFringe',(x,.019,1.28+i*.031),(x+(-.035 if x<0 else .035),.019,1.29+i*.031),.0025,'Rug',6)

def pot(name,p,r=.105,plantheight=.26):
    x,y,z=p
    bpy.ops.mesh.primitive_cone_add(vertices=24,radius1=r*.73,radius2=r,depth=r*1.45,location=xyz((x,y+r*.725,z)))
    finish(bpy.context.object,name+'Pot','Ceramic')
    cylinder(name+'Soil',(x,y+r*1.42,z),(x,y+r*1.44,z),r*.89,'WoodDark',24)
    rng=random.Random(name)
    for branch in range(7):
        a=branch*2.40;h=plantheight*(.50+branch*.08);end=(x+math.cos(a)*r*.66,y+r*1.5+h,z+math.sin(a)*r*.65)
        cylinder(name+'Stem',(x,y+r*1.4,z),end,.003,'Green',8)
        for j in range(3):
            yy=end[1]-.025*j;aa=a+j*1.5;length=r*(.7+rng.random()*.4)
            center=(end[0],yy,end[2]);tip=(center[0]+math.cos(aa)*length,yy+.018,center[2]+math.sin(aa)*length)
            side=(-math.sin(aa)*length*.28,0,math.cos(aa)*length*.28)
            middle=((center[0]+tip[0])/2,yy+.025,(center[2]+tip[2])/2)
            leaf=surface(name+'Leaf',[center,(middle[0]+side[0],middle[1],middle[2]+side[2]),tip,
                (middle[0]-side[0],middle[1],middle[2]-side[2]),middle],[(0,1,4),(1,2,4),(2,3,4),(3,0,4)],
                ('Leaf','LeafLight','LeafDark')[branch%3])
            for f in leaf.data.polygons:f.use_smooth=True

def book(name,p,size,material='BookBlue',open=False):
    x,y,z=p;w,h,d=size
    box(name+'Pages',(x,y+h/2,z),(w*.97,h*.74,d*.98),'Paper',.003)
    for yy in (y+.002,y+h):box(name+'Cover',(x,yy,z),(w+.01,.0035,d+.01),material,.002)
    box(name+'Spine',(x-w/2,y+h/2,z),(.012,h,d),material,.002)
    for i in range(3):cylinder(name+'PageEdge',(x+w/2,y+h*.2+i*h*.18,z-d*.45),(x+w/2,y+h*.2+i*h*.18,z+d*.45),.0006,'Linen',6)

def notebook(p):
    x,y,z=p;N=22;verts=[];faces=[]
    for side in (-1,1):
        for i in range(N+1):
            u=i/N
            for j in range(13):
                v=j/12
                verts.append((x+side*u*.17,y+.014+.025*(1-u)**3,z+(v-.5)*.23))
        start=0 if side==-1 else (N+1)*13
        for i in range(N):
            for j in range(12):
                a=start+i*13+j;f=(a,a+13,a+14,a+1);faces.append(f if side==1 else tuple(reversed(f)))
    o=surface('OpenNotebookPages',verts,faces,'Paper')
    for f in o.data.polygons:f.use_smooth=True
    for side in (-1,1):
        for i in range(8):
            zline=z-.09+i*.024
            cylinder('NotebookRule',(x+side*.025,y+.019,zline),(x+side*.159,y+.015,zline),.0005,'BookBlue',6)
    return o

def props():
    # Under-bed containers and a pair of ordinary shoes.
    for x,z,w in ((-1.53,.37,.57),(-1.50,-.35,.50)):
        box('UnderbedBox',(x,.141,z),(w,.27,.49),'Box',.024)
        box('UnderbedLid',(x,.27,z),(w+.02,.025,.51),'Box',.010)
        box('UnderbedLabel',(x+w*.503,.16,z),(.004,.045,.13),'Paper',.001)
    for x in (-1.82,-1.60):
        sphere('ShoeSole',(x,.036,.86),(.078,.027,.155),'Rubber')
        sphere('ShoeUpper',(x,.085,.89),(.070,.069,.12),'Shoe')
        for j in range(4):cylinder('Shoelace',(x-.025,.135-j*.005,.85+j*.024),(x+.026,.132-j*.005,.85+j*.024),.002,'Linen',8)
    for y in (2.225,2.47):
        box('WardrobeStorage',(-.60,y,-1.95),(.48,.24,.42),'Box',.018)
        box('StorageLid',(-.60,y+.126,-1.95),(.50,.023,.44),'Box',.008)
        box('StorageHandle',(-.60,y,-1.722),(.11,.018,.013),'DarkMetal',.003)
    box('WardrobeSmallStorage',(.012,2.30,-1.98),(.34,.24,.36),'BookOchre',.016)
    # Hanging backpack: actual volume, pocket, shoulder straps and zipper.
    box('BagHookBoard',(.57,1.83,-2.25),(.32,.075,.035),'Wood')
    cylinder('BagHook',(.57,1.82,-2.22),(.57,1.81,-2.16),.010,'DarkMetal')
    bag=pillow_mesh('Backpack',(.57,1.32,-2.04),(.32,.18,.47),'Bag',24)
    # Pillow helper is horizontal; orient the bag upright around its own centre.
    pivot=Vector(xyz((.57,1.32,-2.04)))
    from mathutils import Matrix
    rot=Matrix.Rotation(math.pi/2,4,'X')
    for v in bag.data.vertices:v.co=pivot+rot.to_3x3()@(v.co-pivot)
    box('BackpackPocket',(.57,1.22,-1.925),(.235,.18,.034),'Bag',.035)
    cylinder('BackpackZipper',(.457,1.315,-1.90),(.68,1.315,-1.90),.003,'DarkMetal',10)
    for x in (.48,.66):cylinder('BagHangingStrap',(x,1.54,-2.06),(.57,1.82,-2.16),.006,'Bag')
    # Desk books, open exercise paper, flask, pen cup and articulated lamp.
    book('DeskBook1',(1.67,.789,-.90),(.24,.035,.18),'BookBlue')
    book('DeskBook2',(1.67,.825,-.90),(.22,.023,.16),'BookOchre')
    notebook((1.63,.783,-.28))
    cylinder('Pen',(1.46,.814,-.22),(1.67,.814,-.30),.004,'BookBlue',10)
    cylinder('PenTip',(1.67,.814,-.30),(1.688,.812,-.306),.002,'DarkMetal',8)
    cylinder('Flask',(1.88,.783,-.75),(1.88,1.02,-.75),.042,'Green',24)
    cylinder('FlaskLid',(1.88,1.02,-.75),(1.88,1.046,-.75),.038,'DarkMetal',24)
    cylinder('PencilCup',(1.81,.783,.17),(1.81,.89,.17),.041,'Window',20)
    for i in range(5):
        x=1.79+(i%3)*.015;z=.15+(i//3)*.026
        cylinder('Pencil',(x,.89,z),(x+(i-2)*.009,1.02+(i%2)*.03,z+.007),.003,'BookOchre' if i%2 else 'BookBlue',8)
    cylinder('LampBase',(1.72,.782,.38),(1.72,.803,.38),.077,'Window',24)
    cylinder('LampLowerArm',(1.72,.80,.38),(1.72,1.15,.32),.008,'DarkMetal')
    cylinder('LampUpperArm',(1.72,1.15,.32),(1.58,1.23,.04),.007,'DarkMetal')
    sphere('LampJoint',(1.72,1.15,.32),(.021,.021,.021),'Window',16)
    bpy.ops.mesh.primitive_cone_add(vertices=24,radius1=.072,radius2=.031,depth=.067,location=xyz((1.58,1.205,.04)))
    finish(bpy.context.object,'LampShade','Window')
    sphere('LampBulb',(1.58,1.166,.04),(.027,.020,.027),'Ceramic',16)
    pot('WindowPlant',(1.98,.984,-1.11),.10,.23)
    # Bedside mug, books and plant stay outside the walking lane.
    book('BedsideBook1',(-1.77,.59,1.29),(.29,.025,.20),'BookBlue')
    book('BedsideBook2',(-1.77,.62,1.29),(.26,.024,.19),'BookOchre')
    cylinder('Mug',(-1.82,.646,1.28),(-1.82,.75,1.28),.041,'Ceramic',24)
    cylinder('MugTea',(-1.82,.747,1.28),(-1.82,.749,1.28),.033,'WoodDark',24)
    # Torus mug handle.
    bpy.ops.mesh.primitive_torus_add(major_segments=20,minor_segments=8,major_radius=.027,minor_radius=.007,location=xyz((-1.768,.697,1.28)),rotation=(math.pi/2,0,0))
    finish(bpy.context.object,'MugHandle','Ceramic')
    pot('EntrancePlant',(-1.97,.013,1.92),.12,.40)
    # No invented paper text: re-use the already authored localized notebook cover.
    cover=mat('NotebookCover','ffffff',.89)
    bs=cover.node_tree.nodes.get('Principled BSDF');tex=cover.node_tree.nodes.new('ShaderNodeTexImage')
    tex.image=bpy.data.images.load(str(ROOT/'assets/generated/environment-refinement-v1/graphics/notebook.png'));tex.image.pack()
    cover.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
    o=surface('ExerciseCover',[(1.51,.852,-.98),(1.51,.852,-.82),(1.74,.852,-.82),(1.74,.852,-.98)],[(0,1,2,3)],'NotebookCover')
    uv=o.data.uv_layers.new(name='UVMap')
    for loop,co in zip(o.data.loops,((0,0),(0,1),(1,1),(1,0))):uv.data[loop.index].uv=co
    # A modest wall clock uses the existing face asset; hands remain separate meshes.
    clock=mat('ClockFace','ffffff',.90)
    bs=clock.node_tree.nodes.get('Principled BSDF');tex=clock.node_tree.nodes.new('ShaderNodeTexImage')
    tex.image=bpy.data.images.load(str(ROOT/'assets/generated/environment-refinement-v1/graphics/clock.png'));tex.image.pack()
    clock.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
    center=(.65,2.16,2.282)
    cylinder('ClockCase',(.65,2.16,2.255),(.65,2.16,2.29),.145,'Window',48)
    verts=[center]+[(center[0]+.134*math.sin(i*math.tau/64),center[1]+.134*math.cos(i*math.tau/64),center[2]-.023) for i in range(64)]
    o=surface('ClockDial',verts,[(0,i+1,(i+1)%64+1) for i in range(64)],'ClockFace')
    uv=o.data.uv_layers.new(name='UVMap')
    for loop in o.data.loops:
        v=o.data.vertices[loop.vertex_index].co;uv.data[loop.index].uv=((v.x-.65)/.268+.5,(v.z-2.16)/.268+.5)
    # Neutral preview-only hands, no link to game time or clue state.
    cylinder('ClockHourHand',(.65,2.16,2.24),(.595,2.185,2.24),.004,'DarkMetal',10)
    cylinder('ClockMinuteHand',(.65,2.16,2.24),(.65,2.255,2.24),.003,'DarkMetal',10)

def exterior():
    # Actual geometry at several depths, so the view changes when approaching the window.
    box('OutsideGround',(12,-.22,0),(24,.3,28),'Green',0)
    rng=random.Random(91)
    for x,z,w,h,d in [(6.5,-3.8,3.0,3.3,3.2),(9.8,2.5,3.2,4.2,3.2),(13,-5.4,3.5,3.7,3.0),(15,6.1,4,3.4,3.2)]:
        box('OutsideHouse',(x,h/2,z),(w,h,d),'House',.035)
        verts=[(x-w*.58,h-.08,z-d*.60),(x+w*.58,h-.08,z-d*.60),
               (x+w*.58,h-.08,z+d*.60),(x-w*.58,h-.08,z+d*.60),(x,h+1.02,z-d*.60),(x,h+1.02,z+d*.60)]
        surface('OutsideRoof',verts,[(0,4,5,3),(4,1,2,5),(0,1,4),(3,5,2)],'Roof')
        for zz in (z-d*.28,z+d*.28):
            for yy in (1.3,h-.55):
                box('OutsideWindow',(x-w/2-.026,yy,zz),(.025,.63,.53),'Window',.005)
                box('OutsideWindowCentre',(x-w/2-.043,yy,zz),(.02,.63,.03),'House',.003)
        # Roof tile courses and eaves read as structure at medium distance.
        for i in range(12):
            t=i/11
            for side in (-1,1):
                xx=x+side*w*.58*t;yy=h+1.02-(1.10*t)
                cylinder('OutsideRoofCourse',(xx,yy,z-d*.60),(xx,yy,z+d*.60),.018,'Roof',8)
    for x,z,h in ((5.0,2.9,3.0),(7,-6.5,4.0),(11,0,3.2),(14,-1.4,4.3),(5.8,-1,2.5),(17,3.8,4.5)):
        cylinder('OutsideTreeTrunk',(x,0,z),(x,h*.75,z),.09,'WoodDark')
        for branch in range(8):
            a=branch*2.4;xx=x+math.cos(a)*(.55+branch*.028);zz=z+math.sin(a)*.7
            yy=h*.65+math.sin(branch*1.7)*.30
            cylinder('OutsideTreeBranch',(x,h*.53,z),(xx,yy,zz),.025,'WoodDark',8)
            sphere('OutsideTreeCrown',(xx,yy+.35,zz),(.52+branch*.022,.43,.48),('Leaf','LeafLight','LeafDark')[branch%3],16)
    for distance,material,height in ((28,'Hill',6.5),(43,'HillFar',9.3)):
        verts=[]
        for i in range(27):
            z=-30+i*2.4;hh=height+1.8*math.sin(i*.48)+1.3*math.cos(i*.91)
            verts.extend([(distance,0,z),(distance,hh,z),(distance+7,0,z)])
        faces=[]
        for i in range(26):
            a=i*3;b=a+3;faces.extend([(a,b,b+1,a+1),(a+1,b+1,b+2,a+2)])
        surface('OutsideMountain',verts,faces,material)

def textures():
    import numpy as np
    folder=SOURCE/'textures';folder.mkdir(exist_ok=True)
    # Original low-contrast grain. All textures are material inputs, not scene paintings.
    N=1024;v,u=np.mgrid[0:N,0:N].astype(np.float32)/N
    rng=np.random.default_rng(44)
    flow=u+.009*np.sin(v*9+u*5)+.003*np.sin(v*29+1)
    grain=.018*np.sin(flow*143)+.010*np.sin(flow*317+v*.7)+.007*np.sin(flow*911)
    cloud=.012*np.sin(u*12+v*4)*np.sin(v*8)+rng.normal(0,.0018,(N,N))
    base=np.array(rgba('b79a79')[:3],dtype=np.float32)
    color=np.clip(base[None,None,:]*(1+(grain+cloud)[:,:,None]),0,1)
    pixels=np.concatenate((color,np.ones((N,N,1),dtype=np.float32)),axis=2)
    image=bpy.data.images.new('Bedroom restrained wood albedo',width=N,height=N)
    image.pixels.foreach_set(pixels.astype(np.float32).ravel());image.filepath_raw=str(folder/'wood-albedo.png');image.file_format='PNG';image.save();image.pack()
    for key in ('Wood','WoodEdge'):
        mat=M[key];bs=mat.node_tree.nodes.get('Principled BSDF');tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image
        mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
    # Retained licensed normal maps are subtle; silhouettes come from geometry.
    for keys,path,strength in [
        (('Blue','Linen','Bag','Rug'),'art/sources/cc0/village-art-v3/denim_fabric/denim_fabric_nor_gl_1k.png',.18),
        (('Plaster',),'art/sources/cc0/polyhaven/environment-cinematic-v1/painted_plaster_wall/painted_plaster_wall_nor_gl_2k.jpg',.12)]:
        image=bpy.data.images.load(str(ROOT/path));image.colorspace_settings.name='Non-Color';image.pack()
        for key in keys:
            m=M[key];bs=m.node_tree.nodes.get('Principled BSDF');tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image
            norm=m.node_tree.nodes.new('ShaderNodeNormalMap');norm.inputs['Strength'].default_value=strength
            m.node_tree.links.new(tex.outputs['Color'],norm.inputs['Color']);m.node_tree.links.new(norm.outputs['Normal'],bs.inputs['Normal'])
    for o in bpy.context.scene.objects:
        if o.type!='MESH' or o.name.startswith(('ExerciseCover','ClockDial')):continue
        uv=o.data.uv_layers.active or o.data.uv_layers.new(name='UVMap')
        bb=[Vector(v) for v in o.bound_box];span=[max(v[i] for v in bb)-min(v[i] for v in bb) for i in range(3)]
        main=max(range(3),key=lambda i:span[i])
        wooden=o.data.materials and o.data.materials[0].name in ('Wood','WoodEdge','WoodDark')
        for poly in o.data.polygons:
            normalaxis=max(range(3),key=lambda i:abs(poly.normal[i]))
            axes=[i for i in range(3) if i!=normalaxis]
            long=main if main in axes else axes[1];short=next(i for i in axes if i!=long)
            for loopindex in poly.loop_indices:
                co=o.data.vertices[o.data.loops[loopindex].vertex_index].co
                # The image grain runs along V; choose the board's long axis explicitly.
                uv.data[loopindex].uv=(co[short]*1.8,co[long]*.9) if wooden else (co[axes[0]]*3,co[axes[1]]*3)
    provenance={'date':'2026-10-05','tool':'Blender 5.2.0 LTS Python mesh authoring + offline cloth simulation',
      'styleReference':'art/concepts/environment-refinement-v1/bedroom-target.png','newAIImages':False,
      'wood':'Original procedural restrained grain; saved PNG source; board-axis UV mapping',
      'normalMaps':[{'path':'art/sources/cc0/village-art-v3/denim_fabric/denim_fabric_nor_gl_1k.png','license':'CC0; see retained files.json'},
                    {'path':'art/sources/cc0/polyhaven/environment-cinematic-v1/painted_plaster_wall/painted_plaster_wall_nor_gl_2k.jpg','license':'CC0; retained Poly Haven sources'}],
      'graphics':'Existing notebook and clock from environment-refinement-v1/graphics/manifest.json; hands separate geometry',
      'characters':False,'authoringMethod':'Scripted bespoke meshes, applied cloth solver and local mesh correction; not hand high-poly sculpting'}
    (SOURCE/'provenance.json').write_text(json.dumps(provenance,ensure_ascii=False,indent=2),encoding='utf8')

def refine():
    """One saved-source correction pass, retaining the complete initial source."""
    source=SOURCE/'bedroom-target-v2.blend';marker=SOURCE/'refinement-v1.json'
    if marker.exists():raise RuntimeError('This source refinement is already applied; edit the saved source instead.')
    revisions=SOURCE/'revisions';revisions.mkdir(exist_ok=True)
    shutil.copy2(source,revisions/'initial-cloth.blend')
    bpy.ops.wm.open_mainfile(filepath=str(source))
    M.update({m.name:m for m in bpy.data.materials})
    # Move surrounding houses below the room and farther away, opening the sky
    # and sunlight path. These are independent scenery, not the formal village.
    for o in bpy.context.scene.objects:
        if o.name.startswith('Exterior'):
            for v in o.data.vertices:
                world=o.matrix_world@v.co
                world.x=2.1+(world.x-2.1)*1.65
                world.y*=1.20;world.z-=1.45
                v.co=o.matrix_world.inverted()@world
    # Exported image pixels use sRGB encoding; avoid darkening the wood twice.
    import numpy as np
    image=next(i for i in bpy.data.images if i.name.startswith('Bedroom restrained wood'))
    a=np.empty(len(image.pixels),dtype=np.float32);image.pixels.foreach_get(a);a=a.reshape(-1,4)
    a[:,:3]=np.where(a[:,:3]<=.0031308,a[:,:3]*12.92,1.055*np.power(np.maximum(a[:,:3],0),1/2.4)-.055)
    image.pixels.foreach_set(a.ravel());image.pack();image.save()
    # Fix page winding and bring the printed clock face in front of its case.
    for o in bpy.context.scene.objects:
        if o.type!='MESH':continue
        if o.name=='Furniture_ClockFace':
            for v in o.data.vertices:v.co.y=-2.248
        if o.name=='Furniture_Paper':
            bm=bmesh.new();bm.from_mesh(o.data)
            selected=[]
            for f in bm.faces:
                c=o.matrix_world@f.calc_center_median()
                if 1.43<c.x<1.82 and -.43<-c.y<-.12 and .78<c.z<.84 and f.normal.z<-.2:selected.append(f)
            bmesh.ops.reverse_faces(bm,faces=selected);bm.to_mesh(o.data);bm.free()
    # Gather the draped cloth into large, asymmetric folds, maintaining its drop.
    quilt=bpy.data.objects['Furniture_Blue']
    folds=[(-1.55,-.25,.08,.31,.09,.4),(-1.07,.12,.06,.35,.08,-.6),
           (-1.63,.39,.075,.24,.095,-.3),(-1.01,.51,.053,.28,.11,.55),(-1.19,-.50,.04,.30,.075,.65)]
    for v in quilt.data.vertices:
        p=quilt.matrix_world@v.co;x,z,y=p.x,-p.y,p.z
        if z>-.77 and y>.48:
            mask=min(1,max(0,(y-.48)/.075))
            bump=0
            for cx,cz,height,w,d,angle in folds:
                u=(x-cx)*math.cos(angle)+(z-cz)*math.sin(angle)
                vv=-(x-cx)*math.sin(angle)+(z-cz)*math.cos(angle)
                bump+=height*math.exp(-(u/w)**2-(vv/d)**2)
            p.z+=mask*bump;v.co=quilt.matrix_world.inverted()@p
    # Subdivision rounds pillow/cushion seams and the bag's grid corners.
    for key in ('Furniture_Blue','Furniture_Bag'):
        o=bpy.data.objects[key];bpy.context.view_layer.objects.active=o
        bpy.ops.object.select_all(action='DESELECT');o.select_set(True)
        sub=o.modifiers.new('Soft seam correction','SUBSURF');sub.levels=1
        bpy.ops.object.modifier_apply(modifier=sub.name)
        for f in o.data.polygons:f.use_smooth=True
    bs=M['Blue'].node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=rgba('7889a7')
    for key in ('Blue','Linen','Bag'):
        for n in M[key].node_tree.nodes:
            if n.type=='NORMAL_MAP':n.inputs['Strength'].default_value=.08
    # A short finished hall is visible through the doorway in reverse views.
    for name,p,s in [('HallLeft',(-2.25,1.45,2.92),(.10,2.9,1.22)),
                     ('HallRight',(-.55,1.45,2.92),(.10,2.9,1.22)),
                     ('HallEnd',(-1.4,1.45,3.5),(1.8,2.9,.12)),
                     ('HallCeiling',(-1.4,2.95,2.95),(1.8,.10,1.2))]:box(name,p,s,'Plaster',.002)
    # Consistent planar UVs on fabric prevent per-face projection seams.
    for key in ('Furniture_Blue','Furniture_Bag','Furniture_Linen'):
        o=bpy.data.objects[key];uv=o.data.uv_layers.active
        for loop in o.data.loops:
            p=o.matrix_world@o.data.vertices[loop.vertex_index].co
            uv.data[loop.index].uv=(p.x*3,p.y*3) if key!='Furniture_Bag' else (p.x*3,p.z*3)
        bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
    # Merge just the new hall walls, preserving the rest of the saved geometry.
    objects=[o for o in bpy.context.scene.objects if o.name.startswith('Hall') and o.type=='MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();bpy.context.object.name='Architecture_Hall'
    bpy.ops.wm.save_as_mainfile(filepath=str(source))
    marker.write_text(json.dumps({'retained':'revisions/initial-cloth.blend','changes':[
        'opened scenic sunlight path','sRGB wood encoding','paper normals and clock face depth',
        'asymmetric duvet loft folds','rounded fabric seams','finished short hall','continuous fabric UVs']},indent=2),encoding='utf8')
    export(source,'bedroom-target-v2.glb')

def repair_graphics():
    source=SOURCE/'bedroom-target-v2.blend'
    bpy.ops.wm.open_mainfile(filepath=str(source));o=bpy.data.objects['Furniture_Paper']
    bm=bmesh.new();bm.from_mesh(o.data);faces=[]
    for f in bm.faces:
        c=o.matrix_world@f.calc_center_median()
        if 1.43<c.x<1.82 and -.43<-c.y<-.12 and .78<c.z<.84 and f.normal.z<-.2:faces.append(f)
    if not faces:print('BEDROOM_PAPER_ALREADY_CORRECT');bm.free();return
    bmesh.ops.reverse_faces(bm,faces=faces);bm.to_mesh(o.data);bm.free()
    print('BEDROOM_PAPER_CORRECTED',len(faces))
    bpy.ops.wm.save_as_mainfile(filepath=str(source));export(source,'bedroom-target-v2.glb')

def finish_review():
    """Saved-source response to the actual five-angle Godot review."""
    source=SOURCE/'bedroom-target-v2.blend';marker=SOURCE/'review-corrections.json'
    if marker.exists():raise RuntimeError('Review corrections already applied; edit saved source.')
    shutil.copy2(source,SOURCE/'revisions/pre-review-corrections.blend')
    bpy.ops.wm.open_mainfile(filepath=str(source));M.update({m.name:m for m in bpy.data.materials})
    quilt=bpy.data.objects['Furniture_Blue']
    corrected=0
    for v in quilt.data.vertices:
        p=quilt.matrix_world@v.co;depth=-p.y
        # The initial solver collided with the mattress, not the lower end rail.
        # Move only the hanging foot section in front of the entire bed frame.
        if depth>.57 and p.z<.82 and p.x<-.45:
            weight=min(1,max(0,(depth-.57)/.09))
            p.y=-max(depth,depth+(max(0,.79-depth))*weight)
            v.co=quilt.matrix_world.inverted()@p;corrected+=1
    # Replace the coarse ellipsoid canopy with branched, layered leaves.
    for o in list(bpy.context.scene.objects):
        if o.name.startswith('Exterior_Leaf'):bpy.data.objects.remove(o,do_unlink=True)
    rng=random.Random(159)
    tree_points=((5.0,2.9,3.0),(7,-6.5,4.0),(11,0,3.2),(14,-1.4,4.3),(5.8,-1,2.5),(17,3.8,4.5))
    verts={key:[] for key in ('Leaf','LeafLight','LeafDark')};faces={key:[] for key in verts}
    for x,z,h in tree_points:
        for branch in range(8):
            angle=branch*2.4
            center=Vector((x+math.cos(angle)*(.55+branch*.028),h*.65+math.sin(branch*1.7)*.30+.35,z+math.sin(angle)*.7))
            for _ in range(180):
                a=rng.uniform(0,math.tau);b=rng.uniform(-1,1);radius=rng.random()**.32
                p=center+Vector((math.cos(a)*math.sqrt(1-b*b)*radius*.64,b*radius*.47,math.sin(a)*math.sqrt(1-b*b)*radius*.60))
                yaw=rng.uniform(0,math.tau);pitch=rng.uniform(-.65,.65);length=rng.uniform(.08,.15)
                axis=Vector((math.cos(yaw)*math.cos(pitch),math.sin(pitch),math.sin(yaw)*math.cos(pitch)))*length
                side=Vector((-math.sin(yaw),0,math.cos(yaw)))*length*.32
                key=rng.choices(('Leaf','LeafLight','LeafDark'),weights=(5,2,3))[0]
                start=len(verts[key]);v=[p-axis,p-side,p+Vector((0,.018,0)),p+side,p+axis]
                for point in v:
                    verts[key].append((2.1+(point.x-2.1)*1.65,point.y-1.45,point.z*1.2))
                # Both sides are real geometry, so side views remain readable.
                for f in ((0,1,2),(1,4,2),(4,3,2),(3,0,2)):
                    face=tuple(start+i for i in f);faces[key].extend((face,tuple(reversed(face))))
    for key in verts:
        o=surface('Exterior_'+key,verts[key],faces[key],key)
        for p in o.data.polygons:p.use_smooth=False
    bpy.ops.wm.save_as_mainfile(filepath=str(source))
    marker.write_text(json.dumps({'duvetFootVerticesCorrected':corrected,'exteriorLeaves':8640,
        'retained':'revisions/pre-review-corrections.blend','reason':'Actual Godot foot-rail penetration and coarse window canopy'},indent=2),encoding='utf8')
    export(source,'bedroom-target-v2.glb')

def repair_contact():
    source=SOURCE/'bedroom-target-v2.blend';marker=SOURCE/'contact-correction.json'
    if marker.exists():raise RuntimeError('Contact correction already applied.')
    bpy.ops.wm.open_mainfile(filepath=str(source));quilt=bpy.data.objects['Furniture_Blue']
    # Preserve both faces and filled thickness; flattening them to a common
    # depth causes coplanar self-overlap in the hanging part.
    with bpy.data.libraries.load(str(SOURCE/'revisions/pre-review-corrections.blend'),link=False) as (data,new):
        new.objects=['Furniture_Blue']
    old=new.objects[0];quilt.data=old.data.copy();bpy.data.objects.remove(old,do_unlink=True)
    for v in quilt.data.vertices:
        p=quilt.matrix_world@v.co;depth=-p.y
        if depth>.57 and p.z<.82 and p.x<-.45:
            weight=min(1,max(0,(depth-.57)/.09));weight=weight*weight*(3-2*weight)
            p.y-=.17*weight;v.co=quilt.matrix_world.inverted()@p
    dial=bpy.data.objects['Furniture_ClockFace'];uv=dial.data.uv_layers.active
    for loop in dial.data.loops:uv.data[loop.index].uv.x=1-uv.data[loop.index].uv.x
    bpy.ops.wm.save_as_mainfile(filepath=str(source))
    marker.write_text(json.dumps({'duvet':'Smooth additive drape offset preserves filled thickness; no planar collapse',
        'clock':'UV horizontal handedness corrected for viewing the front wall from inside'},indent=2),encoding='utf8')
    export(source,'bedroom-target-v2.glb')

def repair_cover():
    source=SOURCE/'bedroom-target-v2.blend';marker=SOURCE/'cover-correction.json'
    if marker.exists():raise RuntimeError('Cover correction already applied.')
    bpy.ops.wm.open_mainfile(filepath=str(source))
    cover=bpy.data.objects['Furniture_NotebookCover'];uv=cover.data.uv_layers.active
    for loop in cover.data.loops:uv.data[loop.index].uv.y=1-uv.data[loop.index].uv.y
    # Raise the pen above the curved open pages rather than passing through them.
    for key in ('Furniture_BookBlue','Furniture_DarkMetal'):
        o=bpy.data.objects[key]
        for v in o.data.vertices:
            p=o.matrix_world@v.co
            if 1.445<p.x<1.697 and -.315<-p.y<-.20 and .806<p.z<.821:
                p.z+=.025;v.co=o.matrix_world.inverted()@p
    bpy.ops.wm.save_as_mainfile(filepath=str(source))
    marker.write_text(json.dumps({'cover':'Correct top-facing UV handedness; retain original localized image',
        'pen':'Raised 0.025m over curved notebook'},indent=2),encoding='utf8')
    export(source,'bedroom-target-v2.glb')

def merge_static():
    # Keep glass, floor, architecture and furniture separate for UV density and editing.
    groups={}
    for o in list(bpy.context.scene.objects):
        if o.type!='MESH' or o.parent:continue
        cat=('Floor' if o.name.startswith('Floor') else 'Architecture' if o.name.startswith(('Wall','Ceiling','Skirting','Hall'))
             else 'Exterior' if o.name.startswith('Outside') else 'Furniture')
        matname=o.data.materials[0].name if o.data.materials else 'None'
        if matname=='Glass':cat='Glass'
        groups.setdefault((cat,matname),[]).append(o)
    for (cat,matname),objects in groups.items():
        bpy.ops.object.select_all(action='DESELECT')
        for o in objects:o.select_set(True)
        bpy.context.view_layer.objects.active=objects[0]
        bpy.ops.object.join();bpy.context.object.name=f'{cat}_{matname}'

def export(source,filename):
    bpy.ops.wm.open_mainfile(filepath=str(source))
    DEST.mkdir(parents=True,exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=str(DEST/filename),export_format='GLB',export_yup=True,
        export_animations=False,export_cameras=False,export_lights=False,export_materials='EXPORT',export_image_format='AUTO')
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    report={'source':str(source.relative_to(ROOT)),'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),
      'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes),
      'meshObjects':len(meshes),'materials':len(bpy.data.materials),'roomMetres':[4.2,4.6,2.9],
      'characters':0,'runtimeClothSimulation':False,'formalAdoption':False,'visualApproval':False}
    (DEST/(filename.replace('.glb','-manifest.json'))).write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')

def init(gray):
    SOURCE.mkdir(parents=True,exist_ok=True)
    source=SOURCE/('bedroom-gray.blend' if gray else 'bedroom-target-v2.blend')
    if source.exists():raise RuntimeError('Refusing to overwrite saved source: '+str(source))
    bpy.ops.wm.read_factory_settings(use_empty=True);palette();architecture(gray)
    if gray:gray_furniture()
    else:detailed_furniture();soft_furnishings();props();exterior();textures()
    merge_static();bpy.ops.wm.save_as_mainfile(filepath=str(source))
    export(source,'bedroom-gray.glb' if gray else 'bedroom-target-v2.glb')

if __name__=='__main__':
    command=sys.argv[sys.argv.index('--')+1]
    if command in ('gray','init'):init(command=='gray')
    elif command=='refine':refine()
    elif command=='repair-graphics':repair_graphics()
    elif command=='finish-review':finish_review()
    elif command=='repair-contact':repair_contact()
    elif command=='repair-cover':repair_cover()
    elif command=='export':export(SOURCE/'bedroom-target-v2.blend','bedroom-target-v2.glb')
    elif command=='check':
        bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'bedroom-target-v2.blend'))
        assert not any(o.type=='ARMATURE' for o in bpy.context.scene.objects)
        assert not any(m.type=='CLOTH' for o in bpy.context.scene.objects for m in o.modifiers)
        assert bpy.data.objects.get('BedroomDoorPivot') is not None
        print('BEDROOM_SOURCE_CHECK_PASS')
    else:raise ValueError(command)
