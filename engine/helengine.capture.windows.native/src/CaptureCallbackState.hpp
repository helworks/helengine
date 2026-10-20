#pragma once
#include "CaptureFrameQueue.hpp"
#include "CaptureFailure.hpp"
#include <windows.graphics.directx.direct3d11.interop.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Graphics.Capture.h>
#include <winrt/Windows.Graphics.DirectX.Direct3D11.h>

/// <summary>Keeps callback state alive independently of the session pointer and fences publication during teardown.</summary>
class CaptureCallbackState {
public:
    /// <summary>Serializes callbacks and terminal transitions; no WinRT Close operation is called while held.</summary>
    mutable std::mutex Gate;
    /// <summary>Owns pending textures while independent frames retain their own resources.</summary>
    CaptureFrameQueue Queue;
    /// <summary>Retains the GPU used to create the WinRT capture device.</summary>
    std::shared_ptr<CaptureGpuContext> Gpu;
    /// <summary>Agile capture wrapper used only while the session is running.</summary>
    winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DDevice CaptureDevice { nullptr };
    /// <summary>Current frame pool dimensions, updated only under the publication gate.</summary>
    winrt::Windows::Graphics::SizeInt32 PoolSize {};
    /// <summary>Current terminal or running state.</summary>
    he_capture_state Status = he_capture_state::Running;
    /// <summary>First terminal capture failure, preserved for later polling calls.</summary>
    CaptureFailure Failure;
    /// <summary>Creates callback state without establishing a window dependency.</summary>
    CaptureCallbackState(uint32_t capacity, std::shared_ptr<CaptureGpuContext> gpu) : Queue(capacity, gpu), Gpu(std::move(gpu)) {}
    /// <summary>Publishes only while running; also supplies a deterministic synthetic input seam for native tests.</summary>
    void PublishTexture(ID3D11Texture2D* texture, int32_t width, int32_t height, int64_t timestamp) {
        std::lock_guard<std::mutex> lock(Gate);
        if (Status == he_capture_state::Running) { Queue.TryPush(texture, width, height, timestamp); }
    }
    /// <summary>Changes state atomically with clearing pending frames, preserving previously delivered leases.</summary>
    void Terminate(he_capture_state status, const CaptureFailure& failure = {}) {
        std::lock_guard<std::mutex> lock(Gate);
        if (Status == he_capture_state::Disposed || (Status != he_capture_state::Running && status != he_capture_state::Disposed)) { return; }
        Status = status; Failure = failure; Queue.DiscardPending();
    }
    /// <summary>Copies a Windows frame before returning its surface and recreates the pool after valid size changes.</summary>
    void Refresh(const winrt::Windows::Graphics::Capture::Direct3D11CaptureFramePool& pool) noexcept {
        std::lock_guard<std::mutex> lock(Gate);
        if (Status != he_capture_state::Running) { return; }
        try {
            auto frame = pool.TryGetNextFrame(); if (!frame) { return; }
            auto size = frame.ContentSize();
            auto access = frame.Surface().as<::Windows::Graphics::DirectX::Direct3D11::IDirect3DDxgiInterfaceAccess>();
            Microsoft::WRL::ComPtr<ID3D11Texture2D> texture;
            winrt::check_hresult(access->GetInterface(IID_PPV_ARGS(&texture)));
            Queue.TryPush(texture.Get(), size.Width, size.Height, frame.SystemRelativeTime().count());
            frame.Close();
            if (size.Width > 0 && size.Height > 0 && (size.Width != PoolSize.Width || size.Height != PoolSize.Height)) {
                PoolSize = size;
                pool.Recreate(CaptureDevice, winrt::Windows::Graphics::DirectX::DirectXPixelFormat::B8G8R8A8UIntNormalized, 2, size);
            }
        } catch (...) {
            Status = he_capture_state::Faulted;
            try { Failure = CaptureFailure::Current(); } catch (...) { Failure.Code = E_OUTOFMEMORY; }
            Queue.DiscardPending();
        }
    }
};
