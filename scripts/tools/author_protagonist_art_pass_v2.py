#!/usr/bin/env python3
"""One-time authoring and review renders for protagonist art pass v2.

The tracked Blend is the authority. ``bootstrap`` deliberately refuses to
overwrite an existing v2 Blend; continuing art work must be performed in that
Blend. Texture generation and review rendering are separate deterministic
production helpers. All Blender modes are intended for an independent
background Blender process and never touch the interactive classroom session.
"""

from __future__ import annotations

import argparse
import hashlib
import math
import random
import sys
from pathlib import Path
from typing import Any, Iterable, Sequence


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "art/blender/characters/source-v1/blender-human-base-male-realistic-v1.4.1.blend"
BLEND = ROOT / "art/blender/characters/protagonist-art-pass-v2.blend"
TEXTURE_DIR = ROOT / "art/blender/characters/textures-v2"
OUTPUT_DIR = ROOT / "outputs/protagonist-art-pass-v2"
TEXTURES = {
    "albedo": TEXTURE_DIR / "protagonist-art-pass-v2-albedo.png",
    "normal": TEXTURE_DIR / "protagonist-art-pass-v2-normal.png",
    "orm": TEXTURE_DIR / "protagonist-art-pass-v2-orm.png",
}
TARGET_HEIGHT = 1.69
ATLAS_SIZE = 2048
CELL_COUNT = 4


def parse_args() -> argparse.Namespace:
    args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else sys.argv[1:]
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "mode",
        choices=(
            "texture-build",
            "bootstrap",
            "refine",
            "topology-fix",
            "export-mesh-fix",
            "visual-fix",
            "visual-refine",
            "visual-rebuild",
            "visual-eyes",
            "visual-polish",
            "visual-finalize",
            "visual-resolve",
            "visual-correct",
            "visual-seal",
            "visual-surface",
            "visual-garment",
            "visual-tailor",
            "visual-cuffs",
            "visual-topology-clean",
            "render",
            "report",
        ),
    )
    parser.add_argument("--root", default=str(ROOT))
    return parser.parse_args(args)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def build_textures(root: Path) -> None:
    from PIL import Image, ImageDraw, ImageFilter

    texture_dir = root / TEXTURE_DIR.relative_to(ROOT)
    texture_dir.mkdir(parents=True, exist_ok=True)
    size = ATLAS_SIZE
    cell = size // CELL_COUNT
    rng = random.Random(220826)

    palette = {
        0: ((181, 126, 94, 255), 152, "skin"),
        1: ((225, 218, 202, 255), 118, "eye"),
        2: ((18, 20, 25, 255), 184, "hair"),
        3: ((126, 48, 40, 255), 174, "shirt"),
        4: ((24, 38, 60, 255), 205, "denim"),
        5: ((24, 25, 28, 255), 192, "canvas"),
        6: ((185, 166, 128, 255), 214, "rubber"),
        7: ((88, 66, 46, 255), 112, "metal"),
        8: ((55, 25, 25, 255), 158, "lip"),
        9: ((64, 40, 27, 255), 108, "iris"),
        10: ((10, 10, 12, 255), 176, "pupil"),
    }
    default = ((90, 90, 90, 255), 190, "reserved")
    albedo = Image.new("RGBA", (size, size), (96, 96, 96, 255))
    normal = Image.new("RGBA", (size, size), (128, 128, 255, 255))
    orm = Image.new("RGBA", (size, size), (246, 190, 0, 255))
    for index in range(CELL_COUNT * CELL_COUNT):
        base, rough, kind = palette.get(index, default)
        cx = (index % CELL_COUNT) * cell
        cy = (index // CELL_COUNT) * cell
        # Keep every atlas cell calm and nearly uniform.  Earlier review textures
        # drew opaque, high-frequency circles into the atlas; planar UVs turned
        # those marks into large stains and zebra bands on curved surfaces.  The
        # low-resolution value field below gives subtle, deterministic variation
        # without becoming character-defining detail.
        low_size = 40
        values = [rng.randrange(112, 145) for _ in range(low_size * low_size)]
        noise = Image.new("L", (low_size, low_size))
        noise.putdata(values)
        noise = noise.resize((cell, cell), Image.Resampling.BICUBIC).filter(ImageFilter.GaussianBlur(3.0))
        dark = tuple(max(0, channel - 7) for channel in base[:3]) + (255,)
        light = tuple(min(255, channel + 7) for channel in base[:3]) + (255,)
        surface = Image.composite(
            Image.new("RGBA", (cell, cell), light),
            Image.new("RGBA", (cell, cell), dark),
            noise,
        )
        detail = Image.new("RGBA", (cell, cell), (0, 0, 0, 0))
        dd = ImageDraw.Draw(detail, "RGBA")
        if kind in {"shirt", "denim", "canvas"}:
            step = 14 if kind == "shirt" else 12
            for offset in range(-cell, cell * 2, step):
                dd.line((0, offset, cell, offset + cell), fill=(235, 220, 195, 7), width=1)
            for offset in range(0, cell, step + 5):
                dd.line((offset, 0, offset, cell), fill=(8, 12, 18, 5), width=1)
        elif kind == "hair":
            for _ in range(90):
                x = rng.randrange(0, cell)
                y = rng.randrange(0, cell)
                length = rng.randrange(18, 70)
                dd.line((x, y, x + rng.randrange(-6, 7), min(cell - 1, y + length)), fill=(68, 66, 72, rng.randrange(8, 18)), width=1)
        elif kind == "skin":
            for _ in range(100):
                x = rng.randrange(0, cell)
                y = rng.randrange(0, cell)
                dd.point((x, y), fill=(110, 66, 52, rng.randrange(3, 9)))
        elif kind == "rubber":
            for y in range(16, cell, 28):
                dd.line((0, y, cell, y), fill=(95, 83, 65, 10), width=1)
        surface = Image.alpha_composite(surface, detail)
        albedo.paste(surface, (cx, cy))
        orm.paste(Image.new("RGBA", (cell, cell), (246, rough, 0, 255)), (cx, cy))

    # The normal atlas intentionally stays neutral until the hand-authored UV
    # pass exists.  False woven normals were much more visible than the intended
    # semi-realistic surface response at gameplay distance.
    for key, image in (("albedo", albedo), ("normal", normal), ("orm", orm)):
        image.save(texture_dir / TEXTURES[key].name, format="PNG", optimize=False, compress_level=9)
    print(f"PROTAGONIST_V2_TEXTURE_BUILD_PASS size={size} files=3")


try:
    import bpy  # type: ignore
    import bmesh  # type: ignore
    from mathutils import Matrix, Vector  # type: ignore
except ImportError:
    bpy = bmesh = Matrix = Vector = None


def require_blender() -> None:
    if bpy is None:
        raise RuntimeError("This mode must run inside Blender 5.2")


def collection(name: str, parent: Any | None = None) -> Any:
    result = bpy.data.collections.get(name) or bpy.data.collections.new(name)
    owner = parent.children if parent else bpy.context.scene.collection.children
    if result.name not in {child.name for child in owner}:
        owner.link(result)
    return result


def move_to(obj: Any, target: Any) -> None:
    for owner in list(obj.users_collection):
        owner.objects.unlink(obj)
    target.objects.link(obj)


def select_only(objects: Sequence[Any], active: Any | None = None) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.hide_set(False)
        obj.hide_viewport = False
        obj.select_set(True)
    bpy.context.view_layer.objects.active = active or objects[0]


def join_objects(objects: Sequence[Any], name: str, target: Any) -> Any:
    objects = [obj for obj in objects if obj and obj.name in bpy.data.objects]
    select_only(objects, objects[0])
    bpy.ops.object.join()
    result = bpy.context.object
    result.name = name
    move_to(result, target)
    return result


def apply_transform(obj: Any) -> None:
    select_only([obj])
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)


def add_bevel(obj: Any, width: float, segments: int = 2) -> None:
    modifier = obj.modifiers.new("ArtistBevel", "BEVEL")
    modifier.width = width
    modifier.segments = segments
    modifier.limit_method = "ANGLE"
    select_only([obj])
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def atlas_uv(obj: Any, cell_index: int) -> None:
    if obj.type != "MESH":
        return
    uv_layer = obj.data.uv_layers.get("UVMap") or obj.data.uv_layers.new(name="UVMap")
    coords = [vertex.co for vertex in obj.data.vertices]
    if not coords:
        return
    min_x, max_x = min(v.x for v in coords), max(v.x for v in coords)
    min_z, max_z = min(v.z for v in coords), max(v.z for v in coords)
    span_x = max(max_x - min_x, 1e-5)
    span_z = max(max_z - min_z, 1e-5)
    col, row = cell_index % CELL_COUNT, cell_index // CELL_COUNT
    inset = 0.018
    for loop in obj.data.loops:
        vertex = obj.data.vertices[loop.vertex_index].co
        u_local = (vertex.x - min_x) / span_x
        v_local = (vertex.z - min_z) / span_z
        uv_layer.data[loop.index].uv = (
            (col + inset + u_local * (1 - inset * 2)) / CELL_COUNT,
            1.0 - (row + 1 - inset - v_local * (1 - inset * 2)) / CELL_COUNT,
        )


def remap_existing_uv_to_cell(obj: Any, cell_index: int) -> None:
    """Preserve a source unwrap while nesting it inside one atlas cell."""
    if obj.type != "MESH":
        return
    uv_layer = obj.data.uv_layers.get("UVMap")
    if uv_layer is None:
        raise RuntimeError(f"{obj.name} has no source UVMap to preserve")
    col, row = cell_index % CELL_COUNT, cell_index // CELL_COUNT
    inset = 0.018
    for loop in uv_layer.data:
        source_u = min(1.0, max(0.0, loop.uv.x))
        source_v = min(1.0, max(0.0, loop.uv.y))
        loop.uv = (
            (col + inset + source_u * (1 - inset * 2)) / CELL_COUNT,
            1.0 - (row + 1 - inset - source_v * (1 - inset * 2)) / CELL_COUNT,
        )


def material(root: Path) -> Any:
    mat = bpy.data.materials.new("M_Protagonist_Atlas")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    albedo = nodes.new("ShaderNodeTexImage")
    albedo.name = "T_Protagonist_Albedo"
    albedo.image = bpy.data.images.load(str(root / TEXTURES["albedo"].relative_to(ROOT)), check_existing=True)
    albedo.image.colorspace_settings.name = "sRGB"
    normal_tex = nodes.new("ShaderNodeTexImage")
    normal_tex.name = "T_Protagonist_Normal"
    normal_tex.image = bpy.data.images.load(str(root / TEXTURES["normal"].relative_to(ROOT)), check_existing=True)
    normal_tex.image.colorspace_settings.name = "Non-Color"
    orm = nodes.new("ShaderNodeTexImage")
    orm.name = "T_Protagonist_ORM"
    orm.image = bpy.data.images.load(str(root / TEXTURES["orm"].relative_to(ROOT)), check_existing=True)
    orm.image.colorspace_settings.name = "Non-Color"
    separate = nodes.new("ShaderNodeSeparateColor")
    normal = nodes.new("ShaderNodeNormalMap")
    normal.inputs["Strength"].default_value = 0.42
    principled.inputs["Specular IOR Level"].default_value = 0.32
    links.new(albedo.outputs["Color"], principled.inputs["Base Color"])
    links.new(normal_tex.outputs["Color"], normal.inputs["Color"])
    links.new(normal.outputs["Normal"], principled.inputs["Normal"])
    links.new(orm.outputs["Color"], separate.inputs["Color"])
    links.new(separate.outputs["Green"], principled.inputs["Roughness"])
    links.new(separate.outputs["Blue"], principled.inputs["Metallic"])
    links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    return mat


def ensure_material(obj: Any, mat: Any) -> None:
    obj.data.materials.clear()
    obj.data.materials.append(mat)


