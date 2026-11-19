#include "engine.hpp"
#include <shellapi.h>
#include <mmsystem.h>
#include <iomanip>
#include <dwmapi.h>
#pragma comment(lib, "dwmapi.lib")
struct Options
{
    HWND source{};
    DWORD pid = 0, parent = 0;
    int seconds = 0;
    bool original = false, windowed = false, console = false;
    std::wstring log, stop;
};
Options options()
{
    int n = 0;
    auto v = CommandLineToArgvW(GetCommandLineW(), &n);
    Options o;
    for (int i = 1; i < n; i++)
    {
        std::wstring k = v[i];
        if (k == L"--console")
        {
            o.console = true;
            continue;
        }
        if (k == L"--original")
        {
            o.original = true;
            continue;
        }
        if (k == L"--windowed")
        {
            o.windowed = true;
            continue;
        }
        if (i + 1 >= n)
            throw hresult_invalid_argument();
        auto val = v[++i];
        if (k == L"--window")
            o.source = (HWND)(uintptr_t)std::stoull(val);
        else if (k == L"--pid")
            o.pid = std::stoul(val);
        else if (k == L"--parent")
            o.parent = std::stoul(val);
        else if (k == L"--seconds")
            o.seconds = std::stoi(val);
        else if (k == L"--log")
            o.log = val;
        else if (k == L"--stop")
            o.stop = val;
        else
            throw hresult_invalid_argument();
    }
    LocalFree(v);
    return o;
}
struct ViewportChild
{
    RECT client;
    RECT best{};
    long long area = 0;
};
BOOL CALLBACK findBrowserViewport(HWND child, LPARAM data)
{
    wchar_t cls[64]{};
    GetClassNameW(child, cls, 64);
    if (wcscmp(cls, L"Chrome_RenderWidgetHostHWND") != 0 || !IsWindowVisible(child))
        return TRUE;
    auto &hit = *reinterpret_cast<ViewportChild *>(data);
    RECT rc{}, inside{};
    GetWindowRect(child, &rc);
    if (IntersectRect(&inside, &rc, &hit.client))
    {
        auto a = (long long)(inside.right - inside.left) * (inside.bottom - inside.top);
        if (a > hit.area)
        {
            hit.area = a;
            hit.best = inside;
        }
    }
    return TRUE;
}
RECT consoleBounds(HWND source, int w, int h)
{
    RECT whole{0, 0, w, h}, frame{}, client{};
    if (FAILED(DwmGetWindowAttribute(source, DWMWA_EXTENDED_FRAME_BOUNDS, &frame, sizeof(frame))) ||
        !GetClientRect(source, &client))
        return whole;
    POINT pt{};
    if (!ClientToScreen(source, &pt))
        return whole;
    OffsetRect(&client, pt.x, pt.y);
    ViewportChild child{client};
    EnumChildWindows(source, findBrowserViewport, (LPARAM)&child);
    if (child.area > (long long)(client.right - client.left) * (client.bottom - client.top) / 2)
        client = child.best;
    int fw = frame.right - frame.left, fh = frame.bottom - frame.top;
    if (fw < 2 || fh < 2)
        return whole;
    RECT box{std::clamp(MulDiv(client.left - frame.left, w, fw), 0, w),
             std::clamp(MulDiv(client.top - frame.top, h, fh), 0, h),
             std::clamp(MulDiv(client.right - frame.left, w, fw), 0, w),
             std::clamp(MulDiv(client.bottom - frame.top, h, fh), 0, h)};
    int cw = box.right - box.left, ch = box.bottom - box.top;
    if (cw < 32 || ch < 32)
        return whole;
    int vw = std::min(cw, MulDiv(ch, 16, 9)), vh = std::min(ch, MulDiv(cw, 9, 16));
    int x = box.left + (cw - vw) / 2, y = box.top + (ch - vh) / 2;
    return {x, y, x + vw, y + vh};
}
void run(const Options &o, std::ofstream &log)
{
    using Access = ::Windows::Graphics::DirectX::Direct3D11::IDirect3DDxgiInterfaceAccess;
    DWORD pid = 0;
    GetWindowThreadProcessId(o.source, &pid);
    if (!o.source || !pid || pid != o.pid || !IsWindowVisible(o.source) || IsIconic(o.source))
        throw hresult_error(E_INVALIDARG, L"Restore the source window and select it again.");
    if (!GraphicsCaptureSession::IsSupported())
        throw hresult_error(E_FAIL, L"Windows Graphics Capture is unavailable.");
    winrt::handle parent;
    if (o.parent)
    {
        parent.attach(OpenProcess(SYNCHRONIZE, FALSE, o.parent));
        if (!parent)
            throw hresult_error(E_FAIL, L"The launcher has closed.");
    }
    auto interop = get_activation_factory<GraphicsCaptureItem, IGraphicsCaptureItemInterop>();
    GraphicsCaptureItem item{nullptr};
    check_hresult(interop->CreateForWindow(o.source, guid_of<GraphicsCaptureItem>(), put_abi(item)));
    MONITORINFOEXW mon{};
    mon.cbSize = sizeof(mon);
    if (!GetMonitorInfoW(MonitorFromWindow(o.source, MONITOR_DEFAULTTONEAREST), &mon))
        throw_last_error();
    DEVMODEW displayMode{};
    displayMode.dmSize = sizeof(displayMode);
    bool haveFrequency =
        EnumDisplaySettingsW(mon.szDevice, ENUM_CURRENT_SETTINGS, &displayMode) && displayMode.dmDisplayFrequency > 1;
    int x = mon.rcMonitor.left, y = mon.rcMonitor.top, w = mon.rcMonitor.right - x, h = mon.rcMonitor.bottom - y;
    if (o.windowed)
    {
        w = 640;
        h = 360;
        x += 12;
        y += 12;
    }
    WNDCLASSW wc{};
    wc.lpfnWndProc = windowProc;
    wc.hInstance = GetModuleHandleW(nullptr);
    wc.lpszClassName = appClass;
    RegisterClassW(&wc);
    HWND out =
        CreateWindowExW(WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST | WS_EX_TRANSPARENT | WS_EX_LAYERED,
                        appClass, L"NAIMAN - output", WS_POPUP, x, y, w, h, nullptr, nullptr, wc.hInstance, nullptr);
    if (!out)
        throw_last_error();
    if (!SetLayeredWindowAttributes(out, 0, 255, LWA_ALPHA))
        throw_last_error();
    if (!RegisterHotKey(out, 1, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, 'Q'))
        throw hresult_error(E_FAIL, L"Ctrl+Alt+Q is in use. Close the other session or release the shortcut.");
    if (!RegisterHotKey(out, 2, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, 'B'))
        throw hresult_error(E_FAIL, L"Ctrl+Alt+B is in use. Release the shortcut and try again.");
    Engine engine(out, w, h, haveFrequency ? displayMode.dmDisplayFrequency : 60);
    smooth = !o.original;
    auto size = item.Size();
    auto pool = Direct3D11CaptureFramePool::CreateFreeThreaded(engine.winDevice,
                                                               DirectXPixelFormat::B8G8R8A8UIntNormalized, 2, size);
    auto session = pool.CreateCaptureSession(item);
    try
    {
        session.IsCursorCaptureEnabled(false);
    }
    catch (hresult_error const &)
    {
    }
    try
    {
        session.MinUpdateInterval(std::chrono::milliseconds(8));
    }
    catch (hresult_error const &)
    {
    }
    session.StartCapture();
    auto start = Clock::now(), lastReport = start, lastFrame = start;
    bool shown = false, hasFrame = false;
    RECT region{}, lastBase{}, candidate{};
    unsigned confirmations = 0;
    Point checkBounds{}, nextEdgeProbe{};
    if (!o.windowed)
        SetForegroundWindow(o.source);
    log << "started\n" << std::flush;
    while (running)
    {
        MSG msg;
        while (PeekMessageW(&msg, nullptr, 0, 0, PM_REMOVE))
        {
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
        if (!running)
            break;
        auto now = Clock::now();
        const bool sourceActive = GetForegroundWindow() == o.source;
        const bool shouldShow = hasFrame && (o.windowed || sourceActive);
        if (shouldShow != shown)
        {
            // Returning to another application must uncover it. Returning to the
            // game restores the overlay without taking keyboard/controller focus.
            ShowWindow(out, shouldShow ? SW_SHOWNOACTIVATE : SW_HIDE);
            shown = shouldShow;
        }
        if (now - lastReport >= std::chrono::seconds(1))
        {
            const auto &s = engine.stats;
            log << "sample " << std::fixed << std::setprecision(3) << std::chrono::duration<double>(now - start).count()
                << " " << s.received << " " << s.real << " " << s.generated << " " << s.repeats << " " << s.cuts << " "
                << (smooth ? 1 : 0) << " " << (sourceActive || o.windowed ? 1 : 0) << "\n"
                << std::flush;
            lastReport = now;
            DWORD current = 0;
            GetWindowThreadProcessId(o.source, &current);
            if (current != pid || IsIconic(o.source) || !IsWindowVisible(o.source))
                break;
            if (parent && WaitForSingleObject(parent.get(), 0) == WAIT_OBJECT_0)
                break;
            if (!o.stop.empty() && std::filesystem::exists(o.stop))
                break;
        }
        if (o.seconds && now - start >= std::chrono::seconds(o.seconds))
            break;
        engine.tick();
        auto frame = pool.TryGetNextFrame();
        if (frame)
        {
            const auto content = frame.ContentSize();
            if (content.Width <= 0 || content.Height <= 0)
                break;
            if (content.Width != size.Width || content.Height != size.Height)
            {
                frame.Close();
                size = content;
                checkBounds = nextEdgeProbe = {};
                lastBase = {};
                pool.Recreate(engine.winDevice, DirectXPixelFormat::B8G8R8A8UIntNormalized, 2, size);
                continue;
            }
            auto access = frame.Surface().as<Access>();
            Ptr<ID3D11Texture2D> tex;
            check_hresult(access->GetInterface(__uuidof(ID3D11Texture2D), tex.put_void()));
            if (now >= checkBounds || region.right > size.Width || region.bottom > size.Height)
            {
                auto base =
                    o.console ? consoleBounds(o.source, size.Width, size.Height) : RECT{0, 0, size.Width, size.Height};
                if (!EqualRect(&lastBase, &base))
                {
                    confirmations = 0;
                    lastBase = base;
                    nextEdgeProbe = {};
                }
                const bool cropConfirmed = confirmations == 2 && !EqualRect(&candidate, &base);
                if (o.console && !cropConfirmed && now >= nextEdgeProbe)
                {
                    auto probe = engine.refineConsoleRegion(tex.get(), base);
                    // Two matching samples keep a single dark frame from cropping the HUD.
                    confirmations = EqualRect(&probe, &candidate) ? std::min(2u, confirmations + 1) : 1;
                    candidate = probe;
                    nextEdgeProbe = now + std::chrono::milliseconds(confirmations == 2 ? 1800 : 500);
                }
                auto newRegion = o.console && confirmations == 2 ? candidate : base;
                if (!EqualRect(&region, &newRegion))
                    log << "viewport " << newRegion.left << " " << newRegion.top << " "
                        << (newRegion.right - newRegion.left) << " " << (newRegion.bottom - newRegion.top) << " output "
                        << w << " " << h << "\n"
                        << std::flush;
                region = newRegion;
                checkBounds = now + std::chrono::milliseconds(350);
            }
            engine.accept(tex.get(), region.right - region.left, region.bottom - region.top, now, region.left,
                          region.top);
            lastFrame = now;
            hasFrame = true;
        }
        else
        {
            if (!hasFrame && now - lastFrame > std::chrono::seconds(8))
                throw hresult_error(E_FAIL, L"No frames arrived. Restore the source and start playback.");
            MsgWaitForMultipleObjectsEx(0, nullptr, 2, QS_ALLINPUT, MWMO_INPUTAVAILABLE);
        }
    }
    session.Close();
    pool.Close();
    DestroyWindow(out);
    const auto &s = engine.stats;
    log << "end " << s.received << " " << s.real << " " << s.generated << " " << s.repeats << " " << s.cuts << "\n";
}
int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR, int)
{
    std::ofstream log;
    try
    {
        SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        init_apartment(apartment_type::multi_threaded);
        auto o = options();
        log.open(std::filesystem::path(o.log));
        if (!log)
            return 2;
        timeBeginPeriod(1);
        run(o, log);
        timeEndPeriod(1);
        return 0;
    }
    catch (hresult_error const &e)
    {
        log << "error " << to_string(e.message()) << "\n";
        return 1;
    }
    catch (std::exception const &e)
    {
        log << "error " << e.what() << "\n";
        return 1;
    }
}
