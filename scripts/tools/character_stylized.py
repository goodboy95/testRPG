"""Editable stylized character materials and original opaque hair, Blender only.

The recipe owns all design data. No restricted character is loaded or sampled.
"""
import math


def make_material(name, color, roughness):
    import bpy
    mat=bpy.data.materials.new(name);mat.use_nodes=True
    shader=mat.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value=(*color,1)
    shader.inputs['Roughness'].default_value=roughness
    shader.inputs['Specular IOR Level'].default_value=.22
    return mat


def apply_materials(base, recipe):
    import bpy
    config=recipe['style']['materials']
    # Broad anatomical color zones are vertex colors, not painted lighting.
    # Original UVs/topology and original CC0 material datablocks remain in the source.
    skin=make_material('Skin_Stylized', (1,1,1),config['skinRoughness'])
    colors=base.data.color_attributes.new(name='SkinTone',type='FLOAT_COLOR',domain='POINT')
    def bell(value,center,width):return math.exp(-((value-center)/width)**2)
    for vertex,item in zip(base.data.vertices,colors.data):
        x,y,z=base.matrix_world@vertex.co
        front=bell(y,-.126,.03)
        cheek=bell(abs(x),.046,.023)*bell(z,1.473,.032)*front
        nose=bell(x,0,.013)*bell(z,1.471,.018)*front
        lips=bell(x,0,.022)*bell(z,config.get('lipHeight',1.431),.010)*bell(y,-.141,.014)
        ears=bell(abs(x),.069,.012)*bell(z,1.48,.04)
        warm=min(1,.4*cheek+.2*nose+.12*ears)
        tone=config['skinColor'];lip=config['lipColor']
        c=[tone[0]+.035*warm,tone[1]-.035*warm,tone[2]-.028*warm]
        c=[c[i]*(1-.65*lips)+lip[i]*.65*lips for i in range(3)]
        item.color=(*c,1)
    nodes=skin.node_tree.nodes;attr=nodes.new('ShaderNodeVertexColor');attr.layer_name='SkinTone'
    skin.node_tree.links.new(attr.outputs['Color'],nodes['Principled BSDF'].inputs['Base Color'])
    base.data.materials.clear();base.data.materials.append(skin)
    for obj in bpy.context.scene.objects:
        if obj.type!='MESH':continue
        role=obj.get('workflow_role','')
        for mat in obj.data.materials:
            if not mat.use_nodes:continue
            bsdf=mat.node_tree.nodes.get('Principled BSDF')
            if not bsdf:continue
            if role.startswith('clothes.'):
                bsdf.inputs['Roughness'].default_value=config['clothRoughness']
                bsdf.inputs['Specular IOR Level'].default_value=.16
                for node in mat.node_tree.nodes:
                    if node.type=='NORMAL_MAP':node.inputs['Strength'].default_value=config['clothNormalStrength']
            if role=='eyes' and config.get('irisScale'):
                # Preserve original UV layer; modify only UVs near the two iris islands.
                original=obj.data.uv_layers.active
                uv=obj.data.uv_layers.new(name='StylizedIrisUV',do_init=True)
                obj.data.uv_layers.active=uv;uv.active_render=True
                for loop in uv.data:
                    for center in ((.291,.291),(.709,.703)):
                        dx=loop.uv.x-center[0];dy=loop.uv.y-center[1]
                        r=(dx*dx+dy*dy)**.5
                        if r<.23:
                            weight=1 if r<.13 else (.23-r)/.10
                            scale=1+(config['irisScale']-1)*weight
                            loop.uv=(center[0]+dx/scale,center[1]+dy/scale)
                            break
            if role.startswith('clothes.') and config.get('clothTextureSimplification'):
                import numpy as np
                color=bsdf.inputs['Base Color']
                if color.is_linked and color.links[0].from_node.type=='TEX_IMAGE':
                    node=color.links[0].from_node;image=node.image.copy();image.name+=' Stylized'
                    w,h=image.size;pixels=np.empty(w*h*4,dtype=np.float32);image.pixels.foreach_get(pixels)
                    image.scale(256,256);image.scale(w,h)
                    smooth=np.empty(w*h*4,dtype=np.float32);image.pixels.foreach_get(smooth)
                    mix=config['clothTextureSimplification'];smooth[:]=pixels*(1-mix)+smooth*mix
                    image.pixels.foreach_set(smooth);image.update();image.pack();node.image=image
            if role=='eyelashes':
                # Keep attachment geometry; soften visible coverage instead of moving lash roots.
                alpha=bsdf.inputs['Alpha']
                if alpha.is_linked:
                    previous=alpha.links[0].from_socket
                    multiply=mat.node_tree.nodes.new('ShaderNodeMath');multiply.operation='MULTIPLY'
                    multiply.inputs[1].default_value=config['lashOpacity']
                    mat.node_tree.links.new(previous,multiply.inputs[0]);mat.node_tree.links.new(multiply.outputs[0],alpha)


