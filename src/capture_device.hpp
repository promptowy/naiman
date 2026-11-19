#pragma once
#include <dshow.h>
#include <dvdmedia.h>
#include <atomic>
#include <mutex>
#include <limits>

struct __declspec(uuid("0579154A-2B53-4994-B0D0-E773148EFF85")) GrabCallback : IUnknown
{
    virtual HRESULT STDMETHODCALLTYPE SampleCB(double, IMediaSample *) = 0;
    virtual HRESULT STDMETHODCALLTYPE BufferCB(double, BYTE *, long) = 0;
};
struct __declspec(uuid("6B652FFF-11FE-4FCE-92AD-0266B5D7C78F")) SampleGrabber : IUnknown
{
    virtual HRESULT STDMETHODCALLTYPE SetOneShot(BOOL) = 0;
    virtual HRESULT STDMETHODCALLTYPE SetMediaType(const AM_MEDIA_TYPE *) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetConnectedMediaType(AM_MEDIA_TYPE *) = 0;
    virtual HRESULT STDMETHODCALLTYPE SetBufferSamples(BOOL) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetCurrentBuffer(long *, long *) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetCurrentSample(IMediaSample **) = 0;
    virtual HRESULT STDMETHODCALLTYPE SetCallback(GrabCallback *, long) = 0;
};
static constexpr GUID grabberClass{0xC1F400A0,0x3F08,0x11D3,{0x9F,0x0B,0x00,0x60,0x08,0x03,0x9E,0x37}};
static constexpr GUID nullRendererClass{0xC1F400A4,0x3F08,0x11D3,{0x9F,0x0B,0x00,0x60,0x08,0x03,0x9E,0x37}};

inline void captureCheck(HRESULT result, const wchar_t *stage)
{
    if (FAILED(result)) throw hresult_error(result, std::wstring(stage) + L": " + hresult_error(result).message().c_str());
}
inline void freeFormat(AM_MEDIA_TYPE &mt)
{
    CoTaskMemFree(mt.pbFormat);
    if (mt.pUnk) mt.pUnk->Release();
}
inline const BITMAPINFOHEADER *bitmapInfo(const AM_MEDIA_TYPE &mt)
{
    if (!mt.pbFormat) return nullptr;
    if (mt.formattype == FORMAT_VideoInfo && mt.cbFormat >= sizeof(VIDEOINFOHEADER))
        return &reinterpret_cast<const VIDEOINFOHEADER *>(mt.pbFormat)->bmiHeader;
    if (mt.formattype == FORMAT_VideoInfo2 && mt.cbFormat >= sizeof(VIDEOINFOHEADER2))
        return &reinterpret_cast<const VIDEOINFOHEADER2 *>(mt.pbFormat)->bmiHeader;
    return nullptr;
}
inline Ptr<IBaseFilter> deviceFilter(const GUID &category, const std::wstring &id)
{
    auto devices = winrt::create_instance<ICreateDevEnum>(CLSID_SystemDeviceEnum, CLSCTX_INPROC_SERVER);
    Ptr<IEnumMoniker> list;
    auto result = devices->CreateClassEnumerator(category, list.put(), 0);
    check_hresult(result);
    if (list)
    {
        Ptr<IMoniker> entry;
        while (list->Next(1, entry.put(), nullptr) == S_OK)
        {
            LPOLESTR name = nullptr;
            captureCheck(entry->GetDisplayName(nullptr, nullptr, &name), L"Read device identifier");
            bool match = id == name;
            CoTaskMemFree(name);
            if (match)
            {
                Ptr<IBaseFilter> filter;
                captureCheck(entry->BindToObject(nullptr, nullptr, IID_IBaseFilter, filter.put_void()), L"Open capture device");
                return filter;
            }
            entry = nullptr;
        }
    }
    throw hresult_error(HRESULT_FROM_WIN32(ERROR_NOT_FOUND), L"Capture device disconnected. Refresh the device list.");
}

