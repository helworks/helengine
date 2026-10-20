#pragma once
#include <stdint.h>
#include <stddef.h>

#pragma pack(push, 8)
/// <summary>Versioned options for an independently owned Windows capture session.</summary>
struct he_capture_options {
    uint32_t Size, Version, BufferCapacity, CaptureCursor, RemoveBorder;
};
/// <summary>Caller-owned diagnostic storage, never shared between operations or threads.</summary>
struct he_capture_error {
    uint32_t Size, Version;
    int32_t HResult;
    wchar_t Operation[64], Message[512];
};
/// <summary>Borrowed texture/device pointers and original timing owned by one native frame lease.</summary>
struct he_capture_frame_info {
    uint32_t Size, Version;
    void* Texture;
    void* Device;
    uint32_t Width, Height, PixelFormat, Reserved;
    int64_t TimestampTicks;
    uint64_t SequenceNumber;
};
/// <summary>Counts observed inputs and explicit library-side delivery or discard decisions.</summary>
struct he_capture_statistics {
    uint32_t Size, Version;
    uint64_t ObservedFrames, DeliveredFrames, OverflowDiscardedFrames, ResizeDiscardedFrames, ShutdownDiscardedFrames;
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
HE_CAPTURE_API int32_t __cdecl he_capture_open(void*, void*, const he_capture_options*, void**, he_capture_error*);
HE_CAPTURE_API int32_t __cdecl he_capture_try_acquire(void*, void**, he_capture_frame_info*, he_capture_error*);
HE_CAPTURE_API int32_t __cdecl he_capture_get_state(void*, uint32_t*, he_capture_error*);
HE_CAPTURE_API int32_t __cdecl he_capture_get_statistics(void*, he_capture_statistics*, he_capture_error*);
HE_CAPTURE_API int32_t __cdecl he_capture_copy_pixels(void*, uint8_t*, uint64_t, he_capture_error*);
HE_CAPTURE_API int32_t __cdecl he_capture_stop(void*, he_capture_statistics*, he_capture_error*);
HE_CAPTURE_API void __cdecl he_capture_release_frame(void*) noexcept;
HE_CAPTURE_API void __cdecl he_capture_close(void*) noexcept;
}
