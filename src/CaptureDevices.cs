using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Forms;

static class CaptureDevices
{
    public static readonly Guid Video = new Guid("860bb310-5d01-11d0-bd3b-00a0c911ce86");
    public static readonly Guid Audio = new Guid("33d9a762-90c8-11d0-bd43-00a0c911ce86");
    [ComImport, Guid("29840822-5b84-11d0-bd3b-00a0c911ce86"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface DeviceEnumerator
    {
        [PreserveSig] int CreateClassEnumerator(ref Guid category, out IEnumMoniker result, int flags);
    }
    [ComImport, Guid("55272a00-42cb-11ce-8135-00aa004bb851"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface Properties
    {
        [PreserveSig] int Read([MarshalAs(UnmanagedType.LPWStr)] string key, [MarshalAs(UnmanagedType.Struct)] out object value, IntPtr log);
        [PreserveSig] int Write([MarshalAs(UnmanagedType.LPWStr)] string key, [MarshalAs(UnmanagedType.Struct)] ref object value);
    }
    public sealed class Item
    {
        public string Id, Name;
        public override string ToString() { return Name; }
    }
    public static List<Item> List(Guid category)
    {
        var result = new List<Item>();
        var enumerator = (DeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("62be5d10-60eb-11d0-bd3b-00a0c911ce86")));
        IEnumMoniker list = null;
        try
        {
            if (enumerator.CreateClassEnumerator(ref category, out list, 0) != 0 || list == null) return result;
            var next = new IMoniker[1];
            while (list.Next(1, next, IntPtr.Zero) == 0)
            {
                object bag = null;
                try
                {
                    string id;
                    next[0].GetDisplayName(null, null, out id);
                    var iid = typeof(Properties).GUID;
                    next[0].BindToStorage(null, null, ref iid, out bag);
                    object label;
                    if (((Properties)bag).Read("FriendlyName", out label, IntPtr.Zero) == 0)
                        result.Add(new Item { Id = id, Name = Convert.ToString(label) });
                }
                catch (COMException) { /* Removed between enumeration and descriptor lookup. */ }
                finally
                {
                    if (bag != null) Marshal.ReleaseComObject(bag);
                    Marshal.ReleaseComObject(next[0]);
                }
            }
        }
        finally
        {
            if (list != null) Marshal.ReleaseComObject(list);
            Marshal.ReleaseComObject(enumerator);
        }
        return result;
    }

    // Standard Windows command-line quoting for DirectShow monikers, including USB path separators.
    public static string Argument(string value)
    {
        var s = new System.Text.StringBuilder("\"");
        int slashes = 0;
        foreach (char c in value)
        {
            if (c == '\\') { slashes++; continue; }
            s.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
            s.Append(c);
            slashes = 0;
        }
        return s.Append('\\', slashes * 2).Append('"').ToString();
    }
}
partial class Launcher
{
    List<CaptureDevices.Item> audioInputs = new List<CaptureDevices.Item>();
    CaptureDevices.Item audioPick;
    bool DirectCapture { get { return built && step == 3 && route == 2 && !obsFallback; } }
    static bool SameDevice(object a, object b)
    {
        var x = a as CaptureDevices.Item;
        var y = b as CaptureDevices.Item;
        return x != null && y != null && x.Id == y.Id;
    }
    // Returns true when the picture or sound choices changed and the screen needs composing again.
    bool ScanDevices()
    {
        scanAt = DateTime.UtcNow;
        try
        {
            var video = CaptureDevices.List(CaptureDevices.Video);
            bool changed = list.SetItems(
                video.Select(v => new Rows.Item { Title = v.Name, Sub = T("Video capture device", "Urządzenie wideo"), Tag = v })
                    .ToList(),
                SameDevice);
            var inputs = CaptureDevices.List(CaptureDevices.Audio);
            inputs.Insert(0, new CaptureDevices.Item { Id = "", Name = T("from the console or headset", "z konsoli albo słuchawek") });
            var kept = audioPick == null ? null : inputs.FirstOrDefault(v => v.Id == audioPick.Id);
            changed |= audioInputs.Count != inputs.Count || kept == null || kept.Name != audioPick.Name;
            audioInputs = inputs;
            audioPick = kept ?? inputs[0];
            return changed;
        }
        catch (COMException e)
        {
            Fail(T("Device list unavailable: ", "Lista urządzeń niedostępna: ") + e.Message);
            return false;
        }
    }
}
