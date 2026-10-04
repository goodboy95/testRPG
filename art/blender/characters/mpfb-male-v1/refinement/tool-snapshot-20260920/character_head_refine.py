"""Staged head refinement; corrections are additive and original assets remain intact."""
import json, hashlib
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]

def apply_correction(base,recipe):
    from mathutils import Vector
    cfg=recipe['style'].get('corrective')
    if not cfg:return
    path=(ROOT/cfg['path']).resolve()
    if not path.is_relative_to(ROOT/'art/blender/characters/mpfb-male-v1/refinement'):
        raise RuntimeError('Corrective outside source workspace')
    if hashlib.sha256(path.read_bytes()).hexdigest()!=cfg['sha256']:raise RuntimeError('Changed corrective source')
    data=json.loads(path.read_text(encoding='utf-8'))
    topology=json.dumps([list(p.vertices) for p in base.data.polygons],separators=(',',':')).encode()
    if len(base.data.vertices)!=data['vertexCount'] or hashlib.sha256(topology).hexdigest()!=data['topologySha256']:
        raise RuntimeError('Corrective topology mismatch')
    key=base.shape_key_add(name=data['name'],from_mix=False)
    inverse=base.matrix_world.inverted().to_3x3()
    for index,dx,dy,dz in data['deltas']:
        if not 0<=index<13380 or Vector((dx,dy,dz)).length>.003:raise RuntimeError('Corrective exceeds local repair scope')
        key.data[index].co+=inverse@Vector((dx,dy,dz))
    key.value=cfg['weight'];base['corrective_source']=cfg['path']

def finish_character(base,recipe):
    import bpy
    cfg=recipe['style']
    # Keep source eye UV; the derived second layer is inspectable and reversible.
    eyes=next(o for o in bpy.context.scene.objects if o.get('workflow_role')=='eyes')
    uv=eyes.data.uv_layers.new(name='RefinedIrisUV',do_init=True)
    eyes.data.uv_layers.active=uv;uv.active_render=True
    for loop in uv.data:
        for cx,cy in ((.291,.291),(.709,.703)):
            dx=loop.uv.x-cx;dy=loop.uv.y-cy;r=(dx*dx+dy*dy)**.5
            if r<.23:
                weight=1 if r<.13 else (.23-r)/.10
                scale=1+(cfg.get('irisScale',1)-1)*weight
                loop.uv=(cx+dx/scale,cy+dy/scale);break
    if cfg['phase'] in ('material','hair'):
        refine_skin(base,cfg)
        shorten_lashes(base,cfg)
    elif cfg['phase']!='structure':raise RuntimeError('Unvalidated refinement phase')
    if cfg['phase']=='hair':create_hair(base,cfg['hair'])

def curve_uv(mesh):
    """Native bevel curves generate geometry; add deterministic export-only UV if needed."""
    if mesh.uv_layers:return
    uv=mesh.uv_layers.new(name='CurveProjectionUV')
    for loop in mesh.loops:
        p=mesh.vertices[loop.vertex_index].co
        uv.data[loop.index].uv=(p.x*4+.5,(p.z-1.45)*4)

