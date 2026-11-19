using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// The setup screens can be driven with a controller, like a console menu.
// Only while this window is active and no game is running, so the pad never reaches NAIMAN during play.
partial class Launcher
{
    [DllImport("xinput1_4.dll")]
    static extern int XInputGetState(int index, out PadState state);
    [StructLayout(LayoutKind.Sequential)]
    struct PadState
    {
        public uint Packet;
        public ushort Buttons;
        public byte LeftTrigger, RightTrigger;
        public short LX, LY, RX, RY;
    }
    const int GoUp = 1, GoDown = 2, GoLeft = 4, GoRight = 8, A = 0x1000, B = 0x2000;
    readonly Timer padTimer = new Timer();
    int padHeld = -1, padMask, padTick;
    bool padSeen;

    void StartPad()
    {
        if (staged || checking)
            return;
        padTimer.Interval = 70;
        padTimer.Tick += delegate
        {
            try
            {
                PollPad();
            }
            catch (DllNotFoundException)
            {
                padTimer.Stop();
            }
            catch (EntryPointNotFoundException)
            {
                padTimer.Stop();
            }
        };
        padTimer.Start();
    }
    void PollPad()
    {
        // Asking about an empty slot is slow, so empty slots are checked only every two seconds.
        bool sweep = padTick++ % 30 == 0;
        int now = 0, mask = 0;
        for (int i = 0; i < 4; i++)
        {
            if (!sweep && (padMask & 1 << i) == 0)
                continue;
            PadState s;
            if (XInputGetState(i, out s) != 0)
                continue;
            mask |= 1 << i;
            now |= s.Buttons & (GoUp | GoDown | GoLeft | GoRight | A | B);
            if (s.LY > 20000) now |= GoUp;
            if (s.LY < -20000) now |= GoDown;
            if (s.LX < -20000) now |= GoLeft;
            if (s.LX > 20000) now |= GoRight;
        }
        padMask = mask;
        if (padSeen != (mask != 0))
        {
            padSeen = mask != 0;
            Invalidate();
        }
        if (engine != null || Form.ActiveForm != this)
        {
            padHeld = -1;
            return;
        }
        int pressed = now & ~padHeld;
        padHeld = now;
        if (pressed == 0)
            return;
        if (!Ui.Cues)
        {
            Ui.Cues = true;
            Invalidate(true);
        }
        var focus = ActiveControl;
        if ((pressed & (GoUp | GoDown)) != 0 && focus == list && list.Step((pressed & GoUp) != 0 ? -1 : 1))
            return;
        if ((pressed & (GoUp | GoLeft)) != 0)
            SelectNextControl(focus, false, true, true, true);
        else if ((pressed & (GoDown | GoRight)) != 0)
            SelectNextControl(focus, true, true, true, true);
        else if ((pressed & A) != 0)
        {
            var button = focus as Button;
            if (button != null && button.Enabled)
                button.PerformClick();
            else if (focus == list && phase == 0 && list.Selected != null)
                Launch();
        }
        else if ((pressed & B) != 0 && (step > 0 || phase != 0))
            Back();
    }
}
