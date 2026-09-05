#!/usr/bin/env python3
"""Export and validate the hand-authored protagonist art pass v2.

The v2 Blend is the sole authoring source.  This tool intentionally has no
bootstrap or model-building mode: ``art-export`` opens the authored Blend and
writes the tracked GLB/manifest, while ``art-check`` exports only to a temporary
directory and must not modify repository files.

Run with Blender 5.2 after ``--``::

    blender --background --python-exit-code 1 --python scripts/tools/build_protagonist_art_pass_v2.py -- art-export --root .
    blender --background --python-exit-code 1 --python scripts/tools/build_protagonist_art_pass_v2.py -- art-check --root .
"""

from __future__ import annotations

import argparse
from collections import Counter
import hashlib
import json
import math
from pathlib import Path
import re
import struct
import sys
import tempfile
from typing import Any, Iterable

import bpy
from mathutils import Vector


BLENDER_VERSION = (5, 2, 0)
ASSET_ID = "protagonist.art_pass_v2"
SOURCE_RELATIVE = Path("art/blender/characters/protagonist-art-pass-v2.blend")
TEXTURE_DIRECTORY_RELATIVE = Path("art/blender/characters/textures-v2")
GLB_RELATIVE = Path("assets/generated/characters/3d/protagonist-art-pass-v2.glb")
MANIFEST_RELATIVE = Path(
    "assets/generated/characters/3d/protagonist-art-pass-v2-manifest.json"
)
SCHEMA_RELATIVE = Path(
    "scripts/tools/schemas/protagonist-art-pass-v2-manifest.schema.json"
)

ROOT_COLLECTION = "Character"
REQUIRED_CHILD_COLLECTIONS = {
    "Body",
    "Garments",
    "Hair",
    "Accessories",
    "Lookdev",
}
REQUIRED_MESH_COLLECTIONS = {
    "Body": {"Body", "Eyes"},
    "Garments": {"Shirt", "Trousers", "Shoes"},
    "Hair": {"Hair"},
}
REQUIRED_MESH_OBJECTS = {
    "Body",
    "Shirt",
    "Trousers",
    "Shoes",
    "Hair",
    "Eyes",
}
REQUIRED_EMPTY_OBJECTS = {
    "ProtagonistRoot",
    "HeadAnchor",
    "FeetAnchor",
}
REQUIRED_OBJECTS = REQUIRED_MESH_OBJECTS | REQUIRED_EMPTY_OBJECTS
EXPECTED_TEXTURES = {
    "albedo": "protagonist-art-pass-v2-albedo.png",
    "normal": "protagonist-art-pass-v2-normal.png",
    "orm": "protagonist-art-pass-v2-orm.png",
}

MIN_HEIGHT_METERS = 1.67
MAX_HEIGHT_METERS = 1.71
MIN_TRIANGLES = 30_000
MAX_TRIANGLES = 36_000
MAX_VISIBLE_MESHES = 6
MAX_MATERIAL_SURFACES = 5
TEXTURE_SIZE = 2048
MAX_GLB_BYTES = 20 * 1024 * 1024
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
GLB_MAGIC = b"glTF"
GLB_VERSION = 2
GLB_JSON_CHUNK = 0x4E4F534A
GLB_BINARY_CHUNK = 0x004E4942


def parse_args() -> argparse.Namespace:
    raw = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=("art-export", "art-check"))
    parser.add_argument("--root", required=True)
    return parser.parse_args(raw)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def require_file(path: Path, label: str) -> None:
    if not path.is_file():
        raise RuntimeError(f"{label} is missing: {path}")


def _collection_tree(collection: bpy.types.Collection) -> list[bpy.types.Collection]:
    result = [collection]
    for child in collection.children:
        result.extend(_collection_tree(child))
    return result


def _collection_objects(collection: bpy.types.Collection) -> set[bpy.types.Object]:
    return {
        obj
        for nested in _collection_tree(collection)
        for obj in nested.objects
    }


def _object_is_in_collection(obj: bpy.types.Object, collection: bpy.types.Collection) -> bool:
    return any(
        any(candidate == obj for candidate in nested.objects)
        for nested in _collection_tree(collection)
    )


def _resolve_blender_path(path: str, library: Any = None) -> Path:
    return Path(bpy.path.abspath(path, library=library)).resolve()


def _missing_external_files() -> list[str]:
    missing: set[str] = set()

    for library in bpy.data.libraries:
        if library.filepath:
            path = _resolve_blender_path(library.filepath, library.library)
            if not path.is_file():
                missing.add(str(path))

    for image in bpy.data.images:
        if image.source not in {"FILE", "SEQUENCE", "MOVIE"} or image.packed_file:
            continue
        if not image.filepath:
            missing.add(f"image:{image.name}:empty-filepath")
            continue
        path = _resolve_blender_path(image.filepath, image.library)
        if not path.is_file():
            missing.add(str(path))

    for datablocks in (bpy.data.movieclips, bpy.data.sounds, bpy.data.volumes):
        for datablock in datablocks:
            if not datablock.filepath:
                continue
            path = _resolve_blender_path(datablock.filepath, datablock.library)
            if not path.is_file():
                missing.add(str(path))

    for font in bpy.data.fonts:
        if not font.filepath or font.filepath == "<builtin>":
            continue
        path = _resolve_blender_path(font.filepath, font.library)
        if not path.is_file():
            missing.add(str(path))

    return sorted(missing)


def _png_metadata(path: Path) -> dict[str, int | str]:
    with path.open("rb") as handle:
        signature = handle.read(8)
        length_bytes = handle.read(4)
        chunk_type = handle.read(4)
        if signature != PNG_SIGNATURE or len(length_bytes) != 4 or chunk_type != b"IHDR":
            raise RuntimeError(f"Texture is not a valid PNG with an IHDR header: {path}")
        length = struct.unpack(">I", length_bytes)[0]
        payload = handle.read(length)
    if length != 13 or len(payload) != 13:
        raise RuntimeError(f"Texture has an invalid PNG IHDR payload: {path}")
    width, height, bit_depth, color_type, compression, filtering, interlace = struct.unpack(
        ">IIBBBBB", payload
    )
    if compression != 0 or filtering != 0 or interlace not in {0, 1}:
        raise RuntimeError(f"Texture uses an unsupported PNG encoding: {path}")
    channels_by_color_type = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}
    channels = channels_by_color_type.get(color_type, 0)
    return {
        "width": width,
        "height": height,
        "bitDepth": bit_depth,
        "colorType": color_type,
        "channels": channels,
        "pixelFormat": "RGBA" if color_type == 6 else f"PNG-color-type-{color_type}",
    }


