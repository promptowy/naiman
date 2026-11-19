# NAIMAN

A portable Windows player for smoothing a console stream on your PC. One executable, separate Xbox and PS5 setup walkthroughs in English and Polish, and a built-in motion test. The source app keeps controller input and audio.

[Download the Windows preview](https://github.com/promptowy/naiman/releases/download/v0.2.0-preview.1/NAIMAN.exe) · [English guide](https://promptowy.github.io/naiman/) · [Polski poradnik](https://promptowy.github.io/naiman/pl.html) · [Test results](docs/testing.md)

## Use

1. Run NAIMAN.exe on Windows 11 x64 with an SDR monitor and a Direct3D 11 feature level 11.0 GPU.
2. Choose Xbox or PlayStation 5. Follow the instructions on the console, open the stream on your PC, and confirm the game window. If the stream is already running, use the shortcut on the first step. For an HDMI card, choose the OBS projector route. Console mode uses a centered 16:9 viewport from the client area. Press F11 in Edge to remove any visible browser chrome.
3. Use Ctrl + Alt + B to compare original and smoothed playback. Ctrl + Alt + Q exits playback. In the launcher you can also try the 30 Hz motion test without a console.

The preview uses Windows Graphics Capture and Direct3D 11. It detects repeated frames, estimates motion with a coarse-to-fine GPU block search, draws an intermediate frame, and fits the result to the source monitor. Protected content, HDR presentation, direct HDMI device access and native console pairing are outside this preview. Interpolation can add delay and create artifacts. Physical PS5 and capture card validation is pending.

Hardware photography in the application and guide is credited at [Photo credits](https://promptowy.github.io/naiman/credits.html). Regenerate the static landing pages with `python site/build.py`.

## Local files

The executable unpacks its embedded engine to the current user's local NAIMAN application directory. Settings remember the selected source type and language. Session logs contain timings and frame counts. Window titles and gameplay recordings are not saved to those logs. There is no telemetry. The Xbox and PlayStation buttons open the respective official sites.

## Build and test

Install the Visual Studio 2022 C++ x64 build tools, the Windows SDK 10.0.26100 (or later with C++/WinRT headers) and .NET Framework on Windows. Run:

```powershell
.\build.ps1
.\dist\NAIMAN.exe
```

For the interactive desktop smoke test, the launcher opens its own 30 Hz scene, selects it, starts capture, and closes after the test. The process exits with code 0 when it presents original and generated frames:

```powershell
$log = Join-Path $env:TEMP 'naiman-check.log'
$p = Start-Process .\dist\NAIMAN.exe -ArgumentList @('--check', ('"' + $log + '"')) -PassThru -Wait
$p.ExitCode
Get-Content $log
```

The smoke test requires a visible desktop and a hardware GPU. Build artifacts are generated locally with build.ps1. Download and run the official preview release or build from the source in this repository.