def create_hair(base,cfg):
    import bpy, math
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree
    graph=bpy.context.evaluated_depsgraph_get();ev=base.evaluated_get(graph);skin=ev.to_mesh()
    tree=BVHTree.FromPolygons([base.matrix_world@v.co for v in skin.vertices],[tuple(p.vertices) for p in skin.polygons]);ev.to_mesh_clear()
    center=Vector((0,-.035,1.528));vertices=[];uvs=[];faces=[]
    rows=48;columns=112
    def surface(t,phi):
        back=(1-math.cos(phi))*.5
        limit=1.08+.96*back+.10*abs(math.sin(phi))
        limit+=.018*math.sin(7*phi)+.009*math.sin(13*phi+.6)
        if cfg.get('revision',1)>=2:
            limit+=.17*max(0,-math.sin(phi))*max(0,math.cos(phi))+.022*math.sin(17*phi+.8)
        theta=max(.002,t*limit)
        direction=Vector((math.sin(theta)*math.sin(phi),-math.sin(theta)*math.cos(phi),math.cos(theta)))
        hit,normal,_,_=tree.ray_cast(center,direction,.3)
        if hit is None:raise RuntimeError('Scalp coverage projection missed')
        crown=math.cos(theta/2)**4
        lift=.006+.017*crown+.010*max(0,-math.sin(phi))*math.sin(theta)**2
        edge=min(1,max(0,(1-t)/.16))
        # Shallow swept variation belongs to the mass itself, not separate pasted ribbons.
        lift+=.0013*math.sin(8*phi+4*t)*math.sin(math.pi*t)**2
        if cfg.get('revision',1)>=2:
            lift=.003+.014*crown+.006*max(0,-math.sin(phi))*math.sin(theta)**2
            edge=math.sin(math.pi*.5*min(1,max(0,(1-t)/.42)))**.7
            lift+=.0007*math.sin(11*phi+3*t)*math.sin(math.pi*t)**2
        return hit+direction*(.00035+lift*edge)
    for r in range(rows+1):
        for c in range(columns+1):
            vertices.append(tuple(surface(r/rows,-math.pi+2*math.pi*c/columns)));uvs.append((c/columns,r/rows))
    for r in range(rows):
        for c in range(columns):
            a=r*(columns+1)+c;faces.append((a,a+columns+1,a+columns+2,a+1))
    mesh=bpy.data.meshes.new('Continuous_Hair_Volume');mesh.from_pydata(vertices,[],faces);mesh.update()
    uv=mesh.uv_layers.new(name='HairVolumeUV')
    for loop in mesh.loops:uv.data[loop.index].uv=uvs[loop.vertex_index]
    obj=bpy.data.objects.new('Hair_Volume',mesh);bpy.context.collection.objects.link(obj);obj['workflow_role']='hair.volume'
    obj['source']='Original volume projected to evaluated MPFB scalp; no old hairstyle reused'
    mat=bpy.data.materials.new('Dark_Brown_Hair');mat.use_nodes=True
    shader=mat.node_tree.nodes.get('Principled BSDF');shader.inputs['Base Color'].default_value=(.031,.020,.014,1)
    shader.inputs['Metallic'].default_value=0;shader.inputs['Roughness'].default_value=.68;shader.inputs['Specular IOR Level'].default_value=.25
    mesh.materials.append(mat)
    for p in mesh.polygons:p.use_smooth=True
    # Hair paths and their oval profile are the rendered geometry, not decorative guides.
    if not cfg.get('locks'):return
    profile=bpy.data.curves.new('Lock_Oval_Profile','CURVE');profile.dimensions='2D';profile.resolution_u=3
    spline=profile.splines.new('BEZIER');spline.bezier_points.add(7);spline.use_cyclic_u=True
    for i,p in enumerate(spline.bezier_points):
        a=i*math.tau/8;p.co=(math.cos(a),.38*math.sin(a),0);p.handle_left_type=p.handle_right_type='AUTO'
    pobj=bpy.data.objects.new('Editable_Hair_Cross_Section',profile);bpy.context.collection.objects.link(pobj);pobj.hide_render=True;pobj.hide_set(True)
    data=bpy.data.curves.new('Side_Part_Live_Locks','CURVE');data.dimensions='3D';data.resolution_u=12
    data.bevel_mode='OBJECT';data.bevel_object=pobj;data.use_fill_caps=True;data.twist_mode='Z_UP'
    volume_tree=BVHTree.FromPolygons([Vector(v) for v in vertices],faces)
    for lock in cfg['locks']:
        spline=data.splines.new('BEZIER');spline.bezier_points.add(len(lock['points'])-1)
        for p,co,radius,tilt in zip(spline.bezier_points,lock['points'],lock['radii'],lock['tilts']):
            direction=(Vector(co)-center).normalized();hit,normal,_,_=volume_tree.ray_cast(center,direction,.3)
            if hit is None:
                # Tapered ends may deliberately continue beyond the coverage boundary.
                hit,normal,_,_=tree.ray_cast(center,direction,.3)
                if hit is None:raise RuntimeError('Lock missed scalp')
                hit+=direction*.001
            p.co=hit+direction*lock.get('clearance',.0005);p.radius=radius;p.tilt=tilt;p.handle_left_type=p.handle_right_type='AUTO'
    locks=bpy.data.objects.new('Hair_Live_Curve_Locks',data);bpy.context.collection.objects.link(locks)
    locks['workflow_role']='hair.locks';locks['source']='Original editable Bezier paths with live oval bevel profile';data.materials.append(mat)

