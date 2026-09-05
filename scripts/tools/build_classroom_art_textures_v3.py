"""Build deterministic, repeat-safe textures for classroom art pass v3.

The v3 Blend packs these authoring textures before export.  Runtime GLB files
therefore have no external image dependency and Godot never runs this script.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import tempfile

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageOps


SOURCE_DIR = Path("art/blender/dream_classroom/source-v2")
OUTPUT_DIR = Path("art/blender/dream_classroom/textures-v3")
MANIFEST = OUTPUT_DIR / "classroom-art-textures-v3-manifest.json"
FONT = Path(r"C:\Windows\Fonts\simkai.ttf")
ALBEDO_SIZE = (2048, 2048)
CHANNEL_SIZE = (1024, 1024)


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


def source(root: Path, name: str, size: tuple[int, int] = ALBEDO_SIZE) -> np.ndarray:
    image = Image.open(root / SOURCE_DIR / name).convert("RGB")
    return np.asarray(ImageOps.fit(image, size, Image.Resampling.LANCZOS), dtype=np.float32)


def luminance(array: np.ndarray) -> np.ndarray:
    return array[..., 0] * 0.2126 + array[..., 1] * 0.7152 + array[..., 2] * 0.0722


def tinted_detail(array: np.ndarray, color: tuple[float, float, float], contrast: float) -> np.ndarray:
    gray = luminance(array) / 255.0
    value = 1.0 + (gray - float(gray.mean())) * contrast
    return np.clip(value[..., None] * np.asarray(color, dtype=np.float32), 0, 255)


def plaster(root: Path) -> np.ndarray:
    raw = source(root, "plaster-albedo-source.png")
    # The reference is deliberately much darker than the v2 atlas.  Keep the
    # photographed crack structure while pulling broad values out of white.
    return np.clip(raw * np.array([0.64, 0.62, 0.58]) + np.array([8, 8, 7]), 0, 255)


def teal(root: Path) -> np.ndarray:
    raw = source(root, "plaster-albedo-source.png")
    result = tinted_detail(raw, (30, 72, 72), 0.74)
    # A gentle vertical grime gradient keeps the wainscot from reading as a
    # single clean cyan slab when viewed at eye level.
    gradient = np.linspace(0.72, 1.02, result.shape[0], dtype=np.float32)[:, None, None]
    return np.clip(result * gradient, 0, 255)


def wood(root: Path, dark: bool = False) -> np.ndarray:
    raw = source(root, "wood-albedo-source.png")
    factor = np.array([0.66, 0.61, 0.55]) if not dark else np.array([0.40, 0.35, 0.31])
    return np.clip(raw * factor, 0, 255)


def floor(root: Path) -> np.ndarray:
    raw = source(root, "terrazzo-albedo-source.png")
    result = raw * np.array([0.68, 0.66, 0.63])
    rng = np.random.default_rng(200103)
    columns, rows = 16, 12
    height, width = result.shape[:2]
    for row in range(rows):
        y0, y1 = round(row * height / rows), round((row + 1) * height / rows)
        for column in range(columns):
            x0, x1 = round(column * width / columns), round((column + 1) * width / columns)
            result[y0:y1, x0:x1] += float(rng.normal(0, 4.2))
    for column in range(1, columns):
        x = round(column * width / columns)
        result[:, max(0, x - 2):x + 1] *= 0.42
    for row in range(1, rows):
        y = round(row * height / rows)
        result[max(0, y - 2):y + 1, :] *= 0.42
    return np.clip(result, 0, 255)


def blackboard(root: Path) -> np.ndarray:
    raw = source(root, "plaster-albedo-source.png")
    result = tinted_detail(raw, (16, 39, 37), 0.35)
    image = Image.fromarray(np.uint8(result), "RGB")
    overlay = Image.new("RGBA", ALBEDO_SIZE, (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay, "RGBA")
    rng = np.random.default_rng(199907)
    for _ in range(42):
        y = int(rng.integers(80, 1960))
        x0 = int(rng.integers(30, 680))
        x1 = int(rng.integers(1320, 2020))
        draw.arc((x0, y - 55, x1, y + 55), 184, 350,
                 fill=(177, 181, 161, int(rng.integers(6, 18))), width=int(rng.integers(2, 8)))
    return np.asarray(Image.alpha_composite(image.convert("RGBA"), overlay).convert("RGB"), dtype=np.float32)


def normal_map(albedo: np.ndarray, strength: float) -> Image.Image:
    reduced = np.asarray(Image.fromarray(np.uint8(albedo), "RGB").resize(CHANNEL_SIZE, Image.Resampling.LANCZOS), dtype=np.float32)
    gray = luminance(reduced) / 255.0
    dx = (np.roll(gray, 1, 1) - np.roll(gray, -1, 1)) * strength
    dy = (np.roll(gray, 1, 0) - np.roll(gray, -1, 0)) * strength
    normal = np.dstack((dx, -dy, np.ones_like(dx)))
    normal /= np.linalg.norm(normal, axis=2, keepdims=True)
    return Image.fromarray(np.uint8(np.clip((normal * 0.5 + 0.5) * 255, 0, 255)), "RGB")


def orm_map(albedo: np.ndarray, roughness: int) -> Image.Image:
    reduced = np.asarray(Image.fromarray(np.uint8(albedo), "RGB").resize(CHANNEL_SIZE, Image.Resampling.LANCZOS), dtype=np.float32)
    gray = luminance(reduced)
    ao = np.uint8(np.clip(242 - np.maximum(0, 100 - gray) * 0.18, 206, 250))
    rough = np.uint8(np.clip(roughness + (gray - gray.mean()) * 0.08, 0, 255))
    metal = np.zeros_like(ao, dtype=np.uint8)
    return Image.fromarray(np.dstack((ao, rough, metal)), "RGB")


def details() -> Image.Image:
    image = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image, "RGBA")
    font = ImageFont.truetype(str(FONT), 82)
    small = ImageFont.truetype(str(FONT), 28)
    rng = np.random.default_rng(200012)
    for _ in range(78):
        x = int(rng.integers(30, 980)); y = int(rng.integers(30, 440))
        draw.line((x, y, min(1008, x + int(rng.integers(18, 120))), y + int(rng.integers(-9, 10))),
                  fill=(216, 211, 184, int(rng.integers(8, 29))), width=int(rng.integers(1, 4)))
    text = "窗  →  钟  →  值日表"
    box = draw.textbbox((0, 0), text, font=font)
    draw.text(((1024 - box[2]) // 2, 182), text, font=font,
              fill=(214, 209, 180, 222), stroke_width=1, stroke_fill=(92, 94, 79, 110))
    for index, rect in enumerate(((34, 558, 296, 936), (346, 588, 604, 936), (660, 562, 976, 932))):
        draw.rounded_rectangle(rect, 5, fill=(180, 165, 126, 244), outline=(73, 58, 39, 225), width=3)
        draw.text((rect[0] + 24, rect[1] + 12), "旧校记录", font=small, fill=(66, 55, 40, 220))
        for row in range(8):
            y = rect[1] + 58 + row * 31
            draw.line((rect[0] + 28, y, rect[2] - 25, y), fill=(55, 53, 45, 175), width=2)
        if index == 1:
            draw.ellipse((rect[0] + 16, rect[1] + 72, rect[0] + 66, rect[1] + 122), outline=(126, 35, 29, 240), width=6)
    return image


def build_outputs(root: Path, destination: Path) -> dict[str, object]:
    destination.mkdir(parents=True, exist_ok=True)
    surfaces = {
        "plaster": (plaster(root), 225, 2.0),
        "teal": (teal(root), 214, 1.8),
        "floor": (floor(root), 194, 2.3),
        "wood": (wood(root), 184, 1.3),
        "darkwood": (wood(root, True), 205, 1.2),
        "blackboard": (blackboard(root), 232, 0.9),
    }
    images: dict[str, Image.Image] = {}
    for name, (albedo, roughness, strength) in surfaces.items():
        images[f"{name}-albedo.png"] = Image.fromarray(np.uint8(albedo), "RGB")
        images[f"{name}-normal.png"] = normal_map(albedo, strength)
        images[f"{name}-orm.png"] = orm_map(albedo, roughness)
    images["details.png"] = details()
    exterior = Image.open(root / SOURCE_DIR / "window-exterior-source.png").convert("RGB")
    images["window-exterior.png"] = ImageOps.fit(exterior, (1024, 512), Image.Resampling.LANCZOS, centering=(0.5, 0.52))
    for name, image in images.items():
        image.save(destination / name, "PNG", optimize=True, compress_level=9)
    return {
        "schemaVersion": 1,
        "generator": "Pillow 12.1.1 and NumPy 2.4.2 deterministic v3 texture preparation",
        "outputs": {name: {"size": list(image.size), "mode": image.mode, "sha256": sha256(destination / name)}
                    for name, image in images.items()},
    }


def build(root: Path) -> None:
    manifest = build_outputs(root, root / OUTPUT_DIR)
    (root / MANIFEST).write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"CLASSROOM_ART_TEXTURES_V3_BUILD_PASS files={len(manifest['outputs'])}")


def check(root: Path) -> None:
    existing = json.loads((root / MANIFEST).read_text(encoding="utf-8"))
    with tempfile.TemporaryDirectory(prefix="town-rpg-classroom-v3-textures-") as temporary:
        candidate = build_outputs(root, Path(temporary))
        if candidate != existing:
            raise RuntimeError("Classroom v3 texture manifest is stale")
        for name in candidate["outputs"]:
            if (Path(temporary) / name).read_bytes() != (root / OUTPUT_DIR / name).read_bytes():
                raise RuntimeError(f"Classroom v3 texture differs from deterministic build: {name}")
    print(f"CLASSROOM_ART_TEXTURES_V3_CHECK_PASS files={len(existing['outputs'])}")


if __name__ == "__main__":
    args = parse_args()
    root = Path(args.root).resolve()
    build(root) if args.mode == "build" else check(root)