def _used_materials(mesh_objects: Iterable[bpy.types.Object]) -> list[bpy.types.Material]:
    materials: dict[str, bpy.types.Material] = {}
    for obj in mesh_objects:
        for slot in obj.material_slots:
            if slot.material is not None:
                materials[slot.material.name] = slot.material
    return [materials[name] for name in sorted(materials)]


def _image_nodes(
    node_tree: bpy.types.NodeTree,
    visited: set[int] | None = None,
) -> Iterable[bpy.types.Node]:
    visited = visited or set()
    pointer = node_tree.as_pointer()
    if pointer in visited:
        return
    visited.add(pointer)
    for node in node_tree.nodes:
        if node.type == "TEX_IMAGE" and node.image is not None:
            yield node
        nested = getattr(node, "node_tree", None)
        if nested is not None:
            yield from _image_nodes(nested, visited)


def _used_images(materials: Iterable[bpy.types.Material]) -> dict[Path, set[str]]:
    result: dict[Path, set[str]] = {}
    for material in materials:
        if material.node_tree is None:
            continue
        for node in _image_nodes(material.node_tree):
            if not node.image.filepath:
                continue
            path = _resolve_blender_path(node.image.filepath, node.image.library)
            result.setdefault(path, set()).add(material.name)
    return result


def _validate_textures(
    root: Path, materials: Iterable[bpy.types.Material]
) -> list[dict[str, Any]]:
    materials = list(materials)
    material_images = _used_images(materials)
    expected_paths = {
        (root / TEXTURE_DIRECTORY_RELATIVE / filename).resolve()
        for filename in EXPECTED_TEXTURES.values()
    }
    packed_images: set[str] = set()
    pathless_images: set[str] = set()
    for material in materials:
        if material.node_tree is None:
            continue
        for node in _image_nodes(material.node_tree):
            image = node.image
            if image.packed_file:
                packed_images.add(f"{material.name}:{image.name}")
            if not image.filepath:
                pathless_images.add(f"{material.name}:{image.name}")
    if packed_images:
        raise RuntimeError(
            "Exported protagonist materials must use the tracked external texture set, "
            "not packed image copies: " + ", ".join(sorted(packed_images))
        )
    if pathless_images:
        raise RuntimeError(
            "Exported protagonist materials contain image nodes without tracked paths: "
            + ", ".join(sorted(pathless_images))
        )
    unexpected_paths = sorted(set(material_images) - expected_paths)
    if unexpected_paths:
        raise RuntimeError(
            "Exported protagonist materials reference textures outside the single tracked "
            "Albedo/Normal/ORM set: "
            + ", ".join(str(path) for path in unexpected_paths)
        )
    reports: list[dict[str, Any]] = []
    for role, filename in EXPECTED_TEXTURES.items():
        path = (root / TEXTURE_DIRECTORY_RELATIVE / filename).resolve()
        require_file(path, f"Protagonist {role} texture")
        metadata = _png_metadata(path)
        if metadata["width"] != TEXTURE_SIZE or metadata["height"] != TEXTURE_SIZE:
            raise RuntimeError(
                f"Protagonist {role} texture must be {TEXTURE_SIZE}x{TEXTURE_SIZE}: "
                f"{metadata['width']}x{metadata['height']}"
            )
        if metadata["bitDepth"] != 8 or metadata["pixelFormat"] != "RGBA":
            raise RuntimeError(
                f"Protagonist {role} texture must be 8-bit RGBA PNG: "
                f"{metadata['pixelFormat']} {metadata['bitDepth']}-bit"
            )
        if path not in material_images:
            raise RuntimeError(
                f"Protagonist {role} texture is not referenced by an exported material: {filename}"
            )
        loaded_images = [
            node.image
            for material in materials
            if material.node_tree is not None
            for node in _image_nodes(material.node_tree)
            if node.image.filepath
            and _resolve_blender_path(node.image.filepath, node.image.library) == path
        ]
        color_spaces = sorted({image.colorspace_settings.name for image in loaded_images})
        expected_color_space = "sRGB" if role == "albedo" else "Non-Color"
        if color_spaces != [expected_color_space]:
            raise RuntimeError(
                f"Protagonist {role} texture color space must be {expected_color_space}, "
                f"got {color_spaces}"
            )
        reports.append(
            {
                "role": role,
                "path": (TEXTURE_DIRECTORY_RELATIVE / filename).as_posix(),
                "sha256": sha256(path),
                **metadata,
                "colorSpace": expected_color_space,
                "usedByMaterials": sorted(material_images[path]),
            }
        )
    return reports


