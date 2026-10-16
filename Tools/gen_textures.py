"""Generates EatingAnim's tool textures (the things pawns hold that the game has no item for).

Everything that exists in-game (food, meals, ingredients, products, books, medicine, apparel,
weapons, meat) is drawn with the game's own graphics at runtime - these are only the tools.

Drawn 4x supersampled at 128px for smooth edges, in RimWorld's flat-shaded outlined style.
Every tool points "up" (its working end at the top); the code rotates it.
    python3 Tools/gen_textures.py
"""
import math
import os
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "Textures", "EatingAnim")
S = 4
N = 128
OL = (28, 22, 18, 255)

# palette: (base, shadow, highlight)
STEEL = ((178, 186, 196, 255), (118, 126, 138, 255), (236, 241, 246, 255))
IRON = ((74, 78, 86, 255), (44, 46, 52, 255), (128, 134, 144, 255))
WOOD = ((158, 108, 62, 255), (112, 72, 38, 255), (198, 148, 96, 255))
DWOOD = ((104, 66, 40, 255), (72, 44, 26, 255), (142, 96, 62, 255))
BRASS = ((196, 156, 72, 255), (148, 110, 44, 255), (238, 206, 128, 255))
STONE = ((168, 160, 150, 255), (120, 112, 104, 255), (210, 204, 196, 255))
PAPER = ((240, 232, 212, 255), (204, 192, 166, 255), (255, 252, 240, 255))
CLOTH = ((236, 234, 226, 255), (190, 186, 176, 255), (255, 255, 255, 255))
RED = ((186, 52, 46, 255), (130, 30, 28, 255), (226, 104, 92, 255))
STRAW = ((214, 178, 92, 255), (164, 128, 56, 255), (240, 214, 140, 255))
FEATHER = ((236, 236, 240, 255), (176, 180, 192, 255), (255, 255, 255, 255))


def new():
    img = Image.new("RGBA", (N * S, N * S), (0, 0, 0, 0))
    return img, ImageDraw.Draw(img)


def sc(pts):
    return [(x * S, y * S) for x, y in pts]


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    img.resize((N, N), Image.LANCZOS).save(os.path.join(OUT, name + ".png"))


def shape(img, draw_fn, pal, light=(-1, -1), ow=2.5, inset=3.0):
    """Draw a flat shape with outline, then shade it: shadow on the far side, rim light on the near.

    draw_fn(draw, fill) draws the silhouette in a single colour onto a mask layer.
    """
    base, shadow, hi = pal
    W = N * S
    mask = Image.new("L", (W, W), 0)
    draw_fn(ImageDraw.Draw(mask), 255)
    # outline = dilated mask
    grow = int(ow * S) * 2 + 1
    outline = mask.filter(ImageFilter.MaxFilter(grow))
    img.paste(Image.new("RGBA", (W, W), OL), (0, 0), outline)
    img.paste(Image.new("RGBA", (W, W), base), (0, 0), mask)
    # shading via shifted masks
    d = int(inset * S)
    lx, ly = light
    shifted_dark = Image.new("L", (W, W), 0)
    shifted_dark.paste(mask, (int(-lx * d), int(-ly * d)))
    shade = Image.composite(Image.new("L", (W, W), 0), mask, shifted_dark)  # mask minus shifted
    img.paste(Image.new("RGBA", (W, W), shadow), (0, 0), shade)
    shifted_light = Image.new("L", (W, W), 0)
    shifted_light.paste(mask, (int(lx * d * 0.7), int(ly * d * 0.7)))
    rim = Image.composite(Image.new("L", (W, W), 0), mask, shifted_light)
    img.paste(Image.new("RGBA", (W, W), hi), (0, 0), rim)


def rect(x0, y0, x1, y1, r=0):
    return lambda d, f: d.rounded_rectangle((x0 * S, y0 * S, x1 * S, y1 * S), radius=r * S, fill=f)


