using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

partial class Launcher
{
    [DllImport("user32.dll")]
    static extern bool AllowSetForegroundWindow(int pid);
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr window, int attr, ref int value, int bytes);
    bool guideReady;
    int step, route;
    Label intro, stage, routeName, note;
    Button back, next, skip, demoLink, otherLink, help;
    HardwareButton xbox, ps;
    PictureBox consolePicture;
    Bitmap xboxPhoto, psPhoto;
    readonly Color sage = Color.FromArgb(182, 207, 164), lilac = Color.FromArgb(195, 185, 222);
    static Bitmap Photo(string name)
    {
        using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)) using (
            var img = Image.FromStream(s)) return new Bitmap(img);
    }
    void InitWizard()
    {
        SuspendLayout();
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        HandleCreated += delegate
        {
            int dark = 1;
            DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
        };
        ClientSize = new Size(808, 558);
        MinimumSize = new Size(824, 597);
        MaximumSize = new Size(824, 597);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        AutoScroll = true;
        MinimumSize = Size.Empty;
        MaximumSize = Size.Empty;
        ClientSize = new Size(808, 558);
        if (Screen.FromControl(this).WorkingArea.Height < Height || Screen.FromControl(this).WorkingArea.Width < Width)
        {
            AutoScrollMinSize = ClientSize;
            var r = Screen.FromControl(this).WorkingArea;
            Size = new Size(Math.Min(Width, r.Width), Math.Min(Height, r.Height));
        }
        brand.SetBounds(32, 17, 300, 39);
        brand.Font = new Font("Segoe UI", 19, FontStyle.Bold | FontStyle.Italic);
        brand.ForeColor = white;
        language.SetBounds(659, 23, 115, 29);
        stage = LabelAt(34, 78, 740, 24, 10, true);
        stage.ForeColor = accent;
        heading.SetBounds(29, 112, 751, 58);
        heading.Font = new Font("Segoe UI", 29, FontStyle.Bold);
        intro = LabelAt(34, 184, 740, 47, 12, false);
        intro.ForeColor = muted;
        xboxPhoto = Photo("xbox.png");
        psPhoto = Photo("ps5.png");
        xbox = new HardwareButton("Xbox", sage, xboxPhoto);
        ps = new HardwareButton("PlayStation 5", lilac, psPhoto);
        xbox.SetBounds(34, 239, 358, 196);
        ps.SetBounds(407, 239, 367, 196);
        Controls.Add(xbox);
        Controls.Add(ps);
        xbox.Click += delegate
        {
            ChooseRoute(0);
        };
        ps.Click += delegate
        {
            ChooseRoute(1);
        };
        back = ButtonAt(34, 488, 130, 39, false);
        back.Click += delegate
        {
            if (engine == null)
                Go(step == 3 && route >= 2 ? 0 : Math.Max(0, step - 1));
        };
        next = ButtonAt(417, 482, 357, 45, true);
        next.Click += delegate
        {
            Go(Math.Min(3, step + 1));
        };
        skip = ButtonAt(174, 488, 228, 39, false);
        skip.Click += delegate
        {
            if (step == 3)
            {
                mode.SelectedIndex = 3;
                Scan();
                RenderGuide();
            }
            else
                Go(3);
        };
        demoLink = ButtonAt(34, 480, 214, 40, false);
        demoLink.Click += delegate
        {
            ChooseRoute(4);
        };
        otherLink = ButtonAt(260, 480, 220, 40, false);
        otherLink.Click += delegate
        {
            ChooseRoute(2);
        };
        help = ButtonAt(524, 479, 250, 40, false);
        help.Click += delegate
        {
            HelpPage();
        };
        consolePicture = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, TabStop = false,
                                          Location = new Point(34, 195), Size = new Size(181, 255) };
        Controls.Add(consolePicture);
        routeName = LabelAt(235, 196, 530, 46, 16, true);
        note = LabelAt(235, 252, 529, 145, 12, false);
        open.SetBounds(235, 399, 352, 42);
        window.SetBounds(34, 283, 606, 30);
        refresh.SetBounds(651, 278, 123, 38);
        windowLabel.SetBounds(34, 255, 600, 27);
        status.SetBounds(34, 337, 738, 45);
        keys.SetBounds(34, 414, 730, 48);
        start.SetBounds(417, 482, 357, 45);
        start.Font = new Font("Segoe UI", 12, FontStyle.Bold);
        step = checking ? 3 : 0;
        route = checking ? 4 : Math.Max(0, mode.SelectedIndex);
        guideReady = true;
        ResumeLayout();
        RenderGuide();
    }
    void HelpPage()
    {
        string prefix = pl ? "pl-pl" : "en-us";
        string url = step == 0    ? "https://promptowy.github.io/naiman/" + (pl ? "pl.html" : "")
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
        route = selected;
        mode.SelectedIndex = selected;
        if (selected == 4)
        {
            OpenSource();
            Go(3);
        }
        else
            Go(selected == 2 ? 3 : 1);
    }
    void Go(int target)
    {
        if (engine != null)
            return;
        if (target <= 2 && route <= 1 && mode.SelectedIndex != route)
            mode.SelectedIndex = route;
        step = target;
        preserveStatusUntil = DateTime.MinValue;
        if (target == 3)
            Scan();
        RenderGuide();
        if (step != 0)
            heading.Focus();
    }
    void RenderGuide()
    {
        SuspendLayout();
        bool home = step == 0, walk = step == 1 || step == 2, play = step == 3, isXbox = route == 0, hdmi = route == 2;
        inputLabel.Visible = mode.Visible = hint.Visible = false;
        xbox.Visible = ps.Visible = demoLink.Visible = otherLink.Visible = home;
        intro.Visible = home || play;
        back.Visible = !home;
        back.Enabled = engine == null;
        skip.Visible = walk || play && route != 4;
        skip.Enabled = engine == null;
        next.Visible = walk;
        open.Visible = step == 2;
        note.Visible = routeName.Visible = consolePicture.Visible = walk;
        refresh.Visible = windowLabel.Visible = window.Visible = keys.Visible = status.Visible = start.Visible = play;
        help.Visible = home;
        xbox.Prompt = T("Choose Xbox", "Wybierz Xbox");
        ps.Prompt = T("Choose PS5", "Wybierz PS5");
        xbox.AccessibleName = xbox.Text = xbox.Prompt;
        ps.AccessibleName = ps.Text = ps.Prompt;
        xbox.Invalidate();
        ps.Invalidate();
        stage.Text = home ? T("SETUP GUIDE", "KONFIGURACJA")
                          : String.Format(T("{0}    /    STEP {1} OF 3", "{0}    /    KROK {1} Z 3"),
                                          isXbox       ? "XBOX"
                                          : route == 1 ? "PLAYSTATION 5"
                                          : hdmi       ? "HDMI"
                                                       : "NAIMAN",
                                          step);
        heading.Text = home         ? T("Which console do you play on?", "Na czym grasz?")
                       : step == 1  ? T("Enable Remote Play", "Włącz grę zdalną")
                       : step == 2  ? T("Open your console on this PC", "Połącz konsolę z PC")
                       : route == 4 ? T("Try smoothing without a console", "Sprawdź wygładzanie bez konsoli")
                                    : T("Choose your game window", "Wybierz okno z grą");
        intro.Text =
            home         ? T("Pick your console for instructions on connecting it to this PC.",
                             "Wybierz konsolę. Pokażę ci, co włączyć na niej i na komputerze.")
            : route == 4 ? T("The test scene opens in its own window. Start playback and compare with Ctrl + Alt + B.",
                             "Plansza testowa otwiera się w osobnym oknie. Uruchom wygładzanie i porównaj ruch " +
                             "skrótem Ctrl + Alt + B.")
            : hdmi
                ? T("In OBS, add your capture card as a Video Capture Device and open its Windowed Projector. Select " +
                    "that window below.",
                    "W OBS dodaj kartę jako urządzenie do przechwytywania wideo. Otwórz projektor okienkowy tego " +
                    "źródła i wybierz go poniżej.")
                : T("Start with a visible game. NAIMAN finds the stream window and applies its 2x settings.",
                    "Zostaw grę widoczną w przywróconym oknie. NAIMAN wykryje transmisję i sam ustawi wygładzanie 2x.");
        routeName.Text =
            step == 1
                ? (isXbox ? T("On your Xbox", "Na Xboxie") : T("On your PlayStation 5", "Na PlayStation 5"))
                : (isXbox ? T("In Microsoft Edge", "W Microsoft Edge") : T("In PS Remote Play", "W PS Remote Play"));
        note.Text =
            step == 1
                ? (isXbox ? T("Press the Xbox button on the controller. Open Profile & system > Settings > Devices & " +
                              "connections > Remote features. Enable remote features and leave the console on.",
                              "Naciśnij przycisk Xbox na padzie. Wejdź w Profil i system > Ustawienia > Urządzenia i " +
                              "połączenia > Funkcje zdalne. Włącz funkcje zdalne i zostaw konsolę włączoną.")
                          : T("On the PS5 home screen open Settings > System > Remote Play. Turn on Enable Remote " +
                              "Play. Leave the console on for your first connection.",
                              "Na ekranie głównym PS5 otwórz Ustawienia > System > Remote Play. Włącz funkcję Remote " +
                              "Play. Na pierwszą próbę zostaw konsolę włączoną."))
                : (isXbox ? T("Open Xbox Remote Play below. Sign in with the account used on your console, pick your " +
                              "Xbox and start the game. Come back here once the picture is moving.",
                              "Otwórz Xbox Remote Play poniżej. Zaloguj się kontem używanym na konsoli, wybierz " +
                              "Xboxa i uruchom grę. Gdy zobaczysz rozgrywkę, wróć tutaj.")
                          : T("Connect a DualSense to the PC by USB. Open or install Sony's PS Remote Play, sign in " +
                              "with the account used on your PS5 and choose PS5. Return here when you see the game.",
                              "Podłącz DualSense do PC przez USB. Otwórz lub zainstaluj PS Remote Play od Sony, " +
                              "zaloguj się kontem z konsoli i wybierz PS5. Gdy zobaczysz grę, wróć tutaj."));
        consolePicture.Image = isXbox ? xboxPhoto : psPhoto;
        consolePicture.BackColor = isXbox ? sage : lilac;
        open.Text = isXbox ? T("Open Xbox Remote Play", "Otwórz Xbox Remote Play")
                           : T("Open or get PS Remote Play", "Otwórz lub pobierz PS Remote Play");
        demoLink.Text = T("Try without a console", "Test bez konsoli");
        otherLink.Text = T("Use an HDMI capture card", "Mam kartę przechwytującą");
        help.Text = T("Read the guide online", "Instrukcja w przeglądarce");
        back.Text = T("Back", "Wstecz");
        next.Text = step == 1 ? T("Enabled. Continue", "Włączone. Dalej")
                              : T("I can see the game on PC", "Widzę grę na komputerze");
        skip.Text = play ? T("Find another window", "Znajdź inne okno")
                         : T("My stream is already open", "Mam już otwartą grę na PC");
        // The last step can fall back to an unusual or localized window title.
        if (play)
        {
            skip.Visible = route != 4 && mode.SelectedIndex != 3;
        }
        ResumeLayout();
    }
    sealed class HardwareButton : Button
    {
        readonly string device;
        readonly Color tone;
        readonly Image photo;
        public string Prompt = "";
        public HardwareButton(string title, Color bg, Image image)
        {
            device = title;
            tone = bg;
            photo = image;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            UseVisualStyleBackColor = false;
            AccessibleRole = AccessibleRole.PushButton;
            DoubleBuffered = true;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(tone);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            int ph = Height - 15, pw = ph * photo.Width / photo.Height;
            g.DrawImage(photo, new Rectangle(Width - pw - 5, 8, pw, ph));
            Color ink = Color.FromArgb(22, 25, 27);
            using (var f = new Font("Segoe UI", 20, FontStyle.Bold))
                TextRenderer.DrawText(g, device, f, new Point(16, 20), ink, TextFormatFlags.NoPadding);
            using (var f = new Font("Segoe UI", 10, FontStyle.Bold))
                TextRenderer.DrawText(g, Prompt + "  ›", f, new Point(17, Height - 39), ink, TextFormatFlags.NoPadding);
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(g, new Rectangle(4, 4, Width - 8, Height - 8), ink, tone);
        }
    }
}
