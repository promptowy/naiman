"""Render the NAIMAN pages with the Python standard library.

python site/build.py              public pages, download points at the GitHub release
python site/build.py --preview    review copy: download points at NAIMAN.exe next to the pages

App pictures in docs/media/app come from the app itself: dist\\NAIMAN.exe --shots docs\\media\\app 1.5
"""
import hashlib
import sys
from html import escape
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DOCS = ROOT / "docs"
REPO = "https://github.com/promptowy/naiman"
RELEASE = "v0.3.1-preview.1"  # bump together with the published executable
HOME = "https://promptowy.com"  # the main copy of these pages lives there; GitHub Pages points to it
PREVIEW = "--preview" in sys.argv
EXE = ROOT / "dist" / "next" / "NAIMAN.exe"
XBOX = "https://www.xbox.com/remoteplay"

# The wordmark, same outlines as src/Ui.cs.
GLYPHS = {
    "N": "0,100 0,0 27,0 58,57 58,0 84,0 84,100 57,100 26,43 26,100",
    "A": "0,100 33,0 61,0 94,100 66,100 47,38 28,100",
    "I": "26,6 26,100 0,100 0,32",
    "M": "0,100 0,0 28,0 55,52 82,0 110,0 110,100 84,100 84,44 65,82 45,82 26,44 26,100",
}
ADVANCE = [("N", 89), ("A", 103), ("I", 40), ("M", 115), ("A", 99), ("N", 84)]


def logo(cls="mark"):
    x, parts = 0, []
    for letter, step in ADVANCE:
        parts.append(f'<polygon transform="translate({x})" points="{GLYPHS[letter]}"/>')
        if letter == "I":
            parts.append(f'<polygon class="pad" transform="translate({x})" points="0,0 21,0 0,21"/>')
        x += step
    return f'<svg class="{cls}" viewBox="0 0 532 100" role="img" aria-label="NAIMAN">{"".join(parts)}</svg>'


def sun():
    """The same low sun the app draws on its first screen."""
    bands = []
    for k in range(7):
        q = (k + .14) % 7 / 7
        thick = 200 * (.012 + .07 * q)
        bands.append(f'<rect x="0" y="{200 - 200 * .64 * (1 - q) ** 1.5 - thick:.1f}" width="400" height="{thick:.1f}"/>')
    return ('<svg class="sun" viewBox="0 0 400 200" aria-hidden="true"><defs><linearGradient id="dusk" x1="0" y1="0" x2="0" y2="1">'
            '<stop offset="0" stop-color="#ffb627"/><stop offset=".55" stop-color="#ff5a1f"/><stop offset="1" stop-color="#ff2d78"/></linearGradient>'
            '<clipPath id="half"><rect width="400" height="200"/></clipPath></defs>'
            f'<circle cx="200" cy="200" r="200" fill="url(#dusk)" clip-path="url(#half)"/><g class="cut">{"".join(bands)}</g></svg>')


def ticks(pl):
    """One second of picture changes, drawn to the measured counts: 25 without, 47 with 2x."""
    def row(y, n, mixed):
        out = []
        for i in range(n):
            x = 2 + i * (996 / n)
            made = mixed and i % 2 == 1
            out.append(f'<rect class="{"made" if made else "real"}" x="{x:.1f}" y="{y}" width="{996 / n * 0.56:.1f}" height="44"/>')
        return "".join(out)
    return ('<svg class="ticks" viewBox="0 0 1000 150" role="img" aria-label="'
            + ("25 zmian obrazu na sekundę w oryginale, 47 z NAIMAN 2x" if pl else "25 picture changes per second in the original, 47 with NAIMAN 2x")
            + f'">{row(8, 25, False)}{row(98, 47, True)}</svg>')


