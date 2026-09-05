from __future__ import annotations

import argparse
import hashlib
import io
import json
import shutil
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[2]
WORK_SOURCE = ROOT / "work" / "dream_escape_art_refs" / "classroom-art-direction-2026-08-17.png"
OUTPUT_DIR = ROOT / "assets" / "generated" / "dream_escape" / "experimental"
SOURCE = OUTPUT_DIR / "classroom-original-source-v1.png"
BACKGROUND = OUTPUT_DIR / "classroom-original-background-v1.png"
OCCLUDERS = OUTPUT_DIR / "classroom-original-occluders-v1.png"
MANIFEST = OUTPUT_DIR / "classroom-original-manifest.json"

RUNTIME_SIZE = (640, 360)
SOURCE_SIZE = (1672, 941)

# Each group is rendered into one 640x360 atlas band. The bottom coordinate is
# also the Y-sort pivot used by the Godot presentation layer.
OCCLUDER_GROUPS: dict[str, tuple[int, list[list[tuple[int, int]]]]] = {
    "map_cabinet": (
        214,
        [[(126, 65), (231, 65), (231, 214), (126, 214)]],
    ),
    "teacher_desk": (
        251,
        [[(282, 174), (391, 174), (391, 251), (282, 251)]],
    ),
    "student_desks_back": (
        320,
        [
            [(68, 241), (157, 241), (157, 320), (68, 320)],
            [(205, 242), (286, 242), (286, 320), (205, 320)],
            [(313, 241), (406, 241), (406, 320), (313, 320)],
            [(419, 241), (504, 241), (504, 320), (419, 320)],
            [(525, 242), (610, 242), (610, 320), (525, 320)],
        ],
    ),
    "student_desks_front": (
        360,
        [
            [(4, 302), (139, 302), (139, 359), (4, 359)],
            [(192, 302), (297, 302), (297, 359), (192, 359)],
            [(310, 302), (414, 302), (414, 359), (310, 359)],
            [(421, 302), (531, 302), (531, 359), (421, 359)],
            [(540, 302), (639, 302), (639, 359), (540, 359)],
        ],
    ),
}


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def encode_png(image: Image.Image) -> bytes:
    buffer = io.BytesIO()
    image.save(buffer, "PNG", optimize=True)
    return buffer.getvalue()


def load_source() -> Image.Image:
    if not SOURCE.exists():
        raise FileNotFoundError(
            f"Missing adopted source image: {SOURCE}. Run with --import-source once."
        )
    image = Image.open(SOURCE)
    image.load()
    if image.size != SOURCE_SIZE or image.mode != "RGB":
        raise ValueError(
            f"Expected source {SOURCE_SIZE} RGB, got {image.size} {image.mode}."
        )
    return image


def render_outputs() -> tuple[bytes, bytes, dict[str, object]]:
    source = load_source()
    background = source.resize(RUNTIME_SIZE, Image.Resampling.LANCZOS).convert("RGBA")

    atlas = Image.new("RGBA", (640, 360 * len(OCCLUDER_GROUPS)), (0, 0, 0, 0))
    groups: dict[str, object] = {}
    for index, (name, (sort_y, polygons)) in enumerate(OCCLUDER_GROUPS.items()):
        mask = Image.new("L", RUNTIME_SIZE, 0)
        draw = ImageDraw.Draw(mask)
        for polygon in polygons:
            draw.polygon(polygon, fill=255)
        layer = Image.new("RGBA", RUNTIME_SIZE, (0, 0, 0, 0))
        layer.paste(background, (0, 0), mask)
        atlas.paste(layer, (0, index * 360))
        groups[name] = {
            "atlasRegion": [0, index * 360, 640, 360],
            "sortY": sort_y,
            "polygons": polygons,
        }

    background_bytes = encode_png(background)
    occluder_bytes = encode_png(atlas)
    source_bytes = SOURCE.read_bytes()
    manifest: dict[str, object] = {
        "schemaVersion": 1,
        "generatedBy": "scripts/tools/prepare_original_classroom.py",
        "pillowVersion": Image.__version__,
        "source": {
            "file": SOURCE.name,
            "width": SOURCE_SIZE[0],
            "height": SOURCE_SIZE[1],
            "mode": "RGB",
            "sha256": sha256(source_bytes),
        },
        "runtimeBackground": {
            "file": BACKGROUND.name,
            "width": 640,
            "height": 360,
            "mode": "RGBA",
            "sha256": sha256(background_bytes),
        },
        "occluderAtlas": {
            "file": OCCLUDERS.name,
            "width": 640,
            "height": 360 * len(OCCLUDER_GROUPS),
            "mode": "RGBA",
            "sha256": sha256(occluder_bytes),
            "groups": groups,
        },
    }
    return background_bytes, occluder_bytes, manifest


def import_source() -> None:
    if not WORK_SOURCE.exists():
        raise FileNotFoundError(f"Missing offline work reference: {WORK_SOURCE}")
    image = Image.open(WORK_SOURCE)
    image.load()
    if image.size != SOURCE_SIZE or image.mode != "RGB":
        raise ValueError(
            f"Expected work reference {SOURCE_SIZE} RGB, got {image.size} {image.mode}."
        )
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(WORK_SOURCE, SOURCE)


def build() -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    background_bytes, occluder_bytes, manifest = render_outputs()
    BACKGROUND.write_bytes(background_bytes)
    OCCLUDERS.write_bytes(occluder_bytes)
    MANIFEST.write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    print(
        "ORIGINAL_CLASSROOM_BUILD_PASS "
        f"source={SOURCE_SIZE[0]}x{SOURCE_SIZE[1]} occluders={len(OCCLUDER_GROUPS)}"
    )


def check() -> None:
    background_bytes, occluder_bytes, manifest = render_outputs()
    expected_manifest = json.dumps(manifest, ensure_ascii=False, indent=2) + "\n"
    failures: list[str] = []
    if not BACKGROUND.exists() or BACKGROUND.read_bytes() != background_bytes:
        failures.append(BACKGROUND.name)
    if not OCCLUDERS.exists() or OCCLUDERS.read_bytes() != occluder_bytes:
        failures.append(OCCLUDERS.name)
    if not MANIFEST.exists() or MANIFEST.read_text(encoding="utf-8") != expected_manifest:
        failures.append(MANIFEST.name)
    if failures:
        raise RuntimeError("Generated original-classroom assets differ: " + ", ".join(failures))
    print(
        "ORIGINAL_CLASSROOM_CHECK_PASS "
        f"source={SOURCE_SIZE[0]}x{SOURCE_SIZE[1]} occluders={len(OCCLUDER_GROUPS)}"
    )


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=("build", "check"))
    parser.add_argument("--import-source", action="store_true")
    args = parser.parse_args()
    if args.import_source:
        import_source()
    if args.mode == "build":
        build()
    else:
        check()


if __name__ == "__main__":
    main()
