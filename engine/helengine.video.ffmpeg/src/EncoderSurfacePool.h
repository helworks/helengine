#pragma once
#include <d3d11.h>
#include <wrl/client.h>
#include <atomic>
extern "C" {
#include <libavutil/hwcontext.h>
}
// Owns at most eight independent NV12 surfaces and GPU-only RGB conversion.
class EncoderSurfacePool {
    Microsoft::WRL::ComPtr<ID3D11Device> Device;
    Microsoft::WRL::ComPtr<ID3D11DeviceContext> Context;
    Microsoft::WRL::ComPtr<ID3D11VideoDevice> VideoDevice;
    Microsoft::WRL::ComPtr<ID3D11VideoContext> VideoContext;
    Microsoft::WRL::ComPtr<ID3D11VideoProcessorEnumerator> Enumerator;
    Microsoft::WRL::ComPtr<ID3D11VideoProcessor> Processor;
    Microsoft::WRL::ComPtr<ID3D11Texture2D> PaddedInput;
    Microsoft::WRL::ComPtr<ID3D11RenderTargetView> PaddedTarget;
    int Width=0;
    int Height=0;
    std::atomic<unsigned int> Allocated{0};
    static AVBufferRef* Allocate(void* opaque,size_t size);
    static void Free(void* opaque,uint8_t* data);
public:
    AVBufferRef* Hardware=nullptr;
    AVBufferRef* Frames=nullptr;
    ~EncoderSurfacePool();
    void Open(ID3D11Device* device,int width,int height);
    void Convert(ID3D11Texture2D* source,AVFrame* destination);
};
