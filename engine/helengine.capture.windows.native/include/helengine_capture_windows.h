#pragma once
#include <stdint.h>
#include <stddef.h>

#pragma pack(push, 8)
/// <summary>Versioned options for an independently owned Windows capture session.</summary>
struct he_capture_options {
    /// <summary>Complete options buffer size in bytes.</summary>
    uint32_t Size;
    /// <summary>Supported ABI revision, currently one.</summary>
    uint32_t Version;
    /// <summary>Maximum combined pending and consumer-held frame count.</summary>
    uint32_t BufferCapacity;
    /// <summary>Zero or one flag controlling cursor capture.</summary>
    uint32_t CaptureCursor;
    /// <summary>Zero or one flag explicitly requesting consented border removal.</summary>
    uint32_t RemoveBorder;
};
/// <summary>Caller-owned diagnostic storage, never shared between operations or threads.</summary>
struct he_capture_error {
    /// <summary>Complete error buffer size, including its fixed strings.</summary>
    uint32_t Size;
    /// <summary>Diagnostic ABI revision.</summary>
    uint32_t Version;
    /// <summary>Original native failure HRESULT.</summary>
    int32_t HResult;
    /// <summary>Null-terminated name of the failed operation.</summary>
    wchar_t Operation[64];
    /// <summary>Null-terminated operation-owned diagnostic text.</summary>
    wchar_t Message[512];
};
/// <summary>Borrowed texture/device pointers and original timing owned by one native frame lease.</summary>
struct he_capture_frame_info {
    /// <summary>Complete x64 frame metadata size.</summary>
    uint32_t Size;
    /// <summary>Frame metadata ABI revision.</summary>
    uint32_t Version;
    /// <summary>Borrowed ID3D11Texture2D reference, valid until the frame is released.</summary>
    void* Texture;
    /// <summary>Borrowed matching ID3D11Device reference, valid for the same lease.</summary>
    void* Device;
    /// <summary>Valid captured content width in pixels.</summary>
    uint32_t Width;
    /// <summary>Valid captured content height in pixels.</summary>
    uint32_t Height;
    /// <summary>Capture pixel enum; value one represents BGRA8.</summary>
    uint32_t PixelFormat;
    /// <summary>Reserved version-one padding emitted as zero.</summary>
    uint32_t Reserved;
    /// <summary>Original SystemRelativeTime in 100 ns units.</summary>
    int64_t TimestampTicks;
    /// <summary>Monotonic source observation count including known discards.</summary>
    uint64_t SequenceNumber;
};
/// <summary>Counts observed inputs and explicit library-side delivery or discard decisions.</summary>
struct he_capture_statistics {
    /// <summary>Complete accounting snapshot size.</summary>
    uint32_t Size;
    /// <summary>Accounting ABI revision.</summary>
    uint32_t Version;
    /// <summary>Source frames actually acquired from Windows.</summary>
    uint64_t ObservedFrames;
    /// <summary>Frames transferred to the consumer.</summary>
    uint64_t DeliveredFrames;
    /// <summary>New frames discarded while the allocation budget was occupied.</summary>
    uint64_t OverflowDiscardedFrames;
    /// <summary>Observed frames with invalid content dimensions.</summary>
    uint64_t ResizeDiscardedFrames;
    /// <summary>Pending frames discarded during terminal transitions.</summary>
    uint64_t ShutdownDiscardedFrames;
};
#pragma pack(pop)
/// <summary>Stable state values shared with the managed facade.</summary>
enum class he_capture_state : uint32_t { Running = 0, WindowClosed = 1, Faulted = 2, Disposed = 3 };

#if defined(HE_CAPTURE_EXPORTS)
#define HE_CAPTURE_API __declspec(dllexport)
#else
#define HE_CAPTURE_API
#endif
extern "C" {
/// <summary>Creates an owned capture session from validated options and a source HWND.</summary>
HE_CAPTURE_API int32_t __cdecl he_capture_open(void*, void*, const he_capture_options*, void**, he_capture_error*);
/// <summary>Transfers one frame or reports pending, closed or failed capture explicitly.</summary>
HE_CAPTURE_API int32_t __cdecl he_capture_try_acquire(void*, void**, he_capture_frame_info*, he_capture_error*);
/// <summary>Queries running or terminal state without acquiring a frame.</summary>
HE_CAPTURE_API int32_t __cdecl he_capture_get_state(void*, uint32_t*, he_capture_error*);
/// <summary>Copies a versioned library accounting snapshot.</summary>
HE_CAPTURE_API int32_t __cdecl he_capture_get_statistics(void*, he_capture_statistics*, he_capture_error*);
/// <summary>Copies one leased frame into an explicitly sized packed BGRA destination.</summary>
HE_CAPTURE_API int32_t __cdecl he_capture_copy_pixels(void*, uint8_t*, uint64_t, he_capture_error*);
/// <summary>Stops production and returns accounting after pending frames have been released.</summary>
HE_CAPTURE_API int32_t __cdecl he_capture_stop(void*, he_capture_statistics*, he_capture_error*);
/// <summary>Releases one frame owner independently of its original capture session.</summary>
HE_CAPTURE_API void __cdecl he_capture_release_frame(void*) noexcept;
/// <summary>Stops and destroys one capture session owner.</summary>
HE_CAPTURE_API void __cdecl he_capture_close(void*) noexcept;
}