def create_hair(recipe):
    import bpy
    from mathutils import Vector
    cfg=recipe['style']['hair'];center=Vector(cfg['center']);radius=Vector(cfg['radius'])
    vertices=[];faces=[];uvs=[];face_mats=[];groups={}
    scalp=None
    if cfg.get('fitScalp'):
        from mathutils.bvhtree import BVHTree
        body=next(o for o in bpy.context.scene.objects if o.get('workflow_role')=='body')
        ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());m=ev.to_mesh()
        scalp=BVHTree.FromPolygons([body.matrix_world@v.co for v in m.vertices],[tuple(p.vertices) for p in m.polygons])
        ev.to_mesh_clear()
    def fit(p,clearance):
        if scalp is None:return p
        direction=(p-center).normalized();hit,normal,_,distance=scalp.ray_cast(center,direction,.4)
        if hit is None:raise RuntimeError('Hair scalp projection missed body')
        return hit+direction*clearance
    mats=[make_material('Hair_Opaque_'+str(i),color,cfg['roughness']) for i,color in enumerate(cfg['colors'])]
    def surface(theta,phi,offset=0):
        n=Vector((math.sin(theta)*math.sin(phi),-math.sin(theta)*math.cos(phi),math.cos(theta)))
        p=center+Vector((radius.x*n.x,radius.y*n.y,radius.z*n.z))
        normal=Vector((n.x/radius.x,n.y/radius.y,n.z/radius.z)).normalized()
        return p+offset*normal,normal
    def append_grid(rows,columns,coords,name,material=0):
        start=len(vertices)
        for r in range(rows):
            for c in range(columns):
                vertices.append(tuple(coords(r,c)));uvs.append((c/(columns-1),r/(rows-1)))
        for r in range(rows-1):
            for c in range(columns-1):
                a=start+r*columns+c;faces.append((a,a+1,a+columns+1,a+columns));face_mats.append(material)
        groups[name]=list(range(start,len(vertices)))
    def cap(r,c):
        phi=-math.pi+2*math.pi*c/72
        # Front hairline high enough to preserve forehead, sides clear the ears.
        limit=1.18+cfg.get('backCoverage',.60)*(1-math.cos(phi))*.5+.18*abs(math.sin(phi))
        if cfg.get('earClearance'):
            limit=.98+1.27*((1-math.cos(phi))*.5)**2+.12*abs(math.sin(phi))
            limit+=.028*math.sin(phi*11)
        theta=.015+(limit-.015)*r/24
        return fit(surface(theta,phi)[0],.0025)
    append_grid(25,73,cap,'Scalp',0)
    faces[:]=[tuple(reversed(face)) for face in faces]
    for index,lock in enumerate(cfg['locks']):
        points=[Vector(p) for p in lock['points']];width=lock['width'];thickness=lock['thickness']
        def curve(t):
            return (1-t)**3*points[0]+3*(1-t)**2*t*points[1]+3*(1-t)*t*t*points[2]+t**3*points[3]
        def strand(r,c):
            t=r/24;u=2*c/10-1;p=curve(t)
            tangent=(curve(min(1,t+.003))-curve(max(0,t-.003))).normalized()
            n=Vector(((p.x-center.x)/radius.x**2,(p.y-center.y)/radius.y**2,(p.z-center.z)/radius.z**2)).normalized()
            side=tangent.cross(n).normalized()
            taper=max(.015,(1-t)**.55)*(.55+.45*math.sin(math.pi*min(1,t*1.8)))
            arch=max(0,1-u*u)**.7
            # Flattened convex ribbons, tapered to points, never tubes.
            ridge=arch*(1+.05*math.cos(u*math.pi*5))
            result=p+side*u*width*taper+n*thickness*ridge*math.sin(math.pi*(.12+.88*t))
            return fit(result,.003+thickness*ridge+.004*math.sin(math.pi*t)) if scalp else result
        append_grid(25,11,strand,'Lock_%02d'%index,1+index%(len(mats)-1))
    mesh=bpy.data.meshes.new('SidePart_Original');mesh.from_pydata(vertices,[],faces);mesh.update()
    uv=mesh.uv_layers.new(name='UVMap')
    for loop in mesh.loops:uv.data[loop.index].uv=uvs[loop.vertex_index]
    hair=bpy.data.objects.new('Original_SidePart_Hair',mesh);bpy.context.collection.objects.link(hair)
    hair['workflow_role']='hair';hair['hair_geometry']='opaque';hair['source']='Project original editable swept locks, no external model'
    for mat in mats:mesh.materials.append(mat)
    for polygon,material in zip(mesh.polygons,face_mats):polygon.material_index=material;polygon.use_smooth=True
    for name,indices in groups.items():hair.vertex_groups.new(name=name).add(indices,1,'REPLACE')
    solid=hair.modifiers.new('Thin solid locks','SOLIDIFY');solid.thickness=.0007;solid.offset=-1
    # Preserve control paths as editable curves excluded from renders/exports.
    guides=bpy.data.collections.new('Hair_Edit_Guides');bpy.context.scene.collection.children.link(guides)
    guides.hide_render=True;guides.hide_viewport=True
    for index,lock in enumerate(cfg['locks']):
        data=bpy.data.curves.new('LockPath_%02d'%index,'CURVE');data.dimensions='3D'
        spline=data.splines.new('BEZIER');spline.bezier_points.add(1)
        a,b=spline.bezier_points
        a.co=lock['points'][0];a.handle_right_type='FREE';a.handle_right=lock['points'][1];a.handle_left_type='VECTOR'
        b.co=lock['points'][3];b.handle_left_type='FREE';b.handle_left=lock['points'][2];b.handle_right_type='VECTOR'
        obj=bpy.data.objects.new(data.name,data);guides.objects.link(obj)
    return hair
