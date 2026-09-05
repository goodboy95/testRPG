"""Build, export, and validate the adopted Dream Manor 3D asset.

Run through Blender 5.2, not system Python:
  blender --background --factory-startup --python-exit-code 1 \
    --python scripts/tools/build_dream_manor_3d.py -- manor-build --root .
  blender --background --python-exit-code 1 \
    --python scripts/tools/build_dream_manor_3d.py -- manor-export --root .
  blender --background --python-exit-code 1 \
    --python scripts/tools/build_dream_manor_3d.py -- manor-check --root .

The Blend is the authoritative shared source. The hall and exterior GLBs are
separate deterministic exports: the game only loads the hall in this phase.
Godot owns gameplay collision and never depends on Blender at runtime.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
import sys
import tempfile

import bpy
from mathutils import Vector


BLENDER_VERSION = (5, 2, 0)
SOURCE_RELATIVE = Path("art/blender/dream_manor/dream-manor-v1.blend")
HALL_GLB_RELATIVE = Path("assets/generated/dream_escape/3d/dream-manor-hall-v1.glb")
EXTERIOR_GLB_RELATIVE = Path("assets/generated/dream_escape/3d/dream-manor-exterior-v1.glb")
MANIFEST_RELATIVE = Path("assets/generated/dream_escape/3d/dream-manor-v1-manifest.json")
TEXTURE_RELATIVE = Path("art/blender/dream_manor/textures-v1")
TEXTURE_MANIFEST_RELATIVE = TEXTURE_RELATIVE / "dream-manor-textures-v1-manifest.json"

FOYER_SIZE = (7.6, 6.8, 4.2)
HALL_SIZE = (6.8, 18.0, 4.2)
WALL_THICKNESS = 0.24
DOOR_OPEN_ANGLES = {
    "Door_Room1_Hinge": -72.0,
    "Door_Room2_Hinge": 72.0,
    "Door_Room3_Hinge": -72.0,
    "Door_Room4_Hinge_L": -62.0,
    "Door_Room4_Hinge_R": 62.0,
}
COLLECTIONS = {
    "InteriorShell",
    "ExteriorShell",
    "Doors",
    "Furniture",
    "Props",
    "Decals",
    "ReservedModules",
    "Lookdev",
}
REQUIRED_HALL_OBJECTS = {
    "DreamManorHall",
    "ManorHall_InteriorShell",
    "Hall_Floor",
    "Foyer_Floor",
    "Hall_Ceiling",
    "Foyer_Ceiling",
    "Door_Front_Root",
    "Door_Front_Hinge_L",
    "Door_Front_Hinge_R",
    "Door_Room1_Root",
    "Door_Room1_Hinge",
    "Door_Room2_Root",
    "Door_Room2_Hinge",
    "Door_Room3_Root",
    "Door_Room3_Hinge",
    "Door_Room4_Root",
    "Door_Room4_Hinge_L",
    "Door_Room4_Hinge_R",
    "Furniture_Fireplace",
    "Furniture_Console",
    "Furniture_Bench",
    "Prop_UmbrellaStand",
    "Runner_Rug",
    "ReservedModule_A",
    "ReservedModule_B",
}
REQUIRED_EXTERIOR_OBJECTS = {
    "DreamManorExterior",
    "Exterior_MainVolume",
    "Exterior_Roof_Main",
    "Exterior_FrontDoor",
    "Exterior_RoomVolume_1",
    "Exterior_RoomVolume_2",
    "Exterior_RoomVolume_3",
    "Exterior_RoomVolume_4",
}
TEXTURE_FILES = (
    "manor-building-albedo.png",
    "manor-building-normal.png",
    "manor-building-orm.png",
    "manor-furnishings-albedo.png",
    "manor-furnishings-normal.png",
    "manor-furnishings-orm.png",
    "manor-rug-albedo.png",
    "manor-rug-normal.png",
    "manor-rug-orm.png",
    "manor-details.png",
)


def parse_args() -> argparse.Namespace:
    raw = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "mode",
        choices=(
            "build", "export", "check", "capture",
            "manor-build", "manor-export", "manor-check", "manor-capture",
        ),
    )
    parser.add_argument("--root", required=True)
    parser.add_argument("--output")
    return parser.parse_args(raw)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def logical_position(value: tuple[float, float, float]) -> tuple[float, float, float]:
    """Godot X/Y-up/Z-north to Blender X/Y-south/Z-up."""
    x, y, z = value
    return x, -z, y


def logical_dimensions(value: tuple[float, float, float]) -> tuple[float, float, float]:
    x, y, z = value
    return x, z, y


def link_only(obj: bpy.types.Object, target: bpy.types.Collection) -> None:
    for owner in list(obj.users_collection):
        owner.objects.unlink(obj)
    target.objects.link(obj)


def parent_keep_world(obj: bpy.types.Object, parent: bpy.types.Object | None) -> None:
    if parent is None:
        return
    world = obj.matrix_world.copy()
    obj.parent = parent
    obj.matrix_world = world


def new_collection(name: str) -> bpy.types.Collection:
    result = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(result)
    return result


def new_empty(name: str, target: bpy.types.Collection, parent: bpy.types.Object | None = None,
              location: tuple[float, float, float] = (0.0, 0.0, 0.0)) -> bpy.types.Object:
    result = bpy.data.objects.new(name, None)
    target.objects.link(result)
    result.location = logical_position(location)
    parent_keep_world(result, parent)
    return result


def assign_uv_region(obj: bpy.types.Object, region: tuple[float, float, float, float]) -> None:
    if obj.type != "MESH" or not obj.data.uv_layers:
        return
    u0, v0, u1, v1 = region
    layer = obj.data.uv_layers.active.data
    if len(obj.data.polygons) == 6 and all(len(polygon.loop_indices) == 4 for polygon in obj.data.polygons):
        # Primitive boxes need a complete atlas region on every face. Blender's
        # default cube-cross UV would otherwise sample only a thin strip of a
        # quadrant, which is especially visible on the long runner and floors.
        corners = ((u0, v0), (u1, v0), (u1, v1), (u0, v1))
        for polygon in obj.data.polygons:
            for loop_index, uv in zip(polygon.loop_indices, corners):
                layer[loop_index].uv = uv
    else:
        for loop in layer:
            loop.uv.x = u0 + max(0.0, min(1.0, loop.uv.x)) * (u1 - u0)
            loop.uv.y = v0 + max(0.0, min(1.0, loop.uv.y)) * (v1 - v0)


def add_box(name: str, position: tuple[float, float, float], dimensions: tuple[float, float, float],
            target: bpy.types.Collection, material: bpy.types.Material, region: tuple[float, float, float, float],
            bevel: float = 0.0, parent: bpy.types.Object | None = None,
            rotation: tuple[float, float, float] = (0.0, 0.0, 0.0)) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(location=logical_position(position), rotation=rotation)
    result = bpy.context.object
    result.name = name
    result.dimensions = logical_dimensions(dimensions)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    link_only(result, target)
    result.data.materials.append(material)
    assign_uv_region(result, region)
    if bevel > 0.0:
        modifier = result.modifiers.new("EdgeSoftness", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        bpy.context.view_layer.objects.active = result
        result.select_set(True)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        result.select_set(False)
    parent_keep_world(result, parent)
    return result


def add_cylinder(name: str, position: tuple[float, float, float], radius: float, depth: float,
                 target: bpy.types.Collection, material: bpy.types.Material, region: tuple[float, float, float, float],
                 vertices: int = 16, rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
                 parent: bpy.types.Object | None = None) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth,
                                       location=logical_position(position), rotation=rotation)
    result = bpy.context.object
    result.name = name
    link_only(result, target)
    result.data.materials.append(material)
    assign_uv_region(result, region)
    parent_keep_world(result, parent)
    return result


def add_uv_sphere(name: str, position: tuple[float, float, float], scale: tuple[float, float, float],
                  target: bpy.types.Collection, material: bpy.types.Material,
                  region: tuple[float, float, float, float], parent: bpy.types.Object | None = None) -> bpy.types.Object:
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, location=logical_position(position))
    result = bpy.context.object
    result.name = name
    result.scale = logical_dimensions(scale)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    link_only(result, target)
    result.data.materials.append(material)
    assign_uv_region(result, region)
    parent_keep_world(result, parent)
    return result


def add_roof(name: str, center: tuple[float, float, float], width: float, depth: float,
             eave_height: float, ridge_height: float, target: bpy.types.Collection,
             material: bpy.types.Material, region: tuple[float, float, float, float],
             parent: bpy.types.Object, ridge_axis: str = "z") -> bpy.types.Object:
    root = new_empty(name, target, parent)
    rise = ridge_height - eave_height
    if ridge_axis == "z":
        run = width * 0.5 + 0.35
        slope = math.atan2(rise, run)
        length = math.sqrt(run * run + rise * rise)
        for side in (-1, 1):
            x = center[0] + side * width * 0.25
            y = (eave_height + ridge_height) * 0.5
            obj = add_box(f"{name}_Slope_{'W' if side < 0 else 'E'}", (x, y, center[2]),
                          (length, 0.12, depth + 0.7), target, material, region, 0.025, root,
                          rotation=(0.0, -side * slope, 0.0))
            obj["sharedSurface"] = "roof_slate"
    else:
        run = depth * 0.5 + 0.35
        slope = math.atan2(rise, run)
        length = math.sqrt(run * run + rise * rise)
        for side in (-1, 1):
            z = center[2] + side * depth * 0.25
            y = (eave_height + ridge_height) * 0.5
            obj = add_box(f"{name}_Slope_{'S' if side < 0 else 'N'}", (center[0], y, z),
                          (width + 0.7, 0.12, length), target, material, region, 0.025, root,
                          rotation=(side * slope, 0.0, 0.0))
            obj["sharedSurface"] = "roof_slate"
    return root


def load_image(path: Path, non_color: bool) -> bpy.types.Image:
    image = bpy.data.images.load(str(path), check_existing=True)
    image.colorspace_settings.name = "Non-Color" if non_color else "sRGB"
    return image


def pbr_material(name: str, albedo: Path, normal: Path, orm: Path,
                 roughness: float = 0.8, metallic: float = 0.0) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    base = nodes.new("ShaderNodeTexImage")
    base.name = f"{name}_Albedo"
    base.image = load_image(albedo, False)
    base.interpolation = "Linear"
    norm = nodes.new("ShaderNodeTexImage")
    norm.name = f"{name}_Normal"
    norm.image = load_image(normal, True)
    norm.interpolation = "Linear"
    normal_map = nodes.new("ShaderNodeNormalMap")
    normal_map.inputs["Strength"].default_value = 0.45
    packed = nodes.new("ShaderNodeTexImage")
    packed.name = f"{name}_ORM"
    packed.image = load_image(orm, True)
    packed.interpolation = "Linear"
    separate = nodes.new("ShaderNodeSeparateColor")
    links.new(base.outputs["Color"], shader.inputs["Base Color"])
    links.new(norm.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], shader.inputs["Normal"])
    links.new(packed.outputs["Color"], separate.inputs["Color"])
    links.new(separate.outputs["Green"], shader.inputs["Roughness"])
    links.new(separate.outputs["Blue"], shader.inputs["Metallic"])
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return material


def flat_material(name: str, color: tuple[float, float, float, float], roughness: float,
                  metallic: float = 0.0, emission: tuple[float, float, float, float] | None = None) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = color
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    if emission:
        shader.inputs["Emission Color"].default_value = emission
        shader.inputs["Emission Strength"].default_value = 1.3
    return material


def make_materials(root: Path) -> dict[str, bpy.types.Material]:
    texture = root / TEXTURE_RELATIVE
    building = tuple(texture / name for name in ("manor-building-albedo.png", "manor-building-normal.png", "manor-building-orm.png"))
    furnishing = tuple(texture / name for name in ("manor-furnishings-albedo.png", "manor-furnishings-normal.png", "manor-furnishings-orm.png"))
    rug = tuple(texture / name for name in ("manor-rug-albedo.png", "manor-rug-normal.png", "manor-rug-orm.png"))
    return {
        "plaster": pbr_material("M_Manor_Plaster", *building, 0.86),
        "stone": pbr_material("M_Manor_Stone", *building, 0.80),
        "terrazzo": pbr_material("M_Manor_Terrazzo", *building, 0.76),
        "slate": pbr_material("M_Manor_Slate", *building, 0.82),
        "wood": pbr_material("M_Manor_DarkOak", *furnishing, 0.68),
        "brass": pbr_material("M_Manor_AgedBrass", *furnishing, 0.48, 0.65),
        "upholstery": pbr_material("M_Manor_Upholstery", *furnishing, 0.84),
        "iron": pbr_material("M_Manor_DarkIron", *furnishing, 0.58, 0.55),
        "rug": pbr_material("M_Manor_Runner", *rug, 0.84),
        "glass": flat_material("M_Manor_LeadedGlass", (0.31, 0.39, 0.34, 1.0), 0.22, 0.05,
                               (0.17, 0.09, 0.025, 1.0)),
        "paper": flat_material("M_Manor_AgedPaper", (0.48, 0.39, 0.27, 1.0), 0.91),
        "dark": flat_material("M_Manor_DarkVoid", (0.009, 0.012, 0.014, 1.0), 1.0),
        "plant": flat_material("M_Manor_Foliage", (0.055, 0.095, 0.068, 1.0), 0.93),
    }


REGION_BUILDING = {
    "plaster": (0.0, 0.5, 0.5, 1.0),
    "stone": (0.5, 0.5, 1.0, 1.0),
    "terrazzo": (0.0, 0.0, 0.5, 0.5),
    "slate": (0.5, 0.0, 1.0, 0.5),
}
REGION_FURNISHING = {
    "wood": (0.0, 0.5, 0.5, 1.0),
    "brass": (0.5, 0.5, 1.0, 1.0),
    "upholstery": (0.0, 0.0, 0.5, 0.5),
    "iron": (0.5, 0.0, 1.0, 0.5),
}
REGION_FULL = (0.0, 0.0, 1.0, 1.0)


def wall_segments_x(name: str, fixed_x: float, ranges: list[tuple[float, float]], target: bpy.types.Collection,
                    root: bpy.types.Object, mats: dict[str, bpy.types.Material], inside_sign: float) -> None:
    for index, (start, end) in enumerate(ranges, 1):
        add_box(f"{name}_Plaster_{index:02d}", (fixed_x, 2.1, (start+end)*0.5),
                (WALL_THICKNESS, 4.2, end-start), target, mats["plaster"], REGION_BUILDING["plaster"], parent=root)
        add_box(f"{name}_Wainscot_{index:02d}", (fixed_x + inside_sign*0.135, 0.70, (start+end)*0.5),
                (0.055, 1.40, max(0.02, end-start-0.06)), target, mats["wood"], REGION_FURNISHING["wood"], 0.012, root)
        add_box(f"{name}_ChairRail_{index:02d}", (fixed_x + inside_sign*0.17, 1.43, (start+end)*0.5),
                (0.07, 0.08, end-start), target, mats["wood"], REGION_FURNISHING["wood"], 0.015, root)
    add_box(f"{name}_Baseboard", (fixed_x + inside_sign*0.17, 0.09, (ranges[0][0]+ranges[-1][1])*0.5),
            (0.08, 0.18, ranges[-1][1]-ranges[0][0]), target, mats["wood"], REGION_FURNISHING["wood"], 0.012, root)


def wall_segments_z(name: str, fixed_z: float, ranges: list[tuple[float, float]], target: bpy.types.Collection,
                    root: bpy.types.Object, mats: dict[str, bpy.types.Material], inside_sign: float) -> None:
    for index, (start, end) in enumerate(ranges, 1):
        add_box(f"{name}_Plaster_{index:02d}", ((start+end)*0.5, 2.1, fixed_z),
                (end-start, 4.2, WALL_THICKNESS), target, mats["plaster"], REGION_BUILDING["plaster"], parent=root)
        add_box(f"{name}_Wainscot_{index:02d}", ((start+end)*0.5, 0.70, fixed_z + inside_sign*0.135),
                (max(0.02, end-start-0.06), 1.40, 0.055), target, mats["wood"], REGION_FURNISHING["wood"], 0.012, root)
        add_box(f"{name}_ChairRail_{index:02d}", ((start+end)*0.5, 1.43, fixed_z + inside_sign*0.17),
                (end-start, 0.08, 0.07), target, mats["wood"], REGION_FURNISHING["wood"], 0.015, root)
    add_box(f"{name}_Baseboard", ((ranges[0][0]+ranges[-1][1])*0.5, 0.09, fixed_z + inside_sign*0.17),
            (ranges[-1][1]-ranges[0][0], 0.18, 0.08), target, mats["wood"], REGION_FURNISHING["wood"], 0.012, root)


def frame_side_door(prefix: str, x: float, z: float, inside_sign: float, target: bpy.types.Collection,
                    mats: dict[str, bpy.types.Material], parent: bpy.types.Object) -> None:
    add_box(f"{prefix}_Frame_S", (x + inside_sign*0.16, 1.35, z-0.67), (0.09, 2.70, 0.12), target,
            mats["wood"], REGION_FURNISHING["wood"], 0.018, parent)
    add_box(f"{prefix}_Frame_N", (x + inside_sign*0.16, 1.35, z+0.67), (0.09, 2.70, 0.12), target,
            mats["wood"], REGION_FURNISHING["wood"], 0.018, parent)
    add_box(f"{prefix}_Frame_Top", (x + inside_sign*0.16, 2.68, z), (0.09, 0.14, 1.45), target,
            mats["wood"], REGION_FURNISHING["wood"], 0.018, parent)
    add_box(f"{prefix}_Overdoor", (x + inside_sign*0.18, 2.92, z), (0.075, 0.28, 1.70), target,
            mats["wood"], REGION_FURNISHING["wood"], 0.025, parent)


def build_side_door(number: int, x: float, z: float, west: bool, target: bpy.types.Collection,
                    mats: dict[str, bpy.types.Material], hall_root: bpy.types.Object) -> None:
    root = new_empty(f"Door_Room{number}_Root", target, hall_root)
    hinge_z = z - 0.60 if west else z + 0.60
    hinge = new_empty(f"Door_Room{number}_Hinge", target, root, (x, 0.0, hinge_z))
    leaf_center_z = hinge_z + (0.60 if west else -0.60)
    leaf = add_box(f"Door_Room{number}_Leaf", (x, 1.30, leaf_center_z), (0.10, 2.60, 1.16), target,
                   mats["wood"], REGION_FURNISHING["wood"], 0.018, hinge)
    leaf["doorId"] = f"room{number}"
    add_box(f"Door_Room{number}_Inset", (x + (0.061 if west else -0.061), 1.35, leaf_center_z),
            (0.025, 1.88, 0.80), target, mats["wood"], REGION_FURNISHING["wood"], 0.012, hinge)
    add_cylinder(f"Door_Room{number}_Handle", (x + (0.085 if west else -0.085), 1.08,
                 leaf_center_z + (0.36 if west else -0.36)), 0.055, 0.16, target, mats["brass"],
                 REGION_FURNISHING["brass"], 16, rotation=(0.0, math.pi/2.0, 0.0), parent=hinge)
    frame_side_door(f"Door_Room{number}", x, z, 1.0 if west else -1.0, target, mats, root)


def build_double_door(prefix: str, center_x: float, z: float, facing_north: bool, target: bpy.types.Collection,
                      mats: dict[str, bpy.types.Material], hall_root: bpy.types.Object, room4: bool) -> None:
    root_name = "Door_Room4_Root" if room4 else "Door_Front_Root"
    root = new_empty(root_name, target, hall_root)
    total_width = 2.72 if room4 else 2.46
    half = total_width * 0.5
    leaf_width = half - 0.03
    inward = -0.01 if facing_north else 0.01
    for side, label in ((-1, "L"), (1, "R")):
        hinge_x = center_x + side * half
        hinge_name = f"Door_Room4_Hinge_{label}" if room4 else f"Door_Front_Hinge_{label}"
        hinge = new_empty(hinge_name, target, root, (hinge_x, 0.0, z))
        leaf_x = hinge_x - side * leaf_width * 0.5
        leaf_name = f"Door_Room4_Leaf_{label}" if room4 else f"Door_Front_Leaf_{label}"
        leaf = add_box(leaf_name, (leaf_x, 1.38, z + inward), (leaf_width, 2.76, 0.11), target,
                       mats["wood"], REGION_FURNISHING["wood"], 0.022, hinge)
        leaf["doorId"] = "room4" if room4 else "mansion_front_door"
        add_box(f"{leaf_name}_UpperPanel", (leaf_x, 1.88, z + inward + (0.061 if facing_north else -0.061)),
                (leaf_width*0.70, 0.75, 0.026), target, mats["wood"], REGION_FURNISHING["wood"], 0.012, hinge)
        add_box(f"{leaf_name}_LowerPanel", (leaf_x, 0.72, z + inward + (0.061 if facing_north else -0.061)),
                (leaf_width*0.70, 0.70, 0.026), target, mats["wood"], REGION_FURNISHING["wood"], 0.012, hinge)
        handle_x = center_x + side * 0.12
        add_cylinder(f"{leaf_name}_Handle", (handle_x, 1.12, z + inward + (0.08 if facing_north else -0.08)),
                     0.055, 0.16, target, mats["brass"], REGION_FURNISHING["brass"], 16,
                     rotation=(math.pi/2.0, 0.0, 0.0), parent=hinge)
    frame_z = z + (0.15 if facing_north else -0.15)
    add_box(f"{prefix}_Frame_L", (center_x-half-0.10, 1.42, frame_z), (0.16, 2.84, 0.12), target,
            mats["wood"], REGION_FURNISHING["wood"], 0.018, root)
    add_box(f"{prefix}_Frame_R", (center_x+half+0.10, 1.42, frame_z), (0.16, 2.84, 0.12), target,
            mats["wood"], REGION_FURNISHING["wood"], 0.018, root)
    add_box(f"{prefix}_Frame_Top", (center_x, 2.82, frame_z), (total_width+0.36, 0.16, 0.12), target,
            mats["wood"], REGION_FURNISHING["wood"], 0.018, root)


def build_hall(root: Path, collections: dict[str, bpy.types.Collection], mats: dict[str, bpy.types.Material]) -> None:
    shell, doors = collections["InteriorShell"], collections["Doors"]
    furniture, props, reserved = collections["Furniture"], collections["Props"], collections["ReservedModules"]
    hall_root = new_empty("DreamManorHall", shell)
    shell_root = new_empty("ManorHall_InteriorShell", shell, hall_root)
    shell_root["foyerSizeMeters"] = FOYER_SIZE
    shell_root["hallSizeMeters"] = HALL_SIZE

    add_box("Foyer_Floor", (0.0, -0.08, -3.4), (7.6, 0.16, 6.8), shell, mats["terrazzo"], REGION_BUILDING["terrazzo"], parent=shell_root)
    add_box("Hall_Floor", (0.0, -0.08, 9.0), (6.8, 0.16, 18.0), shell, mats["terrazzo"], REGION_BUILDING["terrazzo"], parent=shell_root)
    add_box("Foyer_Ceiling", (0.0, 4.20, -3.4), (7.6, 0.16, 6.8), shell, mats["plaster"], REGION_BUILDING["plaster"], parent=shell_root)
    add_box("Hall_Ceiling", (0.0, 4.20, 9.0), (6.8, 0.16, 18.0), shell, mats["plaster"], REGION_BUILDING["plaster"], parent=shell_root)

    wall_segments_x("Wall_Foyer_West", -3.8, [(-6.8, 0.0)], shell, shell_root, mats, 1.0)
    wall_segments_x("Wall_Foyer_East", 3.8, [(-6.8, 0.0)], shell, shell_root, mats, -1.0)
    wall_segments_z("Wall_Foyer_South", -6.8, [(-3.8, -1.88), (1.88, 3.8)], shell, shell_root, mats, 1.0)
    add_box("Wall_Foyer_South_AboveDoor", (0.0, 3.50, -6.8), (3.76, 1.40, WALL_THICKNESS), shell,
            mats["plaster"], REGION_BUILDING["plaster"], parent=shell_root)
    add_box("Wall_Foyer_Transition_W", (-3.6, 2.1, 0.0), (0.4, 4.2, WALL_THICKNESS), shell,
            mats["plaster"], REGION_BUILDING["plaster"], parent=shell_root)
    add_box("Wall_Foyer_Transition_E", (3.6, 2.1, 0.0), (0.4, 4.2, WALL_THICKNESS), shell,
            mats["plaster"], REGION_BUILDING["plaster"], parent=shell_root)

    west_ranges = [(0.0, 2.58), (3.82, 11.38), (12.62, 18.0)]
    east_ranges = [(0.0, 2.58), (3.82, 18.0)]
    wall_segments_x("Wall_Hall_West", -3.4, west_ranges, shell, shell_root, mats, 1.0)
    wall_segments_x("Wall_Hall_East", 3.4, east_ranges, shell, shell_root, mats, -1.0)
    for z in (3.2, 12.0):
        add_box(f"Wall_Hall_West_DoorTop_{int(z)}", (-3.4, 3.46, z), (WALL_THICKNESS, 1.48, 1.24), shell,
                mats["plaster"], REGION_BUILDING["plaster"], parent=shell_root)
    add_box("Wall_Hall_East_DoorTop_3", (3.4, 3.46, 3.2), (WALL_THICKNESS, 1.48, 1.24), shell,
            mats["plaster"], REGION_BUILDING["plaster"], parent=shell_root)
    wall_segments_z("Wall_Hall_North", 18.0, [(-3.4, -1.42), (1.42, 3.4)], shell, shell_root, mats, -1.0)
    add_box("Wall_Hall_North_DoorTop", (0.0, 3.46, 18.0), (2.84, 1.48, WALL_THICKNESS), shell,
            mats["plaster"], REGION_BUILDING["plaster"], parent=shell_root)

    for index, z in enumerate((-5.2, -3.1, -1.0, 1.4, 3.8, 6.2, 8.6, 11.0, 13.4, 15.8), 1):
        width = 7.35 if z < 0 else 6.55
        add_box(f"CeilingBeam_Cross_{index:02d}", (0.0, 4.02, z), (width, 0.23, 0.18), shell,
                mats["wood"], REGION_FURNISHING["wood"], 0.018, shell_root)
    for x in (-2.25, 0.0, 2.25):
        add_box(f"CeilingBeam_Long_{x:+.2f}", (x, 3.97, 5.6), (0.16, 0.28, 24.2), shell,
                mats["wood"], REGION_FURNISHING["wood"], 0.015, shell_root)

    build_double_door("Door_Front", 0.0, -6.79, True, doors, mats, hall_root, False)
    for x in (-1.63, 1.63):
        add_box(f"Front_Sidelight_Glass_{'L' if x < 0 else 'R'}", (x, 1.45, -6.64), (0.52, 2.58, 0.035), doors,
                mats["glass"], REGION_FULL, 0.01, hall_root)
        for offset in (-0.22, 0.0, 0.22):
            add_box(f"Front_Sidelight_Mullion_{x:+.2f}_{offset:+.2f}", (x+offset, 1.45, -6.60), (0.025, 2.58, 0.035), doors,
                    mats["brass"], REGION_FURNISHING["brass"], 0.004, hall_root)
    build_side_door(1, -3.39, 3.2, True, doors, mats, hall_root)
    build_side_door(2, 3.39, 3.2, False, doors, mats, hall_root)
    build_side_door(3, -3.39, 12.0, True, doors, mats, hall_root)
    build_double_door("Door_Room4", 0.0, 17.99, False, doors, mats, hall_root, True)

    rug_root = add_box("Runner_Rug", (0.0, 0.015, 5.25), (2.15, 0.03, 20.2), props, mats["rug"], REGION_FULL, 0.015, hall_root)
    rug_root["clearRouteWidthMeters"] = 2.2

    fireplace = new_empty("Furniture_Fireplace", furniture, hall_root)
    add_box("Fireplace_Back", (-3.49, 1.05, -3.45), (0.42, 2.10, 1.78), furniture, mats["wood"], REGION_FURNISHING["wood"], 0.025, fireplace)
    add_box("Fireplace_Opening", (-3.73, 0.77, -3.45), (0.05, 1.18, 0.84), furniture, mats["dark"], REGION_FULL, parent=fireplace)
    add_box("Fireplace_Mantle", (-3.35, 1.98, -3.45), (0.64, 0.16, 2.05), furniture, mats["wood"], REGION_FURNISHING["wood"], 0.025, fireplace)
    add_box("Fireplace_Hearth", (-3.10, 0.06, -3.45), (0.85, 0.12, 2.05), furniture, mats["stone"], REGION_BUILDING["stone"], 0.02, fireplace)
    for z in (-3.88, -3.45, -3.02):
        add_box(f"Fireplace_Tile_{z:+.2f}", (-3.74, 1.46, z), (0.03, 0.27, 0.25), furniture, mats["terrazzo"],
                REGION_BUILDING["terrazzo"], 0.006, fireplace)

    console = new_empty("Furniture_Console", furniture, hall_root)
    add_box("Console_Top", (3.06, 0.86, -1.72), (0.62, 0.10, 1.70), furniture, mats["wood"], REGION_FURNISHING["wood"], 0.025, console)
    add_box("Console_Leg_N", (3.10, 0.43, -1.08), (0.11, 0.82, 0.11), furniture, mats["wood"], REGION_FURNISHING["wood"], 0.018, console)
    add_box("Console_Leg_S", (3.10, 0.43, -2.36), (0.11, 0.82, 0.11), furniture, mats["wood"], REGION_FURNISHING["wood"], 0.018, console)
    add_box("Console_Stretcher", (3.10, 0.32, -1.72), (0.10, 0.10, 1.25), furniture, mats["wood"], REGION_FURNISHING["wood"], 0.015, console)
    add_uv_sphere("Console_Vase", (3.02, 1.18, -1.38), (0.20, 0.30, 0.20), props, mats["upholstery"], REGION_FURNISHING["upholstery"], console)
    add_box("Console_Book", (2.98, 0.97, -1.98), (0.35, 0.07, 0.52), props, mats["paper"], REGION_FULL, 0.01, console)

    bench = new_empty("Furniture_Bench", furniture, hall_root)
    add_box("Bench_Seat", (3.00, 0.48, 6.8), (0.62, 0.16, 1.62), furniture, mats["upholstery"], REGION_FURNISHING["upholstery"], 0.06, bench)
    add_box("Bench_Back", (3.18, 1.04, 6.8), (0.18, 0.92, 1.62), furniture, mats["wood"], REGION_FURNISHING["wood"], 0.035, bench)
    for z in (6.18, 7.42):
        add_box(f"Bench_Leg_{z:.2f}", (3.03, 0.23, z), (0.16, 0.44, 0.16), furniture, mats["wood"], REGION_FURNISHING["wood"], 0.025, bench)

    stand = new_empty("Prop_UmbrellaStand", props, hall_root)
    add_cylinder("UmbrellaStand_Bucket", (2.93, 0.38, -5.75), 0.21, 0.68, props, mats["brass"], REGION_FURNISHING["brass"], 20, parent=stand)
    for index, offset in enumerate((-0.10, 0.0, 0.10), 1):
        add_cylinder(f"Umbrella_{index}", (2.93+offset, 0.86, -5.75), 0.018, 1.10, props, mats["iron"], REGION_FURNISHING["iron"], 10, parent=stand)

    for index, (x, z, west) in enumerate(((-3.28, -0.9, True), (3.28, 9.0, False), (-3.28, 14.7, True)), 1):
        painting = new_empty(f"Painting_{index:02d}", props, hall_root)
        add_box(f"Painting_{index:02d}_Frame", (x, 2.45, z), (0.10, 1.25, 1.55), props, mats["wood"], REGION_FURNISHING["wood"], 0.025, painting)
        add_box(f"Painting_{index:02d}_Canvas", (x + (0.061 if west else -0.061), 2.45, z), (0.02, 1.03, 1.31), props,
                mats["upholstery"], REGION_FURNISHING["upholstery"], 0.006, painting)

    for index, (x, z) in enumerate(((-3.27, -5.0), (3.27, -5.0), (-3.27, 1.0), (3.27, 5.8), (-3.27, 9.0), (3.27, 15.4)), 1):
        root_obj = new_empty(f"WallSconce_{index:02d}", props, hall_root)
        add_cylinder(f"WallSconce_{index:02d}_Stem", (x, 2.30, z), 0.045, 0.38, props, mats["brass"], REGION_FURNISHING["brass"], 12,
                     rotation=(0.0, math.pi/2.0, 0.0), parent=root_obj)
        add_uv_sphere(f"WallSconce_{index:02d}_Shade", (x + (0.20 if x < 0 else -0.20), 2.42, z), (0.13, 0.18, 0.13), props,
                      mats["glass"], REGION_FULL, root_obj)

    for label, z in (("A", 10.0), ("B", 14.0)):
        module = new_empty(f"ReservedModule_{label}", reserved, hall_root)
        module["futureExpansion"] = True
        add_box(f"ReservedModule_{label}_Panel", (3.25, 1.62, z), (0.08, 2.62, 1.62), reserved,
                mats["wood"], REGION_FURNISHING["wood"], 0.025, module)
        add_box(f"ReservedModule_{label}_Inset", (3.19, 1.65, z), (0.04, 2.18, 1.18), reserved,
                mats["plaster"], REGION_BUILDING["plaster"], 0.018, module)
        add_box(f"ReservedModule_{label}_Lintel", (3.15, 3.02, z), (0.12, 0.22, 1.90), reserved,
                mats["wood"], REGION_FURNISHING["wood"], 0.02, module)


def exterior_window(name: str, position: tuple[float, float, float], wall: str,
                    target: bpy.types.Collection, mats: dict[str, bpy.types.Material], parent: bpy.types.Object) -> None:
    x, y, z = position
    if wall in ("east", "west"):
        add_box(f"{name}_Glass", (x, y, z), (0.035, 1.42, 1.50), target, mats["glass"], REGION_FULL, 0.006, parent)
        for dz in (-0.74, 0.0, 0.74):
            add_box(f"{name}_MullionZ_{dz:+.2f}", (x, y, z+dz), (0.07, 1.56, 0.07), target, mats["wood"], REGION_FURNISHING["wood"], 0.008, parent)
        add_box(f"{name}_MullionY", (x, y, z), (0.07, 0.07, 1.56), target, mats["wood"], REGION_FURNISHING["wood"], 0.008, parent)
    else:
        add_box(f"{name}_Glass", (x, y, z), (1.50, 1.42, 0.035), target, mats["glass"], REGION_FULL, 0.006, parent)
        for dx in (-0.74, 0.0, 0.74):
            add_box(f"{name}_MullionX_{dx:+.2f}", (x+dx, y, z), (0.07, 1.56, 0.07), target, mats["wood"], REGION_FURNISHING["wood"], 0.008, parent)
        add_box(f"{name}_MullionY", (x, y, z), (1.56, 0.07, 0.07), target, mats["wood"], REGION_FURNISHING["wood"], 0.008, parent)


def build_exterior(collections: dict[str, bpy.types.Collection], mats: dict[str, bpy.types.Material]) -> None:
    target = collections["ExteriorShell"]
    root = new_empty("DreamManorExterior", target)
    main = new_empty("Exterior_MainVolume", target, root)
    add_box("Exterior_MainFloor", (0.0, -0.10, 5.6), (8.0, 0.20, 25.6), target, mats["stone"], REGION_BUILDING["stone"], parent=main)
    add_box("Exterior_MainWall_W", (-4.0, 1.95, 5.6), (0.30, 3.90, 25.6), target, mats["plaster"], REGION_BUILDING["plaster"], 0.02, main)
    add_box("Exterior_MainWall_E", (4.0, 1.95, 5.6), (0.30, 3.90, 25.6), target, mats["plaster"], REGION_BUILDING["plaster"], 0.02, main)
    add_box("Exterior_MainWall_S", (0.0, 1.95, -7.2), (8.0, 3.90, 0.30), target, mats["plaster"], REGION_BUILDING["plaster"], 0.02, main)
    add_box("Exterior_MainWall_N", (0.0, 1.95, 18.4), (8.0, 3.90, 0.30), target, mats["plaster"], REGION_BUILDING["plaster"], 0.02, main)
    for x, z, width, depth in ((0.0, 5.6, 8.0, 25.6),):
        add_box("Exterior_MainStoneBase", (x, 0.38, z), (width+0.22, 0.76, depth+0.22), target, mats["stone"], REGION_BUILDING["stone"], 0.025, main)
    add_roof("Exterior_Roof_Main", (0.0, 0.0, 5.6), 8.5, 26.2, 3.9, 6.45, target, mats["slate"], REGION_BUILDING["slate"], main)

    volumes = (
        (1, -6.6, 3.2, 5.2, 6.0, "z"),
        (2, 6.6, 3.2, 5.2, 6.0, "z"),
        (3, -6.3, 12.2, 4.6, 5.6, "z"),
        (4, 0.0, 21.7, 11.6, 6.6, "x"),
    )
    for number, x, z, width, depth, ridge in volumes:
        volume = new_empty(f"Exterior_RoomVolume_{number}", target, root)
        add_box(f"Exterior_Room{number}_Walls", (x, 1.80, z), (width, 3.60, depth), target,
                mats["plaster"], REGION_BUILDING["plaster"], 0.025, volume)
        add_box(f"Exterior_Room{number}_StoneBase", (x, 0.34, z), (width+0.18, 0.68, depth+0.18), target,
                mats["stone"], REGION_BUILDING["stone"], 0.025, volume)
        add_roof(f"Exterior_Room{number}_Roof", (x, 0.0, z), width+0.35, depth+0.35, 3.6, 5.55,
                 target, mats["slate"], REGION_BUILDING["slate"], volume, ridge)

    porch = new_empty("Exterior_Porch", target, root)
    add_box("Exterior_Porch_Platform", (0.0, 0.10, -7.62), (3.9, 0.20, 1.15), target, mats["stone"], REGION_BUILDING["stone"], 0.025, porch)
    for step, z in enumerate((-7.90, -8.18, -8.46), 1):
        add_box(f"Exterior_Porch_Step_{step}", (0.0, 0.05*step, z), (4.2+0.25*step, 0.10*step, 0.38), target,
                mats["stone"], REGION_BUILDING["stone"], 0.012, porch)
    for x in (-1.72, 1.72):
        add_box(f"Exterior_Porch_Post_{x:+.2f}", (x, 1.75, -7.56), (0.25, 3.50, 0.25), target,
                mats["wood"], REGION_FURNISHING["wood"], 0.02, porch)
    add_roof("Exterior_Porch_Roof", (0.0, 0.0, -7.55), 4.4, 1.7, 3.50, 4.55, target,
             mats["slate"], REGION_BUILDING["slate"], porch)

    front = new_empty("Exterior_FrontDoor", target, root)
    for side, label in ((-1, "L"), (1, "R")):
        add_box(f"Exterior_FrontDoor_Leaf_{label}", (side*0.61, 1.38, -7.37), (1.18, 2.76, 0.12), target,
                mats["wood"], REGION_FURNISHING["wood"], 0.022, front)
        add_cylinder(f"Exterior_FrontDoor_Handle_{label}", (side*0.12, 1.10, -7.46), 0.055, 0.16, target,
                     mats["brass"], REGION_FURNISHING["brass"], 16, rotation=(math.pi/2.0, 0.0, 0.0), parent=front)
    for x in (-1.62, 1.62):
        add_box(f"Exterior_FrontSidelight_{x:+.2f}", (x, 1.45, -7.37), (0.50, 2.55, 0.035), target,
                mats["glass"], REGION_FULL, 0.008, front)

    window_specs = [
        ("Exterior_Window_E01", (4.16, 1.70, -3.4), "east"),
        ("Exterior_Window_E02", (4.16, 1.70, 8.0), "east"),
        ("Exterior_Window_E03", (4.16, 1.70, 14.5), "east"),
        ("Exterior_Window_W01", (-4.16, 1.70, -3.3), "west"),
        ("Exterior_Window_R1", (-9.22, 1.65, 3.2), "west"),
        ("Exterior_Window_R2", (9.22, 1.65, 3.2), "east"),
        ("Exterior_Window_R3", (-8.62, 1.65, 12.2), "west"),
        ("Exterior_Window_N1", (-2.5, 1.75, 25.05), "north"),
        ("Exterior_Window_N2", (2.5, 1.75, 25.05), "north"),
    ]
    for name, position, wall in window_specs:
        exterior_window(name, position, wall, target, mats, root)

    for index, (x, z) in enumerate(((-2.2, -0.8), (2.2, 8.5), (-2.0, 17.0)), 1):
        chimney = new_empty(f"Exterior_Chimney_{index}", target, root)
        add_box(f"Exterior_Chimney_{index}_Stack", (x, 5.45, z), (0.75, 2.30, 0.68), target,
                mats["stone"], REGION_BUILDING["stone"], 0.025, chimney)
        add_box(f"Exterior_Chimney_{index}_Cap", (x, 6.62, z), (0.92, 0.14, 0.84), target,
                mats["stone"], REGION_BUILDING["stone"], 0.02, chimney)
        for offset in (-0.18, 0.18):
            add_cylinder(f"Exterior_Chimney_{index}_Pot_{offset:+.2f}", (x+offset, 6.96, z), 0.11, 0.54,
                         target, mats["terracotta"] if "terracotta" in mats else mats["stone"], REGION_BUILDING["stone"], 12, parent=chimney)

    for index, (x, z, sx, sz) in enumerate(((-4.2, -5.7, 1.0, 0.7), (4.4, -4.6, 1.2, 0.8), (-9.0, 5.0, 1.1, 0.7),
                                             (9.0, 5.0, 1.1, 0.7), (-8.2, 14.5, 1.2, 0.8), (6.0, 18.8, 1.2, 0.8)), 1):
        add_uv_sphere(f"Exterior_Shrub_{index:02d}", (x, 0.55, z), (sx, 0.65, sz), target,
                      mats["plant"], REGION_FULL, root)


def add_lookdev(collections: dict[str, bpy.types.Collection]) -> None:
    target = collections["Lookdev"]
    interior = bpy.data.cameras.new("Lookdev_Interior_CameraData")
    interior.lens = 46.0
    camera = bpy.data.objects.new("Lookdev_Interior_Camera", interior)
    target.objects.link(camera)
    camera.location = logical_position((0.1, 1.65, -4.8))
    camera.rotation_euler = (math.radians(72), 0.0, 0.0)
    exterior = bpy.data.cameras.new("Lookdev_Exterior_CameraData")
    exterior.lens = 52.0
    camera2 = bpy.data.objects.new("Lookdev_Exterior_Camera", exterior)
    target.objects.link(camera2)
    camera2.location = logical_position((19.0, 10.0, -22.0))
    camera2.rotation_euler = (math.radians(65), 0.0, math.radians(38))
    sun_data = bpy.data.lights.new("Lookdev_SunData", "SUN")
    sun_data.energy = 1.1
    sun_data.color = (1.0, 0.56, 0.28)
    sun = bpy.data.objects.new("Lookdev_Sun", sun_data)
    target.objects.link(sun)
    sun.rotation_euler = (math.radians(48), 0.0, math.radians(-34))


def deduplicate_meshes(root_name: str) -> None:
    """Link byte-equivalent repeated details to one mesh datablock.

    This keeps every stable object transform/node while ensuring beams, mullions,
    sconces, trim and other repeated modules do not duplicate geometry.
    """
    canonical: dict[tuple, bpy.types.Mesh] = {}
    for obj in [candidate for candidate in descendants(bpy.data.objects[root_name]) if candidate.type == "MESH"]:
        mesh = obj.data
        uv = mesh.uv_layers.active
        signature = (
            tuple(slot.material.name if slot.material else "" for slot in obj.material_slots),
            tuple(tuple(round(value, 6) for value in vertex.co) for vertex in mesh.vertices),
            tuple(tuple(polygon.vertices) for polygon in mesh.polygons),
            tuple((round(loop.uv.x, 6), round(loop.uv.y, 6)) for loop in uv.data) if uv else (),
        )
        if signature in canonical:
            obj.data = canonical[signature]
        else:
            canonical[signature] = mesh


def build_scene(root: Path) -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE"
    scene["assetId"] = "dream_manor_v1"
    scene["axisConvention"] = "X east, Godot Y up, Z north"
    scene["foyerSizeMeters"] = FOYER_SIZE
    scene["hallSizeMeters"] = HALL_SIZE
    scene["wallThicknessMeters"] = WALL_THICKNESS
    scene["currentRoomDoorCount"] = 4
    scene["lockedExteriorDoorCount"] = 1
    scene["reservedExpansionModuleCount"] = 2
    collections = {name: new_collection(name) for name in sorted(COLLECTIONS)}
    mats = make_materials(root)
    build_hall(root, collections, mats)
    build_exterior(collections, mats)
    deduplicate_meshes("DreamManorHall")
    deduplicate_meshes("DreamManorExterior")
    add_lookdev(collections)
    world = bpy.data.worlds.new("DreamManorWorld")
    world.color = (0.018, 0.028, 0.035)
    bpy.context.scene.world = world
    bpy.context.view_layer.update()


def descendants(root: bpy.types.Object) -> list[bpy.types.Object]:
    result = [root]
    for child in root.children:
        result.extend(descendants(child))
    return result


def export_root(root_name: str, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    root = bpy.data.objects[root_name]
    for obj in descendants(root):
        obj.hide_set(False)
        obj.hide_render = False
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    result = bpy.ops.export_scene.gltf(
        filepath=str(path),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_animations=False,
        export_cameras=False,
        export_lights=False,
        export_materials="EXPORT",
        export_yup=True,
    )
    if "FINISHED" not in result:
        raise RuntimeError(f"GLB export failed for {root_name}: {result}")


def mesh_summary(root_name: str) -> dict:
    root = bpy.data.objects[root_name]
    objects = descendants(root)
    meshes = [obj for obj in objects if obj.type == "MESH"]
    triangles = 0
    for obj in meshes:
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
    materials = sorted({slot.material.name for obj in meshes for slot in obj.material_slots if slot.material})
    return {
        "root": root_name,
        "objectCount": len(objects),
        "meshObjectCount": len(meshes),
        "uniqueMeshCount": len({obj.data.as_pointer() for obj in meshes}),
        "sharedMeshDataBlockCount": len({obj.data.as_pointer() for obj in meshes if obj.data.users > 1}),
        "triangleCount": triangles,
        "materialCount": len(materials),
        "materials": materials,
    }


def validate_scene(root: Path) -> dict:
    if bpy.app.version[:3] != BLENDER_VERSION:
        raise RuntimeError(f"Expected Blender {BLENDER_VERSION}, got {bpy.app.version[:3]}")
    if bpy.context.scene.unit_settings.system != "METRIC" or abs(bpy.context.scene.unit_settings.scale_length - 1.0) > 1e-6:
        raise RuntimeError("Dream Manor source must use meter units")
    missing_collections = sorted(COLLECTIONS - set(bpy.data.collections.keys()))
    if missing_collections:
        raise RuntimeError(f"Missing collections: {', '.join(missing_collections)}")
    names = set(bpy.data.objects.keys())
    missing_hall = sorted(REQUIRED_HALL_OBJECTS - names)
    missing_exterior = sorted(REQUIRED_EXTERIOR_OBJECTS - names)
    if missing_hall or missing_exterior:
        raise RuntimeError(f"Missing required objects: hall={missing_hall}, exterior={missing_exterior}")
    if bpy.context.scene.get("currentRoomDoorCount") != 4:
        raise RuntimeError("Dream Manor must expose exactly four current room doors")
    if bpy.context.scene.get("lockedExteriorDoorCount") != 1:
        raise RuntimeError("Dream Manor must contain one locked exterior door")
    if bpy.context.scene.get("reservedExpansionModuleCount") != 2:
        raise RuntimeError("Dream Manor must keep exactly two solid expansion modules")
    for name, angle in DOOR_OPEN_ANGLES.items():
        hinge = bpy.data.objects.get(name)
        if hinge is None or hinge.type != "EMPTY":
            raise RuntimeError(f"Door hinge is missing or not an Empty: {name}")
        if any(abs(value) > 1e-6 for value in hinge.rotation_euler):
            raise RuntimeError(f"Door hinge must be closed at local rotation zero: {name}")
        hinge["openAngleGodotYDegrees"] = angle
    for filename in TEXTURE_FILES:
        if not (root / TEXTURE_RELATIVE / filename).is_file():
            raise RuntimeError(f"Missing Dream Manor texture: {filename}")
    texture_manifest_path = root / TEXTURE_MANIFEST_RELATIVE
    if not texture_manifest_path.is_file():
        raise RuntimeError("Dream Manor texture manifest is missing")
    missing_external = []
    for image in bpy.data.images:
        if image.source == "FILE" and image.filepath:
            resolved = Path(bpy.path.abspath(image.filepath))
            if not resolved.is_file():
                missing_external.append(str(resolved))
    if missing_external:
        raise RuntimeError(f"Missing external files: {missing_external}")
    hall = mesh_summary("DreamManorHall")
    exterior = mesh_summary("DreamManorExterior")
    if hall["triangleCount"] > 120_000 or hall["meshObjectCount"] > 160 or hall["uniqueMeshCount"] > 80 or hall["materialCount"] > 18:
        raise RuntimeError(f"Hall budget exceeded: {hall}")
    if exterior["triangleCount"] > 80_000:
        raise RuntimeError(f"Exterior triangle budget exceeded: {exterior['triangleCount']}")
    return {
        "schemaVersion": 1,
        "blenderVersion": bpy.app.version_string,
        "axisConvention": "X east, Godot Y up, Z north",
        "foyerSizeMeters": list(FOYER_SIZE),
        "hallSizeMeters": list(HALL_SIZE),
        "wallThicknessMeters": WALL_THICKNESS,
        "currentRoomDoors": ["Door_Room1_Root", "Door_Room2_Root", "Door_Room3_Root", "Door_Room4_Root"],
        "lockedExteriorDoor": "Door_Front_Root",
        "reservedExpansionModules": ["ReservedModule_A", "ReservedModule_B"],
        "doorOpenAnglesDegrees": DOOR_OPEN_ANGLES,
        "requiredHallObjects": sorted(REQUIRED_HALL_OBJECTS),
        "requiredExteriorObjects": sorted(REQUIRED_EXTERIOR_OBJECTS),
        "hall": hall,
        "exterior": exterior,
        "textureFiles": {name: sha256(root / TEXTURE_RELATIVE / name) for name in TEXTURE_FILES},
    }


def write_manifest(root: Path, summary: dict) -> dict:
    source = root / SOURCE_RELATIVE
    hall = root / HALL_GLB_RELATIVE
    exterior = root / EXTERIOR_GLB_RELATIVE
    result = dict(summary)
    result.update({
        "sourceFile": SOURCE_RELATIVE.as_posix(),
        "hallRuntimeFile": HALL_GLB_RELATIVE.as_posix(),
        "exteriorReviewFile": EXTERIOR_GLB_RELATIVE.as_posix(),
        "textureManifest": TEXTURE_MANIFEST_RELATIVE.as_posix(),
        "sourceSha256": sha256(source),
        "hallRuntimeSha256": sha256(hall),
        "hallRuntimeBytes": hall.stat().st_size,
        "exteriorReviewSha256": sha256(exterior),
        "exteriorReviewBytes": exterior.stat().st_size,
        "exportProfile": {
            "format": "GLB",
            "runtimeLoadsExterior": False,
            "animations": False,
            "cameras": False,
            "lights": False,
            "appliedModifiers": True,
            "yUp": True,
        },
    })
    path = root / MANIFEST_RELATIVE
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return result


def build(root: Path) -> None:
    build_scene(root)
    source = root / SOURCE_RELATIVE
    source.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(source), check_existing=False)
    summary = validate_scene(root)
    export_root("DreamManorHall", root / HALL_GLB_RELATIVE)
    export_root("DreamManorExterior", root / EXTERIOR_GLB_RELATIVE)
    manifest = write_manifest(root, summary)
    print(f"DREAM_MANOR_BUILD_PASS hallMeshes={summary['hall']['meshObjectCount']} hallTriangles={summary['hall']['triangleCount']} "
          f"exteriorMeshes={summary['exterior']['meshObjectCount']} exteriorTriangles={summary['exterior']['triangleCount']} "
          f"hallBytes={manifest['hallRuntimeBytes']} exteriorBytes={manifest['exteriorReviewBytes']}")


def export(root: Path) -> None:
    source = root / SOURCE_RELATIVE
    if not source.is_file():
        raise RuntimeError("Dream Manor authoritative Blend is missing")
    bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False)
    summary = validate_scene(root)
    export_root("DreamManorHall", root / HALL_GLB_RELATIVE)
    export_root("DreamManorExterior", root / EXTERIOR_GLB_RELATIVE)
    manifest = write_manifest(root, summary)
    print(f"DREAM_MANOR_EXPORT_PASS hallTriangles={summary['hall']['triangleCount']} exteriorTriangles={summary['exterior']['triangleCount']} "
          f"hallBytes={manifest['hallRuntimeBytes']} exteriorBytes={manifest['exteriorReviewBytes']}")


def check(root: Path) -> None:
    source = root / SOURCE_RELATIVE
    hall = root / HALL_GLB_RELATIVE
    exterior = root / EXTERIOR_GLB_RELATIVE
    manifest_path = root / MANIFEST_RELATIVE
    for path in (source, hall, exterior, manifest_path):
        if not path.is_file():
            raise RuntimeError(f"Required Dream Manor asset is missing: {path}")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False)
    summary = validate_scene(root)
    if manifest.get("sourceSha256") != sha256(source):
        raise RuntimeError("Dream Manor source Blend hash does not match manifest")
    if manifest.get("hallRuntimeSha256") != sha256(hall) or manifest.get("exteriorReviewSha256") != sha256(exterior):
        raise RuntimeError("Dream Manor GLB hash does not match manifest")
    for key in ("foyerSizeMeters", "hallSizeMeters", "doorOpenAnglesDegrees", "requiredHallObjects",
                "requiredExteriorObjects", "hall", "exterior", "textureFiles"):
        if manifest.get(key) != summary.get(key):
            raise RuntimeError(f"Dream Manor manifest field is stale: {key}")
    with tempfile.TemporaryDirectory(prefix="town-rpg-dream-manor-") as temporary:
        temp = Path(temporary)
        hall_candidate = temp / "dream-manor-hall-v1.glb"
        exterior_candidate = temp / "dream-manor-exterior-v1.glb"
        export_root("DreamManorHall", hall_candidate)
        export_root("DreamManorExterior", exterior_candidate)
        if sha256(hall_candidate) != sha256(hall):
            raise RuntimeError("Fresh Dream Manor hall GLB export is not deterministic")
        if sha256(exterior_candidate) != sha256(exterior):
            raise RuntimeError("Fresh Dream Manor exterior GLB export is not deterministic")
    print(f"DREAM_MANOR_CHECK_PASS hallMeshes={summary['hall']['meshObjectCount']} hallTriangles={summary['hall']['triangleCount']} "
          f"exteriorMeshes={summary['exterior']['meshObjectCount']} exteriorTriangles={summary['exterior']['triangleCount']} "
          f"doors=4 reserved=2 readOnly=true")


def point_camera(camera: bpy.types.Object, target: tuple[float, float, float]) -> None:
    direction = Vector(logical_position(target)) - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def capture(root: Path, output_argument: str | None) -> None:
    """Render three deterministic 1080p exterior review angles without saving the Blend."""
    source = root / SOURCE_RELATIVE
    if not source.is_file():
        raise RuntimeError("Dream Manor authoritative Blend is missing")
    bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False)
    validate_scene(root)

    output = Path(output_argument).resolve() if output_argument else root / "outputs/dream-manor-exterior-1080p"
    output.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.render.resolution_x = 1920
    scene.render.resolution_y = 1080
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.render.engine = "BLENDER_EEVEE"

    for obj in descendants(bpy.data.objects["DreamManorHall"]):
        obj.hide_render = True
    for obj in descendants(bpy.data.objects["DreamManorExterior"]):
        obj.hide_render = False
    for obj in bpy.data.objects:
        if obj.type in {"LIGHT", "CAMERA"}:
            obj.hide_render = True

    world = scene.world or bpy.data.worlds.new("DreamManorCaptureWorld")
    scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.025, 0.045, 0.060, 1.0)
    background.inputs["Strength"].default_value = 0.48

    ground_material = bpy.data.materials.new("M_Manor_CaptureGround")
    ground_material.diffuse_color = (0.055, 0.075, 0.060, 1.0)
    ground_material.roughness = 0.92
    bpy.ops.mesh.primitive_plane_add(size=90.0, location=(0.0, 0.0, -0.035))
    ground = bpy.context.object
    ground.name = "ReviewOnly_Ground"
    ground.data.materials.append(ground_material)

    sun_data = bpy.data.lights.new("ReviewOnly_SunData", "SUN")
    sun_data.energy = 2.1
    sun_data.color = (1.0, 0.62, 0.36)
    sun = bpy.data.objects.new("ReviewOnly_Sun", sun_data)
    scene.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(42), 0.0, math.radians(-32))

    fill_data = bpy.data.lights.new("ReviewOnly_FillData", "AREA")
    fill_data.energy = 2200.0
    fill_data.color = (0.28, 0.43, 0.56)
    fill_data.shape = "DISK"
    fill_data.size = 14.0
    fill = bpy.data.objects.new("ReviewOnly_Fill", fill_data)
    scene.collection.objects.link(fill)
    fill.location = logical_position((-10.0, 14.0, 2.0))
    fill.rotation_euler = (math.radians(18), 0.0, math.radians(-70))

    camera_data = bpy.data.cameras.new("ReviewOnly_ExteriorCameraData")
    camera_data.lens = 50.0
    camera_data.sensor_width = 36.0
    camera_data.clip_start = 0.10
    camera_data.clip_end = 180.0
    camera = bpy.data.objects.new("ReviewOnly_ExteriorCamera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera

    captures = (
        ("01-front-southeast.png", (19.0, 6.8, -24.0), (0.0, 2.0, 5.0)),
        ("02-front-southwest.png", (-19.0, 6.8, -23.0), (0.0, 2.0, 5.5)),
        ("03-rear-northeast.png", (18.0, 7.4, 32.0), (0.0, 2.2, 11.0)),
    )
    for filename, position, target in captures:
        camera.location = logical_position(position)
        point_camera(camera, target)
        fill.location = logical_position(position)
        point_camera(fill, target)
        scene.render.filepath = str(output / filename)
        bpy.ops.render.render(write_still=True)

    print(f"DREAM_MANOR_CAPTURE_PASS files={len(captures)} size=1920x1080 directory={output.as_posix()}")


def main() -> None:
    args = parse_args()
    root = Path(args.root).resolve()
    mode = args.mode.removeprefix("manor-")
    if mode == "build":
        build(root)
    elif mode == "export":
        export(root)
    elif mode == "check":
        check(root)
    else:
        capture(root, args.output)


if __name__ == "__main__":
    main()