def loft(
    name: str,
    rings: Sequence[tuple[tuple[float, float, float], float, float]],
    sides: int,
    target: Any,
    *,
    cap_start: bool = True,
    cap_end: bool = True,
) -> Any:
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    for center, radius_x, radius_y in rings:
        cx, cy, cz = center
        for index in range(sides):
            angle = math.tau * index / sides
            vertices.append((cx + radius_x * math.cos(angle), cy + radius_y * math.sin(angle), cz))
    for ring_index in range(len(rings) - 1):
        start = ring_index * sides
        next_start = (ring_index + 1) * sides
        for index in range(sides):
            nxt = (index + 1) % sides
            faces.append((start + index, start + nxt, next_start + nxt, next_start + index))
    if cap_start:
        faces.append(tuple(reversed(range(sides))))
    if cap_end:
        last = (len(rings) - 1) * sides
        faces.append(tuple(last + index for index in range(sides)))
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    target.objects.link(obj)
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    return obj


def tube_path(name: str, points: Sequence[tuple[float, float, float]], radii: Sequence[float], sides: int, target: Any) -> Any:
    pts = [Vector(point) for point in points]
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    for index, point in enumerate(pts):
        if index == 0:
            tangent = (pts[1] - point).normalized()
        elif index == len(pts) - 1:
            tangent = (point - pts[index - 1]).normalized()
        else:
            tangent = (pts[index + 1] - pts[index - 1]).normalized()
        reference = Vector((0.0, 0.0, 1.0))
        if abs(tangent.dot(reference)) > 0.92:
            reference = Vector((0.0, 1.0, 0.0))
        basis_a = tangent.cross(reference).normalized()
        basis_b = tangent.cross(basis_a).normalized()
        radius = radii[index]
        for side in range(sides):
            angle = math.tau * side / sides
            vertex = point + radius * (basis_a * math.cos(angle) + basis_b * math.sin(angle))
            vertices.append(tuple(vertex))
    for ring in range(len(pts) - 1):
        for side in range(sides):
            nxt = (side + 1) % sides
            faces.append((ring * sides + side, ring * sides + nxt, (ring + 1) * sides + nxt, (ring + 1) * sides + side))
    faces.append(tuple(reversed(range(sides))))
    last = (len(pts) - 1) * sides
    faces.append(tuple(last + side for side in range(sides)))
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    target.objects.link(obj)
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    return obj


