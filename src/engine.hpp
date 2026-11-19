#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <d3d11.h>
#include <d3dcompiler.h>
#include <dxgi1_2.h>
#include <windows.graphics.capture.interop.h>
#include <windows.graphics.directx.direct3d11.interop.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Graphics.Capture.h>
#include <winrt/Windows.Graphics.DirectX.Direct3D11.h>
#include <algorithm>
#include <array>
#include <chrono>
#include <cmath>
#include <cstring>
#include <deque>
#include <filesystem>
#include <fstream>
#include <optional>
#include <string>
#include <thread>
#include <vector>
#include "motion.h"
#include "console_edges.hpp"

using namespace winrt;
using namespace winrt::Windows::Graphics::Capture;
using namespace winrt::Windows::Graphics::DirectX;
using namespace winrt::Windows::Graphics::DirectX::Direct3D11;
using Clock = std::chrono::steady_clock;
using Point = Clock::time_point;
using winrt::check_hresult;
template <class T> using Ptr = winrt::com_ptr<T>;
constexpr int SW = 64, SH = 36;
constexpr wchar_t appClass[] = L"NaimanOutput";
struct Image
{
    Ptr<ID3D11Texture2D> tex;
    Ptr<ID3D11ShaderResourceView> srv;
    Ptr<ID3D11RenderTargetView> rtv;
};
struct Stats
{
    uint64_t received = 0, real = 0, generated = 0, repeats = 0, cuts = 0;
};
static bool running = true, smooth = true;
static LRESULT CALLBACK windowProc(HWND h, UINT m, WPARAM w, LPARAM l)
{
    if (m == WM_HOTKEY && w == 2)
    {
        smooth = !smooth;
        return 0;
    }
    if ((m == WM_HOTKEY && w == 1) || m == WM_CLOSE || m == WM_ENDSESSION)
    {
        running = false;
        return 0;
    }
    if (m == WM_NCHITTEST && (GetWindowLongPtrW(h, GWL_EXSTYLE) & WS_EX_TRANSPARENT))
        return HTTRANSPARENT;
    return DefWindowProcW(h, m, w, l);
}
class Engine
{
    Ptr<ID3D11Device> dev;
    Ptr<ID3D11DeviceContext> ctx;
    Ptr<IDXGISwapChain1> swap;
    Ptr<ID3D11RenderTargetView> back;
    Ptr<ID3D11VertexShader> vert;
    Ptr<ID3D11PixelShader> flow, mid, copy, signature;
    Ptr<ID3D11Buffer> params;
    Ptr<ID3D11SamplerState> sampler;
    std::array<Image, 2> frames;
    Image grid, signatureTarget;
    Ptr<ID3D11Texture2D> readback;
    Ptr<ID3D11Texture2D> edgeReadback;
    Ptr<ID3D11Texture2D> deviceUpload;
    Image packedNV12, decodedRGB;
    Ptr<ID3D11PixelShader> decodeYuv;
    int edgeWidth = 0;
    std::array<unsigned char, SW * SH * 3> last{};
    int width = 0, height = 0, ow = 0, oh = 0, latest = 0;
    bool have = false, pending = false;
    double outputPeriodMs;
    Point prev{}, due{};
    double gapMs = 33.333;
    Ptr<ID3DBlob> shader(const char *entry, const char *type)
    {
        Ptr<ID3DBlob> bytes, errors;
        check_hresult(D3DCompile(shaders, strlen(shaders), nullptr, nullptr, nullptr, entry, type,
                                 D3DCOMPILE_OPTIMIZATION_LEVEL3, 0, bytes.put(), errors.put()));
        return bytes;
    }
    Image texture(int w, int h, DXGI_FORMAT fmt, bool pyramid = false)
    {
        Image o;
        D3D11_TEXTURE2D_DESC d{};
        d.Width = w;
        d.Height = h;
        d.ArraySize = 1;
        d.MipLevels = pyramid ? 0 : 1;
        d.Format = fmt;
        d.SampleDesc.Count = 1;
        d.BindFlags = D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;
        d.MiscFlags = pyramid ? D3D11_RESOURCE_MISC_GENERATE_MIPS : 0;
        check_hresult(dev->CreateTexture2D(&d, nullptr, o.tex.put()));
        check_hresult(dev->CreateShaderResourceView(o.tex.get(), nullptr, o.srv.put()));
        check_hresult(dev->CreateRenderTargetView(o.tex.get(), nullptr, o.rtv.put()));
        return o;
    }
    void draw(ID3D11RenderTargetView *target, int w, int h, ID3D11PixelShader *ps, ID3D11ShaderResourceView *a,
              ID3D11ShaderResourceView *b, ID3D11ShaderResourceView *v = nullptr, bool letterbox = false)
    {
        D3D11_VIEWPORT vp{0, 0, (float)w, (float)h, 0, 1};
        if (letterbox)
        {
            float fit = std::min(w / (float)width, h / (float)height);
            vp.Width = width * fit;
            vp.Height = height * fit;
            vp.TopLeftX = (w - vp.Width) * .5f;
            vp.TopLeftY = (h - vp.Height) * .5f;
            const float black[4] = {0, 0, 0, 1};
            ctx->ClearRenderTargetView(target, black);
        }
        ctx->RSSetViewports(1, &vp);
        ctx->OMSetRenderTargets(1, &target, nullptr);
        ID3D11ShaderResourceView *src[] = {a, b, v};
        ctx->PSSetShaderResources(0, 3, src);
        ctx->PSSetShader(ps, nullptr, 0);
        ctx->Draw(3, 0);
        ID3D11ShaderResourceView *clear[3] = {};
        ctx->PSSetShaderResources(0, 3, clear);
        ctx->OMSetRenderTargets(0, nullptr, nullptr);
    }
    void present(bool generated)
    {
        int old = 1 - latest;
        if (generated)
            draw(grid.rtv.get(), gridWidth(), gridHeight(), flow.get(), frames[old].srv.get(),
                 frames[latest].srv.get());
        draw(back.get(), ow, oh, generated ? mid.get() : copy.get(), frames[old].srv.get(), frames[latest].srv.get(),
             generated ? grid.srv.get() : nullptr, true);
        check_hresult(swap->Present(1, 0));
        if (generated)
            stats.generated++;
        else
            stats.real++;
    }
    int gridWidth() const
    {
        return std::clamp((width + 11) / 12, 8, 192);
    }
    int gridHeight() const
    {
        return std::clamp((height + 11) / 12, 8, 108);
    }

