# Przejście prawdziwego okna NAIMAN przez UI Automation: ścieżki, test bez konsoli, start, stop, utrata źródła.
# Użycie: powershell -File tests\przejscie.ps1 -Exe dist\next\NAIMAN.exe -Out ocena\okno
param([string]$Exe = (Join-Path $PSScriptRoot '..\dist\next\NAIMAN.exe'), [string]$Out = (Join-Path $PSScriptRoot '..\ocena\okno'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class Native { [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r); [DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; } }
'@
[void][Native]::SetProcessDPIAware()
$null = New-Item -ItemType Directory -Force $Out
$Exe = (Resolve-Path $Exe).Path
$app = Start-Process $Exe -PassThru
$results = New-Object System.Collections.Generic.List[string]
function Window { $app.Refresh(); [System.Windows.Automation.AutomationElement]::FromHandle($app.MainWindowHandle) }
function Buttons {
    (Window).FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition) |
        Where-Object { $_.Current.ClassName -like '*BUTTON*' -and -not $_.Current.IsOffscreen }
}
function Find([string]$name) { Buttons | Where-Object { $_.Current.Name -eq $name } | Select-Object -First 1 }
function Press([string]$name) {
    $b = Find $name
    if (!$b) { throw "Brak przycisku: $name. Widoczne: " + ((Buttons | ForEach-Object { $_.Current.Name }) -join ' | ') }
    [void][Native]::SendMessage([IntPtr]$b.Current.NativeWindowHandle, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 450
}
function Expect([string]$what, [bool]$ok) { $results.Add(($(if ($ok) { 'OK   ' } else { 'BLAD ' })) + $what) }
function Shot([string]$name) {
    $r = New-Object Native+RECT
    [void][Native]::GetWindowRect($app.MainWindowHandle, [ref]$r)
    $bmp = New-Object System.Drawing.Bitmap ($r.R - $r.L), ($r.B - $r.T)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($r.L, $r.T, 0, 0, $bmp.Size)
    $bmp.Save((Join-Path $Out ($name + '.png')))
    $g.Dispose(); $bmp.Dispose()
}
try {
    for ($i = 0; $i -lt 40 -and $app.MainWindowHandle -eq 0; $i++) { Start-Sleep -Milliseconds 250; $app.Refresh() }
    Start-Sleep -Milliseconds 600
    if (Find 'PL') { Press 'PL' }
    Expect 'start: Xbox, PlayStation 5, Karta HDMI, skrót i test' ([bool](Find 'Xbox') -and [bool](Find 'PlayStation 5') -and [bool](Find 'Karta HDMI') -and [bool](Find 'Przejdź prosto do okna z grą') -and [bool](Find 'Nie masz konsoli pod ręką? Włącz test ruchu'))
    Shot '01-start'
    Press 'EN'
    Expect 'EN: przełącznik języka zmienia etykiety' ([bool](Find 'HDMI capture card') -and [bool](Find 'Go straight to the game window'))
    Press 'Xbox'
    Expect 'EN Xbox krok 1' ([bool](Find 'Done, next round') -and [bool](Find 'Back'))
    Press 'Done, next round'
    Expect 'EN Xbox krok 2' ([bool](Find 'Open Xbox Remote Play') -and [bool](Find 'I can see the game on this PC'))
    Press 'Back'; Press 'Back'
    Press 'PlayStation 5'
    Press 'Done, next round'
    Expect 'EN PS5 krok 2' ([bool](Find 'Open PS Remote Play'))
    Press 'Back'; Press 'Back'
    Press 'PL'
    Press 'Xbox'
    Expect 'PL Xbox krok 1' ([bool](Find 'Włączone, następna runda') -and [bool](Find 'Wstecz') -and [bool](Find 'Mam już otwartą grę'))
    Shot '02-xbox-krok1'
    Press 'Włączone, następna runda'
    Expect 'PL Xbox krok 2' ([bool](Find 'Otwórz Xbox Remote Play') -and [bool](Find 'Widzę grę na komputerze'))
    Press 'Widzę grę na komputerze'
    Expect 'PL Xbox krok 3: wybór okna, Odśwież i Pokaż wszystkie okna' ([bool](Find 'Odśwież') -and [bool](Find 'Pokaż wszystkie okna') -and [bool](Find 'Graj z wygładzaniem 2x'))
    $none = -not (Find 'Graj z wygładzaniem 2x').Current.IsEnabled
    Expect ('brak źródła: przycisk gry ' + $(if ($none) { 'wyłączony' } else { 'włączony, bo na tym komputerze jest otwarte okno Xbox' })) $true
    Shot '03-xbox-okno'
    Press 'Odśwież'
    Press 'Wstecz'; Press 'Wstecz'; Press 'Wstecz'
    Press 'PlayStation 5'
    Expect 'PL PS5 krok 1' ([bool](Find 'Włączone, następna runda'))
    Press 'Włączone, następna runda'
    Expect 'PL PS5 krok 2' ([bool](Find 'Otwórz PS Remote Play'))
    Shot '04-ps5-krok2'
    Press 'Mam już otwartą grę'
    Expect 'PL PS5 skrót do okna' ([bool](Find 'Graj z wygładzaniem 2x'))
    Press 'Wstecz'; Press 'Wstecz'; Press 'Wstecz'
    Press 'Karta HDMI'
    Expect 'HDMI: wybór karty i droga przez OBS' ([bool](Find 'Użyj projektora OBS'))
    Press 'Użyj projektora OBS'
    Expect 'HDMI: powrót do karty bezpośrednio' ([bool](Find 'Użyj karty bezpośrednio'))
    Shot '05-hdmi-obs'
    Press 'Wstecz'
    Press 'Przejdź prosto do okna z grą'
    Expect 'skrót: lista okien' ([bool](Find 'Graj z wygładzaniem 2x'))
    Shot '06-skrot'
    Press 'Wstecz'
    Press 'Nie masz konsoli pod ręką? Włącz test ruchu'
    $ready = $false
    for ($i = 0; $i -lt 20 -and -not $ready; $i++) { Start-Sleep -Milliseconds 400; $ready = (Find 'Graj z wygładzaniem 2x').Current.IsEnabled }
    Expect 'test bez konsoli: plansza wykryta sama' $ready
    Start-Sleep -Milliseconds 800
    Shot '07-test-wykryty'
    Press 'Graj z wygładzaniem 2x'
    Start-Sleep -Seconds 4
    Expect 'start: sesja trwa (Zatrzymaj, Wróć do gry)' ([bool](Find 'Zatrzymaj') -and [bool](Find 'Wróć do gry'))
    $engine = Get-Process NaimanCapture -ErrorAction SilentlyContinue
    Expect 'start: silnik działa na pełnym ekranie' ([bool]$engine)
    Press 'Zatrzymaj'
    $done = $false
    for ($i = 0; $i -lt 20 -and -not $done; $i++) { Start-Sleep -Milliseconds 400; $done = [bool](Find 'Graj ponownie') }
    Expect 'stop: sesja zakończona, Graj ponownie aktywne' ($done -and (Find 'Graj ponownie').Current.IsEnabled)
    Start-Sleep -Milliseconds 500
    Shot '08-koniec'
    Press 'Graj ponownie'
    Start-Sleep -Seconds 3
    Get-CimInstance Win32_Process -Filter "Name='NAIMAN.exe'" | Where-Object { $_.CommandLine -like '*--demo*' -and $_.ParentProcessId -eq $app.Id } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
    $lost = $false
    for ($i = 0; $i -lt 25 -and -not $lost; $i++) { Start-Sleep -Milliseconds 400; $b = Find 'Graj ponownie'; $lost = $b -and -not $b.Current.IsEnabled }
    Expect 'utrata źródła: sesja kończy się sama, Graj ponownie wyłączone' $lost
    Start-Sleep -Milliseconds 500
    Shot '09-brak-zrodla'
    Press 'Wybierz inne źródło'
    Expect 'po utracie: powrót do wyboru źródła z pustym stanem' ([bool](Find 'Odśwież'))
    Press 'Wstecz'
    Expect 'powrót na start' ([bool](Find 'Karta HDMI'))
} catch {
    $results.Add('BLAD ' + $_.Exception.Message)
}
$results | Set-Content -Encoding UTF8 (Join-Path $Out 'wynik.txt')
$results
