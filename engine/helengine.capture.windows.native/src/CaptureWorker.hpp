#pragma once
#include "CaptureCallbackState.hpp"
#include "CaptureBorderPolicy.hpp"
#include <windows.graphics.capture.interop.h>
#include <dxgi.h>
#include <roapi.h>
#include <condition_variable>
#include <future>
#include <thread>

/// <summary>Initializes and releases all WGC objects on one persistent MTA worker owned by the session.</summary>
class CaptureWorker {
    /// <summary>Target window is used once to establish capture identity.</summary>
    HWND Window;
    /// <summary>Approved immutable capture options.</summary>
    he_capture_options Options;
    /// <summary>Callback lifetime and GPU owner.</summary>
    std::shared_ptr<CaptureCallbackState> Shared;
    /// <summary>Owns the MTA thread until shutdown completes.</summary>
    std::thread Thread;
    /// <summary>Publishes initialization success or its exact failure.</summary>
    std::promise<void> Initialized;
    /// <summary>Protects the stop request.</summary>
    std::mutex Mutex;
    /// <summary>Wakes the worker when the capture session is disposed.</summary>
    std::condition_variable Changed;
    /// <summary>Set once when the caller requests teardown.</summary>
    bool Stopping = false;
    /// <summary>Retains a cleanup failure for deterministic Stop to report.</summary>
    std::exception_ptr ShutdownFailure;
    /// <summary>Worker-owned source identity.</summary>
    winrt::Windows::Graphics::Capture::GraphicsCaptureItem Item { nullptr };
    /// <summary>Worker-owned free-threaded capture pool.</summary>
    winrt::Windows::Graphics::Capture::Direct3D11CaptureFramePool Pool { nullptr };
    /// <summary>Worker-owned live session.</summary>
    winrt::Windows::Graphics::Capture::GraphicsCaptureSession Session { nullptr };
    /// <summary>Subscription removed before disposing the pool.</summary>
    winrt::event_token FrameToken {};
    /// <summary>Subscription removed before releasing source identity.</summary>
    winrt::event_token ClosedToken {};
public:
    /// <summary>Starts one MTA and propagates failed initialization before returning a session.</summary>
    CaptureWorker(HWND window, he_capture_options options, std::shared_ptr<CaptureCallbackState> shared)
        : Window(window), Options(options), Shared(std::move(shared)) {
        auto ready = Initialized.get_future(); Thread = std::thread(&CaptureWorker::Run, this);
        try { ready.get(); } catch (...) { Thread.join(); throw; }
    }
    /// <summary>Finalizer-safe release; deterministic Stop exposes cleanup failures before destruction.</summary>
    ~CaptureWorker() { try { Stop(); } catch (...) {} }
    /// <summary>Signals stop, joins without holding callback locks, and propagates cleanup failure.</summary>
    void Stop() {
        { std::lock_guard<std::mutex> lock(Mutex); Stopping = true; }
        Changed.notify_one(); if (Thread.joinable()) { Thread.join(); }
        if (ShutdownFailure) { std::rethrow_exception(ShutdownFailure); }
    }
    /// <summary>Creates capture resources exclusively on the worker; callbacks reference shared state weakly.</summary>
    void Initialize() {
        using namespace winrt::Windows::Graphics::Capture;
        if (!GraphicsCaptureSession::IsSupported()) { throw winrt::hresult_not_implemented(L"Windows Graphics Capture is unavailable."); }
        auto factory = winrt::get_activation_factory<GraphicsCaptureItem, IGraphicsCaptureItemInterop>();
        winrt::check_hresult(factory->CreateForWindow(Window, winrt::guid_of<GraphicsCaptureItem>(), winrt::put_abi(Item)));
        auto size = Item.Size(); if (size.Width <= 0 || size.Height <= 0) { throw winrt::hresult_invalid_argument(L"Window capture dimensions are empty."); }
        Microsoft::WRL::ComPtr<IDXGIDevice> dxgi; winrt::check_hresult(Shared->Gpu->Device.As(&dxgi));
        winrt::com_ptr<IInspectable> inspectable;
        winrt::check_hresult(CreateDirect3D11DeviceFromDXGIDevice(dxgi.Get(), inspectable.put()));
        Shared->CaptureDevice = inspectable.as<winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DDevice>();
        Shared->PoolSize = size;
        Pool = Direct3D11CaptureFramePool::CreateFreeThreaded(Shared->CaptureDevice,
            winrt::Windows::Graphics::DirectX::DirectXPixelFormat::B8G8R8A8UIntNormalized, 2, size);
        std::weak_ptr<CaptureCallbackState> weak = Shared;
        FrameToken = Pool.FrameArrived([weak](const auto& pool, const auto&) { if (auto state = weak.lock()) { state->Refresh(pool); } });
        ClosedToken = Item.Closed([weak](const auto&, const auto&) {
            if (auto state = weak.lock()) { state->Terminate(he_capture_state::WindowClosed); }
        });
        Session = Pool.CreateCaptureSession(Item); Session.IsCursorCaptureEnabled(Options.CaptureCursor != 0);
        CaptureBorderPolicy::Apply(Session, Options.RemoveBorder != 0); Session.StartCapture();
    }
    /// <summary>Closes each initialized resource even if a preceding Close operation fails.</summary>
    void Cleanup() noexcept {
        try { if (Pool && FrameToken.value) { Pool.FrameArrived(FrameToken); } } catch (...) { ShutdownFailure = std::current_exception(); }
        try { if (Item && ClosedToken.value) { Item.Closed(ClosedToken); } } catch (...) { if (!ShutdownFailure) { ShutdownFailure = std::current_exception(); } }
        try { if (Session) { Session.Close(); } } catch (...) { if (!ShutdownFailure) { ShutdownFailure = std::current_exception(); } }
        try { if (Pool) { Pool.Close(); } } catch (...) { if (!ShutdownFailure) { ShutdownFailure = std::current_exception(); } }
        Session = nullptr; Pool = nullptr; Item = nullptr;
        std::lock_guard<std::mutex> lock(Shared->Gate); Shared->CaptureDevice = nullptr;
    }
    /// <summary>Pairs COM initialization with resource release before leaving the worker apartment.</summary>
    void Run() noexcept {
        HRESULT initialized = RoInitialize(RO_INIT_MULTITHREADED); bool published = false;
        try {
            winrt::check_hresult(initialized); Initialize(); Initialized.set_value(); published = true;
            std::unique_lock<std::mutex> lock(Mutex); Changed.wait(lock, [this] { return Stopping; });
        } catch (...) {
            if (!published) { Initialized.set_exception(std::current_exception()); }
            else { ShutdownFailure = std::current_exception(); }
            Shared->Terminate(he_capture_state::Faulted);
        }
        Cleanup(); if (SUCCEEDED(initialized)) { RoUninitialize(); }
    }
};
