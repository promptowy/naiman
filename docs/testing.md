---
layout: default
title: Measurements
---

[English guide](./) · [Polski poradnik](./pl.html)

## What the Windows preview did on one RTX 4090

We replayed the same 30-second recording of GTA VI from Xbox Remote Play twice on a Windows PC. NAIMAN captured the player window at 1280 × 720. A second recorder sampled NAIMAN's 640 × 360 output window up to 120 times a second. The first run used original playback and the second enabled interpolation. The two real-time runs started separately.

| Playback | Visible changes per second in moving scenes | Seconds with clear motion |
| --- | ---: | ---: |
| Original | 25.1 | 27 of 30 |
| NAIMAN 2x | 46.7 | 25 of 30 |

We counted changes in the centre of the picture after downsampling to a 256 × 144 grayscale image. A change counted if the mean absolute brightness difference exceeded 0.45 on the 0-255 scale and over 3% of pixels changed by at least three levels. The moving-scene average excludes seconds without eight clear changes. Compression, slight motion and the starting phase of capture affect this estimate.

## Other checks

- In a nine-second run of the built-in 30 Hz scene, the release build submitted 270 source frames and 269 intermediate frames.
- On a 2560 × 1440 desktop, the launcher opened a fullscreen output, switched interpolation off and on again, and returned to the launcher. The session log confirmed that generated-frame counts stopped increasing in original mode.
- Xbox testing used a previously recorded stream. A fresh live NAIMAN Xbox session, physical PS5, HDMI capture hardware, HDR and controller-to-screen latency still need testing. Counters and these clips measure picture changes on the PC. They do not measure native console FPS or input latency.
- Our earlier live Veyra trial reached 51.3 visible changes/s in a 30-second sample and 47.1 across a longer sample. That trial used a different scene and capture path. A ranking against Veyra, NitLink and SmoovPlay would need matched inputs, output measurements and latency tests.

## Wynik po polsku

Na RTX 4090 ten sam klip dał w ruchu średnio **25,1 zmiany obrazu/s** w trybie oryginalnym i **46,7** z NAIMAN 2x. Wygładzanie działało też na pełnym ekranie. W tej wersji wynik dla GTA pochodzi z odtworzonego nagrania. Pomiar opóźnienia pada i bezpośredni test trzech konkurentów na tym samym stanowisku pozostają otwarte.
