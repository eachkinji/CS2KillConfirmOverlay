#include <windows.h>
#include <dcomp.h>
#include <d3d11.h>
#include <dxgi1_2.h>
#include <wrl/client.h>
#include <memory>
#include <cstring>
using Microsoft::WRL::ComPtr;
// ABI from the installed Win2D Microsoft.Graphics.Canvas.native.h.
struct __declspec(uuid("5F10688D-EA55-4D55-A3B0-4DDB55C0C20A")) Wrapper: IUnknown {
    virtual HRESULT STDMETHODCALLTYPE GetNativeResource(void*,float,REFIID,void**)=0;
};
struct Host {
    HWND hwnd{};ComPtr<IDXGISwapChain1> chain;ComPtr<IDCompositionDevice> device;
    ComPtr<IDCompositionTarget> target;ComPtr<IDCompositionVisual> visual;ComPtr<IDCompositionEffectGroup> effect;
    ~Host(){if(hwnd)DestroyWindow(hwnd);}
};
LRESULT CALLBACK Proc(HWND h,UINT m,WPARAM w,LPARAM l){
    if(m==WM_NCHITTEST)return HTTRANSPARENT;if(m==WM_MOUSEACTIVATE)return MA_NOACTIVATE;
    return DefWindowProc(h,m,w,l);
}
#define CHECK(x) {HRESULT hr=(x);if(FAILED(hr))return hr;}
extern "C" __declspec(dllexport) HRESULT __stdcall CreateHost(IUnknown* canvas,int width,int height,Host** result){
    *result=nullptr;auto h=std::make_unique<Host>();ComPtr<Wrapper> wrapper;
    CHECK(canvas->QueryInterface(IID_PPV_ARGS(&wrapper)));
    CHECK(wrapper->GetNativeResource(nullptr,96,__uuidof(IDXGISwapChain1),(void**)&h->chain));
    ComPtr<IDXGIDevice> dxgi;CHECK(h->chain->GetDevice(IID_PPV_ARGS(&dxgi)));
    CHECK(DCompositionCreateDevice(dxgi.Get(),IID_PPV_ARGS(&h->device)));
    WNDCLASSW cls{};cls.lpfnWndProc=Proc;cls.hInstance=GetModuleHandle(nullptr);cls.lpszClassName=L"KillConfirm.Composition";
    RegisterClassW(&cls);
    h->hwnd=CreateWindowExW(WS_EX_NOREDIRECTIONBITMAP|WS_EX_TRANSPARENT|WS_EX_NOACTIVATE|WS_EX_TOOLWINDOW|WS_EX_TOPMOST,cls.lpszClassName,L"Kill Confirm Playback",WS_POPUP,0,0,width,height,nullptr,nullptr,cls.hInstance,nullptr);
    if(!h->hwnd)return HRESULT_FROM_WIN32(GetLastError());
    CHECK(h->device->CreateTargetForHwnd(h->hwnd,TRUE,&h->target));
    CHECK(h->device->CreateVisual(&h->visual));CHECK(h->visual->SetContent(h->chain.Get()));
    CHECK(h->device->CreateEffectGroup(&h->effect));CHECK(h->effect->SetOpacity(1.0f));CHECK(h->visual->SetEffect(h->effect.Get()));
    CHECK(h->target->SetRoot(h->visual.Get()));CHECK(h->device->Commit());
    *result=h.release();return S_OK;
}
extern "C" __declspec(dllexport) void __stdcall DestroyHost(Host* h){delete h;}
extern "C" __declspec(dllexport) HWND __stdcall GetHostWindow(Host* h){return h->hwnd;}
extern "C" __declspec(dllexport) HRESULT __stdcall PlaceHost(Host* h,int x,int y,int width,int height,
    float scaleX,float scaleY,float offsetX,float offsetY,float opacity,int visible){
    D2D_MATRIX_3X2_F transform={scaleX,0,0,scaleY,offsetX,offsetY};
    CHECK(h->visual->SetTransform(transform));
    CHECK(h->effect->SetOpacity(opacity));CHECK(h->device->Commit());
    if(!SetWindowPos(h->hwnd,HWND_TOPMOST,x,y,width,height,SWP_NOACTIVATE))return HRESULT_FROM_WIN32(GetLastError());
    ShowWindow(h->hwnd,visible?SW_SHOWNOACTIVATE:SW_HIDE);return S_OK;
}

// Explicit diagnostic only; never used by the playback loop.
extern "C" __declspec(dllexport) HRESULT __stdcall ReadBack(Host* h,unsigned char* pixels,int length){
    ComPtr<ID3D11Texture2D> buffer;CHECK(h->chain->GetBuffer(0,IID_PPV_ARGS(&buffer)));
    D3D11_TEXTURE2D_DESC desc;buffer->GetDesc(&desc);if(length<int(desc.Width*desc.Height*4))return E_INVALIDARG;
    ComPtr<ID3D11Device> device;buffer->GetDevice(&device);ComPtr<ID3D11DeviceContext> context;device->GetImmediateContext(&context);
    desc.Usage=D3D11_USAGE_STAGING;desc.BindFlags=0;desc.CPUAccessFlags=D3D11_CPU_ACCESS_READ;desc.MiscFlags=0;
    ComPtr<ID3D11Texture2D> staging;CHECK(device->CreateTexture2D(&desc,nullptr,&staging));context->CopyResource(staging.Get(),buffer.Get());
    D3D11_MAPPED_SUBRESOURCE mapped;CHECK(context->Map(staging.Get(),0,D3D11_MAP_READ,0,&mapped));
    for(UINT y=0;y<desc.Height;y++)memcpy(pixels+y*desc.Width*4,(char*)mapped.pData+y*mapped.RowPitch,desc.Width*4);
    context->Unmap(staging.Get(),0);return S_OK;
}
