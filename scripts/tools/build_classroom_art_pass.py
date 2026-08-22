"""Bootstrap, export, and validate the Blender-authored classroom art pass v2.

`art-bootstrap` is a one-time helper that creates the first editable v2 Blend
from the deterministic MVP. After that, the Blend is the source of truth and
`art-export` / `art-check` never reconstruct it.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import sys
import tempfile

import bpy
from mathutils import Vector


BLENDER_VERSION = (5, 2, 0)
ROOM_SIZE = (9.6, 7.2, 3.2)
SOURCE_RELATIVE = Path("art/blender/dream_classroom/classroom-art-pass-v2.blend")
GLB_RELATIVE = Path("assets/generated/dream_escape/3d/classroom-art-pass-v2.glb")
MANIFEST_RELATIVE = Path("assets/generated/dream_escape/3d/classroom-art-pass-v2-manifest.json")
TEXTURE_RELATIVE = Path("art/blender/dream_classroom/textures-v2")
TEXTURE_MANIFEST_RELATIVE = TEXTURE_RELATIVE / "classroom-art-textures-v2-manifest.json"
TEXTURE_FILES = (
    "classroom-shell-albedo.png",
    "classroom-shell-normal.png",
    "classroom-shell-orm.png",
    "classroom-furniture-albedo.png",
    "classroom-furniture-normal.png",
    "classroom-furniture-orm.png",
    "classroom-details.png",
    "window-exterior.png",
)

REQUIRED_OBJECTS = {
    "Room_Floor",
    "Wall_North",
    "Wall_South",
    "Wall_East",
    "Wall_West",
    "Door_East",
    "Door_East_Hinge",
    "DoorV3_Leaf",
    "ThirdPerson_WallCap_North",
    "Window_West",
    "Blackboard_Blank",
    "Blackboard_Revealed",
    "Clock_Seven",
    "DutyRoster_Second",
    "TeacherDesk_Closed",
    "TeacherDesk_Open",
    "MapCabinet_Closed",
    "MapCabinet_Open",
    "Chalk",
    "Window_Exterior",
    "StudentDeskArt_01",
}

OLD_ATLAS_REGIONS = {
    "M_OldPlaster": (0, 0),
    "M_FadedTeal": (1, 0),
    "M_WornFloor": (2, 0),
    "M_WornWood": (3, 0),
    "M_DarkWood": (0, 1),
    "M_DarkMetal": (1, 1),
    "M_Blackboard": (2, 1),
    "M_AgedPaper": (3, 1),
    "M_DirtyGlass": (0, 2),
    "M_Amber": (1, 2),
    "M_Chalk": (2, 2),
    "M_FadedRed": (3, 2),
    "M_Baseboard": (0, 3),
    "M_Curtain": (1, 3),
    "M_Rubber": (2, 3),
    "M_Dust": (3, 3),
}

SHELL_ATLAS_REGIONS = {
    "M_OldPlaster": (0, 0),
    "M_FadedTeal": (1, 0),
    "M_WornFloor": (0, 1),
    "M_Blackboard": (1, 1),
    "M_DirtyGlass": (0, 2),
    "M_Baseboard": (1, 2),
    "M_Curtain": (0, 3),
    "M_Dust": (1, 3),
}

FURNITURE_ATLAS_REGIONS = {
    "M_WornWood": (0, 0),
    "M_DarkWood": (1, 0),
    "M_DarkMetal": (0, 1),
    "M_AgedPaper": (1, 1),
    "M_Amber": (0, 2),
    "M_Chalk": (1, 2),
    "M_FadedRed": (0, 3),
    "M_Rubber": (1, 3),
}

ATLAS_REGIONS = {
    **{name: ("shell", *cell) for name, cell in SHELL_ATLAS_REGIONS.items()},
    **{name: ("furniture", *cell) for name, cell in FURNITURE_ATLAS_REGIONS.items()},
}


def parse_args() -> argparse.Namespace:
    raw = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=("art-bootstrap", "art-upgrade-third-person", "art-export", "art-check"))
    parser.add_argument("--root", required=True)
    return parser.parse_args(raw)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def load_mvp_builder(root: Path):
    script = root / "scripts/tools/build_classroom_3d.py"
    spec = importlib.util.spec_from_file_location("town_rpg_classroom_mvp", script)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load MVP builder: {script}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def ensure_collection(name: str) -> bpy.types.Collection:
    existing = bpy.data.collections.get(name)
    if existing is not None:
        return existing
    result = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(result)
    return result


def move_collection(old_name: str, new_name: str) -> bpy.types.Collection:
    current = bpy.data.collections.get(old_name) or bpy.data.collections.get(new_name)
    if current is None:
        raise RuntimeError(f"Missing collection: {old_name}")
    current.name = new_name
    return current


def load_image(root: Path, filename: str, colorspace: str) -> bpy.types.Image:
    absolute = root / TEXTURE_RELATIVE / filename
    if not absolute.is_file():
        raise RuntimeError(f"Missing art-pass texture: {absolute}")
    image = bpy.data.images.load(str(absolute), check_existing=True)
    image.name = f"T_{Path(filename).stem}"
    image.filepath = f"//textures-v2/{filename}"
    image.colorspace_settings.name = colorspace
    return image


def rebuild_atlas_material(material: bpy.types.Material, albedo: bpy.types.Image, normal: bpy.types.Image, orm: bpy.types.Image) -> None:
    material.use_nodes = True
    material.diffuse_color = (0.55, 0.55, 0.55, 1.0)
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    output.location = (620, 0)
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.location = (340, 0)
    base = nodes.new("ShaderNodeTexImage")
    base.name = "AtlasAlbedo"
    base.image = albedo
    base.interpolation = "Linear"
    base.location = (-420, 160)
    normal_tex = nodes.new("ShaderNodeTexImage")
    normal_tex.name = "AtlasNormal"
    normal_tex.image = normal
    normal_tex.interpolation = "Linear"
    normal_tex.location = (-420, -90)
    normal_map = nodes.new("ShaderNodeNormalMap")
    normal_map.inputs["Strength"].default_value = 0.38
    normal_map.location = (80, -120)
    orm_tex = nodes.new("ShaderNodeTexImage")
    orm_tex.name = "AtlasORM"
    orm_tex.image = orm
    orm_tex.interpolation = "Linear"
    orm_tex.location = (-420, -350)
    separate = nodes.new("ShaderNodeSeparateColor")
    separate.location = (-80, -330)
    links.new(base.outputs["Color"], bsdf.inputs["Base Color"])
    links.new(normal_tex.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], bsdf.inputs["Normal"])
    links.new(orm_tex.outputs["Color"], separate.inputs["Color"])
    links.new(separate.outputs["Green"], bsdf.inputs["Roughness"])
    links.new(separate.outputs["Blue"], bsdf.inputs["Metallic"])
    links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    if material.name == "M_DirtyGlass":
        bsdf.inputs["Alpha"].default_value = 0.22
        if hasattr(material, "surface_render_method"):
            material.surface_render_method = "DITHERED"
        elif hasattr(material, "blend_method"):
            material.blend_method = "BLEND"


def make_image_material(name: str, image: bpy.types.Image, alpha: bool, emission: float = 0.0) -> bpy.types.Material:
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = image
    texture.interpolation = "Linear"
    texture.location = (-320, 40)
    bsdf.location = (20, 40)
    output.location = (320, 40)
    links.new(texture.outputs["Color"], bsdf.inputs["Base Color"])
    if alpha:
        links.new(texture.outputs["Alpha"], bsdf.inputs["Alpha"])
        if hasattr(material, "surface_render_method"):
            material.surface_render_method = "DITHERED"
        elif hasattr(material, "blend_method"):
            material.blend_method = "BLEND"
    if emission > 0:
        emission_input = bsdf.inputs.get("Emission Color") or bsdf.inputs.get("Emission")
        strength_input = bsdf.inputs.get("Emission Strength")
        if emission_input is not None:
            links.new(texture.outputs["Color"], emission_input)
        if strength_input is not None:
            strength_input.default_value = emission
    bsdf.inputs["Roughness"].default_value = 0.88
    links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    return material


def assign_atlas_materials(root: Path) -> tuple[bpy.types.Material, bpy.types.Material]:
    shell = (
        load_image(root, "classroom-shell-albedo.png", "sRGB"),
        load_image(root, "classroom-shell-normal.png", "Non-Color"),
        load_image(root, "classroom-shell-orm.png", "Non-Color"),
    )
    furniture = (
        load_image(root, "classroom-furniture-albedo.png", "sRGB"),
        load_image(root, "classroom-furniture-normal.png", "Non-Color"),
        load_image(root, "classroom-furniture-orm.png", "Non-Color"),
    )
    details = load_image(root, "classroom-details.png", "sRGB")
    exterior = load_image(root, "window-exterior.png", "sRGB")
    for name, (atlas, _, _) in ATLAS_REGIONS.items():
        material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        rebuild_atlas_material(material, *(shell if atlas == "shell" else furniture))
    return make_image_material("M_DetailDecal", details, True), make_image_material("M_WindowExterior", exterior, False, 0.22)


def new_box(name: str, logical_position: tuple[float, float, float], logical_size: tuple[float, float, float], target: bpy.types.Collection, material: bpy.types.Material, bevel: float = 0.0, parent: bpy.types.Object | None = None) -> bpy.types.Object:
    x, height, depth = logical_position
    width, size_height, size_depth = logical_size
    bpy.ops.mesh.primitive_cube_add(location=(x, -depth, height))
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = (width, size_depth, size_height)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    for owner in list(obj.users_collection):
        owner.objects.unlink(obj)
    target.objects.link(obj)
    obj.data.materials.append(material)
    obj.parent = parent
    if bevel > 0:
        modifier = obj.modifiers.new("SoftEdges", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        obj.select_set(False)
    return obj


def new_cylinder(name: str, logical_position: tuple[float, float, float], radius: float, height: float, target: bpy.types.Collection, material: bpy.types.Material, vertices: int = 12, parent: bpy.types.Object | None = None) -> bpy.types.Object:
    x, logical_height, depth = logical_position
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=height, location=(x, -depth, logical_height))
    obj = bpy.context.object
    obj.name = name
    for owner in list(obj.users_collection):
        owner.objects.unlink(obj)
    target.objects.link(obj)
    obj.data.materials.append(material)
    obj.parent = parent
    return obj


def remove_desk_children(root: bpy.types.Object) -> None:
    for child in list(root.children):
        bpy.data.objects.remove(child, do_unlink=True)


def make_desk_prototype(target: bpy.types.Collection, materials: dict[str, bpy.types.Material]) -> bpy.types.Object:
    parts: list[bpy.types.Object] = []
    parts.append(new_box("DeskArt_Desktop", (0, 0.79, 0), (1.05, 0.09, 0.58), target, materials["M_WornWood"], 0.035))
    parts.append(new_box("DeskArt_Apron", (0, 0.64, -0.245), (0.92, 0.20, 0.045), target, materials["M_DarkWood"], 0.012))
    parts.append(new_box("DeskArt_Shelf", (0, 0.48, 0.01), (0.88, 0.045, 0.44), target, materials["M_DarkWood"], 0.012))
    for x in (-0.42, 0.42):
        for z in (-0.22, 0.22):
            parts.append(new_cylinder(f"DeskArt_Leg_{x}_{z}", (x, 0.38, z), 0.025, 0.72, target, materials["M_DarkMetal"]))
    parts.append(new_box("DeskArt_Crossbar", (0, 0.28, 0.22), (0.82, 0.035, 0.035), target, materials["M_DarkMetal"], 0.008))
    parts.append(new_box("DeskArt_ChairSeat", (0, 0.48, 0.61), (0.50, 0.065, 0.43), target, materials["M_WornWood"], 0.028))
    parts.append(new_box("DeskArt_ChairBack", (0, 0.79, 0.82), (0.50, 0.44, 0.055), target, materials["M_WornWood"], 0.026))
    for x in (-0.19, 0.19):
        for z in (0.48, 0.73):
            parts.append(new_cylinder(f"ChairArt_Leg_{x}_{z}", (x, 0.24, z), 0.021, 0.44, target, materials["M_DarkMetal"]))
    for x in (-0.40, 0.40):
        for z in (-0.20, 0.20):
            screw = new_cylinder(f"DeskArt_Screw_{x}_{z}", (x, 0.842, z), 0.014, 0.012, target, materials["M_DarkMetal"], 10)
            parts.append(screw)

    bpy.ops.object.select_all(action="DESELECT")
    for part in parts:
        part.select_set(True)
    active = parts[0]
    bpy.context.view_layer.objects.active = active
    bpy.ops.object.join()
    active.name = "StudentDeskArt_01"
    bpy.context.scene.cursor.location = (0, 0, 0)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    active["town_rpg_linked_prototype"] = True
    return active


def replace_student_desks(furniture: bpy.types.Collection, materials: dict[str, bpy.types.Material]) -> None:
    xs = (-3.35, -1.68, 0.0, 1.68, 3.35)
    zs = (-0.45, 1.28)
    roots = [bpy.data.objects.get(f"StudentDeskGroup_{index:02d}") for index in range(1, 11)]
    if any(root is None for root in roots):
        raise RuntimeError("The MVP desk roots are incomplete")
    for root in roots:
        remove_desk_children(root)
    prototype = make_desk_prototype(furniture, materials)
    prototype.parent = roots[0]
    prototype.location = (0, 0, 0)
    for index, (z, x) in enumerate([(z, x) for z in zs for x in xs], start=1):
        root = roots[index - 1]
        root.location = (x, -z, 0)
        if index == 1:
            continue
        instance = bpy.data.objects.new(f"StudentDeskArt_{index:02d}", prototype.data)
        furniture.objects.link(instance)
        instance.parent = root
        instance.location = (0, 0, 0)


def make_vertical_plane(name: str, x_center: float, y_height: float, z_depth: float, width: float, height: float, target: bpy.types.Collection, material: bpy.types.Material, uv_min: tuple[float, float] = (0, 0), uv_max: tuple[float, float] = (1, 1), parent: bpy.types.Object | None = None) -> bpy.types.Object:
    y = -z_depth
    z0 = y_height - height / 2
    z1 = y_height + height / 2
    x0 = x_center - width / 2
    x1 = x_center + width / 2
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata([(x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)], [], [(0, 1, 2, 3)])
    mesh.materials.append(material)
    uv = mesh.uv_layers.new(name="UVMap")
    u0, v0 = uv_min
    u1, v1 = uv_max
    for loop, coordinate in zip(uv.data, ((u0, v0), (u1, v0), (u1, v1), (u0, v1))):
        loop.uv = coordinate
    obj = bpy.data.objects.new(name, mesh)
    target.objects.link(obj)
    obj.parent = parent
    return obj


def enrich_architecture(collections: dict[str, bpy.types.Collection], materials: dict[str, bpy.types.Material], detail_material: bpy.types.Material, exterior_material: bpy.types.Material) -> None:
    shell = collections["Shell"]
    props = collections["Props"]
    decals = collections["Decals"]
    states = collections["StateVariants"]
    north = bpy.data.objects["Wall_North"]
    south = bpy.data.objects["Wall_South"]
    east = bpy.data.objects["Wall_East"]
    west = bpy.data.objects["Wall_West"]

    baseboard = materials["M_Baseboard"]
    new_box("Baseboard_North", (0, 0.13, -3.48), (9.58, 0.26, 0.09), shell, baseboard, 0.015, north)
    new_box("Baseboard_South", (0, 0.13, 3.48), (9.58, 0.26, 0.09), shell, baseboard, 0.015, south)
    new_box("Baseboard_West", (-4.68, 0.13, 0), (0.09, 0.26, 6.94), shell, baseboard, 0.015, west)
    new_box("Baseboard_East", (4.68, 0.13, -0.45), (0.09, 0.26, 5.92), shell, baseboard, 0.015, east)

    window = bpy.data.objects["Window_West"]
    window.parent = west
    new_box("Window_Sill", (-4.60, 0.83, -1.10), (0.28, 0.09, 1.65), props, materials["M_DarkWood"], 0.018, window)
    for offset in (-0.45, 0.45):
        new_box(f"Window_ExtraMullion_{offset}", (-4.64, 1.55, -1.10 + offset), (0.045, 1.38, 0.04), props, materials["M_DarkWood"], 0.008, window)
    new_box("Curtain_Left", (-4.52, 1.57, -1.73), (0.055, 1.42, 0.32), props, materials["M_Curtain"], 0.018, west)
    new_box("Curtain_Right", (-4.52, 1.57, -0.47), (0.055, 1.42, 0.32), props, materials["M_Curtain"], 0.018, west)
    new_box("Curtain_Rail", (-4.52, 2.32, -1.10), (0.055, 0.045, 1.78), props, materials["M_DarkMetal"], 0.01, west)
    new_box("Window_Exterior", (-4.91, 1.58, -1.10), (0.035, 1.52, 1.58), decals, exterior_material, 0, west)

    door = bpy.data.objects["Door_East"]
    door.parent = east
    new_box("Door_East_Leaf", (4.73, 1.08, 3.22), (0.10, 2.12, 0.78), props, materials["M_DarkWood"], 0.025, door)
    new_box("Door_East_Glass", (4.66, 1.62, 3.22), (0.025, 0.55, 0.40), props, materials["M_DirtyGlass"], 0.008, door)
    new_cylinder("Door_East_Handle", (4.59, 1.02, 2.98), 0.045, 0.075, props, materials["M_Amber"], 12, door)

    for name in ("Blackboard_Blank", "Blackboard_Revealed", "Clock_Seven", "DutyRoster_Second"):
        bpy.data.objects[name].parent = north
    for board_name in ("Blackboard_Blank", "Blackboard_Revealed"):
        board = bpy.data.objects[board_name]
        for suffix, position, size in (
            ("Top", (0, 2.52, -3.40), (4.10, 0.09, 0.10)),
            ("Bottom", (0, 1.12, -3.40), (4.10, 0.11, 0.14)),
            ("Left", (-2.02, 1.82, -3.40), (0.10, 1.48, 0.10)),
            ("Right", (2.02, 1.82, -3.40), (0.10, 1.48, 0.10)),
        ):
            new_box(f"{board_name}_Frame{suffix}", position, size, states, materials["M_DarkWood"], 0.018, board)
        new_box(f"{board_name}_Tray", (0, 1.08, -3.30), (3.95, 0.06, 0.24), states, materials["M_DarkWood"], 0.012, board)
        new_box(f"{board_name}_Eraser", (1.18, 1.16, -3.25), (0.28, 0.07, 0.12), states, materials["M_Rubber"], 0.015, board)
    for obj in list(bpy.data.objects):
        if obj.name.startswith("Blackboard_Chalk_"):
            bpy.data.objects.remove(obj, do_unlink=True)
    revealed = bpy.data.objects["Blackboard_Revealed"]
    make_vertical_plane("Blackboard_Revealed_Writing", 0, 1.85, -3.385, 3.42, 0.73, decals, detail_material, (0, 0.5), (1, 1), revealed)

    fixture = bpy.data.objects.new("Wall_FluorescentFixture", None)
    props.objects.link(fixture)
    fixture.parent = north
    new_box("Fluorescent_Housing", (0, 2.88, -3.38), (2.45, 0.13, 0.15), props, materials["M_DarkMetal"], 0.018, fixture)
    new_box("Fluorescent_Tube", (0, 2.84, -3.28), (2.18, 0.055, 0.09), props, materials["M_Chalk"], 0.018, fixture)
    new_box("Electrical_Conduit", (2.82, 2.82, -3.40), (1.28, 0.035, 0.04), props, materials["M_DarkMetal"], 0.007, north)
    new_box("Electrical_Switch", (3.48, 2.65, -3.37), (0.16, 0.24, 0.08), props, materials["M_AgedPaper"], 0.012, north)


def parent_keep_world(obj: bpy.types.Object, parent: bpy.types.Object) -> None:
    world = obj.matrix_world.copy()
    obj.parent = parent
    obj.matrix_parent_inverse = parent.matrix_world.inverted()
    obj.matrix_world = world


def upgrade_third_person_details(collections: dict[str, bpy.types.Collection], materials: dict[str, bpy.types.Material]) -> None:
    props = collections["Props"]
    shell = collections["Shell"]
    door_root = bpy.data.objects["Door_East"]
    for obj in list(bpy.data.objects):
        if obj.name in {"Door_East_Leaf", "Door_East_Glass", "Door_East_Handle", "Door_East_Hinge"} or \
                obj.name.startswith("DoorV3_") or obj.name.startswith("ThirdPerson_"):
            bpy.data.objects.remove(obj, do_unlink=True)

    hinge = bpy.data.objects.new("Door_East_Hinge", None)
    props.objects.link(hinge)
    hinge.location = (4.68, -2.75, 0.0)
    parent_keep_world(hinge, door_root)

    door_parts = [
        new_box("DoorV3_Leaf", (4.68, 0.61, 2.20), (0.10, 1.18, 1.08), props, materials["M_DarkWood"], 0.022),
        new_box("DoorV3_TopRail", (4.68, 2.05, 2.20), (0.10, 0.18, 1.08), props, materials["M_DarkWood"], 0.018),
        new_box("DoorV3_MiddleRail", (4.68, 1.22, 2.20), (0.10, 0.15, 1.08), props, materials["M_DarkWood"], 0.018),
        new_box("DoorV3_LeftRail", (4.68, 1.63, 1.72), (0.10, 0.72, 0.12), props, materials["M_DarkWood"], 0.018),
        new_box("DoorV3_RightRail", (4.68, 1.63, 2.68), (0.10, 0.72, 0.12), props, materials["M_DarkWood"], 0.018),
        new_box("DoorV3_Glass", (4.68, 1.63, 2.20), (0.055, 0.70, 0.82), props, materials["M_DirtyGlass"], 0.006),
        new_box("DoorV3_InsideLock", (4.57, 1.02, 1.86), (0.045, 0.18, 0.15), props, materials["M_DarkMetal"], 0.008),
        new_box("DoorV3_OutsideLock", (4.79, 1.02, 1.86), (0.045, 0.18, 0.15), props, materials["M_DarkMetal"], 0.008),
        new_box("DoorV3_InsideHandle", (4.54, 1.04, 1.75), (0.045, 0.045, 0.24), props, materials["M_Amber"], 0.012),
        new_box("DoorV3_OutsideHandle", (4.82, 1.04, 1.75), (0.045, 0.045, 0.24), props, materials["M_Amber"], 0.012),
    ]
    for part in door_parts:
        parent_keep_world(part, hinge)
    for index, height in enumerate((0.30, 1.05, 1.90), start=1):
        hardware = new_cylinder(
            f"DoorV3_HingeHardware_{index}",
            (4.57, height, 2.73),
            0.035,
            0.16,
            props,
            materials["M_DarkMetal"],
            12)
        parent_keep_world(hardware, hinge)

    north = bpy.data.objects["Wall_North"]
    south = bpy.data.objects["Wall_South"]
    east = bpy.data.objects["Wall_East"]
    west = bpy.data.objects["Wall_West"]
    wall_details = [
        new_box("ThirdPerson_SouthNoticeBoard", (-1.65, 1.75, 3.42), (1.65, 0.90, 0.055), shell, materials["M_FadedTeal"], 0.018),
        new_box("ThirdPerson_SouthPictureRail", (1.35, 2.35, 3.42), (2.15, 0.065, 0.055), shell, materials["M_DarkWood"], 0.012),
        new_box("ThirdPerson_EastSwitchPlate", (4.59, 1.28, 1.25), (0.055, 0.22, 0.16), shell, materials["M_AgedPaper"], 0.008),
        new_box("ThirdPerson_WestConduit", (-4.59, 2.58, 1.10), (0.045, 0.045, 2.30), shell, materials["M_DarkMetal"], 0.006),
        new_box("ThirdPerson_NorthRepairPatch", (-3.32, 2.52, -3.43), (0.82, 0.38, 0.025), shell, materials["M_OldPlaster"], 0.006),
    ]
    for detail, wall in zip(wall_details, (south, south, east, west, north)):
        parent_keep_world(detail, wall)

    # The room deliberately remains ceilingless, but the orbit camera is close
    # enough to the walls that the original 3.2 m shell could reveal the void
    # above it at 16:9. These upper wall courses preserve the open dollhouse
    # construction while keeping every playable camera ray inside the model.
    wall_caps = [
        (new_box("ThirdPerson_WallCap_North", (0.0, 3.70, -3.51), (9.60, 1.00, 0.18), shell, materials["M_OldPlaster"], 0.008), north),
        (new_box("ThirdPerson_WallCap_South", (0.0, 3.70, 3.51), (9.60, 1.00, 0.18), shell, materials["M_OldPlaster"], 0.008), south),
        (new_box("ThirdPerson_WallCap_East", (4.71, 3.70, 0.0), (0.18, 1.00, 7.20), shell, materials["M_OldPlaster"], 0.008), east),
        (new_box("ThirdPerson_WallCap_West", (-4.71, 3.70, 0.0), (0.18, 1.00, 7.20), shell, materials["M_OldPlaster"], 0.008), west),
    ]
    for cap, wall in wall_caps:
        parent_keep_world(cap, wall)

    for index, (x, z, rotation) in enumerate(((-2.0, -0.35, -0.12), (0.25, 1.05, 0.08), (2.95, -0.25, -0.05)), start=1):
        book = new_box(
            f"ThirdPerson_DeskBook_{index}",
            (x, 0.88, z),
            (0.34, 0.055, 0.24),
            props,
            materials["M_FadedRed" if index == 2 else "M_AgedPaper"],
            0.008)
        book.rotation_euler.z = rotation

    bpy.context.scene["town_rpg_third_person_art"] = "v4"


def enrich_teacher_desks(states: bpy.types.Collection, materials: dict[str, bpy.types.Material]) -> None:
    for root_name in ("TeacherDesk_Closed", "TeacherDesk_Open"):
        root = bpy.data.objects[root_name]
        new_box(f"{root_name}_FrontPanel", (0.40, 0.43, -1.72), (1.18, 0.56, 0.065), states, materials["M_DarkWood"], 0.018, root)
        new_box(f"{root_name}_LockPlate", (0.40, 0.60, -1.675), (0.34, 0.18, 0.045), states, materials["M_DarkMetal"], 0.008, root)
        for x in (0.30, 0.40, 0.50):
            new_cylinder(f"{root_name}_Key_{x}", (x, 0.60, -1.64), 0.018, 0.025, states, materials["M_Amber"], 10, root)
        paper = new_box(f"{root_name}_Paper", (0.15, 0.86, -2.12), (0.42, 0.018, 0.31), states, materials["M_AgedPaper"], 0.004, root)
        paper.rotation_euler.z = 0.16
        new_cylinder(f"{root_name}_PencilCup", (0.92, 0.94, -2.10), 0.075, 0.22, states, materials["M_DarkMetal"], 16, root)


def atlas_remap_meshes() -> None:
    handled: set[int] = set()
    padding = 0.018
    for obj in bpy.data.objects:
        if obj.type != "MESH" or obj.data.as_pointer() in handled:
            continue
        mesh = obj.data
        handled.add(mesh.as_pointer())
        if bool(mesh.get("town_rpg_atlas_v3")):
            continue
        if not mesh.uv_layers:
            mesh.uv_layers.new(name="UVMap")
        uv_data = mesh.uv_layers.active.data
        old_atlas = bool(mesh.get("town_rpg_atlas_v2"))
        for polygon in mesh.polygons:
            if polygon.material_index >= len(mesh.materials):
                continue
            material = mesh.materials[polygon.material_index]
            if material is None or material.name not in ATLAS_REGIONS:
                continue
            _, column, row = ATLAS_REGIONS[material.name]
            old_column, old_row = OLD_ATLAS_REGIONS[material.name]
            for loop_index in polygon.loop_indices:
                uv = uv_data[loop_index].uv
                if old_atlas:
                    local_u = (uv.x * 4 - old_column - padding) / (1 - padding * 2)
                    local_v = (uv.y * 4 - (3 - old_row) - padding) / (1 - padding * 2)
                else:
                    local_u = uv.x
                    local_v = uv.y
                local_u = min(max(local_u, 0.0), 1.0)
                local_v = min(max(local_v, 0.0), 1.0)
                uv.x = (column + padding + local_u * (1 - padding * 2)) / 2
                uv.y = (3 - row + padding + local_v * (1 - padding * 2)) / 4
        mesh["town_rpg_atlas_v3"] = True
        if "town_rpg_atlas_v2" in mesh:
            del mesh["town_rpg_atlas_v2"]


def expand_floor_uv() -> None:
    """Give the visible floor face the entire atlas region, not one box-unwrap island."""
    floor = bpy.data.objects["Room_Floor"]
    mesh = floor.data
    uv_layer = mesh.uv_layers.active
    top = max(mesh.polygons, key=lambda polygon: polygon.normal.z)
    vertices = [mesh.vertices[mesh.loops[index].vertex_index].co for index in top.loop_indices]
    x_min, x_max = min(vertex.x for vertex in vertices), max(vertex.x for vertex in vertices)
    y_min, y_max = min(vertex.y for vertex in vertices), max(vertex.y for vertex in vertices)
    padding = 0.018
    for loop_index, vertex in zip(top.loop_indices, vertices):
        local_u = (vertex.x - x_min) / (x_max - x_min)
        local_v = (vertex.y - y_min) / (y_max - y_min)
        uv_layer.data[loop_index].uv = (
            (padding + local_u * (1 - padding * 2)) / 2,
            (2 + padding + local_v * (1 - padding * 2)) / 4,
        )
    mesh.update()
    bpy.context.scene["town_rpg_floor_uv"] = "0.6m_16x12"


def make_lookdev(lookdev: bpy.types.Collection) -> None:
    world = bpy.context.scene.world or bpy.data.worlds.new("ClassroomLookdevWorld")
    bpy.context.scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.018, 0.028, 0.034, 1)
    background.inputs["Strength"].default_value = 0.24
    bpy.context.scene.render.engine = "BLENDER_EEVEE"
    bpy.context.scene.render.resolution_x = 1280
    bpy.context.scene.render.resolution_y = 720
    bpy.context.scene.render.resolution_percentage = 100

    def look_at(obj: bpy.types.Object, target: Vector) -> None:
        obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()

    target = Vector((0, 0, 1.0))
    camera_positions = {
        "Lookdev_Camera_SE": (7.2, -7.2, 7.3),
        "Lookdev_Camera_SW": (-7.2, -7.2, 7.3),
        "Lookdev_Camera_NW": (-7.2, 7.2, 7.3),
        "Lookdev_Camera_NE": (7.2, 7.2, 7.3),
    }
    for name, position in camera_positions.items():
        data = bpy.data.cameras.new(f"{name}_Data")
        data.type = "ORTHO"
        data.ortho_scale = 9.4
        camera = bpy.data.objects.new(name, data)
        lookdev.objects.link(camera)
        camera.location = position
        look_at(camera, target)
        if name.endswith("SE"):
            bpy.context.scene.camera = camera

    sun_data = bpy.data.lights.new("Lookdev_Sun_Data", "SUN")
    sun_data.color = (1.0, 0.60, 0.32)
    sun_data.energy = 2.1
    sun = bpy.data.objects.new("Lookdev_Sun", sun_data)
    lookdev.objects.link(sun)
    sun.rotation_euler = (math.radians(48), 0, math.radians(-58))
    area_data = bpy.data.lights.new("Lookdev_CoolFill_Data", "AREA")
    area_data.color = (0.30, 0.48, 0.58)
    area_data.energy = 480
    area_data.shape = "RECTANGLE"
    area_data.size = 5.0
    area = bpy.data.objects.new("Lookdev_CoolFill", area_data)
    lookdev.objects.link(area)
    area.location = (1.5, -0.5, 5.5)
    look_at(area, target)


def bootstrap_scene(root: Path) -> None:
    builder = load_mvp_builder(root)
    builder.build_scene()
    collections = {
        "Shell": move_collection("ClassroomShell", "Shell"),
        "Furniture": move_collection("ClassroomFurniture", "Furniture"),
        "Props": move_collection("ClassroomDetails", "Props"),
        "StateVariants": move_collection("ClassroomStates", "StateVariants"),
        "Decals": ensure_collection("Decals"),
        "Lookdev": ensure_collection("Lookdev"),
    }
    detail_material, exterior_material = assign_atlas_materials(root)
    materials = {name: bpy.data.materials[name] for name in ATLAS_REGIONS}
    replace_student_desks(collections["Furniture"], materials)
    enrich_architecture(collections, materials, detail_material, exterior_material)
    enrich_teacher_desks(collections["StateVariants"], materials)
    atlas_remap_meshes()
    expand_floor_uv()
    upgrade_third_person_details(collections, materials)
    atlas_remap_meshes()
    make_lookdev(collections["Lookdev"])
    bpy.context.scene["town_rpg_asset"] = "dream_classroom_art_pass_v2"
    bpy.context.scene["town_rpg_room_size"] = "9.6x7.2x3.2"
    bpy.context.scene["town_rpg_source_authority"] = "blend"


def validate_scene(root: Path) -> dict[str, object]:
    if bpy.app.version[:3] != BLENDER_VERSION:
        raise RuntimeError(f"Expected Blender {BLENDER_VERSION}, got {bpy.app.version[:3]}")
    if bpy.context.scene.get("town_rpg_asset") != "dream_classroom_art_pass_v2":
        raise RuntimeError("This Blend is not tagged as the classroom art pass v2")
    if bpy.context.scene.get("town_rpg_floor_uv") != "0.6m_16x12":
        raise RuntimeError("The classroom floor UV is not configured for 0.6 m square tiles")
    if bpy.context.scene.get("town_rpg_third_person_art") != "v4":
        raise RuntimeError("The classroom has not received the third-person detail upgrade")
    names = set(bpy.data.objects.keys())
    missing = sorted(REQUIRED_OBJECTS - names)
    if missing:
        raise RuntimeError(f"Missing required art-pass objects: {', '.join(missing)}")
    desk_groups = sorted(name for name in names if name.startswith("StudentDeskGroup_") and "_" not in name[17:])
    if len(desk_groups) != 10:
        raise RuntimeError(f"Expected 10 desk groups, got {len(desk_groups)}")
    desk_objects = [bpy.data.objects[f"StudentDeskArt_{index:02d}"] for index in range(1, 11)]
    if len({obj.data.as_pointer() for obj in desk_objects}) != 1:
        raise RuntimeError("All ten art-pass desks must share one mesh data-block")
    mesh_objects = [obj for obj in bpy.data.objects if obj.type == "MESH" and not any(collection.name == "Lookdev" for collection in obj.users_collection)]
    triangles = sum(sum(max(0, len(poly.vertices) - 2) for poly in obj.data.polygons) for obj in mesh_objects)
    if triangles > 100_000:
        raise RuntimeError(f"Art-pass triangle budget exceeded: {triangles}")
    if len(mesh_objects) > 140:
        raise RuntimeError(f"Art-pass mesh-instance budget exceeded: {len(mesh_objects)}")
    for filename in TEXTURE_FILES:
        if not (root / TEXTURE_RELATIVE / filename).is_file():
            raise RuntimeError(f"Missing texture: {filename}")
    if not (root / TEXTURE_MANIFEST_RELATIVE).is_file():
        raise RuntimeError("Texture manifest is missing")
    return {
        "blenderVersion": bpy.app.version_string,
        "roomSizeMeters": list(ROOM_SIZE),
        "requiredObjects": sorted(REQUIRED_OBJECTS),
        "studentDeskGroups": desk_groups,
        "meshObjectCount": len(mesh_objects),
        "uniqueMeshCount": len({obj.data.as_pointer() for obj in mesh_objects}),
        "triangleCount": triangles,
        "materialCount": len(bpy.data.materials),
        "imageCount": len(bpy.data.images),
        "textureFiles": {
            filename: sha256(root / TEXTURE_RELATIVE / filename) for filename in TEXTURE_FILES
        },
    }


def export_glb(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in bpy.context.scene.objects:
        if not any(collection.name == "Lookdev" for collection in obj.users_collection):
            obj.select_set(True)
    result = bpy.ops.export_scene.gltf(
        filepath=str(path),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_animations=False,
        export_cameras=False,
        export_lights=False,
        export_materials="EXPORT",
        export_texcoords=True,
        export_normals=True,
        # Godot imports this GLB with `meshes/ensure_tangents=true`. Letting the
        # engine generate them avoids unstable Blender tangents on tiny bevels.
        export_tangents=False,
        export_yup=True,
    )
    if "FINISHED" not in result:
        raise RuntimeError(f"glTF export failed: {result}")


def write_runtime(root: Path, source: Path) -> dict[str, object]:
    summary = validate_scene(root)
    glb = root / GLB_RELATIVE
    export_glb(glb)
    summary.update({
        "schemaVersion": 2,
        "sourceAuthority": "blend",
        "sourceFile": SOURCE_RELATIVE.as_posix(),
        "runtimeFile": GLB_RELATIVE.as_posix(),
        "textureManifest": TEXTURE_MANIFEST_RELATIVE.as_posix(),
        "sourceSha256": sha256(source),
        "runtimeSha256": sha256(glb),
        "exportProfile": {
            "format": "GLB",
            "cameras": False,
            "lights": False,
            "animations": False,
            "appliedModifiers": True,
            "tangents": "generated-by-godot-import",
        },
    })
    (root / MANIFEST_RELATIVE).write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return summary


def bootstrap(root: Path) -> None:
    bootstrap_scene(root)
    source = root / SOURCE_RELATIVE
    source.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(source), check_existing=False)
    summary = write_runtime(root, source)
    print(f"CLASSROOM_ART_BOOTSTRAP_PASS meshes={summary['meshObjectCount']} triangles={summary['triangleCount']} desks=10")


def export(root: Path) -> None:
    source = root / SOURCE_RELATIVE
    bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False)
    summary = write_runtime(root, source)
    print(f"CLASSROOM_ART_EXPORT_PASS meshes={summary['meshObjectCount']} triangles={summary['triangleCount']} desks=10")


def upgrade_third_person(root: Path) -> None:
    source = root / SOURCE_RELATIVE
    bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False)
    collections = {
        name: ensure_collection(name)
        for name in ("Shell", "Furniture", "Props", "Decals", "StateVariants", "Lookdev")
    }
    detail_material, exterior_material = assign_atlas_materials(root)
    _ = detail_material, exterior_material
    materials = {name: bpy.data.materials[name] for name in ATLAS_REGIONS}
    atlas_remap_meshes()
    expand_floor_uv()
    upgrade_third_person_details(collections, materials)
    atlas_remap_meshes()
    bpy.ops.wm.save_as_mainfile(filepath=str(source), check_existing=False)
    summary = write_runtime(root, source)
    print(f"CLASSROOM_ART_UPGRADE_PASS meshes={summary['meshObjectCount']} triangles={summary['triangleCount']} desks=10")


def check(root: Path) -> None:
    source = root / SOURCE_RELATIVE
    glb = root / GLB_RELATIVE
    manifest_path = root / MANIFEST_RELATIVE
    if not source.is_file() or not glb.is_file() or not manifest_path.is_file():
        raise RuntimeError("Art-pass Blend, GLB, or manifest is missing")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False)
    summary = validate_scene(root)
    if manifest.get("sourceSha256") != sha256(source):
        raise RuntimeError("Tracked art-pass Blend hash does not match manifest")
    if manifest.get("runtimeSha256") != sha256(glb):
        raise RuntimeError("Tracked art-pass GLB hash does not match manifest")
    for key in ("requiredObjects", "studentDeskGroups", "meshObjectCount", "triangleCount", "textureFiles"):
        if manifest.get(key) != summary[key]:
            raise RuntimeError(f"Art-pass manifest field is stale: {key}")
    with tempfile.TemporaryDirectory(prefix="town-rpg-classroom-art-") as temporary:
        candidate = Path(temporary) / "classroom-art-pass-v2.glb"
        export_glb(candidate)
        if sha256(candidate) != sha256(glb):
            raise RuntimeError("Fresh art-pass GLB export is not deterministic")
    print(f"CLASSROOM_ART_CHECK_PASS meshes={summary['meshObjectCount']} triangles={summary['triangleCount']} desks=10")


def main() -> None:
    args = parse_args()
    root = Path(args.root).resolve()
    if args.mode == "art-bootstrap":
        bootstrap(root)
    elif args.mode == "art-upgrade-third-person":
        upgrade_third_person(root)
    elif args.mode == "art-export":
        export(root)
    else:
        check(root)


if __name__ == "__main__":
    main()