def ell(x0, y0, x1, y1):
    return lambda d, f: d.ellipse((x0 * S, y0 * S, x1 * S, y1 * S), fill=f)


def poly(*pts):
    return lambda d, f: d.polygon(sc(pts), fill=f)


def line(pts, w):
    def fn(d, f):
        d.line(sc(pts), fill=f, width=int(w * S), joint="curve")
        r = w * S / 2
        for x, y in (pts[0], pts[-1]):
            d.ellipse((x * S - r, y * S - r, x * S + r, y * S + r), fill=f)
    return fn


def both(*fns):
    def fn(d, f):
        for g in fns:
            g(d, f)
    return fn


def handle(img, x, y0, y1, w=7, pal=WOOD, ferrule=None):
    shape(img, rect(x - w / 2, y0, x + w / 2, y1, w / 2), pal)
    if ferrule:
        shape(img, rect(x - w / 2 - 1, y0 - 2, x + w / 2 + 1, y0 + 6, 1), ferrule, inset=1.5)


# ----------------------------------------------------------------- kitchen

def frying_pan():
    img, d = new()
    handle(img, 64, 74, 124, 9, DWOOD, ferrule=IRON)
    shape(img, ell(16, 12, 112, 84), IRON, inset=4)
    shape(img, ell(26, 20, 102, 76), ((58, 60, 66, 255), (38, 40, 44, 255), (92, 96, 104, 255)), light=(1, 1), inset=3)
    save(img, "FryingPan")


def pot():
    img, d = new()
    shape(img, rect(8, 34, 26, 44, 4), IRON)      # side handles
    shape(img, rect(102, 34, 120, 44, 4), IRON)
    shape(img, both(rect(20, 30, 108, 104, 18), ell(20, 88, 108, 116)), ((96, 100, 110, 255), (60, 62, 70, 255), (150, 156, 168, 255)), inset=5)
    shape(img, ell(18, 18, 110, 46), IRON, inset=2)
    shape(img, ell(26, 22, 102, 42), ((70, 50, 34, 255), (48, 34, 22, 255), (96, 72, 50, 255)), light=(1, 1), inset=2)
    save(img, "Pot")


def spatula():
    img, d = new()
    handle(img, 64, 54, 124, 8, WOOD)
    shape(img, rect(46, 6, 82, 58, 8), WOOD, inset=3)
    for x in (56, 64, 72):
        d.line(sc([(x, 14), (x, 46)]), fill=WOOD[1], width=2 * S)
    save(img, "Spatula")


def wooden_spoon():
    img, d = new()
    handle(img, 64, 40, 124, 8, WOOD)
    shape(img, ell(46, 4, 82, 50), WOOD, inset=3)
    shape(img, ell(52, 10, 76, 42), ((136, 90, 50, 255), (112, 72, 38, 255), (170, 120, 74, 255)), light=(1, 1), inset=2)
    save(img, "WoodenSpoon")


def spoon():
    img, d = new()
    shape(img, rect(60, 44, 68, 122, 4), STEEL)
    shape(img, ell(48, 6, 80, 50), STEEL, inset=3)
    shape(img, ell(53, 11, 75, 44), ((150, 158, 170, 255), (120, 128, 140, 255), (210, 216, 224, 255)), light=(1, 1), inset=2)
    save(img, "Spoon")


def fork():
    img, d = new()
    shape(img, rect(60, 50, 68, 122, 4), STEEL)
    shape(img, both(rect(46, 34, 82, 56, 8), *[rect(x, 6, x + 6, 44, 3) for x in (46, 56, 66, 76)]), STEEL, inset=2)
    save(img, "Fork")


def knife():
    img, d = new()
    handle(img, 64, 80, 124, 10, DWOOD)
    for y in (90, 104, 116):
        d.ellipse(sc([(62, y), (66, y + 4)]), fill=BRASS[0])
    shape(img, poly((54, 82), (76, 82), (76, 20), (56, 4), (54, 30)), STEEL, light=(1, -1))
    d.line(sc([(55, 32), (57, 80)]), fill=STEEL[2], width=2 * S)
    save(img, "Knife")