def _evaluated_mesh_report(
    obj: bpy.types.Object, dependency_graph: bpy.types.Depsgraph
) -> tuple[dict[str, Any], list[Vector]]:
    evaluated = obj.evaluated_get(dependency_graph)
    mesh = evaluated.to_mesh(preserve_all_data_layers=True, depsgraph=dependency_graph)
    try:
        if not mesh.polygons:
            raise RuntimeError(f"Exported protagonist mesh has no faces: {obj.name}")
        if not mesh.vertices:
            raise RuntimeError(f"Exported protagonist mesh has no vertices: {obj.name}")

        zero_area_faces = sum(1 for polygon in mesh.polygons if polygon.area <= 1.0e-10)
        if zero_area_faces:
            raise RuntimeError(
                f"Exported protagonist mesh has zero-area faces: {obj.name} ({zero_area_faces})"
            )
        edge_use = Counter(
            tuple(sorted(edge_key))
            for polygon in mesh.polygons
            for edge_key in polygon.edge_keys
        )
        boundary_edges = sum(1 for uses in edge_use.values() if uses == 1)
        loose_edges = sum(
            1
            for edge in mesh.edges
            if edge_use[tuple(sorted(edge.vertices))] == 0
        )
        non_manifold_edges = sum(1 for uses in edge_use.values() if uses > 2)
        if boundary_edges or loose_edges or non_manifold_edges:
            raise RuntimeError(
                f"Exported protagonist mesh must be closed and manifold: {obj.name} "
                f"boundaryEdges={boundary_edges} looseEdges={loose_edges} "
                f"nonManifoldEdges={non_manifold_edges}"
            )
        used_vertices = {
            index for polygon in mesh.polygons for index in polygon.vertices
        }
        loose_vertices = len(mesh.vertices) - len(used_vertices)
        if loose_vertices:
            raise RuntimeError(
                f"Exported protagonist mesh has loose vertices: {obj.name} ({loose_vertices})"
            )

        uv_layer = mesh.uv_layers.active
        if uv_layer is None or len(uv_layer.data) != len(mesh.loops):
            raise RuntimeError(f"Exported protagonist mesh has no complete UV map: {obj.name}")
        uvs = [tuple(loop.uv) for loop in uv_layer.data]
        if any(not math.isfinite(component) for uv in uvs for component in uv):
            raise RuntimeError(f"Exported protagonist mesh has non-finite UVs: {obj.name}")
        if any(component < -0.0001 or component > 1.0001 for uv in uvs for component in uv):
            raise RuntimeError(f"Exported protagonist UVs leave the 0..1 atlas: {obj.name}")
        if len({(round(uv[0], 6), round(uv[1], 6)) for uv in uvs}) < 3:
            raise RuntimeError(f"Exported protagonist UV map is collapsed: {obj.name}")

        if not mesh.materials or any(material is None for material in mesh.materials):
            raise RuntimeError(f"Exported protagonist mesh has no material: {obj.name}")
        if any(polygon.material_index >= len(mesh.materials) for polygon in mesh.polygons):
            raise RuntimeError(f"Exported protagonist mesh has an invalid material index: {obj.name}")
        if any(mesh.materials[polygon.material_index] is None for polygon in mesh.polygons):
            raise RuntimeError(f"Exported protagonist mesh uses an empty material slot: {obj.name}")
        used_material_indices = {polygon.material_index for polygon in mesh.polygons}
        unused_material_indices = sorted(set(range(len(mesh.materials))) - used_material_indices)
        if unused_material_indices:
            raise RuntimeError(
                f"Exported protagonist mesh contains unused material slots: {obj.name} "
                f"indices={unused_material_indices}"
            )
        material_slots = [mesh.materials[index] for index in sorted(used_material_indices)]

        mesh.calc_loop_triangles()
        polygons_by_edge: dict[tuple[int, int], list[int]] = {}
        for polygon in mesh.polygons:
            for edge_key in polygon.edge_keys:
                polygons_by_edge.setdefault(tuple(sorted(edge_key)), []).append(
                    polygon.index
                )
        polygon_neighbours: dict[int, set[int]] = {
            polygon.index: set() for polygon in mesh.polygons
        }
        for polygon_indices in polygons_by_edge.values():
            first, second = polygon_indices
            polygon_neighbours[first].add(second)
            polygon_neighbours[second].add(first)
        components: list[set[int]] = []
        unvisited = set(polygon_neighbours)
        while unvisited:
            pending = [unvisited.pop()]
            component: set[int] = set()
            while pending:
                polygon_index = pending.pop()
                if polygon_index in component:
                    continue
                component.add(polygon_index)
                neighbours = polygon_neighbours[polygon_index] & unvisited
                unvisited.difference_update(neighbours)
                pending.extend(neighbours)
            components.append(component)

        triangles_by_polygon: dict[int, list[bpy.types.MeshLoopTriangle]] = {}
        for triangle in mesh.loop_triangles:
            triangles_by_polygon.setdefault(triangle.polygon_index, []).append(triangle)
        signed_component_volumes: list[float] = []
        for component in components:
            signed_volume = 0.0
            for polygon_index in component:
                for triangle in triangles_by_polygon.get(polygon_index, []):
                    p0, p1, p2 = (
                        evaluated.matrix_world @ mesh.vertices[index].co
                        for index in triangle.vertices
                    )
                    signed_volume += p0.dot(p1.cross(p2)) / 6.0
            if signed_volume <= 1.0e-12:
                orientation = "inward" if signed_volume < -1.0e-12 else "zero-volume"
                raise RuntimeError(
                    f"Exported protagonist mesh component has {orientation} winding: "
                    f"{obj.name} signedVolume={signed_volume:.12g}"
                )
            signed_component_volumes.append(signed_volume)

        triangle_count = len(mesh.loop_triangles)
        world_points = [evaluated.matrix_world @ vertex.co for vertex in mesh.vertices]
        return (
            {
                "name": obj.name,
                "vertexCount": len(mesh.vertices),
                "polygonCount": len(mesh.polygons),
                "triangleCount": triangle_count,
                "boundaryEdgeCount": boundary_edges,
                "nonManifoldEdgeCount": non_manifold_edges,
                "looseEdgeCount": loose_edges,
                "looseVertexCount": loose_vertices,
                "zeroAreaFaceCount": zero_area_faces,
                "closedComponentCount": len(components),
                "minimumSignedComponentVolumeMeters3": round(
                    min(signed_component_volumes), 12
                ),
                "uvLayer": uv_layer.name,
                "materialNames": sorted({material.name for material in material_slots}),
            },
            world_points,
        )
    finally:
        evaluated.to_mesh_clear()


