param([string]$OutDir = (Join-Path $PSScriptRoot 'dist'))
$ErrorActionPreference='Stop'
$src = Join-Path $PSScriptRoot 'src'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vs = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (!$vs) {throw 'Install Visual Studio 2022 C++ Build Tools and Windows SDK 10.0.26100 or later.'}
$dev = Join-Path $vs 'Common7\Tools\VsDevCmd.bat'
$null = New-Item -ItemType Directory -Force $OutDir,(Join-Path $OutDir 'engine')
$OutDir = (Resolve-Path $OutDir).Path
$work = Join-Path ([IO.Path]::GetTempPath()) ('naiman-build-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory $work
$batch = Join-Path $work 'build.cmd'
@"
@echo off
call "$dev" -arch=x64 -host_arch=x64 >nul
if errorlevel 1 exit /b 1
cl /nologo /std:c++20 /EHsc /O2 /MT /utf-8 /DUNICODE /D_UNICODE "$src\session.cpp" /Fe:"$OutDir\engine\NaimanCapture.exe" /link /SUBSYSTEM:WINDOWS d3d11.lib dxgi.lib d3dcompiler.lib windowsapp.lib user32.lib shell32.lib winmm.lib
"@ | Set-Content -Encoding ascii $batch
Push-Location $work
try {
    & $env:ComSpec /d /c $batch
    if ($LASTEXITCODE) {throw 'Capture engine build failed.'}
} finally {Pop-Location}
$cs = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path "$src\Launcher.cs")) {throw 'Missing launcher source.'}
& $cs /nologo /optimize+ /target:winexe /platform:x64 /codepage:65001 "/win32icon:$src\naiman.ico" "/out:$OutDir\NAIMAN.exe" "/resource:$OutDir\engine\NaimanCapture.exe,NaimanCapture.bin" "/resource:$src\fonts\naiman-display.ttf,naiman-display.ttf" "/resource:$src\fonts\naiman-sans.ttf,naiman-sans.ttf" "/resource:$src\fonts\naiman-sans-bold.ttf,naiman-sans-bold.ttf" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll "/win32manifest:$src\app.manifest" "$src\Launcher.cs" "$src\Wizard.cs" "$src\Ui.cs" "$src\Shots.cs" "$src\Pad.cs" "$src\CaptureDevices.cs"
if ($LASTEXITCODE) {throw 'Launcher build failed.'}
