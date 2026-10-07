#include <windows.h>
#include <d3d11.h>
#include <string>
#include <vector>
#include <memory>
#include <algorithm>
#include <stdexcept>
#include <limits>
#include "helengine_video_ffmpeg.h"
extern "C" {
#include <libavformat/avformat.h>
#include <libavcodec/avcodec.h>
#include <libavutil/hwcontext.h>
#include <libavutil/hwcontext_d3d11va.h>
#include <libavutil/imgutils.h>
#include <libswscale/swscale.h>
}
// The error buffer is thread-local so concurrent jobs cannot replace each other's diagnostics.
thread_local std::string LastError;
static void Check(int result,const char* operation) {
    if(result<0) { char text[AV_ERROR_MAX_STRING_SIZE]; av_strerror(result,text,sizeof(text)); throw std::runtime_error(std::string(operation)+": "+text); }
}
static std::string Utf8(const wchar_t* source) {
    if(!source) {throw std::runtime_error("A media path is required.");}
    int size=WideCharToMultiByte(CP_UTF8,WC_ERR_INVALID_CHARS,source,-1,nullptr,0,nullptr,nullptr);
    if(size<=1) {throw std::runtime_error("A valid UTF-16 media path is required.");}
    std::string result(size,'\0'); WideCharToMultiByte(CP_UTF8,WC_ERR_INVALID_CHARS,source,-1,result.data(),size,nullptr,nullptr);result.resize(size-1);return result;
}
// All decoder state, frames and device references belong to one independent source cursor.
class NativeVideoDecoder {
public:
    AVFormatContext* Format=nullptr;
    AVCodecContext* Codec=nullptr;
    AVFrame* Frame=av_frame_alloc();
    AVPacket* Packet=av_packet_alloc();
    AVBufferRef* Hardware=nullptr;
    SwsContext* Converter=nullptr;
    ID3D11Device* Device=nullptr;
    int StreamIndex=-1;
    int Mode=2;
    int64_t Origin=0;
    bool Draining=false;
    struct HeldFrame { he_video_frame Info; AVFrame* Frame; ID3D11Texture2D* Texture; };
    std::vector<HeldFrame> Held;
    ~NativeVideoDecoder() {
        for(auto& held:Held) {av_frame_free(&held.Frame);held.Texture->Release();}
        sws_freeContext(Converter);av_frame_free(&Frame);av_packet_free(&Packet);avcodec_free_context(&Codec);avformat_close_input(&Format);av_buffer_unref(&Hardware);if(Device) {Device->Release();}
    }
    static AVPixelFormat ChooseFormat(AVCodecContext* codec,const AVPixelFormat* formats) {
        auto* self=static_cast<NativeVideoDecoder*>(codec->opaque);
        for(auto* format=formats;*format!=AV_PIX_FMT_NONE;format++) {if(*format==AV_PIX_FMT_D3D11) {return *format;}}
        return self->Mode==0 ? AV_PIX_FMT_NONE : formats[0];
    }
    void Open(void* device,const wchar_t* source,int mode,he_video_stream_info* info) {
        if(!device || !info || mode<0 || mode>2 || !Frame || !Packet) {throw std::runtime_error("Invalid video decoder arguments.");}
        Device=static_cast<ID3D11Device*>(device);Device->AddRef();Mode=mode;
        std::string path=Utf8(source);Check(avformat_open_input(&Format,path.c_str(),nullptr,nullptr),"Open video");Check(avformat_find_stream_info(Format,nullptr),"Probe video");
        StreamIndex=av_find_best_stream(Format,AVMEDIA_TYPE_VIDEO,-1,-1,nullptr,0);Check(StreamIndex,"Find video stream");
        AVStream* stream=Format->streams[StreamIndex];const AVCodec* implementation=avcodec_find_decoder(stream->codecpar->codec_id);
        if(!implementation) {throw std::runtime_error("Video codec is unavailable.");}
        Codec=avcodec_alloc_context3(implementation);if(!Codec) {throw std::bad_alloc();}
        Check(avcodec_parameters_to_context(Codec,stream->codecpar),"Configure video codec");Codec->pkt_timebase=stream->time_base;
        Origin=stream->start_time==AV_NOPTS_VALUE ? 0 : stream->start_time;
        bool compatible=false;
        for(int index=0;;index++) {auto* config=avcodec_get_hw_config(implementation,index);if(!config) {break;}if(config->device_type==AV_HWDEVICE_TYPE_D3D11VA && (config->methods&AV_CODEC_HW_CONFIG_METHOD_HW_DEVICE_CTX)) {compatible=true;break;}}
        ID3D11VideoDevice* videoDevice=nullptr;
        bool hardwareDevice=SUCCEEDED(Device->QueryInterface(__uuidof(ID3D11VideoDevice),reinterpret_cast<void**>(&videoDevice))) && videoDevice->GetVideoDecoderProfileCount()>0;
        if(videoDevice) {videoDevice->Release();}
        if(mode!=2 && compatible && hardwareDevice) {
            Hardware=av_hwdevice_ctx_alloc(AV_HWDEVICE_TYPE_D3D11VA);if(!Hardware) {throw std::bad_alloc();}
            auto* context=reinterpret_cast<AVHWDeviceContext*>(Hardware->data);
            auto* d3d=reinterpret_cast<AVD3D11VADeviceContext*>(context->hwctx);d3d->device=Device;Device->AddRef();
            int initialized=av_hwdevice_ctx_init(Hardware);
            if(initialized<0) {av_buffer_unref(&Hardware);if(mode==0) {Check(initialized,"Hardware required but initialization failed");}}
            if(Hardware) {Codec->hw_device_ctx=av_buffer_ref(Hardware);Codec->opaque=this;Codec->get_format=&ChooseFormat;}
        }
        if(mode==0 && !Hardware) {throw std::runtime_error("Hardware required but this device or codec cannot provide D3D11 video decoding.");}
        Check(avcodec_open2(Codec,implementation,nullptr),"Open video codec");
        AVRational rate=av_guess_frame_rate(Format,stream,nullptr);if(rate.num<=0 || rate.den<=0) {throw std::runtime_error("Video frame rate is invalid.");}
        int64_t duration=stream->duration!=AV_NOPTS_VALUE ? av_rescale_q(stream->duration,stream->time_base,{1,10000000}) : Format->duration==AV_NOPTS_VALUE ? 0 : av_rescale_q(Format->duration,AV_TIME_BASE_Q,{1,10000000});
        *info={Codec->width,Codec->height,av_q2d(rate),std::max<int64_t>(0,duration),Hardware ? 1 : 2,Hardware ? 1 : 0};
    }
    int Read(he_video_frame* output) {
        if(!output) {throw std::runtime_error("Video frame output is required.");}
        for(;;) {
            int received=avcodec_receive_frame(Codec,Frame);
            if(received==0) {MakeOutput(output);av_frame_unref(Frame);return 1;}
            if(received==AVERROR_EOF) {return 0;}
            if(received!=AVERROR(EAGAIN)) {Check(received,"Decode video frame");}
            if(Draining) {throw std::runtime_error("Decoder requested input after drain.");}
            int packetResult;
            do {av_packet_unref(Packet);packetResult=av_read_frame(Format,Packet);} while(packetResult>=0 && Packet->stream_index!=StreamIndex);
            if(packetResult==AVERROR_EOF) {Check(avcodec_send_packet(Codec,nullptr),"Drain video codec");Draining=true;}
            else {Check(packetResult,"Read video packet");Check(avcodec_send_packet(Codec,Packet),"Send video packet");av_packet_unref(Packet);}
        }
    }
    void MakeOutput(he_video_frame* output) {
        AVStream* stream=Format->streams[StreamIndex];int64_t pts=Frame->best_effort_timestamp;
        if(pts==AV_NOPTS_VALUE) {throw std::runtime_error("Video frame has no presentation timestamp.");}
        int64_t timestamp=av_rescale_q(pts-Origin,stream->time_base,{1,10000000});
        int64_t duration=Frame->duration>0 ? av_rescale_q(Frame->duration,stream->time_base,{1,10000000}) : av_rescale_q(1,av_inv_q(av_guess_frame_rate(Format,stream,Frame)),{1,10000000});
        ID3D11Texture2D* texture=nullptr;AVFrame* held=nullptr;int subresource=0;int format=2;
        if(Frame->format==AV_PIX_FMT_D3D11) {
            texture=reinterpret_cast<ID3D11Texture2D*>(Frame->data[0]);subresource=static_cast<int>(reinterpret_cast<intptr_t>(Frame->data[1]));format=1;
            held=av_frame_clone(Frame);if(!held) {throw std::bad_alloc();}texture->AddRef();
        } else {
            if(Mode==0) {throw std::runtime_error("Hardware required but the codec produced a software frame.");}
            Converter=sws_getCachedContext(Converter,Frame->width,Frame->height,static_cast<AVPixelFormat>(Frame->format),Frame->width,Frame->height,AV_PIX_FMT_RGBA,SWS_BILINEAR,nullptr,nullptr,nullptr);
            if(!Converter) {throw std::runtime_error("Video color converter could not initialize.");}
            std::vector<uint8_t> pixels(static_cast<size_t>(Frame->width)*Frame->height*4);
            uint8_t* data[]={pixels.data(),nullptr,nullptr,nullptr};int strides[]={Frame->width*4,0,0,0};
            int colorspace=Frame->colorspace==AVCOL_SPC_BT709 ? SWS_CS_ITU709 : SWS_CS_ITU601;
            sws_setColorspaceDetails(Converter,sws_getCoefficients(colorspace),Frame->color_range==AVCOL_RANGE_JPEG,sws_getCoefficients(colorspace),1,0,1<<16,1<<16);
            if(sws_scale(Converter,Frame->data,Frame->linesize,0,Frame->height,data,strides)!=Frame->height) {throw std::runtime_error("Video color conversion failed.");}
            D3D11_TEXTURE2D_DESC description={};description.Width=Frame->width;description.Height=Frame->height;description.MipLevels=1;description.ArraySize=1;description.Format=DXGI_FORMAT_R8G8B8A8_UNORM;description.SampleDesc.Count=1;description.Usage=D3D11_USAGE_DEFAULT;description.BindFlags=D3D11_BIND_SHADER_RESOURCE;
            D3D11_SUBRESOURCE_DATA initial={};initial.pSysMem=pixels.data();initial.SysMemPitch=Frame->width*4;
            if(FAILED(Device->CreateTexture2D(&description,&initial,&texture))) {throw std::runtime_error("Decoded video texture allocation failed.");}
        }
        *output={texture,subresource,Frame->width,Frame->height,format,timestamp,std::max<int64_t>(0,duration)};
        Held.push_back({*output,held,texture});
    }
    void Seek(int64_t ticks) {
        if(ticks<0) {throw std::runtime_error("Seek time cannot be negative.");}
        AVStream* stream=Format->streams[StreamIndex];int64_t target=Origin+av_rescale_q(ticks,{1,10000000},stream->time_base);
        Check(av_seek_frame(Format,StreamIndex,target,AVSEEK_FLAG_BACKWARD),"Seek video");Flush();
    }
    void Flush() {avcodec_flush_buffers(Codec);av_frame_unref(Frame);av_packet_unref(Packet);Draining=false;}
    void Release(he_video_frame* frame) {
        auto found=std::find_if(Held.begin(),Held.end(),[frame](const HeldFrame& item) {return item.Info.Texture==frame->Texture && item.Info.SubresourceIndex==frame->SubresourceIndex && item.Info.TimestampTicks==frame->TimestampTicks;});
        if(found!=Held.end()) {av_frame_free(&found->Frame);found->Texture->Release();Held.erase(found);}*frame={};
    }
};
extern "C" __declspec(dllexport) const char* __cdecl he_media_last_error() {return LastError.c_str();}
extern "C" __declspec(dllexport) void* __cdecl he_video_decoder_create(void* device,const wchar_t* source,int mode,he_video_stream_info* info) {
    try {LastError.clear();auto decoder=std::make_unique<NativeVideoDecoder>();decoder->Open(device,source,mode,info);return decoder.release();}catch(const std::exception& error) {LastError=error.what();return nullptr;}
}
extern "C" __declspec(dllexport) int __cdecl he_video_decoder_try_get_frame(void* decoder,he_video_frame* frame) {
    try {if(!decoder || !frame) {throw std::runtime_error("Decoder handle and frame output are required.");}return static_cast<NativeVideoDecoder*>(decoder)->Read(frame);}catch(const std::exception& error) {LastError=error.what();return -1;}
}
extern "C" __declspec(dllexport) int __cdecl he_video_decoder_seek(void* decoder,int64_t ticks) {
    try {if(!decoder) {throw std::runtime_error("Decoder handle is required.");}static_cast<NativeVideoDecoder*>(decoder)->Seek(ticks);return 1;}catch(const std::exception& error) {LastError=error.what();return 0;}
}
extern "C" __declspec(dllexport) void __cdecl he_video_decoder_flush(void* decoder) {if(decoder) {static_cast<NativeVideoDecoder*>(decoder)->Flush();}}
extern "C" __declspec(dllexport) void __cdecl he_video_decoder_release_frame(void* decoder,he_video_frame* frame) {if(decoder && frame) {static_cast<NativeVideoDecoder*>(decoder)->Release(frame);}}
extern "C" __declspec(dllexport) void __cdecl he_video_decoder_destroy(void* decoder) {delete static_cast<NativeVideoDecoder*>(decoder);}
