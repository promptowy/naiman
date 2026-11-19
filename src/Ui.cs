using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Palette, type and the wordmark. Everything on screen is drawn from here.
static class Ui
{
    public static readonly Color Bg = Hex(0x0C0D0F), Surface = Hex(0x141619), Raised = Hex(0x1C1F23),
                                 Line = Hex(0x2C3035), Ink = Hex(0xF5F2EB), Muted = Hex(0xA2A6A8),
                                 Dim = Hex(0x6B7074), Action = Hex(0xFF5A1F), ActionHot = Hex(0xFF7B47),
                                 OnAction = Hex(0x140904), Dusk = Hex(0xFF2D78), Noon = Hex(0xFFB627);
    // Set once a controller or the keyboard is used, so focus is always visible from then on.
    public static bool Cues;
    public static float K = 1f;
    static readonly PrivateFontCollection faces = new PrivateFontCollection();
    static readonly Dictionary<string, Font> cache = new Dictionary<string, Font>();
    static FontFamily display, sans, bold;
    public static readonly StringFormat Flow = Typographic(false), OneLine = Typographic(true);

    static Color Hex(int rgb)
    {
        return Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
    }
    static StringFormat Typographic(bool single)
    {
        var f = new StringFormat(StringFormat.GenericTypographic);
        f.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
        if (single)
        {
            f.FormatFlags |= StringFormatFlags.NoWrap;
            f.Trimming = StringTrimming.EllipsisCharacter;
        }
        return f;
    }
    public static int S(float v)
    {
        return (int)Math.Round(v * K);
    }
    static FontFamily Load(string resource, string family)
    {
        using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
        {
            var bytes = new byte[s.Length];
            s.Read(bytes, 0, bytes.Length);
            // GDI+ keeps reading this block for the life of the process.
            IntPtr block = Marshal.AllocCoTaskMem(bytes.Length);
            Marshal.Copy(bytes, 0, block, bytes.Length);
            faces.AddMemoryFont(block, bytes.Length);
        }
        foreach (var f in faces.Families)
            if (f.Name == family)
                return f;
        return FontFamily.GenericSansSerif;
    }
    static Font Face(ref FontFamily family, string resource, string name, float px)
    {
        if (family == null)
            family = Load(resource, name);
        string key = name + "|" + px.ToString("0.00") + "|" + K.ToString("0.000");
        Font font;
        if (!cache.TryGetValue(key, out font))
            cache[key] = font = new Font(family, Math.Max(1f, px * K), FontStyle.Regular, GraphicsUnit.Pixel);
        return font;
    }
    public static Font Display(float px)
    {
        return Face(ref display, "naiman-display.ttf", "NAIMAN Display", px);
    }
    public static Font Sans(float px)
    {
        return Face(ref sans, "naiman-sans.ttf", "NAIMAN Sans", px);
    }
    public static Font Bold(float px)
    {
        return Face(ref bold, "naiman-sans-bold.ttf", "NAIMAN Sans Bold", px);
    }
    public static void Quality(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    }
    public static SizeF Measure(Graphics g, string text, Font font, int width)
    {
        return g.MeasureString(text, font, width <= 0 ? 100000 : width, width <= 0 ? OneLine : Flow);
    }
    public static void Text(Graphics g, string text, Font font, Color color, float x, float y)
    {
        using (var b = new SolidBrush(color))
            g.DrawString(text, font, b, x, y, OneLine);
    }
    public static void Clip(Graphics g, string text, Font font, Color color, RectangleF box)
    {
        using (var b = new SolidBrush(color))
            g.DrawString(text, font, b, box, OneLine);
    }
    public static void Fill(Graphics g, Color c, float x, float y, float w, float h)
    {
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.None;
        using (var b = new SolidBrush(c))
            g.FillRectangle(b, x, y, w, h);
        g.SmoothingMode = old;
    }
    public static void Frame(Graphics g, Color c, int x, int y, int w, int h, int t)
    {
        Fill(g, c, x, y, w, t);
        Fill(g, c, x, y + h - t, w, t);
        Fill(g, c, x, y, t, h);
        Fill(g, c, x + w - t, y, t, h);
    }

