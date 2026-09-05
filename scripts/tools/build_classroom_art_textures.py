"""Build and validate the classroom art-pass v2 texture set.

The source albedo paintings are tracked authoring inputs. This script crops,
grades, packs, and derives deterministic real-time textures from them. Runtime
builds do not execute this script.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import tempfile

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageOps


ATLAS_SIZE = 2048
TILE_SIZE = (1024, 512)
DETAIL_SIZE = 1024
EXTERIOR_SIZE = (1024, 512)
FONT_PATH = Path(r"C:\Windows\Fonts\simkai.ttf")
SOURCE_DIR = Path("art/blender/dream_classroom/source-v2")
OUTPUT_DIR = Path("art/blender/dream_classroom/textures-v2")
MANIFEST_PATH = OUTPUT_DIR / "classroom-art-textures-v2-manifest.json"
RUNTIME_OUTPUT_DIR = Path("assets/generated/dream_escape/3d/textures-v2")
RUNTIME_MANIFEST_PATH = RUNTIME_OUTPUT_DIR / "classroom-art-textures-v2-manifest.json"
OBSOLETE_OUTPUTS = (
    OUTPUT_DIR / "classroom-surfaces-albedo.png",
    OUTPUT_DIR / "classroom-surfaces-normal.png",
    OUTPUT_DIR / "classroom-surfaces-orm.png",
    RUNTIME_OUTPUT_DIR / "classroom-surfaces-albedo.png",
    RUNTIME_OUTPUT_DIR / "classroom-surfaces-albedo.png.import",
    RUNTIME_OUTPUT_DIR / "classroom-surfaces-normal.png",
    RUNTIME_OUTPUT_DIR / "classroom-surfaces-normal.png.import",
    RUNTIME_OUTPUT_DIR / "classroom-surfaces-orm.png",
    RUNTIME_OUTPUT_DIR / "classroom-surfaces-orm.png.import",
    Path("assets/generated/dream_escape/3d/classroom-art-pass-v2_T_classroom-surfaces-albedo.png"),
    Path("assets/generated/dream_escape/3d/classroom-art-pass-v2_T_classroom-surfaces-albedo.png.import"),
    Path("assets/generated/dream_escape/3d/classroom-art-pass-v2_T_classroom-surfaces-normal.png"),
    Path("assets/generated/dream_escape/3d/classroom-art-pass-v2_T_classroom-surfaces-normal.png.import"),
    Path("assets/generated/dream_escape/3d/classroom-art-pass-v2_T_classroom-surfaces-orm.png"),
    Path("assets/generated/dream_escape/3d/classroom-art-pass-v2_T_classroom-surfaces-orm.png.import"),
)

SOURCE_FILES = {
    "plaster": SOURCE_DIR / "plaster-albedo-source.png",
    "terrazzo": SOURCE_DIR / "terrazzo-albedo-source.png",
    "wood": SOURCE_DIR / "wood-albedo-source.png",
    "exterior": SOURCE_DIR / "window-exterior-source.png",
}

SHELL_REGIONS = {
    "M_OldPlaster": (0, 0),
    "M_FadedTeal": (1, 0),
    "M_WornFloor": (0, 1),
    "M_Blackboard": (1, 1),
    "M_DirtyGlass": (0, 2),
    "M_Baseboard": (1, 2),
    "M_Curtain": (0, 3),
    "M_Dust": (1, 3),
}

FURNITURE_REGIONS = {
    "M_WornWood": (0, 0),
    "M_DarkWood": (1, 0),
    "M_DarkMetal": (0, 1),
    "M_AgedPaper": (1, 1),
    "M_Amber": (0, 2),
    "M_Chalk": (1, 2),
    "M_FadedRed": (0, 3),
    "M_Rubber": (1, 3),
}

MATERIAL_PROPERTIES = {
    "M_OldPlaster": (228, 0),
    "M_FadedTeal": (214, 0),
    "M_WornFloor": (178, 0),
    "M_WornWood": (164, 0),
    "M_DarkWood": (188, 0),
    "M_DarkMetal": (112, 235),
    "M_Blackboard": (226, 0),
    "M_AgedPaper": (224, 0),
    "M_DirtyGlass": (92, 20),
    "M_Amber": (116, 190),
    "M_Chalk": (250, 0),
    "M_FadedRed": (190, 0),
    "M_Baseboard": (198, 0),
    "M_Curtain": (236, 0),
    "M_Rubber": (208, 0),
    "M_Dust": (246, 0),
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=("build", "check"))
    parser.add_argument("--root", default=".")
    return parser.parse_args()


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def source_tile(path: Path) -> np.ndarray:
    image = Image.open(path).convert("RGB")
    image = ImageOps.fit(image, TILE_SIZE, Image.Resampling.LANCZOS)
    return np.asarray(image, dtype=np.float32)


def tint_luminance(source: np.ndarray, color: tuple[int, int, int], contrast: float = 0.36) -> np.ndarray:
    gray = source.mean(axis=2, keepdims=True) / 255.0
    value = (gray - 0.5) * contrast + 1.0
    return np.clip(np.asarray(color, dtype=np.float32) * value, 0, 255)


def procedural_tile(name: str, plaster: np.ndarray, wood: np.ndarray) -> np.ndarray:
    rng = np.random.default_rng(abs(hashlib.sha256(name.encode()).digest()[0]) + 3917)
    width, height = TILE_SIZE
    noise = rng.normal(0.0, 1.0, (height, width))
    noise_image = Image.fromarray(np.uint8(np.clip(noise * 28 + 128, 0, 255))).filter(ImageFilter.GaussianBlur(3.2))
    smooth = (np.asarray(noise_image, dtype=np.float32)[..., None] - 128) / 128

    if name == "M_FadedTeal":
        tile = tint_luminance(plaster, (42, 92, 91), 0.48)
    elif name == "M_DarkWood":
        tile = np.clip(wood * np.array([0.52, 0.47, 0.43]), 0, 255)
    elif name == "M_DarkMetal":
        tile = np.broadcast_to(np.array([43, 49, 48], dtype=np.float32), plaster.shape).copy()
        tile += smooth * np.array([15, 13, 10])
        rust = np.maximum(0, smooth - 0.08) * np.array([58, 24, 7])
        tile += rust
    elif name == "M_Blackboard":
        tile = np.broadcast_to(np.array([19, 48, 44], dtype=np.float32), plaster.shape).copy()
        tile += smooth * np.array([10, 13, 12])
        for row in range(64, height, 92):
            tile[row:row + 2] += np.array([8, 12, 11])
    elif name == "M_AgedPaper":
        tile = np.broadcast_to(np.array([194, 181, 145], dtype=np.float32), plaster.shape).copy()
        tile += smooth * 14
    elif name == "M_DirtyGlass":
        tile = np.broadcast_to(np.array([74, 112, 112], dtype=np.float32), plaster.shape).copy()
        tile += smooth * np.array([14, 19, 18])
    elif name == "M_Amber":
        tile = np.broadcast_to(np.array([155, 95, 34], dtype=np.float32), plaster.shape).copy()
        tile += smooth * np.array([28, 18, 7])
    elif name == "M_Chalk":
        tile = tint_luminance(plaster, (224, 219, 191), 0.18)
    elif name == "M_FadedRed":
        tile = tint_luminance(plaster, (116, 42, 34), 0.30)
    elif name == "M_Baseboard":
        tile = tint_luminance(wood, (54, 43, 34), 0.30)
    elif name == "M_Curtain":
        tile = np.broadcast_to(np.array([164, 158, 132], dtype=np.float32), plaster.shape).copy()
        tile += smooth * 8
        for column in range(0, width, 38):
            tile[:, column:column + 3] -= 12
    elif name == "M_Rubber":
        tile = np.broadcast_to(np.array([31, 34, 34], dtype=np.float32), plaster.shape).copy()
        tile += smooth * 5
    elif name == "M_Dust":
        tile = np.broadcast_to(np.array([141, 124, 94], dtype=np.float32), plaster.shape).copy()
        tile += smooth * 18
    else:
        raise KeyError(name)
    return np.clip(tile, 0, 255)


def normal_from_albedo(tile: np.ndarray, strength: float) -> np.ndarray:
    gray = tile.mean(axis=2) / 255.0
    left = np.roll(gray, 1, axis=1)
    right = np.roll(gray, -1, axis=1)
    up = np.roll(gray, 1, axis=0)
    down = np.roll(gray, -1, axis=0)
    dx = (left - right) * strength
    dy = (up - down) * strength
    normal = np.dstack((dx, -dy, np.ones_like(dx)))
    normal /= np.linalg.norm(normal, axis=2, keepdims=True)
    return np.uint8(np.clip((normal * 0.5 + 0.5) * 255, 0, 255))


def terrazzo_square_floor(source: np.ndarray) -> np.ndarray:
    """Lay out 0.6 m-looking square tiles across the 9.6 x 7.2 m floor UV."""
    result = source.astype(np.float32).copy()
    rng = np.random.default_rng(200104)
    columns, rows = 16, 12
    width, height = TILE_SIZE
    for row in range(rows):
        y0 = round(row * height / rows)
        y1 = round((row + 1) * height / rows)
        for column in range(columns):
            x0 = round(column * width / columns)
            x1 = round((column + 1) * width / columns)
            variation = float(rng.normal(0.0, 5.0))
            result[y0:y1, x0:x1] = np.clip(result[y0:y1, x0:x1] + variation, 0, 255)
    # Two-pixel dark grout with a faint worn upper edge reads clearly at 640x360.
    for column in range(1, columns):
        x = round(column * width / columns)
        result[:, max(0, x - 1):x + 1] *= 0.54
        result[:, min(width - 1, x + 1):min(width, x + 2)] *= 1.05
    for row in range(1, rows):
        y = round(row * height / rows)
        result[max(0, y - 1):y + 1, :] *= 0.54
        result[min(height - 1, y + 1):min(height, y + 2), :] *= 1.05
    return np.clip(result, 0, 255)


def build_surface_atlas(sources: dict[str, Path], regions: dict[str, tuple[int, int]]) -> tuple[Image.Image, Image.Image, Image.Image]:
    plaster = source_tile(sources["plaster"])
    terrazzo = source_tile(sources["terrazzo"])
    wood = source_tile(sources["wood"])
    direct = {
        "M_OldPlaster": plaster,
        "M_WornFloor": terrazzo_square_floor(terrazzo),
        "M_WornWood": wood,
    }
    albedo = Image.new("RGB", (ATLAS_SIZE, ATLAS_SIZE))
    normal = Image.new("RGB", (ATLAS_SIZE, ATLAS_SIZE), (128, 128, 255))
    orm = Image.new("RGB", (ATLAS_SIZE, ATLAS_SIZE), (255, 220, 0))

    tile_width, tile_height = TILE_SIZE
    for material, (column, row) in regions.items():
        tile = direct.get(material)
        if tile is None:
            tile = procedural_tile(material, plaster, wood)
        tile = np.uint8(np.clip(tile, 0, 255))
        position = (column * tile_width, row * tile_height)
        albedo.paste(Image.fromarray(tile, "RGB"), position)

        strength = 2.4 if material in {"M_OldPlaster", "M_FadedTeal", "M_WornFloor"} else 1.25
        normal.paste(Image.fromarray(normal_from_albedo(tile, strength), "RGB"), position)

        gray = tile.mean(axis=2)
        roughness, metallic = MATERIAL_PROPERTIES[material]
        ao = np.uint8(np.clip(245 - np.maximum(0, 118 - gray) * 0.22, 205, 255))
        rough = np.uint8(np.clip(roughness + (gray - gray.mean()) * 0.10, 0, 255))
        metal = np.full_like(ao, metallic, dtype=np.uint8)
        orm.paste(Image.fromarray(np.dstack((ao, rough, metal)), "RGB"), position)
    return albedo, normal, orm


def build_details() -> Image.Image:
    image = Image.new("RGBA", (DETAIL_SIZE, DETAIL_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image, "RGBA")
    font = ImageFont.truetype(str(FONT_PATH), 88)
    small = ImageFont.truetype(str(FONT_PATH), 30)

    rng = np.random.default_rng(220826)
    for _ in range(54):
        x = int(rng.integers(35, 985))
        y = int(rng.integers(44, 435))
        length = int(rng.integers(16, 90))
        draw.line((x, y, min(1000, x + length), y + int(rng.integers(-7, 8))), fill=(220, 216, 185, int(rng.integers(10, 34))), width=int(rng.integers(1, 4)))

    text = "窗  →  钟  →  值日表"
    bounds = draw.textbbox((0, 0), text, font=font)
    x = (DETAIL_SIZE - (bounds[2] - bounds[0])) // 2
    draw.text((x + 2, 186 + 1), text, font=font, fill=(89, 99, 88, 90))
    draw.text((x, 186), text, font=font, fill=(224, 219, 190, 238), stroke_width=1, stroke_fill=(201, 198, 173, 180))

    paper_boxes = [(40, 560, 286, 930), (342, 594, 590, 930), (660, 574, 968, 924)]
    for index, box in enumerate(paper_boxes):
        draw.rounded_rectangle(box, radius=5, fill=(206, 190, 148, 242), outline=(99, 78, 52, 210), width=3)
        for row in range(8):
            y = box[1] + 54 + row * 31
            draw.line((box[0] + 32, y, box[2] - 28, y), fill=(72, 67, 56, 160), width=2)
        if index == 1:
            draw.ellipse((box[0] + 18, box[1] + 72, box[0] + 64, box[1] + 118), outline=(129, 38, 31, 235), width=6)
        draw.text((box[0] + 28, box[1] + 12), "旧校记录", font=small, fill=(78, 66, 49, 190))
    return image


def build_exterior(source: Path) -> Image.Image:
    image = Image.open(source).convert("RGB")
    return ImageOps.fit(image, EXTERIOR_SIZE, Image.Resampling.LANCZOS, centering=(0.5, 0.52))


def save_png(image: Image.Image, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path, format="PNG", optimize=True, compress_level=9)


def build_outputs(root: Path, destination: Path) -> dict[str, object]:
    sources = {name: root / relative for name, relative in SOURCE_FILES.items()}
    missing = [str(path) for path in sources.values() if not path.is_file()]
    if missing:
        raise RuntimeError(f"Missing classroom texture sources: {', '.join(missing)}")
    if not FONT_PATH.is_file():
        raise RuntimeError(f"Required authoring font is missing: {FONT_PATH}")

    shell_albedo, shell_normal, shell_orm = build_surface_atlas(sources, SHELL_REGIONS)
    furniture_albedo, furniture_normal, furniture_orm = build_surface_atlas(sources, FURNITURE_REGIONS)
    images = {
        "classroom-shell-albedo.png": shell_albedo,
        "classroom-shell-normal.png": shell_normal,
        "classroom-shell-orm.png": shell_orm,
        "classroom-furniture-albedo.png": furniture_albedo,
        "classroom-furniture-normal.png": furniture_normal,
        "classroom-furniture-orm.png": furniture_orm,
        "classroom-details.png": build_details(),
        "window-exterior.png": build_exterior(sources["exterior"]),
    }
    for filename, image in images.items():
        save_png(image, destination / filename)

    return {
        "schemaVersion": 1,
        "generator": "Pillow 12.1.1 and NumPy 2.4.2 deterministic texture preparation",
        "font": str(FONT_PATH),
        "sources": {
            name: {"file": str(relative).replace("\\", "/"), "sha256": sha256(root / relative)}
            for name, relative in SOURCE_FILES.items()
        },
        "regions": {
            **{name: {"atlas": "shell", "cell": list(region)} for name, region in SHELL_REGIONS.items()},
            **{name: {"atlas": "furniture", "cell": list(region)} for name, region in FURNITURE_REGIONS.items()},
        },
        "outputs": {
            filename: {
                "size": list(image.size),
                "mode": image.mode,
                "sha256": sha256(destination / filename),
            }
            for filename, image in images.items()
        },
    }


def build(root: Path) -> None:
    for relative in OBSOLETE_OUTPUTS:
        obsolete = root / relative
        if obsolete.is_file():
            obsolete.unlink()
    destination = root / OUTPUT_DIR
    manifest = build_outputs(root, destination)
    manifest_text = json.dumps(manifest, ensure_ascii=False, indent=2) + "\n"
    (root / MANIFEST_PATH).write_text(manifest_text, encoding="utf-8")
    runtime_destination = root / RUNTIME_OUTPUT_DIR
    runtime_destination.mkdir(parents=True, exist_ok=True)
    for filename in manifest["outputs"]:
        (runtime_destination / filename).write_bytes((destination / filename).read_bytes())
    (root / RUNTIME_MANIFEST_PATH).write_text(manifest_text, encoding="utf-8")
    print("CLASSROOM_ART_TEXTURES_BUILD_PASS files=8 atlases=2x2048 details=1024 exterior=1024x512")


def check(root: Path) -> None:
    manifest_path = root / MANIFEST_PATH
    if not manifest_path.is_file():
        raise RuntimeError(f"Texture manifest is missing: {manifest_path}")
    existing = json.loads(manifest_path.read_text(encoding="utf-8"))
    runtime_manifest_path = root / RUNTIME_MANIFEST_PATH
    if not runtime_manifest_path.is_file() or json.loads(runtime_manifest_path.read_text(encoding="utf-8")) != existing:
        raise RuntimeError("Runtime classroom texture manifest is missing or stale")
    with tempfile.TemporaryDirectory(prefix="town-rpg-classroom-textures-") as temporary:
        candidate_dir = Path(temporary)
        candidate = build_outputs(root, candidate_dir)
        if candidate != existing:
            raise RuntimeError("Classroom art texture manifest is stale")
        for filename in candidate["outputs"]:
            if (candidate_dir / filename).read_bytes() != (root / OUTPUT_DIR / filename).read_bytes():
                raise RuntimeError(f"Classroom art texture differs from deterministic build: {filename}")
            if (candidate_dir / filename).read_bytes() != (root / RUNTIME_OUTPUT_DIR / filename).read_bytes():
                raise RuntimeError(f"Runtime classroom texture differs from deterministic build: {filename}")
    print("CLASSROOM_ART_TEXTURES_CHECK_PASS files=8 atlases=2x2048 details=1024 exterior=1024x512")


def main() -> None:
    args = parse_args()
    root = Path(args.root).resolve()
    if args.mode == "build":
        build(root)
    else:
        check(root)


if __name__ == "__main__":
    main()
