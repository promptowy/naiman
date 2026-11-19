"""Cut the three NAIMAN faces from OFL variable fonts: python site/fonts.py ARCHIVO.ttf PLEXSANS.ttf

Display = Archivo (wdth 116, wght 800). Sans = IBM Plex Sans 400 and 600.
Output: src/fonts/*.ttf for the Windows app and docs/media/*.woff2 for the pages.
The files are renamed because both licences reserve the original font names.
The semibold cut is its own family so GDI+ never has to guess a style link.
"""
import sys
from pathlib import Path
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont
from fontTools.subset import Subsetter, Options

ROOT = Path(__file__).resolve().parents[1]
KEEP = (list(range(0x20, 0x7F)) + list(range(0xA0, 0x180)) +
        [0x2013, 0x2014, 0x2018, 0x2019, 0x201C, 0x201D, 0x201E, 0x2022, 0x2026, 0x203A, 0x2039,
         0x2190, 0x2191, 0x2192, 0x2193, 0x00D7, 0x2212, 0x20AC])


def cut(source, axes, family, bold, name):
    font = instantiateVariableFont(TTFont(source), axes)
    options = Options()
    options.layout_features = ["kern", "liga", "tnum", "lnum", "ccmp", "locl", "mark", "mkmk"]
    options.name_IDs = [0, 1, 2, 3, 4, 5, 6, 13, 14]
    options.notdef_outline = True
    sub = Subsetter(options)
    sub.populate(unicodes=KEEP)
    sub.subset(font)
    style = "Bold" if bold else "Regular"
    for record in list(font["name"].names):
        if record.nameID in (16, 17, 21, 22, 25):
            font["name"].removeNames(nameID=record.nameID)
    for nid, text in ((1, family), (2, style), (3, family + " " + style + " 1.0"), (4, family + " " + style),
                      (6, family.replace(" ", "") + "-" + style)):
        font["name"].setName(text, nid, 3, 1, 0x409)
        font["name"].setName(text, nid, 1, 0, 0)
    font["OS/2"].usWeightClass = 700 if bold else 400
    font["OS/2"].fsSelection = (font["OS/2"].fsSelection & ~0x61) | (0x20 if bold else 0x40)
    font["head"].macStyle = 1 if bold else 0
    for table in ("STAT", "avar", "MVAR", "HVAR"):
        if table in font:
            del font[table]
    (ROOT / "src" / "fonts").mkdir(exist_ok=True)
    font.save(ROOT / "src" / "fonts" / (name + ".ttf"))
    font.flavor = "woff2"
    font.save(ROOT / "docs" / "media" / (name + ".woff2"))
    print(name, (ROOT / "src" / "fonts" / (name + ".ttf")).stat().st_size)


if __name__ == "__main__":
    archivo, plex = sys.argv[1], sys.argv[2]
    cut(archivo, {"wght": 800, "wdth": 116}, "NAIMAN Display", False, "naiman-display")
    cut(plex, {"wght": 400, "wdth": 100}, "NAIMAN Sans", False, "naiman-sans")
    cut(plex, {"wght": 600, "wdth": 100}, "NAIMAN Sans Bold", False, "naiman-sans-bold")