def cube(name: str, location: tuple[float, float, float], scale: tuple[float, float, float], target: Any, bevel: float = 0.0025) -> Any:
    bpy.ops.mesh.primitive_cube_add(location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    apply_transform(obj)
    move_to(obj, target)
    if bevel:
        add_bevel(obj, bevel, 2)
    return obj


def cylinder(name: str, radius: float, depth: float, location: tuple[float, float, float], target: Any, rotation: tuple[float, float, float] = (0, 0, 0), vertices: int = 20) -> Any:
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    move_to(obj, target)
    add_bevel(obj, min(radius * 0.18, 0.002), 2)
    return obj


def panel_prism(
    name: str,
    points_xz: Sequence[tuple[float, float]],
    front_y: float,
    thickness: float,
    target: Any,
) -> Any:
    """Create a closed, thin garment panel without the toy-like depth of a cube."""
    vertices = [(x, front_y, z) for x, z in points_xz]
    vertices.extend((x, front_y + thickness, z) for x, z in points_xz)
    count = len(points_xz)
    faces: list[tuple[int, ...]] = [tuple(range(count)), tuple(reversed(range(count, count * 2)))]
    for index in range(count):
        nxt = (index + 1) % count
        faces.append((index, nxt, count + nxt, count + index))
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    target.objects.link(obj)
    return obj


def ribbon_prism(
    name: str,
    centers: Sequence[tuple[float, float, float]],
    half_widths: Sequence[float],
    thickness: float,
    target: Any,
) -> Any:
    """Create a tapered solid hair lock whose broad face follows the forehead."""
    if len(centers) != len(half_widths) or len(centers) < 2:
        raise ValueError("Ribbon lock needs matching centers and widths")
    front: list[tuple[float, float, float]] = []
    back: list[tuple[float, float, float]] = []
    for (x, y, z), width in zip(centers, half_widths):
        front.extend(((x - width, y, z), (x + width, y, z)))
        back.extend(((x - width, y + thickness, z), (x + width, y + thickness, z)))
    vertices = front + back
    rows = len(centers)
    faces: list[tuple[int, ...]] = []
    for row in range(rows - 1):
        a = row * 2
        b = (row + 1) * 2
        faces.append((a, a + 1, b + 1, b))
        offset = rows * 2
        faces.append((offset + a + 1, offset + a, offset + b, offset + b + 1))
        faces.append((a, b, offset + b, offset + a))
        faces.append((a + 1, offset + a + 1, offset + b + 1, b + 1))
    offset = rows * 2
    faces.append((0, offset, offset + 1, 1))
    end = (rows - 1) * 2
    faces.append((end, end + 1, offset + end + 1, offset + end))
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    target.objects.link(obj)
    return obj


def make_body(source_body: Any, body_collection: Any, mat: Any) -> Any:
    body = source_body
    body.name = "BodyBase"
    # The official sculpt ships with a level-3 Multires modifier.  Its base
    # topology is the adopted game mesh; carrying the sculpt modifier into the
    # export candidate would inflate the GLB to more than 700k triangles.
    body.modifiers.clear()
    move_to(body, body_collection)
    body.location.x = 0.0
    scale = TARGET_HEIGHT / body.dimensions.z
    body.scale = (scale, scale, scale)
    apply_transform(body)
    # Quiet, lean silhouette and a narrower lower face without exaggeration.
    for vertex in body.data.vertices:
        co = vertex.co
        if 0.82 < co.z < 1.45 and abs(co.x) < 0.24:
            co.x *= 0.94
            co.y *= 0.96
        if co.z > 1.45:
            if co.z < 1.55:
                co.x *= 0.89
            elif co.z < 1.63:
                co.x *= 0.94
            else:
                co.x *= 0.97
            if co.y < -0.105 and 1.54 < co.z < 1.65:
                co.y += 0.007
        if co.z < 0.9 and abs(co.x) < 0.28:
            co.x *= 0.96
    # Remove the covered trunk and legs while retaining head, neck, forearms and hands.
    bm = bmesh.new()
    bm.from_mesh(body.data)
    hidden_faces = []
    for face in bm.faces:
        center = face.calc_center_median()
        keep = center.z > 1.435 or (abs(center.x) > 0.20 and center.z > 0.76)
        if not keep:
            hidden_faces.append(face)
    bmesh.ops.delete(bm, geom=hidden_faces, context="FACES")
    bmesh.ops.delete(bm, geom=[vertex for vertex in bm.verts if not vertex.link_faces], context="VERTS")
    covered_boundaries = [
        edge for edge in bm.edges
        if edge.is_boundary and max(vertex.co.z for vertex in edge.verts) < 1.46
    ]
    if covered_boundaries:
        bmesh.ops.holes_fill(bm, edges=covered_boundaries, sides=0)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(body.data)
    bm.free()
    body.data.update()
    # Preserve the official anatomical unwrap; planar projection was acceptable
    # for flat props but visibly collapsed the cheeks, ears and hands.
    remap_existing_uv_to_cell(body, 0)
    body.name = "Body"
    ensure_material(body, mat)
    return body


def make_eyes(source_eyes: Sequence[Any], body_collection: Any, mat: Any) -> Any:
    # Preserve the official anatomical eyeballs when they are available.  The
    # source places the whole character around X=-2.264 m and stores eye size in
    # object scale; both facts must be retained when recentering.  The first v2
    # attempt replaced them with flattened ellipsoids, which read as protruding
    # glossy capsules instead of eyes seated behind the source eyelids.
    parts: list[Any] = []
    usable_sources = [eye for eye in source_eyes if eye is not None and eye.name in bpy.data.objects]
    eye_centers: list[tuple[float, float, float]] = []
    if len(usable_sources) == 2:
        source_origin_x = sum(eye.location.x for eye in usable_sources) * 0.5
        scale = TARGET_HEIGHT / 1.6900835037231445
        for index, source_eye in enumerate(sorted(usable_sources, key=lambda eye: eye.location.x)):
            eye = source_eye
            eye.name = f"Sclera_{index}"
            eye.location = Vector(
                (
                    (eye.location.x - source_origin_x) * scale,
                    eye.location.y * scale + 0.0025,
                    eye.location.z * scale,
                )
            )
            eye.scale = tuple(component * scale for component in eye.scale)
            move_to(eye, body_collection)
            apply_transform(eye)
            for vertex in eye.data.vertices:
                vertex.co.x *= 0.94
                vertex.co.y *= 0.80
                vertex.co.z *= 0.64
            eye.data.update()
            remap_existing_uv_to_cell(eye, 1)
            parts.append(eye)
            eye_centers.append(tuple(eye.location))
    else:
        for index, x in enumerate((-0.0326, 0.0326)):
            bpy.ops.mesh.primitive_uv_sphere_add(
                segments=28,
                ring_count=16,
                location=(x, -0.1195, 1.5737),
            )
            eye = bpy.context.object
            eye.name = f"Sclera_{index}"
            eye.scale = (0.0120, 0.0120, 0.0120)
            apply_transform(eye)
            move_to(eye, body_collection)
            atlas_uv(eye, 1)
            parts.append(eye)
            eye_centers.append(tuple(eye.location))

    for index, (x, _, z) in enumerate(eye_centers):
        # Iris and pupil are shallow discs tangent to the eyeball.  They stay
        # behind the anatomical eyelid opening and avoid the concentric sphere
        # reflections visible in the earlier close-up.
        for suffix, radius, y, depth, cell in (
            ("Iris", 0.0042, -0.1288, 0.00030, 9),
            ("Pupil", 0.00185, -0.12915, 0.00022, 10),
        ):
            detail = cylinder(
                f"{suffix}_{index}",
                radius,
                depth,
                (x, y, z),
                body_collection,
                (math.pi / 2, 0, 0),
                24,
            )
            atlas_uv(detail, cell)
            parts.append(detail)
    result = join_objects(parts, "Eyes", body_collection)
    ensure_material(result, mat)
    return result


def make_hair(hair_collection: Any, mat: Any) -> Any:
    parts: list[Any] = []
    # A single irregular, closed shell supplies the short layered silhouette.
    # Earlier radial clumps all converged on the crown and produced a regular
    # mushroom fringe.  This scalp-following volume keeps the mass continuous;
    # only the small front locks below break its hairline.
    segments = 64
    rings = 14
    vertices: list[tuple[float, float, float]] = [(0.0, 0.004, 1.708)]
    for ring in range(1, rings + 1):
        fraction = ring / rings
        for segment in range(segments):
            phi = -math.pi + math.tau * segment / segments
            frontness = max(0.0, -math.sin(phi))
            backness = max(0.0, math.sin(phi))
            sideness = abs(math.cos(phi))
            edge_variation = 0.035 * math.sin(phi * 5.0) + 0.018 * math.cos(phi * 9.0)
            theta_max = 1.25 + 0.35 * backness + 0.04 * sideness + edge_variation
            theta = theta_max * fraction
            surface_noise = (
                1.0
                + 0.018 * math.sin(phi * 7.0 + fraction * 3.0)
                + 0.009 * math.cos(phi * 13.0 - fraction * 2.0)
            )
            x = 0.114 * surface_noise * math.sin(theta) * math.cos(phi)
            y = 0.004 + 0.135 * surface_noise * math.sin(theta) * math.sin(phi)
            y -= 0.040 * frontness * math.sin(math.pi * fraction)
            z = 1.565 + 0.143 * surface_noise * math.cos(theta)
            vertices.append((x, y, z))
    faces: list[tuple[int, ...]] = []
    for segment in range(segments):
        nxt = (segment + 1) % segments
        faces.append((0, 1 + segment, 1 + nxt))
    for ring in range(1, rings):
        start = 1 + (ring - 1) * segments
        following = start + segments
        for segment in range(segments):
            nxt = (segment + 1) % segments
            faces.append((start + segment, following + segment, following + nxt, start + nxt))
    mesh = bpy.data.meshes.new("HairVolumeMesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    volume = bpy.data.objects.new("HairVolume", mesh)
    hair_collection.objects.link(volume)
    solidify = volume.modifiers.new("HairShellThickness", "SOLIDIFY")
    solidify.thickness = 0.0045
    solidify.offset = -0.35
    solidify.use_even_offset = False
    select_only([volume])
    bpy.ops.object.modifier_apply(modifier=solidify.name)
    atlas_uv(volume, 2)
    parts.append(volume)

    # Irregular front locks bridge the volume to the forehead without forming a
    # straight geometric curtain.
    fringe = (
        (-0.075, 1.626, -0.113, -0.006),
        (-0.055, 1.614, -0.120, 0.004),
        (-0.035, 1.622, -0.125, -0.004),
        (-0.015, 1.607, -0.129, 0.004),
        (0.006, 1.618, -0.130, -0.003),
        (0.027, 1.610, -0.127, 0.004),
        (0.048, 1.622, -0.121, -0.004),
        (0.070, 1.629, -0.114, 0.003),
    )
    for index, (end_x, end_z, end_y, lean) in enumerate(fringe):
        start_x = end_x * 0.45 + lean
        lock = ribbon_prism(
            f"Fringe_{index:02d}",
            (
                (start_x, -0.086, 1.665 - (index % 2) * 0.004),
                ((start_x + end_x) * 0.52, -0.108, (1.665 + end_z) * 0.5 + 0.006),
                (end_x, end_y, end_z),
            ),
            (0.0085, 0.0068, 0.0014),
            0.0025,
            hair_collection,
        )
        atlas_uv(lock, 2)
        parts.append(lock)
    # Restrained straight brows restore the identity read at close range.  They
    # remain part of the Hair mesh, so the six-mesh budget does not grow.
    for side in (-1, 1):
        brow = tube_path(
            f"Brow_{side}",
            (
                (side * 0.016, -0.1345, 1.590),
                (side * 0.037, -0.1360, 1.593),
                (side * 0.058, -0.1325, 1.590),
            ),
            (0.0014, 0.0018, 0.0011),
            7,
            hair_collection,
        )
        atlas_uv(brow, 2)
        parts.append(brow)
    result = join_objects(parts, "Hair", hair_collection)
    ensure_material(result, mat)
    return result


def make_shirt(garments: Any, mat: Any) -> Any:
    parts: list[Any] = []
    torso = loft(
        "ShirtTorso",
        [((0, 0.002, z), rx, ry) for z, rx, ry in (
            (0.875, 0.174, 0.098), (0.910, 0.179, 0.101), (0.970, 0.181, 0.104),
            (1.050, 0.178, 0.106), (1.130, 0.179, 0.107), (1.220, 0.183, 0.108),
            (1.295, 0.192, 0.107), (1.340, 0.197, 0.103), (1.370, 0.172, 0.088),
            (1.395, 0.060, 0.045),
        )], 64, garments,
    )
    atlas_uv(torso, 3)
    parts.append(torso)
    for side in (-1, 1):
        shoulder = Vector((side * 0.058, 0.0, 1.388))
        outer = Vector((side * 0.282, -0.002, 1.238))
        sleeve = tube_path(
            f"Sleeve_{side}",
            (
                tuple(shoulder),
                tuple(shoulder.lerp(outer, 0.24)),
                tuple(shoulder.lerp(outer, 0.52)),
                tuple(shoulder.lerp(outer, 0.78)),
                tuple(outer),
            ),
            (0.052, 0.058, 0.057, 0.053, 0.047),
            48,
            garments,
        )
        atlas_uv(sleeve, 3)
        parts.append(sleeve)
    # Thin collar leaves, shoulder yoke and placket read as cloth instead of
    # floating blocks.  The open neck is deliberate and matches the identity art.
    for side in (-1, 1):
        leaf = panel_prism(
            f"CollarLeaf_{side}",
            ((side * 0.012, 1.405), (side * 0.083, 1.405), (side * 0.054, 1.330)),
            -0.095,
            0.003,
            garments,
        )
        atlas_uv(leaf, 3)
        parts.append(leaf)
    placket = cube("FrontPlacket", (0, -0.109, 1.145), (0.007, 0.0015, 0.235), garments, 0.001)
    atlas_uv(placket, 3)
    parts.append(placket)
    yoke = cube("ShoulderYoke", (0, 0.101, 1.342), (0.160, 0.0015, 0.012), garments, 0.001)
    atlas_uv(yoke, 3)
    parts.append(yoke)
    pocket = panel_prism(
        "ChestPocket",
        ((-0.146, 1.275), (-0.057, 1.275), (-0.057, 1.185), (-0.101, 1.170), (-0.146, 1.185)),
        -0.111,
        0.002,
        garments,
    )
    atlas_uv(pocket, 3)
    parts.append(pocket)
    for index, z in enumerate((1.355, 1.260, 1.165, 1.070, 0.975)):
        button = cylinder(f"ShirtButton_{index}", 0.0045, 0.003, (0, -0.113, z), garments, (math.pi / 2, 0, 0), 14)
        atlas_uv(button, 7)
        parts.append(button)
    # Piped seams around sleeves and hem provide structure at 1080p.
    for side in (-1, 1):
        seam = tube_path(f"SleeveSeam_{side}", ((side * 0.244, -0.038, 1.274), (side * 0.268, -0.030, 1.250), (side * 0.282, 0.000, 1.238)), (0.0012, 0.0012, 0.0009), 6, garments)
        atlas_uv(seam, 3)
        parts.append(seam)
        # Two restrained drape folds break the perfectly cylindrical torso at
        # 1080p without baking a pose or changing the collision silhouette.
        fold = tube_path(
            f"ShirtFold_{side}",
            (
                (side * 0.128, -0.108, 1.225),
                (side * 0.142, -0.110, 1.090),
                (side * 0.132, -0.104, 0.930),
            ),
            (0.0014, 0.0011, 0.0006),
            6,
            garments,
        )
        atlas_uv(fold, 3)
        parts.append(fold)
    result = join_objects(parts, "Shirt", garments)
    ensure_material(result, mat)
    return result


def make_surface_shirt(source_path: Path, garments: Any, mat: Any) -> Any:
    """Extract a connected short-sleeve shirt shell from the adopted CC0 body.

    The official A-pose surface supplies the shoulder and upper-arm transition.
    This avoids the disconnected cones and spherical gap fillers used by the
    technical prototype while keeping the clothing as one static review mesh.
    """
    source_name = "GEO-body_male_realistic"
    with bpy.data.libraries.load(str(source_path), link=False) as (available, imported):
        if source_name not in available.objects:
            raise RuntimeError("Official CC0 source body is missing for the shirt surface")
        imported.objects = [source_name]
    shirt = imported.objects[0]
    if shirt is None:
        raise RuntimeError("Failed to append the official body for the shirt surface")
    shirt.name = "ShirtSurfaceSource"
    shirt.modifiers.clear()
    move_to(shirt, garments)
    shirt.location.x = 0.0
    scale = TARGET_HEIGHT / shirt.dimensions.z
    shirt.scale = (scale, scale, scale)
    apply_transform(shirt)

    # Match the quiet, lean proportions applied to the visible skin mesh before
    # extracting clothing, then add a restrained cloth clearance.
    for vertex in shirt.data.vertices:
        co = vertex.co
        if 0.82 < co.z < 1.45 and abs(co.x) < 0.24:
            co.x *= 0.94
            co.y *= 0.96
        if co.z > 1.45:
            if co.z < 1.55:
                co.x *= 0.89
            elif co.z < 1.63:
                co.x *= 0.94
            else:
                co.x *= 0.97
            if co.y < -0.105 and 1.54 < co.z < 1.65:
                co.y += 0.007
        if co.z < 0.9 and abs(co.x) < 0.28:
            co.x *= 0.96

    bm = bmesh.new()
    bm.from_mesh(shirt.data)

    def keep_face(face: Any) -> bool:
        center = face.calc_center_median()
        x = abs(center.x)
        z = center.z
        torso = 0.875 <= z <= 1.445 and x <= 0.225
        upper_arm = 1.155 <= z <= 1.445 and 0.175 <= x <= 0.355
        if not (torso or upper_arm):
            return False
        # Open the upper-front neckline into a shallow V.  Back and shoulder
        # faces remain connected so the shirt reads as a real garment shell.
        if center.y < -0.070 and z > 1.335 + x * 0.50:
            return False
        return True

    removed = [face for face in bm.faces if not keep_face(face)]
    bmesh.ops.delete(bm, geom=removed, context="FACES")
    bmesh.ops.delete(bm, geom=[vertex for vertex in bm.verts if not vertex.link_faces], context="VERTS")
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.normal_update()

    # Face-center clipping follows source topology and would otherwise leave a
    # saw-tooth sleeve edge.  Project the two open sleeve loops onto planes that
    # are perpendicular to the A-pose upper arms, then regularise hem and neck.
    boundary_vertices = {
        vertex
        for edge in bm.edges
        if edge.is_boundary
        for vertex in edge.verts
    }
    for vertex in boundary_vertices:
        co = vertex.co
        if abs(co.x) > 0.205 and 1.08 < co.z < 1.32:
            side = -1.0 if co.x < 0 else 1.0
            arm_axis = Vector((side * 0.62, 0.0, -0.785)).normalized()
            cuff_center = Vector((side * 0.305, 0.0, 1.205))
            co -= arm_axis * (co - cuff_center).dot(arm_axis)
        elif abs(co.x) < 0.225 and co.z < 0.94:
            co.z = 0.882
        elif abs(co.x) < 0.18 and co.z > 1.31:
            if co.y < -0.02:
                co.z = min(1.405, 1.330 + abs(co.x) * 0.68)
            else:
                co.z = 1.405

    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.normal_update()
    for vertex in bm.verts:
        clearance = 0.0065
        if vertex.co.z < 1.10 and abs(vertex.co.x) < 0.23:
            clearance += (1.10 - vertex.co.z) * 0.020
        vertex.co += vertex.normal * clearance
    bm.to_mesh(shirt.data)
    bm.free()
    shirt.data.update()
    remap_existing_uv_to_cell(shirt, 3)

    thickness = shirt.modifiers.new("GarmentThickness", "SOLIDIFY")
    thickness.thickness = 0.0045
    thickness.offset = 0.0
    thickness.use_even_offset = True
    thickness.use_quality_normals = True
    select_only([shirt])
    bpy.ops.object.modifier_apply(modifier=thickness.name)
    for polygon in shirt.data.polygons:
        polygon.use_smooth = True

    parts: list[Any] = [shirt]
    # Compact collar leaves, placket, pocket and seams preserve the adopted
    # brick-red shirt identity without covering the anatomical cloth surface.
    for side in (-1, 1):
        leaf = panel_prism(
            f"CollarLeaf_{side}",
            (
                (side * 0.008, 1.402),
                (side * 0.073, 1.392),
                (side * 0.101, 1.338),
                (side * 0.040, 1.320),
            ),
            -0.126,
            0.003,
            garments,
        )
        atlas_uv(leaf, 3)
        parts.append(leaf)
    placket = cube("FrontPlacket", (0, -0.134, 1.125), (0.006, 0.0015, 0.205), garments, 0.001)
    atlas_uv(placket, 3)
    parts.append(placket)
    yoke = cube("ShoulderYoke", (0, 0.103, 1.345), (0.158, 0.0014, 0.009), garments, 0.001)
    atlas_uv(yoke, 3)
    parts.append(yoke)
    pocket = panel_prism(
        "ChestPocket",
        ((-0.139, 1.262), (-0.064, 1.262), (-0.064, 1.188), (-0.101, 1.174), (-0.139, 1.188)),
        -0.136,
        0.002,
        garments,
    )
    atlas_uv(pocket, 3)
    parts.append(pocket)
    for index, z in enumerate((1.346, 1.260, 1.172, 1.084, 0.996)):
        button = cylinder(
            f"ShirtButton_{index}",
            0.0041,
            0.0026,
            (0, -0.138, z),
            garments,
            (math.pi / 2, 0, 0),
            14,
        )
        atlas_uv(button, 7)
        parts.append(button)
    for side in (-1, 1):
        fold = tube_path(
            f"ShirtFold_{side}",
            (
                (side * 0.120, -0.108, 1.210),
                (side * 0.137, -0.111, 1.080),
                (side * 0.126, -0.105, 0.930),
            ),
            (0.0012, 0.0009, 0.0005),
            6,
            garments,
        )
        atlas_uv(fold, 3)
        parts.append(fold)
    result = join_objects(parts, "Shirt", garments)
    ensure_material(result, mat)
    return result


def make_trousers(garments: Any, mat: Any) -> Any:
    parts: list[Any] = []
    waist = loft("TrouserWaist", [((0, 0.006, 0.835), 0.166, 0.103), ((0, 0.006, 0.905), 0.181, 0.111)], 40, garments)
    atlas_uv(waist, 4)
    parts.append(waist)
    for side in (-1, 1):
        rings = []
        for z, x, rx, ry in (
            (0.850, 0.082, 0.098, 0.096), (0.720, 0.082, 0.103, 0.098),
            (0.560, 0.083, 0.095, 0.090), (0.390, 0.086, 0.088, 0.082),
            (0.230, 0.088, 0.082, 0.074), (0.100, 0.090, 0.084, 0.072),
            (0.055, 0.090, 0.087, 0.071),
        ):
            rings.append(((side * x, 0.006, z), rx, ry))
        leg = loft(f"TrouserLeg_{side}", rings, 36, garments)
        atlas_uv(leg, 4)
        parts.append(leg)
    # Waistband, fly, belt loops, rear pockets and side-pocket seams.
    waistband = tube_path("WaistbandFront", ((-0.16, -0.096, 0.882), (0, -0.110, 0.895), (0.16, -0.096, 0.882)), (0.004, 0.004, 0.004), 7, garments)
    atlas_uv(waistband, 4)
    parts.append(waistband)
    fly = tube_path("FlySeam", ((0, -0.108, 0.870), (0.012, -0.107, 0.795), (0.028, -0.098, 0.745)), (0.002, 0.002, 0.0015), 6, garments)
    atlas_uv(fly, 4)
    parts.append(fly)
    for index, x in enumerate((-0.135, -0.055, 0.055, 0.135)):
        loop = cube(f"BeltLoop_{index}", (x, -0.106, 0.885), (0.0045, 0.0015, 0.024), garments, 0.001)
        atlas_uv(loop, 4)
        parts.append(loop)
    for side in (-1, 1):
        rear = panel_prism(
            f"RearPocket_{side}",
            ((side * 0.132, 0.835), (side * 0.047, 0.835), (side * 0.047, 0.748), (side * 0.090, 0.735), (side * 0.132, 0.748)),
            0.101,
            0.002,
            garments,
        )
        atlas_uv(rear, 4)
        parts.append(rear)
        pocket_seam = tube_path(f"FrontPocketSeam_{side}", ((side * 0.065, -0.106, 0.860), (side * 0.120, -0.102, 0.820), (side * 0.145, -0.092, 0.770)), (0.0022, 0.0022, 0.0015), 6, garments)
        atlas_uv(pocket_seam, 4)
        parts.append(pocket_seam)
    result = join_objects(parts, "Trousers", garments)
    ensure_material(result, mat)
    return result


def make_shoes(garments: Any, mat: Any) -> Any:
    parts: list[Any] = []
    for side in (-1, 1):
        x = side * 0.095
        sole = cube(f"Sole_{side}", (x, -0.022, 0.031), (0.079, 0.137, 0.020), garments, 0.010)
        atlas_uv(sole, 6)
        parts.append(sole)
        bpy.ops.mesh.primitive_uv_sphere_add(segments=28, ring_count=16, location=(x, -0.035, 0.082))
        upper = bpy.context.object
        upper.name = f"ShoeUpper_{side}"
        upper.scale = (0.073, 0.128, 0.043)
        apply_transform(upper)
        move_to(upper, garments)
        atlas_uv(upper, 5)
        parts.append(upper)
        bpy.ops.mesh.primitive_uv_sphere_add(segments=28, ring_count=16, location=(x, -0.137, 0.073))
        toe = bpy.context.object
        toe.name = f"ToeCap_{side}"
        toe.scale = (0.061, 0.034, 0.034)
        apply_transform(toe)
        move_to(toe, garments)
        atlas_uv(toe, 6)
        parts.append(toe)
        # Six crossed laces and compact eyelets; all stay inside the Shoes mesh.
        for lace_index in range(6):
            y = -0.095 + lace_index * 0.026
            lace_a = tube_path(f"Lace_{side}_{lace_index}", ((x - 0.047, y, 0.116), (x, y - 0.004, 0.125), (x + 0.047, y + 0.005, 0.116)), (0.0025, 0.0028, 0.0025), 6, garments)
            atlas_uv(lace_a, 6)
            parts.append(lace_a)
        for eye_index in range(5):
            y = -0.088 + eye_index * 0.027
            for eye_side in (-1, 1):
                bpy.ops.mesh.primitive_torus_add(major_radius=0.0042, minor_radius=0.0012, major_segments=10, minor_segments=5, location=(x + eye_side * 0.045, y, 0.114), rotation=(math.pi / 2, 0, 0))
                eyelet = bpy.context.object
                eyelet.name = f"Eyelet_{side}_{eye_index}_{eye_side}"
                move_to(eyelet, garments)
                atlas_uv(eyelet, 7)
                parts.append(eyelet)
    result = join_objects(parts, "Shoes", garments)
    ensure_material(result, mat)
    return result


def add_anchor(name: str, location: tuple[float, float, float], root_obj: Any, target: Any) -> Any:
    empty = bpy.data.objects.new(name, None)
    empty.empty_display_type = "SPHERE"
    empty.empty_display_size = 0.035
    empty.location = location
    empty.parent = root_obj
    target.objects.link(empty)
    return empty


def scene_bounds(objects: Iterable[Any]) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box if obj.type == "MESH"]
    return Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points))), Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))