// Negotiate a source format before connecting. Prefer the largest supported image
// that fits the monitor, then up to 60 source frames per second at that size.
inline bool chooseVideoFormat(ICaptureGraphBuilder2 *builder, IBaseFilter *source, int maxW, int maxH)
{
    Ptr<IAMStreamConfig> config;
    if (FAILED(builder->FindInterface(&PIN_CATEGORY_CAPTURE, &MEDIATYPE_Video, source,
                                     IID_IAMStreamConfig, config.put_void()))) return false;
    int count = 0, size = 0;
    if (FAILED(config->GetNumberOfCapabilities(&count, &size)) || size <= 0 || size > 65536) return false;
    std::vector<BYTE> caps(size);
    std::vector<std::pair<double,int>> choices;
    for (int i=0; i<count; ++i)
    {
        AM_MEDIA_TYPE *mt = nullptr;
        if (FAILED(config->GetStreamCaps(i, &mt, caps.data())) || !mt) continue;
        auto bi = bitmapInfo(*mt);
        if (bi && bi->biWidth > 0 && bi->biWidth <= 4096 && std::abs(bi->biHeight) <= 2160 && bi->biHeight != 0)
        {
            int w = bi->biWidth, h = std::abs(bi->biHeight);
            auto interval = mt->formattype == FORMAT_VideoInfo ?
                reinterpret_cast<VIDEOINFOHEADER *>(mt->pbFormat)->AvgTimePerFrame :
                reinterpret_cast<VIDEOINFOHEADER2 *>(mt->pbFormat)->AvgTimePerFrame;
            double fps = interval > 0 ? 1.e7 / interval : 30.;
            double pixels = w * double(h);
            double score = (w <= maxW && h <= maxH ? pixels : -pixels) + std::min(60., fps) * 10.;
            if (mt->subtype == MEDIASUBTYPE_NV12) score += 1.;
            if (fps < 25.) score -= 1.e8;
            choices.emplace_back(score, i);
        }
        freeFormat(*mt);
        CoTaskMemFree(mt);
    }
    std::stable_sort(choices.begin(), choices.end(), [](auto a, auto b){return a.first > b.first;});
    for (auto choice : choices)
    {
        AM_MEDIA_TYPE *mt = nullptr;
        if (FAILED(config->GetStreamCaps(choice.second, &mt, caps.data())) || !mt) continue;
        auto hr = config->SetFormat(mt);
        const bool nv12 = mt->subtype == MEDIASUBTYPE_NV12;
        freeFormat(*mt);
        CoTaskMemFree(mt);
        if (SUCCEEDED(hr)) return nv12;
    }
    return false;
}

inline Ptr<IPin> filterPin(IBaseFilter *filter, PIN_DIRECTION wanted, bool videoOnly = false)
{
    Ptr<IEnumPins> pins;
    check_hresult(filter->EnumPins(pins.put()));
    Ptr<IPin> pin;
    while (pins->Next(1, pin.put(), nullptr) == S_OK)
    {
        PIN_DIRECTION direction;
        check_hresult(pin->QueryDirection(&direction));
        if (direction == wanted)
        {
            if (!videoOnly) return pin;
            Ptr<IEnumMediaTypes> types;
            if (SUCCEEDED(pin->EnumMediaTypes(types.put())))
            {
                AM_MEDIA_TYPE *mt = nullptr;
                while (types->Next(1, &mt, nullptr) == S_OK)
                {
                    bool video = mt->majortype == MEDIATYPE_Video;
                    freeFormat(*mt);
                    CoTaskMemFree(mt);
                    if (video) return pin;
                }
            }
        }
        pin = nullptr;
    }
    throw hresult_error(VFW_E_NOT_FOUND, L"No active video pin. Check the HDMI signal or start the source.");
}
struct DeviceFrame : winrt::implements<DeviceFrame, GrabCallback>
{
    int width = 0, height = 0;
    bool bottomUp = false, fresh = false, nv12 = false;
    std::mutex guard;
    std::vector<BYTE> latest;
    HRESULT STDMETHODCALLTYPE SampleCB(double, IMediaSample *) noexcept override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE BufferCB(double, BYTE *data, long size) noexcept override
    {
        const size_t stride = size_t(width) * 4;
        if (!data || size < 0 || size_t(size) < latestSize()) return S_OK;
        std::lock_guard<std::mutex> lock(guard);
        if (bottomUp)
            for (int y=0; y<height; ++y)
                std::memcpy(latest.data() + y*stride, data + (height-1-y)*stride, stride);
        else std::memcpy(latest.data(), data, latestSize());
        fresh = true;
        return S_OK;
    }
    size_t latestSize() const {return nv12 ? size_t(width)*(height+height/2) : size_t(width)*height*4;}
    bool take(std::vector<BYTE> &target)
    {
        std::lock_guard<std::mutex> lock(guard);
        if (!fresh) return false;
        latest.swap(target);
        fresh = false;
        return true;
    }
};

