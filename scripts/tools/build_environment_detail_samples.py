"""Build non-runtime Blender detail samples for classroom v3.1 and manor v2.

This script deliberately writes only to ``work/environment-detail-samples`` and
``outputs/environment-detail-samples``.  The authoritative Blend/GLB assets are
opened read-only and are never overwritten.  Run with Blender 5.2:

    blender --background --factory-startup --python-exit-code 1 \
      --python scripts/tools/build_environment_detail_samples.py -- --root .

The samples are a visual approval gate.  Stable gameplay nodes, collision and
runtime scene references remain untouched until the samples are approved.
"""

from __future__ import annotations

import argparse
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector


CLASSROOM_SOURCE = Path("art/blender/dream_classroom/classroom-art-pass-v3.blend")
MANOR_SOURCE = Path("art/blender/dream_manor/dream-manor-v1.blend")
WORK_RELATIVE = Path("work/environment-detail-samples")
OUTPUT_RELATIVE = Path("outputs/environment-detail-samples")
MATERIAL_TEXTURE_RELATIVE = WORK_RELATIVE / "textures-v2"
SAMPLE_COLLECTION = "EnvironmentDetailSample"


def parse_args() -> argparse.Namespace:
    raw = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", required=True)
    return parser.parse_args(raw)


def require_object(name: str) -> bpy.types.Object:
    result = bpy.data.objects.get(name)
    if result is None:
        raise RuntimeError(f"Required sample anchor is missing: {name}")
    return result


def get_material(name: str, color: tuple[float, float, float, float], roughness: float,
                 metallic: float = 0.0) -> bpy.types.Material:
    existing = bpy.data.materials.get(name)
    if existing is not None:
        return existing
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = color
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    return material


def load_image(path: Path, non_color: bool) -> bpy.types.Image:
    image = bpy.data.images.load(str(path), check_existing=True)
    image.colorspace_settings.name = "Non-Color" if non_color else "sRGB"
    return image


def pbr_material(name: str, texture_dir: Path, family: str,
                 roughness: float, metallic: float = 0.0) -> bpy.types.Material:
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    albedo = nodes.new("ShaderNodeTexImage")
    albedo.image = load_image(texture_dir / f"{family}-albedo.png", False)
    normal = nodes.new("ShaderNodeTexImage")
    normal.image = load_image(texture_dir / f"{family}-normal.png", True)
    packed = nodes.new("ShaderNodeTexImage")
    packed.image = load_image(texture_dir / f"{family}-orm.png", True)
    normal_map = nodes.new("ShaderNodeNormalMap")
    normal_map.inputs["Strength"].default_value = 0.62
    separate = nodes.new("ShaderNodeSeparateColor")
    links.new(albedo.outputs["Color"], shader.inputs["Base Color"])
    links.new(normal.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], shader.inputs["Normal"])
    links.new(packed.outputs["Color"], separate.inputs["Color"])
    links.new(separate.outputs["Green"], shader.inputs["Roughness"])
    links.new(separate.outputs["Blue"], shader.inputs["Metallic"])
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return material


def image_material(name: str, image_path: Path) -> bpy.types.Material:
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Roughness"].default_value = 0.78
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = load_image(image_path, False)
    links.new(texture.outputs["Color"], shader.inputs["Base Color"])
    links.new(texture.outputs["Color"], shader.inputs["Emission Color"])
    shader.inputs["Emission Strength"].default_value = 0.035
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return material


