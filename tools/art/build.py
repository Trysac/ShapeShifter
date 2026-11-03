"""Draw every sprite, tile, backdrop and icon of ShapeShifter into Assets/Art/.

Usage (from the repository root, Python 3 standard library only):
    python tools/art/build.py

Everything is drawn from code: the four runner forms from shapes plus an outline and
rim-light pass, small icons from ASCII templates, and the backdrops from periodic noise so
they tile horizontally. One unit in the game is 16 pixels.
"""

import math
import pathlib
import random
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))

from image import Image, hex_color, mix  # noqa: E402

OUTPUT = pathlib.Path(__file__).resolve().parents[2] / "Assets" / "Art"
FRAME = 32
OUTLINE = hex_color("1c1420")
CLEAR = (0, 0, 0, 0)

SKINS = {
    "ember": {"main": "d9622b", "light": "f29a52", "dark": "8a3418", "belly": "f6e3c4", "glow": "ffd36b"},
    "moss": {"main": "4f9a3a", "light": "86c95a", "dark": "2c5a24", "belly": "e4f0c8", "glow": "c8ff7a"},
    "frost": {"main": "7fbde3", "light": "d4f1ff", "dark": "3f6f98", "belly": "ffffff", "glow": "a0f0ff"},
    "dusk": {"main": "7a4fb0", "light": "a983d8", "dark": "432a6e", "belly": "f0d8ff", "glow": "ff8ae0"},
    "gilded": {"main": "d9a520", "light": "ffe070", "dark": "8a6410", "belly": "fff4c8", "glow": "ffffff"},
}

# The colours that mean a form everywhere in the UI (icons, signs, touch buttons).
FORM_COLORS = {"fox": "e07a35", "bird": "4fa8e0", "golem": "9a8f7a", "fish": "36b8a0", "jump": "e0c040"}


# ---- drawing helpers ----------------------------------------------------------

def ellipse(img, cx, cy, rx, ry, color):
    for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
        for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
            if ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2 <= 1.0:
                img.set(x, y, color)


def triangle(img, a, b, c, color):
    xs = [p[0] for p in (a, b, c)]
    ys = [p[1] for p in (a, b, c)]

    def side(p, q, r):
        return (p[0] - r[0]) * (q[1] - r[1]) - (q[0] - r[0]) * (p[1] - r[1])

    for y in range(int(min(ys)), int(max(ys)) + 1):
        for x in range(int(min(xs)), int(max(xs)) + 1):
            p = (x + 0.5, y + 0.5)
            d1, d2, d3 = side(p, a, b), side(p, b, c), side(p, c, a)
            neg = d1 < 0 or d2 < 0 or d3 < 0
            pos = d1 > 0 or d2 > 0 or d3 > 0
            if not (neg and pos):
                img.set(x, y, color)


def line(img, x0, y0, x1, y1, color, width=1):
    steps = max(abs(x1 - x0), abs(y1 - y0), 1)
    for i in range(int(steps * 2) + 1):
        t = i / (steps * 2)
        x = x0 + (x1 - x0) * t
        y = y0 + (y1 - y0) * t
        img.rect(int(round(x - (width - 1) / 2)), int(round(y - (width - 1) / 2)), width, width, color)


