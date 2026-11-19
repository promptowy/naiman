"""Collect the review package: python site/ocena.py

Builds build/podglad (preview pages + NAIMAN.exe + zrzuty.html) and ocena/ (the same pictures as files).
Run after: build.ps1 -OutDir dist/next, NAIMAN.exe --shots, site/build.py --preview.
"""
import hashlib
import shutil
import subprocess
import sys
from pathlib import Path

from PIL import Image
from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "build" / "podglad"
REVIEW = ROOT / "ocena"
EXE = ROOT / "dist" / "next" / "NAIMAN.exe"
PUBLIC = "https://promptowy.github.io/naiman/"


def grab(browser, url, target, width, height, full):
    phone = width < 500
    ctx = browser.new_context(viewport={"width": width, "height": height}, device_scale_factor=2 if phone else 1,
                              is_mobile=phone, has_touch=phone)
    page = ctx.new_page()
    page.goto(url)
    page.wait_for_load_state("networkidle")
    page.evaluate("document.querySelectorAll('img[loading=lazy]').forEach(i => i.loading = 'eager')")
    page.evaluate("document.fonts.ready")
    page.wait_for_timeout(600)
    wide = page.evaluate("document.documentElement.scrollWidth > document.documentElement.clientWidth")
    page.screenshot(path=str(target), full_page=full)
    ctx.close()
    return wide


def pair(before, after, target):
    a, b = Image.open(before).convert("RGB"), Image.open(after).convert("RGB")
    h = 900
    a = a.resize((int(a.width * h / a.height), h), Image.LANCZOS)
    b = b.resize((int(b.width * h / b.height), h), Image.LANCZOS)
    sheet = Image.new("RGB", (a.width + b.width + 60, h + 40), (40, 40, 40))
    sheet.paste(a, (20, 20))
    sheet.paste(b, (a.width + 40, 20))
    sheet.save(target)


