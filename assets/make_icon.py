#!/usr/bin/env python3
"""Generate the Project Pinner app icon: a white location-pin on a blue rounded square.
Renders supersampled for crisp edges, then writes a multi-size .ico (16-256)."""
import os
from PIL import Image, ImageDraw, ImageFilter

SS = 4            # supersample factor
S = 256 * SS      # working canvas
OUT_DIR = os.path.dirname(os.path.abspath(__file__))

TOP = (91, 156, 255)     # #5B9CFF
BOT = (61, 123, 240)     # #3D7BF0
WHITE = (255, 255, 255, 255)


def rounded_mask(size, radius):
    m = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(m)
    d.rounded_rectangle([0, 0, size - 1, size - 1], radius=radius, fill=255)
    return m


def vertical_gradient(size, top, bot):
    g = Image.new("RGB", (size, size), top)
    px = g.load()
    for y in range(size):
        t = y / (size - 1)
        r = int(top[0] + (bot[0] - top[0]) * t)
        gr = int(top[1] + (bot[1] - top[1]) * t)
        b = int(top[2] + (bot[2] - top[2]) * t)
        for x in range(size):
            px[x, y] = (r, gr, b)
    return g


def pin_masks(size):
    """Return (teardrop_mask, hole_mask) in 'L' mode for the location-pin glyph."""
    cx = size // 2
    head_cy = int(size * 0.40)
    head_r = int(size * 0.205)
    tip_y = int(size * 0.80)
    hole_r = int(size * 0.092)

    teard = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(teard)
    # head circle
    d.ellipse([cx - head_r, head_cy - head_r, cx + head_r, head_cy + head_r], fill=255)
    # body triangle: tangent-ish from the circle's flanks down to the tip
    flank = int(head_r * 0.86)
    fy = head_cy + int(head_r * 0.52)
    d.polygon([(cx - flank, fy), (cx + flank, fy), (cx, tip_y)], fill=255)

    hole = Image.new("L", (size, size), 0)
    ImageDraw.Draw(hole).ellipse(
        [cx - hole_r, head_cy - hole_r, cx + hole_r, head_cy + hole_r], fill=255)
    return teard, hole


def build():
    radius = int(S * 0.22)
    bg = vertical_gradient(S, TOP, BOT)
    icon = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    icon.paste(bg, (0, 0), rounded_mask(S, radius))

    teard, hole = pin_masks(S)

    # soft drop shadow beneath the pin
    shadow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    sh_mask = teard.point(lambda v: int(v * 0.45))
    shadow.paste((10, 30, 70, 255), (0, int(S * 0.018)), sh_mask)
    shadow = shadow.filter(ImageFilter.GaussianBlur(S * 0.012))
    icon = Image.alpha_composite(icon, shadow)

    # white pin with the hole punched out (background shows through)
    pin_mask = teard.copy()
    pin_mask.paste(0, (0, 0), hole)          # subtract hole
    white = Image.new("RGBA", (S, S), WHITE)
    icon = Image.composite(white, icon, pin_mask)

    base = icon.resize((256, 256), Image.LANCZOS)
    base.resize((512, 512), Image.LANCZOS).save(os.path.join(OUT_DIR, "icon_preview.png"))

    sizes = [16, 24, 32, 48, 64, 128, 256]
    frames = [base.resize((s, s), Image.LANCZOS) for s in sizes]
    base.save(os.path.join(OUT_DIR, "app.ico"), format="ICO",
              sizes=[(s, s) for s in sizes], append_images=frames)
    print("wrote app.ico + icon_preview.png")


if __name__ == "__main__":
    build()