def refine_skin(base,cfg):
    import bpy, numpy as np, math
    original=base.data.materials[0];mat=original.copy();mat.name='Refined_Official_Skin';base.data.materials[0]=mat
    nodes=mat.node_tree.nodes;links=mat.node_tree.links
    shader=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
    source_node=shader.inputs['Base Color'].links[0].from_node
    if source_node.type!='TEX_IMAGE':raise RuntimeError('Expected official skin image')
    original_image=source_node.image
    # Final macro + corrective coordinates, with modifiers temporarily disabled to preserve indices.
    states=[(m,m.show_viewport) for m in base.modifiers]
    for m,_ in states:m.show_viewport=False
    bpy.context.view_layer.update();ev=base.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh()
    coords=[base.matrix_world@v.co for v in mesh.vertices];ev.to_mesh_clear()
    for m,enabled in states:m.show_viewport=enabled
    attribute=base.data.color_attributes.new(name='Anatomical_UV_Mask',type='FLOAT_COLOR',domain='POINT')
    def bell(x,c,w):return math.exp(-((x-c)/w)**2)
    for p,item in zip(coords,attribute.data):
        x,y,z=p;head=max(0,min(1,(z-1.38)/.045))
        lip=bell(x,0,.026)*bell(z,1.4475,.012)*bell(y,-.146,.015)
        eyes=bell(abs(x),.029,.021)*bell(z,1.521,.018)*bell(y,-.13,.027)
        tzone=bell(x,0,.026)*bell(z,1.49,.030)*bell(y,-.146,.020)+bell(z,1.568,.028)*bell(y,-.122,.032)
        item.color=(head*(1-.95*max(lip,eyes)),min(1,lip),min(1,tzone),1)
    width,height=original_image.size
    mask=bpy.data.images.new('Head_Anatomical_Mask',width=width,height=height,alpha=False);mask.colorspace_settings.name='Non-Color'
    tex=nodes.new('ShaderNodeTexImage');tex.image=mask;nodes.active=tex
    attr=nodes.new('ShaderNodeVertexColor');attr.layer_name=attribute.name
    emit=nodes.new('ShaderNodeEmission');links.new(attr.outputs['Color'],emit.inputs['Color'])
    output=next(n for n in nodes if n.type=='OUTPUT_MATERIAL');links.new(emit.outputs[0],output.inputs['Surface'])
    bpy.ops.object.select_all(action='DESELECT');base.select_set(True);bpy.context.view_layer.objects.active=base
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=1
    bpy.ops.object.bake(type='EMIT',margin=8)
    links.new(shader.outputs[0],output.inputs['Surface']);nodes.remove(emit);nodes.remove(attr);mask.pack()
    pixels=np.empty(width*height*4,dtype=np.float32);original_image.pixels.foreach_get(pixels);pixels=pixels.reshape(height,width,4)
    weights=np.empty(width*height*4,dtype=np.float32);mask.pixels.foreach_get(weights);weights=weights.reshape(height,width,4)
    def blur(array,sigma):
        radius=int(math.ceil(sigma*3));kernel=np.exp(-np.arange(-radius,radius+1,dtype=float)**2/(2*sigma*sigma));kernel/=kernel.sum()
        result=array
        for axis in (0,1):
            pads=[(0,0)]*3;pads[axis]=(radius,radius);padded=np.pad(result,pads,mode='reflect');out=np.zeros_like(result)
            for offset,k in enumerate(kernel):
                sl=[slice(None)]*3;sl[axis]=slice(offset,offset+result.shape[axis]);out+=padded[tuple(sl)]*k
            result=out
        return result
    sigma=cfg['materials'].get('detailRadiusPixels',2.2)*width/2048
    filtered=blur(pixels,sigma)*.8+blur(pixels,sigma*3)*.2
    strength=weights[:,:,0:1]*cfg['materials'].get('detailReduction',.68)
    result=pixels*(1-strength)+filtered*strength;result[:,:,3]=pixels[:,:,3]
    image=bpy.data.images.new('Skin_Refined_Albedo',width=width,height=height,alpha=True)
    image.pixels.foreach_set(result.ravel());image.update();image.pack();source_node.image=image
    rough=.72-.15*weights[:,:,1]-.08*weights[:,:,2]
    rgba=np.ones_like(pixels);rgba[:,:,:3]=rough[:,:,None]
    rough_image=bpy.data.images.new('Skin_Regional_Roughness',width=width,height=height,alpha=False);rough_image.colorspace_settings.name='Non-Color'
    rough_image.pixels.foreach_set(rgba.ravel());rough_image.update();rough_image.pack()
    rnode=nodes.new('ShaderNodeTexImage');rnode.image=rough_image;links.new(rnode.outputs['Color'],shader.inputs['Roughness'])
    shader.inputs['Specular IOR Level'].default_value=.23
    shader.inputs['Emission Strength'].default_value=0
    # The mask remains an inspectable packed image, not runtime vertex albedo.
    base.data.color_attributes.remove(attribute)
    for obj in bpy.context.scene.objects:
        if obj.get('workflow_role')=='eyebrows':
            for m in obj.data.materials:
                n=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
                n.inputs['Roughness'].default_value=.8

def shorten_lashes(base,cfg):
    import bpy
    from mathutils.bvhtree import BVHTree
    obj=next(o for o in bpy.context.scene.objects if o.get('workflow_role')=='eyelashes')
    # Pull protruding lash geometry toward its closest eyelid surface, preserving skin clearance.
    ev=base.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh()
    tree=BVHTree.FromPolygons([base.matrix_world@v.co for v in mesh.vertices],[tuple(p.vertices) for p in mesh.polygons]);ev.to_mesh_clear()
    group=obj.vertex_groups.new(name='Short_Natural_Lashes')
    for v in obj.data.vertices:
        p=obj.matrix_world@v.co;hit,normal,_,distance=tree.find_nearest(p)
        if hit is not None and distance>.0006:group.add([v.index],.55,'REPLACE')
    modifier=obj.modifiers.new('Lash tip shortening','SHRINKWRAP')
    modifier.target=base;modifier.wrap_method='NEAREST_SURFACEPOINT'
    modifier.wrap_mode='ON_SURFACE';modifier.offset=.0006;modifier.vertex_group=group.name
