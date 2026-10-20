#include "CaptureSession.hpp"

/// <summary>Validates caller-provided ABI buffers before exporting handles or writing metadata.</summary>
class CaptureAbiUtils {
public:
    /// <summary>Requires the complete current structure rather than guessing an ABI version.</summary>
    template<class T> static void Require(const T* value) {
        if (!value || value->Size != sizeof(T) || value->Version != 1) { throw std::invalid_argument("Invalid capture ABI size or version."); }
    }
    /// <summary>Rejects a missing native owner without dereferencing it.</summary>
    template<class T> static T* Owner(void* value) {
        if (!value) { throw std::invalid_argument("A native capture handle is required."); }
        return static_cast<T*>(value);
    }
};

/// <summary>Opens one validated HWND and transfers exactly one session owner to the caller.</summary>
int32_t he_capture_open(void* hwnd, void* device, const he_capture_options* options, void** output, he_capture_error* error) {
    if (output) { *output = nullptr; }
    try {
        CaptureAbiUtils::Require(error); CaptureAbiUtils::Require(options);
        if (!output || !options->BufferCapacity || options->CaptureCursor > 1 || options->RemoveBorder > 1) {
            throw std::invalid_argument("Invalid capture options or output pointer.");
        }
        auto session = CaptureSession::Open(static_cast<HWND>(hwnd), static_cast<ID3D11Device*>(device), *options);
        *output = session.release(); return 0;
    } catch (...) { return CaptureFailure::Write(error, L"open"); }
}
/// <summary>Transfers a newly acquired frame without reusing previously delivered handles.</summary>
int32_t he_capture_try_acquire(void* session, void** output, he_capture_frame_info* info, he_capture_error* error) {
    if (output) { *output = nullptr; }
    try {
        CaptureAbiUtils::Require(error); CaptureAbiUtils::Require(info);
        if (!output) { throw std::invalid_argument("A frame output pointer is required."); }
        info->Texture = info->Device = nullptr;
        std::unique_ptr<CaptureFrame> frame; auto result = CaptureAbiUtils::Owner<CaptureSession>(session)->TryAcquire(frame);
        if (frame) { *info = frame->Info(); *output = frame.release(); }
        return result;
    } catch (...) { return CaptureFailure::Write(error, L"try_acquire"); }
}
/// <summary>Reads session state even when the window has closed or capture has faulted.</summary>
int32_t he_capture_get_state(void* session, uint32_t* state, he_capture_error* error) {
    try {
        CaptureAbiUtils::Require(error); if (!state) { throw std::invalid_argument("A state output is required."); }
        *state = static_cast<uint32_t>(CaptureAbiUtils::Owner<CaptureSession>(session)->State()); return 0;
    } catch (...) { return CaptureFailure::Write(error, L"get_state"); }
}
/// <summary>Copies an accounting snapshot into the caller-owned versioned buffer.</summary>
int32_t he_capture_get_statistics(void* session, he_capture_statistics* stats, he_capture_error* error) {
    try {
        CaptureAbiUtils::Require(error); CaptureAbiUtils::Require(stats);
        *stats = CaptureAbiUtils::Owner<CaptureSession>(session)->Statistics(); return 0;
    } catch (...) { return CaptureFailure::Write(error, L"get_statistics"); }
}
/// <summary>Performs explicit packed BGRA readback for a live frame owner.</summary>
int32_t he_capture_copy_pixels(void* frame, uint8_t* destination, uint64_t length, he_capture_error* error) {
    try {
        CaptureAbiUtils::Require(error); CaptureAbiUtils::Owner<CaptureFrame>(frame)->CopyPixels(destination, length); return 0;
    } catch (...) { return CaptureFailure::Write(error, L"copy_pixels"); }
}
/// <summary>Stops capture before returning statistics, preserving the owner until its eventual close.</summary>
int32_t he_capture_stop(void* session, he_capture_statistics* stats, he_capture_error* error) {
    try {
        CaptureAbiUtils::Require(error); CaptureAbiUtils::Require(stats); auto owner = CaptureAbiUtils::Owner<CaptureSession>(session);
        owner->Stop(); *stats = owner->Statistics(); return 0;
    } catch (...) { return CaptureFailure::Write(error, L"stop"); }
}
/// <summary>Releases exactly one frame owner; its texture and budget token are destroyed independently.</summary>
void he_capture_release_frame(void* frame) noexcept { delete static_cast<CaptureFrame*>(frame); }
/// <summary>Destroys a session without invalidating frames already owned by consumers.</summary>
void he_capture_close(void* session) noexcept { delete static_cast<CaptureSession*>(session); }