def main():
    if OUT.exists():
        shutil.rmtree(OUT)
    shutil.copytree(ROOT / "docs", OUT, ignore=shutil.ignore_patterns("testing.md"))
    shutil.copy2(EXE, OUT / "NAIMAN.exe")
    shots = OUT / "zrzuty"
    for folder in (shots / "aplikacja", shots / "landing", shots / "przed", REVIEW / "przed"):
        folder.mkdir(parents=True, exist_ok=True)
    for scale, tag in (("1", "100"), ("1.25", "125"), ("1.5", "150")):
        target = shots / "aplikacja" / tag
        subprocess.run([str(EXE), "--shots", str(target), scale], check=True)
    notes = []
    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        for lang, name in (("pl", "pl.html"), ("en", "index.html")):
            for width, height, tag in ((1440, 900, "komputer-1440"), (390, 844, "telefon-390")):
                url = (OUT / name).as_uri()
                wide = grab(browser, url, shots / "landing" / f"{tag}-{lang}.png", width, height, False)
                grab(browser, url, shots / "landing" / f"{tag}-{lang}-cala.png", width, height, True)
                notes.append(f"{lang} {width}px: {'POZIOMY SCROLL' if wide else 'bez poziomego scrolla'}")
        for width, height, tag in ((1440, 900, "komputer-1440"), (390, 844, "telefon-390")):
            grab(browser, PUBLIC + "pl.html", shots / "przed" / f"landing-{tag}-pl.png", width, height, False)
        browser.close()
    for old in (REVIEW / "przed").glob("aplikacja-start-*.png"):
        shutil.copy2(old, shots / "przed" / old.name)
    pair(shots / "przed" / "aplikacja-start-pl.png", shots / "aplikacja" / "100" / "01-start-pl.png", shots / "przed-po-aplikacja.png")
    pair(shots / "przed" / "landing-komputer-1440-pl.png", shots / "landing" / "komputer-1440-pl.png", shots / "przed-po-landing.png")
    pair(shots / "przed" / "landing-telefon-390-pl.png", shots / "landing" / "telefon-390-pl.png", shots / "przed-po-telefon.png")

    digest = hashlib.sha256(EXE.read_bytes()).hexdigest()
    names = {"01-start": "Pierwszy ekran", "02-xbox-konsola": "Xbox, krok 1: na konsoli", "03-xbox-edge": "Xbox, krok 2: w Edge",
             "03b-xbox-edge-otwarte": "Xbox, krok 2 po otwarciu Edge", "04-xbox-okno": "Xbox, krok 3: okno gry",
             "04b-ps5-okno": "PS5, krok 3: okno gry", "05-ps5-konsola": "PS5, krok 1: na konsoli",
             "06-ps5-komputer": "PS5, krok 2: na komputerze", "07-skrot-kilka-okien": "Skrót: gra już otwarta, kilka okien",
             "08-brak-okna": "Pusty stan: brak okna", "09-hdmi": "Karta HDMI", "10-sesja-start": "Sesja: start przechwytywania",
             "11-sesja-trwa": "Sesja: gra działa", "12-sesja-fokus": "Sesja: gra straciła fokus", "13-brak-zrodla": "Sesja: utrata źródła",
             "14-zrodlo-wrocilo": "Sesja: źródło wróciło", "15-koniec": "Sesja: zakończenie", "16-test-ruchu": "Test bez konsoli"}

    def fig(src, caption, cls=""):
        return f'<figure class="{cls}"><a href="{src}"><img loading="lazy" src="{src}" alt="{caption}"></a><figcaption>{caption}</figcaption></figure>'

    app_pl = "".join(fig(f"zrzuty/aplikacja/100/{k}-pl.png", v) for k, v in names.items())
    app_en = "".join(fig(f"zrzuty/aplikacja/100/{k}-en.png", v + " (EN)") for k, v in names.items() if k in ("01-start", "02-xbox-konsola", "06-ps5-komputer", "07-skrot-kilka-okien", "11-sesja-trwa"))
    dpi = "".join(fig(f"zrzuty/aplikacja/{d}/01-start-pl.png", f"Pierwszy ekran przy skali {d}%") for d in ("100", "125", "150"))
    land = "".join(fig(f"zrzuty/landing/{t}-{l}.png", f"Landing {l.upper()}, {t.replace('-', ' ')} px", "tall" if "telefon" in t else "")
                   for l in ("pl", "en") for t in ("komputer-1440", "telefon-390"))
    full = "".join(f'<li><a href="zrzuty/landing/{t}-{l}-cala.png">Cała strona {l.upper()}, {t.replace("-", " ")} px</a></li>'
                   for l in ("pl", "en") for t in ("komputer-1440", "telefon-390"))
    html = f'''<!doctype html>
<html lang="pl">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<meta name="robots" content="noindex,nofollow">
<title>NAIMAN | zrzuty do oceny</title>
<link rel="stylesheet" href="field.css">
<style>
.review {{ padding: 48px 0 88px; }}
.review h1 {{ font-size: clamp(30px, 4vw, 46px); line-height: 1.08; }}
.review h2 {{ font-size: 24px; margin: 64px 0 20px; padding-top: 24px; border-top: 1px solid var(--line); }}
.review p, .review li {{ color: var(--muted); max-width: 60em; }}
.review ul {{ margin-top: 14px; }}
.review li {{ padding: 8px 0; border-top: 1px solid var(--line); }}
.review a {{ color: var(--ink); text-underline-offset: 5px; text-decoration-color: var(--action); }}
.links {{ display: flex; flex-wrap: wrap; gap: 12px; margin-top: 26px; }}
.links a {{ height: 48px; padding: 0 20px; display: grid; place-items: center; border: 1px solid var(--line); font-weight: 600; text-decoration: none; }}
.links a:first-child {{ background: var(--action); color: var(--on); border-color: var(--action); }}
.grid {{ display: grid; grid-template-columns: repeat(auto-fill, minmax(min(100%, 520px), 1fr)); gap: 28px 24px; }}
.grid.three {{ grid-template-columns: repeat(auto-fill, minmax(min(100%, 360px), 1fr)); }}
figure img {{ display: block; width: 100%; height: auto; border: 1px solid var(--line); }}
figure.tall img {{ max-width: 390px; }}
figcaption {{ margin-top: 8px; font-size: 14.5px; color: var(--muted); }}
.wide img {{ width: 100%; height: auto; border: 1px solid var(--line); display: block; }}
code {{ font: 400 13px ui-monospace, Consolas, monospace; word-break: break-all; }}
</style>
</head>
<body>
<header class="top"><a class="home" href="pl.html"><svg class="mark" viewBox="0 0 532 100"><use href="#m"/></svg></a></header>
<main class="review">
<h1>NAIMAN 0.3: aplikacja i landing do oceny</h1>
<div class="links"><a href="NAIMAN.exe" download>Pobierz NAIMAN.exe</a><a href="pl.html">Landing PL</a><a href="./">Landing EN</a></div>
<p style="margin-top:18px">SHA-256 pliku: <code>{digest}</code></p>
<h2>Przed i po</h2>
<p>Po lewej wersja publiczna 0.2, po prawej nowa.</p>
<div class="wide" style="margin-top:20px"><a href="zrzuty/przed-po-aplikacja.png"><img src="zrzuty/przed-po-aplikacja.png" alt="Aplikacja przed i po"></a></div>
<div class="wide" style="margin-top:24px"><a href="zrzuty/przed-po-landing.png"><img src="zrzuty/przed-po-landing.png" alt="Landing przed i po"></a></div>
<div class="wide" style="margin-top:24px;max-width:860px"><a href="zrzuty/przed-po-telefon.png"><img src="zrzuty/przed-po-telefon.png" alt="Landing na telefonie przed i po"></a></div>
<h2>Aplikacja, wszystkie ekrany (PL)</h2>
<p>Obraz rysuje sama aplikacja w trybie zrzutów. Na listach okien są wpisy przykładowe.</p>
<div class="grid" style="margin-top:20px">{app_pl}</div>
<h2>Aplikacja po angielsku</h2>
<div class="grid">{app_en}</div>
<h2>Skala 100, 125 i 150%</h2>
<div class="grid three">{dpi}</div>
<h2>Landing</h2>
<div class="grid">{land}</div>
<ul>{full}</ul>
</main>
</body>
</html>
'''
    mark = (OUT / "pl.html").read_text(encoding="utf-8")
    start = mark.index('<svg class="mark"')
    svg = mark[start:mark.index("</svg>", start) + 6]
    html = html.replace('<svg class="mark" viewBox="0 0 532 100"><use href="#m"/></svg>', svg)
    (OUT / "zrzuty.html").write_text(html, encoding="utf-8", newline="\n")

    if (REVIEW / "zrzuty").exists():
        shutil.rmtree(REVIEW / "zrzuty")
    shutil.copytree(shots, REVIEW / "zrzuty")
    shutil.copy2(EXE, REVIEW / "NAIMAN.exe")
    print("\n".join(notes))
    print("sha256", digest)


if __name__ == "__main__":
    sys.exit(main())
