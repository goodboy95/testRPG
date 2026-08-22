#!/usr/bin/env python3
"""Build and validate the protagonist's unbound 3D technical modeling sample.

Texture modes run with the repository Python runtime. Blender modes run with
Blender 5.2 after ``--``.  The tracked Blend contains an A-pose reference and a
baked natural stance, but deliberately contains no armature or animation.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import random
import shutil
import sys
import tempfile
import zipfile
from pathlib import Path
from typing import Any, Iterable


OFFICIAL_URL = (
    "https://download.blender.org/demo/asset-bundles/human-base-meshes/"
    "human-base-meshes-bundle-v1.4.1.zip"
)
OFFICIAL_ZIP_SHA256 = "811f43accbb31a88266d932f8f5563b2d13586fca0ba2693aad1f5fe582b3515"
OFFICIAL_COLLECTION = "Body Male - Realistic"
OFFICIAL_BODY = "GEO-body_male_realistic"
OFFICIAL_EYES = (
    "GEO-body_male_realistic.eye.L",
    "GEO-body_male_realistic.eye.R",
)

REFERENCE_RELATIVE = Path("art/concepts/characters/protagonist-3d-turnaround-v1.png")
PIXEL_REFERENCE_RELATIVE = Path(
    "assets/generated/dream_escape/pixel/protagonist-walk-4x4-v1.png"
)
WORK_SOURCE_RELATIVE = Path(
    "work/character_sources/human-base-meshes-v1.4.1/expanded/"
    "human-base-meshes-bundle-v1.4.1/human_base_meshes_bundle.blend"
)
WORK_ZIP_RELATIVE = Path(
    "work/character_sources/human-base-meshes-v1.4.1/"
    "human-base-meshes-bundle-v1.4.1.zip"
)
CHARACTER_DIR_RELATIVE = Path("art/blender/characters")
ADOPTED_BASE_RELATIVE = CHARACTER_DIR_RELATIVE / "source-v1/blender-human-base-male-realistic-v1.4.1.blend"
TEXTURE_DIR_RELATIVE = CHARACTER_DIR_RELATIVE / "textures-v1"
BLEND_RELATIVE = CHARACTER_DIR_RELATIVE / "protagonist-static-v1.blend"
GLB_RELATIVE = CHARACTER_DIR_RELATIVE / "exports/protagonist-static-v1.glb"
MANIFEST_RELATIVE = CHARACTER_DIR_RELATIVE / "protagonist-static-v1-manifest.json"
OUTPUT_RELATIVE = Path("outputs/protagonist-static-v1")

TEXTURE_SIZE = 1024
TEXTURE_NAMES = (
    "protagonist-static-v1-albedo.png",
    "protagonist-static-v1-normal.png",
    "protagonist-static-v1-orm.png",
)
TARGET_BODY_HEIGHT = 1.68

MATERIAL_CELLS: dict[str, tuple[int, tuple[int, int, int], int, int]] = {
    # name: (atlas cell, albedo, roughness, metallic)
    "skin": (0, (184, 126, 92), 132, 0),
    "skin_warm": (1, (202, 145, 108), 126, 0),
    "hair": (2, (17, 20, 24), 190, 0),
    "shirt": (3, (105, 37, 32), 182, 0),
    "shirt_dark": (4, (70, 24, 23), 190, 0),
    "denim": (5, (17, 29, 48), 210, 0),
    "shoe_canvas": (6, (22, 24, 27), 196, 0),
    "shoe_rubber": (7, (183, 171, 142), 210, 0),
    "button": (8, (68, 48, 38), 112, 16),
    "eye": (9, (48, 31, 23), 98, 0),
    "classroom_floor": (10, (54, 55, 51), 222, 0),
    "reserved": (11, (92, 94, 90), 190, 0),
}

REQUIRED_EXPORT_OBJECTS = {
    "Protagonist_Body_Natural",
    "Protagonist_HairCap_Natural",
    "Protagonist_ShirtTorso_Natural",
    "Protagonist_PantsWaist_Natural",
    "Protagonist_Shoe_L_Natural",
    "Protagonist_Shoe_R_Natural",
}


def parse_args() -> argparse.Namespace:
    source = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else sys.argv[1:]
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "mode",
        choices=("prepare-source", "adopt-source", "texture-build", "texture-check", "build", "render", "check"),
    )
    parser.add_argument("--root", default=".")
    parser.add_argument("--source-blend", default=None)
    parser.add_argument("--skip-renders", action="store_true")
    return parser.parse_args(source)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def require_file(path: Path, label: str) -> None:
    if not path.is_file():
        raise RuntimeError(f"{label} is missing: {path}")


def prepare_source(root: Path) -> None:
    zip_path = root / WORK_ZIP_RELATIVE
    require_file(
        zip_path,
        "Official Blender Human Base Meshes v1.4.1 zip; download it from " + OFFICIAL_URL,
    )
    actual = sha256(zip_path)
    if actual != OFFICIAL_ZIP_SHA256:
        raise RuntimeError(f"Official source zip hash mismatch: {actual}")
    target = root / WORK_SOURCE_RELATIVE
    if not target.is_file():
        target.parent.parent.mkdir(parents=True, exist_ok=True)
        with zipfile.ZipFile(zip_path) as archive:
            archive.extractall(target.parents[1])
    require_file(target, "Extracted official Blender asset library")
    print(f"PROTAGONIST_SOURCE_PREPARE_PASS sha256={actual}")


def _texture_bytes() -> dict[str, bytes]:
    try:
        from PIL import Image
    except ImportError as exc:  # pragma: no cover - local production guard
        raise RuntimeError("Pillow is required for protagonist texture production") from exc

    cell_count = 4
    cell_size = TEXTURE_SIZE // cell_count
    albedo = Image.new("RGB", (TEXTURE_SIZE, TEXTURE_SIZE))
    normal = Image.new("RGB", (TEXTURE_SIZE, TEXTURE_SIZE), (128, 128, 255))
    orm = Image.new("RGB", (TEXTURE_SIZE, TEXTURE_SIZE), (255, 180, 0))
    albedo_pixels = albedo.load()
    normal_pixels = normal.load()
    orm_pixels = orm.load()
    by_cell = {value[0]: value for value in MATERIAL_CELLS.values()}

    for y in range(TEXTURE_SIZE):
        for x in range(TEXTURE_SIZE):
            cell = (y // cell_size) * cell_count + (x // cell_size)
            _, base, roughness, metallic = by_cell.get(cell, MATERIAL_CELLS["reserved"])
            local_x = x % cell_size
            local_y = y % cell_size
            seed = (x * 73856093) ^ (y * 19349663) ^ (cell * 83492791)
            noise = ((seed >> 5) % 9) - 4
            weave = 0
            if cell in {3, 4, 5, 6}:
                weave = 3 if local_x % 7 == 0 else (-2 if local_y % 9 == 0 else 0)
            skin_mottle = 0
            if cell in {0, 1}:
                skin_mottle = ((seed >> 12) % 5) - 2
            albedo_pixels[x, y] = tuple(
                max(0, min(255, channel + noise + weave + skin_mottle)) for channel in base
            )
            nx = 128 + (2 if cell in {3, 4, 5, 6} and local_x % 7 == 0 else 0)
            ny = 128 + (2 if cell in {3, 4, 5, 6} and local_y % 9 == 0 else 0)
            normal_pixels[x, y] = (nx, ny, 255)
            orm_pixels[x, y] = (246, roughness, metallic)

    result: dict[str, bytes] = {}
    for name, image in zip(TEXTURE_NAMES, (albedo, normal, orm), strict=True):
        import io

        stream = io.BytesIO()
        image.save(stream, format="PNG", optimize=False, compress_level=9)
        result[name] = stream.getvalue()
    return result


def texture_build(root: Path) -> None:
    directory = root / TEXTURE_DIR_RELATIVE
    directory.mkdir(parents=True, exist_ok=True)
    payloads = _texture_bytes()
    for name, payload in payloads.items():
        (directory / name).write_bytes(payload)
    print(f"PROTAGONIST_TEXTURE_BUILD_PASS files={len(payloads)} size={TEXTURE_SIZE}")


def texture_check(root: Path) -> None:
    payloads = _texture_bytes()
    for name, payload in payloads.items():
        path = root / TEXTURE_DIR_RELATIVE / name
        require_file(path, "Tracked protagonist texture")
        if path.read_bytes() != payload:
            raise RuntimeError(f"Protagonist texture is stale or non-deterministic: {name}")
    print(f"PROTAGONIST_TEXTURE_CHECK_PASS files={len(payloads)} size={TEXTURE_SIZE}")


try:
    import bpy  # type: ignore
    from mathutils import Matrix, Vector  # type: ignore
except ImportError:
    bpy = None
    Matrix = Vector = None


def _require_blender() -> None:
    if bpy is None:
        raise RuntimeError("This mode must run inside Blender 5.2")


def _clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in list(bpy.data.collections):
        bpy.data.collections.remove(collection)
    for material in list(bpy.data.materials):
        bpy.data.materials.remove(material)
    for image in list(bpy.data.images):
        bpy.data.images.remove(image)
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)


def _ensure_collection(name: str) -> Any:
    collection = bpy.data.collections.get(name)
    if collection is None:
        collection = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(collection)
    return collection


def _move_to_collection(obj: Any, target: Any) -> None:
    for collection in list(obj.users_collection):
        collection.objects.unlink(obj)
    target.objects.link(obj)


def _activate_only(obj: Any) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def _all_collection_objects(collection: Any) -> list[Any]:
    result: list[Any] = list(collection.objects)
    for child in collection.children:
        result.extend(_all_collection_objects(child))
    return result


def _append_official_base(source_blend: Path) -> tuple[Any, list[Any]]:
    with bpy.data.libraries.load(str(source_blend), link=False) as (available, loaded):
        if OFFICIAL_COLLECTION not in available.collections:
            raise RuntimeError(f"Official asset collection not found: {OFFICIAL_COLLECTION}")
        loaded.collections = [OFFICIAL_COLLECTION]
    collection = loaded.collections[0]
    bpy.context.scene.collection.children.link(collection)
    objects = _all_collection_objects(collection)
    names = {obj.name for obj in objects}
    if OFFICIAL_BODY not in names or not set(OFFICIAL_EYES).issubset(names):
        raise RuntimeError("Official male base collection has unexpected contents")
    return collection, objects


def adopt_source(root: Path, source_override: str | None) -> None:
    _require_blender()
    source_blend = Path(source_override).resolve() if source_override else root / WORK_SOURCE_RELATIVE
    require_file(source_blend, "Extracted Blender Human Base Meshes source")
    _clear_scene()
    collection, objects = _append_official_base(source_blend)
    keep_names = {OFFICIAL_BODY, *OFFICIAL_EYES}
    for obj in list(objects):
        if obj.name not in keep_names:
            bpy.data.objects.remove(obj, do_unlink=True)
    adopted = root / ADOPTED_BASE_RELATIVE
    adopted.parent.mkdir(parents=True, exist_ok=True)
    collection["source_url"] = OFFICIAL_URL
    collection["source_zip_sha256"] = OFFICIAL_ZIP_SHA256
    collection["source_license"] = "CC0 1.0 / Public Domain"
    collection["adopted_collection_only"] = OFFICIAL_COLLECTION
    bpy.ops.wm.save_as_mainfile(filepath=str(adopted), check_existing=False)
    print(
        "PROTAGONIST_SOURCE_ADOPT_PASS "
        f"objects={len([obj for obj in collection.all_objects if obj.type == 'MESH'])} "
        f"sha256={sha256(adopted)}"
    )


def _duplicate_mesh(source: Any, name: str, target: Any) -> Any:
    obj = source.copy()
    obj.data = source.data.copy()
    obj.name = name
    obj.data.name = f"{name}_Mesh"
    target.objects.link(obj)
    obj.matrix_world = source.matrix_world.copy()
    for modifier in list(obj.modifiers):
        obj.modifiers.remove(modifier)
    return obj


def _world_bounds(obj: Any) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    minimum = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    maximum = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    return minimum, maximum


def _bake_transform(obj: Any, transform: Matrix) -> None:
    obj.data.transform(transform @ obj.matrix_world)
    obj.matrix_world = Matrix.Identity(4)


def _normalise_base(objects: Iterable[Any], source_body: Any) -> tuple[float, float]:
    minimum, maximum = _world_bounds(source_body)
    height = maximum.z - minimum.z
    scale = TARGET_BODY_HEIGHT / height
    center_x = (minimum.x + maximum.x) * 0.5
    center_y = (minimum.y + maximum.y) * 0.5
    transform = Matrix.Translation((-center_x * scale, -center_y * scale, -minimum.z * scale)) @ Matrix.Scale(scale, 4)
    for obj in objects:
        _bake_transform(obj, transform)
    return scale, center_y


def _lower_arms_baked(obj: Any) -> None:
    # The official A-pose is retained untouched in a separate collection.  This
    # deformation only creates an unbound static natural stance for approval.
    for vertex in obj.data.vertices:
        point = vertex.co.copy()
        side = 1.0 if point.x >= 0.0 else -1.0
        shoulder = Vector((side * 0.195, 0.0, 1.405))
        outward = abs(point.x)
        if outward <= 0.17 or point.z <= 0.70:
            continue
        weight = max(0.0, min(1.0, (outward - 0.17) / 0.19))
        weight *= max(0.0, min(1.0, (1.50 - point.z) / 0.23))
        angle = math.radians(11.0 * side)
        relative = point - shoulder
        rotated = Matrix.Rotation(angle, 4, "Y") @ relative + shoulder
        vertex.co = point.lerp(rotated, weight)


def _shape_student_proportions(obj: Any, front_sign: float) -> None:
    """Soften the athletic official base into an average, slightly lean student."""
    face_sets = obj.data.attributes.get(".sculpt_face_set")
    if face_sets is None:
        raise RuntimeError("Official human base mesh lost its sculpt face-set regions")
    memberships: list[set[int]] = [set() for _ in obj.data.vertices]
    for polygon in obj.data.polygons:
        region = int(face_sets.data[polygon.index].value)
        for index in polygon.vertices:
            memberships[index].add(region)
    arm_regions = {9, 10, 11, 12, 20, 21, *range(64, 104)}
    leg_regions = {13, 14, 15, 16, 23, 24, *range(25, 64)}
    for vertex, regions in zip(obj.data.vertices, memberships, strict=True):
        point = vertex.co.copy()
        if point.z >= 1.44:
            # A flatter projection, softer jaw and restrained skull width read
            # closer to the plain young East Asian reference than the heroic
            # default head without claiming a full likeness sculpt.
            if point.z < 1.535:
                point.x *= 0.925
            elif point.z < 1.610:
                point.x *= 0.985
            else:
                point.x *= 0.965
            if point.y * front_sign > 0.0:
                point.y *= 0.935
            else:
                point.y *= 0.975
        elif regions & arm_regions:
            point.x *= 0.860
            point.y *= 0.820
        elif regions & leg_regions:
            side = -1.0 if point.x < 0.0 else 1.0
            center_x = side * 0.095
            point.x = center_x + (point.x - center_x) * 0.750
            point.y *= 0.810
        else:
            point.x *= 0.820
            point.y *= 0.840
        vertex.co = point
    obj.data.update()


def _shape_eye_to_head(obj: Any, front_sign: float) -> None:
    for vertex in obj.data.vertices:
        vertex.co.x *= 0.985
        if vertex.co.y * front_sign > 0.0:
            vertex.co.y *= 0.935
        else:
            vertex.co.y *= 0.975
    obj.data.update()


def _set_smooth(obj: Any) -> None:
    if obj.type != "MESH":
        return
    for polygon in obj.data.polygons:
        polygon.use_smooth = True


def _atlas_uv(obj: Any, material_name: str) -> None:
    if obj.type != "MESH":
        return
    cell = MATERIAL_CELLS[material_name][0]
    columns = 4
    cell_x = cell % columns
    cell_y = cell // columns
    margin = 0.035
    uv_layer = obj.data.uv_layers.active or obj.data.uv_layers.new(name="UVMap")
    if not obj.data.vertices:
        return
    xs = [vertex.co.x for vertex in obj.data.vertices]
    zs = [vertex.co.z for vertex in obj.data.vertices]
    min_x, max_x = min(xs), max(xs)
    min_z, max_z = min(zs), max(zs)
    span_x = max(max_x - min_x, 0.001)
    span_z = max(max_z - min_z, 0.001)
    for polygon in obj.data.polygons:
        for loop_index in polygon.loop_indices:
            vertex = obj.data.vertices[obj.data.loops[loop_index].vertex_index]
            u = (vertex.co.x - min_x) / span_x
            v = (vertex.co.z - min_z) / span_z
            uv_layer.data[loop_index].uv = (
                (cell_x + margin + u * (1.0 - 2.0 * margin)) / columns,
                1.0 - (cell_y + 1.0 - margin - v * (1.0 - 2.0 * margin)) / columns,
            )


def _load_materials(root: Path) -> dict[str, Any]:
    texture_directory = root / TEXTURE_DIR_RELATIVE
    paths = [texture_directory / name for name in TEXTURE_NAMES]
    for path in paths:
        require_file(path, "Protagonist texture")
    albedo = bpy.data.images.load(str(paths[0]), check_existing=True)
    normal = bpy.data.images.load(str(paths[1]), check_existing=True)
    orm = bpy.data.images.load(str(paths[2]), check_existing=True)
    albedo.colorspace_settings.name = "sRGB"
    normal.colorspace_settings.name = "Non-Color"
    orm.colorspace_settings.name = "Non-Color"
    for image, path in zip((albedo, normal, orm), paths, strict=True):
        image.name = path.stem
        image.filepath = f"//textures-v1/{path.name}"

    materials: dict[str, Any] = {}
    for key, (_, _color, roughness, metallic) in MATERIAL_CELLS.items():
        material = bpy.data.materials.new(f"Protagonist_{key}")
        material.use_nodes = True
        nodes = material.node_tree.nodes
        links = material.node_tree.links
        nodes.clear()
        output = nodes.new("ShaderNodeOutputMaterial")
        shader = nodes.new("ShaderNodeBsdfPrincipled")
        base_tex = nodes.new("ShaderNodeTexImage")
        base_tex.image = albedo
        base_tex.interpolation = "Linear"
        normal_tex = nodes.new("ShaderNodeTexImage")
        normal_tex.image = normal
        normal_tex.image.colorspace_settings.name = "Non-Color"
        normal_node = nodes.new("ShaderNodeNormalMap")
        normal_node.inputs["Strength"].default_value = 0.30 if key in {"shirt", "denim", "shoe_canvas"} else 0.15
        orm_tex = nodes.new("ShaderNodeTexImage")
        orm_tex.image = orm
        orm_tex.image.colorspace_settings.name = "Non-Color"
        separate = nodes.new("ShaderNodeSeparateColor")
        links.new(base_tex.outputs["Color"], shader.inputs["Base Color"])
        links.new(normal_tex.outputs["Color"], normal_node.inputs["Color"])
        links.new(normal_node.outputs["Normal"], shader.inputs["Normal"])
        links.new(orm_tex.outputs["Color"], separate.inputs["Color"])
        links.new(separate.outputs["Green"], shader.inputs["Roughness"])
        links.new(separate.outputs["Blue"], shader.inputs["Metallic"])
        links.new(shader.outputs["BSDF"], output.inputs["Surface"])
        shader.inputs["Roughness"].default_value = roughness / 255.0
        shader.inputs["Metallic"].default_value = metallic / 255.0
        materials[key] = material
    return materials


def _assign_material(obj: Any, material: Any, cell: str) -> None:
    obj.data.name = f"{obj.name}_Mesh"
    obj.data.materials.clear()
    obj.data.materials.append(material)
    _atlas_uv(obj, cell)


def _mesh_object(name: str, vertices: list[tuple[float, float, float]], faces: list[tuple[int, ...]], target: Any) -> Any:
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    target.objects.link(obj)
    _set_smooth(obj)
    return obj


def _elliptical_frustum(
    name: str,
    rings: list[tuple[float, float, float, float]],
    target: Any,
    segments: int = 24,
    cap_bottom: bool = True,
    cap_top: bool = False,
) -> Any:
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    for z, center_y, radius_x, radius_y in rings:
        for segment in range(segments):
            angle = (segment / segments) * math.tau
            vertices.append((math.cos(angle) * radius_x, center_y + math.sin(angle) * radius_y, z))
    for ring_index in range(len(rings) - 1):
        start = ring_index * segments
        next_start = (ring_index + 1) * segments
        for segment in range(segments):
            following = (segment + 1) % segments
            faces.append((start + segment, start + following, next_start + following, next_start + segment))
    if cap_bottom:
        faces.append(tuple(reversed(tuple(range(segments)))))
    if cap_top:
        offset = (len(rings) - 1) * segments
        faces.append(tuple(offset + index for index in range(segments)))
    return _mesh_object(name, vertices, faces, target)


def _cylinder_between(
    name: str,
    start: Vector,
    end: Vector,
    radius_start: float,
    radius_end: float,
    target: Any,
    segments: int = 16,
) -> Any:
    direction = end - start
    length = direction.length
    midpoint = (start + end) * 0.5
    bpy.ops.mesh.primitive_cone_add(
        vertices=segments,
        radius1=radius_start,
        radius2=radius_end,
        depth=length,
        location=midpoint,
    )
    obj = bpy.context.object
    obj.name = name
    _move_to_collection(obj, target)
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = Vector((0.0, 0.0, 1.0)).rotation_difference(direction.normalized())
    obj.rotation_mode = "XYZ"
    _activate_only(obj)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    _set_smooth(obj)
    return obj


def _rounded_box(name: str, location: tuple[float, float, float], scale: tuple[float, float, float], target: Any, bevel: float = 0.02) -> Any:
    bpy.ops.mesh.primitive_cube_add(location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    _move_to_collection(obj, target)
    _activate_only(obj)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    modifier = obj.modifiers.new("SoftEdges", "BEVEL")
    modifier.width = bevel
    modifier.segments = 2
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    _set_smooth(obj)
    return obj


def _finish_shell(obj: Any, thickness: float = 0.004, bevel: float = 0.0015) -> Any:
    solidify = obj.modifiers.new("GarmentThickness", "SOLIDIFY")
    solidify.thickness = thickness
    solidify.offset = -1.0
    solidify.use_even_offset = False
    _activate_only(obj)
    bpy.ops.object.modifier_apply(modifier=solidify.name)
    if bevel > 0.0:
        edge_softness = obj.modifiers.new("GarmentEdgeSoftness", "BEVEL")
        edge_softness.width = bevel
        edge_softness.segments = 2
        _activate_only(obj)
        bpy.ops.object.modifier_apply(modifier=edge_softness.name)
    _set_smooth(obj)
    return obj


def _sleeve_shell(name: str, body: Any, side: float, target: Any, cuff: bool = False) -> Any:
    expected = 20 if side < 0.0 else 21
    centers = [
        _polygon_center(body, polygon)
        for polygon in body.data.polygons
        if _face_set(body, polygon) == expected
    ]
    shoulder = Vector((side * 0.145, 0.0, 1.355))
    centroid = sum(centers, Vector()) / max(1, len(centers))
    direction = centroid - shoulder
    direction.y *= 0.35
    if direction.length < 0.05:
        direction = Vector((side, 0.0, -0.5))
    direction.normalize()
    if direction.x * side < 0.15:
        direction.x = side * max(0.25, abs(direction.x))
        direction.normalize()
    across = Vector((0.0, 1.0, 0.0))
    around = direction.cross(across).normalized()
    start = shoulder - direction * 0.015
    length = 0.225
    ring_specs = (
        (0.00, 0.091, 0.086),
        (0.20, 0.094, 0.089),
        (0.72, 0.083, 0.080),
        (1.00, 0.076, 0.074),
    )
    if cuff:
        ring_specs = ((0.88, 0.078, 0.076), (1.02, 0.078, 0.076))
    segments = 24
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    for ring_index, (fraction, radius_y, radius_side) in enumerate(ring_specs):
        center = start + direction * (length * fraction)
        for segment in range(segments):
            angle = segment / segments * math.tau
            subtle = 1.0 + 0.012 * math.sin(angle * 3.0 + ring_index * 1.7)
            point = center + across * (math.cos(angle) * radius_y * subtle) + around * (
                math.sin(angle) * radius_side * subtle
            )
            vertices.append(tuple(point))
    for ring_index in range(len(ring_specs) - 1):
        offset = ring_index * segments
        following = (ring_index + 1) * segments
        for segment in range(segments):
            next_segment = (segment + 1) % segments
            faces.append((offset + segment, offset + next_segment, following + next_segment, following + segment))
    obj = _mesh_object(name, vertices, faces, target)
    return _finish_shell(obj, thickness=0.0035 if cuff else 0.004, bevel=0.001)


def _closed_canvas_shoe(name: str, side: float, target: Any) -> Any:
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=32,
        ring_count=16,
        radius=1.0,
        location=(side * 0.100, 0.018, 0.076),
    )
    shoe = bpy.context.object
    shoe.name = name
    shoe.scale = (0.076, 0.142, 0.068)
    _move_to_collection(shoe, target)
    _activate_only(shoe)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    for vertex in shoe.data.vertices:
        if vertex.co.z < 0.0:
            vertex.co.z *= 0.55
        if vertex.co.y < 0.0:
            toe = min(1.0, abs(vertex.co.y) / 0.142)
            vertex.co.z *= 1.0 - 0.13 * toe
    shoe.data.update()
    smooth = shoe.modifiers.new("CanvasRelax", "SMOOTH")
    smooth.factor = 0.18
    smooth.iterations = 2
    _activate_only(shoe)
    bpy.ops.object.modifier_apply(modifier=smooth.name)
    _set_smooth(shoe)
    return shoe


def _collar_patch(name: str, points: list[tuple[float, float, float]], target: Any) -> Any:
    obj = _mesh_object(name, points, [tuple(range(len(points)))], target)
    return _finish_shell(obj, thickness=0.0025, bevel=0.0012)


def _hair_cap(name: str, front_sign: float, target: Any) -> Any:
    center = Vector((0.0, 0.005, 1.570))
    rings = 10
    segments = 32
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    for ring in range(rings + 1):
        fraction = ring / rings
        for segment in range(segments):
            phi = (segment / segments) * math.tau
            toward_front = max(0.0, math.sin(phi) * front_sign)
            toward_back = max(0.0, -math.sin(phi) * front_sign)
            max_theta = math.radians(68.0 + toward_back * 40.0 - toward_front * 3.0)
            theta = fraction * max_theta
            irregular = 1.0 + 0.025 * math.sin(phi * 5.0) + 0.015 * math.cos(phi * 9.0)
            x = center.x + 0.112 * irregular * math.sin(theta) * math.cos(phi)
            y = center.y + 0.094 * irregular * math.sin(theta) * math.sin(phi)
            z = center.z + 0.128 * math.cos(theta)
            vertices.append((x, y, z))
    for ring in range(rings):
        start = ring * segments
        next_start = (ring + 1) * segments
        for segment in range(segments):
            following = (segment + 1) % segments
            faces.append((start + segment, start + following, next_start + following, next_start + segment))
    obj = _mesh_object(name, vertices, faces, target)
    solidify = obj.modifiers.new("HairThickness", "SOLIDIFY")
    solidify.thickness = 0.008
    _activate_only(obj)
    bpy.ops.object.modifier_apply(modifier=solidify.name)
    return obj


def _add_hair_clumps(prefix: str, front_sign: float, target: Any, materials: dict[str, Any]) -> list[Any]:
    clumps: list[Any] = []
    # These are deliberately overlapping, low-profile locks rather than the
    # bead-like fringe used by the first rough sample.  The copied scalp below
    # supplies the continuous silhouette; the locks only break up its edge.
    layout = (
        (-0.036, 0.060, 1.651, 0.018, 0.012, 0.009, -11.0),
        (0.000, 0.065, 1.657, 0.020, 0.012, 0.010, 0.0),
        (0.037, 0.058, 1.650, 0.018, 0.012, 0.009, 12.0),
        (-0.018, 0.008, 1.682, 0.022, 0.018, 0.009, -6.0),
        (0.022, 0.010, 1.680, 0.022, 0.018, 0.009, 7.0),
    )
    for index, (x, front_depth, z, sx, sy, sz, roll) in enumerate(layout):
        bpy.ops.mesh.primitive_ico_sphere_add(
            subdivisions=2,
            radius=1.0,
            location=(x, front_sign * front_depth, z),
        )
        obj = bpy.context.object
        obj.name = f"{prefix}_HairClump_{index + 1:02d}"
        obj.scale = (sx, sy, sz)
        obj.rotation_euler = (
            math.radians(8.0 * front_sign),
            math.radians(roll),
            math.radians(roll * 0.35),
        )
        _move_to_collection(obj, target)
        _activate_only(obj)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        _set_smooth(obj)
        _assign_material(obj, materials["hair"], "hair")
        clumps.append(obj)
    return clumps


def _add_eyebrows(suffix: str, front_sign: float, target: Any, materials: dict[str, Any]) -> list[Any]:
    eyebrows: list[Any] = []
    for side, label in ((-1.0, "L"), (1.0, "R")):
        eyebrow = _rounded_box(
            f"Protagonist_Eyebrow_{label}_{suffix}",
            (side * 0.033, front_sign * 0.108, 1.603),
            (0.024, 0.0018, 0.0028),
            target,
            bevel=0.0018,
        )
        eyebrow.rotation_euler.y = math.radians(side * 4.0)
        _assign_material(eyebrow, materials["hair"], "hair")
        eyebrows.append(eyebrow)
    return eyebrows


def _polygon_center(source: Any, polygon: Any) -> Vector:
    return sum((source.data.vertices[index].co for index in polygon.vertices), Vector()) / len(polygon.vertices)


def _face_set(source: Any, polygon: Any) -> int:
    attribute = source.data.attributes.get(".sculpt_face_set")
    if attribute is None or attribute.domain != "FACE":
        raise RuntimeError("Official human base mesh lost its sculpt face-set regions")
    return int(attribute.data[polygon.index].value)


def _surface_copy(
    source: Any,
    name: str,
    target: Any,
    keep_face: Any,
    normal_offset: float,
    fold_strength: float = 0.0,
    thickness: float = 0.003,
    smooth_factor: float = 0.0,
    smooth_iterations: int = 0,
    decimate_ratio: float = 1.0,
    shape_vertex: Any = None,
) -> Any:
    selected = [
        polygon
        for polygon in source.data.polygons
        if keep_face(_face_set(source, polygon), _polygon_center(source, polygon))
    ]
    if not selected:
        raise RuntimeError(f"Tailored surface selection is empty: {name}")
    source_indices = sorted({index for polygon in selected for index in polygon.vertices})
    remap = {source_index: index for index, source_index in enumerate(source_indices)}
    vertices: list[tuple[float, float, float]] = []
    for source_index in source_indices:
        vertex = source.data.vertices[source_index]
        phase = vertex.co.z * 39.0 + vertex.co.x * 21.0 + vertex.co.y * 17.0
        offset = normal_offset + fold_strength * math.sin(phase)
        position = vertex.co + vertex.normal * offset
        if shape_vertex is not None:
            position = shape_vertex(position)
        vertices.append(tuple(position))
    faces = [tuple(remap[index] for index in polygon.vertices) for polygon in selected]
    obj = _mesh_object(name, vertices, faces, target)
    if smooth_iterations > 0 and smooth_factor > 0.0:
        smooth = obj.modifiers.new("ClothRelax", "SMOOTH")
        smooth.factor = smooth_factor
        smooth.iterations = smooth_iterations
        _activate_only(obj)
        bpy.ops.object.modifier_apply(modifier=smooth.name)
    if thickness > 0.0:
        solidify = obj.modifiers.new("ClothThickness", "SOLIDIFY")
        solidify.thickness = thickness
        solidify.offset = -1.0
        # Even-offset can produce metre-long spikes on the intentionally open
        # garment boundaries inherited from the human base topology.
        solidify.use_even_offset = False
        _activate_only(obj)
        bpy.ops.object.modifier_apply(modifier=solidify.name)
    if decimate_ratio < 1.0:
        decimate = obj.modifiers.new("StaticBudget", "DECIMATE")
        decimate.decimate_type = "COLLAPSE"
        decimate.ratio = decimate_ratio
        _activate_only(obj)
        bpy.ops.object.modifier_apply(modifier=decimate.name)
    _set_smooth(obj)
    return obj


def _front_patch(
    name: str,
    points: list[tuple[float, float]],
    front_y: float,
    target: Any,
    thickness: float = 0.0025,
) -> Any:
    vertices = [(x, front_y, z) for x, z in points]
    obj = _mesh_object(name, vertices, [tuple(range(len(vertices)))], target)
    solidify = obj.modifiers.new("ClothPatchThickness", "SOLIDIFY")
    solidify.thickness = thickness
    solidify.offset = 0.0
    _activate_only(obj)
    bpy.ops.object.modifier_apply(modifier=solidify.name)
    edge_softness = obj.modifiers.new("PatchEdgeSoftness", "BEVEL")
    edge_softness.width = min(0.0015, thickness * 0.45)
    edge_softness.segments = 2
    _activate_only(obj)
    bpy.ops.object.modifier_apply(modifier=edge_softness.name)
    _set_smooth(obj)
    return obj


def _shirt_torso_face(face_set: int, center: Vector) -> bool:
    return (
        (face_set == 1 and center.z <= 1.455)
        or face_set == 19
        or (face_set == 18 and center.z >= 0.815)
    )


def _shirt_sleeve_face(face_set: int, center: Vector, side: float) -> bool:
    expected = 20 if side < 0.0 else 21
    shoulder = Vector((side * 0.125, 0.0, 1.355))
    return face_set == expected and (Vector((center.x, 0.0, center.z)) - shoulder).length <= 0.205


def _shirt_cuff_face(face_set: int, center: Vector, side: float) -> bool:
    expected = 20 if side < 0.0 else 21
    shoulder = Vector((side * 0.125, 0.0, 1.355))
    distance = (Vector((center.x, 0.0, center.z)) - shoulder).length
    return face_set == expected and 0.175 <= distance <= 0.220


def _pants_face(face_set: int, center: Vector) -> bool:
    return (
        (face_set == 19 and center.z <= 0.945)
        or (face_set == 18 and center.z <= 0.950)
        or (face_set in {23, 24, 15, 16} and center.z >= 0.085)
    )


FOOT_FACE_SETS = {13, 14, *range(25, 64)}


def _shoe_face(face_set: int, center: Vector, side: float) -> bool:
    return face_set in FOOT_FACE_SETS and center.x * side > 0.0


def _hair_face(face_set: int, center: Vector, front_sign: float) -> bool:
    if face_set != 17:
        return False
    frontness = center.y * front_sign
    if frontness > 0.005:
        temple = min(1.0, abs(center.x) / 0.082)
        irregular = 0.004 * math.sin(center.x * 92.0)
        return center.z >= 1.598 + 0.018 * temple + irregular
    return center.z >= 1.515


def _loosen_shirt(position: Vector) -> Vector:
    result = position.copy()
    lower = max(0.0, min(1.0, (1.20 - position.z) / 0.42))
    result.x *= 1.060 + 0.055 * lower
    if abs(position.y) >= 0.040:
        cloth_depth = 0.126 + 0.004 * math.sin(position.z * 25.0 + position.x * 19.0)
        result.y = math.copysign(abs(position.y) * 0.25 + cloth_depth * 0.75, position.y)
    else:
        result.y *= 1.08
    return result


def _straighten_trouser_leg(position: Vector) -> Vector:
    if position.z >= 0.72:
        return position
    side = -1.0 if position.x < 0.0 else 1.0
    center_x = side * 0.095
    weight = max(0.0, min(1.0, (0.72 - position.z) / 0.64))
    result = position.copy()
    result.x = center_x + (position.x - center_x) * 0.84 * (1.0 + 0.38 * weight)
    result.y *= 1.0 + 0.22 * weight
    return result


def _visible_skin_face(face_set: int, center: Vector, front_sign: float) -> bool:
    covered = (
        _shirt_torso_face(face_set, center)
        or _shirt_sleeve_face(face_set, center, -1.0)
        or _shirt_sleeve_face(face_set, center, 1.0)
        or _shirt_cuff_face(face_set, center, -1.0)
        or _shirt_cuff_face(face_set, center, 1.0)
        or _pants_face(face_set, center)
        or _shoe_face(face_set, center, -1.0)
        or _shoe_face(face_set, center, 1.0)
        or _hair_face(face_set, center, front_sign)
    )
    return not covered


def _create_clothes(
    suffix: str,
    body: Any,
    front_sign: float,
    target: Any,
    materials: dict[str, Any],
) -> list[Any]:
    objects: list[Any] = []
    # The approval candidate uses independent cloth shells.  Copying the body
    # surface made the previous iteration read like painted-on muscle anatomy.
    shirt = _elliptical_frustum(
        f"Protagonist_ShirtTorso_{suffix}",
        [
            (0.795, 0.004, 0.188, 0.132),
            (0.835, 0.003, 0.193, 0.134),
            (0.945, 0.001, 0.190, 0.136),
            (1.070, 0.000, 0.185, 0.138),
            (1.195, 0.000, 0.187, 0.141),
            (1.300, 0.000, 0.194, 0.143),
            (1.365, 0.000, 0.204, 0.140),
            (1.425, 0.000, 0.078, 0.074),
        ],
        target,
        segments=32,
        cap_bottom=False,
        cap_top=False,
    )
    for vertex in shirt.data.vertices:
        if vertex.co.z <= 1.25:
            vertex.co.x += 0.0018 * math.sin(vertex.co.z * 34.0 + vertex.co.y * 28.0)
            vertex.co.y += 0.0023 * math.sin(vertex.co.z * 29.0 + vertex.co.x * 21.0)
    shirt.data.update()
    _finish_shell(shirt, thickness=0.004, bevel=0.0012)
    objects.append(shirt)
    _assign_material(shirt, materials["shirt"], "shirt")

    for side, label in ((-1.0, "L"), (1.0, "R")):
        sleeve = _sleeve_shell(f"Protagonist_ShirtSleeve_{label}_{suffix}", body, side, target)
        objects.append(sleeve)
        _assign_material(sleeve, materials["shirt"], "shirt")
        cuff = _sleeve_shell(f"Protagonist_ShirtCuff_{label}_{suffix}", body, side, target, cuff=True)
        objects.append(cuff)
        _assign_material(cuff, materials["shirt"], "shirt")

    front_y = front_sign * 0.144
    placket = _front_patch(
        f"Protagonist_ShirtPlacket_{suffix}",
        [(-0.005, 0.820), (0.005, 0.820), (0.005, 1.392), (-0.005, 1.392)],
        front_y,
        target,
        thickness=0.0016,
    )
    objects.append(placket)
    _assign_material(placket, materials["shirt"], "shirt")
    pocket = _front_patch(
        f"Protagonist_ShirtPocket_{suffix}",
        [(-0.137, 1.278), (-0.072, 1.278), (-0.074, 1.203), (-0.104, 1.188), (-0.134, 1.203)],
        front_y + front_sign * 0.001,
        target,
        thickness=0.0014,
    )
    objects.append(pocket)
    _assign_material(pocket, materials["shirt"], "shirt")
    collar_shapes = {
        "L": [
            (-0.004, front_sign * 0.075, 1.425),
            (-0.071, front_sign * 0.091, 1.405),
            (-0.034, front_sign * 0.126, 1.365),
            (-0.002, front_sign * 0.111, 1.392),
        ],
        "R": [
            (0.004, front_sign * 0.075, 1.425),
            (0.071, front_sign * 0.091, 1.405),
            (0.034, front_sign * 0.126, 1.365),
            (0.002, front_sign * 0.111, 1.392),
        ],
    }
    for label, points in collar_shapes.items():
        collar = _collar_patch(f"Protagonist_ShirtCollar_{label}_{suffix}", points, target)
        objects.append(collar)
        _assign_material(collar, materials["shirt"], "shirt")
    for index, z in enumerate((0.91, 1.02, 1.13, 1.24, 1.35)):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=6, radius=0.0055, location=(0.0, front_y + front_sign * 0.003, z))
        button = bpy.context.object
        button.name = f"Protagonist_ShirtButton_{index + 1:02d}_{suffix}"
        button.scale = (1.0, 0.35, 1.0)
        _move_to_collection(button, target)
        _activate_only(button)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        objects.append(button)
        _assign_material(button, materials["button"], "button")

    waist = _elliptical_frustum(
        f"Protagonist_PantsWaist_{suffix}",
        [(0.735, 0.004, 0.171, 0.112), (0.845, 0.002, 0.174, 0.116), (0.935, 0.0, 0.164, 0.111)],
        target,
        segments=32,
        cap_bottom=False,
        cap_top=False,
    )
    _finish_shell(waist, thickness=0.004, bevel=0.001)
    objects.append(waist)
    _assign_material(waist, materials["denim"], "denim")
    for side, label in ((-1.0, "L"), (1.0, "R")):
        leg = _elliptical_frustum(
            f"Protagonist_PantsLeg_{label}_{suffix}",
            [
                (0.075, 0.006, 0.074, 0.077),
                (0.270, 0.004, 0.076, 0.079),
                (0.500, 0.002, 0.079, 0.082),
                (0.690, 0.001, 0.086, 0.090),
                (0.815, 0.000, 0.094, 0.101),
            ],
            target,
            segments=28,
            cap_bottom=False,
            cap_top=False,
        )
        leg.location.x = side * 0.095
        _activate_only(leg)
        bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
        for vertex in leg.data.vertices:
            vertex.co.x += 0.0015 * math.sin(vertex.co.z * 37.0 + side)
            vertex.co.y += 0.0018 * math.sin(vertex.co.z * 31.0 + side * 1.4)
        leg.data.update()
        _finish_shell(leg, thickness=0.004, bevel=0.001)
        objects.append(leg)
        _assign_material(leg, materials["denim"], "denim")

        shoe = _closed_canvas_shoe(f"Protagonist_Shoe_{label}_{suffix}", side, target)
        objects.append(shoe)
        _assign_material(shoe, materials["shoe_canvas"], "shoe_canvas")
        sole = _elliptical_frustum(
            f"Protagonist_ShoeSole_{label}_{suffix}",
            [(0.012, 0.018, 0.080, 0.145), (0.038, 0.018, 0.082, 0.147)],
            target,
            segments=32,
            cap_bottom=True,
            cap_top=True,
        )
        sole.location.x = side * 0.100
        _activate_only(sole)
        bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
        objects.append(sole)
        _assign_material(sole, materials["shoe_rubber"], "shoe_rubber")
        for lace_index, (lace_y, lace_z) in enumerate(((-0.035, 0.137), (-0.004, 0.143), (0.027, 0.142))):
            lace = _rounded_box(
                f"Protagonist_ShoeLace_{label}_{lace_index + 1:02d}_{suffix}",
                (side * 0.100, lace_y, lace_z),
                (0.043, 0.0024, 0.0012),
                target,
                bevel=0.0015,
            )
            objects.append(lace)
            _assign_material(lace, materials["shoe_rubber"], "shoe_rubber")
    hair = _hair_cap(f"Protagonist_HairCap_{suffix}", front_sign, target)
    objects.append(hair)
    _assign_material(hair, materials["hair"], "hair")
    objects.extend(_add_hair_clumps(f"Protagonist_{suffix}", front_sign, target, materials))
    return objects


def _create_character_variant(
    name: str,
    natural: bool,
    source_body: Any,
    source_eyes: list[Any],
    transform: Matrix,
    front_sign: float,
    target: Any,
    materials: dict[str, Any],
) -> list[Any]:
    tailor_reference = _duplicate_mesh(source_body, f"Protagonist_TailorReference_{name}", target)
    _bake_transform(tailor_reference, transform)
    _shape_student_proportions(tailor_reference, front_sign)
    if natural:
        _lower_arms_baked(tailor_reference)
    tailor_reference.data.update()
    clothes = _create_clothes(name, tailor_reference, front_sign, target, materials)
    body = _surface_copy(
        tailor_reference,
        f"Protagonist_Body_{name}",
        target,
        lambda face_set, center: _visible_skin_face(face_set, center, front_sign),
        normal_offset=0.0,
        thickness=0.0,
    )
    _assign_material(body, materials["skin"], "skin")
    bpy.data.objects.remove(tailor_reference, do_unlink=True)
    objects = [body]
    for index, eye_source in enumerate(source_eyes):
        label = "L" if index == 0 else "R"
        eye = _duplicate_mesh(eye_source, f"Protagonist_Eye_{label}_{name}", target)
        _bake_transform(eye, transform)
        _shape_eye_to_head(eye, front_sign)
        _assign_material(eye, materials["eye"], "eye")
        objects.append(eye)
    objects.extend(_add_eyebrows(name, front_sign, target, materials))
    objects.extend(clothes)
    for obj in objects:
        obj["town_rpg_character_variant"] = name
        obj["town_rpg_static_unbound"] = True
    return objects


def _look_at(obj: Any, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def _configure_studio(lookdev: Any) -> tuple[Any, list[Any]]:
    scene = bpy.context.scene
    try:
        scene.render.engine = "BLENDER_EEVEE_NEXT"
    except TypeError:
        scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1920
    scene.render.resolution_y = 1080
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.world.color = (0.018, 0.022, 0.028)
    if hasattr(scene, "view_settings"):
        scene.view_settings.look = "AgX - Medium High Contrast"

    camera_data = bpy.data.cameras.new("ProtagonistLookdevCamera")
    camera_data.lens = 58.0
    camera = bpy.data.objects.new("ProtagonistLookdevCamera", camera_data)
    lookdev.objects.link(camera)
    scene.camera = camera
    lights: list[Any] = []
    for name, location, energy, color, size in (
        ("Key", (-2.4, -3.0, 3.4), 620.0, (1.0, 0.76, 0.58), 2.1),
        ("Fill", (2.6, -1.7, 2.4), 320.0, (0.46, 0.62, 0.82), 2.3),
        ("Rim", (0.6, 2.8, 3.0), 420.0, (0.92, 0.72, 0.52), 1.5),
    ):
        light_data = bpy.data.lights.new(f"Protagonist{name}", "AREA")
        light_data.energy = energy
        light_data.color = color
        light_data.shape = "DISK"
        light_data.size = size
        light = bpy.data.objects.new(f"Protagonist{name}", light_data)
        light.location = location
        _look_at(light, Vector((0.0, 0.0, 0.95)))
        lookdev.objects.link(light)
        lights.append(light)
    floor = _rounded_box("ProtagonistStudioFloor", (0.0, 0.0, -0.04), (2.5, 2.5, 0.04), lookdev, bevel=0.01)
    floor_material = bpy.data.materials.new("ProtagonistStudioFloorMaterial")
    floor_material.diffuse_color = (0.055, 0.065, 0.075, 1.0)
    floor.data.materials.append(floor_material)
    return camera, lights


def _set_collection_render(collection: Any, enabled: bool) -> None:
    collection.hide_render = not enabled
    collection.hide_viewport = not enabled


def _render_studio(root: Path, camera: Any, a_pose: Any, natural: Any, front_sign: float) -> None:
    output = root / OUTPUT_RELATIVE
    output.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene

    _set_collection_render(a_pose, True)
    _set_collection_render(natural, False)
    camera.location = (0.0, front_sign * 5.3, 1.00)
    _look_at(camera, Vector((0.0, 0.0, 0.88)))
    scene.render.filepath = str(output / "01-protagonist-a-pose-front-1080p.png")
    bpy.ops.render.render(write_still=True)

    _set_collection_render(a_pose, False)
    _set_collection_render(natural, True)
    camera.location = (3.0, front_sign * 4.25, 1.62)
    _look_at(camera, Vector((0.0, 0.0, 0.92)))
    scene.render.filepath = str(output / "02-protagonist-natural-stance-1080p.png")
    bpy.ops.render.render(write_still=True)


def _render_classroom_scale(root: Path, camera: Any, natural: Any, front_sign: float) -> None:
    classroom = root / "assets/generated/dream_escape/3d/classroom-art-pass-v3.glb"
    if not classroom.is_file():
        return
    scene = bpy.context.scene
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=str(classroom))
    imported = set(bpy.context.scene.objects) - before
    studio_floor = bpy.data.objects.get("ProtagonistStudioFloor")
    previous_floor_render = studio_floor.hide_render if studio_floor is not None else False
    if studio_floor is not None:
        studio_floor.hide_render = True
    character_objects = set(_all_collection_objects(natural))
    # Match a playable over-the-shoulder placement inside the 9.6 x 7.2 m v3
    # room.  The earlier camera sat outside the south wall and quite correctly
    # rendered only the wall's black back side.
    translation = Vector((1.35, 1.15, 0.0))
    for obj in character_objects:
        if obj.type == "MESH":
            obj.location += translation
    camera.location = (3.45, 2.95, 2.05)
    camera.data.lens = 43.0
    _look_at(camera, Vector((0.10, -0.75, 0.92)))
    scene.render.filepath = str(root / OUTPUT_RELATIVE / "03-protagonist-classroom-scale-1080p.png")
    bpy.ops.render.render(write_still=True)
    if studio_floor is not None:
        studio_floor.hide_render = previous_floor_render
    for obj in imported:
        bpy.data.objects.remove(obj, do_unlink=True)


def _export_glb(path: Path, collection: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in _all_collection_objects(collection):
        if obj.type == "MESH":
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
        export_tangents=False,
        export_yup=True,
    )
    if "FINISHED" not in result:
        raise RuntimeError(f"Protagonist glTF export failed: {result}")


def _triangles(objects: Iterable[Any]) -> int:
    total = 0
    for obj in objects:
        if obj.type != "MESH":
            continue
        total += sum(max(0, len(polygon.vertices) - 2) for polygon in obj.data.polygons)
    return total


def _validate_scene(root: Path) -> dict[str, Any]:
    if bpy.app.version < (5, 2, 0):
        raise RuntimeError(f"Blender 5.2 or newer is required, got {bpy.app.version_string}")
    if bpy.data.armatures or bpy.data.actions:
        raise RuntimeError("Static approval asset must not contain armatures or animation actions")
    a_pose = bpy.data.collections.get("Protagonist_APose_Reference")
    natural = bpy.data.collections.get("Protagonist_Static_Natural")
    if a_pose is None or natural is None:
        raise RuntimeError("Required protagonist collections are missing")
    natural_meshes = [obj for obj in _all_collection_objects(natural) if obj.type == "MESH"]
    names = {obj.name for obj in natural_meshes}
    missing = sorted(REQUIRED_EXPORT_OBJECTS - names)
    if missing:
        raise RuntimeError(f"Required protagonist export objects are missing: {missing}")
    triangles = _triangles(natural_meshes)
    if not 12000 <= triangles <= 45000:
        raise RuntimeError(f"Static protagonist triangle budget exceeded: {triangles}")
    minimum_z = min((obj.matrix_world @ Vector(corner)).z for obj in natural_meshes for corner in obj.bound_box)
    maximum_z = max((obj.matrix_world @ Vector(corner)).z for obj in natural_meshes for corner in obj.bound_box)
    height = maximum_z - minimum_z
    if not 1.60 <= height <= 1.80:
        raise RuntimeError(f"Static protagonist height is implausible: {height:.3f}m")
    textures = {
        name: sha256(root / TEXTURE_DIR_RELATIVE / name) for name in TEXTURE_NAMES
    }
    return {
        "blenderVersion": bpy.app.version_string,
        "assetStage": "technical-modeling-sample",
        "visualCandidate": False,
        "knownVisualLimitations": [
            "procedural garment silhouette requires an artist-authored retopology pass",
            "generic CC0 face has not received a final likeness sculpt or skin texture pass",
            "hair and canvas shoes are topology and material studies, not final assets",
        ],
        "bound": False,
        "animated": False,
        "sourceCollection": OFFICIAL_COLLECTION,
        "referenceImage": REFERENCE_RELATIVE.as_posix(),
        "referenceSha256": sha256(root / REFERENCE_RELATIVE),
        "pixelIdentityReference": PIXEL_REFERENCE_RELATIVE.as_posix(),
        "pixelIdentitySha256": sha256(root / PIXEL_REFERENCE_RELATIVE),
        "meshObjectCount": len(natural_meshes),
        "triangleCount": triangles,
        "materialCount": len({slot.material.name for obj in natural_meshes for slot in obj.material_slots if slot.material}),
        "heightMeters": round(height, 4),
        "requiredExportObjects": sorted(REQUIRED_EXPORT_OBJECTS),
        "textureFiles": textures,
    }


def build(root: Path, source_override: str | None, skip_renders: bool) -> None:
    _require_blender()
    reference = root / REFERENCE_RELATIVE
    pixel_reference = root / PIXEL_REFERENCE_RELATIVE
    source_blend = Path(source_override).resolve() if source_override else root / ADOPTED_BASE_RELATIVE
    require_file(reference, "Primary protagonist turnaround")
    require_file(pixel_reference, "Existing protagonist pixel identity reference")
    require_file(source_blend, "Extracted Blender Human Base Meshes source")
    for texture in TEXTURE_NAMES:
        require_file(root / TEXTURE_DIR_RELATIVE / texture, "Built protagonist texture")

    _clear_scene()
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    source_collection, source_objects = _append_official_base(source_blend)
    bpy.ops.object.select_all(action="DESELECT")
    source_by_name = {obj.name: obj for obj in source_objects}
    source_body = source_by_name[OFFICIAL_BODY]
    source_eyes = [source_by_name[name] for name in OFFICIAL_EYES]
    minimum, maximum = _world_bounds(source_body)
    scale = TARGET_BODY_HEIGHT / (maximum.z - minimum.z)
    center_x = (minimum.x + maximum.x) * 0.5
    center_y = (minimum.y + maximum.y) * 0.5
    transform = Matrix.Translation((-center_x * scale, -center_y * scale, -minimum.z * scale)) @ Matrix.Scale(scale, 4)
    eye_center_y = sum((_world_bounds(eye)[0].y + _world_bounds(eye)[1].y) * 0.5 for eye in source_eyes) / len(source_eyes)
    front_sign = -1.0 if eye_center_y < center_y else 1.0

    a_pose = _ensure_collection("Protagonist_APose_Reference")
    natural = _ensure_collection("Protagonist_Static_Natural")
    lookdev = _ensure_collection("Protagonist_Lookdev")
    metadata = _ensure_collection("Protagonist_Metadata")
    materials = _load_materials(root)
    _create_character_variant("APose", False, source_body, source_eyes, transform, front_sign, a_pose, materials)
    _create_character_variant("Natural", True, source_body, source_eyes, transform, front_sign, natural, materials)

    metadata_anchor = bpy.data.objects.new("Protagonist_Source_Metadata", None)
    metadata.objects.link(metadata_anchor)
    metadata_anchor["source_url"] = OFFICIAL_URL
    metadata_anchor["source_zip_sha256"] = OFFICIAL_ZIP_SHA256
    metadata_anchor["source_license"] = "CC0 1.0 / Public Domain"
    metadata_anchor["source_collection"] = OFFICIAL_COLLECTION
    metadata_anchor["primary_reference"] = REFERENCE_RELATIVE.as_posix()
    metadata_anchor["static_unbound"] = True
    metadata_anchor["front_sign"] = front_sign

    for obj in source_objects:
        bpy.data.objects.remove(obj, do_unlink=True)
    if source_collection.name in bpy.data.collections:
        bpy.data.collections.remove(source_collection)

    camera, _lights = _configure_studio(lookdev)
    _set_collection_render(a_pose, False)
    _set_collection_render(natural, True)
    blend_path = root / BLEND_RELATIVE
    blend_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)

    summary = _validate_scene(root)
    glb_path = root / GLB_RELATIVE
    _export_glb(glb_path, natural)
    summary.update(
        {
            "schemaVersion": 1,
            "sourceAuthority": "blend",
            "sourceFile": BLEND_RELATIVE.as_posix(),
            "runtimeCandidateFile": GLB_RELATIVE.as_posix(),
            "sourceSha256": sha256(blend_path),
            "glbSha256": sha256(glb_path),
            "officialSource": {
                "url": OFFICIAL_URL,
                "version": "1.4.1",
                "license": "CC0 1.0 / Public Domain",
                "downloadSha256": OFFICIAL_ZIP_SHA256,
                "adoptedCollectionOnly": OFFICIAL_COLLECTION,
                "adoptedSourceFile": ADOPTED_BASE_RELATIVE.as_posix(),
                "adoptedSourceSha256": sha256(root / ADOPTED_BASE_RELATIVE),
                "bundleTracked": False,
            },
            "exportProfile": {
                "format": "GLB",
                "animations": False,
                "armature": False,
                "cameras": False,
                "lights": False,
                "materials": "PBR texture atlas",
            },
        }
    )
    manifest = root / MANIFEST_RELATIVE
    manifest.write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    if not skip_renders:
        _render_studio(root, camera, a_pose, natural, front_sign)
        _render_classroom_scale(root, camera, natural, front_sign)
    print(
        "PROTAGONIST_STATIC_BUILD_PASS "
        f"meshes={summary['meshObjectCount']} triangles={summary['triangleCount']} "
        f"height={summary['heightMeters']} bound=false animated=false"
    )


def render_existing(root: Path) -> None:
    _require_blender()
    blend_path = root / BLEND_RELATIVE
    require_file(blend_path, "Static protagonist Blend")
    bpy.ops.wm.open_mainfile(filepath=str(blend_path), load_ui=False)
    a_pose = bpy.data.collections.get("Protagonist_APose_Reference")
    natural = bpy.data.collections.get("Protagonist_Static_Natural")
    camera = bpy.data.objects.get("ProtagonistLookdevCamera")
    metadata = bpy.data.objects.get("Protagonist_Source_Metadata")
    if a_pose is None or natural is None or camera is None:
        raise RuntimeError("Static protagonist lookdev nodes are missing")
    front_sign = float(metadata.get("front_sign", -1.0)) if metadata else -1.0
    _render_studio(root, camera, a_pose, natural, front_sign)
    _render_classroom_scale(root, camera, natural, front_sign)
    print("PROTAGONIST_STATIC_RENDER_PASS files=3 resolution=1920x1080")


def check(root: Path) -> None:
    _require_blender()
    blend_path = root / BLEND_RELATIVE
    glb_path = root / GLB_RELATIVE
    manifest_path = root / MANIFEST_RELATIVE
    for path, label in (
        (blend_path, "Static protagonist Blend"),
        (glb_path, "Static protagonist GLB"),
        (manifest_path, "Static protagonist manifest"),
    ):
        require_file(path, label)
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    bpy.ops.wm.open_mainfile(filepath=str(blend_path), load_ui=False)
    summary = _validate_scene(root)
    if manifest.get("sourceSha256") != sha256(blend_path):
        raise RuntimeError("Static protagonist Blend hash does not match manifest")
    if manifest.get("glbSha256") != sha256(glb_path):
        raise RuntimeError("Static protagonist GLB hash does not match manifest")
    for key in (
        "assetStage",
        "bound",
        "animated",
        "referenceSha256",
        "pixelIdentitySha256",
        "meshObjectCount",
        "triangleCount",
        "materialCount",
        "heightMeters",
        "requiredExportObjects",
        "textureFiles",
    ):
        if manifest.get(key) != summary.get(key):
            raise RuntimeError(f"Static protagonist manifest field is stale: {key}")
    natural = bpy.data.collections["Protagonist_Static_Natural"]
    with tempfile.TemporaryDirectory(prefix="town-rpg-protagonist-static-") as temporary:
        candidate = Path(temporary) / "protagonist-static-v1.glb"
        _export_glb(candidate, natural)
        if sha256(candidate) != sha256(glb_path):
            raise RuntimeError("Fresh static protagonist GLB export is not deterministic")
    print(
        "PROTAGONIST_STATIC_CHECK_PASS "
        f"meshes={summary['meshObjectCount']} triangles={summary['triangleCount']} "
        f"height={summary['heightMeters']} bound=false animated=false"
    )


def main() -> None:
    args = parse_args()
    root = Path(args.root).resolve()
    if args.mode == "prepare-source":
        prepare_source(root)
    elif args.mode == "adopt-source":
        adopt_source(root, args.source_blend)
    elif args.mode == "texture-build":
        texture_build(root)
    elif args.mode == "texture-check":
        texture_check(root)
    elif args.mode == "build":
        build(root, args.source_blend, args.skip_renders)
    elif args.mode == "render":
        render_existing(root)
    else:
        check(root)


if __name__ == "__main__":
    main()