def orient_closed_components_outward(obj: Any) -> None:
    """Make every disconnected closed component consistently outward-facing."""
    if obj.type != "MESH":
        return
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.faces.ensure_lookup_table()
    unvisited = set(bm.faces)
    while unvisited:
        seed = unvisited.pop()
        component = {seed}
        stack = [seed]
        while stack:
            face = stack.pop()
            for edge in face.edges:
                for neighbor in edge.link_faces:
                    if neighbor in unvisited:
                        unvisited.remove(neighbor)
                        component.add(neighbor)
                        stack.append(neighbor)
        bmesh.ops.recalc_face_normals(bm, faces=list(component))
        signed_volume = 0.0
        for face in component:
            vertices = [loop.vert.co for loop in face.loops]
            if len(vertices) < 3:
                continue
            origin = vertices[0]
            for index in range(1, len(vertices) - 1):
                signed_volume += origin.dot(vertices[index].cross(vertices[index + 1])) / 6.0
        if signed_volume < -1e-10:
            bmesh.ops.reverse_faces(bm, faces=list(component))
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def bootstrap(root: Path) -> None:
    require_blender()
    blend = root / BLEND.relative_to(ROOT)
    source = root / SOURCE.relative_to(ROOT)
    if blend.exists():
        raise RuntimeError(f"Refusing to overwrite authoritative art Blend: {blend}")
    for texture in TEXTURES.values():
        if not (root / texture.relative_to(ROOT)).is_file():
            raise RuntimeError("Build 2048 PBR textures before bootstrapping the Blend")
    bpy.ops.wm.open_mainfile(filepath=str(source))
    scene = bpy.context.scene
    scene["town_rpg_asset"] = "protagonist.art_pass_v2"
    scene["town_rpg_source_authority"] = "blend"
    scene["town_rpg_visual_candidate"] = True
    scene["town_rpg_bound"] = False
    scene["town_rpg_animated"] = False
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE"
    # Preserve only the adopted base meshes before rebuilding clean collection ownership.
    source_body = bpy.data.objects.get("GEO-body_male_realistic")
    source_eyes = [bpy.data.objects.get("GEO-body_male_realistic.eye.L"), bpy.data.objects.get("GEO-body_male_realistic.eye.R")]
    if source_body is None or any(eye is None for eye in source_eyes):
        raise RuntimeError("Adopted official CC0 male base has unexpected contents")
    for obj in list(bpy.data.objects):
        if obj not in {source_body, *source_eyes}:
            bpy.data.objects.remove(obj, do_unlink=True)
    for old_collection in list(bpy.data.collections):
        bpy.data.collections.remove(old_collection)
    character = collection("Character")
    body_collection = collection("Body", character)
    garments = collection("Garments", character)
    hair_collection = collection("Hair", character)
    accessories = collection("Accessories", character)
    collection("Lookdev", character)
    root_obj = bpy.data.objects.new("ProtagonistRoot", None)
    root_obj.empty_display_type = "PLAIN_AXES"
    accessories.objects.link(root_obj)
    mat = material(root)
    objects = [
        make_body(source_body, body_collection, mat),
        make_eyes(source_eyes, body_collection, mat),
        make_shirt(garments, mat),
        make_trousers(garments, mat),
        make_shoes(garments, mat),
        make_hair(hair_collection, mat),
    ]
    for obj in objects:
        obj.parent = root_obj
        obj["town_rpg_export"] = True
    add_anchor("HeadAnchor", (0, 0, 1.675), root_obj, accessories)
    add_anchor("FeetAnchor", (0, 0, 0.0), root_obj, accessories)
    lower, upper = scene_bounds(objects)
    height = upper.z - lower.z
    if not (1.67 <= height <= 1.71):
        scale = TARGET_HEIGHT / height
        for obj in objects:
            obj.scale = (scale, scale, scale)
            apply_transform(obj)
        lower, upper = scene_bounds(objects)
        height = upper.z - lower.z
    for obj in objects:
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
    scene["town_rpg_height_m"] = round(height, 5)
    blend.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)
    print(f"PROTAGONIST_V2_BOOTSTRAP_PASS height={height:.4f} blend={blend}")