def cleaver():
    img, d = new()
    handle(img, 64, 78, 124, 11, DWOOD)
    shape(img, poly((38, 80), (90, 80), (92, 10), (38, 18)), STEEL, light=(1, -1), inset=4)
    d.line(sc([(40, 22), (40, 76)]), fill=STEEL[2], width=3 * S)
    d.ellipse(sc([(76, 22), (86, 32)]), fill=OL)
    save(img, "Cleaver")


def mortar():
    img, d = new()
    shape(img, both(poly((14, 50), (114, 50), (96, 112), (32, 112)), rect(36, 104, 92, 120, 4)), STONE, inset=5)
    shape(img, ell(12, 36, 116, 64), STONE, inset=2)
    shape(img, ell(22, 40, 106, 60), ((92, 120, 70, 255), (66, 92, 48, 255), (130, 160, 96, 255)), light=(1, 1), inset=2)
    save(img, "Mortar")


def pestle():
    img, d = new()
    shape(img, both(rect(56, 30, 72, 112, 8), ell(50, 4, 78, 40)), STONE, inset=3)
    save(img, "Pestle")


# ----------------------------------------------------------------- crafting / work

def hammer():
    img, d = new()
    handle(img, 64, 30, 124, 9, WOOD)
    shape(img, both(rect(30, 10, 98, 36, 3), rect(22, 14, 34, 32, 2)), IRON, inset=3)
    save(img, "Hammer")


def mallet():
    img, d = new()
    handle(img, 64, 40, 124, 8, WOOD)
    shape(img, rect(28, 6, 100, 46, 8), DWOOD, inset=4)
    d.line(sc([(36, 8), (36, 44)]), fill=DWOOD[1], width=2 * S)
    d.line(sc([(92, 8), (92, 44)]), fill=DWOOD[1], width=2 * S)
    save(img, "Mallet")


def chisel():
    img, d = new()
    handle(img, 64, 70, 124, 12, WOOD, ferrule=BRASS)
    shape(img, poly((58, 72), (70, 72), (70, 18), (64, 6), (58, 18)), STEEL)
    save(img, "Chisel")


def needle():
    img, d = new()
    shape(img, poly((62, 6), (66, 6), (67, 104), (64, 112), (61, 104)), STEEL, inset=1)
    d.ellipse(sc([(62, 92), (66, 102)]), fill=OL)
    d.line(sc([(64, 98), (84, 108), (98, 98), (110, 110), (100, 124)]), fill=OL, width=5 * S, joint="curve")
    d.line(sc([(64, 98), (84, 108), (98, 98), (110, 110), (100, 124)]), fill=RED[0], width=3 * S, joint="curve")
    save(img, "Needle")


def saw():
    img, d = new()
    shape(img, both(rect(44, 92, 84, 124, 10)), DWOOD, inset=3)
    d.ellipse(sc([(54, 102), (74, 116)]), fill=OL)
    teeth = [(46, 94)]
    for i, y in enumerate(range(90, 6, -7)):
        teeth.append((44 if i % 2 == 0 else 40, y))
    teeth += [(46, 6), (78, 14), (80, 94)]
    shape(img, poly(*teeth), STEEL, light=(1, -1), inset=3)
    save(img, "Saw")


def pickaxe():
    img, d = new()
    handle(img, 64, 22, 126, 9, WOOD)
    shape(img, poly((6, 40), (30, 18), (64, 10), (98, 18), (122, 40), (98, 30), (64, 24), (30, 30)), IRON, inset=3)
    save(img, "Pickaxe")


def trowel():
    img, d = new()
    handle(img, 64, 84, 126, 12, ((70, 130, 70, 255), (46, 92, 46, 255), (110, 170, 110, 255)), ferrule=IRON)
    shape(img, poly((64, 4), (90, 54), (80, 74), (64, 80), (48, 74), (38, 54)), STEEL)
    d.line(sc([(64, 14), (64, 74)]), fill=STEEL[1], width=2 * S)
    save(img, "Trowel")