    // The wordmark is drawn, not typed: six cut letters on a 100-unit cap height.
    // The corner cut from the I is the one mark of colour: a ring-corner pad.
    static readonly float[][] N = { new float[] { 0, 100, 0, 0, 27, 0, 58, 57, 58, 0, 84, 0, 84, 100, 57, 100, 26, 43, 26, 100 } };
    static readonly float[][] A = { new float[] { 0, 100, 33, 0, 61, 0, 94, 100, 66, 100, 47, 38, 28, 100 } };
    static readonly float[][] I = { new float[] { 26, 6, 26, 100, 0, 100, 0, 32 } };
    static readonly float[][] M = { new float[] { 0, 100, 0, 0, 28, 0, 55, 52, 82, 0, 110, 0, 110, 100, 84, 100, 84, 44,
                                                  65, 82, 45, 82, 26, 44, 26, 100 } };
    static readonly float[] Pad = { 0, 0, 21, 0, 0, 21 };
    public const float LogoUnits = 532f;
    public static float Logo(Graphics g, float x, float y, float height, Color ink, Color mark)
    {
        float u = height / 100f, pen = 0;
        var glyphs = new[] { N, A, I, M, A, N };
        var advance = new float[] { 84 + 5, 94 + 9, 26 + 14, 110 + 5, 94 + 5, 84 };
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var b = new SolidBrush(ink)) using (var m = new SolidBrush(mark))
        {
            for (int i = 0; i < glyphs.Length; i++)
            {
                foreach (var poly in glyphs[i])
                    g.FillPolygon(b, Points(poly, x + pen * u, y, u));
                if (glyphs[i] == I)
                    g.FillPolygon(m, Points(Pad, x + pen * u, y, u));
                pen += advance[i];
            }
        }
        g.SmoothingMode = old;
        return LogoUnits * u;
    }
    static PointF[] Points(float[] v, float x, float y, float u)
    {
        var p = new PointF[v.Length / 2];
        for (int i = 0; i < p.Length; i++)
            p[i] = new PointF(x + v[i * 2] * u, y + v[i * 2 + 1] * u);
        return p;
    }
    // A low sun cut by horizon bands: the one picture in the app, in Vice-coast colours.
    public static void Sun(Graphics g, float cx, float ground, float r, float drift)
    {
        var state = g.Save();
        g.SetClip(new RectangleF(cx - r, ground - r, r * 2, r));
        using (var disc = new LinearGradientBrush(new PointF(0, ground - r - 1), new PointF(0, ground + 1), Noon, Dusk))
        {
            disc.InterpolationColors = new ColorBlend { Colors = new[] { Noon, Action, Dusk }, Positions = new[] { 0f, .55f, 1f } };
            g.FillEllipse(disc, cx - r, ground - r, r * 2, r * 2);
        }
        // Bands are born thin high on the disc and slide down, thickening, into the horizon.
        for (int k = 0; k < 7; k++)
        {
            float q = (k + drift) % 7f / 7f, thick = r * (.012f + .07f * q);
            Fill(g, Bg, cx - r, ground - r * .64f * (float)Math.Pow(1 - q, 1.5) - thick, r * 2, thick);
        }
        g.Restore(state);
    }
    // A controller face button: a ring with its letter.
    public static float PadKey(Graphics g, string legend, float x, float y)
    {
        var f = Bold(12.5f);
        float d = S(26);
        using (var pen = new Pen(Muted, Math.Max(1f, S(1.5f))))
            g.DrawEllipse(pen, x + 1, y + 1, d - 2, d - 2);
        Text(g, legend, f, Ink, x + (d - Measure(g, legend, f, 0).Width) / 2f, y + S(4.5f));
        return d;
    }
    public static void Corner(Graphics g, int size)
    {
        using (var b = new SolidBrush(Action))
            g.FillPolygon(b, new[] { new Point(0, 0), new Point(size, 0), new Point(0, size) });
    }
    // A key on the keyboard: outlined cap with its legend.
    public static float Key(Graphics g, string legend, float x, float y)
    {
        var f = Bold(12.5f);
        float w = Math.Max(S(30), Measure(g, legend, f, 0).Width + S(16)), h = S(26);
        Fill(g, Raised, x, y, w, h);
        Frame(g, Line, (int)x, (int)y, (int)w, (int)h, 1);
        Text(g, legend, f, Ink, x + (w - Measure(g, legend, f, 0).Width) / 2f, y + S(4.5f));
        return w;
    }
}

