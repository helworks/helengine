#pragma once
#include "CaptureFrame.hpp"
#include <deque>

/// <summary>Serializes a bounded FIFO while leases retain their own device and capacity token.</summary>
class CaptureFrameQueue {
    /// <summary>GPU owner shared by independent frame leases.</summary>
    std::shared_ptr<CaptureGpuContext> Gpu;
    /// <summary>Capacity ledger shared with delivered frames, without an ownership cycle.</summary>
    std::shared_ptr<CaptureFrameBudget> Budget;
    /// <summary>Owns only frames that have not been transferred to consumers.</summary>
    std::deque<std::unique_ptr<CaptureFrame>> Pending;
    /// <summary>Protects queue publication, acquisition and accounting snapshots.</summary>
    mutable std::mutex Mutex;
    /// <summary>Counts known source observations and library decisions.</summary>
    he_capture_statistics Statistics { sizeof(he_capture_statistics), 1, 0, 0, 0, 0, 0 };
public:
    /// <summary>Creates one hard allocation budget shared by queued and delivered frames.</summary>
    CaptureFrameQueue(uint32_t capacity, std::shared_ptr<CaptureGpuContext> gpu)
        : Gpu(std::move(gpu)), Budget(std::make_shared<CaptureFrameBudget>(capacity)) {
        if (!Gpu) { throw std::invalid_argument("A GPU context is required."); }
    }
    /// <summary>Copies only valid source content or explicitly accounts for resize/overflow discard.</summary>
    bool TryPush(ID3D11Texture2D* source, int32_t width, int32_t height, int64_t timestamp) {
        std::lock_guard<std::mutex> queueLock(Mutex); auto sequence = ++Statistics.ObservedFrames;
        if (!source) { throw std::invalid_argument("A source texture is required."); }
        D3D11_TEXTURE2D_DESC input {}; source->GetDesc(&input);
        if (width <= 0 || height <= 0 || static_cast<uint32_t>(width) > input.Width || static_cast<uint32_t>(height) > input.Height) {
            ++Statistics.ResizeDiscardedFrames; return false;
        }
        if (!Budget->TryReserve()) { ++Statistics.OverflowDiscardedFrames; return false; }
        bool reservationOwned = true;
        try {
            std::lock_guard<std::mutex> gpuLock(Gpu->Mutex);
            D3D11_TEXTURE2D_DESC output {};
            output.Width = width; output.Height = height; output.MipLevels = output.ArraySize = 1;
            output.Format = DXGI_FORMAT_B8G8R8A8_UNORM; output.SampleDesc.Count = 1;
            output.Usage = D3D11_USAGE_DEFAULT; output.BindFlags = D3D11_BIND_SHADER_RESOURCE;
            Microsoft::WRL::ComPtr<ID3D11Texture2D> texture;
            winrt::check_hresult(Gpu->Device->CreateTexture2D(&output, nullptr, &texture));
            D3D11_BOX region { 0, 0, 0, static_cast<UINT>(width), static_cast<UINT>(height), 1 };
            Gpu->Context->CopySubresourceRegion(texture.Get(), 0, 0, 0, 0, source, 0, &region);
            auto frame = std::make_unique<CaptureFrame>(Gpu, Budget, std::move(texture), width, height, timestamp, sequence);
            reservationOwned = false;
            Pending.push_back(std::move(frame));
        } catch (...) {
            if (reservationOwned) { Budget->Release(); }
            throw;
        }
        return true;
    }
    /// <summary>Transfers the next pending frame exactly once, preserving FIFO order.</summary>
    std::unique_ptr<CaptureFrame> TryAcquire() {
        std::lock_guard<std::mutex> lock(Mutex);
        if (Pending.empty()) { return nullptr; }
        auto result = std::move(Pending.front()); Pending.pop_front(); ++Statistics.DeliveredFrames; return result;
    }
    /// <summary>Releases pending frames without waiting for frames already held by consumers.</summary>
    void DiscardPending() {
        std::lock_guard<std::mutex> lock(Mutex); Statistics.ShutdownDiscardedFrames += Pending.size(); Pending.clear();
    }
    /// <summary>Returns an internally consistent snapshot of capture accounting.</summary>
    he_capture_statistics Snapshot() const { std::lock_guard<std::mutex> lock(Mutex); return Statistics; }
};