def refine_authoritative_blend(root: Path) -> None:
    """Apply the first artist refinement to the existing authority exactly once."""
    require_blender()
    blend = root / BLEND.relative_to(ROOT)
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    scene = bpy.context.scene
    if scene.get("town_rpg_v2_refinement_revision", 0) >= 1:
        raise RuntimeError("v2 refinement revision 1 has already been applied")
    visible = [bpy.data.objects.get(name) for name in ("Body", "Shirt", "Trousers", "Shoes", "Hair", "Eyes")]
    if any(obj is None for obj in visible):
        raise RuntimeError("Cannot refine because a required visible mesh is missing")
    lower, upper = scene_bounds(visible)
    scale = 1.695 / (upper.z - lower.z)
    for obj in visible:
        obj.scale = (scale, scale, scale)
        apply_transform(obj)
    for anchor_name in ("HeadAnchor", "FeetAnchor"):
        anchor = bpy.data.objects.get(anchor_name)
        if anchor:
            anchor.location *= scale
    hair = bpy.data.objects["Hair"]
    hair_collection = bpy.data.collections["Hair"]
    mat = hair.data.materials[0]
    details: list[Any] = [hair]
    rng = random.Random(5927)
    # Short nape and temple layers remove the helmet-cap silhouette and bring
    # the back/side views closer to the adopted face-and-hair board.
    for index in range(18):
        angle = math.radians(22 + index * (316 / 17))
        x = math.cos(angle) * rng.uniform(0.075, 0.099)
        y = math.sin(angle) * rng.uniform(0.072, 0.092) + 0.008
        start = (x * 0.45, y * 0.35, rng.uniform(1.630, 1.676))
        mid_a = (x * 0.78, y * 0.76, rng.uniform(1.596, 1.638))
        mid_b = (x * 0.96, y * 0.96, rng.uniform(1.555, 1.605))
        end = (x * 1.03, y * 1.05, mid_b[2] - rng.uniform(0.020, 0.042))
        lock = tube_path(f"RefinedLock_{index:02d}", (start, mid_a, mid_b, end), (0.0088, 0.0105, 0.0075, 0.0018), 7, hair_collection)
        atlas_uv(lock, 2)
        details.append(lock)
    hair = join_objects(details, "Hair", hair_collection)
    ensure_material(hair, mat)
    hair.parent = bpy.data.objects["ProtagonistRoot"]
    hair["town_rpg_export"] = True
    scene["town_rpg_v2_refinement_revision"] = 1
    lower, upper = scene_bounds([bpy.data.objects.get(name) for name in ("Body", "Shirt", "Trousers", "Shoes", "Hair", "Eyes")])
    scene["town_rpg_height_m"] = round(upper.z - lower.z, 5)
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)
    print(f"PROTAGONIST_V2_REFINE_PASS revision=1 height={upper.z-lower.z:.4f}")


def fix_authoritative_topology(root: Path) -> None:
    """Remove authoring-time collapsed bevel faces without rebuilding the asset."""
    require_blender()
    blend = root / BLEND.relative_to(ROOT)
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    changed = 0
    for name in ("Body", "Shirt", "Trousers", "Shoes", "Hair", "Eyes"):
        obj = bpy.data.objects.get(name)
        if obj is None:
            raise RuntimeError(f"Missing required mesh while fixing topology: {name}")
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        before = len(bm.faces)
        bmesh.ops.dissolve_degenerate(bm, dist=1e-7, edges=list(bm.edges))
        bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-8)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        changed += before - len(bm.faces)
        bm.to_mesh(obj.data)
        bm.free()
        obj.data.update()
        obj.modifiers.clear()
    bpy.context.scene["town_rpg_v2_topology_fix_revision"] = 1
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)
    print(f"PROTAGONIST_V2_TOPOLOGY_FIX_PASS removed_faces={changed}")


def fix_export_body_mesh(root: Path) -> None:
    """Keep the official Multires sculpt in Lookdev and author a clean export Body."""
    require_blender()
    blend = root / BLEND.relative_to(ROOT)
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    body = bpy.data.objects.get("Body")
    if body is None:
        raise RuntimeError("Export Body is missing")
    if not body.modifiers:
        raise RuntimeError("Export Body is already modifier-free")
    lookdev = bpy.data.collections["Lookdev"]
    body_collection = bpy.data.collections["Body"]
    root_obj = bpy.data.objects["ProtagonistRoot"]
    source = body
    source.name = "Body_HighSculpt_Source"
    source["town_rpg_export"] = False
    source.hide_render = True
    source.hide_viewport = True
    source.hide_set(True)
    move_to(source, lookdev)
    export_body = bpy.data.objects.new("Body", source.data.copy())
    export_body.matrix_world = source.matrix_world.copy()
    export_body.parent = root_obj
    export_body["town_rpg_export"] = True
    body_collection.objects.link(export_body)
    for polygon in export_body.data.polygons:
        polygon.use_smooth = True
    bpy.context.scene["town_rpg_v2_export_mesh_revision"] = 1
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)
    print(
        "PROTAGONIST_V2_EXPORT_MESH_FIX_PASS "
        f"base_tris={sum(max(0, len(p.vertices)-2) for p in export_body.data.polygons)} "
        f"lookdev_source={source.name}"
    )


def fix_static_visual_candidate(root: Path) -> None:
    """Apply the reviewed static visual corrections to the existing v2 authority."""
    require_blender()
    blend = root / BLEND.relative_to(ROOT)
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    scene = bpy.context.scene
    if scene.get("town_rpg_v2_visual_fix_revision", 0) >= 2:
        raise RuntimeError("v2 static visual fix revision 2 has already been applied")
    body = bpy.data.objects.get("Body")
    if body is None or body.modifiers:
        raise RuntimeError("Visual fix requires the modifier-free export Body")
    if not body.data.materials or body.data.materials[0] is None:
        raise RuntimeError("Visual fix requires the protagonist atlas material")
    mat = body.data.materials[0]

    # Remove the oversized geometry brows and lip line by their atlas cells while
    # preserving the official anatomical base.  Covered lower openings are capped;
    # facial openings above the shirt line remain untouched.
    bm = bmesh.new()
    bm.from_mesh(body.data)
    uv_layer = bm.loops.layers.uv.get("UVMap")
    if uv_layer is None:
        raise RuntimeError("Body UVMap is missing")

    def cell_for(loop: Any) -> int:
        uv = loop[uv_layer].uv
        col = min(CELL_COUNT - 1, max(0, int(uv.x * CELL_COUNT)))
        flipped_row = min(CELL_COUNT - 1, max(0, int(uv.y * CELL_COUNT)))
        row = CELL_COUNT - 1 - flipped_row
        return row * CELL_COUNT + col

    overlay_faces = [
        face
        for face in bm.faces
        if face.loops and {cell_for(loop) for loop in face.loops}.issubset({2, 8})
    ]
    bmesh.ops.delete(bm, geom=overlay_faces, context="FACES")
    bmesh.ops.delete(bm, geom=[vertex for vertex in bm.verts if not vertex.link_faces], context="VERTS")
    lower_boundaries = [
        edge for edge in bm.edges
        if edge.is_boundary and max(vertex.co.z for vertex in edge.verts) < 1.46
    ]
    if lower_boundaries:
        bmesh.ops.holes_fill(bm, edges=lower_boundaries, sides=0)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(body.data)
    bm.free()
    body.data.update()

    for name in ("Eyes", "Hair", "Shirt", "Trousers", "Shoes"):
        old = bpy.data.objects.get(name)
        if old is not None:
            bpy.data.objects.remove(old, do_unlink=True)
    root_obj = bpy.data.objects["ProtagonistRoot"]
    body_collection = bpy.data.collections["Body"]
    hair_collection = bpy.data.collections["Hair"]
    garments = bpy.data.collections["Garments"]
    rebuilt = (
        make_eyes((), body_collection, mat),
        make_hair(hair_collection, mat),
        make_shirt(garments, mat),
        make_trousers(garments, mat),
        make_shoes(garments, mat),
    )
    body.parent = root_obj
    body["town_rpg_export"] = True
    for obj in rebuilt:
        obj.parent = root_obj
        obj["town_rpg_export"] = True
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
    visible = [body, *rebuilt]
    lower, upper = scene_bounds(visible)
    height = upper.z - lower.z
    if not (1.67 <= height <= 1.71):
        scale = 1.695 / height
        for obj in visible:
            obj.scale = (scale, scale, scale)
            apply_transform(obj)
        for anchor_name in ("HeadAnchor", "FeetAnchor"):
            anchor = bpy.data.objects.get(anchor_name)
            if anchor is not None:
                anchor.location *= scale
        lower, upper = scene_bounds(visible)
        height = upper.z - lower.z
    scene["town_rpg_height_m"] = round(height, 5)
    scene["town_rpg_v2_visual_fix_revision"] = 2
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)
    print(f"PROTAGONIST_V2_VISUAL_FIX_PASS revision=2 height={height:.4f}")


def refine_static_visual_candidate(root: Path) -> None:
    """Second reviewed correction: eye seating, neckline continuity and silhouette."""
    require_blender()
    blend = root / BLEND.relative_to(ROOT)
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    scene = bpy.context.scene
    if scene.get("town_rpg_v2_visual_fix_revision", 0) != 2:
        raise RuntimeError("Visual refinement requires revision 2 exactly")
    body = bpy.data.objects.get("Body")
    if body is None or not body.data.materials or body.data.materials[0] is None:
        raise RuntimeError("Visual refinement requires the corrected Body")
    mat = body.data.materials[0]
    root_obj = bpy.data.objects["ProtagonistRoot"]
    body_collection = bpy.data.collections["Body"]
    garments = bpy.data.collections["Garments"]
    hair_collection = bpy.data.collections["Hair"]

    # The source body was intentionally removed beneath clothing; this small,
    # closed skin insert bridges the open collar without restoring hidden torso
    # geometry or changing the six-mesh runtime budget.
    chest = loft(
        "CollarSkinInsert",
        (
            ((0, 0.002, 1.382), 0.074, 0.052),
            ((0, 0.001, 1.438), 0.064, 0.048),
        ),
        28,
        body_collection,
    )
    atlas_uv(chest, 0)
    ensure_material(chest, mat)
    body = join_objects((body, chest), "Body", body_collection)
    ensure_material(body, mat)
    body.parent = root_obj
    body["town_rpg_export"] = True

    for name in ("Eyes", "Hair", "Shirt"):
        old = bpy.data.objects.get(name)
        if old is not None:
            bpy.data.objects.remove(old, do_unlink=True)
    rebuilt = (
        make_eyes((), body_collection, mat),
        make_hair(hair_collection, mat),
        make_shirt(garments, mat),
    )
    for obj in rebuilt:
        obj.parent = root_obj
        obj["town_rpg_export"] = True
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
    visible = [bpy.data.objects.get(name) for name in ("Body", "Shirt", "Trousers", "Shoes", "Hair", "Eyes")]
    if any(obj is None for obj in visible):
        raise RuntimeError("Visual refinement lost a required visible mesh")
    lower, upper = scene_bounds(visible)
    scene["town_rpg_height_m"] = round(upper.z - lower.z, 5)
    scene["town_rpg_v2_visual_fix_revision"] = 3
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)
    print(f"PROTAGONIST_V2_VISUAL_REFINE_PASS revision=3 height={upper.z-lower.z:.4f}")


def rebuild_static_body_from_source(root: Path) -> None:
    """Restore the official CC0 unwrap and rebuild the corrected face/hair assembly."""
    require_blender()
    blend = root / BLEND.relative_to(ROOT)
    source_path = root / SOURCE.relative_to(ROOT)
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    scene = bpy.context.scene
    if scene.get("town_rpg_v2_visual_fix_revision", 0) != 3:
        raise RuntimeError("Source body rebuild requires visual revision 3 exactly")
    old_body = bpy.data.objects.get("Body")
    if old_body is None or not old_body.data.materials or old_body.data.materials[0] is None:
        raise RuntimeError("Source body rebuild requires the current atlas material")
    mat = old_body.data.materials[0]
    root_obj = bpy.data.objects["ProtagonistRoot"]
    body_collection = bpy.data.collections["Body"]
    hair_collection = bpy.data.collections["Hair"]
    for name in ("Body", "Eyes", "Hair"):
        old = bpy.data.objects.get(name)
        if old is not None:
            bpy.data.objects.remove(old, do_unlink=True)

    source_name = "GEO-body_male_realistic"
    with bpy.data.libraries.load(str(source_path), link=False) as (available, imported):
        if source_name not in available.objects:
            raise RuntimeError("Official CC0 source body is missing")
        imported.objects = [source_name]
    source_body = imported.objects[0]
    if source_body is None:
        raise RuntimeError("Failed to append official CC0 source body")
    body = make_body(source_body, body_collection, mat)
    chest = loft(
        "CollarSkinInsert",
        (
            ((0, 0.002, 1.382), 0.074, 0.052),
            ((0, 0.001, 1.438), 0.064, 0.048),
        ),
        28,
        body_collection,
    )
    atlas_uv(chest, 0)
    ensure_material(chest, mat)
    body = join_objects((body, chest), "Body", body_collection)
    ensure_material(body, mat)
    body.parent = root_obj
    body["town_rpg_export"] = True
    rebuilt = (
        make_eyes((), body_collection, mat),
        make_hair(hair_collection, mat),
    )
    for obj in rebuilt:
        obj.parent = root_obj
        obj["town_rpg_export"] = True
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
    visible = [bpy.data.objects.get(name) for name in ("Body", "Shirt", "Trousers", "Shoes", "Hair", "Eyes")]
    if any(obj is None for obj in visible):
        raise RuntimeError("Source body rebuild lost a required visible mesh")
    lower, upper = scene_bounds(visible)
    scene["town_rpg_height_m"] = round(upper.z - lower.z, 5)
    scene["town_rpg_v2_visual_fix_revision"] = 4
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)
    print(f"PROTAGONIST_V2_SOURCE_REBUILD_PASS revision=4 height={upper.z-lower.z:.4f}")