// One button class in five cuts. No system chrome is left to show through.
sealed class Btn : Button
{
    public const int Primary = 0, Outline = 1, Link = 2, Choice = 3, Segment = 4;
    public int Kind;
    public string Sub = "", Tail = "→";
    public bool On, Last;
    bool hot, down;
    public Btn(int kind)
    {
        Kind = kind;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        BackColor = Ui.Bg;
        UseVisualStyleBackColor = false;
        AccessibleRole = AccessibleRole.PushButton;
    }
    public string Label
    {
        get { return Text; }
        set
        {
            if (Text != value)
            {
                Text = value;
                AccessibleName = value;
                Invalidate();
            }
        }
    }
    protected override void OnMouseEnter(EventArgs e)
    {
        hot = true;
        Invalidate();
        base.OnMouseEnter(e);
    }
    protected override void OnMouseLeave(EventArgs e)
    {
        hot = down = false;
        Invalidate();
        base.OnMouseLeave(e);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        down = true;
        Invalidate();
        base.OnMouseDown(e);
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        down = false;
        Invalidate();
        base.OnMouseUp(e);
    }
    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }
    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }
    protected override void OnLostFocus(EventArgs e)
    {
        Invalidate();
        base.OnLostFocus(e);
    }
    public int Fit(Graphics g)
    {
        return (int)Ui.Measure(g, Text, Kind == Link ? Ui.Sans(15) : Ui.Bold(15), 0).Width + Ui.S(Kind == Link ? 30 : 44);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Ui.Quality(g);
        int w = Width, h = Height;
        bool keyed = Focused && (ShowFocusCues || Ui.Cues), lit = hot || keyed;
        g.Clear(Parent == null ? Ui.Bg : Parent.BackColor);
        if (Kind == Primary)
        {
            Color fill = !Enabled ? Ui.Raised : down ? Ui.Ink : lit ? Ui.ActionHot : Ui.Action;
            Color ink = Enabled ? Ui.OnAction : Ui.Dim;
            Ui.Fill(g, fill, 0, 0, w, h);
            var f = Ui.Bold(16);
            float ty = (h - f.GetHeight(g)) / 2f;
            float tw = Tail.Length == 0 ? 0 : Ui.Measure(g, Tail, f, 0).Width;
            Ui.Clip(g, Text, f, ink, new RectangleF(Ui.S(20), ty, w - Ui.S(52) - tw, f.GetHeight(g) + 2));
            if (Tail.Length > 0)
                Ui.Text(g, Tail, f, ink, w - Ui.S(20) - tw, ty);
        }
        else if (Kind == Outline)
        {
            Color ink = !Enabled ? Ui.Dim : Ui.Ink;
            Ui.Fill(g, down ? Ui.Line : lit && Enabled ? Ui.Raised : Ui.Bg, 0, 0, w, h);
            Ui.Frame(g, lit && Enabled ? Ui.Ink : Ui.Line, 0, 0, w, h, 1);
            var f = Ui.Bold(15);
            float ty = (h - f.GetHeight(g)) / 2f;
            if (Sub.Length > 0)
            {
                // Wide form: label on the left, arrow on the right.
                float tw = Ui.Measure(g, Sub, f, 0).Width;
                Ui.Clip(g, Text, f, ink, new RectangleF(Ui.S(18), ty, w - Ui.S(48) - tw, f.GetHeight(g) + 2));
                Ui.Text(g, Sub, f, Enabled ? Ui.Action : Ui.Dim, w - Ui.S(18) - tw, ty);
            }
            else
            {
                float tw = Math.Min(w - Ui.S(16), Ui.Measure(g, Text, f, 0).Width);
                Ui.Clip(g, Text, f, ink, new RectangleF((w - tw) / 2f, ty, tw + 2, f.GetHeight(g) + 2));
            }
        }
        else if (Kind == Link)
        {
            var f = Ui.Sans(15);
            Color ink = lit ? Ui.Ink : Ui.Muted;
            float ty = (h - f.GetHeight(g)) / 2f, tw = Ui.Measure(g, Text, f, 0).Width;
            Ui.Text(g, Text, f, ink, 0, ty);
            Ui.Fill(g, lit ? Ui.Action : Ui.Line, 0, ty + f.GetHeight(g) + Ui.S(1), tw, Math.Max(1, Ui.S(1)));
            if (Tail.Length > 0)
                Ui.Text(g, Tail, f, Ui.Action, tw + Ui.S(8), ty);
        }
        else if (Kind == Choice)
        {
            Color fill = down ? Ui.Ink : lit ? Ui.Action : Ui.Bg, ink = lit || down ? Ui.OnAction : Ui.Ink,
                  soft = lit || down ? Ui.OnAction : Ui.Muted;
            Ui.Fill(g, fill, 0, 0, w, h);
            if (!lit && !down)
            {
                Ui.Fill(g, Ui.Line, 0, 0, w, 1);
                if (Last)
                    Ui.Fill(g, Ui.Line, 0, h - 1, w, 1);
            }
            var title = Ui.Display(27);
            var sub = Ui.Sans(15);
            float block = title.GetHeight(g) + Ui.S(4) + sub.GetHeight(g), ty = (h - block) / 2f;
            int pad = Ui.S(lit || down ? 22 : 0);
            Ui.Text(g, Text, title, ink, pad, ty);
            Ui.Clip(g, Sub, sub, soft, new RectangleF(pad + Ui.S(1), ty + title.GetHeight(g) + Ui.S(4), w - pad - Ui.S(70), sub.GetHeight(g) + 2));
            var arrow = Ui.Display(26);
            float aw = Ui.Measure(g, "→", arrow, 0).Width;
            Ui.Text(g, "→", arrow, lit || down ? Ui.OnAction : Ui.Action, w - aw - Ui.S(lit || down ? 22 : 4), (h - arrow.GetHeight(g)) / 2f);
        }
        else
        {
            var f = Ui.Bold(13);
            Ui.Fill(g, On ? Ui.Ink : lit ? Ui.Raised : Ui.Bg, 0, 0, w, h);
            if (!On)
                Ui.Frame(g, Ui.Line, 0, 0, w, h, 1);
            float tw = Ui.Measure(g, Text, f, 0).Width;
            Ui.Text(g, Text, f, On ? Ui.Bg : Ui.Muted, (w - tw) / 2f, (h - f.GetHeight(g)) / 2f);
        }
        if (keyed && Kind != Choice)
            Ui.Frame(g, Kind == Primary ? Ui.OnAction : Ui.Ink, Ui.S(3), Ui.S(3), w - Ui.S(6), h - Ui.S(6), Math.Max(1, Ui.S(2)));
    }
}