def _validate_hierarchy() -> tuple[bpy.types.Collection, list[bpy.types.Object]]:
    character = bpy.data.collections.get(ROOT_COLLECTION)
    if character is None:
        raise RuntimeError(f"Required protagonist collection is missing: {ROOT_COLLECTION}")
    direct_children = {collection.name: collection for collection in character.children}
    missing_collections = sorted(REQUIRED_CHILD_COLLECTIONS - direct_children.keys())
    if missing_collections:
        raise RuntimeError(
            "Required direct Character child collections are missing: "
            + ", ".join(missing_collections)
        )

    names = set(bpy.data.objects.keys())
    missing_objects = sorted(REQUIRED_OBJECTS - names)
    if missing_objects:
        raise RuntimeError(
            "Required protagonist objects are missing: " + ", ".join(missing_objects)
        )
    for collection_name, object_names in REQUIRED_MESH_COLLECTIONS.items():
        collection = direct_children[collection_name]
        for object_name in object_names:
            if not _object_is_in_collection(bpy.data.objects[object_name], collection):
                raise RuntimeError(
                    f"Protagonist object {object_name} is not inside {collection_name}"
                )
    accessories = direct_children["Accessories"]
    for object_name in REQUIRED_EMPTY_OBJECTS:
        if not _object_is_in_collection(bpy.data.objects[object_name], accessories):
            raise RuntimeError(
                f"Protagonist object {object_name} is not inside Accessories"
            )

    root = bpy.data.objects["ProtagonistRoot"]
    if root.type != "EMPTY" or root.parent is not None:
        raise RuntimeError("ProtagonistRoot must be an unparented Empty")
    for name in ("HeadAnchor", "FeetAnchor"):
        anchor = bpy.data.objects[name]
        if anchor.type != "EMPTY" or anchor.parent != root:
            raise RuntimeError(f"{name} must be an Empty parented to ProtagonistRoot")

    for name in REQUIRED_MESH_OBJECTS:
        obj = bpy.data.objects[name]
        if obj.type != "MESH" or obj.parent != root:
            raise RuntimeError(f"{name} must be a Mesh parented to ProtagonistRoot")
        if obj.hide_render or obj.hide_viewport:
            raise RuntimeError(f"Required protagonist mesh is hidden: {name}")

    lookdev_objects = _collection_objects(direct_children["Lookdev"])
    exported_objects = sorted(
        (
            obj
            for obj in _collection_objects(character)
            if obj not in lookdev_objects
            and (obj.name in REQUIRED_OBJECTS)
        ),
        key=lambda obj: obj.name,
    )
    exported_names = {obj.name for obj in exported_objects}
    if exported_names != REQUIRED_OBJECTS:
        raise RuntimeError(
            "Character export set does not match required objects: "
            f"expected={sorted(REQUIRED_OBJECTS)} actual={sorted(exported_names)}"
        )
    extra_visible_meshes = sorted(
        obj.name
        for obj in _collection_objects(character)
        if obj not in lookdev_objects
        and obj.type == "MESH"
        and obj.name not in REQUIRED_MESH_OBJECTS
        and not obj.hide_render
    )
    if extra_visible_meshes:
        raise RuntimeError(
            "Character contains additional visible export meshes: "
            + ", ".join(extra_visible_meshes)
        )
    return character, exported_objects


def _validate_static_only() -> None:
    if bpy.data.armatures or any(obj.type == "ARMATURE" for obj in bpy.data.objects):
        raise RuntimeError("Static art candidate must not contain an Armature")
    armature_modifiers = sorted(
        obj.name
        for obj in bpy.data.objects
        if any(modifier.type == "ARMATURE" for modifier in obj.modifiers)
    )
    if armature_modifiers:
        raise RuntimeError(
            "Static art candidate contains Armature modifiers: "
            + ", ".join(armature_modifiers)
        )
    if bpy.data.actions:
        raise RuntimeError("Static art candidate must not contain Actions")


def validate_scene(root: Path) -> dict[str, Any]:
    if bpy.app.version[:3] != BLENDER_VERSION:
        raise RuntimeError(
            f"Expected Blender {BLENDER_VERSION}, got {bpy.app.version[:3]}"
        )
    scene = bpy.context.scene
    if scene.unit_settings.system != "METRIC" or not math.isclose(
        scene.unit_settings.scale_length, 1.0, abs_tol=1.0e-6
    ):
        raise RuntimeError("Protagonist Blend must use metric units at 1 Blender unit = 1 meter")
    if scene.get("town_rpg_asset") != ASSET_ID:
        raise RuntimeError(
            f"Blend is not tagged with town_rpg_asset={ASSET_ID}"
        )
    if scene.get("town_rpg_source_authority") != "blend":
        raise RuntimeError("Blend must be tagged with town_rpg_source_authority=blend")

    _validate_static_only()
    _character, exported_objects = _validate_hierarchy()
    mesh_objects = [obj for obj in exported_objects if obj.type == "MESH"]
    if len(mesh_objects) > MAX_VISIBLE_MESHES:
        raise RuntimeError(
            f"Protagonist visible mesh budget exceeded: {len(mesh_objects)}"
        )

    dependency_graph = bpy.context.evaluated_depsgraph_get()
    mesh_reports: list[dict[str, Any]] = []
    world_points: list[Vector] = []
    mesh_errors: list[str] = []
    for obj in mesh_objects:
        try:
            report, points = _evaluated_mesh_report(obj, dependency_graph)
            mesh_reports.append(report)
            world_points.extend(points)
        except RuntimeError as error:
            mesh_errors.append(str(error))
    if mesh_errors:
        raise RuntimeError(
            "Protagonist mesh validation failed: " + " | ".join(mesh_errors)
        )
    mesh_reports.sort(key=lambda item: item["name"])
    triangle_count = sum(report["triangleCount"] for report in mesh_reports)
    if not MIN_TRIANGLES <= triangle_count <= MAX_TRIANGLES:
        raise RuntimeError(
            f"Static protagonist must contain {MIN_TRIANGLES}-{MAX_TRIANGLES} evaluated "
            f"triangles, got {triangle_count}"
        )

    minimum = Vector(
        (
            min(point.x for point in world_points),
            min(point.y for point in world_points),
            min(point.z for point in world_points),
        )
    )
    maximum = Vector(
        (
            max(point.x for point in world_points),
            max(point.y for point in world_points),
            max(point.z for point in world_points),
        )
    )
    height = maximum.z - minimum.z
    if not MIN_HEIGHT_METERS <= height <= MAX_HEIGHT_METERS:
        raise RuntimeError(
            f"Static protagonist height must be {MIN_HEIGHT_METERS:.2f}-"
            f"{MAX_HEIGHT_METERS:.2f}m, got {height:.4f}m"
        )

    materials = _used_materials(mesh_objects)
    if not materials or len(materials) > MAX_MATERIAL_SURFACES:
        raise RuntimeError(
            f"Static protagonist must use 1-{MAX_MATERIAL_SURFACES} material surfaces, "
            f"got {len(materials)}"
        )
    textures = _validate_textures(root, materials)
    missing_external_files = _missing_external_files()
    if missing_external_files:
        raise RuntimeError(
            "Protagonist Blend has missing external files: "
            + ", ".join(missing_external_files)
        )

    head_anchor = bpy.data.objects["HeadAnchor"].matrix_world.translation
    feet_anchor = bpy.data.objects["FeetAnchor"].matrix_world.translation
    if not minimum.z - 0.03 <= feet_anchor.z <= minimum.z + 0.12:
        raise RuntimeError("FeetAnchor is not aligned with the character's feet")
    if not maximum.z - 0.30 <= head_anchor.z <= maximum.z + 0.03:
        raise RuntimeError("HeadAnchor is not aligned with the character's head")

    return {
        "blenderVersion": bpy.app.version_string,
        "bound": False,
        "animated": False,
        "heightMeters": round(height, 4),
        "worldBoundsMeters": {
            "minimum": [round(value, 5) for value in minimum],
            "maximum": [round(value, 5) for value in maximum],
        },
        "requiredCollections": [
            ROOT_COLLECTION,
            *sorted(REQUIRED_CHILD_COLLECTIONS),
        ],
        "requiredObjects": sorted(REQUIRED_OBJECTS),
        "exportedObjects": [obj.name for obj in exported_objects],
        "majorVisibleMeshCount": len(mesh_objects),
        "triangleCount": triangle_count,
        "materialSurfaceCount": len(materials),
        "materialNames": [material.name for material in materials],
        "topology": {
            "meshReports": mesh_reports,
            "boundaryEdgeCount": sum(
                report["boundaryEdgeCount"] for report in mesh_reports
            ),
            "nonManifoldEdgeCount": 0,
            "looseEdgeCount": 0,
            "looseVertexCount": 0,
            "zeroAreaFaceCount": 0,
            "closedComponentCount": sum(
                report["closedComponentCount"] for report in mesh_reports
            ),
            "minimumSignedComponentVolumeMeters3": round(
                min(
                    report["minimumSignedComponentVolumeMeters3"]
                    for report in mesh_reports
                ),
                12,
            ),
        },
        "textures": textures,
        "missingExternalFiles": [],
        "anchorsMeters": {
            "head": [round(value, 5) for value in head_anchor],
            "feet": [round(value, 5) for value in feet_anchor],
        },
    }


