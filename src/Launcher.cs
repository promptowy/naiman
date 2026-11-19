using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (args.Contains("--demo"))
        {
            Application.Run(new Demo());
            return 0;
        }
        string check = args.Length == 2 && args[0] == "--check" ? Path.GetFullPath(args[1]) : null;
        using (var app = new Launcher(check))
        {
            Application.Run(app);
            return app.Result;
        }
    }
}
static class WindowList
{
    delegate bool Visitor(IntPtr h, IntPtr data);
    [DllImport("user32.dll")]
    static extern bool EnumWindows(Visitor v, IntPtr data);
    [DllImport("user32.dll")]
    static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")]
    static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetWindowText(IntPtr h, StringBuilder text, int size);
    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr h, out int pid);
    [DllImport("dwmapi.dll")]
    static extern int DwmGetWindowAttribute(IntPtr h, int attribute, out int value, int size);
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr h);
    public sealed class Item
    {
        public IntPtr Handle;
        public int Pid;
        public string Name, ProcessName;
        public override string ToString()
        {
            return Name;
        }
    }
    public static List<Item> Scan(int mode, int demoPid)
    {
        var items = new List<Item>();
        EnumWindows(delegate(IntPtr h, IntPtr ignored) {
            if (!IsWindowVisible(h) || IsIconic(h))
                return true;
            int cloaked;
            if (DwmGetWindowAttribute(h, 14, out cloaked, 4) == 0 && cloaked != 0)
                return true;
            var t = new StringBuilder(512);
            if (GetWindowText(h, t, t.Capacity) == 0)
                return true;
            int pid;
            GetWindowThreadProcessId(h, out pid);
            if (pid == Process.GetCurrentProcess().Id)
                return true;
            try
            {
                using (var p = Process.GetProcessById(pid))
                {
                    string proc = p.ProcessName.ToLowerInvariant(), title = t.ToString(),
                           key = title.ToLowerInvariant();
                    bool browser = new[] { "msedge", "chrome", "firefox", "brave", "vivaldi" }.Contains(proc);
                    bool xbox = browser && (key.Contains("play xbox") || key.Contains("xbox remote play") ||
                                            key.Contains("xbox cloud gaming") || key.Contains("xbox.com/play") ||
                                            key.StartsWith("xbox "));
                    bool ps = (proc == "remoteplay" || proc.StartsWith("chiaki") || proc == "psremoteplay");
                    bool cast = proc.StartsWith("obs") && (key.Contains("projector") || key.Contains("projektor") ||
                                                           key.Contains("projecteur") || key.Contains("proyector"));
                    bool match = mode == 0   ? xbox
                                 : mode == 1 ? ps
                                 : mode == 2 ? cast
                                 : mode == 4 ? pid == demoPid
                                             : !new[] { "explorer", "searchhost", "shellexperiencehost",
                                                        "applicationframehost", "textinputhost" }
                                                    .Contains(proc);
                    if (match)
                        items.Add(new Item { Handle = h, Pid = pid, Name = title, ProcessName = proc });
                }
            }
            catch (ArgumentException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
            return true;
        }, IntPtr.Zero);
        return items;
    }
}
class Launcher : Form
{
    readonly bool checking;
    readonly string checkPath;
    bool pl = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pl", sync, stopSent;
    readonly Color bg = Color.FromArgb(17, 19, 23), panel = Color.FromArgb(32, 35, 40),
                   white = Color.FromArgb(243, 242, 235), muted = Color.FromArgb(185, 190, 194),
                   accent = Color.FromArgb(232, 228, 92);
    Label brand, heading, inputLabel, windowLabel, hint, status, keys;
    ComboBox mode, window, language;
    Button start, refresh, open;
    readonly Timer timer = new Timer();
    Process engine, demo;
    string statsPath, stopPath;
    int demoPid;
    DateTime scanAt;
    string lastLine;
    public int Result;
    readonly DateTime launched = DateTime.UtcNow;
    DateTime preserveStatusUntil;
    static readonly string Prefs = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NAIMAN", "preferences.txt");
    void SavePrefs()
    {
        if (checking || sync)
            return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Prefs));
            File.WriteAllText(Prefs, (pl ? "pl" : "en") + "\n" + mode.SelectedIndex);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
    string T(string en, string pol)
    {
        return pl ? pol : en;
    }
    public Launcher(string report)
    {
        checkPath = report;
        checking = report != null;
        Text = "NAIMAN";
        BackColor = bg;
        ForeColor = white;
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(664, 508);
        MinimumSize = new Size(620, 535);
        StartPosition = FormStartPosition.CenterScreen;
        brand = LabelAt(30, 21, 300, 31, 18, true);
        brand.Text = "NAIMAN";
        brand.ForeColor = accent;
        language = ComboAt(548, 23, 85);
        language.Items.AddRange(new object[] { "English", "Polski" });
        language.SelectedIndex = pl ? 1 : 0;
        heading = LabelAt(28, 72, 610, 55, 27, true);
        inputLabel = LabelAt(32, 149, 340, 23, 10, true);
        mode = ComboAt(32, 177, 400);
        open = ButtonAt(443, 176, 189, 33, false);
        windowLabel = LabelAt(32, 224, 320, 22, 10, true);
        window = ComboAt(32, 252, 490);
        refresh = ButtonAt(530, 251, 102, 33, false);
        hint = LabelAt(32, 292, 598, 49, 10, false);
        hint.ForeColor = muted;
        start = ButtonAt(32, 352, 600, 54, true);
        start.Font = new Font("Segoe UI", 14, FontStyle.Bold);
        status = LabelAt(32, 418, 600, 35, 10, false);
        keys = LabelAt(32, 460, 605, 36, 9, false);
        keys.ForeColor = muted;
        language.SelectedIndexChanged += delegate
        {
            pl = language.SelectedIndex == 1;
            Localize();
        };
        mode.SelectedIndexChanged += delegate
        {
            if (!sync)
            {
                Scan();
                SetText();
            }
        };
        open.Click += delegate
        {
            try
            {
                OpenSource();
            }
            catch (Exception e)
            {
                status.Text = e.Message;
            }
        };
        refresh.Click += delegate
        {
            Scan();
        };
        start.Click += delegate
        {
            try
            {
                if (engine != null)
                    Stop();
                else
                    Begin();
            }
            catch (Exception e)
            {
                Fail(e.Message);
            }
        };
        Shown += delegate
        {
            if (checking)
            {
                mode.SelectedIndex = 4;
                OpenSource();
            }
            else
                Scan();
        };
        FormClosing += delegate
        {
            Stop();
            if (demo != null && !demo.HasExited)
                demo.CloseMainWindow();
        };
        timer.Interval = 350;
        timer.Tick += delegate
        {
            try
            {
                Poll();
            }
            catch (Exception e)
            {
                Fail(e.Message);
            }
        };
        timer.Start();
        Localize();
        if (!checking && File.Exists(Prefs))
        {
            try
            {
                string[] pref = File.ReadAllLines(Prefs);
                int selected;
                if (pref.Length == 2 && int.TryParse(pref[1], out selected) && selected >= 0 &&
                    selected < mode.Items.Count)
                {
                    language.SelectedIndex = pref[0] == "pl" ? 1 : 0;
                    mode.SelectedIndex = selected;
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
        mode.SelectedIndexChanged += delegate
        {
            SavePrefs();
        };
        language.SelectedIndexChanged += delegate
        {
            SavePrefs();
        };
    }
    Label LabelAt(int x, int y, int w, int h, float size, bool bold)
    {
        var c = new Label { Location = new Point(x, y), Size = new Size(w, h),
                            Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
                            ForeColor = white };
        Controls.Add(c);
        return c;
    }
    ComboBox ComboAt(int x, int y, int w)
    {
        var c =
            new ComboBox { Location = new Point(x, y), Width = w,           DropDownStyle = ComboBoxStyle.DropDownList,
                           FlatStyle = FlatStyle.Flat, BackColor = panel,   ForeColor = white,
                           IntegralHeight = false,     DropDownHeight = 240 };
        Controls.Add(c);
        return c;
    }
    Button ButtonAt(int x, int y, int w, int h, bool primary)
    {
        var c = new Button { Location = new Point(x, y),       Size = new Size(w, h),
                             FlatStyle = FlatStyle.Flat,       BackColor = primary ? accent : panel,
                             ForeColor = primary ? bg : white, Cursor = Cursors.Hand };
        c.FlatAppearance.BorderSize = primary ? 0 : 1;
        c.FlatAppearance.BorderColor = Color.FromArgb(77, 79, 84);
        Controls.Add(c);
        return c;
    }
    void Localize()
    {
        int selected = Math.Max(0, mode.SelectedIndex);
        sync = true;
        mode.Items.Clear();
        mode.Items.AddRange(new object[] { "Xbox Remote Play", "PS Remote Play / chiaki-ng",
                                           T("Capture card via OBS projector", "Karta HDMI przez projektor OBS"),
                                           T("Choose another window", "Wybierz inne okno"),
                                           T("Built-in motion test", "Wbudowany test ruchu") });
        mode.SelectedIndex = selected;
        sync = false;
        heading.Text = T("Play your console on PC", "Graj na konsoli przez PC");
        inputLabel.Text = T("Source", "Źródło");
        windowLabel.Text = T("Game window", "Okno gry");
        refresh.Text = T("Refresh", "Odśwież");
        keys.Text = T("Ctrl + Alt + B   Compare original / 2x     ·     Ctrl + Alt + Q   Exit playback",
                      "Ctrl + Alt + B   Porównaj oryginał / 2x     ·     Ctrl + Alt + Q   Zakończ grę w NAIMAN");
        SetText();
        if (engine == null)
            Scan();
    }
    void SetText()
    {
        int m = mode.SelectedIndex;
        open.Enabled = engine == null && (m == 0 || m == 1 || m == 4);
        open.Text = m == 0   ? T("Open Xbox", "Otwórz Xbox")
                    : m == 1 ? T("PlayStation guide", "Pomoc PlayStation")
                    : m == 4 ? T("Open test", "Otwórz test")
                             : T("Use an open window", "Wybierz otwarte okno");
        hint.Text =
            m == 0   ? T("Start your stream in a browser. Keep the game tab open and the window restored.",
                         "Uruchom grę w przeglądarce. Zostaw aktywną kartę gry i przywrócone okno.")
            : m == 1 ? T("Start a stream in PS Remote Play or chiaki-ng. Your controller and audio stay with that app.",
                         "Uruchom transmisję w PS Remote Play lub chiaki-ng. Pad i dźwięk obsługuje ta aplikacja.")
            : m == 2
                ? T("In OBS, open a Windowed Projector for the capture card. Enable audio monitoring in OBS if needed.",
                    "W OBS otwórz projektor okienkowy źródła karty HDMI. Odsłuch dźwięku ustaw w OBS.")
            : m == 4 ? T("Try 2x motion on a moving 30 Hz test scene without connecting a console.",
                         "Sprawdź wygładzanie na ruchomej planszy 30 Hz bez podłączania konsoli.")
                     : T("Select the window that actually shows your game. Fullscreen borderless works best.",
                         "Wybierz okno, w którym widzisz grę. Najlepiej użyć pełnego ekranu bez ramek.");
        start.Text =
            engine != null ? T("Stop playback", "Zatrzymaj") : T("Play with 2x smoothing", "Graj z wygładzaniem 2x");
    }
    void Scan()
    {
        if (engine != null)
            return;
        var prev = window.SelectedItem as WindowList.Item;
        var items = WindowList.Scan(mode.SelectedIndex, demoPid);
        window.BeginUpdate();
        window.Items.Clear();
        window.Items.AddRange(items.ToArray());
        var kept = prev == null ? null : items.FirstOrDefault(v => v.Handle == prev.Handle && v.Pid == prev.Pid);
        if (kept != null)
            window.SelectedItem = kept;
        else if (items.Count == 1)
            window.SelectedIndex = 0;
        window.EndUpdate();
        start.Enabled = items.Count > 0;
        if (DateTime.UtcNow >= preserveStatusUntil)
            status.Text = items.Count == 0
                              ? T("Waiting for a visible source window.", "Czekam na widoczne okno źródłowe.")
                          : items.Count == 1 ? T("Window found. Ready to play.", "Okno znalezione. Możesz grać.")
                                             : T("Select the game window above.", "Wybierz okno gry z listy.");
        scanAt = DateTime.UtcNow;
    }
    void OpenSource()
    {
        if (mode.SelectedIndex == 4)
        {
            if (demo == null || demo.HasExited)
            {
                demo = Process.Start(Application.ExecutablePath, "--demo");
                demoPid = demo.Id;
            }
            return;
        }
        if (mode.SelectedIndex == 0)
        {
            try
            {
                Process.Start(
                    new ProcessStartInfo("microsoft-edge:https://www.xbox.com/remoteplay") { UseShellExecute = true });
                return;
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
        }
        string url =
            mode.SelectedIndex == 0 ? "https://www.xbox.com/remoteplay" : "https://www.playstation.com/remote-play/";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
    static string ExtractEngine()
    {
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("NaimanCapture.bin"))
        {
            if (stream == null)
                throw new IOException("Capture engine is missing.");
            string hash;
            using (var sha = SHA256.Create())
            {
                hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            }
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                      "NAIMAN", "engine", hash);
            Directory.CreateDirectory(dir);
            string exe = Path.Combine(dir, "NaimanCapture.exe");
            if (File.Exists(exe))
            {
                using (var existing = File.OpenRead(exe)) using (var sha = SHA256.Create())
                {
                    if (BitConverter.ToString(sha.ComputeHash(existing)).Replace("-", "") == hash)
                        return exe;
                }
                throw new IOException("Engine integrity check failed.");
            }
            string tmp = Path.Combine(dir, Guid.NewGuid() + ".tmp");
            stream.Position = 0;
            try
            {
                using (var dst = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    stream.CopyTo(dst);
                try
                {
                    File.Move(tmp, exe);
                }
                catch (IOException)
                {
                    if (!File.Exists(exe))
                        throw;
                    return ExtractEngine();
                }
            }
            finally
            {
                if (File.Exists(tmp))
                    File.Delete(tmp);
            }
            return exe;
        }
    }
    void Begin()
    {
        var item = window.SelectedItem as WindowList.Item;
        if (item == null)
        {
            status.Text = T("Select a game window.", "Wybierz okno gry.");
            return;
        }
        if (!WindowList.Scan(mode.SelectedIndex, demoPid).Any(i => i.Handle == item.Handle && i.Pid == item.Pid))
        {
            Scan();
            return;
        }
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NAIMAN",
                                  "sessions");
        Directory.CreateDirectory(dir);
        statsPath = checking ? checkPath
                             : Path.Combine(dir, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" +
                                                     Guid.NewGuid().ToString("N").Substring(0, 6) + ".log");
        stopPath = statsPath + "." + Guid.NewGuid().ToString("N") + ".stop";
        lastLine = null;
        stopSent = false;
        string exe = ExtractEngine();
        WindowList.SetForegroundWindow(item.Handle);
        string args = "--window " + item.Handle.ToInt64() + " --pid " + item.Pid + " --parent " +
                      Process.GetCurrentProcess().Id + " --log \"" + statsPath + "\" --stop \"" + stopPath + "\"" +
                      (checking ? " --seconds 9 --windowed" : "");
        engine = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true });
        mode.Enabled = window.Enabled = refresh.Enabled = false;
        SetText();
        status.Text = T("Starting capture...", "Uruchamiam przechwytywanie...");
    }
    void Stop()
    {
        if (engine != null && !engine.HasExited && !stopSent)
        {
            File.WriteAllText(stopPath, "");
            stopSent = true;
        }
    }
    void Fail(string reason)
    {
        status.Text = reason;
        preserveStatusUntil = DateTime.UtcNow.AddSeconds(15);
        if (checking)
        {
            Result = 1;
            Close();
        }
    }
    void Poll()
    {
        if (checking && DateTime.UtcNow - launched > TimeSpan.FromSeconds(35))
        {
            Fail("Test timed out.");
            return;
        }
        if (engine == null)
        {
            if (DateTime.UtcNow - scanAt > TimeSpan.FromSeconds(2) && !window.DroppedDown && !mode.DroppedDown)
                Scan();
            if (checking && window.SelectedItem != null)
                Begin();
            return;
        }
        string[] rows = new string[0];
        if (File.Exists(statsPath))
        {
            using (var f = new FileStream(statsPath, FileMode.Open, FileAccess.Read,
                                          FileShare.ReadWrite | FileShare.Delete))
            {
                if (f.Length > 8192)
                    f.Seek(-8192, SeekOrigin.End);
                using (var r = new StreamReader(f))
                {
                    rows = r.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                }
            }
        }
        if (engine.HasExited)
        {
            int code = engine.ExitCode;
            engine.Dispose();
            engine = null;
            if (File.Exists(stopPath))
                File.Delete(stopPath);
            mode.Enabled = window.Enabled = refresh.Enabled = true;
            SetText();
            Scan();
            string error = rows.LastOrDefault(s => s.StartsWith("error "));
            if (error != null || code != 0)
            {
                status.Text = T("Capture stopped: ", "Przechwytywanie zatrzymane: ") +
                              (error == null ? code.ToString() : error.Substring(6));
            }
            else
                status.Text = T("Playback finished.", "Sesja zakończona.");
            preserveStatusUntil = DateTime.UtcNow.AddSeconds(15);
            if (checking)
            {
                var end = rows.LastOrDefault(s => s.StartsWith("end "));
                long real = 0, generated = 0;
                var fields = end == null ? new string[0] : end.Split(' ');
                bool motion = fields.Length >= 4 && long.TryParse(fields[2], out real) &&
                              long.TryParse(fields[3], out generated) && real >= 30 && generated >= 15;
                Result = code == 0 && motion ? 0 : 1;
                Close();
            }
            return;
        }
        var line = rows.LastOrDefault(s => s.StartsWith("sample "));
        if (line == null || line == lastLine)
            return;
        var curr = line.Split(' ');
        var old = lastLine == null ? new[] { "sample", "0", "0", "0", "0", "0", "0", "1" } : lastLine.Split(' ');
        var inv = CultureInfo.InvariantCulture;
        double dt = double.Parse(curr[1], inv) - double.Parse(old[1], inv);
        if (dt > 0)
        {
            double a = (long.Parse(curr[3]) - long.Parse(old[3])) / dt,
                   g = (long.Parse(curr[4]) - long.Parse(old[4])) / dt;
            status.Text = String.Format(T("Playback: {0:0} source + {1:0} generated frames/s",
                                          "Odtwarzanie: {0:0} źródłowych + {1:0} generowanych kl./s"),
                                        a, g);
        }
        lastLine = line;
    }
}
class Demo : Form
{
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly Timer ticker = new Timer();
    public Demo()
    {
        Text = "NAIMAN Motion Test";
        ClientSize = new Size(960, 540);
        BackColor = Color.Black;
        DoubleBuffered = true;
        StartPosition = FormStartPosition.CenterScreen;
        ticker.Interval = 8;
        long before = -1;
        ticker.Tick += delegate
        {
            long frame = clock.ElapsedMilliseconds * 30 / 1000;
            if (frame != before)
            {
                before = frame;
                Invalidate();
            }
        };
        ticker.Start();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        int w = ClientSize.Width, h = ClientSize.Height;
        long frame = clock.ElapsedMilliseconds * 30 / 1000;
        float shift = (frame * 8) % 160;
        e.Graphics.Clear(Color.FromArgb(20, 25, 32));
        using (var lane = new Pen(Color.FromArgb(67, 80, 92), 2)) using (
            var marker = new SolidBrush(Color.FromArgb(229, 225, 97)))
        {
            for (float x = -160; x < w + 160; x += 160)
            {
                e.Graphics.DrawLine(lane, x - shift, 0, x - shift, h);
                e.Graphics.FillRectangle(marker, x - shift + 20, h * .5f, 65, 18);
            }
        }
        using (var brush = new SolidBrush(Color.White)) using (var f = new Font("Segoe UI", 16, FontStyle.Bold))
        {
            e.Graphics.DrawString("NAIMAN    /    30 Hz", f, brush, 24, 20);
        }
    }
}