def ensure_sample_collection() -> bpy.types.Collection:
    old = bpy.data.collections.get(SAMPLE_COLLECTION)
    if old is not None:
        for obj in list(old.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
        bpy.data.collections.remove(old)
    result = bpy.data.collections.new(SAMPLE_COLLECTION)
    bpy.context.scene.collection.children.link(result)
    return result


def link_only(obj: bpy.types.Object, collection: bpy.types.Collection) -> None:
    for owner in list(obj.users_collection):
        owner.objects.unlink(obj)
    collection.objects.link(obj)


def full_face_uvs(obj: bpy.types.Object) -> None:
    if obj.type != "MESH":
        return
    if not obj.data.uv_layers:
        obj.data.uv_layers.new(name="UVMap")
    layer = obj.data.uv_layers.active.data
    corners = ((0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0))
    for polygon in obj.data.polygons:
        if len(polygon.loop_indices) == 4:
            for loop_index, uv in zip(polygon.loop_indices, corners):
                layer[loop_index].uv = uv


def replace_material(name: str, material: bpy.types.Material, normalize_uvs: bool = True) -> None:
    obj = require_object(name)
    if obj.type != "MESH":
        raise RuntimeError(f"Material target is not a mesh: {name}")
    obj.data.materials.clear()
    obj.data.materials.append(material)
    if normalize_uvs:
        full_face_uvs(obj)


def add_box(name: str, location: tuple[float, float, float], dimensions: tuple[float, float, float],
            material: bpy.types.Material, collection: bpy.types.Collection, bevel: float = 0.006,
            rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
            parent: bpy.types.Object | None = None) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=rotation)
    result = bpy.context.object
    result.name = name
    result.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    link_only(result, collection)
    result.data.materials.append(material)
    full_face_uvs(result)
    if bevel > 0.0:
        modifier = result.modifiers.new("DetailEdge", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        bpy.context.view_layer.objects.active = result
        result.select_set(True)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        result.select_set(False)
    if parent is not None:
        world = result.matrix_world.copy()
        result.parent = parent
        result.matrix_world = world
    return result


def add_cylinder(name: str, location: tuple[float, float, float], radius: float, depth: float,
                 material: bpy.types.Material, collection: bpy.types.Collection, vertices: int = 20,
                 rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
                 parent: bpy.types.Object | None = None) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth,
                                       location=location, rotation=rotation)
    result = bpy.context.object
    result.name = name
    link_only(result, collection)
    result.data.materials.append(material)
    if parent is not None:
        world = result.matrix_world.copy()
        result.parent = parent
        result.matrix_world = world
    return result


def add_curve(name: str, points: list[tuple[float, float, float]], material: bpy.types.Material,
              collection: bpy.types.Collection, thickness: float = 0.006) -> bpy.types.Object:
    curve = bpy.data.curves.new(name + "Data", "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = thickness
    curve.bevel_resolution = 1
    spline = curve.splines.new("POLY")
    spline.points.add(len(points) - 1)
    for index, point in enumerate(points):
        spline.points[index].co = (*point, 1.0)
    obj = bpy.data.objects.new(name, curve)
    collection.objects.link(obj)
    curve.materials.append(material)
    return obj


def add_area_light(name: str, location: tuple[float, float, float], target: tuple[float, float, float],
                   energy: float, color: tuple[float, float, float], size: float) -> bpy.types.Object:
    data = bpy.data.lights.new(name + "Data", "AREA")
    data.energy = energy
    data.color = color
    data.shape = "DISK"
    data.size = size
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = location
    point_at(obj, target)
    return obj


def add_point_light(name: str, location: tuple[float, float, float], energy: float,
                    color: tuple[float, float, float], radius: float) -> bpy.types.Object:
    data = bpy.data.lights.new(name + "Data", "POINT")
    data.energy = energy
    data.color = color
    data.shadow_soft_size = radius
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = location
    return obj


def point_at(obj: bpy.types.Object, target: tuple[float, float, float]) -> None:
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def configure_render(output: Path) -> None:
    scene = bpy.context.scene
    for obj in bpy.data.objects:
        if obj.type in {"LIGHT", "CAMERA"}:
            obj.hide_render = True
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1920
    scene.render.resolution_y = 1080
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.render.filepath = str(output)
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = -0.55
    world = scene.world or bpy.data.worlds.new("EnvironmentSampleWorld")
    scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.010, 0.022, 0.031, 1.0)
    background.inputs["Strength"].default_value = 0.20


