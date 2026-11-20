using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

partial class Launcher
{
    const int CameraKey = 0x4650;
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hwnd, int id, uint mods, uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    bool cameraKeyRegistered;
    CameraGuide cameraGuide;

    void CloseCameraGuide()
    {
        if (cameraGuide != null && !cameraGuide.IsDisposed)
            cameraGuide.Close();
        cameraGuide = null;
    }

    void EndCameraHelp()
    {
        if (cameraKeyRegistered)
            UnregisterHotKey(Handle, CameraKey);
        cameraKeyRegistered = false;
        CloseCameraGuide();
    }

    void ToggleCameraGuide()
    {
        if (cameraGuide != null && !cameraGuide.IsDisposed)
        {
            CloseCameraGuide();
            return;
        }
        var pick = engine != null ? lastItem : step == 3 && list.Selected != null ? list.Selected.Tag as WindowList.Item : null;
        int console = step == 0 ? -1 : pick != null && (pick.Kind == 0 || pick.Kind == 1) ? pick.Kind : route;
        var monitor = Screen.FromHandle(pick == null || pick.Handle == IntPtr.Zero ? Handle : pick.Handle);
        cameraGuide = new CameraGuide(pl, console, monitor.WorkingArea);
        cameraGuide.Show();
    }

    protected override void WndProc(ref Message msg)
    {
        if (msg.Msg == 0x0312 && msg.WParam.ToInt32() == CameraKey && cameraKeyRegistered)
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == Handle || (lastItem != null && fg == lastItem.Handle) ||
                (lastDirect && engine != null && !engine.HasExited && fg == engine.MainWindowHandle))
                ToggleCameraGuide();
            return;
        }
        base.WndProc(ref msg);
    }
}

// This is a reference card for the physical controller. It never generates input.
sealed class CameraGuide : Form
{
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int w, int h, uint flags);
    public void RaiseWithoutFocus()
    {
        SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0013); // TOPMOST, NOMOVE, NOSIZE, NOACTIVATE
    }

    readonly int inset = Ui.S(16);
    int y;
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000 | 0x80; // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
            return cp;
        }
    }

    public CameraGuide(bool pl, int console, Rectangle area)
    {
        Text = pl ? "GTA 6 - Kamera FPP" : "GTA 6 - FPP camera";
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.None;
        AutoScaleMode = AutoScaleMode.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Ui.Surface;
        int width = Math.Min(Ui.S(370), area.Width - 2 * inset);
        ClientSize = new Size(width, 1);
        y = inset;
        Line(Text, Ui.Display(21), Ui.Ink);
        Line(console == 2 ? (pl ? "Przy HDMI pad zostaje przy konsoli." : "With HDMI, leave the controller on the console.")
                          : (pl ? "Użyj pada, którym grasz w GTA 6." : "Use the controller you play GTA 6 with."), Ui.Sans(14), Ui.Muted);
        Line(pl ? "1. Ustaw przycisk kamery" : "1. Set the camera button", Ui.Bold(16), Ui.Ink);
        string cameraButton = console == 0 ? "View" : console == 1 ? "touchpad" : "View (Xbox) / touchpad (PS5)";
        Line((pl ? "Pauza > Ustawienia > Kontroler > Funkcje kontrolera. " : "Pause > Settings > Controller > Controller Features. ") +
             cameraButton + (pl ? " = Kamera (2/2)." : " = Camera (2/2)."), Ui.Sans(15), Ui.Ink);
        Line(pl ? "2. Wsiądź do auta jako kierowca" : "2. Sit in the driver's seat", Ui.Bold(16), Ui.Ink);
        Line(pl ? "Zatrzymaj auto. Naciśnij górę krzyżaka, żeby wyjąć telefon." : "Stop the car. Press D-pad Up to bring up the phone.", Ui.Sans(15), Ui.Ink);
        Line(pl ? "3. Schowaj telefon i od razu zmień kamerę" : "3. Put the phone away and immediately switch camera", Ui.Bold(16), Ui.Ink);
        if (console != 1)
            Line(pl ? "Xbox: naciśnij B, potem szybko View (dwa prostokąty)." : "Xbox: press B, then quickly tap View (two rectangles).", Ui.Sans(15), Ui.Ink);
        if (console != 0)
            Line(pl ? "PS5: naciśnij kółko, potem szybko kliknij touchpad." : "PS5: press Circle, then quickly click the touchpad.", Ui.Sans(15), Ui.Ink);
        Line(pl ? "Wysiądź i sprawdź widok. Jeśli kamera wróci za postać, powtórz kroki 2 i 3. To trik zależny od wersji gry."
                : "Get out and check the view. If the camera resets, repeat steps 2 and 3. This trick depends on the game version.", Ui.Sans(14), Ui.Muted);
        var close = new Btn(Btn.Outline) { Label = pl ? "Zamknij ściągę" : "Close guide" };
        close.SetBounds(inset, y, width - 2 * inset, Ui.S(35));
        close.Click += delegate { Close(); };
        Controls.Add(close);
        ClientSize = new Size(width, close.Bottom + inset);
        Location = new Point(area.Right - width - inset, area.Top + inset);
    }

    void Line(string text, Font font, Color color)
    {
        var label = new Label { Text = text, Font = font, ForeColor = color, UseCompatibleTextRendering = true,
                                UseMnemonic = false, AutoSize = true, MaximumSize = new Size(ClientSize.Width - 2 * inset, 0),
                                Location = new Point(inset, y) };
        Controls.Add(label);
        y = label.Bottom + Ui.S(6);
    }
}