// The list of windows or capture devices. Arrow keys move, Enter starts.
sealed class Rows : Control
{
    public sealed class Item
    {
        public string Title = "", Sub = "", Badge = "";
        public object Tag;
    }
    readonly List<Item> items = new List<Item>();
    int selected = -1, top, hover = -1;
    public event EventHandler Picked, Confirmed;
    public Rows()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = true;
        BackColor = Ui.Bg;
        AccessibleRole = AccessibleRole.List;
    }
    public int Count { get { return items.Count; } }
    public List<Item> All { get { return items; } }
    public int RowHeight { get { return Ui.S(60); } }
    public Item Selected { get { return selected >= 0 && selected < items.Count ? items[selected] : null; } }
    public bool SetItems(List<Item> next, Func<object, object, bool> same)
    {
        bool equal = next.Count == items.Count;
        for (int i = 0; equal && i < next.Count; i++)
            equal = same(next[i].Tag, items[i].Tag) && next[i].Title == items[i].Title && next[i].Sub == items[i].Sub;
        if (equal)
        {
            for (int i = 0; i < next.Count; i++)
                items[i].Tag = next[i].Tag;
            return false;
        }
        var before = Selected;
        items.Clear();
        items.AddRange(next);
        selected = before == null ? -1 : items.FindIndex(v => same(v.Tag, before.Tag));
        if (selected < 0 && items.Count == 1)
            selected = 0;
        top = Math.Max(0, Math.Min(top, items.Count - Visible_()));
        Invalidate();
        if (Picked != null)
            Picked(this, EventArgs.Empty);
        return true;
    }
    public void Select(int index)
    {
        if (index < 0 || index >= items.Count || index == selected)
            return;
        selected = index;
        int rows = Visible_();
        if (selected < top)
            top = selected;
        if (selected >= top + rows)
            top = selected - rows + 1;
        Invalidate();
        if (Picked != null)
            Picked(this, EventArgs.Empty);
    }
    public bool Step(int by)
    {
        int next = Math.Max(0, Math.Min(items.Count - 1, selected + by));
        if (next == selected)
            return false;
        Select(next);
        return true;
    }
    public int Find(Predicate<Item> match)
    {
        return items.FindIndex(match);
    }
    int Visible_()
    {
        return Math.Max(1, Height / RowHeight);
    }
    protected override bool IsInputKey(Keys key)
    {
        return key == Keys.Up || key == Keys.Down || base.IsInputKey(key);
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Down)
            Select(Math.Min(items.Count - 1, selected + 1));
        else if (e.KeyCode == Keys.Up)
            Select(Math.Max(0, selected - 1));
        else if (e.KeyCode == Keys.Enter && Selected != null && Confirmed != null)
            Confirmed(this, EventArgs.Empty);
        base.OnKeyDown(e);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        Select(top + e.Y / RowHeight);
        base.OnMouseDown(e);
    }
    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        if (Selected != null && Confirmed != null)
            Confirmed(this, EventArgs.Empty);
        base.OnMouseDoubleClick(e);
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        int row = top + e.Y / RowHeight;
        if (row != hover)
        {
            hover = row;
            Invalidate();
        }
        base.OnMouseMove(e);
    }
    protected override void OnMouseLeave(EventArgs e)
    {
        hover = -1;
        Invalidate();
        base.OnMouseLeave(e);
    }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        top = Math.Max(0, Math.Min(items.Count - Visible_(), top + (e.Delta < 0 ? 1 : -1)));
        Invalidate();
        base.OnMouseWheel(e);
    }
    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }
    protected override void OnLostFocus(EventArgs e)
    {
        Invalidate();
        base.OnLostFocus(e);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Ui.Quality(g);
        g.Clear(Ui.Bg);
        int rh = RowHeight, w = Width, rows = Visible_();
        var title = Ui.Bold(15.5f);
        var sub = Ui.Sans(13.5f);
        for (int i = top; i < items.Count && i < top + rows; i++)
        {
            int y = (i - top) * rh;
            bool on = i == selected;
            Ui.Fill(g, on ? Ui.Raised : i == hover ? Ui.Surface : Ui.Bg, 0, y, w, rh - 1);
            Ui.Fill(g, Ui.Line, 0, y + rh - 1, w, 1);
            if (on)
                Ui.Fill(g, Ui.Action, 0, y, Ui.S(4), rh - 1);
            float bw = items[i].Badge.Length == 0 ? 0 : Ui.Measure(g, items[i].Badge, Ui.Bold(12), 0).Width + Ui.S(26);
            Ui.Clip(g, items[i].Title, title, on ? Ui.Ink : Ui.Muted, new RectangleF(Ui.S(18), y + Ui.S(10), w - Ui.S(30) - bw, title.GetHeight(g) + 2));
            Ui.Clip(g, items[i].Sub, sub, on ? Ui.Muted : Ui.Dim, new RectangleF(Ui.S(18), y + Ui.S(32), w - Ui.S(30) - bw, sub.GetHeight(g) + 2));
            if (bw > 0)
                Ui.Text(g, items[i].Badge, Ui.Bold(12), on ? Ui.Action : Ui.Dim, w - bw + Ui.S(12), y + Ui.S(22));
        }
        if (items.Count > rows)
        {
            // A plain position marker; the wheel and the arrow keys scroll.
            float track = Height, thumb = track * rows / items.Count;
            Ui.Fill(g, Ui.Dim, w - Ui.S(3), track * top / items.Count, Ui.S(3), thumb);
        }
        if (Focused && (ShowFocusCues || Ui.Cues))
            Ui.Frame(g, Ui.Ink, 0, 0, w, Math.Min(Height, Math.Min(items.Count, rows) * rh), 1);
    }
}

sealed class MenuColors : ProfessionalColorTable
{
    public override Color MenuItemSelected { get { return Ui.Action; } }
    public override Color MenuItemBorder { get { return Ui.Action; } }
    public override Color MenuBorder { get { return Ui.Line; } }
    public override Color ToolStripDropDownBackground { get { return Ui.Surface; } }
    public override Color ImageMarginGradientBegin { get { return Ui.Surface; } }
    public override Color ImageMarginGradientMiddle { get { return Ui.Surface; } }
    public override Color ImageMarginGradientEnd { get { return Ui.Surface; } }
}