def reseat_static_eyes(root: Path) -> None:
    """Seat the eye assembly against the restored source body's original sockets."""
    require_blender()
    blend = root / BLEND.relative_to(ROOT)
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    scene = bpy.context.scene
    if scene.get("town_rpg_v2_visual_fix_revision", 0) != 4:
        raise RuntimeError("Eye seating requires visual revision 4 exactly")
    body = bpy.data.objects.get("Body")
    if body is None or not body.data.materials or body.data.materials[0] is None:
        raise RuntimeError("Eye seating requires the rebuilt Body")
    old = bpy.data.objects.get("Eyes")
    if old is not None:
        bpy.data.objects.remove(old, do_unlink=True)
    eyes = make_eyes((), bpy.data.collections["Body"], body.data.materials[0])
    eyes.parent = bpy.data.objects["ProtagonistRoot"]
    eyes["town_rpg_export"] = True
    for polygon in eyes.data.polygons:
        polygon.use_smooth = True
    scene["town_rpg_v2_visual_fix_revision"] = 5
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)
    print("PROTAGONIST_V2_EYE_SEATING_PASS revision=5")


def polish_static_visual_candidate(root: Path) -> None:
    """Apply the high-impact static review polish without adding a rig or pose."""
    require_blender()
    blend = root / BLEND.relative_to(ROOT)
    source_path = root / SOURCE.relative_to(ROOT)
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    scene = bpy.context.scene
    if scene.get("town_rpg_v2_visual_fix_revision", 0) != 5:
        raise RuntimeError("Visual polish requires visual revision 5 exactly")
    body = bpy.data.objects.get("Body")
    if body is None or not body.data.materials or body.data.materials[0] is None:
        raise RuntimeError("Visual polish requires the restored official Body")
    mat = body.data.materials[0]
    root_obj = bpy.data.objects["ProtagonistRoot"]
    body_collection = bpy.data.collections["Body"]
    garments = bpy.data.collections["Garments"]
    hair_collection = bpy.data.collections["Hair"]

    for name in ("Eyes", "Hair", "Shirt"):
        old = bpy.data.objects.get(name)
        if old is not None:
            bpy.data.objects.remove(old, do_unlink=True)

    source_eye_names = (
        "GEO-body_male_realistic.eye.L",
        "GEO-body_male_realistic.eye.R",
    )
    with bpy.data.libraries.load(str(source_path), link=False) as (available, imported):
        missing = [name for name in source_eye_names if name not in available.objects]
        if missing:
            raise RuntimeError(f"Official CC0 source eyes are missing: {missing}")
        imported.objects = list(source_eye_names)
    source_eyes = [eye for eye in imported.objects if eye is not None]
    rebuilt = (
        make_eyes(source_eyes, body_collection, mat),
        make_hair(hair_collection, mat),
        make_shirt(garments, mat),
    )
    for obj in rebuilt:
        obj.parent = root_obj
        obj["town_rpg_export"] = True
        for polygon in obj.data.polygons:
            polygon.use_smooth = True

    required = [
        bpy.data.objects.get(name)
        for name in ("Body", "Shirt", "Trousers", "Shoes", "Hair", "Eyes")
    ]
    if any(obj is None for obj in required):
        raise RuntimeError("Visual polish lost a required visible mesh")
    for obj in required:
        orient_closed_components_outward(obj)
    if hasattr(mat, "use_backface_culling"):
        mat.use_backface_culling = True

    lower, upper = scene_bounds(required)
    height = upper.z - lower.z
    if not (1.67 <= height <= 1.71):
        raise RuntimeError(f"Visual polish moved height outside the approved range: {height:.4f}")
    scene["town_rpg_height_m"] = round(height, 5)
    scene["town_rpg_v2_visual_fix_revision"] = 6
    scene["town_rpg_visual_candidate_status"] = "static_review_pending"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)
    print(f"PROTAGONIST_V2_VISUAL_POLISH_PASS revision=6 height={height:.4f}")


def finalize_static_visual_candidate(root: Path) -> None:
    """Second visual review pass: calm eyes, layered hair and cloth shoulders."""
    require_blender()
    blend = root / BLEND.relative_to(ROOT)
    source_path = root / SOURCE.relative_to(ROOT)
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    scene = bpy.context.scene
    if scene.get("town_rpg_v2_visual_fix_revision", 0) != 6:
        raise RuntimeError("Visual finalization requires visual revision 6 exactly")
    body = bpy.data.objects.get("Body")
    if body is None or not body.data.materials or body.data.materials[0] is None:
        raise RuntimeError("Visual finalization requires the restored official Body")
    mat = body.data.materials[0]
    root_obj = bpy.data.objects["ProtagonistRoot"]
    body_collection = bpy.data.collections["Body"]
    garments = bpy.data.collections["Garments"]
    hair_collection = bpy.data.collections["Hair"]
    for name in ("Eyes", "Hair", "Shirt"):
        old = bpy.data.objects.get(name)
        if old is not None:
            bpy.data.objects.remove(old, do_unlink=True)

    source_eye_names = (
        "GEO-body_male_realistic.eye.L",
        "GEO-body_male_realistic.eye.R",
    )
    with bpy.data.libraries.load(str(source_path), link=False) as (available, imported):
        missing = [name for name in source_eye_names if name not in available.objects]
        if missing:
            raise RuntimeError(f"Official CC0 source eyes are missing: {missing}")
        imported.objects = list(source_eye_names)
    rebuilt = (
        make_eyes([eye for eye in imported.objects if eye is not None], body_collection, mat),
        make_hair(hair_collection, mat),
        make_shirt(garments, mat),
    )
    for obj in rebuilt:
        obj.parent = root_obj
        obj["town_rpg_export"] = True
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
    required = [
        bpy.data.objects.get(name)
        for name in ("Body", "Shirt", "Trousers", "Shoes", "Hair", "Eyes")
    ]
    if any(obj is None for obj in required):
        raise RuntimeError("Visual finalization lost a required visible mesh")
    for obj in required:
        orient_closed_components_outward(obj)
    if hasattr(mat, "use_backface_culling"):
        mat.use_backface_culling = True
    lower, upper = scene_bounds(required)
    height = upper.z - lower.z
    if not (1.67 <= height <= 1.71):
        raise RuntimeError(f"Visual finalization moved height outside the approved range: {height:.4f}")
    scene["town_rpg_height_m"] = round(height, 5)
    scene["town_rpg_v2_visual_fix_revision"] = 7
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)
    print(f"PROTAGONIST_V2_VISUAL_FINALIZE_PASS revision=7 height={height:.4f}")


def resolve_static_visual_candidate(root: Path) -> None:
    """Resolve the final static-review silhouette without touching runtime state."""
    require_blender()
    blend = root / BLEND.relative_to(ROOT)
    source_path = root / SOURCE.relative_to(ROOT)
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    scene = bpy.context.scene
    if scene.get("town_rpg_v2_visual_fix_revision", 0) != 7:
        raise RuntimeError("Visual resolution requires visual revision 7 exactly")
    body = bpy.data.objects.get("Body")
    if body is None or not body.data.materials or body.data.materials[0] is None:
        raise RuntimeError("Visual resolution requires the restored official Body")
    mat = body.data.materials[0]
    root_obj = bpy.data.objects["ProtagonistRoot"]
    body_collection = bpy.data.collections["Body"]
    garments = bpy.data.collections["Garments"]
    hair_collection = bpy.data.collections["Hair"]
    for name in ("Eyes", "Hair", "Shirt"):
        old = bpy.data.objects.get(name)
        if old is not None:
            bpy.data.objects.remove(old, do_unlink=True)
    source_eye_names = (
        "GEO-body_male_realistic.eye.L",
        "GEO-body_male_realistic.eye.R",
    )
    with bpy.data.libraries.load(str(source_path), link=False) as (available, imported):
        missing = [name for name in source_eye_names if name not in available.objects]
        if missing:
            raise RuntimeError(f"Official CC0 source eyes are missing: {missing}")
        imported.objects = list(source_eye_names)
    rebuilt = (
        make_eyes([eye for eye in imported.objects if eye is not None], body_collection, mat),
        make_hair(hair_collection, mat),
        make_shirt(garments, mat),
    )
    for obj in rebuilt:
        obj.parent = root_obj
        obj["town_rpg_export"] = True
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
    required = [
        bpy.data.objects.get(name)
        for name in ("Body", "Shirt", "Trousers", "Shoes", "Hair", "Eyes")
    ]
    if any(obj is None for obj in required):
        raise RuntimeError("Visual resolution lost a required visible mesh")
    for obj in required:
        orient_closed_components_outward(obj)
    if hasattr(mat, "use_backface_culling"):
        mat.use_backface_culling = True
    lower, upper = scene_bounds(required)
    height = upper.z - lower.z
    if not (1.67 <= height <= 1.71):
        raise RuntimeError(f"Visual resolution moved height outside the approved range: {height:.4f}")
    scene["town_rpg_height_m"] = round(height, 5)
    scene["town_rpg_v2_visual_fix_revision"] = 8
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)
    print(f"PROTAGONIST_V2_VISUAL_RESOLVE_PASS revision=8 height={height:.4f}")


def correct_static_visual_candidate(root: Path) -> None:
    """Correct shell clearance and shoulder continuity found in revision 8 renders."""
    require_blender()
    blend = root / BLEND.relative_to(ROOT)
    source_path = root / SOURCE.relative_to(ROOT)
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    scene = bpy.context.scene
    if scene.get("town_rpg_v2_visual_fix_revision", 0) != 8:
        raise RuntimeError("Visual correction requires visual revision 8 exactly")
    body = bpy.data.objects.get("Body")
    if body is None or not body.data.materials or body.data.materials[0] is None:
        raise RuntimeError("Visual correction requires the restored official Body")
    mat = body.data.materials[0]
    root_obj = bpy.data.objects["ProtagonistRoot"]
    body_collection = bpy.data.collections["Body"]
    garments = bpy.data.collections["Garments"]
    hair_collection = bpy.data.collections["Hair"]
    for name in ("Eyes", "Hair", "Shirt"):
        old = bpy.data.objects.get(name)
        if old is not None:
            bpy.data.objects.remove(old, do_unlink=True)
    source_eye_names = (
        "GEO-body_male_realistic.eye.L",
        "GEO-body_male_realistic.eye.R",
    )
    with bpy.data.libraries.load(str(source_path), link=False) as (available, imported):
        missing = [name for name in source_eye_names if name not in available.objects]
        if missing:
            raise RuntimeError(f"Official CC0 source eyes are missing: {missing}")
        imported.objects = list(source_eye_names)
    rebuilt = (
        make_eyes([eye for eye in imported.objects if eye is not None], body_collection, mat),
        make_hair(hair_collection, mat),
        make_shirt(garments, mat),
    )
    for obj in rebuilt:
        obj.parent = root_obj
        obj["town_rpg_export"] = True
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
    required = [
        bpy.data.objects.get(name)
        for name in ("Body", "Shirt", "Trousers", "Shoes", "Hair", "Eyes")
    ]
    if any(obj is None for obj in required):
        raise RuntimeError("Visual correction lost a required visible mesh")
    for obj in required:
        orient_closed_components_outward(obj)
    if hasattr(mat, "use_backface_culling"):
        mat.use_backface_culling = True
    lower, upper = scene_bounds(required)
    height = upper.z - lower.z
    if not (1.67 <= height <= 1.71):
        raise RuntimeError(f"Visual correction moved height outside the approved range: {height:.4f}")
    scene["town_rpg_height_m"] = round(height, 5)
    scene["town_rpg_v2_visual_fix_revision"] = 9
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)
    print(f"PROTAGONIST_V2_VISUAL_CORRECT_PASS revision=9 height={height:.4f}")