def set_render_tree(name: str, visible: bool) -> None:
    root = bpy.data.objects.get(name)
    if root is None:
        return
    stack = [root]
    while stack:
        current = stack.pop()
        current.hide_render = not visible
        stack.extend(current.children)


def add_camera(name: str, location: tuple[float, float, float], target: tuple[float, float, float],
               lens: float = 47.0) -> bpy.types.Object:
    data = bpy.data.cameras.new(name + "Data")
    data.lens = lens
    data.sensor_width = 36.0
    data.clip_start = 0.05
    data.clip_end = 100.0
    camera = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(camera)
    camera.location = location
    point_at(camera, target)
    bpy.context.scene.camera = camera
    return camera


def classroom_sample(root: Path, work: Path, output: Path) -> dict:
    bpy.ops.wm.open_mainfile(filepath=str(root / CLASSROOM_SOURCE), load_ui=False)
    collection = ensure_sample_collection()
    wood = get_material("M_DarkWood", (0.07, 0.035, 0.020, 1.0), 0.58)
    metal = get_material("M_DarkMetal", (0.025, 0.032, 0.033, 1.0), 0.48, 0.48)
    brass = get_material("M_SampleAgedBrass", (0.24, 0.12, 0.035, 1.0), 0.38, 0.72)
    chalk = get_material("M_SampleChalk", (0.75, 0.73, 0.62, 1.0), 0.98)
    paper = get_material("M_SampleAgedPaper", (0.48, 0.38, 0.23, 1.0), 0.93)
    graphite = get_material("M_SampleGraphite", (0.025, 0.030, 0.032, 1.0), 0.97)

    # Blackboard frame: stepped moulding, chalk rail, brackets, chalk and eraser.
    for x in (-2.42, 2.42):
        add_box(f"Sample_Blackboard_Pilaster_{x:+.2f}", (x, 3.236, 2.56),
                (0.075, 0.052, 1.60), wood, collection, 0.012)
        for z in (1.80, 3.32):
            add_cylinder(f"Sample_Blackboard_Screw_{x:+.2f}_{z:.2f}", (x, 3.202, z), 0.018, 0.018,
                         brass, collection, 16, (math.radians(90), 0.0, 0.0))
    add_box("Sample_Blackboard_Crown", (0.0, 3.225, 3.36), (4.98, 0.075, 0.105), wood, collection, 0.016)
    add_box("Sample_Blackboard_ChalkRail", (0.0, 3.16, 1.74), (4.90, 0.23, 0.075), wood, collection, 0.012)
    add_box("Sample_Blackboard_RailLip", (0.0, 3.07, 1.79), (4.90, 0.035, 0.13), metal, collection, 0.005)
    for index, x in enumerate((-1.75, -1.58, 1.36, 1.54, 1.72), 1):
        color = chalk if index != 4 else get_material("M_SampleBlueChalk", (0.22, 0.43, 0.50, 1.0), 0.96)
        add_cylinder(f"Sample_Chalk_{index:02d}", (x, 3.055, 1.82), 0.018, 0.10,
                     color, collection, 12, (0.0, math.radians(90), 0.0))
    add_box("Sample_Blackboard_Eraser", (-0.28, 3.045, 1.84), (0.32, 0.09, 0.09), wood, collection, 0.018)

    # Teacher desk: drawer reveals, brass handles and a tactile three-wheel lock.
    for row, z in enumerate((0.43, 0.67), 1):
        for col, x in enumerate((-0.17, 0.40, 0.97), 1):
            add_box(f"Sample_TeacherDrawer_{row}_{col}", (x, 1.634, z),
                    (0.48, 0.026, 0.18), wood, collection, 0.014)
            add_box(f"Sample_TeacherHandle_{row}_{col}", (x, 1.607, z),
                    (0.16, 0.035, 0.032), brass, collection, 0.012)
    add_box("Sample_TeacherLockCase", (0.40, 1.59, 0.92), (0.44, 0.08, 0.16), metal, collection, 0.018)
    for index, x in enumerate((0.29, 0.40, 0.51), 1):
        add_cylinder(f"Sample_TeacherLockWheel_{index}", (x, 1.54, 0.92), 0.055, 0.035,
                     brass, collection, 20, (math.radians(90), 0.0, 0.0))
    add_box("Sample_TeacherLedger", (-0.04, 1.86, 1.27), (0.42, 0.28, 0.045), paper, collection, 0.009,
            rotation=(0.0, 0.0, math.radians(-7)))
    for index in range(3):
        add_cylinder(f"Sample_TeacherPencil_{index}", (0.77 + index * 0.035, 1.84, 1.29), 0.009, 0.26,
                     graphite, collection, 10, (0.0, math.radians(90), math.radians(8 + index * 3)))

    # One hero desk receives edge brackets, screws, a notebook and under-desk clutter.
    desk = require_object("StudentDeskArt_01")
    center = desk.matrix_world.translation
    for sx in (-0.44, 0.44):
        for sy in (-0.27, 0.27):
            add_cylinder(f"Sample_DeskScrew_{sx:+.2f}_{sy:+.2f}",
                         (center.x + sx, center.y + sy, center.z + 0.50), 0.014, 0.012,
                         metal, collection, 12)
    add_box("Sample_DeskNotebook", (center.x - 0.10, center.y, center.z + 0.53),
            (0.32, 0.23, 0.035), paper, collection, 0.008, rotation=(0.0, 0.0, math.radians(8)))
    add_box("Sample_DeskBookInTray", (center.x + 0.03, center.y, center.z + 0.13),
            (0.46, 0.27, 0.07), paper, collection, 0.006)

    # Sparse, authored wall damage instead of uniform procedural waviness.
    for index, points in enumerate((
        [(-2.7, 3.305, 3.55), (-2.55, 3.304, 3.40), (-2.62, 3.303, 3.25), (-2.48, 3.302, 3.05)],
        [(2.85, 3.305, 2.95), (2.72, 3.304, 2.78), (2.79, 3.303, 2.59)],
    ), 1):
        add_curve(f"Sample_PlasterCrack_{index}", points, graphite, collection, 0.007)

    for name, visible in (("Blackboard_Blank", True), ("Blackboard_Revealed", False),
                          ("TeacherDesk_Closed", True), ("TeacherDesk_Open", False)):
        set_render_tree(name, visible)

    # Match the adopted dollhouse lookdev composition while reviewing the
    # environment sample; runtime third-person walls are not changed.
    set_render_tree("Wall_South", False)
    set_render_tree("Wall_East", False)

    configure_render(output / "01-classroom-blackboard-wall.png")
    camera = add_camera("Sample_ClassroomCamera", (7.20, -7.20, 5.80), (0.05, 2.30, 1.35), 50.0)
    add_area_light("Sample_ClassroomWindowKey", (-4.1, 0.4, 2.5), (0.4, 2.0, 1.0),
                   470.0, (1.0, 0.50, 0.23), 3.5)
    add_area_light("Sample_ClassroomCoolFill", (2.3, -1.5, 3.7), (0.0, 2.5, 1.2),
                   180.0, (0.20, 0.38, 0.52), 4.5)
    bpy.ops.render.render(write_still=True)
    camera.location = (2.55, -1.10, 2.15)
    point_at(camera, (0.30, 2.05, 0.82))
    camera.data.lens = 58.0
    bpy.context.scene.render.filepath = str(output / "02-classroom-teacher-desk-detail.png")
    bpy.ops.render.render(write_still=True)

    sample_source = work / "classroom-v3-1-detail-sample.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(sample_source), check_existing=False)
    mesh_count = len([obj for obj in collection.objects if obj.type == "MESH"])
    return {"source": str(sample_source), "addedObjects": len(collection.objects), "addedMeshes": mesh_count}