def page(pl):
    def t(en, pol):
        return pol if pl else en

    lang = t("en", "pl")
    home = t("./", "pl.html")
    sony = "https://www.playstation.com/" + t("en-us", "pl-pl") + "/support/games/playstation-remote-play-on-pc-and-mac/"
    xboxhelp = "https://support.xbox.com/" + t("en-US", "pl-PL") + "/help/games-apps/game-setup-and-play/how-to-set-up-remote-play"
    size = EXE.stat().st_size / 1048576 if EXE.exists() else 0.7
    megabytes = f"{size:.1f}".replace(".", t(".", ","))
    digest = hashlib.sha256(EXE.read_bytes()).hexdigest() if EXE.exists() else ""
    main = HOME + t("/en/naiman/", "/naiman/")
    canonical = "" if PREVIEW else f'<link rel="canonical" href="{main}">\n'
    mainlink = "" if PREVIEW else f'<a href="{main}">{t("NAIMAN on promptowy.com", "NAIMAN na promptowy.com")}</a>'
    download = "NAIMAN.exe" if PREVIEW else f"{REPO}/releases/download/{RELEASE}/NAIMAN.exe"
    release = f"{REPO}/releases/tag/{RELEASE}"

    def shot(name, alt, eager=False):
        return (f'<figure class="shot"><div class="bar">NAIMAN</div><img src="media/app/{name}-{lang}.png" width="1560" height="960" '
                f'{"" if eager else "loading=lazy "}alt="{escape(alt, quote=True)}"></figure>')

    def clicks(*rows):
        out = []
        for i, row in enumerate(rows):
            label, hint = row[0], row[1] if len(row) > 1 else ""
            last = ' class="hit"' if i == len(rows) - 1 else ""
            out.append(f'<li{last}><b>{label}</b>{f"<span>{hint}</span>" if hint else ""}</li>')
        return f'<ol class="clicks">{"".join(out)}</ol>'

    def stage(n, where, lead, body, picture, extra=""):
        return (f'<section class="stage"><div class="say"><p class="round">{t("Round", "Runda")} {n}</p><p class="where">{where}</p><p class="lead">{lead}</p>{body}{extra}</div>'
                f'{picture}</section>')

    def link(url, label):
        return f'<a class="more" href="{url}">{label} <span aria-hidden="true">→</span></a>'

    def final(picture, alt):
        return stage(3, t("In NAIMAN", "W NAIMAN"),
                      t("The window is found for you. You check the live preview and start.",
                        "Okno znajduje się samo. Sprawdzasz podgląd na żywo i zaczynasz."),
                      clicks((t("I can see the game on this PC", "Widzę grę na komputerze"), t("the button at the bottom of round 2", "przycisk na dole rundy 2")),
                             (t("Your game window", "Twoje okno z grą"), t("already selected when only one matches", "zaznaczone od razu, gdy pasuje tylko jedno")),
                             (t("Play with 2x smoothing", "Graj z wygładzaniem 2x"), t("the picture goes fullscreen on that monitor", "obraz przechodzi na pełny ekran tego monitora"))),
                      shot(picture, alt))
    xbox = (
        stage(1, t("On the Xbox", "Na Xboxie"),
              t("Once, with the controller, on the console itself. Leave the Xbox switched on.",
                "Raz, padem, na samej konsoli. Zostaw Xboxa włączonego."),
              clicks((t("Xbox button", "Przycisk Xbox"), t("on the controller, it opens the guide", "na padzie, otwiera przewodnik")),
                     (t("Profile & system", "Profil i system"),), (t("Settings", "Ustawienia"),),
                     (t("Devices & connections", "Urządzenia i połączenia"),), (t("Remote features", "Funkcje zdalne"),),
                     (t("Enable remote features", "Włącz funkcje zdalne"), t("tick this box", "zaznacz to pole"))),
              shot("02-xbox-konsola", t("NAIMAN step 1: the menu path on the Xbox", "NAIMAN, krok 1: ścieżka w menu Xboxa")),
              link(xboxhelp, t("Official Xbox guide", "Oficjalna instrukcja Xbox")))
        + stage(2, t("On this PC, in Edge", "Na komputerze, w Edge"),
                t("Connect the controller to the PC by USB or Bluetooth. In NAIMAN pick Xbox and go to round 2.",
                  "Pada podłącz do komputera przez USB albo Bluetooth. W NAIMAN wybierz Xbox i przejdź do rundy 2."),
                clicks((t("Open Xbox Remote Play", "Otwórz Xbox Remote Play"), t("the button in NAIMAN, it opens Edge", "przycisk w NAIMAN, otwiera Edge")),
                       (t("Sign in", "Zaloguj się"), t("with the Microsoft account from your console", "kontem Microsoft z konsoli")),
                       (t("Your Xbox", "Twój Xbox"), t("pick the console from the list", "wybierz konsolę z listy")),
                       (t("Start the game", "Uruchom grę"), t("with the controller, as on the TV", "padem, tak jak na telewizorze")),
                       ("F11", t("hides the browser bars", "chowa paski przeglądarki"))),
                shot("03-xbox-edge", t("NAIMAN step 2: what to click in Microsoft Edge", "NAIMAN, krok 2: co kliknąć w Microsoft Edge")),
                link(XBOX, "xbox.com/remoteplay"))
        + final("04-xbox-okno", t("NAIMAN with the Xbox Remote Play window selected and a preview", "NAIMAN z wybranym oknem Xbox Remote Play i podglądem")))
    ps5 = (
        stage(1, t("On the PS5", "Na PS5"),
              t("Once, on the console itself. Leave the PS5 switched on for the first connection.",
                "Raz, na samej konsoli. Na pierwsze połączenie zostaw PS5 włączone."),
              clicks((t("Settings", "Ustawienia"), t("the gear in the top right of the home screen", "koło zębate w prawym górnym rogu ekranu głównego")),
                     ("System",), (t("Remote Play", "Gra zdalna"),),
                     (t("Enable Remote Play", "Włącz Grę zdalną"), t("turn the switch on", "przesuń przełącznik"))),
              shot("05-ps5-konsola", t("NAIMAN step 1: the menu path on the PS5", "NAIMAN, krok 1: ścieżka w menu PS5")),
              link(sony, t("Official PlayStation guide", "Oficjalna instrukcja PlayStation")))
        + stage(2, t("On this PC", "Na komputerze"),
                t("You need Sony's free PS Remote Play app. chiaki-ng works too. In NAIMAN pick PlayStation 5 and go to round 2.",
                  "Potrzebna jest darmowa aplikacja PS Remote Play od Sony. Działa też chiaki-ng. W NAIMAN wybierz PlayStation 5 i przejdź do rundy 2."),
                clicks((t("DualSense by USB", "DualSense przez USB"), t("plug the controller into the PC", "podłącz pada kablem do komputera")),
                       (t("Open PS Remote Play", "Otwórz PS Remote Play"), t("the button in NAIMAN, or the download page", "przycisk w NAIMAN albo strona pobierania")),
                       (t("Sign In to PSN", "Zaloguj się w PSN"), t("with the account from your console", "kontem z konsoli")),
                       ("PS5", t("choose the console type", "wybierz typ konsoli")),
                       (t("Wait for the picture", "Poczekaj na obraz"), t("the first connection can take a minute", "pierwsze połączenie trwa do minuty"))),
                shot("06-ps5-komputer", t("NAIMAN step 2: connecting the PS5 on the PC", "NAIMAN, krok 2: połączenie z PS5 na komputerze")),
                link(sony, t("Get PS Remote Play", "Pobierz PS Remote Play")))
        + final("04b-ps5-okno", t("NAIMAN with the PS Remote Play window selected and a preview", "NAIMAN z wybranym oknem PS Remote Play i podglądem")))
    hdmi = (
        stage(1, t("Cables", "Kable"),
              t("The capture card sits between the console and the PC. The controller stays on the console.",
                "Karta przechwytująca stoi między konsolą a komputerem. Pad zostaje przy konsoli."),
              clicks((t("Console HDMI OUT", "HDMI OUT konsoli"), t("cable into the card's HDMI IN", "kablem do HDMI IN karty")),
                     (t("Card to the PC", "Karta do komputera"), t("USB, straight into the PC", "USB, prosto do komputera")),
                     (t("Close other capture apps", "Zamknij inne programy do nagrywania"), t("only one app can use the card", "z karty korzysta jeden program naraz"))),
              shot("09-hdmi", t("NAIMAN with a capture card selected", "NAIMAN z wybraną kartą HDMI")))
        + stage(2, t("In NAIMAN", "W NAIMAN"),
                t("Direct HDMI input is new in version 0.3. NAIMAN reads the card itself, no extra software.",
                  "Bezpośrednie wejście HDMI jest nowe w wersji 0.3. NAIMAN sam czyta obraz z karty, bez dodatkowych programów."),
                clicks((t("HDMI capture card", "Karta HDMI"), t("on the first screen", "na pierwszym ekranie")),
                       (t("Your card", "Twoja karta"), t("pick it from the list", "wybierz ją z listy")),
                       (t("Sound", "Dźwięk"), t("pick the card's audio input to hear the game on the PC", "wybierz wejście audio karty, żeby słyszeć grę na PC")),
                       (t("Play with 2x smoothing", "Graj z wygładzaniem 2x"),)),
                "")
        + stage(3, t("If the card is not listed", "Gdy karty nie ma na liście"),
                t("Use OBS as the bridge. This route has worked since version 0.2.",
                  "Użyj OBS jako pośrednika. Ta droga działa od wersji 0.2."),
                clicks((t("Video Capture Device", "Urządzenie do przechwytywania wideo"), t("add it as a source in OBS", "dodaj jako źródło w OBS")),
                       (t("Windowed Projector (Source)", "Projektor okienkowy (źródło)"), t("right-click the source", "prawy przycisk na źródle")),
                       (t("Use an OBS projector", "Użyj projektora OBS"), t("the button in NAIMAN, then pick the projector window", "przycisk w NAIMAN, potem wybierz okno projektora"))),
                ""))

    def path(key, title, body):
        return f'<article class="path" data-path id="{key}"><h3>{title}</h3>{body}</article>'

    def pick(key, title, sub):
        return f'<a class="pick" data-route="{key}" href="#{key}"><b>{title}</b><span>{sub}</span><i aria-hidden="true">→</i></a>'

    def key(*names):
        return "".join(f"<kbd>{n}</kbd>" for n in names)

    title = t("GTA VI from your console on a PC screen. With smoother motion.", "GTA VI z konsoli na ekranie PC. Z płynniejszym ruchem.")
    copy = t("On consoles the game runs at 30 frames. NAIMAN takes the picture from your PS5 or Xbox, draws one more frame between every two and puts the game fullscreen on your monitor. One file, free, no installer.",
             "Na konsolach gra chodzi w 30 klatkach. NAIMAN bierze obraz z PS5 albo Xboxa, dorysowuje klatkę między każdymi dwiema i daje grę na pełny ekran monitora. Jeden plik, za darmo, bez instalatora.")
    get = t("Download for Windows 11", "Pobierz na Windows 11")
    sums = (f'<span class="sum"><b>SHA-256</b> <code>{digest}</code></span>' if digest
            else f'<a href="{release}">{t("Release notes and SHA-256", "Wydanie i suma SHA-256")}</a>')
    html = f'''<!doctype html>
<html lang="{lang}">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<meta name="robots" content="noindex,{"nofollow" if PREVIEW else "follow"}">
<meta name="description" content="{escape(copy, quote=True)}">
{canonical}<title>NAIMAN | {t("PS5 and Xbox on a PC screen, smoother", "PS5 i Xbox na ekranie PC, płynniej")}</title>
<link rel="preload" href="media/naiman-display.woff2" as="font" type="font/woff2" crossorigin>
<link rel="stylesheet" href="field.css">
<script src="guide.js" defer></script>
<script src="demo.js" defer></script>
<script src="stats.js" defer></script>
</head>
<body>
<header class="top">
<a class="home" href="{home}">{logo()}</a>
<nav aria-label="{t("Sections", "Sekcje")}"><a href="#ps5" data-route="ps5">PS5</a><a href="#xbox" data-route="xbox">Xbox</a><a href="#hdmi" data-route="hdmi">HDMI</a><a href="#demo">{t("Live", "Na żywo")}</a><a href="#przewagi">{t("Why us", "Przewagi")}</a><a href="#pomiar">{t("Measurement", "Pomiar")}</a></nav>
<div class="lang"><a {"aria-current=true " if pl else ""}lang="pl" href="pl.html">PL</a><a {"" if pl else "aria-current=true "}lang="en" href="./">EN</a></div>
</header>
<main>
<section class="hero">
<div class="pitch">
<p class="bill">{t("30 frames in the blue corner. 2x in the orange one.", "W niebieskim narożniku 30 klatek. W pomarańczowym 2x.")}</p>
<h1>{title}</h1>
<p class="sub">{copy}</p>
<div class="get"><a class="cta" href="{download}"{" download" if PREVIEW else ""}><span>{get}</span><i aria-hidden="true">↓</i></a>
<p class="meta">NAIMAN.exe · x64 · {megabytes} MB · {t("version", "wersja")} {"0.3 " + t("for review", "do oceny") if PREVIEW else RELEASE[1:]}{"" if PREVIEW else " · " + mainlink}</p>
<p id="usage-stats"><strong data-stat="views">…</strong> {t("page views since 20 Nov 2026", "odsłon od 20.11.2026")} · <strong data-stat="downloads">…</strong> {t("EXE downloads across all releases", "pobrań EXE ze wszystkich wydań")}</p>
<p>{t("New in 0.3.1: open the GTA 6 camera guide during play with Ctrl+Alt+F. With HDMI, the controller stays on the console.", "Nowość w 0.3.1: podczas gry Ctrl+Alt+F pokazuje ściągę kamery FPP. Przy HDMI pad zostaje przy konsoli.")}</p>
<p class="phone">{t("On your phone? The steps for your console are right below. Get the file later, on the PC.", "Czytasz na telefonie? Kroki dla konsoli masz zaraz niżej. Plik pobierzesz później, na komputerze.")}</p></div>
</div>
<div class="stagefront">{sun()}{shot("01-start", t("The first NAIMAN screen: PlayStation 5, Xbox or HDMI capture card", "Pierwszy ekran NAIMAN: PlayStation 5, Xbox albo karta HDMI"), True)}</div>
</section>
<nav class="picks" aria-label="{t("Choose your console", "Wybierz konsolę")}">
{pick("ps5", t("I have a PS5", "Mam PS5"), t("PS Remote Play or chiaki-ng", "PS Remote Play albo chiaki-ng"))}
{pick("xbox", t("I have an Xbox", "Mam Xboxa"), t("Remote play in Microsoft Edge", "Gra zdalna w Microsoft Edge"))}
{pick("hdmi", t("I have an HDMI card", "Mam kartę HDMI"), t("Elgato and other capture cards", "Elgato i inne karty przechwytujące"))}
</nav>
<section class="block" id="demo">
<div class="head"><h2>{t("See the difference, live", "Zobacz różnicę na żywo")}</h2>
<div class="tabs" role="group" aria-label="{t("Speed", "Tempo")}"><a role="button" tabindex="0" data-speed="0.5">{t("Slow", "Wolno")}</a><a role="button" tabindex="0" data-speed="1" aria-current="true">{t("Normal", "Normalnie")}</a><a role="button" tabindex="0" data-speed="1.8">{t("Fast", "Szybko")}</a></div></div>
<div class="live"><canvas id="live" tabindex="0" aria-label="{t("The same moving scene at 30 frames per second on the left and 60 on the right. Drag the divider.", "Ta sama ruchoma scena w 30 klatkach na sekundę po lewej i w 60 po prawej. Przeciągnij linię podziału.")}"></canvas>
<p class="tag l"><b>30</b>{t("frames a second, as on the console", "klatek na sekundę, jak na konsoli")}</p><p class="tag r"><b>60</b>{t("with NAIMAN 2x", "z NAIMAN 2x")}</p></div>
<p class="method">{t("Drag the line. Left of it the scene moves 30 times a second, right of it 60. This is a frame rate simulation drawn in your browser, the measurement on a real game is below.", "Przeciągnij linię. Po lewej scena przesuwa się 30 razy na sekundę, po prawej 60. To symulacja liczby klatek rysowana w twojej przeglądarce, pomiar na prawdziwej grze jest niżej.")} <button id="live-toggle" class="plain" type="button" data-pause="{t("Pause", "Zatrzymaj")}" data-play="{t("Play", "Odtwórz")}"></button></p>
</section>
<section class="block" id="kroki">
<div class="head"><h2>{t("Three rounds to the game on your screen", "Trzy rundy do gry na ekranie")}</h2>
<div class="tabs" role="group" aria-label="{t("Console", "Konsola")}"><a data-route="ps5" href="#ps5">PlayStation 5</a><a data-route="xbox" href="#xbox">Xbox</a><a data-route="hdmi" href="#hdmi">{t("HDMI card", "Karta HDMI")}</a></div></div>
{path("ps5", "PlayStation 5", ps5)}
{path("xbox", "Xbox", xbox)}
{path("hdmi", t("HDMI capture card", "Karta HDMI"), hdmi)}
</section>
<section class="block split" id="skrot">
<div class="say"><h2>{t("Game already open?", "Gra już otwarta?")}</h2>
<p class="lead">{t("If the game is already running in Edge or PS Remote Play, skip the walkthrough. NAIMAN lists the open windows with a live preview, so you can tell the right one at a glance.", "Jeśli gra działa już w Edge albo w PS Remote Play, pomiń instrukcję. NAIMAN pokazuje otwarte okna z podglądem na żywo, więc od razu widać właściwe.")}</p>
{clicks((t("Go straight to the game window", "Przejdź prosto do okna z grą"), t("on the first screen", "na pierwszym ekranie")), (t("Your game window", "Twoje okno z grą"), t("detected games are marked", "wykryte gry są oznaczone")), (t("Play with 2x smoothing", "Graj z wygładzaniem 2x"),))}</div>
{shot("07-skrot-kilka-okien", t("NAIMAN window list with a preview of the selected game", "Lista okien w NAIMAN z podglądem wybranej gry"))}
</section>
<section class="block split" id="gra">
<div class="say"><h2>{t("While you play", "W czasie gry")}</h2>
<p class="lead">{t("The picture comes first. NAIMAN stays in the background with two counters: frames from the console and frames it generated.", "Obraz ma pierwszeństwo. NAIMAN zostaje w tle z dwoma licznikami: klatki z konsoli i klatki, które sam wygenerował.")}</p>
<ul class="keys">
<li><span>{key("Ctrl", "Alt", "B")}</span>{t("switches between the original and 2x, so you can compare", "przełącza oryginał i 2x, żeby porównać")}</li>
<li><span>{key("Ctrl", "Alt", "Q")}</span>{t("stops and returns to NAIMAN", "kończy i wraca do NAIMAN")}</li>
<li><span>{key("Alt", "Tab")}</span>{t("controller stopped responding? Go back to the game window and the picture returns", "pad przestał reagować? Wróć do okna gry, obraz pojawi się sam")}</li>
</ul></div>
{shot("11-sesja-trwa", t("NAIMAN during a session: 30 source frames and 30 generated frames per second", "NAIMAN w czasie sesji: 30 klatek źródłowych i 30 wygenerowanych na sekundę"))}
</section>
<section class="block" id="pomiar">
<div class="head"><h2>{t("The scorecard", "Werdykt na punkty")}</h2></div>
<div class="card"><div class="side"><p class="who">{t("Original", "Oryginał")}</p><strong>{t("25.1", "25,1")}</strong><p>{t("picture changes per second", "zmiany obrazu na sekundę")}</p></div>
<div class="vs" aria-hidden="true">VS</div>
<div class="side on"><p class="who">NAIMAN 2x</p><strong>{t("46.7", "46,7")}</strong><p>{t("picture changes per second", "zmiany obrazu na sekundę")}</p></div></div>
<div class="measure">
<div class="plot">{ticks(pl)}<p>{t("One second of motion. Top: the original. Bottom: with NAIMAN, generated frames in orange.", "Jedna sekunda ruchu. U góry oryginał. Na dole z NAIMAN, klatki wygenerowane na pomarańczowo.")}</p></div>
</div>
<p class="method">{t("Method: the same 30-second GTA VI recording from Xbox Remote Play, replayed twice on an RTX 4090, once without and once with 2x. We counted visible picture changes during motion. Console frame rate and controller latency were not part of this measurement.", "Metoda: ten sam 30-sekundowy klip GTA VI z Xbox Remote Play odtworzony dwa razy na RTX 4090, raz bez 2x i raz z 2x. Liczyliśmy widoczne zmiany obrazu podczas ruchu. Klatkaż konsoli i opóźnienie pada nie były częścią tego pomiaru.")} {link("testing.html", t("Full method", "Pełna metoda"))}</p>
</section>
<section class="block" id="przewagi">
<div class="head"><h2>{t("Why NAIMAN and not the others", "Dlaczego NAIMAN, a nie inni")}</h2></div>
<ul class="edge">
<li><b>{t("0 zł", "0 zł")}</b><span>{t("Free. Lossless Scaling costs 27 zł, NitLink 23.99 zł.", "Za darmo. Lossless Scaling kosztuje 27 zł, NitLink 23,99 zł.")}</span></li>
<li><b>{t("No capture card", "Bez karty")}</b><span>{t("The picture comes over your home network from Remote Play. NitLink needs a capture card, the cheapest Elgato 4K S starts around 820 zł.", "Obraz idzie po sieci domowej z Remote Play. NitLink wymaga karty przechwytującej, najtańsze Elgato 4K S to ok. 820 zł.")}</span></li>
<li><b>{t("No RTX needed", "Bez RTX")}</b><span>{t("Any graphics card with Direct3D 11. Veyra's NVIDIA generator needs an RTX 40 or 50, NitLink recommends an RTX 3080.", "Wystarczy karta graficzna z Direct3D 11. Generator NVIDIA w Veyrze wymaga RTX 40 albo 50, NitLink zaleca RTX 3080.")}</span></li>
<li><b>{t("3 rounds", "3 rundy")}</b><span>{t("You pick PS5 or Xbox and the app tells you every click. Elsewhere you choose the source, mode and generator yourself.", "Wybierasz PS5 albo Xboxa, a aplikacja mówi, co kliknąć. U innych sam ustawiasz źródło, tryb i generator.")}</span></li>
<li><b>0,6 MB</b><span>{t("One file. No installer, no account, no telemetry.", "Jeden plik. Bez instalatora, bez konta, bez telemetrii.")}</span></li>
</ul>
<div class="versus"><table>
<thead><tr><th></th><th class="us">NAIMAN</th><th>Lossless Scaling</th><th>Veyra</th><th>NitLink</th></tr></thead>
<tbody>
<tr><th>{t("Price", "Cena")}</th><td class="us">0 zł</td><td>27 zł</td><td>0 zł</td><td>{t("23.99 zł", "23,99 zł")}</td></tr>
<tr><th>{t("Capture card", "Karta przechwytująca")}</th><td class="us">{t("not needed", "niepotrzebna")}</td><td>{t("not needed", "niepotrzebna")}</td><td>{t("card or window", "karta albo okno")}</td><td>{t("required", "wymagana")}</td></tr>
<tr><th>{t("Graphics card", "Karta graficzna")}</th><td class="us">Direct3D 11</td><td>{t("RTX 30 recommended", "zalecane RTX 30")}</td><td>{t("RTX 40/50 for the NVIDIA generator", "RTX 40/50 dla generatora NVIDIA")}</td><td>{t("RTX 3080 recommended", "zalecane RTX 3080")}</td></tr>
<tr><th>{t("PS5 and Xbox setup", "Ustawienie PS5 i Xboxa")}</th><td class="us">{t("guided, 3 screens", "prowadzi, 3 ekrany")}</td><td>{t("manual", "ręcznie")}</td><td>{t("manual", "ręcznie")}</td><td>{t("card preview", "podgląd z karty")}</td></tr>
<tr><th>{t("Install", "Instalacja")}</th><td class="us">{t("one 0.6 MB file", "jeden plik 0,6 MB")}</td><td>Steam</td><td>{t("Windows 11 app", "aplikacja Windows 11")}</td><td>Steam</td></tr>
</tbody></table></div>
<p class="method">{t("Prices and requirements from Steam and the projects' own pages, read on 19 November 2026. The others win on extras: NitLink shows HDR, Lossless Scaling goes above 2x. NAIMAN wins on the shortest way from console to screen.", "Ceny i wymagania ze Steama i stron projektów, odczyt 19 listopada 2026. Inni wygrywają dodatkami: NitLink pokazuje HDR, Lossless Scaling ma mnożniki powyżej 2x. NAIMAN wygrywa najkrótszą drogą od konsoli do ekranu.")}</p>
</section>
<section class="block facts">
<div><h2>{t("What you need", "Czego potrzebujesz")}</h2><ul>
<li>Windows 11, x64</li>
<li>{t("A graphics card with Direct3D 11", "Karta graficzna z Direct3D 11")}</li>
<li>{t("A monitor in SDR mode", "Monitor w trybie SDR")}</li>
<li>{t("Xbox or PS5 on the same network, or an HDMI capture card", "Xbox albo PS5 w tej samej sieci albo karta HDMI")}</li></ul></div>
<div><h2>{t("Good to know", "Warto wiedzieć")}</h2><ul>
<li>{t("Controller and sound stay with the app the picture comes from: Edge or PS Remote Play. With HDMI the controller stays on the console.", "Pad i dźwięk obsługuje aplikacja, z której płynie obraz: Edge albo PS Remote Play. Przy HDMI pad zostaje przy konsoli.")}</li>
<li>{t("A generated frame adds a small delay and can leave a trace on fast cuts. Ctrl + Alt + B shows the difference.", "Dorysowana klatka dodaje niewielkie opóźnienie i przy szybkich cięciach może zostawić ślad. Ctrl + Alt + B pokazuje różnicę.")}</li>
<li>{t("HDR output and protected video are not supported.", "Obraz HDR i treści chronione nie są obsługiwane.")}</li>
<li>{t("No telemetry. Frame counters stay on your PC.", "Bez telemetrii. Liczniki klatek zostają na twoim komputerze.")}</li></ul></div>
</section>
<section class="block last">
<h2>{t("One file. Run it and step into the ring.", "Jeden plik. Uruchom i wchodź do ringu.")}</h2>
<a class="cta" href="{download}"{" download" if PREVIEW else ""}><span>{get}</span><i aria-hidden="true">↓</i></a>
<p class="meta">{sums}</p>
</section>
</main>
<footer class="foot">{logo()}{mainlink}<a href="{release}">{t("Releases", "Wydania")}</a><a href="testing.html">{t("Measurements", "Pomiary")}</a><a href="credits.html">{t("Credits", "Licencje")}</a><a href="{REPO}">GitHub</a></footer>
</body>
</html>
'''
    (DOCS / ("pl.html" if pl else "index.html")).write_text(html, encoding="utf-8", newline="\n")