def seal_static_visual_candidate(root: Path) -> None:
    """Seal the shoulder transition and retain two-sided static review surfaces."""
    require_blender()
    blend = root / BLEND.relative_to(ROOT)
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    scene = bpy.context.scene
    if scene.get("town_rpg_v2_visual_fix_revision", 0) != 9:
        raise RuntimeError("Visual sealing requires visual revision 9 exactly")
    body = bpy.data.objects.get("Body")
    old_shirt = bpy.data.objects.get("Shirt")
    if body is None or old_shirt is None or not body.data.materials or body.data.materials[0] is None:
        raise RuntimeError("Visual sealing requires the revision 9 body and shirt")
    mat = body.data.materials[0]
    bpy.data.objects.remove(old_shirt, do_unlink=True)
    shirt = make_shirt(bpy.data.collections["Garments"], mat)
    shirt.parent = bpy.data.objects["ProtagonistRoot"]
    shirt["town_rpg_export"] = True
    orient_closed_components_outward(shirt)
    # The closed solid hair shell contains a deliberately thin inner surface.
    # Compatibility renders the shared atlas most reliably as double-sided;
    # formal topology checks still require zero boundary/non-manifold edges.
    if hasattr(mat, "use_backface_culling"):
        mat.use_backface_culling = False
    required = [
        bpy.data.objects.get(name)
        for name in ("Body", "Shirt", "Trousers", "Shoes", "Hair", "Eyes")
    ]
    lower, upper = scene_bounds(required)
    height = upper.z - lower.z
    scene["town_rpg_height_m"] = round(height, 5)
    scene["town_rpg_v2_visual_fix_revision"] = 10
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)
    print(f"PROTAGONIST_V2_VISUAL_SEAL_PASS revision=10 height={height:.4f}")


def surface_static_visual_candidate(root: Path) -> None:
    """Move the hair surface outside the scalp and round the garment shoulders."""
    require_blender()
    blend = root / BLEND.relative_to(ROOT)
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    scene = bpy.context.scene
    if scene.get("town_rpg_v2_visual_fix_revision", 0) != 10:
        raise RuntimeError("Visual surfacing requires visual revision 10 exactly")
    body = bpy.data.objects.get("Body")
    if body is None or not body.data.materials or body.data.materials[0] is None:
        raise RuntimeError("Visual surfacing requires the revision 10 body")
    mat = body.data.materials[0]
    for name in ("Hair", "Shirt"):
        old = bpy.data.objects.get(name)
        if old is not None:
            bpy.data.objects.remove(old, do_unlink=True)
    rebuilt = (
        make_hair(bpy.data.collections["Hair"], mat),
        make_shirt(bpy.data.collections["Garments"], mat),
    )
    for obj in rebuilt:
        obj.parent = bpy.data.objects["ProtagonistRoot"]
        obj["town_rpg_export"] = True
        orient_closed_components_outward(obj)
    required = [
        bpy.data.objects.get(name)
        for name in ("Body", "Shirt", "Trousers", "Shoes", "Hair", "Eyes")
    ]
    lower, upper = scene_bounds(required)
    height = upper.z - lower.z
    if not (1.67 <= height <= 1.71):
        raise RuntimeError(f"Visual surfacing moved height outside the approved range: {height:.4f}")
    scene["town_rpg_height_m"] = round(height, 5)
    scene["town_rpg_v2_visual_fix_revision"] = 11
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)
    print(f"PROTAGONIST_V2_VISUAL_SURFACE_PASS revision=11 height={height:.4f}")


def garment_static_visual_candidate(root: Path) -> None:
    """Replace the technical shirt assembly with one continuous cloth surface."""
    require_blender()
    blend = root / BLEND.relative_to(ROOT)
    source_path = root / SOURCE.relative_to(ROOT)
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    scene = bpy.context.scene
    if scene.get("town_rpg_v2_visual_fix_revision", 0) != 11:
        raise RuntimeError("Garment surfacing requires visual revision 11 exactly")
    body = bpy.data.objects.get("Body")
    old_shirt = bpy.data.objects.get("Shirt")
    if body is None or old_shirt is None or not body.data.materials or body.data.materials[0] is None:
        raise RuntimeError("Garment surfacing requires the revision 11 body and shirt")
    mat = body.data.materials[0]
    bpy.data.objects.remove(old_shirt, do_unlink=True)
    shirt = make_surface_shirt(source_path, bpy.data.collections["Garments"], mat)
    shirt.parent = bpy.data.objects["ProtagonistRoot"]
    shirt["town_rpg_export"] = True
    orient_closed_components_outward(shirt)
    required = [
        bpy.data.objects.get(name)
        for name in ("Body", "Shirt", "Trousers", "Shoes", "Hair", "Eyes")
    ]
    if any(obj is None for obj in required):
        raise RuntimeError("Garment surfacing lost a required visible mesh")
    lower, upper = scene_bounds(required)
    height = upper.z - lower.z
    if not (1.67 <= height <= 1.71):
        raise RuntimeError(f"Garment surfacing moved height outside the approved range: {height:.4f}")
    scene["town_rpg_height_m"] = round(height, 5)
    scene["town_rpg_v2_visual_fix_revision"] = 12
    scene["town_rpg_visual_candidate_status"] = "static_review_pending"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)
    print(f"PROTAGONIST_V2_VISUAL_GARMENT_PASS revision=12 height={height:.4f}")


def tailor_static_visual_candidate(root: Path) -> None:
    """Regularise the continuous shirt's sleeve, hem and neckline boundaries."""
    require_blender()
    blend = root / BLEND.relative_to(ROOT)
    source_path = root / SOURCE.relative_to(ROOT)
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    scene = bpy.context.scene
    if scene.get("town_rpg_v2_visual_fix_revision", 0) != 12:
        raise RuntimeError("Garment tailoring requires visual revision 12 exactly")
    body = bpy.data.objects.get("Body")
    old_shirt = bpy.data.objects.get("Shirt")
    if body is None or old_shirt is None or not body.data.materials or body.data.materials[0] is None:
        raise RuntimeError("Garment tailoring requires the revision 12 body and shirt")
    mat = body.data.materials[0]
    bpy.data.objects.remove(old_shirt, do_unlink=True)
    shirt = make_surface_shirt(source_path, bpy.data.collections["Garments"], mat)
    shirt.parent = bpy.data.objects["ProtagonistRoot"]
    shirt["town_rpg_export"] = True
    orient_closed_components_outward(shirt)
    required = [
        bpy.data.objects.get(name)
        for name in ("Body", "Shirt", "Trousers", "Shoes", "Hair", "Eyes")
    ]
    if any(obj is None for obj in required):
        raise RuntimeError("Garment tailoring lost a required visible mesh")
    lower, upper = scene_bounds(required)
    height = upper.z - lower.z
    if not (1.67 <= height <= 1.71):
        raise RuntimeError(f"Garment tailoring moved height outside the approved range: {height:.4f}")
    scene["town_rpg_height_m"] = round(height, 5)
    scene["town_rpg_v2_visual_fix_revision"] = 13
    scene["town_rpg_visual_candidate_status"] = "static_review_pending"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)
    print(f"PROTAGONIST_V2_VISUAL_TAILOR_PASS revision=13 height={height:.4f}")


def cuff_static_visual_candidate(root: Path) -> None:
    """Lock the accepted revision-14 shirt boundary without added cuff geometry.

    The historical ``visual-cuffs`` mode name is retained only so the authored
    revision chain remains reproducible. The rejected annular cuff meshes are
    intentionally absent from ``make_surface_shirt``.
    """
    require_blender()
    blend = root / BLEND.relative_to(ROOT)
    source_path = root / SOURCE.relative_to(ROOT)
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    scene = bpy.context.scene
    if scene.get("town_rpg_v2_visual_fix_revision", 0) != 13:
        raise RuntimeError("Sleeve cuff finishing requires visual revision 13 exactly")
    body = bpy.data.objects.get("Body")
    old_shirt = bpy.data.objects.get("Shirt")
    if body is None or old_shirt is None or not body.data.materials or body.data.materials[0] is None:
        raise RuntimeError("Sleeve cuff finishing requires the revision 13 body and shirt")
    mat = body.data.materials[0]
    bpy.data.objects.remove(old_shirt, do_unlink=True)
    shirt = make_surface_shirt(source_path, bpy.data.collections["Garments"], mat)
    shirt.parent = bpy.data.objects["ProtagonistRoot"]
    shirt["town_rpg_export"] = True
    orient_closed_components_outward(shirt)
    required = [
        bpy.data.objects.get(name)
        for name in ("Body", "Shirt", "Trousers", "Shoes", "Hair", "Eyes")
    ]
    if any(obj is None for obj in required):
        raise RuntimeError("Sleeve cuff finishing lost a required visible mesh")
    lower, upper = scene_bounds(required)
    height = upper.z - lower.z
    if not (1.67 <= height <= 1.71):
        raise RuntimeError(f"Sleeve cuff finishing moved height outside the approved range: {height:.4f}")
    scene["town_rpg_height_m"] = round(height, 5)
    scene["town_rpg_v2_visual_fix_revision"] = 14
    scene["town_rpg_visual_candidate_status"] = "static_review_pending"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)
    print(f"PROTAGONIST_V2_VISUAL_CUFFS_PASS revision=14 height={height:.4f}")


def clean_static_visual_topology(root: Path) -> None:
    """Remove zero-area export faces without changing the accepted silhouette.

    Revisions 15 and 16 were rejected garment experiments, so the accepted path
    deliberately advances directly from revision 14 to revision 17.
    """
    require_blender()
    blend = root / BLEND.relative_to(ROOT)
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    scene = bpy.context.scene
    if scene.get("town_rpg_v2_visual_fix_revision", 0) != 14:
        raise RuntimeError("Static topology cleanup requires the accepted visual revision 14")
    cleaned_faces = 0
    for name in ("Body", "Shirt", "Trousers", "Shoes", "Hair", "Eyes"):
        obj = bpy.data.objects.get(name)
        if obj is None:
            raise RuntimeError(f"Static topology cleanup is missing {name}")
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        before = len(bm.faces)
        bmesh.ops.dissolve_degenerate(bm, dist=1e-8, edges=list(bm.edges))
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        cleaned_faces += before - len(bm.faces)
        bm.to_mesh(obj.data)
        bm.free()
        obj.data.update()
    required = [bpy.data.objects.get(name) for name in ("Body", "Shirt", "Trousers", "Shoes", "Hair", "Eyes")]
    lower, upper = scene_bounds(required)
    height = upper.z - lower.z
    if not (1.67 <= height <= 1.71):
        raise RuntimeError(f"Static topology cleanup moved height outside the approved range: {height:.4f}")
    scene["town_rpg_height_m"] = round(height, 5)
    scene["town_rpg_v2_visual_fix_revision"] = 17
    scene["town_rpg_visual_candidate_status"] = "static_review_pending"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)
    print(
        "PROTAGONIST_V2_VISUAL_TOPOLOGY_CLEAN_PASS "
        f"revision=17 cleaned_faces={cleaned_faces} height={height:.4f}"
    )