def manor_sample(root: Path, work: Path, output: Path) -> dict:
    bpy.ops.wm.open_mainfile(filepath=str(root / MANOR_SOURCE), load_ui=False)
    collection = ensure_sample_collection()
    texture_dir = root / MATERIAL_TEXTURE_RELATIVE
    required_textures = [texture_dir / f"{family}-{suffix}.png"
                         for family in ("plaster", "dark-oak", "terrazzo", "aged-brass", "textile")
                         for suffix in ("albedo", "normal", "orm")]
    required_textures.append(texture_dir / "landscape-painting.png")
    missing = [str(path) for path in required_textures if not path.is_file()]
    if missing:
        raise RuntimeError(f"Build environment material sample textures first: {missing}")
    plaster_material = pbr_material("M_SampleManor_PlasterV2", texture_dir, "plaster", 0.86)
    wood = pbr_material("M_SampleManor_DarkOakV2", texture_dir, "dark-oak", 0.68)
    terrazzo_material = pbr_material("M_SampleManor_TerrazzoV2", texture_dir, "terrazzo", 0.73)
    brass = pbr_material("M_SampleManor_AgedBrassV2", texture_dir, "aged-brass", 0.44, 0.72)
    upholstery = pbr_material("M_SampleManor_TextileV2", texture_dir, "textile", 0.88)
    painting_material = image_material("M_SampleManor_LandscapeV2", texture_dir / "landscape-painting.png")
    iron = get_material("M_Manor_DarkIron", (0.020, 0.024, 0.025, 1.0), 0.56, 0.58)
    stone = get_material("M_Manor_Stone", (0.18, 0.16, 0.13, 1.0), 0.80)
    paper = get_material("M_Manor_AgedPaper", (0.44, 0.34, 0.20, 1.0), 0.92)

    for name in ("Door_Room1_Frame_N", "Door_Room1_Frame_S", "Door_Room1_Frame_Top",
                 "Door_Room1_Leaf", "Door_Room1_Inset", "Door_Room1_Overdoor",
                 "Wall_Foyer_West_Baseboard", "Wall_Foyer_West_ChairRail_01",
                 "Wall_Foyer_West_Wainscot_01", "Painting_01_Frame"):
        replace_material(name, wood)
    replace_material("Door_Room1_Handle", brass)
    replace_material("Wall_Foyer_West_Plaster_01", plaster_material)
    replace_material("Foyer_Floor", terrazzo_material)
    replace_material("Hall_Floor", terrazzo_material)
    replace_material("Painting_01_Canvas", painting_material)
    replace_material("WallSconce_03_Stem", brass)

    # Room 1 receives a three-step Arts-and-Crafts casing without moving the door.
    x = -3.105
    for y in (-3.92, -2.48):
        add_box(f"Sample_Room1_CasingOuter_{y:+.2f}", (x, y, 1.44), (0.075, 0.18, 2.88), wood, collection, 0.018)
        add_box(f"Sample_Room1_CasingInner_{y:+.2f}", (x + 0.042, y, 1.38), (0.038, 0.08, 2.68), brass, collection, 0.008)
        add_box(f"Sample_Room1_Plinth_{y:+.2f}", (x + 0.015, y, 0.18), (0.11, 0.28, 0.36), wood, collection, 0.025)
    add_box("Sample_Room1_Architrave", (x, -3.20, 2.84), (0.095, 1.72, 0.24), wood, collection, 0.025)
    add_box("Sample_Room1_Crown", (x + 0.015, -3.20, 3.02), (0.13, 1.96, 0.16), wood, collection, 0.032)
    add_box("Sample_Room1_OverdoorPanel", (x + 0.025, -3.20, 3.33), (0.055, 1.48, 0.42), wood, collection, 0.018)
    for y in (-3.73, -3.20, -2.67):
        add_cylinder(f"Sample_Room1_CrownRosette_{y:+.2f}", (x - 0.015, y, 3.33), 0.075, 0.025,
                     brass, collection, 24, (0.0, math.radians(90), 0.0))

    # Raised door panels, key escutcheon and hinge straps give the leaf depth.
    for y in (-3.47, -2.93):
        for z, height in ((0.61, 0.64), (1.47, 0.78), (2.18, 0.48)):
            add_box(f"Sample_Room1_DoorPanel_{y:+.2f}_{z:.2f}", (-3.322, y, z),
                    (0.028, 0.42, height), wood, collection, 0.025)
    add_cylinder("Sample_Room1_Escutcheon", (-3.304, -2.67, 1.10), 0.065, 0.025,
                 brass, collection, 24, (0.0, math.radians(90), 0.0))
    for z in (0.45, 1.30, 2.16):
        add_box(f"Sample_Room1_HingeStrap_{z:.2f}", (-3.300, -3.74, z),
                (0.025, 0.34, 0.06), iron, collection, 0.015)

    # Wall-panelling reveals around the first room door.
    for y0, y1 in ((-5.95, -4.25), (-2.12, -0.42)):
        center = (y0 + y1) * 0.5
        width = y1 - y0
        add_box(f"Sample_WainscotTop_{center:+.2f}", (-3.075, center, 1.27),
                (0.04, width, 0.055), wood, collection, 0.010)
        add_box(f"Sample_WainscotBottom_{center:+.2f}", (-3.075, center, 0.18),
                (0.04, width, 0.055), wood, collection, 0.010)
        for y in (y0 + 0.12, center, y1 - 0.12):
            add_box(f"Sample_WainscotStile_{y:+.2f}", (-3.075, y, 0.72),
                    (0.04, 0.055, 1.08), wood, collection, 0.010)
        for panel_index, panel_y in enumerate(((y0 + center) * 0.5, (center + y1) * 0.5), 1):
            add_box(f"Sample_WainscotInset_{center:+.2f}_{panel_index}", (-3.067, panel_y, 0.72),
                    (0.025, width * 0.39, 0.82), wood, collection, 0.022)

    # Fireplace: brick firebox, iron grate, logs, fired-tile band and mantel props.
    for row in range(5):
        z = 0.19 + row * 0.20
        count = 4 if row % 2 == 0 else 3
        offset = -0.48 if count == 4 else -0.36
        for col in range(count):
            y = 3.45 + offset + col * 0.32
            add_box(f"Sample_Firebrick_{row}_{col}", (-3.285, y, z),
                    (0.06, 0.29, 0.17), stone, collection, 0.008)
    for y in (3.18, 3.45, 3.72):
        add_cylinder(f"Sample_FireplaceGrate_{y:.2f}", (-3.02, y, 0.34), 0.025, 0.72,
                     iron, collection, 12, (0.0, math.radians(90), 0.0))
    for index, y in enumerate((3.22, 3.45, 3.67), 1):
        add_cylinder(f"Sample_FireplaceLog_{index}", (-3.00, y, 0.23 + index * 0.035), 0.07, 0.62,
                     wood, collection, 14, (0.0, math.radians(90), 0.0))
    for index, y in enumerate((2.62, 2.95, 3.28, 3.61, 3.94, 4.27), 1):
        tile = get_material(f"M_SampleFireplaceTile{index % 2}",
                            (0.08, 0.17 + (index % 2) * 0.035, 0.15, 1.0), 0.55)
        add_box(f"Sample_FireplaceTileBand_{index}", (-3.05, y, 1.50),
                (0.045, 0.27, 0.27), tile, collection, 0.010)
    add_cylinder("Sample_MantelVase", (-3.31, 3.10, 2.18), 0.11, 0.36,
                 brass, collection, 24)
    add_box("Sample_MantelBooks", (-3.32, 3.73, 2.13), (0.34, 0.42, 0.14), paper, collection, 0.012,
            rotation=(0.0, 0.0, math.radians(4)))

    # The foyer bench gains upholstered back, arms, piping and buttons.
    add_box("Sample_BenchBackCushion", (2.93, -6.80, 1.02), (0.20, 1.42, 0.58), upholstery, collection, 0.095)
    for y in (-7.47, -6.13):
        add_box(f"Sample_BenchArm_{y:+.2f}", (2.72, y, 0.80), (0.48, 0.10, 0.12), wood, collection, 0.035)
        add_box(f"Sample_BenchArmPost_{y:+.2f}", (2.93, y, 0.57), (0.10, 0.10, 0.58), wood, collection, 0.025)
    for y in (-7.22, -6.80, -6.38):
        add_cylinder(f"Sample_BenchButton_{y:+.2f}", (2.815, y, 1.03), 0.025, 0.018,
                     brass, collection, 16, (0.0, math.radians(90), 0.0))

    # A restrained local light makes the first-room threshold a navigational focus.
    configure_render(output / "03-manor-room1-foyer.png")
    camera = add_camera("Sample_ManorCamera", (2.55, -6.40, 2.45), (-2.60, -2.55, 1.35), 50.0)
    add_area_light("Sample_ManorDoorKey", (-1.7, -3.25, 2.75), (-3.15, -3.20, 1.45),
                   125.0, (1.0, 0.58, 0.32), 2.0)
    add_area_light("Sample_ManorCoolFill", (2.5, -2.0, 3.4), (-2.0, -1.0, 1.2),
                   205.0, (0.16, 0.32, 0.43), 5.0)
    add_area_light("Sample_ManorFireplaceKey", (-0.8, 3.2, 2.8), (-3.05, 3.45, 1.0),
                   105.0, (1.0, 0.46, 0.20), 1.6)
    add_point_light("Sample_ManorSconceGlow", (-2.84, -1.00, 2.42), 42.0,
                    (1.0, 0.52, 0.23), 0.22)
    bpy.ops.render.render(write_still=True)
    camera.location = (0.55, 0.60, 2.05)
    point_at(camera, (-3.05, 3.45, 1.05))
    camera.data.lens = 55.0
    bpy.context.scene.render.filepath = str(output / "04-manor-fireplace-detail.png")
    bpy.ops.render.render(write_still=True)

    sample_source = work / "dream-manor-v2-detail-sample.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(sample_source), check_existing=False)
    mesh_count = len([obj for obj in collection.objects if obj.type == "MESH"])
    return {"source": str(sample_source), "addedObjects": len(collection.objects), "addedMeshes": mesh_count}


