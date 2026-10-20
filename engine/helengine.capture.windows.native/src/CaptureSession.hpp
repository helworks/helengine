#pragma once
#include "CaptureWorker.hpp"

/// <summary>Owns a single window capture while delivered frames have fully independent resource lifetimes.</summary>
class CaptureSession {
    /// <summary>Allows deterministic native tests to exercise callback publication without opening a real window.</summary>
    friend class SessionContractTests;
    /// <summary>State referenced weakly by callbacks and strongly by active callback invocations.</summary>
    std::shared_ptr<CaptureCallbackState> Shared;
    /// <summary>Owns the initialized MTA and WinRT capture resources.</summary>
    std::unique_ptr<CaptureWorker> Worker;
public:
    /// <summary>Builds state before creating WinRT resources, so partial initialization has deterministic cleanup.</summary>
    CaptureSession(std::shared_ptr<CaptureGpuContext> gpu, const he_capture_options& options)
        : Shared(std::make_shared<CaptureCallbackState>(options.BufferCapacity, std::move(gpu))) {}
    /// <summary>Stops capture before its state is released; finalization cannot throw.</summary>
    ~CaptureSession() { Close(); }
    /// <summary>Creates capture identity once and never searches for replacement windows.</summary>
    static std::unique_ptr<CaptureSession> Open(HWND window, ID3D11Device* device, const he_capture_options& options) {
        if (!window || !IsWindow(window)) { throw winrt::hresult_error(HRESULT_FROM_WIN32(ERROR_INVALID_WINDOW_HANDLE), L"The capture window is invalid."); }
        auto session = std::make_unique<CaptureSession>(CaptureGpuContext::Create(device), options);
        session->Shared->SourceWindow = window;
        session->Shared->SourceThread = GetWindowThreadProcessId(window, &session->Shared->SourceProcess);
        session->Worker = std::make_unique<CaptureWorker>(window, options, session->Shared); return session;
    }
    /// <summary>Returns 0 for pending, 1 for an owned frame and 2 for closed; actual failures throw internally.</summary>
    int32_t TryAcquire(std::unique_ptr<CaptureFrame>& frame) {
        std::lock_guard<std::mutex> lock(Shared->Gate); frame.reset();
        Shared->CheckSourceWindow();
        if (Shared->Status == he_capture_state::Faulted) { Shared->Failure.Throw(); }
        if (Shared->Status == he_capture_state::Disposed) { throw winrt::hresult_illegal_method_call(L"Capture session is disposed."); }
        if (Shared->Status == he_capture_state::WindowClosed) { return 2; }
        frame = Shared->Queue.TryAcquire(); return frame ? 1 : 0;
    }
    /// <summary>Publishes an originating failure and prevents subsequent stale frame delivery.</summary>
    void Fail(HRESULT result, std::wstring operation, std::wstring message) {
        CaptureFailure failure; failure.Code = result; failure.Message = operation + L": " + message;
        Shared->Terminate(he_capture_state::Faulted, failure);
    }
    /// <summary>Provides an atomic state snapshot independently of queued frame availability.</summary>
    he_capture_state State() const { std::lock_guard<std::mutex> lock(Shared->Gate); Shared->CheckSourceWindow(); return Shared->Status; }
    /// <summary>Returns library-side statistics while keeping held frame resources independent.</summary>
    he_capture_statistics Statistics() const { return Shared->Queue.Snapshot(); }
    /// <summary>Stops publication before tearing down WinRT and waiting for its MTA; never waits for consumers.</summary>
    void Stop() { Shared->Terminate(he_capture_state::Disposed); if (Worker) { Worker->Stop(); } }
    /// <summary>Provides no-throw teardown for destructors and SafeHandle release paths.</summary>
    void Close() noexcept { try { Stop(); } catch (...) {} }
};
