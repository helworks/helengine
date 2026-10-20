#pragma once
#include "CaptureGpuContext.hpp"
#include "CaptureFrameBudget.hpp"
#include "helengine_capture_windows.h"
#include <cstring>
#include <limits>

/// <summary>Owns an independent BGRA texture, device lifetime, original metadata, and one capacity reservation.</summary>
class CaptureFrame {
    /// <summary>Retains GPU access after the capture session has been closed.</summary>
    std::shared_ptr<CaptureGpuContext> Gpu;
    /// <summary>Returns this frame's capacity without referencing its producer.</summary>
    std::shared_ptr<CaptureFrameBudget> Budget;
    /// <summary>Texture is never replaced while the frame is held by a consumer.</summary>
    Microsoft::WRL::ComPtr<ID3D11Texture2D> Texture;
    /// <summary>Immutable metadata including borrowed pointers tied to this frame lifetime.</summary>
    he_capture_frame_info Metadata;
public:
    /// <summary>Adopts an initialized texture and an already reserved budget token.</summary>
    CaptureFrame(std::shared_ptr<CaptureGpuContext> gpu, std::shared_ptr<CaptureFrameBudget> budget,
        Microsoft::WRL::ComPtr<ID3D11Texture2D> texture, uint32_t width, uint32_t height, int64_t timestamp, uint64_t sequence)
        : Gpu(std::move(gpu)), Budget(std::move(budget)), Texture(std::move(texture)),
        Metadata { sizeof(he_capture_frame_info), 1, Texture.Get(), Gpu->Device.Get(), width, height, 1, 0, timestamp, sequence } {}
    /// <summary>Destroys the texture before returning its allocation slot to the budget.</summary>
    ~CaptureFrame() { Texture.Reset(); Budget->Release(); }
    /// <summary>Copies immutable metadata; the pointer fields remain borrowed from this lease.</summary>
    he_capture_frame_info Info() const { return Metadata; }
    /// <summary>Reads BGRA pixels only on explicit request and never writes beyond the caller buffer.</summary>
    void CopyPixels(uint8_t* destination, uint64_t length) {
        uint64_t rowBytes = static_cast<uint64_t>(Metadata.Width) * 4;
        uint64_t required = rowBytes * Metadata.Height;
        if (!destination || length < required || required > (std::numeric_limits<size_t>::max)()) {
            throw std::invalid_argument("The BGRA destination buffer is too small.");
        }
        std::lock_guard<std::mutex> lock(Gpu->Mutex);
        D3D11_TEXTURE2D_DESC description {}; Texture->GetDesc(&description);
        description.BindFlags = 0; description.Usage = D3D11_USAGE_STAGING;
        description.CPUAccessFlags = D3D11_CPU_ACCESS_READ; description.MiscFlags = 0;
        Microsoft::WRL::ComPtr<ID3D11Texture2D> staging;
        winrt::check_hresult(Gpu->Device->CreateTexture2D(&description, nullptr, &staging));
        Gpu->Context->CopyResource(staging.Get(), Texture.Get());
        D3D11_MAPPED_SUBRESOURCE mapped {};
        winrt::check_hresult(Gpu->Context->Map(staging.Get(), 0, D3D11_MAP_READ, 0, &mapped));
        for (uint32_t row = 0; row < Metadata.Height; row++) {
            std::memcpy(destination + row * rowBytes, static_cast<const uint8_t*>(mapped.pData) + row * mapped.RowPitch,
                static_cast<size_t>(rowBytes));
        }
        Gpu->Context->Unmap(staging.Get(), 0);
    }
};