def _export_glb(path: Path, exported_objects: Iterable[bpy.types.Object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    objects = list(exported_objects)
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = bpy.data.objects["ProtagonistRoot"]
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
        raise RuntimeError(f"Protagonist v2 glTF export failed: {result}")
    if path.stat().st_size > MAX_GLB_BYTES:
        raise RuntimeError(
            f"Protagonist v2 GLB exceeds {MAX_GLB_BYTES} bytes: {path.stat().st_size}"
        )


def _read_glb(path: Path) -> tuple[dict[str, Any], bytes]:
    require_file(path, "Protagonist v2 GLB")
    payload = path.read_bytes()
    if len(payload) < 20:
        raise RuntimeError(f"Protagonist v2 GLB is truncated: {path}")
    magic, version, declared_length = struct.unpack_from("<4sII", payload, 0)
    if magic != GLB_MAGIC or version != GLB_VERSION:
        raise RuntimeError(
            f"Protagonist v2 GLB header is invalid: magic={magic!r} version={version}"
        )
    if declared_length != len(payload):
        raise RuntimeError(
            f"Protagonist v2 GLB length mismatch: declared={declared_length} "
            f"actual={len(payload)}"
        )

    chunks: list[tuple[int, bytes]] = []
    offset = 12
    while offset < len(payload):
        if offset + 8 > len(payload):
            raise RuntimeError("Protagonist v2 GLB contains a truncated chunk header")
        chunk_length, chunk_type = struct.unpack_from("<II", payload, offset)
        offset += 8
        chunk_end = offset + chunk_length
        if chunk_end > len(payload):
            raise RuntimeError("Protagonist v2 GLB contains a truncated chunk payload")
        chunks.append((chunk_type, payload[offset:chunk_end]))
        offset = chunk_end
    if not chunks or chunks[0][0] != GLB_JSON_CHUNK:
        raise RuntimeError("Protagonist v2 GLB must start with one JSON chunk")
    json_chunks = [chunk for chunk_type, chunk in chunks if chunk_type == GLB_JSON_CHUNK]
    binary_chunks = [chunk for chunk_type, chunk in chunks if chunk_type == GLB_BINARY_CHUNK]
    unknown_chunks = [
        chunk_type
        for chunk_type, _chunk in chunks
        if chunk_type not in {GLB_JSON_CHUNK, GLB_BINARY_CHUNK}
    ]
    if len(json_chunks) != 1 or len(binary_chunks) != 1 or unknown_chunks:
        raise RuntimeError(
            "Protagonist v2 GLB must contain exactly one JSON and one binary chunk"
        )
    try:
        document = json.loads(json_chunks[0].rstrip(b" \t\r\n\x00").decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        raise RuntimeError(f"Protagonist v2 GLB JSON is invalid: {error}") from error
    if not isinstance(document, dict):
        raise RuntimeError("Protagonist v2 GLB JSON root must be an object")
    return document, binary_chunks[0]


def _glb_index(items: list[Any], index: Any, label: str) -> Any:
    if not isinstance(index, int) or isinstance(index, bool) or not 0 <= index < len(items):
        raise RuntimeError(f"Protagonist v2 GLB has an invalid {label} index: {index!r}")
    return items[index]


def _glb_buffer_view_bytes(
    document: dict[str, Any], binary_chunk: bytes, buffer_view_index: Any
) -> bytes:
    buffer_views = document.get("bufferViews", [])
    if not isinstance(buffer_views, list):
        raise RuntimeError("Protagonist v2 GLB bufferViews must be an array")
    view = _glb_index(buffer_views, buffer_view_index, "bufferView")
    if not isinstance(view, dict) or view.get("buffer", 0) != 0:
        raise RuntimeError("Protagonist v2 GLB image must use the embedded buffer")
    start = view.get("byteOffset", 0)
    length = view.get("byteLength")
    if (
        not isinstance(start, int)
        or isinstance(start, bool)
        or not isinstance(length, int)
        or isinstance(length, bool)
        or start < 0
        or length < 1
        or start + length > len(binary_chunk)
    ):
        raise RuntimeError("Protagonist v2 GLB bufferView range is invalid")
    return binary_chunk[start : start + length]


def _inspect_glb(
    path: Path,
    summary: dict[str, Any],
) -> dict[str, Any]:
    document, binary_chunk = _read_glb(path)
    asset = document.get("asset")
    if not isinstance(asset, dict) or asset.get("version") != "2.0":
        raise RuntimeError("Protagonist v2 GLB asset.version must be 2.0")

    buffers = document.get("buffers", [])
    if not isinstance(buffers, list) or len(buffers) != 1:
        raise RuntimeError("Protagonist v2 GLB must contain one embedded buffer")
    buffer = buffers[0]
    if not isinstance(buffer, dict) or "uri" in buffer:
        raise RuntimeError("Protagonist v2 GLB buffer must be embedded")
    declared_binary_length = buffer.get("byteLength")
    if (
        not isinstance(declared_binary_length, int)
        or isinstance(declared_binary_length, bool)
        or declared_binary_length < 1
        or declared_binary_length > len(binary_chunk)
        or len(binary_chunk) - declared_binary_length > 3
    ):
        raise RuntimeError("Protagonist v2 GLB embedded buffer length is invalid")

    nodes = document.get("nodes", [])
    meshes = document.get("meshes", [])
    accessors = document.get("accessors", [])
    materials = document.get("materials", [])
    textures = document.get("textures", [])
    images = document.get("images", [])
    if not all(
        isinstance(items, list)
        for items in (nodes, meshes, accessors, materials, textures, images)
    ):
        raise RuntimeError("Protagonist v2 GLB structural tables must be arrays")

    node_names = [node.get("name") for node in nodes if isinstance(node, dict)]
    if len(node_names) != len(nodes) or set(node_names) != REQUIRED_OBJECTS:
        raise RuntimeError(
            "Protagonist v2 GLB node set does not match the required export objects: "
            f"actual={sorted(str(name) for name in node_names)}"
        )
    if len(nodes) != len(REQUIRED_OBJECTS) or len(set(node_names)) != len(node_names):
        raise RuntimeError("Protagonist v2 GLB contains duplicate or extra nodes")

    mesh_nodes = [node for node in nodes if isinstance(node, dict) and "mesh" in node]
    if (
        len(mesh_nodes) != summary["majorVisibleMeshCount"]
        or len(meshes) != len(mesh_nodes)
    ):
        raise RuntimeError(
            "Protagonist v2 GLB mesh count does not match the authored candidate"
        )
    expected_mesh_names = set(REQUIRED_MESH_OBJECTS)
    if {node["name"] for node in mesh_nodes} != expected_mesh_names:
        raise RuntimeError("Protagonist v2 GLB mesh node names are incomplete")

    runtime_triangle_count = 0
    referenced_materials: set[int] = set()
    for node in mesh_nodes:
        mesh = _glb_index(meshes, node["mesh"], "mesh")
        if not isinstance(mesh, dict) or not isinstance(mesh.get("primitives"), list):
            raise RuntimeError(f"Protagonist v2 GLB mesh is invalid: {node['name']}")
        for primitive in mesh["primitives"]:
            if not isinstance(primitive, dict) or primitive.get("mode", 4) != 4:
                raise RuntimeError("Protagonist v2 GLB meshes must use triangle primitives")
            attributes = primitive.get("attributes")
            if not isinstance(attributes, dict) or "POSITION" not in attributes:
                raise RuntimeError("Protagonist v2 GLB primitive has no POSITION attribute")
            position = _glb_index(accessors, attributes["POSITION"], "POSITION accessor")
            if not isinstance(position, dict) or position.get("type") != "VEC3":
                raise RuntimeError("Protagonist v2 GLB POSITION accessor must be VEC3")
            if "indices" in primitive:
                indices = _glb_index(accessors, primitive["indices"], "index accessor")
                count = indices.get("count") if isinstance(indices, dict) else None
            else:
                count = position.get("count")
            if not isinstance(count, int) or isinstance(count, bool) or count < 3 or count % 3:
                raise RuntimeError("Protagonist v2 GLB triangle accessor count is invalid")
            runtime_triangle_count += count // 3
            material_index = primitive.get("material")
            _glb_index(materials, material_index, "material")
            referenced_materials.add(material_index)
    if runtime_triangle_count != summary["triangleCount"]:
        raise RuntimeError(
            "Protagonist v2 GLB triangle count differs from the evaluated Blend: "
            f"glb={runtime_triangle_count} blend={summary['triangleCount']}"
        )
    if (
        len(referenced_materials) != summary["materialSurfaceCount"]
        or len(materials) != len(referenced_materials)
    ):
        raise RuntimeError(
            "Protagonist v2 GLB material count differs from the authored candidate"
        )

    expected_texture_hashes = {
        texture["role"]: texture["sha256"] for texture in summary["textures"]
    }
    embedded_images: list[dict[str, Any]] = []
    image_hashes: dict[int, str] = {}
    for index, image in enumerate(images):
        if (
            not isinstance(image, dict)
            or image.get("mimeType") != "image/png"
            or "uri" in image
        ):
            raise RuntimeError("Protagonist v2 GLB images must be embedded PNGs")
        image_bytes = _glb_buffer_view_bytes(document, binary_chunk, image.get("bufferView"))
        image_hash = hashlib.sha256(image_bytes).hexdigest()
        image_hashes[index] = image_hash
        roles = sorted(
            role
            for role, expected_hash in expected_texture_hashes.items()
            if expected_hash == image_hash
        )
        if len(roles) != 1:
            raise RuntimeError(
                "Protagonist v2 GLB embeds an unknown or duplicated texture payload"
            )
        embedded_images.append(
            {
                "role": roles[0],
                "name": str(image.get("name", "")),
                "mimeType": "image/png",
                "byteLength": len(image_bytes),
                "sha256": image_hash,
            }
        )
    if (
        len(images) != len(EXPECTED_TEXTURES)
        or len(textures) != len(EXPECTED_TEXTURES)
        or {item["role"] for item in embedded_images} != set(EXPECTED_TEXTURES)
    ):
        raise RuntimeError(
            "Protagonist v2 GLB must embed exactly the tracked Albedo/Normal/ORM set"
        )

    def texture_role(texture_info: Any, label: str) -> str:
        if not isinstance(texture_info, dict):
            raise RuntimeError(f"Protagonist v2 GLB material is missing {label}")
        texture_index = texture_info.get("index")
        texture = _glb_index(textures, texture_index, f"{label} texture")
        if not isinstance(texture, dict):
            raise RuntimeError(f"Protagonist v2 GLB {label} texture is invalid")
        source_index = texture.get("source")
        _glb_index(images, source_index, f"{label} image")
        image_hash = image_hashes[source_index]
        roles = [
            role
            for role, expected_hash in expected_texture_hashes.items()
            if expected_hash == image_hash
        ]
        return roles[0]

    for material in materials:
        if not isinstance(material, dict):
            raise RuntimeError("Protagonist v2 GLB material entry is invalid")
        pbr = material.get("pbrMetallicRoughness")
        if not isinstance(pbr, dict):
            raise RuntimeError("Protagonist v2 GLB material has no metallic-roughness PBR block")
        if texture_role(pbr.get("baseColorTexture"), "base color") != "albedo":
            raise RuntimeError("Protagonist v2 GLB base color texture is not the tracked albedo")
        if texture_role(pbr.get("metallicRoughnessTexture"), "metallic-roughness") != "orm":
            raise RuntimeError("Protagonist v2 GLB metallic-roughness texture is not the tracked ORM")
        if texture_role(material.get("normalTexture"), "normal") != "normal":
            raise RuntimeError("Protagonist v2 GLB normal texture is not the tracked normal map")

    if document.get("skins") or document.get("animations") or document.get("cameras"):
        raise RuntimeError("Static protagonist GLB must not contain skins, animations, or cameras")
    return {
        "glbVersion": GLB_VERSION,
        "nodeNames": sorted(node_names),
        "meshCount": len(meshes),
        "triangleCount": runtime_triangle_count,
        "materialCount": len(materials),
        "materialNames": sorted(str(material.get("name", "")) for material in materials),
        "embeddedImages": sorted(embedded_images, key=lambda item: item["role"]),
        "skinCount": len(document.get("skins", [])),
        "animationCount": len(document.get("animations", [])),
        "cameraCount": len(document.get("cameras", [])),
    }


def _load_schema(root: Path) -> tuple[Path, dict[str, Any]]:
    schema_path = root / SCHEMA_RELATIVE
    require_file(schema_path, "Protagonist v2 manifest schema")
    return schema_path, json.loads(schema_path.read_text(encoding="utf-8"))


def _json_type_matches(value: Any, expected: str) -> bool:
    if expected == "object":
        return isinstance(value, dict)
    if expected == "array":
        return isinstance(value, list)
    if expected == "string":
        return isinstance(value, str)
    if expected == "integer":
        return isinstance(value, int) and not isinstance(value, bool)
    if expected == "number":
        return isinstance(value, (int, float)) and not isinstance(value, bool)
    if expected == "boolean":
        return isinstance(value, bool)
    if expected == "null":
        return value is None
    raise RuntimeError(f"Unsupported manifest schema type: {expected}")


def _validate_json_schema(value: Any, schema: dict[str, Any], path: str = "$") -> None:
    expected_type = schema.get("type")
    if expected_type is not None and not _json_type_matches(value, expected_type):
        raise RuntimeError(
            f"Manifest schema mismatch at {path}: expected {expected_type}, "
            f"got {type(value).__name__}"
        )
    if "const" in schema and value != schema["const"]:
        raise RuntimeError(
            f"Manifest schema mismatch at {path}: expected {schema['const']!r}"
        )
    if "enum" in schema and value not in schema["enum"]:
        raise RuntimeError(
            f"Manifest schema mismatch at {path}: {value!r} not in {schema['enum']!r}"
        )
    if isinstance(value, (int, float)) and not isinstance(value, bool):
        if "minimum" in schema and value < schema["minimum"]:
            raise RuntimeError(f"Manifest value below minimum at {path}: {value}")
        if "maximum" in schema and value > schema["maximum"]:
            raise RuntimeError(f"Manifest value above maximum at {path}: {value}")
    if isinstance(value, str):
        if "minLength" in schema and len(value) < schema["minLength"]:
            raise RuntimeError(f"Manifest string is too short at {path}")
        if "pattern" in schema and re.fullmatch(schema["pattern"], value) is None:
            raise RuntimeError(f"Manifest string does not match its pattern at {path}")
    if isinstance(value, list):
        if "minItems" in schema and len(value) < schema["minItems"]:
            raise RuntimeError(f"Manifest array is too short at {path}")
        if "maxItems" in schema and len(value) > schema["maxItems"]:
            raise RuntimeError(f"Manifest array is too long at {path}")
        if schema.get("uniqueItems") is True:
            serialized = [
                json.dumps(item, ensure_ascii=False, sort_keys=True)
                for item in value
            ]
            if len(serialized) != len(set(serialized)):
                raise RuntimeError(f"Manifest array contains duplicates at {path}")
        if "items" in schema:
            for index, item in enumerate(value):
                _validate_json_schema(item, schema["items"], f"{path}[{index}]")
    if isinstance(value, dict):
        properties = schema.get("properties", {})
        for required in schema.get("required", []):
            if required not in value:
                raise RuntimeError(
                    f"Manifest is missing required field at {path}.{required}"
                )
        if schema.get("additionalProperties") is False:
            unknown = sorted(set(value) - set(properties))
            if unknown:
                raise RuntimeError(
                    f"Manifest has unknown fields at {path}: {', '.join(unknown)}"
                )
        for key, item in value.items():
            if key in properties:
                _validate_json_schema(item, properties[key], f"{path}.{key}")


def _manifest(
    root: Path,
    source: Path,
    glb: Path,
    summary: dict[str, Any],
    runtime_inspection: dict[str, Any],
) -> dict[str, Any]:
    schema_path, _schema = _load_schema(root)
    return {
        "schemaVersion": 1,
        "manifestSchema": {
            "path": SCHEMA_RELATIVE.as_posix(),
            "sha256": sha256(schema_path),
        },
        "assetId": ASSET_ID,
        "assetStage": "static-visual-candidate",
        "visualCandidate": True,
        "visualApprovalStatus": "pending",
        "runtimeReady": False,
        "sourceAuthority": "blend",
        "sourceFile": SOURCE_RELATIVE.as_posix(),
        "runtimeCandidateFile": GLB_RELATIVE.as_posix(),
        "sourceSha256": sha256(source),
        "runtimeSha256": sha256(glb),
        "runtimeBytes": glb.stat().st_size,
        **summary,
        "runtimeInspection": runtime_inspection,
        "limits": {
            "heightMeters": [MIN_HEIGHT_METERS, MAX_HEIGHT_METERS],
            "triangles": [MIN_TRIANGLES, MAX_TRIANGLES],
            "majorVisibleMeshes": MAX_VISIBLE_MESHES,
            "materialSurfaces": MAX_MATERIAL_SURFACES,
            "textureSize": TEXTURE_SIZE,
            "runtimeBytes": MAX_GLB_BYTES,
        },
        "exportProfile": {
            "format": "GLB",
            "appliedModifiers": True,
            "animations": False,
            "armature": False,
            "cameras": False,
            "lights": False,
            "tangents": "generated-by-godot-import",
            "textures": "embedded-from-tracked-2k-rgba-png",
        },
    }


def _open_and_validate(root: Path) -> tuple[Path, dict[str, Any], list[bpy.types.Object]]:
    source = root / SOURCE_RELATIVE
    require_file(source, "Protagonist v2 authoritative Blend")
    bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False)
    summary = validate_scene(root)
    _character, exported_objects = _validate_hierarchy()
    return source, summary, exported_objects


def art_export(root: Path) -> None:
    source, summary, exported_objects = _open_and_validate(root)
    glb = root / GLB_RELATIVE
    manifest_path = root / MANIFEST_RELATIVE
    glb.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(
        prefix="town-rpg-protagonist-v2-export-",
        dir=glb.parent,
    ) as temporary:
        candidate = Path(temporary) / GLB_RELATIVE.name
        candidate_manifest = Path(temporary) / MANIFEST_RELATIVE.name
        _export_glb(candidate, exported_objects)
        runtime_inspection = _inspect_glb(candidate, summary)
        manifest = _manifest(
            root,
            source,
            candidate,
            summary,
            runtime_inspection,
        )
        _schema_path, schema = _load_schema(root)
        _validate_json_schema(manifest, schema)
        candidate_manifest.write_text(
            json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )
        # Validate and serialize both outputs before replacing either tracked file.
        # The temporary directory lives beside the targets, so each replacement is
        # atomic on the same filesystem. A process interruption between the pair is
        # still detected explicitly by art-check's cross-hash validation.
        candidate.replace(glb)
        candidate_manifest.replace(manifest_path)
    print(
        "PROTAGONIST_ART_V2_EXPORT_PASS "
        f"meshes={summary['majorVisibleMeshCount']} "
        f"triangles={summary['triangleCount']} "
        f"height={summary['heightMeters']:.4f} "
        f"bytes={glb.stat().st_size} bound=false animated=false"
    )


def art_check(root: Path) -> None:
    source = root / SOURCE_RELATIVE
    glb = root / GLB_RELATIVE
    manifest_path = root / MANIFEST_RELATIVE
    schema_path = root / SCHEMA_RELATIVE
    for path, label in (
        (source, "Protagonist v2 authoritative Blend"),
        (glb, "Protagonist v2 GLB candidate"),
        (manifest_path, "Protagonist v2 manifest"),
        (schema_path, "Protagonist v2 manifest schema"),
    ):
        require_file(path, label)

    protected_paths = (
        source,
        glb,
        manifest_path,
        schema_path,
        *(
            root / TEXTURE_DIRECTORY_RELATIVE / filename
            for filename in EXPECTED_TEXTURES.values()
        ),
    )
    for path in protected_paths:
        require_file(path, "Protected protagonist v2 check input")
    before = {
        path: (path.stat().st_size, sha256(path))
        for path in protected_paths
    }
    source, summary, exported_objects = _open_and_validate(root)
    runtime_inspection = _inspect_glb(glb, summary)
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    _schema_path, schema = _load_schema(root)
    _validate_json_schema(manifest, schema)
    expected = _manifest(root, source, glb, summary, runtime_inspection)
    if manifest != expected:
        stale_keys = sorted(
            key
            for key in set(manifest) | set(expected)
            if manifest.get(key) != expected.get(key)
        )
        raise RuntimeError(
            "Protagonist v2 manifest is stale: " + ", ".join(stale_keys)
        )

    with tempfile.TemporaryDirectory(prefix="town-rpg-protagonist-v2-check-") as temporary:
        candidate = Path(temporary) / GLB_RELATIVE.name
        _export_glb(candidate, exported_objects)
        candidate_inspection = _inspect_glb(candidate, summary)
        if candidate_inspection != runtime_inspection:
            raise RuntimeError(
                "Fresh protagonist v2 GLB semantic inspection differs from the tracked GLB"
            )
        if sha256(candidate) != sha256(glb):
            raise RuntimeError(
                "Fresh protagonist v2 GLB export is not deterministic"
            )

    after = {
        path: (path.stat().st_size, sha256(path))
        for path in protected_paths
    }
    if after != before:
        changed = sorted(str(path) for path in before if before[path] != after[path])
        raise RuntimeError(
            "art-check modified repository assets: " + ", ".join(changed)
        )
    print(
        "PROTAGONIST_ART_V2_CHECK_PASS "
        f"meshes={summary['majorVisibleMeshCount']} "
        f"triangles={summary['triangleCount']} "
        f"height={summary['heightMeters']:.4f} "
        f"bytes={glb.stat().st_size} readOnly=true bound=false animated=false"
    )


def main() -> None:
    args = parse_args()
    root = Path(args.root).resolve()
    if args.mode == "art-export":
        art_export(root)
    else:
        art_check(root)


if __name__ == "__main__":
    main()
