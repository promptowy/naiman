using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

// NAIMAN.exe --shots <folder> [scale] renders every screen to PNG without showing a window.
// The lists hold sample entries; layout, type and controls are the ones the app really draws.
partial class Launcher
{
    Bitmap Snapshot()
    {
        Compose();
        var bmp = new Bitmap(ClientSize.Width, ClientSize.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Ui.Bg);
            G = g;
            Ui.Quality(g);
            painting = true;
            Draw();
            painting = false;
            G = null;
        }
        foreach (Control c in Controls)
            if (placed.Contains(c))
                c.DrawToBitmap(bmp, c.Bounds);
        return bmp;
    }
    void Sample(int kind, params string[] titles)
    {
        var rows = new List<Rows.Item>();
        for (int i = 0; i < titles.Length; i++)
        {
            bool game = i == 0 && kind >= 0;
            rows.Add(Row(new WindowList.Item { Handle = new IntPtr(100 + i), Pid = 100 + i, Name = titles[i],
                                                Kind = game ? kind : -1,
                                                ProcessName = game ? (kind == 1 ? "remoteplay" : kind == 4 ? "NAIMAN" : "msedge")
                                                              : i == 1 ? "chrome" : i == 2 ? "Discord" : "explorer" }));
        }
        list.SetItems(new List<Rows.Item>(), SameWindow);
        list.SetItems(rows, SameWindow);
        if (titles.Length > 0)
            list.Select(0);
    }
    void Save(string dir, string name)
    {
        using (var bmp = Snapshot())
            bmp.Save(Path.Combine(dir, name + "-" + (pl ? "pl" : "en") + ".png"), ImageFormat.Png);
    }
    public static void Shots(string dir, float scale)
    {
        Directory.CreateDirectory(dir);
        foreach (bool polish in new[] { true, false })
        {
            using (var app = new Launcher(null, scale))
            {
                app.pl = polish;
                app.CreateControl();
                string game = polish ? "Xbox Remote Play | Xbox.com – Microsoft Edge" : "Xbox Remote Play | Xbox.com - Microsoft Edge";
                app.step = 0;
                app.Save(dir, "01-start");
                app.route = app.mode = 0;
                app.step = 1;
                app.Save(dir, "02-xbox-konsola");
                app.step = 2;
                app.Save(dir, "03-xbox-edge");
                app.opened = true;
                app.Save(dir, "03b-xbox-edge-otwarte");
                app.step = 3;
                app.Sample(0, game);
                app.Save(dir, "04-xbox-okno");
                app.opened = false;
                app.route = app.mode = 1;
                app.step = 1;
                app.Save(dir, "05-ps5-konsola");
                app.step = 2;
                app.Save(dir, "06-ps5-komputer");
                app.step = 3;
                app.Sample(1, "PS Remote Play");
                app.Save(dir, "04b-ps5-okno");
                app.route = app.mode = 3;
                app.step = 3;
                app.Sample(0, game, "YouTube - Google Chrome", "Discord", polish ? "Pobrane" : "Downloads");
                app.Save(dir, "07-skrot-kilka-okien");
                app.Sample(-1);
                app.Save(dir, "08-brak-okna");
                app.route = app.mode = 2;
                app.list.SetItems(new List<Rows.Item> { new Rows.Item { Title = "Game Capture HD60 X", Sub = app.T("Video capture device", "Urządzenie wideo"),
                                                                         Tag = new CaptureDevices.Item { Id = "x", Name = "Game Capture HD60 X" } } }, SameDevice);
                app.audioPick = new CaptureDevices.Item { Id = "", Name = app.T("from the console or headset", "z konsoli albo słuchawek") };
                app.Save(dir, "09-hdmi");
                app.route = app.mode = 0;
                app.Sample(0, game);
                app.lastItem = (WindowList.Item)app.list.Selected.Tag;
                app.phase = 1;
                app.Save(dir, "10-sesja-start");
                app.phase = 2;
                app.fpsSource = 30;
                app.fpsGenerated = 30;
                app.Save(dir, "11-sesja-trwa");
                app.phase = 3;
                app.fpsGenerated = 0;
                app.fpsSource = 0;
                app.Save(dir, "12-sesja-fokus");
                app.phase = 5;
                app.totalSource = 54212;
                app.totalGenerated = 54180;
                app.played = TimeSpan.FromSeconds(1807);
                app.Sample(-1);
                app.Save(dir, "13-brak-zrodla");
                app.Sample(0, game);
                app.phase = 7;
                app.Save(dir, "14-zrodlo-wrocilo");
                app.phase = 4;
                app.Save(dir, "15-koniec");
                app.route = app.mode = 4;
                app.phase = 0;
                app.step = 3;
                app.Sample(4, "NAIMAN Motion Test");
                app.Save(dir, "16-test-ruchu");
            }
        }
    }
}