def main() -> None:
    args = parse_args()
    root = Path(args.root).resolve()
    work = root / WORK_RELATIVE
    output = root / OUTPUT_RELATIVE
    work.mkdir(parents=True, exist_ok=True)
    output.mkdir(parents=True, exist_ok=True)
    for relative in (CLASSROOM_SOURCE, MANOR_SOURCE):
        if not (root / relative).is_file():
            raise RuntimeError(f"Missing authoritative source: {relative}")
    classroom = classroom_sample(root, work, output)
    manor = manor_sample(root, work, output)
    report = {
        "status": "visual-sample-only",
        "runtimeAssetsChanged": False,
        "resolution": [1920, 1080],
        "materialTextureManifest": str(
            (root / MATERIAL_TEXTURE_RELATIVE / "environment-material-samples-manifest.json").resolve()
        ),
        "classroom": classroom,
        "manor": manor,
        "outputs": sorted(path.name for path in output.glob("*.png")),
    }
    (output / "sample-report.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    print(
        "ENVIRONMENT_DETAIL_SAMPLE_PASS "
        f"classroomObjects={classroom['addedObjects']} manorObjects={manor['addedObjects']} "
        f"renders={len(report['outputs'])} runtimeAssetsChanged=false"
    )


if __name__ == "__main__":
    main()
