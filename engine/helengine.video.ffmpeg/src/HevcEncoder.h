#pragma once
#include "EncoderSurfacePool.h"
#include "helengine_video_ffmpeg.h"
extern "C" {
#include <libavformat/avformat.h>
#include <libavcodec/avcodec.h>
}
// Serial native encoder; Complete drains and writes the trailer, destruction only aborts.
class HevcEncoder {
    EncoderSurfacePool Pool;
    AVFormatContext* Format=nullptr;
    AVCodecContext* Codec=nullptr;
    AVStream* Stream=nullptr;
    AVFrame* Frame=nullptr;
    AVPacket* Packet=nullptr;
    int64_t NextIndex=0;
    bool Completed=false;
    bool Faulted=false;
    void Receive(bool draining);
public:
    ~HevcEncoder();
    void Open(ID3D11Device* device,const wchar_t* path,const he_video_encoder_options& options);
    void Submit(ID3D11Texture2D* texture,int64_t index);
    void Complete();
};
