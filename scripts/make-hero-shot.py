#!/usr/bin/env python3
"""Merge two screenshots into a single diagonal-split hero image.

Usage:
    python3 scripts/make-hero-shot.py <left> <right> [output]

Defaults:
    left   = docs/screenshot-desktop-dark.png
    right  = docs/screenshot-desktop-light.png
    output = screenshot-hero.png  (in the script's own directory)
"""
from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

SCRIPT_DIR = Path(__file__).resolve().parent
ROOT = SCRIPT_DIR.parent

DEFAULT_LEFT = ROOT / "docs" / "screenshot-desktop-dark.png"
DEFAULT_RIGHT = ROOT / "docs" / "screenshot-desktop-light.png"
DEFAULT_OUT = SCRIPT_DIR / "screenshot-hero.png"

# Diagonal cut passes through the image center (w/2, h/2).
# SLOPE controls steepness: horizontal offset (as fraction of width) between
# where the cut meets the top edge and the bottom edge.
SLOPE = 0.18


def main() -> None:
    left_path = Path(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT_LEFT
    right_path = Path(sys.argv[2]) if len(sys.argv) > 2 else DEFAULT_RIGHT
    out_path = Path(sys.argv[3]) if len(sys.argv) > 3 else DEFAULT_OUT

    left_img = Image.open(left_path).convert("RGBA")
    right_img = Image.open(right_path).convert("RGBA")
    assert left_img.size == right_img.size, (
        f"size mismatch: {left_img.size} vs {right_img.size}"
    )
    w, h = left_img.size

    # Build an alpha mask: white = left image, black = right image.
    # The boundary is a diagonal line through the center.
    mask = Image.new("L", (w, h), 0)
    draw = ImageDraw.Draw(mask)

    offset = int(w * SLOPE) // 2
    top_x = w // 2 - offset
    bottom_x = w // 2 + offset
    draw.polygon(
        [(0, 0), (top_x, 0), (bottom_x, h), (0, h)],
        fill=255,
    )

    # Feather the diagonal edge for a clean anti-aliased cut.
    mask = mask.filter(ImageFilter.GaussianBlur(radius=2))

    # Composite: left image on top of right image, masked by the diagonal.
    merged = right_img.copy()
    merged.paste(left_img, (0, 0), mask)

    out_path.parent.mkdir(parents=True, exist_ok=True)
    merged.save(out_path, format="PNG")
    print(f"wrote {out_path} ({out_path.stat().st_size} bytes)")


if __name__ == "__main__":
    main()