def sickle():
    img, d = new()
    handle(img, 40, 78, 126, 11, WOOD, ferrule=IRON)
    def blade(dr, f):
        # crescent: big disc minus an offset disc, top half only
        dr.ellipse((30 * S, 6 * S, 122 * S, 98 * S), fill=f)
        dr.ellipse((44 * S, 22 * S, 128 * S, 108 * S), fill=0)
        dr.rectangle((0, 60 * S, 128 * S, 128 * S), fill=0)
        dr.rectangle((34 * S, 50 * S, 46 * S, 80 * S), fill=f)
    shape(img, blade, STEEL, inset=2)
    save(img, "Sickle")


def broom():
    img, d = new()
    handle(img, 64, 4, 74, 8, WOOD)
    shape(img, poly((50, 70), (78, 70), (104, 124), (24, 124)), STRAW, inset=4)
    for x in range(30, 100, 9):
        d.line(sc([(64 + (x - 64) * 0.35, 76), (x, 122)]), fill=STRAW[1], width=2 * S)
    shape(img, rect(48, 70, 80, 80, 3), RED, inset=1.5)
    save(img, "Broom")


def quill():
    img, d = new()
    def vane(dr, f):
        dr.polygon(sc([(64, 108), (60, 70), (52, 30), (70, 2), (80, 26), (76, 66)]), fill=f)
    shape(img, vane, FEATHER, inset=3)
    d.line(sc([(64, 124), (66, 70), (70, 4)]), fill=FEATHER[1], width=2 * S)
    shape(img, poly((62, 108), (68, 108), (65, 126)), IRON, inset=1)
    save(img, "Quill")


def paper():
    img, d = new()
    shape(img, poly((22, 12), (106, 8), (110, 116), (18, 120)), PAPER, inset=3)
    for y in range(26, 106, 10):
        d.line(sc([(30, y + 1), (98, y - 1)]), fill=(150, 140, 120, 255), width=S)
    save(img, "Paper")


def page():
    # one page hinged on the left edge at x=64 (the spine); drawn over books
    img, d = new()
    shape(img, poly((64, 30), (114, 22), (116, 96), (64, 104)), PAPER, inset=2)
    for y in range(40, 92, 8):
        d.line(sc([(72, y + 1), (108, y - 4)]), fill=(160, 150, 130, 255), width=S)
    save(img, "Page")


def bandage():
    img, d = new()
    shape(img, poly((84, 70), (122, 82), (118, 100), (80, 90)), CLOTH, inset=2)
    shape(img, ell(18, 30, 92, 104), CLOTH, inset=4)
    shape(img, ell(42, 54, 68, 80), ((160, 154, 144, 255), (120, 116, 108, 255), (200, 196, 188, 255)), light=(1, 1), inset=2)
    shape(img, rect(26, 40, 44, 50, 2), RED, inset=1)
    save(img, "Bandage")


# ----------------------------------------------------------------- games

def card():
    img, d = new()
    shape(img, rect(34, 14, 94, 114, 8), CLOTH, inset=2)
    shape(img, poly((64, 40), (78, 64), (64, 88), (50, 64)), RED, inset=2)
    save(img, "Card")


def card_fan():
    img, _ = new()
    for i, ang in enumerate((-36, -12, 12, 36)):
        layer, _ = new()
        shape(layer, rect(44, 16, 84, 86, 6), CLOTH, inset=2)
        ld = ImageDraw.Draw(layer)
        col = RED[0] if i % 2 == 0 else (40, 40, 52, 255)
        ld.polygon(sc([(64, 32), (72, 44), (64, 56), (56, 44)]), fill=col)
        layer = layer.rotate(-ang, center=(64 * S, 112 * S), resample=Image.BICUBIC)
        img.alpha_composite(layer)
    save(img, "CardFan")


