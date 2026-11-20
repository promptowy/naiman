param([string]$Exe = (Join-Path $PSScriptRoot '..\dist\fpp\NAIMAN.exe'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class CameraTestWindows {
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr window, int msg, IntPtr w, IntPtr l);
}
'@
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path $Exe).Path)
$type = $assembly.GetType('Launcher', $true)
$flags = [Reflection.BindingFlags]'Instance,Public,NonPublic'
$launcher = [Activator]::CreateInstance($type, [object[]]@($null,[single]1))
function Field($name) { $type.GetField($name,$flags).GetValue($launcher) }
function Call($name, [object[]]$values) { $type.GetMethod($name,$flags).Invoke($launcher,$values) }
function Assert-True($condition, $details) { if (!$condition) { throw $details } }
try {
    foreach ($polish in @($true,$false)) {
        Call 'Speak' @($polish)
        foreach ($route in @(0,1,2,3)) {
            Call 'ChooseRoute' @($route)
            $button = Field 'cameraHelp'
            Assert-True ((Field 'placed').Contains($button)) 'Missing FPP control'
            $before = [CameraTestWindows]::GetForegroundWindow()
            $button.GetType().GetMethod('OnClick',$flags).Invoke($button, @([EventArgs]::Empty))
            [Windows.Forms.Application]::DoEvents()
            $card = Field 'cameraGuide'
            Assert-True ($card.Visible -and $card.TopMost) 'Guide did not open above the game'
            Assert-True ($before -eq [CameraTestWindows]::GetForegroundWindow()) 'Guide took focus'
            $lines = ($card.Controls | ForEach-Object { $_.Text }) -join ' '
            Assert-True ($lines.Contains('2/2')) 'Missing initial camera setting'
            Assert-True (($lines -match 'Xbox:') -eq ($route -ne 1)) 'Wrong Xbox instructions'
            Assert-True (($lines -match 'PS5:') -eq ($route -ne 0)) 'Wrong PS5 instructions'
            Assert-True ($lines.Contains($(if ($polish) {'telefon'} else {'phone'}))) 'Missing phone step'
            foreach ($item in $card.Controls) {
                Assert-True ($card.ClientRectangle.Contains($item.Bounds)) 'Clipped guide text'
            }
            Call 'ToggleCameraGuide' @()
            Assert-True ($card.IsDisposed) 'Guide did not close'
        }
    }
    # Dispatch only to our test window. No input is sent to the user's foreground app.
    $itemType = $assembly.GetType('WindowList+Item', $true)
    $item = [Activator]::CreateInstance($itemType)
    $item.Handle = [CameraTestWindows]::GetForegroundWindow()
    $type.GetField('lastItem',$flags).SetValue($launcher,$item)
    $id = [int]$type.GetField('CameraKey', [Reflection.BindingFlags]'Static,NonPublic').GetRawConstantValue()
    $key = $type.GetMethod('RegisterHotKey',[Reflection.BindingFlags]'Static,NonPublic')
    $registered = $key.Invoke($null,@($launcher.Handle,$id,[uint32]0x4003,[uint32][Windows.Forms.Keys]::F))
    Assert-True $registered 'Ctrl+Alt+F unavailable on this test desktop'
    $type.GetField('cameraKeyRegistered',$flags).SetValue($launcher,$true)
    [void][CameraTestWindows]::SendMessage($launcher.Handle,0x312,[IntPtr]$id,[IntPtr]::Zero)
    Assert-True ((Field 'cameraGuide').Visible) 'Session hotkey did not open guide'
    [void][CameraTestWindows]::SendMessage($launcher.Handle,0x312,[IntPtr]$id,[IntPtr]::Zero)
    Assert-True ($null -eq (Field 'cameraGuide')) 'Session hotkey did not close guide'
    'PASS: Xbox, PS5, HDMI, open window, PL and EN, focus, layout, close and session hotkey.'
} finally {
    Call 'EndCameraHelp' @()
    $launcher.Dispose()
}