def finish(img, light=0.22, shade=0.25):
    """Rim-light the top edge, shade the bottom edge and outline every opaque shape."""
    w, h = img.width, img.height
    src = [img.get(x, y) for y in range(h) for x in range(w)]

    def at(x, y):
        return src[y * w + x] if 0 <= x < w and 0 <= y < h else CLEAR

    for y in range(h):
        for x in range(w):
            c = at(x, y)
            if c[3] == 0:
                continue
            if at(x, y - 1)[3] == 0:
                img.pixels[y * w + x] = list(mix(c, (255, 255, 255, c[3]), light))
            elif at(x, y + 1)[3] == 0:
                img.pixels[y * w + x] = list(mix(c, (0, 0, 0, c[3]), shade))
    for y in range(h):
        for x in range(w):
            if at(x, y)[3] == 0 and any(at(x + dx, y + dy)[3] > 0 for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                img.pixels[y * w + x] = list(OUTLINE)
    return img


def flash(img, amount=0.6):
    out = Image(img.width, img.height)
    for i, c in enumerate(img.pixels):
        if c[3]:
            out.pixels[i] = list(mix(tuple(c), (255, 255, 255, c[3]), amount))
    return out


def recolor(img, mapping):
    out = Image(img.width, img.height)
    for i, c in enumerate(img.pixels):
        out.pixels[i] = list(mapping.get(tuple(c), tuple(c)))
    return out


def palette(skin):
    return {k: hex_color(v) for k, v in SKINS[skin].items()}


# ---- the four forms ---------------------------------------------------------------

def fox(p, frame):
    img = Image(FRAME, FRAME)
    bob = -1 if frame in (1, 3) else 0
    jump = frame == 4
    feet = {
        0: [(8, 31), (13, 31), (18, 31), (24, 31)],
        1: [(10, 30), (12, 30), (19, 30), (21, 30)],
        2: [(13, 31), (8, 31), (24, 31), (18, 31)],
        3: [(11, 30), (11, 30), (20, 30), (20, 30)],
        4: [(6, 26), (9, 27), (25, 25), (27, 24)],
    }[min(frame, 4)]
    hips = [(11, 26 + bob), (12, 26 + bob), (19, 26 + bob), (20, 26 + bob)]
    for (hx, hy), (fx, fy) in zip(hips, feet):
        line(img, hx, hy, fx, fy - 1, p["dark"], 2)
    # Tail, swept up and back with a pale tip.
    tail_y = 18 + bob + (2 if jump else 0)
    ellipse(img, 7, tail_y, 5, 3, p["main"])
    ellipse(img, 3.5, tail_y - 1, 2.2, 2, p["belly"])
    # Body and chest.
    ellipse(img, 15, 23 + bob, 7.5, 4.2, p["main"])
    ellipse(img, 19, 25 + bob, 3.5, 2, p["belly"])
    # Head, snout, ears.
    hx, hy = (24, 17 + bob) if not jump else (25, 16)
    ellipse(img, hx, hy, 4.2, 3.8, p["main"])
    triangle(img, (hx + 2, hy - 1), (hx + 7.5, hy + 1.5), (hx + 2, hy + 3), p["main"])
    triangle(img, (hx + 2, hy + 1), (hx + 7, hy + 1.8), (hx + 2, hy + 3), p["belly"])
    triangle(img, (hx - 3, hy - 2), (hx - 2, hy - 8), (hx + 0.5, hy - 3), p["main"])
    triangle(img, (hx, hy - 3), (hx + 2, hy - 8), (hx + 3, hy - 2), p["main"])
    img.set(hx - 1, hy - 5, p["dark"])
    img.set(hx + 2, hy - 5, p["dark"])
    img.set(int(hx + 7), int(hy + 1), OUTLINE)
    finish(img)
    img.set(hx + 1, hy - 1, OUTLINE)
    img.set(hx + 2, hy - 1, p["glow"])
    return img


def bird(p, frame):
    img = Image(FRAME, FRAME)
    beak = hex_color("f2b630")
    bob = [0, -1, 0, 1, 0][min(frame, 4)]
    cy = 20 + bob
    # Tail fan.
    triangle(img, (10, cy - 1), (3, cy - 3), (4, cy + 3), p["dark"])
    # Body, belly, head, beak.
    ellipse(img, 15, cy, 7, 4.2, p["main"])
    ellipse(img, 16, cy + 2, 5, 2, p["belly"])
    ellipse(img, 22, cy - 4, 3.8, 3.5, p["main"])
    triangle(img, (25, cy - 5), (30, cy - 3.5), (25, cy - 2), beak)
    # Wing: up, level, down, level; frame 4 glides.
    tips = [(13, 4), (5, cy - 6), (12, 31), (5, cy - 6), (3, cy - 2)]
    tip = tips[min(frame, 4)]
    triangle(img, (11, cy - 1), (19, cy - 1), (tip[0], tip[1] + (bob if frame != 2 else 0)), p["dark"])
    triangle(img, (12, cy - 1), (17, cy - 1), ((tip[0] + 14) / 2, (tip[1] + cy) / 2), p["light"])
    # Feet tucked under.
    img.rect(14, cy + 4, 1, 2, beak)
    img.rect(17, cy + 4, 1, 2, beak)
    finish(img)
    img.set(23, cy - 5, OUTLINE)
    img.set(24, cy - 5, p["glow"])
    return img


def golem(p, frame):
    img = Image(FRAME, FRAME)
    stone, stone_light, stone_dark = hex_color("8a857a"), hex_color("aaa59a"), hex_color("5b5750")
    step = [0, 1, 0, -1, 0][min(frame, 4)]
    smash = frame == 4
    # Legs.
    img.rect(10 + step, 24, 5, 7 - abs(step), stone_dark)
    img.rect(18 - step, 24, 5, 7 - abs(step), stone_dark)
    img.rect(9 + step, 30 - abs(step), 7, 2, stone_dark)
    img.rect(17 - step, 30 - abs(step), 7, 2, stone_dark)
    # Torso and head.
    ellipse(img, 16, 18, 8.5, 7.5, stone)
    ellipse(img, 17, 8, 4.5, 4, stone)
    # Arms swing against the legs; when smashing both fists go forward.
    if smash:
        ellipse(img, 25, 16, 4, 3.5, stone_light)
        ellipse(img, 27, 13, 3.5, 3.2, stone)
    else:
        ellipse(img, 7, 19 - step, 3.2, 6, stone_light)
        ellipse(img, 26, 19 + step, 3.2, 6, stone)
        ellipse(img, 7, 25 - step, 3, 2.5, stone_dark)
        ellipse(img, 26, 25 + step, 3, 2.5, stone_dark)
    rng = random.Random(7)
    for _ in range(40):
        x, y = rng.randrange(6, 28), rng.randrange(4, 30)
        if img.get(x, y)[3]:
            img.set(x, y, stone_dark if rng.random() < 0.6 else stone_light)
    # Moss on the shoulders in the skin colour, glowing runes and eyes.
    for x in range(9, 24):
        if img.get(x, 11)[3] and (x * 7) % 5 != 0:
            img.set(x, 11, p["main"])
            img.set(x, 12, p["dark"] if x % 3 else p["main"])
    finish(img)
    for x, y in ((15, 17), (16, 18), (17, 17), (16, 19), (16, 16)):
        img.set(x, y, p["glow"])
    img.set(17, 7, p["glow"])
    img.set(19, 7, p["glow"])
    return img


def fish(p, frame):
    img = Image(FRAME, FRAME)
    wag = [0, 2, 0, -2, 0][min(frame, 4)]
    lift = -4 if frame == 4 else 0
    cy = 23 + lift
    triangle(img, (8, cy), (2, cy - 5 + wag), (2, cy + 5 + wag), p["dark"])
    ellipse(img, 16, cy, 9, 5.2, p["main"])
    ellipse(img, 17, cy + 2.5, 6.5, 2.2, p["belly"])
    triangle(img, (12, cy - 4), (19, cy - 4), (14, cy - 9), p["light"])
    triangle(img, (14, cy + 3), (18, cy + 3), (13 - wag // 2, cy + 7), p["light"])
    # Scales.
    for x in range(10, 22, 3):
        img.set(x, cy - 1, p["light"])
        img.set(x + 1, cy, p["dark"])
    finish(img)
    img.set(21, cy - 2, OUTLINE)
    img.set(22, cy - 2, p["glow"])
    img.set(24, cy + 1, OUTLINE)
    return img


FORMS = [("fox", fox), ("bird", bird), ("golem", golem), ("fish", fish)]


def runner_sheet(skin):
    """Four forms in order (fox, bird, golem, fish), 6 frames each: 4 run, 1 jump/glide/smash/hop, 1 hurt."""
    p = palette(skin)
    sheet = Image(FRAME * 6 * len(FORMS), FRAME)
    for f, (_, draw) in enumerate(FORMS):
        frames = [draw(p, i) for i in range(5)]
        frames.append(flash(frames[0], 0.65))
        for i, frame in enumerate(frames):
            sheet.paste(frame, (f * 6 + i) * FRAME, 0)
    return sheet


# ---- icons (ASCII templates) ------------------------------------------------------

ICONS = {
    "fox": [
        ".o........o.",
        ".oo......oo.",
        ".oOo....oOo.",
        ".oOOooooOOo.",
        ".oOOOOOOOOo.",
        "oOwOOOOOOwOo",
        "oOkwOOOOwkOo",
        ".oOOOwwOOOo.",
        "..oOwwwwOo..",
        "...owwkwo...",
        "....owwo....",
        ".....oo.....",
    ],
    "bird": [
        "............",
        ".....ooo....",
        "....oOOOo...",
        "...oOOOkOoy.",
        "..oOOOOOOoyy",
        "oooWWOOOOo..",
        "oWWWWWOOo...",
        ".oWWWWOOo...",
        "..ooWWOOo...",
        "....oOOoo...",
        ".....oo.....",
        "............",
    ],
    "golem": [
        "...oooooo...",
        "..oSSSSSSo..",
        ".oSSSSSSSSo.",
        ".oSgSSSSgSo.",
        ".oSggSSggSo.",
        ".oSSSSSSSSo.",
        ".oSSdSSdSSo.",
        ".oSSSddSSSo.",
        "..oSSSSSSo..",
        ".ooSSSSSSoo.",
        "oSSSSSSSSSSo",
        "oooooooooooo",
    ],
    "fish": [
        "............",
        "............",
        "....ooo.....",
        "...oFFFo....",
        "o.oOOOOOoo..",
        "ooOOOOOOOwo.",
        "oOOOOOOOkOOo",
        "ooOOOOOOOOo.",
        "o.oWWWWWoo..",
        "...ooooo....",
        "............",
        "............",
    ],
    "jump": [
        ".....oo.....",
        "....oyyo....",
        "...oyyyyo...",
        "..oooyyooo..",
        "....oyyo....",
        "....oooo....",
        "...oooooo...",
        "..oRRRRRRo..",
        ".oRRrRRRRRo.",
        ".oRRRRRrRRo.",
        ".oRrRRRRRRo.",
        "..oooooooo..",
    ],
}

ICON_PALETTE = {
    "o": OUTLINE, "k": OUTLINE, "w": hex_color("f6e3c4"), "y": hex_color("f2c440"),
    "O": None, "W": None, "F": None, "S": hex_color("a8a49a"), "g": hex_color("ffd36b"),
    "d": hex_color("5b5750"), "R": hex_color("8a857a"), "r": hex_color("5b5750"),
}


def icon(name, color):
    base = hex_color(color)
    pal = dict(ICON_PALETTE)
    pal["O"] = base
    pal["W"] = mix(base, (0, 0, 0, 255), 0.35)
    pal["F"] = mix(base, (255, 255, 255, 255), 0.4)
    if name == "fish":
        pal["W"] = hex_color("e8f4f0")
    img = Image(12, 12)
    img.stamp(0, 0, ICONS[name], pal)
    return img


def form_badge(name, size=32):
    """A round badge: the form's colour, a dark ring and its icon at 2x."""
    img = Image(size, size)
    color = hex_color(FORM_COLORS[name])
    c = size / 2
    ellipse(img, c, c, c - 0.5, c - 0.5, OUTLINE)
    ellipse(img, c, c, c - 2, c - 2, color)
    ellipse(img, c, c, c - 4, c - 4, hex_color("1e2a30"))
    ellipse(img, c, c - 1, c - 5, c - 6, hex_color("2c3c44"))
    ic = icon(name, FORM_COLORS[name] if name != "golem" else "a8a49a").scaled(2)
    img.paste(ic, int(c - 12), int(c - 12))
    return img


HEART = [
    ".ooo..ooo.",
    "oRRRooRRRo",
    "oRwRRRRRRo",
    "oRRRRRRRRo",
    ".oRRRRRRo.",
    "..oRRRRo..",
    "...oRRo...",
    "....oo....",
]


def heart(full=True):
    img = Image(12, 12)
    red = hex_color("e8423a") if full else hex_color("3a2a30")
    pal = {"o": OUTLINE, "R": red, "w": hex_color("ffb0a0") if full else hex_color("5a4650")}
    img.stamp(1, 2, HEART, pal)
    return img


def coin(frame):
    img = Image(12, 12)
    widths = [5, 4, 2.5, 1, 2.5, 4]
    w = widths[frame % 6]
    gold, light, dark = hex_color("f2c440"), hex_color("fff0a0"), hex_color("a87418")
    ellipse(img, 6, 6, w, 5, dark)
    ellipse(img, 6, 6, max(0.6, w - 1.2), 3.8, gold)
    if w > 2:
        img.rect(5, 4, 1, 4, light)
    finish(img, 0.15, 0.1)
    return img


# ---- obstacles, signs and effects --------------------------------------------------

def boulder():
    img = Image(20, 18)
    rock, light, dark = hex_color("7e766c"), hex_color("a49c90"), hex_color("4e4740")
    ellipse(img, 10, 10, 8.5, 7.5, rock)
    ellipse(img, 8, 8, 4, 3, light)
    rng = random.Random(3)
    for _ in range(18):
        x, y = rng.randrange(3, 17), rng.randrange(4, 17)
        if img.get(x, y)[3]:
            img.set(x, y, dark)
    line(img, 12, 5, 14, 10, dark)
    finish(img)
    return img


def sign(name):
    img = Image(16, 28)
    wood, wood_dark = hex_color("8a5a32"), hex_color("5a3820")
    img.rect(7, 12, 2, 16, wood_dark)
    img.rect(1, 0, 14, 14, wood)
    img.rect(1, 13, 14, 1, wood_dark)
    color = hex_color(FORM_COLORS[name])
    img.rect(2, 1, 12, 12, mix(color, (0, 0, 0, 255), 0.55))
    finish(img, 0.15, 0.2)
    img.paste(icon(name, FORM_COLORS[name] if name != "golem" else "a8a49a"), 2, 1)
    return img


def effects():
    """16x16 frames: 0-3 dust puff, 4-7 sparkle, 8-11 splash, 12-13 rock chunks, 14-17 shift ring, 18-19 leaves."""
    sheet = Image(16 * 20, 16)
    dust = hex_color("d8d0c0", 220)
    for i in range(4):
        f = Image(16, 16)
        r = 2 + i * 1.6
        a = 220 - i * 50
        ellipse(f, 8, 9, r, r * 0.8, (dust[0], dust[1], dust[2], a))
        ellipse(f, 5, 11, r * 0.6, r * 0.5, (dust[0], dust[1], dust[2], a))
        sheet.paste(f, i * 16, 0)
    star = hex_color("fff4b0")
    for i in range(4):
        f = Image(16, 16)
        n = [2, 5, 4, 2][i]
        for d in range(-n, n + 1):
            f.set(8 + d, 8, star)
            f.set(8, 8 + d, star)
        if i in (1, 2):
            for d in (-1, 1):
                f.set(8 + d, 8 + d, star)
                f.set(8 - d, 8 + d, star)
        sheet.paste(f, (4 + i) * 16, 0)
    water = hex_color("9ad8f0", 230)
    for i in range(4):
        f = Image(16, 16)
        for k in range(5):
            ang = math.pi * (0.15 + 0.7 * k / 4)
            dist = 2 + i * 1.8
            x = 8 + math.cos(ang) * dist
            y = 12 - math.sin(ang) * dist * 1.2 + i * i * 0.3
            ellipse(f, x, y, 1.2, 1.2, water)
        sheet.paste(f, (8 + i) * 16, 0)
    for i in range(2):
        f = Image(16, 16)
        ellipse(f, 8, 8, 3 + i, 2.5 + i, hex_color("8a857a" if i == 0 else "6e695f"))
        finish(f)
        sheet.paste(f, (12 + i) * 16, 0)
    for i in range(4):
        f = Image(16, 16)
        r = 2.5 + i * 1.7
        for a in range(48):
            t = a / 48 * math.tau
            f.set(int(round(8 + math.cos(t) * r)), int(round(8 + math.sin(t) * r)), (255, 255, 255, 255 - i * 55))
        sheet.paste(f, (14 + i) * 16, 0)
    for i in range(2):
        f = Image(16, 16)
        leaf = hex_color("5aa040" if i == 0 else "86c95a")
        ellipse(f, 8, 8, 3, 1.6, leaf)
        f.set(5, 8, hex_color("2c5a24"))
        sheet.paste(f, (18 + i) * 16, 0)
    return sheet


# ---- tiles ----------------------------------------------------------------------

def tiles():
    """Rows: 0 forest, 1 cavern. Columns (16x16): 0 top, 1 fill, 2 edge left, 3 edge right,
    4 water top, 5 water, 6 ceiling, 7 wall brick, 8 wall top."""
    sheet = Image(16 * 9, 32)
    themes = [
        {"top": "6aa83a", "top_light": "9bd25a", "dirt": "6a4a32", "dirt_dark": "4a3222", "stone": "8a7a66", "stone_dark": "5e5040"},
        {"top": "4f7a6a", "top_light": "7ab09a", "dirt": "3e4458", "dirt_dark": "2a2e3e", "stone": "5e6478", "stone_dark": "3a3e50"},
    ]
    for row, t in enumerate(themes):
        c = {k: hex_color(v) for k, v in t.items()}
        rng = random.Random(11 + row)
        oy = row * 16

        def fill(img, ox):
            img.rect(ox, oy, 16, 16, c["dirt"])
            for _ in range(5):
                x, y = rng.randrange(0, 14), rng.randrange(0, 14)
                for dx in range(3):
                    for dy in range(2):
                        img.set(ox + (x + dx) % 16, oy + y + dy, c["stone"])
                img.set(ox + (x + 1) % 16, oy + y + 2, c["stone_dark"])
            for _ in range(10):
                img.set(ox + rng.randrange(16), oy + rng.randrange(16), c["dirt_dark"])

        # 1 fill, then 0 top over a fill.
        fill(sheet, 16)
        fill(sheet, 0)
        sheet.rect(0, oy, 16, 4, c["top"])
        for x in range(16):
            h = 4 + (1 if (x * 5 + row) % 3 == 0 else 0)
            sheet.rect(x, oy + 4, 1, h - 4, c["top"])
            if x % 2 == 0:
                sheet.set(x, oy, c["top_light"])
            if (x * 3) % 7 == 0:
                sheet.set(x, oy + 1, c["top_light"])
        sheet.rect(0, oy + 5, 16, 1, c["dirt_dark"]) if row == 1 else None
        # 2, 3 edges: the top tile with a crumbling side and a dark cut face.
        for col, side in ((2, -1), (3, 1)):
            ox = col * 16
            for y in range(16):
                for x in range(16):
                    sheet.set(ox + x, oy + y, sheet.get(x, oy + y))
            for y in range(16):
                cut = 2 + (y * 7 % 3)
                for k in range(cut):
                    x = k if side < 0 else 15 - k
                    sheet.set(ox + x, oy + y, c["dirt_dark"] if y > 3 else c["top"])
        # 4, 5 water (translucent so the tunnel behind shows through).
        deep, shallow, foam = hex_color("2a6aa0", 210), hex_color("4a9ad0", 210), hex_color("c8f0ff", 240)
        sheet.rect(5 * 16, oy, 16, 16, deep)
        for y in range(16):
            for x in range(16):
                if (x + y * 2) % 11 == 0:
                    sheet.set(5 * 16 + x, oy + y, shallow)
        sheet.rect(4 * 16, oy, 16, 16, deep)
        for x in range(16):
            wave = 1 + int(round(math.sin(x / 16 * math.tau) * 1.2))
            sheet.rect(4 * 16 + x, oy + wave + 1, 1, 3, shallow)
            sheet.set(4 * 16 + x, oy + wave, foam)
            for y in range(wave):
                sheet.set(4 * 16 + x, oy + y, CLEAR)
        # 6 ceiling: rock with a dripping lower edge.
        rock = Image(16, 16)
        rock.rect(0, 0, 16, 16, c["stone_dark"])
        for x in range(16):
            drop = 11 + int(round(2 * math.sin(x * 1.3) + (2 if x % 5 == 2 else 0)))
            for y in range(drop, 16):
                rock.set(x, y, CLEAR)
        for _ in range(12):
            x, y = rng.randrange(16), rng.randrange(10)
            rock.set(x, y, c["stone"])
        sheet.paste(rock, 6 * 16, oy)
        # 7 wall bricks, 8 wall top (bricks with a cap).
        brick, brick_light, mortar = hex_color("8c8274"), hex_color("aaa092"), hex_color("4a443c")
        for col in (7, 8):
            ox = col * 16
            sheet.rect(ox, oy, 16, 16, mortar)
            for by in range(0, 16, 5):
                offset = 0 if (by // 5) % 2 == 0 else 4
                for bx in range(-8, 16, 8):
                    x0 = bx + offset
                    for y in range(by, min(16, by + 4)):
                        for x in range(max(0, x0), min(16, x0 + 7)):
                            sheet.set(ox + x, oy + y, brick_light if y == by else brick)
            if col == 8:
                sheet.rect(ox, oy, 16, 3, brick_light)
                sheet.rect(ox, oy + 3, 16, 1, mortar)
            # Cracks show it can be broken.
            line(sheet, ox + 5, oy + 2, ox + 9, oy + 13, mortar)
    return sheet


def thicket():
    """16x40, tiles horizontally: a dark burrow 16 pixels high under a tangle of thorny branches."""
    img = Image(16, 40)
    shade, inner = hex_color("142014"), hex_color("1f2e1a")
    img.rect(0, 24, 16, 16, inner)
    img.rect(0, 22, 16, 3, shade)
    bark, bark_dark = hex_color("6a4628"), hex_color("40281a")
    leaf, leaf_light, leaf_dark = hex_color("3e7a30"), hex_color("6aaa44"), hex_color("244a1e")
    rng = random.Random(5)
    for _ in range(26):
        x, y = rng.randrange(16), rng.randrange(2, 22)
        ellipse(img, x, y, rng.uniform(1.5, 3.5), rng.uniform(1.5, 3), [leaf, leaf_dark, leaf_light][rng.randrange(3)])
        ellipse(img, x + 16, y, 2, 2, leaf)
        ellipse(img, x - 16, y, 2, 2, leaf)
    for k in range(4):
        y0 = 4 + k * 5
        for x in range(16):
            y = y0 + int(round(2 * math.sin((x + k * 4) / 16 * math.tau)))
            img.set(x, y, bark)
            img.set(x, y + 1, bark_dark)
            if (x + k) % 5 == 0:
                img.set(x, y - 1, hex_color("d8d0b0"))
    # Roots hanging into the burrow.
    for x in (2, 7, 12):
        for y in range(24, 24 + 3 + x % 4):
            img.set(x, y, bark_dark)
    for x in range(16):
        img.set(x, 39, hex_color("2a3a22"))
    return img


# ---- backdrops ------------------------------------------------------------------------

BG_W, BG_H = 432, 240


def periodic(x, seed, octaves=((1, 1.0), (2, 0.5), (5, 0.25), (11, 0.12))):
    """Smooth noise that repeats every BG_W pixels, in [-1, 1] roughly."""
    rng = random.Random(seed)
    total = 0.0
    for freq, amp in octaves:
        phase = rng.uniform(0, math.tau)
        total += amp * math.sin(x / BG_W * math.tau * freq + phase)
    return total


def gradient(img, top, bottom):
    for y in range(img.height):
        c = mix(hex_color(top), hex_color(bottom), y / (img.height - 1))
        img.rect(0, y, img.width, 1, c)


def forest_far():
    img = Image(BG_W, BG_H)
    gradient(img, "1b3a3e", "4c7e66")
    # A pale moon and two ranges of misty canopy.
    ellipse(img, 330, 50, 16, 16, hex_color("e8f0d0", 120))
    ellipse(img, 330, 50, 12, 12, hex_color("f4f8e0", 200))
    for layer, (base, color, seed) in enumerate(((150, "2e5a4c", 1), (175, "234838", 2))):
        for x in range(BG_W):
            h = base - 22 * periodic(x, seed) - 8 * abs(math.sin(x * 0.21 + seed))
            img.rect(x, int(h), 1, BG_H - int(h), hex_color(color))
    rng = random.Random(9)
    for _ in range(60):
        x, y = rng.randrange(BG_W), rng.randrange(20, 120)
        img.set(x, y, hex_color("cfe8c0", 140))
    return img


def forest_near():
    img = Image(BG_W, BG_H)
    rng = random.Random(21)
    trunk, trunk_dark, canopy, canopy_dark = hex_color("1e2e24"), hex_color("141f19"), hex_color("19302a"), hex_color("10221c")
    for k in range(9):
        x = int(k * BG_W / 9 + rng.uniform(-10, 10))
        w = rng.randrange(8, 16)
        for dx in range(w):
            img.rect((x + dx) % BG_W, 0, 1, BG_H, trunk if dx < w - 3 else trunk_dark)
        for _ in range(6):
            cx, cy = x + rng.uniform(-30, 30), rng.uniform(-10, 60)
            for ox in (0, -BG_W, BG_W):
                ellipse(img, cx + ox, cy, rng.uniform(18, 34), rng.uniform(12, 22), canopy if rng.random() < 0.6 else canopy_dark)
    for x in range(BG_W):
        h = 205 - 10 * periodic(x, 31, ((3, 1.0), (13, 0.4)))
        img.rect(x, int(h), 1, BG_H - int(h), hex_color("173024"))
        if x % 3 == 0:
            img.set(x, int(h) - 1, hex_color("2a5038"))
    return img


def cave_far():
    img = Image(BG_W, BG_H)
    gradient(img, "120f1e", "2a2a48")
    for x in range(BG_W):
        h = 70 + 30 * periodic(x, 41)
        img.rect(x, 0, 1, int(h), hex_color("1a1830"))
        h2 = 165 - 25 * periodic(x, 42)
        img.rect(x, int(h2), 1, BG_H - int(h2), hex_color("221f3a"))
    rng = random.Random(43)
    for _ in range(40):
        x, y = rng.randrange(BG_W), rng.randrange(60, 170)
        img.set(x, y, hex_color("7ae0d0", 160))
        img.set(x + 1, y, hex_color("7ae0d0", 80))
    return img


def cave_near():
    img = Image(BG_W, BG_H)
    rock, rock_dark, crystal = hex_color("2c2a44"), hex_color("1e1c32"), hex_color("5ad0c0")
    for x in range(BG_W):
        h = 40 + 26 * periodic(x, 51, ((2, 1.0), (7, 0.6), (17, 0.3)))
        img.rect(x, 0, 1, int(h), rock)
        img.set(x, int(h), rock_dark)
    for k in range(7):
        x = int(k * BG_W / 7 + 20)
        for y in range(40, 120):
            w = 3 + (120 - y) // 14
            img.rect(x - w // 2, y, w, 1, rock_dark)
    rng = random.Random(52)
    for _ in range(14):
        x, y = rng.randrange(BG_W), rng.randrange(10, 60)
        triangle(img, (x, y), (x + 3, y), (x + 1.5, y + rng.randrange(4, 9)), crystal)
    for x in range(BG_W):
        h = 215 - 8 * periodic(x, 53, ((4, 1.0), (9, 0.5)))
        img.rect(x, int(h), 1, BG_H - int(h), hex_color("1a182c"))
    return img


# ---- the app icon -------------------------------------------------------------------

def app_icon():
    img = Image(64, 64)
    ellipse(img, 32, 32, 31.5, 31.5, OUTLINE)
    ellipse(img, 32, 32, 29.5, 29.5, hex_color("1f3a3c"))
    ellipse(img, 32, 30, 26, 24, hex_color("2a4e4a"))
    for i, name in enumerate(("fox", "bird", "golem", "fish")):
        badge = form_badge(name, 28)
        img.paste(badge, 4 + (i % 2) * 28, 4 + (i // 2) * 28)
    return img.scaled(4)


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    out = {}
    for skin in SKINS:
        out[f"Runner_{skin}.png"] = runner_sheet(skin)
    items = Image(12 * 8, 12)
    for i in range(6):
        items.paste(coin(i), i * 12, 0)
    items.paste(heart(True), 6 * 12, 0)
    items.paste(heart(False), 7 * 12, 0)
    out["Items.png"] = items
    signs = Image(16 * 5, 28)
    for i, name in enumerate(("fox", "bird", "golem", "fish", "jump")):
        signs.paste(sign(name), i * 16, 0)
    out["Signs.png"] = signs
    out["Boulder.png"] = boulder()
    out["Effects.png"] = effects()
    out["Tiles.png"] = tiles()
    out["Thicket.png"] = thicket()
    out["Background_forest_far.png"] = forest_far()
    out["Background_forest_near.png"] = forest_near()
    out["Background_cave_far.png"] = cave_far()
    out["Background_cave_near.png"] = cave_near()
    for name in ("fox", "bird", "golem", "fish"):
        out[f"Badge_{name}.png"] = form_badge(name, 32)
    out["Heart.png"] = heart(True)
    out["HeartEmpty.png"] = heart(False)
    out["Coin.png"] = coin(0)
    out["AppIcon.png"] = app_icon()
    for name, img in out.items():
        img.save(str(OUTPUT / name))
        print("drew", name, f"{img.width}x{img.height}")


if __name__ == "__main__":
    main()
