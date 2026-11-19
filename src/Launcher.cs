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
        if (args.Length >= 2 && args[0] == "--shots")
        {
            float scale = args.Length >= 3 ? float.Parse(args[2], CultureInfo.InvariantCulture) : 1f;
            Launcher.Shots(Path.GetFullPath(args[1]), scale);
            return 0;
        }
        string check = args.Length == 2 && args[0] == "--check" ? Path.GetFullPath(args[1]) : null;
        using (var app = new Launcher(check, 0f))
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
    [DllImport("user32.dll")]
    static extern bool IsWindow(IntPtr h);
    public static bool Alive(Item item)
    {
        int pid;
        if (item == null || !IsWindow(item.Handle) || !IsWindowVisible(item.Handle) || IsIconic(item.Handle))
            return false;
        GetWindowThreadProcessId(item.Handle, out pid);
        return pid == item.Pid;
    }
    public sealed class Item
    {
        public IntPtr Handle;
        public int Pid, Kind = -1;
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
                           key = System.Text.RegularExpressions.Regex
                                     .Replace(title.ToLowerInvariant().Replace("\u200B", ""), @"\s+", " ")
                                     .Trim();
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
                        items.Add(new Item { Handle = h, Pid = pid, Name = title, ProcessName = proc,
                                             Kind = pid == demoPid ? 4 : xbox ? 0 : ps ? 1 : cast ? 2 : -1 });
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
        // Recognised streams first, so an open game is the first thing in the list.
        return items.OrderBy(i => i.Kind < 0 ? 1 : 0).ToList();
    }
}
partial class Launcher : Form
{
    public const string Version = "0.3";
    const int BaseW = 1040, BaseH = 640;
    readonly bool checking;
    readonly string checkPath;
    bool pl = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pl", stopSent, staged, built;
    readonly Timer timer = new Timer();
    Process engine, demo;
    string statsPath, stopPath, lastLine, failure = "", notice = "";
    int demoPid;
    DateTime scanAt, sessionStart, noticeUntil;
    public int Result;
    readonly DateTime launched = DateTime.UtcNow;
    // Where the person is: route 0 Xbox, 1 PS5, 2 HDMI, 3 a game that is already open, 4 motion test.
    // step 0 start, 1 on the console, 2 on this PC, 3 pick the source. mode is the window filter.
    // phase 0 no session, 1 starting, 2 playing, 3 game lost focus, 4 finished, 5 source lost, 6 failed, 7 source back.
    int route, step, mode, phase;
    bool obsFallback, opened, smoothOn = true, lastDirect;
    double fpsSource, fpsGenerated;
    long totalSource, totalGenerated;
    TimeSpan played;
    WindowList.Item lastItem;
    static readonly string Prefs = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NAIMAN", "preferences.txt");
    static int S(float v)
    {
        return Ui.S(v);
    }
    string T(string en, string pol)
    {
        return pl ? pol : en;
    }
    void SavePrefs()
    {
        if (checking || staged)
            return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Prefs));
            File.WriteAllText(Prefs, (pl ? "pl" : "en") + "\n" + route);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
    public Launcher(string report, float forcedScale)
    {
        checkPath = report;
        checking = report != null;
        staged = forcedScale > 0;
        Text = "NAIMAN";
        BackColor = Ui.Bg;
        ForeColor = Ui.Ink;
        AutoScaleMode = AutoScaleMode.None;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        KeyPreview = true;
        DoubleBuffered = true;
        float k = forcedScale;
        if (k <= 0)
        {
            using (var g = Graphics.FromHwnd(IntPtr.Zero))
                k = g.DpiX / 96f;
            // A small or heavily scaled display gets a smaller window, never a scrolling one.
            var area = Screen.FromPoint(Cursor.Position).WorkingArea;
            k = Math.Min(k, Math.Min((area.Width - 24) / (float)BaseW, (area.Height - 56) / (float)BaseH));
        }
        Ui.K = k;
        ClientSize = new Size(S(BaseW), S(BaseH));
        if (!checking && !staged && File.Exists(Prefs))
        {
            try
            {
                string[] pref = File.ReadAllLines(Prefs);
                if (pref.Length >= 1)
                    pl = pref[0] == "pl";
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
        Build();
        Shown += delegate
        {
            if (checking)
            {
                route = mode = 4;
                OpenSource();
                Go(3);
            }
        };
        FormClosing += delegate
        {
            Stop();
            DropThumb();
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
        if (!staged)
            timer.Start();
        Compose();
    }
    static string AppName(WindowList.Item i)
    {
        string p = (i.ProcessName ?? "").ToLowerInvariant();
        return p == "naiman" ? "NAIMAN" : p == "msedge" ? "Microsoft Edge" : p == "chrome" ? "Google Chrome" : p == "firefox" ? "Firefox"
               : p == "brave" ? "Brave" : p == "vivaldi" ? "Vivaldi"
               : p == "remoteplay" || p == "psremoteplay" ? "PS Remote Play"
               : p.StartsWith("chiaki") ? "chiaki-ng" : p.StartsWith("obs") ? "OBS Studio" : p + ".exe";
    }
    Rows.Item Row(WindowList.Item i)
    {
        string kind = i.Kind == 0 ? "Xbox Remote Play" : i.Kind == 1 ? "PlayStation 5"
                      : i.Kind == 2 ? T("OBS projector", "Projektor OBS")
                      : i.Kind == 4 ? T("Motion test", "Test ruchu") : "";
        return new Rows.Item { Title = i.Name, Sub = kind.Length == 0 ? AppName(i) : kind + "  ·  " + AppName(i),
                               Badge = mode == 3 && i.Kind >= 0 ? T("GAME", "GRA") : "", Tag = i };
    }
    static bool SameWindow(object a, object b)
    {
        var x = a as WindowList.Item;
        var y = b as WindowList.Item;
        return x != null && y != null && x.Handle == y.Handle && x.Pid == y.Pid;
    }
    void Scan()
    {
        scanAt = DateTime.UtcNow;
        if (engine != null || staged)
            return;
        bool changed;
        if (DirectCapture)
            changed = ScanDevices();
        else
        {
            var items = WindowList.Scan(mode, demoPid);
            changed = list.SetItems(items.Select(i => Row(i)).ToList(), SameWindow);
            // With every window listed, a single recognised stream is the obvious pick.
            if (list.Selected == null && items.Count(i => i.Kind >= 0) == 1)
            {
                list.Select(items.FindIndex(i => i.Kind >= 0));
                changed = true;
            }
        }
        if (changed)
            Compose();
    }
    void OpenSource()
    {
        if (route == 4)
        {
            if (demo == null || demo.HasExited)
            {
                demo = Process.Start(Application.ExecutablePath, "--demo");
                demoPid = demo.Id;
            }
            return;
        }
        if (route == 0)
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
        if (route == 1)
        {
            foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                                         Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) })
            {
                var sony = Path.Combine(root, "Sony", "PS Remote Play", "RemotePlay.exe");
                if (File.Exists(sony))
                {
                    Process.Start(sony);
                    return;
                }
            }
        }
        string url = route == 0 ? "https://www.xbox.com/remoteplay"
                                : "https://www.playstation.com/" + (pl ? "pl-pl" : "en-us") +
                                      "/support/games/playstation-remote-play-on-pc-and-mac/";
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
        bool direct = DirectCapture;
        var pick = list.Selected;
        var device = direct && pick != null ? pick.Tag as CaptureDevices.Item : null;
        var item = direct ? (device == null ? null
                                            : new WindowList.Item { Handle = Handle, Pid = Process.GetCurrentProcess().Id,
                                                                    Name = device.Name })
                          : pick == null ? null : pick.Tag as WindowList.Item;
        if (item == null)
            return;
        if (!direct && !WindowList.Scan(mode, demoPid).Any(i => i.Handle == item.Handle && i.Pid == item.Pid))
        {
            scanAt = DateTime.MinValue;
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
        DropThumb();
        if (!direct)
            WindowList.SetForegroundWindow(item.Handle);
        bool console = !direct && (route <= 1 || item.Kind == 0 || item.Kind == 1);
        string args = "--window " + item.Handle.ToInt64() + " --pid " + item.Pid + " --parent " +
                      Process.GetCurrentProcess().Id + " --log \"" + statsPath + "\" --stop \"" + stopPath + "\"" +
                      (checking ? " --seconds 9 --windowed" : "") + (console ? " --console" : "");
        if (direct)
        {
            args += " --video " + CaptureDevices.Argument(device.Id);
            if (audioPick != null && audioPick.Id.Length > 0)
                args += " --audio " + CaptureDevices.Argument(audioPick.Id);
        }
        engine = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true });
        if (!checking)
            AllowSetForegroundWindow(engine.Id);
        lastItem = item;
        lastDirect = direct;
        sessionStart = DateTime.UtcNow;
        fpsSource = fpsGenerated = 0;
        totalSource = totalGenerated = 0;
        smoothOn = true;
        notice = "";
        phase = 1;
        Compose();
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
        notice = reason;
        noticeUntil = DateTime.UtcNow.AddSeconds(15);
        if (checking)
        {
            Result = 1;
            Close();
            return;
        }
        Invalidate();
    }
    // After the source disappears, keep looking for it so the session can resume in one click.
    void Recover()
    {
        if (lastItem == null || lastDirect)
            return;
        int i = list.Find(v => SameWindow(v.Tag, lastItem));
        if (i < 0 && lastItem.Kind >= 0)
        {
            var alike = list.All.Where(v => v.Tag is WindowList.Item && ((WindowList.Item)v.Tag).Kind == lastItem.Kind)
                            .ToList();
            if (alike.Count == 1)
                i = list.All.IndexOf(alike[0]);
        }
        if (i >= 0)
            list.Select(i);
        if (phase == 5 && i >= 0)
            phase = 7;
        else if (phase == 7 && i < 0)
            phase = 5;
        else
            return;
        Compose();
    }
    void Poll()
    {
        if (checking && DateTime.UtcNow - launched > TimeSpan.FromSeconds(35))
        {
            Fail("Test timed out.");
            return;
        }
        if (notice.Length > 0 && DateTime.UtcNow > noticeUntil)
        {
            notice = "";
            Invalidate();
        }
        if (engine == null)
        {
            bool watching = step == 3 || phase >= 4;
            if (watching && DateTime.UtcNow - scanAt > TimeSpan.FromSeconds(DirectCapture ? 5 : 2))
            {
                Scan();
                if (phase == 5 || phase == 7)
                    Recover();
            }
            if (checking && phase == 0 && list.Selected != null)
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
            played = DateTime.UtcNow - sessionStart;
            var end = rows.LastOrDefault(s => s.StartsWith("end "));
            var fields = end == null ? new string[0] : end.Split(' ');
            long real = 0, generated = 0;
            bool counted = fields.Length >= 4 && long.TryParse(fields[2], out real) &&
                           long.TryParse(fields[3], out generated);
            totalSource = real;
            totalGenerated = generated;
            string error = rows.LastOrDefault(s => s.StartsWith("error "));
            if (error != null || code != 0)
            {
                phase = 6;
                failure = error == null ? code.ToString() : error.Substring(6);
            }
            else
                phase = lastDirect || stopSent || WindowList.Alive(lastItem) ? 4 : 5;
            if (checking)
            {
                Result = code == 0 && counted && real >= 30 && generated >= 15 ? 0 : 1;
                Close();
                return;
            }
            scanAt = DateTime.MinValue;
            Scan();
            Compose();
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
            fpsSource = (long.Parse(curr[3]) - long.Parse(old[3])) / dt;
            fpsGenerated = (long.Parse(curr[4]) - long.Parse(old[4])) / dt;
            smoothOn = curr.Length <= 7 || curr[7] != "0";
            int now = curr.Length > 8 && curr[8] == "0" ? 3 : 2;
            if (now != phase)
            {
                phase = now;
                Compose();
            }
            else
                Invalidate();
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
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        // Top-left of the screen, so the NAIMAN window in the centre stays readable next to it.
        StartPosition = FormStartPosition.Manual;
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        int wide = Math.Max(320, Math.Min(640, (area.Width - 1060) / 2 - 24));
        ClientSize = new Size(wide, wide * 9 / 16);
        Location = new Point(area.Left + 16, area.Top + 16);
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
    // The 30 Hz plate: posts and blocks sliding left by a fixed step, so doubled motion is easy to see.
    public static void Scene(Graphics g, Rectangle r, long frame)
    {
        float u = r.Height / 540f, shift = (frame * 8) % 160 * u;
        var state = g.Save();
        g.SetClip(r);
        using (var ground = new SolidBrush(Ui.Bg))
            g.FillRectangle(ground, r);
        using (var lane = new Pen(Ui.Line, Math.Max(1f, 2 * u))) using (var block = new SolidBrush(Ui.Action)) using (
            var pale = new SolidBrush(Ui.Ink))
        {
            for (float x = -160 * u; x < r.Width + 160 * u; x += 160 * u)
            {
                float px = r.Left + x - shift;
                g.DrawLine(lane, px, r.Top, px, r.Bottom);
                g.FillRectangle(block, px + 20 * u, r.Top + 250 * u, 64 * u, 40 * u);
                g.FillRectangle(pale, px + 100 * u, r.Top + 330 * u, 24 * u, 110 * u);
            }
        }
        Ui.Logo(g, r.Left + 28 * u, r.Top + 28 * u, 26 * u, Ui.Ink, Ui.Action);
        using (var f = new Font(Ui.Display(10).FontFamily, 26 * u, FontStyle.Regular, GraphicsUnit.Pixel))
            Ui.Text(g, "30 Hz", f, Ui.Muted, r.Left + 190 * u, r.Top + 25 * u);
        g.Restore(state);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        Ui.Quality(e.Graphics);
        Scene(e.Graphics, ClientRectangle, clock.ElapsedMilliseconds * 30 / 1000);
    }
}