def game_piece():
    img, d = new()
    pal = ((236, 226, 204, 255), (180, 166, 140, 255), (255, 250, 236, 255))
    shape(img, both(ell(34, 94, 94, 120), poly((46, 102), (82, 102), (74, 52), (54, 52)), ell(44, 14, 84, 58)), pal, inset=4)
    save(img, "GamePiece")


def horseshoe():
    img, d = new()
    def hs(dr, f):
        dr.ellipse((22 * S, 14 * S, 106 * S, 98 * S), fill=f)
        dr.ellipse((40 * S, 32 * S, 88 * S, 80 * S), fill=0)
        dr.rectangle((40 * S, 60 * S, 88 * S, 120 * S), fill=0)
        dr.rectangle((22 * S, 56 * S, 40 * S, 116 * S), fill=f)
        dr.rectangle((88 * S, 56 * S, 106 * S, 116 * S), fill=f)
    shape(img, hs, IRON, inset=3)
    for x, y in ((30, 40), (98, 40), (30, 80), (98, 80)):
        d.ellipse(sc([(x - 3, y - 3), (x + 3, y + 3)]), fill=OL)
    save(img, "Horseshoe")


# ----------------------------------------------------------------- fx

def puff(name, col):
    img, d = new()
    W = N * S
    layer = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    ld = ImageDraw.Draw(layer)
    for (x, y, r) in ((64, 74, 34), (40, 60, 24), (88, 58, 26), (64, 40, 28)):
        ld.ellipse(((x - r) * S, (y - r) * S, (x + r) * S, (y + r) * S), fill=col)
    layer = layer.filter(ImageFilter.GaussianBlur(5 * S))
    img.alpha_composite(layer)
    save(img, name)


def spark():
    img, d = new()
    W = N * S
    glow = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse((40 * S, 40 * S, 88 * S, 88 * S), fill=(255, 170, 60, 200))
    img.alpha_composite(glow.filter(ImageFilter.GaussianBlur(10 * S)))
    d.polygon(sc([(64, 8), (72, 56), (120, 64), (72, 72), (64, 120), (56, 72), (8, 64), (56, 56)]), fill=(255, 236, 160, 255))
    d.ellipse(sc([(56, 56), (72, 72)]), fill=(255, 255, 240, 255))
    save(img, "Spark")


def preview():
    W, H = 640, 360
    img = Image.new("RGBA", (W, H), (44, 50, 44, 255))
    d = ImageDraw.Draw(img)
    for i in range(0, W, 32):
        d.line([(i, 0), (i, H)], fill=(50, 57, 50, 255))
    for j in range(0, H, 32):
        d.line([(0, j), (W, j)], fill=(50, 57, 50, 255))
    names = ["FryingPan", "Pot", "Cleaver", "Knife", "Hammer", "Mortar",
             "Quill", "Pickaxe", "Sickle", "Broom", "CardFan", "Saw"]
    for i, n in enumerate(names):
        t = Image.open(os.path.join(OUT, n + ".png")).resize((96, 96), Image.LANCZOS)
        img.alpha_composite(t, (20 + (i % 6) * 102, 60 + (i // 6) * 140))
    d.text((20, 18), "EatingAnim - lightweight pawn animations", fill=(230, 226, 210, 255))
    os.makedirs(os.path.join(HERE, "..", "About"), exist_ok=True)
    img.convert("RGB").save(os.path.join(HERE, "..", "About", "Preview.png"))


if __name__ == "__main__":
    for f in (frying_pan, pot, spatula, wooden_spoon, spoon, fork, knife, cleaver, mortar, pestle,
              hammer, mallet, chisel, needle, saw, pickaxe, trowel, sickle, broom, quill, paper, page,
              bandage, card, card_fan, game_piece, horseshoe, spark):
        f()
    puff("Steam", (240, 240, 240, 170))
    puff("Dust", (170, 150, 120, 170))
    preview()
    print("ok", len(os.listdir(OUT)))
