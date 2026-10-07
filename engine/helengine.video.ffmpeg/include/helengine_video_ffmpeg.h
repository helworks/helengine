#pragma once
#include <stdint.h>
// Matches the existing managed sequential structs with the platform's default eight-byte packing.
struct he_video_stream_info { int Width; int Height; double FrameRate; int64_t DurationTicks; int FrameFormat; int IsHardwareAccelerated; };
struct he_video_frame { void* Texture; int SubresourceIndex; int Width; int Height; int FrameFormat; int64_t TimestampTicks; int64_t DurationTicks; };
extern "C" {
__declspec(dllexport) int __cdecl he_video_probe(const wchar_t* source,he_video_stream_info* info);
__declspec(dllexport) void* __cdecl he_video_decoder_create(void* device,const wchar_t* source,int hardwareMode,he_video_stream_info* info);
__declspec(dllexport) int __cdecl he_video_decoder_try_get_frame(void* decoder,he_video_frame* frame);
__declspec(dllexport) int __cdecl he_video_decoder_seek(void* decoder,int64_t timestampTicks);
__declspec(dllexport) void __cdecl he_video_decoder_flush(void* decoder);
__declspec(dllexport) void __cdecl he_video_decoder_release_frame(void* decoder,he_video_frame* frame);
__declspec(dllexport) void __cdecl he_video_decoder_destroy(void* decoder);
__declspec(dllexport) const char* __cdecl he_media_last_error();
}