  public:
    Stats stats;
    IDirect3DDevice winDevice{nullptr};
    Engine(HWND output, int w, int h, double refreshHz) : ow(w), oh(h), outputPeriodMs(1000.0 / refreshHz)
    {
        D3D_FEATURE_LEVEL level;
        check_hresult(D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                                        nullptr, 0, D3D11_SDK_VERSION, dev.put(), &level, ctx.put()));
        if (level < D3D_FEATURE_LEVEL_11_0)
            throw hresult_error(E_FAIL, L"Direct3D feature level 11.0 is required.");
        auto dx = dev.as<IDXGIDevice>();
        Ptr<::IInspectable> inspect;
        check_hresult(CreateDirect3D11DeviceFromDXGIDevice(dx.get(), inspect.put()));
        winDevice = inspect.as<IDirect3DDevice>();
        Ptr<IDXGIAdapter> adap;
        Ptr<IDXGIFactory2> factory;
        check_hresult(dx->GetAdapter(adap.put()));
        check_hresult(adap->GetParent(__uuidof(IDXGIFactory2), factory.put_void()));
        auto dx1 = dev.as<IDXGIDevice1>();
        dx1->SetMaximumFrameLatency(1);
        DXGI_SWAP_CHAIN_DESC1 sd{};
        sd.Width = w;
        sd.Height = h;
        sd.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
        sd.SampleDesc.Count = 1;
        sd.BufferCount = 2;
        sd.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
        sd.SwapEffect = DXGI_SWAP_EFFECT_FLIP_DISCARD;
        check_hresult(factory->CreateSwapChainForHwnd(dev.get(), output, &sd, nullptr, nullptr, swap.put()));
        factory->MakeWindowAssociation(output, DXGI_MWA_NO_ALT_ENTER);
        Ptr<ID3D11Texture2D> buf;
        check_hresult(swap->GetBuffer(0, __uuidof(ID3D11Texture2D), buf.put_void()));
        check_hresult(dev->CreateRenderTargetView(buf.get(), nullptr, back.put()));
        auto vs = shader("vs", "vs_5_0");
        check_hresult(dev->CreateVertexShader(vs->GetBufferPointer(), vs->GetBufferSize(), nullptr, vert.put()));
        auto make = [&](const char *n, Ptr<ID3D11PixelShader> &p) {
            auto s = shader(n, "ps_5_0");
            check_hresult(dev->CreatePixelShader(s->GetBufferPointer(), s->GetBufferSize(), nullptr, p.put()));
        };
        make("estimate", flow);
        make("midpoint", mid);
        make("copyNew", copy);
        make("signature", signature);
        D3D11_SAMPLER_DESC ss{};
        ss.Filter = D3D11_FILTER_MIN_MAG_MIP_LINEAR;
        ss.AddressU = ss.AddressV = ss.AddressW = D3D11_TEXTURE_ADDRESS_CLAMP;
        ss.MaxLOD = D3D11_FLOAT32_MAX;
        check_hresult(dev->CreateSamplerState(&ss, sampler.put()));
        D3D11_BUFFER_DESC bd{};
        bd.ByteWidth = 16;
        bd.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
        check_hresult(dev->CreateBuffer(&bd, nullptr, params.put()));
        ctx->VSSetShader(vert.get(), nullptr, 0);
        ID3D11SamplerState *samplerPtr = sampler.get();
        ID3D11Buffer *paramsPtr = params.get();
        ctx->PSSetSamplers(0, 1, &samplerPtr);
        ctx->PSSetConstantBuffers(0, 1, &paramsPtr);
        ctx->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
        signatureTarget = texture(SW, SH, DXGI_FORMAT_B8G8R8A8_UNORM);
        D3D11_TEXTURE2D_DESC rd;
        signatureTarget.tex->GetDesc(&rd);
        rd.Usage = D3D11_USAGE_STAGING;
        rd.BindFlags = 0;
        rd.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
        check_hresult(dev->CreateTexture2D(&rd, nullptr, readback.put()));
    }
    RECT refineConsoleRegion(ID3D11Texture2D *source, RECT base)
    {
        const int w = base.right - base.left, h = base.bottom - base.top;
        if (w < 200 || h < 120)
            return base;
        if (w != edgeWidth)
        {
            D3D11_TEXTURE2D_DESC desc{};
            desc.Width = w;
            desc.Height = 9;
            desc.ArraySize = desc.MipLevels = desc.SampleDesc.Count = 1;
            desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
            desc.Usage = D3D11_USAGE_STAGING;
            desc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
            edgeReadback = nullptr;
            check_hresult(dev->CreateTexture2D(&desc, nullptr, edgeReadback.put()));
            edgeWidth = w;
        }
        for (unsigned row = 0; row < 9; ++row)
        {
            unsigned y = base.top + (row < 7 ? (row + 1) * h / 8 : row == 7 ? 1 : h - 2);
            D3D11_BOX line{(UINT)base.left, y, 0, (UINT)base.right, y + 1, 1};
            ctx->CopySubresourceRegion(edgeReadback.get(), 0, 0, row, 0, source, 0, &line);
        }
        D3D11_MAPPED_SUBRESOURCE map{};
        check_hresult(ctx->Map(edgeReadback.get(), 0, D3D11_MAP_READ, 0, &map));
        auto trim = findConsoleEdges(static_cast<const unsigned char *>(map.pData), map.RowPitch, w, h);
        ctx->Unmap(edgeReadback.get(), 0);
        LONG x = base.left + trim.x, y = base.top + trim.y;
        return {x, y, x + trim.w, y + trim.h};
    }
    void resize(int w, int h)
    {
        if (w == width && h == height)
            return;
        width = w;
        height = h;
        have = pending = false;
        for (auto &f : frames)
            f = texture(w, h, DXGI_FORMAT_B8G8R8A8_UNORM, true);
        grid = texture(gridWidth(), gridHeight(), DXGI_FORMAT_R16G16_FLOAT);
        const float cb[] = {1.f / w, 1.f / h, 0, 0};
        ctx->UpdateSubresource(params.get(), 0, nullptr, cb, 0, 0);
    }
    void tick()
    {
        if (pending && Clock::now() >= due)
        {
            present(false);
            pending = false;
        }
    }
    void acceptNV12(const unsigned char *pixels, int w, int h, Point now)
    {
        if (!decodeYuv)
        {
            auto s = shader("decodeNV12", "ps_5_0");
            check_hresult(dev->CreatePixelShader(s->GetBufferPointer(), s->GetBufferSize(), nullptr, decodeYuv.put()));
        }
        if (!packedNV12.tex || w != width || h != height)
        {
            packedNV12 = texture(w, h + h / 2, DXGI_FORMAT_R8_UNORM);
            decodedRGB = texture(w, h, DXGI_FORMAT_B8G8R8A8_UNORM);
        }
        ctx->UpdateSubresource(packedNV12.tex.get(), 0, nullptr, pixels, w, 0);
        draw(decodedRGB.rtv.get(), w, h, decodeYuv.get(), packedNV12.srv.get(), nullptr);
        accept(decodedRGB.tex.get(), w, h, now);
    }
    void acceptPixels(const unsigned char *pixels, int w, int h, Point now)
    {
        if (!deviceUpload || w != width || h != height)
        {
            D3D11_TEXTURE2D_DESC desc{};
            desc.Width = w;
            desc.Height = h;
            desc.MipLevels = desc.ArraySize = desc.SampleDesc.Count = 1;
            desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
            deviceUpload = nullptr;
            check_hresult(dev->CreateTexture2D(&desc, nullptr, deviceUpload.put()));
        }
        ctx->UpdateSubresource(deviceUpload.get(), 0, nullptr, pixels, w * 4, 0);
        accept(deviceUpload.get(), w, h, now);
    }
    void accept(ID3D11Texture2D *src, int w, int h, Point now, int left = 0, int top = 0)
    {
        stats.received++;
        resize(w, h);
        const int next = 1 - latest;
        D3D11_BOX area{(UINT)left, (UINT)top, 0, (UINT)(left + w), (UINT)(top + h), 1};
        ctx->CopySubresourceRegion(frames[next].tex.get(), 0, 0, 0, 0, src, 0, &area);
        draw(signatureTarget.rtv.get(), SW, SH, signature.get(), nullptr, frames[next].srv.get());
        ctx->CopyResource(readback.get(), signatureTarget.tex.get());
        D3D11_MAPPED_SUBRESOURCE map{};
        check_hresult(ctx->Map(readback.get(), 0, D3D11_MAP_READ, 0, &map));
        std::array<unsigned char, SW * SH * 3> sig;
        int total = 0, changed = 0;
        for (int y = 0; y < SH; y++)
        {
            auto row = (const unsigned char *)map.pData + y * map.RowPitch;
            for (int x = 0; x < SW; x++)
            {
                int maxD = 0;
                for (int c = 0; c < 3; c++)
                {
                    int j = (y * SW + x) * 3 + c;
                    sig[j] = row[x * 4 + c];
                    int d = std::abs((int)sig[j] - last[j]);
                    total += d;
                    maxD = std::max(maxD, d);
                }
                if (maxD > 3)
                    changed++;
            }
        }
        ctx->Unmap(readback.get(), 0);
        double mean = total / (double)sig.size();
        if (have && mean < .32 && changed < SW * SH * .025)
        {
            stats.repeats++;
            return;
        }
        if (pending)
        {
            present(false);
            pending = false;
        }
        const double gap = std::chrono::duration<double, std::milli>(now - prev).count();
        bool cut = have && (mean > 39 || gap > 220);
        if (have && gap > 8 && gap < 150)
            gapMs = gapMs * .7 + gap * .3;
        latest = next;
        last = sig;
        ctx->GenerateMips(frames[latest].srv.get());
        // A display already filled by real frames has no free refresh for interpolation.
        if (have && smooth && !cut && gapMs > outputPeriodMs * 1.45)
        {
            present(true);
            pending = true;
            due = now + std::chrono::milliseconds((int)std::clamp(gapMs * .5, 5., 32.));
        }
        else
        {
            present(false);
            if (cut)
                stats.cuts++;
        }
        prev = now;
        have = true;
    }
};
