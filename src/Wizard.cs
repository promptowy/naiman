using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Screens. Compose() runs twice with the same code: once to place controls, once to paint the type.
partial class Launcher
{
    [DllImport("user32.dll")]
    static extern bool AllowSetForegroundWindow(int pid);
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr window, int attr, ref int value, int bytes);
    [DllImport("dwmapi.dll")]
    static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);
    [DllImport("dwmapi.dll")]
    static extern int DwmUnregisterThumbnail(IntPtr thumb);
    [DllImport("dwmapi.dll")]
    static extern int DwmQueryThumbnailSourceSize(IntPtr thumb, out Size size);
    [DllImport("dwmapi.dll")]
    static extern int DwmUpdateThumbnailProperties(IntPtr thumb, ref ThumbProps props);
    [StructLayout(LayoutKind.Sequential)]
    struct Edges
    {
        public int Left, Top, Right, Bottom;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct ThumbProps
    {
        public int Flags;
        public Edges Dest, Source;
        public byte Opacity;
        public bool Visible, ClientOnly;
    }
    Btn langPl, langEn, pickXbox, pickPs, pickHdmi, shortcut, demoLink, back, skip, next, open, refresh, alt, start,
        audio, help, toGame, stop, again, other;
    Rows list;
    Graphics G;
    bool painting;
    int tab;
    readonly List<Control> placed = new List<Control>();
    Rectangle preview;
    readonly Stopwatch motion = Stopwatch.StartNew();
    readonly Timer pulse = new Timer();
    IntPtr thumb, thumbSource;

    Btn Make(int kind, EventHandler click)
    {
        var b = new Btn(kind) { Visible = false };
        b.Click += click;
        Controls.Add(b);
        return b;
    }
    void Build()
    {
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        HandleCreated += delegate
        {
            int dark = 1, caption = Ui.Bg.R | Ui.Bg.G << 8 | Ui.Bg.B << 16;
            DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
            DwmSetWindowAttribute(Handle, 35, ref caption, sizeof(int));
        };
        langPl = Make(Btn.Segment, delegate { Speak(true); });
        langEn = Make(Btn.Segment, delegate { Speak(false); });
        langPl.Label = "PL";
        langEn.Label = "EN";
        pickXbox = Make(Btn.Choice, delegate { ChooseRoute(0); });
        pickPs = Make(Btn.Choice, delegate { ChooseRoute(1); });
        pickHdmi = Make(Btn.Choice, delegate { ChooseRoute(2); });
        pickHdmi.Last = true;
        shortcut = Make(Btn.Outline, delegate { ChooseRoute(3); });
        shortcut.Sub = "→";
        demoLink = Make(Btn.Link, delegate { ChooseRoute(4); });
        back = Make(Btn.Outline, delegate { Back(); });
        skip = Make(Btn.Link, delegate { Go(3); });
        next = Make(Btn.Primary, delegate { Go(step + 1); });
        open = Make(Btn.Primary, delegate
        {
            try
            {
                OpenSource();
                opened = true;
                Compose();
                next.Focus();
            }
            catch (Exception e)
            {
                Fail(e.Message);
            }
        });
        refresh = Make(Btn.Outline, delegate
        {
            scanAt = DateTime.MinValue;
            Scan();
        });
        alt = Make(Btn.Outline, delegate
        {
            if (route == 2)
                obsFallback = !obsFallback;
            else
                mode = mode == 3 ? route : 3;
            list.SetItems(new List<Rows.Item>(), SameWindow);
            Scan();
            Compose();
        });
        start = Make(Btn.Primary, delegate { Launch(); });
        audio = Make(Btn.Outline, delegate { AudioMenu(); });
        audio.Sub = "↓";
        help = Make(Btn.Link, delegate { HelpPage(); });
        toGame = Make(Btn.Primary, delegate
        {
            if (lastItem != null)
                WindowList.SetForegroundWindow(lastItem.Handle);
        });
        stop = Make(Btn.Outline, delegate { Stop(); });
        again = Make(Btn.Primary, delegate { Launch(); });
        other = Make(Btn.Outline, delegate
        {
            phase = 0;
            Go(lastDirect || route != 3 && route != 4 ? 3 : 3);
        });
        list = new Rows { Visible = false };
        list.Picked += delegate
        {
            if (built && step == 3 && phase == 0)
            {
                start.Enabled = list.Selected != null;
                Compose();
            }
        };
        list.Confirmed += delegate
        {
            if (phase == 0)
                Launch();
        };
        Controls.Add(list);
        Text = "NAIMAN " + Version;
        StartPad();
        if (!staged && !checking)
        {
            // Only the strip with the sun is repainted, and only while the first screen is showing.
            pulse.Interval = 15;
            pulse.Tick += delegate
            {
                if (step == 0 && phase == 0 && Visible && WindowState != FormWindowState.Minimized)
                    Invalidate(new Rectangle(S(36), S(436 - 112), S(410), S(112)));
            };
            pulse.Start();
        }
        KeyDown += delegate(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Tab && !Ui.Cues)
            {
                Ui.Cues = true;
                Invalidate(true);
            }
            if (e.KeyCode == Keys.Escape && engine == null && (step > 0 || phase != 0))
            {
                e.Handled = true;
                Back();
            }
        };
        built = true;
    }
    void Launch()
    {
        try
        {
            Begin();
        }
        catch (Exception e)
        {
            Fail(e.Message);
        }
    }
    void Speak(bool polish)
    {
        if (pl == polish)
            return;
        pl = polish;
        SavePrefs();
        list.SetItems(new List<Rows.Item>(), SameWindow);
        if (step == 3 || phase >= 4)
            Scan();
        Compose();
    }
    void HelpPage()
    {
        string prefix = pl ? "pl-pl" : "en-us";
        string url = step == 0 || route > 1
                         ? "https://promptowy.github.io/naiman/" + (pl ? "pl.html" : "")
                         : route == 0 ? "https://support.xbox.com/" + (pl ? "pl-PL" : "en-US") +
                                            "/help/games-apps/game-setup-and-play/how-to-set-up-remote-play"
                                      : "https://www.playstation.com/" + prefix +
                                            "/support/games/playstation-remote-play-on-pc-and-mac/";
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception e)
        {
            Fail(e.Message);
        }
    }
    void ChooseRoute(int selected)
    {
        obsFallback = opened = false;
        route = mode = selected;
        SavePrefs();
        list.SetItems(new List<Rows.Item>(), SameWindow);
        if (selected == 4)
        {
            try
            {
                OpenSource();
            }
            catch (Exception e)
            {
                Fail(e.Message);
            }
        }
        Go(selected >= 2 ? 3 : 1);
    }
    void Back()
    {
        if (engine != null)
            return;
        if (phase != 0)
        {
            phase = 0;
            Go(3);
        }
        else
            Go(step == 3 && route >= 2 ? 0 : Math.Max(0, step - 1));
    }
    void Go(int target)
    {
        if (engine != null)
            return;
        step = Math.Min(3, target);
        phase = 0;
        notice = "";
        if (step == 3)
        {
            scanAt = DateTime.MinValue;
            Scan();
        }
        Compose();
        var first = step == 0 ? (Control)pickPs : step == 3 ? (list.Count > 0 ? (Control)start : refresh)
                    : step == 2 && !opened ? open : next;
        if (first.Visible || staged)
            first.Focus();
    }
    void Place(Control c, int x, int y, int w, int h)
    {
        if (painting)
            return;
        c.SetBounds(x, y, w, h);
        c.TabIndex = tab++;
        placed.Add(c);
    }
    int Block(string text, Font font, Color color, int x, int y, int w)
    {
        var size = G.MeasureString(text, font, w, Ui.Flow);
        if (painting)
            using (var b = new SolidBrush(color))
                G.DrawString(text, font, b, new RectangleF(x, y, w, size.Height + S(4)), Ui.Flow);
        return y + (int)Math.Ceiling(size.Height);
    }
    void Compose()
    {
        if (!built)
            return;
        painting = false;
        placed.Clear();
        tab = 0;
        using (var scratch = new Bitmap(1, 1)) using (G = Graphics.FromImage(scratch))
        {
            Ui.Quality(G);
            Draw();
        }
        G = null;
        SuspendLayout();
        foreach (Control c in Controls)
        {
            bool show = placed.Contains(c);
            if (c.Visible != show)
                c.Visible = show;
        }
        ResumeLayout();
        AcceptButton = phase != 0 ? (placed.Contains(again) ? again : placed.Contains(toGame) ? toGame : null)
                       : step == 3 ? start : step == 2 && !opened ? open : step > 0 ? next : null;
        SyncThumb();
        Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        if (!built)
            return;
        G = e.Graphics;
        Ui.Quality(G);
        painting = true;
        Draw();
        painting = false;
        G = null;
    }
    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (built)
            SyncThumb();
    }
    void Draw()
    {
        if (phase != 0)
            SessionScreen();
        else if (step == 0)
            HomeScreen();
        else if (step < 3)
            GuideScreen();
        else
            SourceScreen();
        Chrome();
    }
    string RouteName()
    {
        return route == 0 ? "Xbox" : route == 1 ? "PlayStation 5" : route == 2 ? T("HDMI capture card", "Karta HDMI")
               : route == 4 ? T("Motion test", "Test ruchu") : T("Game already open", "Gra już otwarta");
    }
    void Chrome()
    {
        int w = ClientSize.Width, h = ClientSize.Height;
        if (painting)
        {
            Ui.Corner(G, S(15));
            Ui.Logo(G, S(40), S(25), S(19), Ui.Ink, Ui.Action);
            Ui.Fill(G, Ui.Line, 0, S(68), w, 1);
            Ui.Fill(G, Ui.Line, 0, h - S(52), w, 1);
            Prompts(w - S(40), h - S(39));
            if (route <= 1 && (step > 0 || phase != 0))
                Rail();
            else if (step > 0 || phase != 0)
                Ui.Text(G, RouteName(), Ui.Bold(14), Ui.Muted, S(300), S(25));
        }
        langPl.On = pl;
        langEn.On = !pl;
        help.Label = step == 0 || route > 1 ? T("Open the guide in a browser", "Otwórz instrukcję w przeglądarce")
                     : route == 0 ? T("Official Xbox guide", "Oficjalna instrukcja Xbox")
                                  : T("Official PlayStation guide", "Oficjalna instrukcja PlayStation");
        Place(langPl, w - S(40) - S(88), S(20), S(44), S(30));
        Place(langEn, w - S(40) - S(44), S(20), S(44), S(30));
        Place(help, S(40), h - S(43), help.Fit(G), S(34));
        if (!painting)
        {
            langPl.Invalidate();
            langEn.Invalidate();
        }
    }
    // Like a console dashboard: the bottom edge always says which button does what.
    void Prompts(float right, float y)
    {
        bool canBack = step > 0 || phase != 0;
        var f = Ui.Sans(14);
        string pick = T("select", "wybierz"), leave = T("back", "wstecz"), move = T("move", "ruch");
        float gap = S(8), group = S(22), kw = Math.Max(S(30), Ui.Measure(G, "Enter", Ui.Bold(12.5f), 0).Width + S(16)),
              ew = Math.Max(S(30), Ui.Measure(G, "Esc", Ui.Bold(12.5f), 0).Width + S(16)),
              tw = Math.Max(S(30), Ui.Measure(G, "Tab", Ui.Bold(12.5f), 0).Width + S(16));
        float total = (padSeen ? S(26) : kw) + gap + Ui.Measure(G, pick, f, 0).Width + group +
                      (padSeen ? S(26) + gap + Ui.Measure(G, "D-pad", Ui.Bold(12.5f), 0).Width + S(16) : tw) + gap + Ui.Measure(G, move, f, 0).Width;
        if (canBack)
            total += group + (padSeen ? S(26) : ew) + gap + Ui.Measure(G, leave, f, 0).Width;
        float x = right - total;
        x += (padSeen ? Ui.PadKey(G, "A", x, y) : Ui.Key(G, "Enter", x, y)) + gap;
        Ui.Text(G, pick, f, Ui.Muted, x, y + S(3));
        x += Ui.Measure(G, pick, f, 0).Width + group;
        if (canBack)
        {
            x += (padSeen ? Ui.PadKey(G, "B", x, y) : Ui.Key(G, "Esc", x, y)) + gap;
            Ui.Text(G, leave, f, Ui.Muted, x, y + S(3));
            x += Ui.Measure(G, leave, f, 0).Width + group;
        }
        x += Ui.Key(G, padSeen ? "D-pad" : "Tab", x, y) + gap;
        Ui.Text(G, move, f, Ui.Muted, x, y + S(3));
    }
    // Console, this PC, game window: the three places the walkthrough visits.
    void Rail()
    {
        string[] names = { T("Console", "Konsola"), T("This PC", "Komputer"), T("Game window", "Okno gry") };
        int at = phase != 0 ? 4 : step;
        float x = S(300), y = S(25);
        for (int i = 0; i < 3; i++)
        {
            bool now = at == i + 1, done = at > i + 1;
            string n = (i + 1).ToString();
            Ui.Text(G, n, Ui.Display(14), now ? Ui.Action : Ui.Dim, x, y);
            x += Ui.Measure(G, n, Ui.Display(14), 0).Width + S(8);
            Ui.Text(G, names[i], Ui.Bold(14), now ? Ui.Ink : done ? Ui.Muted : Ui.Dim, x, y - S(1));
            x += Ui.Measure(G, names[i], Ui.Bold(14), 0).Width + S(14);
            if (i < 2)
            {
                Ui.Fill(G, done ? Ui.Muted : Ui.Line, x, y + S(9), S(34), 1);
                x += S(48);
            }
        }
    }
    int Kicker(int x, int y)
    {
        string text = phase == 1   ? T("Bell", "Gong")
                      : phase == 2 ? T("Fight on", "Walka trwa")
                      : phase == 3 ? T("Break", "Przerwa w narożniku")
                      : phase == 4 ? T("Final bell", "Ostatni gong")
                      : phase == 5 ? T("Source is down", "Źródło na deskach")
                      : phase == 6 ? T("Stopped by the referee", "Przerwane przez sędziego")
                      : phase == 7 ? T("Back on its feet", "Źródło wstało")
                      : route <= 1 ? String.Format(T("{0}  ·  round {1} of 3", "{0}  ·  runda {1} z 3"), RouteName(), step)
                                   : RouteName();
        return Block(text, Ui.Bold(14.5f), Ui.Action, x, y, S(430));
    }
    void HomeScreen()
    {
        int x = S(40), rx = S(500), rw = S(500), rh = S(112), ry = S(100);
        pickXbox.Label = "Xbox";
        pickXbox.Sub = T("Remote play in Microsoft Edge", "Gra zdalna w Microsoft Edge");
        pickPs.Label = "PlayStation 5";
        pickPs.Sub = T("PS Remote Play or chiaki-ng", "PS Remote Play albo chiaki-ng");
        pickHdmi.Label = T("HDMI capture card", "Karta HDMI");
        pickHdmi.Sub = T("Elgato and other capture cards", "Elgato i inne karty przechwytujące");
        Place(pickPs, rx, ry, rw, rh);
        Place(pickXbox, rx, ry + rh, rw, rh);
        Place(pickHdmi, rx, ry + rh * 2, rw, rh);
        int y = Block(T("What do you play on?", "Na czym grasz?"), Ui.Display(46), Ui.Ink, x, S(104), S(430));
        Block(T("Three rounds: console, this PC, game window. Then your game fills the screen at twice the frames.",
                "Trzy rundy: konsola, komputer, okno gry. Potem gra wypełnia ekran i ma dwa razy więcej klatek."),
              Ui.Sans(17), Ui.Muted, x, y + S(14), S(390));
        int by = S(436);
        if (painting)
        {
            double beat = staged ? 0.4 : motion.Elapsed.TotalSeconds;
            Ui.Sun(G, x + S(290), by, S(104), (float)(beat * .35));
            // The same block at 30 and at 60 steps a second: the difference NAIMAN makes, live.
            float lane = S(150), block = S(18);
            Ui.Fill(G, Ui.Ink, x + (float)(Math.Floor(beat * 30) / 30 * S(150) % lane), by - S(14), block, S(4));
            Ui.Fill(G, Ui.Action, x + (float)(Math.Floor(beat * 60) / 60 * S(150) % lane), by - S(7), block, S(4));
            Ui.Text(G, "30", Ui.Display(54), Ui.Ink, x - S(2), by - S(98));
            Ui.Text(G, T("frames in", "klatek wchodzi"), Ui.Sans(14), Ui.Muted, x, by - S(36));
            Ui.Text(G, "60", Ui.Display(54), Ui.Action, x + S(84), by - S(98));
            Ui.Text(G, T("frames out", "wychodzi"), Ui.Sans(14), Ui.Muted, x + S(108), by - S(36));
            Ui.Fill(G, Ui.Line, x, by, S(400), 1);
            Ui.Text(G, T("Game already running on this PC?", "Gra już działa na tym komputerze?"), Ui.Bold(15.5f), Ui.Ink,
                    x, by + S(18));
        }
        shortcut.Label = T("Go straight to the game window", "Przejdź prosto do okna z grą");
        Place(shortcut, x, by + S(52), S(400), S(52));
        demoLink.Label = T("No console at hand? Run the motion test", "Nie masz konsoli pod ręką? Włącz test ruchu");
        Place(demoLink, rx, ry + rh * 3 + S(20), demoLink.Fit(G), S(34));
    }
    static string[] R(string label, string hint, bool target)
    {
        return new[] { label, hint, target ? "1" : "" };
    }
    void GuideScreen()
    {
        bool xb = route == 0, console = step == 1;
        int x = S(40), y = Kicker(x, S(104));
        string h1 = console ? (xb ? T("Turn on remote features on your Xbox", "Włącz funkcje zdalne na Xboxie")
                                  : T("Turn on Remote Play on your PS5", "Włącz Grę zdalną na PS5"))
                            : (xb ? T("Open your Xbox in Edge", "Otwórz Xboxa w Edge")
                                  : T("Open your PS5 on this PC", "Otwórz PS5 na tym komputerze"));
        string lead =
            console ? (xb ? T("You do this once, with the controller, on the console itself. Leave the Xbox switched on.",
                              "Robisz to raz, padem, na samej konsoli. Zostaw Xboxa włączonego.")
                          : T("You do this once, on the console itself. Leave the PS5 switched on for the first connection.",
                              "Robisz to raz, na samej konsoli. Na pierwsze połączenie zostaw PS5 włączone."))
                    : (xb ? T("NAIMAN opens the Xbox Remote Play page. Connect the controller to this PC by USB or Bluetooth.",
                              "NAIMAN otworzy stronę Xbox Remote Play. Pada podłącz do tego komputera przez USB albo Bluetooth.")
                          : T("You need Sony's free PS Remote Play app. NAIMAN opens it, or its download page if it is not installed.",
                              "Potrzebna jest darmowa aplikacja PS Remote Play od Sony. NAIMAN ją otworzy albo pokaże stronę pobierania."));
        y = Block(h1, Ui.Display(33), Ui.Ink, x, y + S(8), S(430));
        y = Block(lead, Ui.Sans(16.5f), Ui.Muted, x, y + S(14), S(410));
        if (!console)
        {
            open.Label = xb ? T("Open Xbox Remote Play", "Otwórz Xbox Remote Play")
                            : T("Open PS Remote Play", "Otwórz PS Remote Play");
            open.Kind = opened ? Btn.Outline : Btn.Primary;
            open.Sub = "→";
            Place(open, x, y + S(26), S(400), S(54));
        }
        next.Label = console ? T("Done, next round", "Włączone, następna runda") : T("I can see the game on this PC", "Widzę grę na komputerze");
        next.Kind = console || opened ? Btn.Primary : Btn.Outline;
        next.Sub = "→";
        Place(next, S(660), S(516), S(340), S(56));
        skip.Label = T("My game is already open", "Mam już otwartą grę");
        skip.Tail = "→";
        back.Label = T("Back", "Wstecz");
        Place(skip, S(184), S(527), skip.Fit(G), S(34));
        Place(back, x, S(520), S(124), S(48));
        if (!painting)
        {
            open.Invalidate();
            next.Invalidate();
            return;
        }
        if (console && xb)
            Steps(T("On the Xbox, in this order", "Na Xboxie, po kolei"),
                  R(T("Xbox button", "Przycisk Xbox"), T("on the controller, it opens the guide", "na padzie, otwiera przewodnik"), false),
                  R(T("Profile & system", "Profil i system"), "", false), R(T("Settings", "Ustawienia"), "", false),
                  R(T("Devices & connections", "Urządzenia i połączenia"), "", false),
                  R(T("Remote features", "Funkcje zdalne"), "", false),
                  R(T("Enable remote features", "Włącz funkcje zdalne"), T("tick this box", "zaznacz to pole"), true));
        else if (console)
            Steps(T("On the PS5, in this order", "Na PS5, po kolei"),
                  R(T("Settings", "Ustawienia"), T("the gear in the top right of the home screen", "koło zębate w prawym górnym rogu ekranu głównego"), false),
                  R("System", "", false), R(T("Remote Play", "Gra zdalna"), "", false),
                  R(T("Enable Remote Play", "Włącz Grę zdalną"), T("turn the switch on", "przesuń przełącznik"), true));
        else if (xb)
            Steps(T("In Microsoft Edge, in this order", "W Microsoft Edge, po kolei"),
                  R(T("Sign in", "Zaloguj się"), T("with the Microsoft account from your console", "kontem Microsoft z konsoli"), false),
                  R(T("Your Xbox", "Twój Xbox"), T("pick the console from the list", "wybierz konsolę z listy"), false),
                  R(T("Start the game", "Uruchom grę"), T("with the controller, as on the TV", "padem, tak jak na telewizorze"), false),
                  R("F11", T("hides the browser bars, the picture fills the window", "chowa paski przeglądarki, obraz wypełnia okno"), true));
        else
            Steps(T("On this PC, in this order", "Na tym komputerze, po kolei"),
                  R(T("DualSense by USB", "DualSense przez USB"), T("plug the controller into this PC", "podłącz pada kablem do tego komputera"), false),
                  R(T("Sign In to PSN", "Zaloguj się w PSN"), T("with the account from your console", "kontem z konsoli"), false),
                  R("PS5", T("choose the console type", "wybierz typ konsoli"), false),
                  R(T("Wait for the picture", "Poczekaj na obraz"), T("the first connection can take a minute", "pierwsze połączenie trwa do minuty"), true));
    }
    void Steps(string title, params string[][] rows)
    {
        int x = S(500), y = S(100), w = S(500), h = S(396);
        Ui.Fill(G, Ui.Surface, x, y, w, h);
        Ui.Text(G, title, Ui.Bold(14), Ui.Muted, x + S(28), y + S(24));
        int ry = y + S(62);
        for (int i = 0; i < rows.Length; i++)
        {
            bool target = rows[i][2] == "1", hinted = rows[i][1].Length > 0;
            int rh = S(hinted ? 64 : 46);
            if (target)
            {
                Ui.Fill(G, Ui.Raised, x, ry, w, rh);
                Ui.Fill(G, Ui.Action, x, ry, S(4), rh);
            }
            else
                Ui.Fill(G, Ui.Line, x + S(28), ry, w - S(56), 1);
            Ui.Text(G, (i + 1).ToString(), Ui.Display(15), target ? Ui.Action : Ui.Dim, x + S(28), ry + S(14));
            Ui.Clip(G, rows[i][0], Ui.Bold(18), Ui.Ink, new RectangleF(x + S(64), ry + S(10), w - S(92), S(28)));
            if (hinted)
                Ui.Clip(G, rows[i][1], Ui.Sans(14), Ui.Muted, new RectangleF(x + S(64), ry + S(35), w - S(92), S(22)));
            ry += rh;
        }
    }
    float Keys_(float x, float y, string a, string b, string c, string text)
    {
        x += Ui.Key(G, a, x, y) + S(5);
        x += Ui.Key(G, b, x, y) + S(5);
        if (c.Length > 0)
            x += Ui.Key(G, c, x, y) + S(5);
        Ui.Text(G, text, Ui.Sans(14.5f), Ui.Muted, x + S(8), y + S(3));
        return y + S(36);
    }
    void SourceScreen()
    {
        bool direct = DirectCapture, obs = route == 2 && obsFallback;
        int x = S(40), count = list.Count, y = Kicker(x, S(104));
        string h1 = route == 4 ? T("Try 2x without a console", "Sprawdź 2x bez konsoli")
                    : direct   ? T("Choose your capture card", "Wybierz kartę HDMI")
                    : obs      ? T("Choose the OBS projector", "Wybierz projektor OBS")
                               : T("Choose the game window", "Wybierz okno z grą");
        y = Block(h1, Ui.Display(33), Ui.Ink, x, y + S(8), S(440));
        refresh.Label = T("Refresh", "Odśwież");
        if (count == 0)
        {
            string none = direct ? T("No capture card found yet", "Nie widzę jeszcze karty")
                          : route == 4 ? T("The test window is opening", "Otwieram okno testu")
                                       : T("No game window found yet", "Nie widzę jeszcze okna z grą");
            string[] todo =
                direct ? new[] { T("Console HDMI OUT into the card's HDMI IN", "HDMI OUT konsoli do HDMI IN karty"),
                                 T("Card into a USB port on this PC", "Kartę do portu USB tego komputera"),
                                 T("Press Refresh below", "Kliknij Odśwież poniżej") }
                : obs ? new[] { T("In OBS add a Video Capture Device source", "W OBS dodaj źródło: Urządzenie do przechwytywania wideo"),
                                T("Right-click it: Windowed Projector (Source)", "Kliknij je prawym: Projektor okienkowy (źródło)"),
                                T("Press Refresh below", "Kliknij Odśwież poniżej") }
                : route == 4 ? new[] { T("A window named NAIMAN Motion Test appears", "Pojawi się okno NAIMAN Motion Test"),
                                       T("Leave it visible, not minimized", "Zostaw je widoczne, nie minimalizuj"),
                                       T("Press Refresh below if it is not listed", "Kliknij Odśwież, jeśli go tu nie ma") }
                : new[] { route == 0   ? T("Open Xbox Remote Play in Edge and start the game", "Otwórz Xbox Remote Play w Edge i uruchom grę")
                          : route == 1 ? T("Open PS Remote Play and connect to the PS5", "Otwórz PS Remote Play i połącz się z PS5")
                                       : T("Open the window that shows your game", "Otwórz okno, w którym widać grę"),
                          T("Keep it visible, not minimized", "Zostaw je widoczne, nie minimalizuj"),
                          T("Press Refresh below", "Kliknij Odśwież poniżej") };
            y += S(22);
            if (painting)
            {
                Ui.Fill(G, Ui.Line, x, y, S(420), 1);
                Ui.Text(G, none, Ui.Bold(17), Ui.Ink, x, y + S(18));
                for (int i = 0; i < todo.Length; i++)
                {
                    Ui.Text(G, (i + 1).ToString(), Ui.Display(14), Ui.Action, x, y + S(60 + i * 32));
                    Ui.Clip(G, todo[i], Ui.Sans(15.5f), Ui.Muted, new RectangleF(x + S(26), y + S(57 + i * 32), S(400), S(26)));
                }
            }
            Place(refresh, x, y + S(166), S(150), S(44));
            if (painting)
                Ui.Text(G, T("The list also refreshes by itself.", "Lista odświeża się też sama."), Ui.Sans(14), Ui.Dim,
                        x + S(166), y + S(178));
        }
        else
        {
            string lead = direct ? T("Controller stays on the console. For sound on this PC, pick the card's audio input.",
                                     "Pad zostaje przy konsoli. Żeby słyszeć dźwięk na PC, wybierz wejście audio karty.")
                          : count == 1 && list.Selected != null
                                ? T("Found it. Check the preview on the right and start.", "Jest. Sprawdź podgląd po prawej i zaczynaj.")
                          : list.Selected == null
                                ? T("Click the window that shows your game. Its preview appears on the right.",
                                    "Kliknij okno, w którym widać grę. Podgląd pojawi się po prawej.")
                                : T("Several windows match. Check the preview on the right before you start.",
                                    "Pasuje kilka okien. Sprawdź podgląd po prawej, zanim zaczniesz.");
            y = Block(lead, Ui.Sans(16), Ui.Muted, x, y + S(12), S(420)) + S(16);
            int fit = Math.Max(1, (S(direct ? 400 : 452) - y) / list.RowHeight), shown = Math.Min(count, fit);
            if (painting)
                Ui.Fill(G, Ui.Line, x, y - 1, S(420), 1);
            Place(list, x, y, S(420), shown * list.RowHeight);
            y += shown * list.RowHeight + S(12);
            if (direct)
            {
                audio.Label = T("Sound: ", "Dźwięk: ") + (audioPick == null ? "" : audioPick.Name);
                Place(audio, x, y, S(420), S(44));
                y += S(54);
            }
            Place(refresh, x, y, S(118), S(38));
            if (painting && count > shown)
                Ui.Text(G, String.Format(T("{0} more below, scroll the list", "jeszcze {0} niżej, przewiń listę"), count - shown),
                        Ui.Sans(14), Ui.Dim, x + S(134), y + S(9));
        }
        if (direct)
        {
            if (painting)
                Steps(T("Connect in this order", "Połącz po kolei"),
                      R(T("Console HDMI OUT", "HDMI OUT konsoli"), T("cable into the card's HDMI IN", "kablem do HDMI IN karty"), false),
                      R(T("Card to this PC", "Karta do komputera"), T("USB, straight into the PC", "USB, prosto do komputera"), false),
                      R(T("Controller on the console", "Pad przy konsoli"), T("it does not go through the PC", "nie przechodzi przez komputer"), false),
                      R(T("Play with 2x smoothing", "Graj z wygładzaniem 2x"), T("the picture opens fullscreen on this monitor", "obraz otworzy się na pełnym ekranie tego monitora"), true));
        }
        else
        {
            preview = new Rectangle(S(500), S(100), S(500), S(281));
            if (painting)
            {
                var pick = list.Selected;
                Ui.Fill(G, Ui.Surface, preview.X, preview.Y, preview.Width, preview.Height);
                if (pick == null)
                {
                    string hint = T("The window you pick shows up here", "Tu zobaczysz wybrane okno");
                    var f = Ui.Sans(15);
                    Ui.Text(G, hint, f, Ui.Dim, preview.X + (preview.Width - Ui.Measure(G, hint, f, 0).Width) / 2f,
                            preview.Y + preview.Height / 2f - S(10));
                }
                else if (staged)
                    Demo.Scene(G, Rectangle.Inflate(preview, -1, -1), 7);
                Ui.Frame(G, Ui.Line, preview.X, preview.Y, preview.Width, preview.Height, 1);
                if (pick != null)
                {
                    Ui.Clip(G, pick.Title, Ui.Bold(15), Ui.Ink, new RectangleF(preview.X, preview.Bottom + S(12), preview.Width, S(24)));
                    Ui.Text(G, T("This picture goes fullscreen with 2x smoothing.", "Ten obraz trafi na pełny ekran z wygładzaniem 2x."),
                            Ui.Sans(14), Ui.Muted, preview.X, preview.Bottom + S(35));
                }
                float ky = Keys_(preview.X, S(446), "Ctrl", "Alt", "B", T("compare the original with 2x", "porównaj oryginał z 2x"));
                Keys_(preview.X, ky, "Ctrl", "Alt", "Q", T("stop and come back here", "zakończ i wróć tutaj"));
            }
        }
        start.Label = T("Play with 2x smoothing", "Graj z wygładzaniem 2x");
        start.Enabled = list.Selected != null;
        Place(start, S(660), S(516), S(340), S(56));
        if (route != 3 && route != 4)
        {
            alt.Label = route == 2 ? (obsFallback ? T("Use the card directly", "Użyj karty bezpośrednio")
                                                  : T("Use an OBS projector", "Użyj projektora OBS"))
                                   : mode == 3 ? T("Show matching only", "Pokaż tylko pasujące")
                                               : T("Show all windows", "Pokaż wszystkie okna");
            int aw = alt.Fit(G);
            Place(alt, S(660) - S(16) - aw, S(520), aw, S(48));
        }
        back.Label = T("Back", "Wstecz");
        Place(back, x, S(520), S(124), S(48));
        if (painting && notice.Length > 0)
            Ui.Clip(G, notice, Ui.Sans(14), Ui.Action, new RectangleF(S(184), S(533), S(300), S(22)));
    }
    static string Count(long n)
    {
        return n.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", " ");
    }
    void SessionScreen()
    {
        bool live = phase >= 1 && phase <= 3;
        int x = S(40), y = Kicker(x, S(104));
        string h1 = phase == 1   ? T("Starting capture…", "Uruchamiam przechwytywanie…")
                    : phase == 2 ? (smoothOn ? T("Your game is fullscreen with 2x", "Gra działa na pełnym ekranie z 2x")
                                             : T("You are watching the original", "Oglądasz oryginalny obraz"))
                    : phase == 3 ? T("Click the game window", "Kliknij okno gry")
                    : phase == 4 ? T("Session finished", "Sesja zakończona")
                    : phase == 5 ? T("The game window is gone", "Okno gry zniknęło")
                    : phase == 6 ? T("Capture stopped", "Przechwytywanie przerwane")
                                 : T("The game window is back", "Okno gry wróciło");
        string lead =
            phase == 1   ? T("The picture goes fullscreen in a moment. Nothing to click.",
                             "Za chwilę obraz przejdzie na pełny ekran. Nic nie musisz klikać.")
            : phase == 2 ? (smoothOn ? T("Play as usual. This window can stay in the background.",
                                         "Graj jak zwykle. To okno może zostać w tle.")
                                     : T("2x is switched off for comparison. Ctrl + Alt + B turns it back on.",
                                         "2x jest wyłączone do porównania. Ctrl + Alt + B włącza je z powrotem."))
            : phase == 3 ? T("The game lost focus, so NAIMAN stepped off the screen and the controller may not respond. Go back to the game and the fullscreen picture returns.",
                             "Gra straciła fokus, więc NAIMAN zszedł z ekranu, a pad może nie reagować. Wróć do gry, a obraz na pełnym ekranie pojawi się sam.")
            : phase == 4 ? T("You can start again with the same window or pick another one.",
                             "Możesz zagrać ponownie w tym samym oknie albo wybrać inne.")
            : phase == 5 ? T("It was closed or minimized. Restore it and NAIMAN will find it again by itself.",
                             "Zostało zamknięte albo zminimalizowane. Przywróć je, a NAIMAN sam znajdzie je ponownie.")
            : phase == 6 ? failure
                         : T("One click and the picture is fullscreen again.", "Jedno kliknięcie i obraz znów jest na pełnym ekranie.");
        y = Block(h1, Ui.Display(33), Ui.Ink, x, y + S(8), S(440));
        y = Block(lead, Ui.Sans(16.5f), Ui.Muted, x, y + S(14), S(410));
        if (painting && lastItem != null && lastItem.Name != null)
        {
            Ui.Fill(G, Ui.Line, x, y + S(22), S(420), 1);
            Ui.Text(G, T("Source", "Źródło"), Ui.Sans(13.5f), Ui.Dim, x, y + S(34));
            Ui.Clip(G, lastItem.Name, Ui.Bold(15), Ui.Ink, new RectangleF(x, y + S(54), S(420), S(24)));
        }
        if (live)
        {
            toGame.Label = T("Back to the game", "Wróć do gry");
            stop.Label = T("Stop", "Zatrzymaj");
            if (!lastDirect)
                Place(toGame, S(660), S(516), S(340), S(56));
            Place(stop, x, S(520), S(150), S(48));
        }
        else
        {
            again.Label = phase == 7 ? T("Resume 2x smoothing", "Wznów wygładzanie 2x") : T("Play again", "Graj ponownie");
            again.Enabled = phase != 5 && list.Selected != null;
            other.Label = T("Pick another source", "Wybierz inne źródło");
            Place(again, S(660), S(516), S(340), S(56));
            Place(other, x, S(520), other.Fit(G), S(48));
        }
        if (!painting)
            return;
        int px = S(500), py = S(100), pw = S(500), ph = S(396);
        Ui.Fill(G, Ui.Surface, px, py, pw, ph);
        string a = live ? (phase == 1 ? "–" : fpsSource.ToString("0")) : Count(totalSource),
               b = live ? (phase == 1 ? "–" : "+" + fpsGenerated.ToString("0")) : "+" + Count(totalGenerated);
        var big = Ui.Display(live ? 72 : 40);
        int ny = py + S(live ? 22 : 40), ly = py + S(112);
        Ui.Text(G, a, big, Ui.Ink, px + S(26), ny);
        Ui.Text(G, b, big, live && !smoothOn ? Ui.Dim : Ui.Action, px + S(262), ny);
        Ui.Text(G, live ? T("source frames / s", "klatek źródłowych / s") : T("source frames", "klatek źródłowych"),
                Ui.Sans(15), Ui.Muted, px + S(28), ly);
        Ui.Text(G, live ? T("generated frames / s", "klatek wygenerowanych / s") : T("generated frames", "klatek wygenerowanych"),
                Ui.Sans(15), Ui.Muted, px + S(264), ly);
        Ui.Fill(G, Ui.Line, px + S(28), py + S(156), pw - S(56), 1);
        string state = live ? (phase == 1 ? T("Waiting for the first frame", "Czekam na pierwszą klatkę")
                                          : T("On screen now: ", "Teraz na ekranie: ") +
                                                (phase == 3 ? T("nothing, the game is in the background", "nic, gra jest w tle")
                                                 : smoothOn ? T("2x smoothing", "wygładzanie 2x") : T("original", "oryginał")))
                            : T("Play time: ", "Czas gry: ") + String.Format("{0}:{1:00}", (int)played.TotalMinutes, played.Seconds);
        Ui.Text(G, state, Ui.Bold(15), Ui.Ink, px + S(28), py + S(174));
        if (!live)
        {
            string after = phase == 5 ? T("Looking for the window every 2 seconds.", "Szukam okna co 2 sekundy.")
                           : phase == 7 ? T("Found the same game window again.", "Znalazłem to samo okno gry.")
                           : phase == 6 ? T("Nothing was changed on the console or in the game.", "Nic nie zmieniło się na konsoli ani w grze.")
                                        : T("Frame counts stay on this PC. Nothing is sent anywhere.", "Liczniki klatek zostają na tym komputerze. Nic nie jest wysyłane.");
            Ui.Text(G, after, Ui.Sans(14.5f), Ui.Muted, px + S(28), py + S(204));
            return;
        }
        float ky = Keys_(px + S(28), py + S(226), "Ctrl", "Alt", "B", T("compare the original with 2x", "porównaj oryginał z 2x"));
        ky = Keys_(px + S(28), ky + S(4), "Ctrl", "Alt", "Q", T("stop and come back here", "zakończ i wróć tutaj"));
        Keys_(px + S(28), ky + S(4), "Alt", "Tab", "", T("controller not responding? Go back to the game", "pad nie reaguje? Wróć do okna gry"));
    }
    void AudioMenu()
    {
        var menu = new ContextMenuStrip { ShowImageMargin = false, BackColor = Ui.Surface, ForeColor = Ui.Ink,
                                          Font = Ui.Sans(15), Renderer = new ToolStripProfessionalRenderer(new MenuColors()) };
        foreach (var input in audioInputs)
        {
            var pick = input;
            menu.Items.Add(new ToolStripMenuItem(input.Name, null, delegate
            {
                audioPick = pick;
                Compose();
            }) { ForeColor = Ui.Ink, Padding = new Padding(S(6)) });
        }
        menu.Show(audio, new Point(0, audio.Height));
    }
    void DropThumb()
    {
        if (thumb != IntPtr.Zero)
            DwmUnregisterThumbnail(thumb);
        thumb = thumbSource = IntPtr.Zero;
    }
    // A live DWM thumbnail of the selected window: the person sees their own game before starting.
    void SyncThumb()
    {
        var pick = phase == 0 && step == 3 && !DirectCapture && !staged && Visible && list.Selected != null
                       ? list.Selected.Tag as WindowList.Item : null;
        IntPtr want = pick == null ? IntPtr.Zero : pick.Handle;
        if (want != thumbSource)
        {
            DropThumb();
            if (want != IntPtr.Zero && DwmRegisterThumbnail(Handle, want, out thumb) == 0)
                thumbSource = want;
            else
                thumb = IntPtr.Zero;
        }
        if (thumb == IntPtr.Zero)
            return;
        Size size;
        if (DwmQueryThumbnailSourceSize(thumb, out size) != 0 || size.Width < 1 || size.Height < 1)
            return;
        var box = Rectangle.Inflate(preview, -1, -1);
        float scale = Math.Min(box.Width / (float)size.Width, box.Height / (float)size.Height);
        int w = (int)(size.Width * scale), h = (int)(size.Height * scale), x = box.X + (box.Width - w) / 2,
            y = box.Y + (box.Height - h) / 2;
        var props = new ThumbProps { Flags = 1 | 4 | 8 | 16, Dest = new Edges { Left = x, Top = y, Right = x + w, Bottom = y + h },
                                     Opacity = 255, Visible = true, ClientOnly = true };
        DwmUpdateThumbnailProperties(thumb, ref props);
    }
}