def paper(name, title, body):
    html = f'''<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<meta name="robots" content="noindex,{"nofollow" if PREVIEW else "follow"}">
<link rel="stylesheet" href="field.css">
<title>NAIMAN | {title}</title>
</head>
<body>
<header class="top"><a class="home" href="./">{logo()}</a><div class="lang"><a lang="pl" href="pl.html">PL</a><a lang="en" href="./">EN</a></div></header>
<main class="paper"><h1>{title}</h1>{body}</main>
</body>
</html>
'''
    (DOCS / name).write_text(html, encoding="utf-8", newline="\n")


TESTING = '''<h2>Original and 2x on one RTX 4090</h2><p>We replayed the same 30-second GTA VI recording from Xbox Remote Play twice on Windows. NAIMAN captured a 1280 × 720 player window. A second recorder sampled its 640 × 360 output up to 120 times per second.</p><p><strong>Original playback: 25.1 visible changes/s</strong> across 27 seconds of clear motion. <strong>NAIMAN 2x: 46.7</strong> across 25 seconds of clear motion. These are measurements from the first preview of the renderer.</p><p>We counted changes in the centre after downsampling to 256 × 144 grayscale. A change counted when the mean absolute brightness difference exceeded 0.45 on a 0-255 scale and over 3% of pixels changed by at least three levels. We averaged seconds with at least eight clear changes. Compression, slight motion and capture timing affect this estimate.</p><h2>What we verified</h2><p>The 2x engine ran on a 2560 × 1440 monitor. The built-in 30 Hz scene, both setup paths, original/2x switching and exit were checked on Windows. The Xbox window selector also recognized a local replay in Edge. A physical PS5, HDMI hardware and controller-to-screen latency await separate tests.</p><h2>Po polsku</h2><p>Na RTX 4090 dwukrotne odtworzenie tego samego klipu dało w ruchu średnio <strong>25,1 widocznej zmiany/s</strong> w trybie oryginalnym i <strong>46,7 z interpolacją</strong>. To wynik nagrania odtwarzanego na PC. Uczciwe porównanie z Veyrą, SmoovPlay i NitLinkiem wymaga wspólnej metody i pomiaru opóźnienia sterowania.</p><p><a href="pl.html">Wróć do instrukcji</a> · <a href="./">English guide</a></p>'''
CREDITS = '''<h2>Type</h2><p>Headlines are set in a cut of Archivo by the Archivo Project Authors. Text is set in a cut of IBM Plex Sans by IBM Corp. Both are used under the SIL Open Font License and renamed as the licence requires: <a href="media/Archivo-OFL.txt">Archivo licence</a>, <a href="media/IBMPlex-OFL.txt">IBM Plex licence</a>.</p><h2>Pictures</h2><p>Every picture of the app on these pages is drawn by NAIMAN itself with its screenshot mode. The window lists in them hold sample entries.</p><p><a href="pl.html">Polski poradnik</a> · <a href="./">English guide</a></p>'''

for language in (True, False):
    page(language)
paper("testing.html", "Playback measurements", TESTING)
paper("credits.html", "Credits", CREDITS)
print("pages written", "(preview)" if PREVIEW else "(public)")