class CaptureDevice
{
    Ptr<IGraphBuilder> graph;
    Ptr<IMediaControl> control;
    Ptr<IMediaEvent> events;
    Ptr<SampleGrabber> grab;
    Ptr<DeviceFrame> callback;
    std::vector<BYTE> front;
  public:
    CaptureDevice(const std::wstring &video, const std::wstring &audio, int maxW, int maxH)
    {
        graph = winrt::create_instance<IGraphBuilder>(CLSID_FilterGraph, CLSCTX_INPROC_SERVER);
        auto builder = winrt::create_instance<ICaptureGraphBuilder2>(CLSID_CaptureGraphBuilder2, CLSCTX_INPROC_SERVER);
        check_hresult(builder->SetFiltergraph(graph.get()));
        auto camera = deviceFilter(CLSID_VideoInputDeviceCategory, video);
        check_hresult(graph->AddFilter(camera.get(), L"Video input"));
        const bool nativeNV12 = chooseVideoFormat(builder.get(), camera.get(), maxW, maxH);
        auto grabFilter = winrt::create_instance<IBaseFilter>(grabberClass, CLSCTX_INPROC_SERVER);
        auto sink = winrt::create_instance<IBaseFilter>(nullRendererClass, CLSCTX_INPROC_SERVER);
        check_hresult(graph->AddFilter(grabFilter.get(), L"NAIMAN frames"));
        check_hresult(graph->AddFilter(sink.get(), L"Video sink"));
        grab = grabFilter.as<SampleGrabber>();
        AM_MEDIA_TYPE rgb{};
        rgb.majortype = MEDIATYPE_Video;
        rgb.subtype = nativeNV12 ? MEDIASUBTYPE_NV12 : MEDIASUBTYPE_RGB32;
        captureCheck(grab->SetMediaType(&rgb), L"Select RGB format");
        auto input = filterPin(camera.get(), PINDIR_OUTPUT, true);
        auto pinIn = filterPin(grabFilter.get(), PINDIR_INPUT);
        captureCheck(graph->Connect(input.get(), pinIn.get()), L"Connect video capture stream");
        auto pinOut = filterPin(grabFilter.get(), PINDIR_OUTPUT);
        auto sinkIn = filterPin(sink.get(), PINDIR_INPUT);
        captureCheck(graph->ConnectDirect(pinOut.get(), sinkIn.get(), nullptr), L"Connect frame receiver");
        AM_MEDIA_TYPE actual{};
        captureCheck(grab->GetConnectedMediaType(&actual), L"Read capture format");
        auto bi = bitmapInfo(actual);
        callback = winrt::make_self<DeviceFrame>();
        callback->nv12 = actual.subtype == MEDIASUBTYPE_NV12;
        if (bi && (bi->biBitCount == 32 || (callback->nv12 && bi->biWidth%2==0 && bi->biHeight%2==0)) && bi->biWidth > 0 && bi->biWidth <= 4096 &&
            bi->biHeight != 0 && std::abs(bi->biHeight) <= 2160)
        {
            callback->width = bi->biWidth;
            callback->height = std::abs(bi->biHeight);
            callback->bottomUp = !callback->nv12 && bi->biHeight > 0;
        }
        freeFormat(actual);
        if (!callback->width) throw hresult_error(E_FAIL, L"Unsupported capture format.");
        callback->latest.resize(callback->latestSize());
        front.resize(callback->latestSize());
        captureCheck(grab->SetCallback(callback.get(), 1), L"Register frame receiver");
        if (!audio.empty())
        {
            auto sound = deviceFilter(CLSID_AudioInputDeviceCategory, audio);
            check_hresult(graph->AddFilter(sound.get(), L"Selected capture audio"));
            auto speakers = winrt::create_instance<IBaseFilter>(CLSID_DSoundRender, CLSCTX_INPROC_SERVER);
            check_hresult(graph->AddFilter(speakers.get(), L"Audio playback"));
            check_hresult(builder->RenderStream(nullptr, &MEDIATYPE_Audio, sound.get(), nullptr, speakers.get()));
        }
        events = graph.as<IMediaEvent>();
        control = graph.as<IMediaControl>();
        if (audio.empty())
            captureCheck(graph.as<IMediaFilter>()->SetSyncSource(nullptr), L"Enable live source timing");
        captureCheck(grab->SetOneShot(FALSE), L"Enable continuous video");
        captureCheck(grab->SetBufferSamples(FALSE), L"Enable live frame delivery");
        captureCheck(control->Run(), L"Start capture device");
    }
    ~CaptureDevice()
    {
        if (control) control->Stop();
        if (grab) grab->SetCallback(nullptr, 1);
    }
    void poll(Engine &engine, Point now)
    {
        long code; LONG_PTR a,b;
        while (events->GetEvent(&code,&a,&b,0) == S_OK)
        {
            events->FreeEventParams(code,a,b);
            if (code == EC_ERRORABORT)
                captureCheck(FAILED((HRESULT)a) ? (HRESULT)a : E_FAIL, L"Capture driver stopped");
            if (code == EC_DEVICE_LOST && b == 0)
                throw hresult_error(E_FAIL, L"Capture device disconnected. Check HDMI/USB and reopen the device.");
            // Some live graphs post EC_COMPLETE during preroll. The frame
            // timeout in the playback loop distinguishes this from loss of video.
        }
        if (callback->take(front))
        {
            if (callback->nv12) engine.acceptNV12(front.data(), callback->width, callback->height, now);
            else engine.acceptPixels(front.data(), callback->width, callback->height, now);
        }
    }
};
