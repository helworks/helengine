#include "HevcEncoder.h"
#include "EncoderFailure.h"
#include <memory>
#include <cstring>
#include <cstddef>
static_assert(sizeof(he_video_encoder_options)==32 && offsetof(he_video_encoder_options,bitrate)==16 && offsetof(he_video_encoder_options,max_bitrate)==24,"Encoder options ABI");
static_assert(sizeof(he_video_encoder_error)==1028,"Encoder error ABI");
static int Fail(he_video_encoder_error* error,int code,const char* message) noexcept {
    if(error) { error->code=code; strncpy_s(error->message,message,_TRUNCATE); } return -1;
}
#define HE_ENCODER_CATCH(error) catch(const EncoderFailure& failure) { return Fail(error,failure.Code,failure.what()); } catch(const std::exception& failure) { return Fail(error,E_FAIL,failure.what()); } catch(...) { return Fail(error,E_FAIL,"Unknown HEVC encoder failure."); }
extern "C" int __cdecl he_video_encoder_create(void* device,const wchar_t* path,const he_video_encoder_options* options,void** encoder,he_video_encoder_error* error) {
    if(error) { *error={}; } if(encoder) { *encoder=nullptr; }
    try { EncoderFailure::Require(options && encoder,"Encoder options and output are required."); auto owner=std::make_unique<HevcEncoder>(); owner->Open(static_cast<ID3D11Device*>(device),path,*options); *encoder=owner.release(); return 0; } HE_ENCODER_CATCH(error)
}
extern "C" int __cdecl he_video_encoder_submit(void* encoder,void* texture,int64_t index,he_video_encoder_error* error) {
    if(error) { *error={}; }
    try { EncoderFailure::Require(encoder!=nullptr,"Encoder is required."); static_cast<HevcEncoder*>(encoder)->Submit(static_cast<ID3D11Texture2D*>(texture),index); return 0; } HE_ENCODER_CATCH(error)
}
extern "C" int __cdecl he_video_encoder_complete(void* encoder,he_video_encoder_error* error) {
    if(error) { *error={}; }
    try { EncoderFailure::Require(encoder!=nullptr,"Encoder is required."); static_cast<HevcEncoder*>(encoder)->Complete(); return 0; } HE_ENCODER_CATCH(error)
}
extern "C" void __cdecl he_video_encoder_destroy(void* encoder) { try { delete static_cast<HevcEncoder*>(encoder); } catch(...) {} }
