#!/usr/bin/env python3
# Copyright (c) 2026 ktsu-dev contributors
"""Draws the tray icon pair and the NuGet package icon for a tool built on ktsu.TrayApp.

A tray tool needs two images that read as the same mark in two states, at 16 pixels, on a panel of
unknown colour. This draws that pair: a filled mark while the tool is doing its job and a hollow
outline of the same mark while it is idle, so the silhouette carries the state even where a panel
renders the icon in monochrome. Everything is solid colour on transparency with no background tile,
for the same reason.

Nothing here is specific to one tool. Colours, sizes, the glyph, and the output paths are all
arguments, so a consuming repository can run it unchanged:

    python3 scripts/generate-icons.py --tray-dir MyTool/Assets --package-icon icon.png
    python3 scripts/generate-icons.py --glyph square --active-colour 90,200,120

Requires Pillow. Every shape is drawn at 4x and downsampled, which is what keeps the 16px tray
rendering legible.
"""

from __future__ import annotations

import argparse
import os
from typing import Sequence

from PIL import Image, ImageDraw

SUPERSAMPLE = 4

DEFAULT_ACTIVE_COLOUR = (245, 176, 65, 255)
DEFAULT_IDLE_COLOUR = (150, 155, 165, 255)
DEFAULT_PACKAGE_BACKGROUND = (30, 36, 48, 255)
TRANSPARENT = (0, 0, 0, 0)

GLYPHS = ("circle", "square", "triangle")

Colour = tuple[int, int, int, int]


def parse_colour(text: str) -> Colour:
    """Reads an ``r,g,b`` or ``r,g,b,a`` argument into a colour."""
    parts = [int(part) for part in text.split(",")]

    if len(parts) == 3:
        parts.append(255)

    if len(parts) != 4 or any(part < 0 or part > 255 for part in parts):
        raise argparse.ArgumentTypeError(f"'{text}' is not an r,g,b or r,g,b,a colour.")

    return (parts[0], parts[1], parts[2], parts[3])


def glyph_points(glyph: str, size: int, inset: float) -> Sequence[tuple[float, float]] | None:
    """Returns the outline of a polygonal glyph, or ``None`` for glyphs the ellipse path draws."""
    low = inset
    high = size - inset

    if glyph == "square":
        # A rounded square is drawn by the caller; the corner radius needs the box, not the points.
        return None

    if glyph == "triangle":
        # Sitting on its base rather than centred on the bounding box, so the mass is where the eye
        # expects it at 16 pixels.
        drop = (high - low) * 0.08
        return [
            ((low + high) / 2, low + drop),
            (high, high - drop),
            (low, high - drop),
        ]

    return None


def draw_glyph(
    image: Image.Image,
    size: int,
    colour: Colour,
    *,
    glyph: str,
    filled: bool,
) -> None:
    """Paints one glyph, filled or as an outline, centred on a square canvas."""
    draw = ImageDraw.Draw(image)

    # A hairline outline vanishes at 16px and a fat one closes up the middle; a sixteenth of the
    # icon is the width that survives both the downsample and a high-DPI panel.
    stroke = max(1, int(round(size * 0.0625)))
    inset = size * 0.17

    fill = colour if filled else None
    outline = None if filled else colour

    if glyph == "circle":
        draw.ellipse([inset, inset, size - inset, size - inset], fill=fill, outline=outline, width=stroke)
        return

    if glyph == "square":
        draw.rounded_rectangle(
            [inset, inset, size - inset, size - inset],
            radius=size * 0.14,
            fill=fill,
            outline=outline,
            width=stroke,
        )
        return

    points = glyph_points(glyph, size, inset)
    assert points is not None, f"unhandled glyph '{glyph}'"
    draw.polygon(points, fill=fill, outline=outline, width=stroke)


def render_tray_icon(size: int, colour: Colour, *, glyph: str, filled: bool) -> Image.Image:
    """Renders one tray icon at the requested size."""
    canvas = size * SUPERSAMPLE
    image = Image.new("RGBA", (canvas, canvas), TRANSPARENT)
    draw_glyph(image, canvas, colour, glyph=glyph, filled=filled)
    return image.resize((size, size), Image.LANCZOS)


def render_package_icon(size: int, colour: Colour, background: Colour, *, glyph: str) -> Image.Image:
    """Renders the NuGet icon: the active mark on a rounded tile."""
    canvas = size * SUPERSAMPLE
    image = Image.new("RGBA", (canvas, canvas), TRANSPARENT)
    ImageDraw.Draw(image).rounded_rectangle(
        [0, 0, canvas - 1, canvas - 1], radius=canvas * 0.22, fill=background
    )

    # The mark has transparent pixels inside it, so composite rather than paste: the tile has to
    # show through wherever the glyph does not cover it.
    mark = Image.new("RGBA", (canvas, canvas), TRANSPARENT)
    draw_glyph(mark, canvas, colour, glyph=glyph, filled=True)
    image.alpha_composite(mark)

    return image.resize((size, size), Image.LANCZOS)


def build_parser() -> argparse.ArgumentParser:
    """Builds the command line parser."""
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--tray-dir", default="Assets", help="Directory the two tray PNGs are written to.")
    parser.add_argument("--active-name", default="tray-active.png", help="File name of the active tray icon.")
    parser.add_argument("--idle-name", default="tray-idle.png", help="File name of the idle tray icon.")
    parser.add_argument("--package-icon", default="icon.png", help="Path of the NuGet package icon, or '' to skip it.")
    parser.add_argument("--glyph", default="circle", choices=GLYPHS, help="The mark to draw.")
    parser.add_argument("--active-colour", type=parse_colour, default=DEFAULT_ACTIVE_COLOUR, help="r,g,b[,a] of the active mark.")
    parser.add_argument("--idle-colour", type=parse_colour, default=DEFAULT_IDLE_COLOUR, help="r,g,b[,a] of the idle mark.")
    parser.add_argument("--background", type=parse_colour, default=DEFAULT_PACKAGE_BACKGROUND, help="r,g,b[,a] of the package icon's tile.")
    parser.add_argument("--size", type=int, default=128, help="Edge length of the tray icons, in pixels.")
    parser.add_argument("--package-size", type=int, default=256, help="Edge length of the package icon, in pixels.")
    return parser


def main(argv: Sequence[str] | None = None) -> None:
    """Writes every icon a tray tool needs."""
    args = build_parser().parse_args(argv)

    os.makedirs(args.tray_dir, exist_ok=True)

    active_path = os.path.join(args.tray_dir, args.active_name)
    idle_path = os.path.join(args.tray_dir, args.idle_name)

    render_tray_icon(args.size, args.active_colour, glyph=args.glyph, filled=True).save(active_path)
    render_tray_icon(args.size, args.idle_colour, glyph=args.glyph, filled=False).save(idle_path)
    print(f"Wrote {active_path} and {idle_path}")

    if args.package_icon:
        directory = os.path.dirname(os.path.abspath(args.package_icon))
        os.makedirs(directory, exist_ok=True)
        render_package_icon(args.package_size, args.active_colour, args.background, glyph=args.glyph).save(args.package_icon)
        print(f"Wrote {args.package_icon}")


if __name__ == "__main__":
    main()