def setup_render(scene: Any, root: Path) -> Any:
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1920
    scene.render.resolution_y = 1080
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.render.image_settings.color_depth = "8"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.world.color = (0.025, 0.031, 0.040)
    lookdev = bpy.data.collections.get("Lookdev")
    if lookdev is None:
        lookdev = collection("Lookdev", bpy.data.collections.get("Character"))
    # Soft studio sweep.
    floor = cube("ReviewFloor", (0, 0, -0.035), (2.4, 2.4, 0.03), lookdev, 0.02)
    floor_mat = bpy.data.materials.new("M_ReviewFloor")
    floor_mat.diffuse_color = (0.055, 0.065, 0.078, 1)
    floor.data.materials.append(floor_mat)
    for name, location, energy, color, size in (
        ("Key", (-2.3, -2.8, 3.2), 430, (1.0, 0.76, 0.62), 2.2),
        ("Fill", (2.4, -1.4, 2.1), 260, (0.58, 0.72, 1.0), 2.4),
        ("Rim", (0.4, 2.2, 2.7), 320, (1.0, 0.58, 0.38), 1.7),
    ):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.color = color
        data.shape = "DISK"
        data.size = size
        lamp = bpy.data.objects.new(name, data)
        lamp.location = location
        lookdev.objects.link(lamp)
        direction = Vector((0, 0, 0.95)) - lamp.location
        lamp.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    camera_data = bpy.data.cameras.new("ReviewCamera")
    camera = bpy.data.objects.new("ReviewCamera", camera_data)
    lookdev.objects.link(camera)
    scene.camera = camera
    camera.data.lens = 72
    root_output = root / OUTPUT_DIR.relative_to(ROOT)
    root_output.mkdir(parents=True, exist_ok=True)
    return camera


def aim(camera: Any, location: tuple[float, float, float], target: tuple[float, float, float], ortho_scale: float = 2.0) -> None:
    camera.location = location
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = ortho_scale
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()


def render_one(scene: Any, root: Path, filename: str, camera: Any, location: tuple[float, float, float], target: tuple[float, float, float], ortho_scale: float = 2.0) -> None:
    aim(camera, location, target, ortho_scale)
    scene.render.filepath = str(root / OUTPUT_DIR.relative_to(ROOT) / filename)
    bpy.ops.render.render(write_still=True)


def lower_arms_for_static_review(obj: Any) -> None:
    """Create a relaxed, unbound review pose without editing the authority."""
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
    obj.data.update()


def build_natural_review_variant() -> tuple[list[Any], list[Any], Any]:
    """Duplicate the six visible meshes into a render/export-only collection."""
    review = bpy.data.collections.get("ReviewNatural")
    if review is not None:
        for obj in list(review.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
        bpy.data.collections.remove(review)
    review = bpy.data.collections.new("ReviewNatural")
    bpy.context.scene.collection.children.link(review)
    review_root = bpy.data.objects.new("ProtagonistNaturalReviewRoot", None)
    review_root["town_rpg_review_only"] = True
    review.objects.link(review_root)
    sources = [
        bpy.data.objects.get(name)
        for name in ("Body", "Shirt", "Trousers", "Shoes", "Hair", "Eyes")
    ]
    if any(source is None for source in sources):
        raise RuntimeError("Natural review variant is missing a required source mesh")
    duplicates: list[Any] = []
    for source in sources:
        duplicate = source.copy()
        duplicate.data = source.data.copy()
        duplicate.name = f"Review_{source.name}"
        duplicate.data.name = f"{duplicate.name}Mesh"
        duplicate.parent = review_root
        duplicate.matrix_parent_inverse = Matrix.Identity(4)
        duplicate.matrix_world = source.matrix_world.copy()
        review.objects.link(duplicate)
        if source.name in {"Body", "Shirt"}:
            lower_arms_for_static_review(duplicate)
        duplicate["town_rpg_review_only"] = True
        duplicates.append(duplicate)
    for name, location in (("HeadAnchor", (0.0, 0.0, 1.675)), ("FeetAnchor", (0.0, 0.0, 0.0))):
        existing = bpy.data.objects.get(name)
        if existing is not None:
            existing.name = f"Author_{name}"
        anchor = bpy.data.objects.new(name, None)
        anchor.location = location
        anchor.parent = review_root
        review.objects.link(anchor)
    for source in sources:
        source.hide_render = True
    return duplicates, sources, review_root


def export_natural_review_glb(root: Path, duplicates: Sequence[Any], review_root: Any) -> Path:
    output = root / OUTPUT_DIR.relative_to(ROOT) / "protagonist-natural-review.glb"
    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    review_root.select_set(True)
    for duplicate in duplicates:
        duplicate.select_set(True)
    for name in ("HeadAnchor", "FeetAnchor"):
        anchor = bpy.data.objects.get(name)
        if anchor is None:
            raise RuntimeError(f"Natural review export is missing {name}")
        anchor.select_set(True)
    bpy.context.view_layer.objects.active = review_root
    result = bpy.ops.export_scene.gltf(
        filepath=str(output),
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
        raise RuntimeError(f"Natural review GLB export failed: {result}")
    return output


def render_reviews(root: Path) -> None:
    require_blender()
    blend = root / BLEND.relative_to(ROOT)
    if not blend.is_file():
        raise RuntimeError(f"Authoritative Blend is missing: {blend}")
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    scene = bpy.context.scene
    camera = setup_render(scene, root)
    # A-pose orthographic-like long lens views.
    render_one(scene, root, "01-a-pose-front-1080p.png", camera, (0, -4.5, 0.88), (0, 0, 0.88), 3.40)
    render_one(scene, root, "02-a-pose-side-1080p.png", camera, (4.5, 0, 0.88), (0, 0, 0.88), 3.40)
    render_one(scene, root, "03-a-pose-back-1080p.png", camera, (0, 4.5, 0.88), (0, 0, 0.88), 3.40)
    # Identity close-ups.
    render_one(scene, root, "04-face-front-1080p.png", camera, (0, -1.35, 1.58), (0, -0.01, 1.58), 0.38)
    render_one(scene, root, "05-face-three-quarter-1080p.png", camera, (0.86, -1.08, 1.59), (0, 0, 1.58), 0.38)
    render_one(scene, root, "06-face-side-1080p.png", camera, (1.35, 0, 1.58), (0, 0, 1.58), 0.38)
    # Material/detail review.
    render_one(scene, root, "07-shirt-detail-1080p.png", camera, (0.36, -1.35, 1.15), (0, 0, 1.18), 0.78)
    render_one(scene, root, "08-shoe-detail-1080p.png", camera, (0.52, -0.90, 0.16), (0.08, 0, 0.08), 0.32)
    render_one(scene, root, "13-hair-three-quarter-1080p.png", camera, (0.72, -0.88, 1.64), (0, 0, 1.62), 0.42)
    render_one(scene, root, "14-hair-back-1080p.png", camera, (0, 1.25, 1.63), (0, 0, 1.61), 0.42)
    render_one(scene, root, "15-collar-pocket-detail-1080p.png", camera, (-0.28, -1.26, 1.23), (-0.03, 0, 1.24), 0.60)
    render_one(scene, root, "16-sleeve-detail-1080p.png", camera, (1.02, -0.94, 1.26), (0.25, 0, 1.24), 0.44)

    # Four genuinely relaxed review angles.  The authority remains an unbound
    # A-pose; only duplicated mesh data is deformed and exported to outputs/.
    duplicates, original_sources, review_root = build_natural_review_variant()
    render_one(scene, root, "09-stance-front-1080p.png", camera, (0, -3.6, 0.92), (0, 0, 0.86), 3.40)
    render_one(scene, root, "10-stance-left-1080p.png", camera, (-3.4, -0.8, 0.94), (0, 0, 0.86), 3.40)
    render_one(scene, root, "11-stance-back-1080p.png", camera, (0, 3.6, 0.92), (0, 0, 0.86), 3.40)
    render_one(scene, root, "12-stance-right-1080p.png", camera, (3.4, -0.8, 0.94), (0, 0, 0.86), 3.40)
    review_glb = export_natural_review_glb(root, duplicates, review_root)
    for source in original_sources:
        source.hide_render = False
    print(
        "PROTAGONIST_V2_RENDER_PASS "
        f"files=16 review_glb={review_glb} output={root / OUTPUT_DIR.relative_to(ROOT)}"
    )


def report(root: Path) -> None:
    require_blender()
    blend = root / BLEND.relative_to(ROOT)
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    required = [bpy.data.objects.get(name) for name in ("Body", "Shirt", "Trousers", "Shoes", "Hair", "Eyes")]
    if any(obj is None for obj in required):
        raise RuntimeError("One or more required v2 visible meshes are missing")
    dependency_graph = bpy.context.evaluated_depsgraph_get()
    triangles = 0
    base_triangles = 0
    evaluated_points: list[Vector] = []
    for obj in required:
        base_triangles += sum(max(0, len(poly.vertices) - 2) for poly in obj.data.polygons)
        evaluated = obj.evaluated_get(dependency_graph)
        mesh = evaluated.to_mesh(
            preserve_all_data_layers=True,
            depsgraph=dependency_graph,
        )
        try:
            mesh.calc_loop_triangles()
            triangles += len(mesh.loop_triangles)
            evaluated_points.extend(
                evaluated.matrix_world @ vertex.co for vertex in mesh.vertices
            )
        finally:
            evaluated.to_mesh_clear()
    lower = Vector(
        (
            min(point.x for point in evaluated_points),
            min(point.y for point in evaluated_points),
            min(point.z for point in evaluated_points),
        )
    )
    upper = Vector(
        (
            max(point.x for point in evaluated_points),
            max(point.y for point in evaluated_points),
            max(point.z for point in evaluated_points),
        )
    )
    print(
        "PROTAGONIST_V2_REPORT "
        f"height={upper.z-lower.z:.4f} triangles={triangles} "
        f"baseTriangles={base_triangles} meshes={len(required)} "
        f"materials={len({m.name for obj in required for m in obj.data.materials if m})} "
        f"armatures={len([o for o in bpy.data.objects if o.type == 'ARMATURE'])} actions={len(bpy.data.actions)} "
        f"blend_sha256={sha256(blend)}"
    )


def main() -> None:
    args = parse_args()
    root = Path(args.root).resolve()
    if args.mode == "texture-build":
        build_textures(root)
    elif args.mode == "bootstrap":
        bootstrap(root)
    elif args.mode == "refine":
        refine_authoritative_blend(root)
    elif args.mode == "topology-fix":
        fix_authoritative_topology(root)
    elif args.mode == "export-mesh-fix":
        fix_export_body_mesh(root)
    elif args.mode == "visual-fix":
        fix_static_visual_candidate(root)
    elif args.mode == "visual-refine":
        refine_static_visual_candidate(root)
    elif args.mode == "visual-rebuild":
        rebuild_static_body_from_source(root)
    elif args.mode == "visual-eyes":
        reseat_static_eyes(root)
    elif args.mode == "visual-polish":
        polish_static_visual_candidate(root)
    elif args.mode == "visual-finalize":
        finalize_static_visual_candidate(root)
    elif args.mode == "visual-resolve":
        resolve_static_visual_candidate(root)
    elif args.mode == "visual-correct":
        correct_static_visual_candidate(root)
    elif args.mode == "visual-seal":
        seal_static_visual_candidate(root)
    elif args.mode == "visual-surface":
        surface_static_visual_candidate(root)
    elif args.mode == "visual-garment":
        garment_static_visual_candidate(root)
    elif args.mode == "visual-tailor":
        tailor_static_visual_candidate(root)
    elif args.mode == "visual-cuffs":
        cuff_static_visual_candidate(root)
    elif args.mode == "visual-topology-clean":
        clean_static_visual_topology(root)
    elif args.mode == "render":
        render_reviews(root)
    elif args.mode == "report":
        report(root)


if __name__ == "__main__":
    main()
