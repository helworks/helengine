#include <windows.h>
#include <string>
#include <stdexcept>
#include "helengine_video_ffmpeg.h"
extern "C" {
#include <libavformat/avformat.h>
}
extern thread_local std::string LastError;
// Probes timed visual metadata without requiring a D3D device or allocating decode surfaces.
extern "C" __declspec(dllexport) int __cdecl he_video_probe(const wchar_t* source,he_video_stream_info* info) {
    AVFormatContext* format=nullptr;
    try {
        if(!source || !info) {throw std::runtime_error("Media path and probe output are required.");}
        int length=WideCharToMultiByte(CP_UTF8,WC_ERR_INVALID_CHARS,source,-1,nullptr,0,nullptr,nullptr);
        if(length<=1) {throw std::runtime_error("A valid media path is required.");}
        std::string path(length,'\0');WideCharToMultiByte(CP_UTF8,WC_ERR_INVALID_CHARS,source,-1,path.data(),length,nullptr,nullptr);path.resize(length-1);
        int result=avformat_open_input(&format,path.c_str(),nullptr,nullptr);
        if(result>=0) {result=avformat_find_stream_info(format,nullptr);}
        if(result<0) {char error[AV_ERROR_MAX_STRING_SIZE];av_strerror(result,error,sizeof(error));throw std::runtime_error(error);}
        int index=av_find_best_stream(format,AVMEDIA_TYPE_VIDEO,-1,-1,nullptr,0);
        if(index<0) {throw std::runtime_error("No supported video stream was found.");}
        AVStream* stream=format->streams[index];AVRational rate=av_guess_frame_rate(format,stream,nullptr);
        int64_t duration=stream->duration!=AV_NOPTS_VALUE ? av_rescale_q(stream->duration,stream->time_base,{1,10000000}) : format->duration!=AV_NOPTS_VALUE ? av_rescale_q(format->duration,AV_TIME_BASE_Q,{1,10000000}) : 0;
        *info={stream->codecpar->width,stream->codecpar->height,av_q2d(rate),duration,2,0};
        avformat_close_input(&format);return 1;
    } catch(const std::exception& error) {LastError=error.what();avformat_close_input(&format);return 0;}
}
