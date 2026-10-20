#include <windows.h>
#include <d3d11.h>
#include <dxgi1_2.h>
#include <wrl/client.h>
#include <filesystem>
#include <iostream>
#include <vector>
#include <stdexcept>
#include <functional>
#include <string>
#include <fstream>
#include "EncoderSurfacePool.h"
extern "C" {
#include <libavformat/avformat.h>
#include <libavcodec/avcodec.h>
#include <libswscale/swscale.h>
}
using Microsoft::WRL::ComPtr;
// Independent ABI declarations make missing exports a runtime RED instead of a linker error.
struct Options { uint32_t Size=32; int32_t Width=640; int32_t Height=480; int64_t Bitrate=40000000; int64_t MaxBitrate=60000000; };
struct Error { int32_t Code=0; char Message[1024]{}; };
using CreateFn=int(__cdecl*)(void*,const wchar_t*,const Options*,void**,Error*);
using SubmitFn=int(__cdecl*)(void*,void*,int64_t,Error*);
using CompleteFn=int(__cdecl*)(void*,Error*);
using DestroyFn=void(__cdecl*)(void*);
static CreateFn Create;
static SubmitFn Submit;
static CompleteFn Complete;
static DestroyFn Destroy;
static void Require(bool condition,const char* message) { if(!condition) { throw std::runtime_error(message); } }
static void Check(int code,const Error& error) { if(code!=0) { throw std::runtime_error(std::to_string(error.Code)+": "+error.Message); } }
static ComPtr<ID3D11Device> MakeDevice() {
    ComPtr<IDXGIFactory1> factory; Require(SUCCEEDED(CreateDXGIFactory1(IID_PPV_ARGS(&factory))),"DXGI factory");
    for(UINT i=0;;i++) {
        ComPtr<IDXGIAdapter1> adapter; if(factory->EnumAdapters1(i,&adapter)==DXGI_ERROR_NOT_FOUND) { break; }
        DXGI_ADAPTER_DESC1 desc{}; adapter->GetDesc1(&desc);
        if(desc.VendorId!=0x10DE || (desc.Flags&DXGI_ADAPTER_FLAG_SOFTWARE)) { continue; }
        ComPtr<ID3D11Device> device;
        Require(SUCCEEDED(D3D11CreateDevice(adapter.Get(),D3D_DRIVER_TYPE_UNKNOWN,nullptr,D3D11_CREATE_DEVICE_BGRA_SUPPORT|D3D11_CREATE_DEVICE_VIDEO_SUPPORT,nullptr,0,D3D11_SDK_VERSION,&device,nullptr,nullptr)),"NVIDIA device creation");
        return device;
    }
    throw std::runtime_error("No NVIDIA hardware adapter; GPU tests are not approved.");
}
static ComPtr<ID3D11Texture2D> Texture(ID3D11Device* device,int width,int height,int color,DXGI_FORMAT format=DXGI_FORMAT_B8G8R8A8_UNORM) {
    std::vector<uint32_t> pixels(static_cast<size_t>(width)*height,0xff000000u|color);
    D3D11_TEXTURE2D_DESC desc{}; desc.Width=width; desc.Height=height; desc.MipLevels=1; desc.ArraySize=1; desc.Format=format; desc.SampleDesc.Count=1; desc.Usage=D3D11_USAGE_DEFAULT; desc.BindFlags=D3D11_BIND_RENDER_TARGET;
    D3D11_SUBRESOURCE_DATA data{}; data.pSysMem=pixels.data(); data.SysMemPitch=width*4;
    ComPtr<ID3D11Texture2D> texture; Require(SUCCEEDED(device->CreateTexture2D(&desc,&data,&texture)),"Synthetic texture"); return texture;
}
// Test-only CPU decode verifies actual pixels and timestamps, not just encoder counters.
static void AssertVideo(const char* path,int width,int height,int count,bool odd=false) {
    std::ifstream atoms(path,std::ios::binary); bool seenMoov=false; bool seenMdat=false;
    while(atoms) {
        unsigned char header[8]{}; atoms.read(reinterpret_cast<char*>(header),8); if(!atoms) { break; }
        uint32_t size=(uint32_t(header[0])<<24)|(uint32_t(header[1])<<16)|(uint32_t(header[2])<<8)|header[3]; Require(size>=8,"Top-level MP4 atom size");
        if(memcmp(header+4,"moov",4)==0) { Require(!seenMdat,"Faststart moov precedes mdat"); seenMoov=true; }
        if(memcmp(header+4,"mdat",4)==0) { seenMdat=true; }
        atoms.seekg(size-8,std::ios::cur);
    }
    Require(seenMoov && seenMdat,"Finalized MP4 atoms");
    AVFormatContext* format=nullptr; Require(avformat_open_input(&format,path,nullptr,nullptr)>=0,"Open encoded MP4");
    Require(avformat_find_stream_info(format,nullptr)>=0,"Probe encoded MP4");
    Require(format->nb_streams==1,"Video only"); AVStream* stream=format->streams[0];
    Require(stream->codecpar->codec_id==AV_CODEC_ID_HEVC,"HEVC codec"); Require(stream->codecpar->codec_tag==MKTAG('h','v','c','1'),"hvc1 sample entry");
    Require(stream->codecpar->width==width && stream->codecpar->height==height,"Output dimensions");
    Require(stream->avg_frame_rate.num==60*stream->avg_frame_rate.den,"60/1 frame rate");
    const AVCodec* implementation=avcodec_find_decoder(AV_CODEC_ID_HEVC); AVCodecContext* codec=avcodec_alloc_context3(implementation);
    avcodec_parameters_to_context(codec,stream->codecpar); Require(avcodec_open2(codec,implementation,nullptr)>=0,"Open test decoder");
    AVPacket* packet=av_packet_alloc(); AVFrame* frame=av_frame_alloc(); int decoded=0; int packets=0;
    SwsContext* converter=nullptr; std::vector<uint8_t> rgb(static_cast<size_t>(width)*height*4);
    while(true) {
        int read=av_read_frame(format,packet);
        if(read>=0) {
            Require(packet->pts==av_rescale_q(packets,{1,60},stream->time_base),"Packet PTS spacing"); packets++;
            Require(avcodec_send_packet(codec,packet)>=0,"Send test packet"); av_packet_unref(packet);
        } else { avcodec_send_packet(codec,nullptr); }
        while(avcodec_receive_frame(codec,frame)>=0) {
            Require(frame->format==AV_PIX_FMT_YUV420P,"Main 8-bit 420");
            Require(frame->color_range==AVCOL_RANGE_MPEG && frame->colorspace==AVCOL_SPC_BT709,"BT709 limited VUI");
            converter=sws_getCachedContext(converter,width,height,static_cast<AVPixelFormat>(frame->format),width,height,AV_PIX_FMT_BGRA,SWS_POINT,nullptr,nullptr,nullptr);
            const int* matrix=sws_getCoefficients(SWS_CS_ITU709); sws_setColorspaceDetails(converter,matrix,0,matrix,1,0,1<<16,1<<16);
            uint8_t* output[]={rgb.data()}; int strides[]={width*4}; sws_scale(converter,frame->data,frame->linesize,0,height,output,strides);
            const int colors[]={0x000000,0xffffff,0xff0000,0x00ff00,0x0000ff}; int expected=colors[decoded%5];
            size_t center=(static_cast<size_t>(height/2)*width+width/2)*4;
            for(int channel=0;channel<3;channel++) { Require(abs(static_cast<int>(rgb[center+channel])-((expected>>(channel*8))&255))<=12,"Decoded RGB color/lifetime"); }
            if(odd) {
                // Border chroma is shared with the last content pixel; inspect luma independently.
                Require(abs(frame->data[0][(height-1)*frame->linesize[0]+width-1]-16)<=6,"Black padded corner luma");
            }
            decoded++; av_frame_unref(frame);
        }
        if(read<0) { break; }
    }
    Require(decoded==count && packets==count,"Complete frame count including final frame");
    sws_freeContext(converter); av_frame_free(&frame); av_packet_free(&packet); avcodec_free_context(&codec); avformat_close_input(&format);
}
static void CreatesHevcMain60Mp4FromGpuTextures() {
    auto device=MakeDevice(); Options options; Error error; void* encoder=nullptr;
    const wchar_t* path=L"C:\\temp\\helengine-window-recording\\native-color.mp4";
    Check(Create(device.Get(),path,&options,&encoder,&error),error);
    try {
        const int colors[]={0x000000,0xffffff,0xff0000,0x00ff00,0x0000ff};
        for(int i=0;i<60;i++) { auto texture=Texture(device.Get(),640,480,colors[i%5]); Check(Submit(encoder,texture.Get(),i,&error),error); }
        Check(Complete(encoder,&error),error); Check(Complete(encoder,&error),error);
        auto texture=Texture(device.Get(),640,480,0); Require(Submit(encoder,texture.Get(),60,&error)!=0,"Submit after Complete rejected");
    } catch(...) { Destroy(encoder); throw; }
    Destroy(encoder); AssertVideo("C:\\temp\\helengine-window-recording\\native-color.mp4",640,480,60);
}
static void RejectsInvalidDeviceFormatDimensionsAndIndex() {
    auto device=MakeDevice(); Options options; Error error; void* encoder=nullptr;
    Require(Create(nullptr,L"unused",&options,&encoder,&error)!=0 && !encoder,"Null device rejected");
    options.MaxBitrate=1; Require(Create(device.Get(),L"unused",&options,&encoder,&error)!=0,"Invalid bitrate rejected"); options.MaxBitrate=60000000;
    Check(Create(device.Get(),L"C:\\temp\\helengine-window-recording\\native-validation.mp4",&options,&encoder,&error),error);
    auto valid=Texture(device.Get(),640,480,0); auto badSize=Texture(device.Get(),642,480,0); auto badFormat=Texture(device.Get(),640,480,0,DXGI_FORMAT_R8G8B8A8_UNORM);
    auto otherDevice=MakeDevice(); auto other=Texture(otherDevice.Get(),640,480,0);
    Require(Submit(encoder,valid.Get(),1,&error)!=0,"Initial index must be zero");
    Require(Submit(encoder,badSize.Get(),0,&error)!=0,"Resize rejected"); Require(Submit(encoder,badFormat.Get(),0,&error)!=0,"Format rejected"); Require(Submit(encoder,other.Get(),0,&error)!=0,"Different device rejected");
    Check(Submit(encoder,valid.Get(),0,&error),error); Require(Submit(encoder,valid.Get(),0,&error)!=0,"Repeated index rejected"); Check(Complete(encoder,&error),error); Destroy(encoder);
}
static void OwnsSubmittedPixelsAndBoundsPool() {
    auto device=MakeDevice(); Options options; options.Width=641; options.Height=479; Error error; void* encoder=nullptr;
    Check(Create(device.Get(),L"C:\\temp\\helengine-window-recording\\native-odd.mp4",&options,&encoder,&error),error);
    const int colors[]={0x000000,0xffffff,0xff0000,0x00ff00,0x0000ff};
    // Every source is released before the next submission. Fixed pool exhaustion must fail, never grow.
    for(int i=0;i<180;i++) { auto texture=Texture(device.Get(),641,479,colors[i%5]); Check(Submit(encoder,texture.Get(),i,&error),error); }
    Check(Complete(encoder,&error),error); Destroy(encoder); AssertVideo("C:\\temp\\helengine-window-recording\\native-odd.mp4",642,480,180,true);
}
static void FixedPoolRefusesNinthInFlightSurface() {
    auto device=MakeDevice(); EncoderSurfacePool pool; pool.Open(device.Get(),640,480); AVFrame* frames[9]{};
    for(int i=0;i<9;i++) { frames[i]=av_frame_alloc(); Require(frames[i]!=nullptr,"Allocate test AVFrame"); }
    for(int i=0;i<8;i++) { Require(av_hwframe_get_buffer(pool.Frames,frames[i],0)==0,"Acquire within eight-surface budget"); }
    Require(av_hwframe_get_buffer(pool.Frames,frames[8],0)<0,"Ninth concurrent surface must not allocate");
    av_frame_unref(frames[0]); Require(av_hwframe_get_buffer(pool.Frames,frames[8],0)==0,"Released surface is reused");
    for(auto*& frame:frames) { av_frame_free(&frame); }
}
static void ReportsErrorsPerInstance() {
    auto device=MakeDevice(); Options options; Error first; Error second; void* encoder=nullptr;
    Require(Create(device.Get(),L"C:\\temp\\helengine-window-recording\\missing\\clip.mp4",&options,&encoder,&first)!=0,"Bad path rejected");
    Require(first.Code!=0 && first.Message[0],"Original diagnostic");
    Require(Create(nullptr,L"x",&options,&encoder,&second)!=0,"Second independent failure"); Require(std::string(first.Message).find("MP4")!=std::string::npos,"Earlier error remains independent");
}
int main() {
    try {
        auto module=LoadLibraryW(L"helengine.video.ffmpeg.dll"); Require(module!=nullptr,"Load native backend");
        Create=reinterpret_cast<CreateFn>(GetProcAddress(module,"he_video_encoder_create")); Submit=reinterpret_cast<SubmitFn>(GetProcAddress(module,"he_video_encoder_submit")); Complete=reinterpret_cast<CompleteFn>(GetProcAddress(module,"he_video_encoder_complete")); Destroy=reinterpret_cast<DestroyFn>(GetProcAddress(module,"he_video_encoder_destroy"));
        Require(Create && Submit && Complete && Destroy,"FAIL: encoder exports missing");
        const std::pair<const char*,std::function<void()>> tests[]={{"CreatesHevcMain60Mp4FromGpuTextures",CreatesHevcMain60Mp4FromGpuTextures},{"RejectsInvalidDeviceFormatDimensionsAndIndex",RejectsInvalidDeviceFormatDimensionsAndIndex},{"OwnsSubmittedPixelsAndBoundsPool",OwnsSubmittedPixelsAndBoundsPool},{"ReportsErrorsPerInstance",ReportsErrorsPerInstance},{"FixedPoolRefusesNinthInFlightSurface",FixedPoolRefusesNinthInFlightSurface}};
        int failed=0; for(const auto& test:tests) { try { test.second(); std::cout<<"PASS "<<test.first<<std::endl; } catch(const std::exception& error) { failed++; std::cerr<<"FAIL "<<test.first<<": "<<error.what()<<std::endl; } }
        std::cout<<(5-failed)<<"/5 native encoder tests passed"<<std::endl; return failed ? 1 : 0;
    } catch(const std::exception& error) { std::cerr<<error.what()<<std::endl; return 1; }
}
