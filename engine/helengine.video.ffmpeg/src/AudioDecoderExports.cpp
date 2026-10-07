#include <windows.h>
#include <string>
#include <vector>
#include <memory>
#include <stdexcept>
#include <algorithm>
extern "C" {
#include <libavformat/avformat.h>
#include <libavcodec/avcodec.h>
#include <libswresample/swresample.h>
}
extern thread_local std::string LastError;
// Audio cursors own codec/resampler state and retain at most one converted source frame.
class NativeAudioDecoder {
public:
    AVFormatContext* Format=nullptr;
    AVCodecContext* Codec=nullptr;
    AVFrame* Frame=av_frame_alloc();
    AVPacket* Packet=av_packet_alloc();
    SwrContext* Converter=nullptr;
    int StreamIndex=-1;
    int Rate=48000;
    int Channels=2;
    int64_t Origin=0;
    int64_t Cursor=0;
    int64_t ChunkStart=0;
    bool Anchored=false;
    bool Draining=false;
    bool Ended=false;
    bool ConvertedEnd=false;
    std::vector<float> Chunk;
    ~NativeAudioDecoder() {swr_free(&Converter);av_frame_free(&Frame);av_packet_free(&Packet);avcodec_free_context(&Codec);avformat_close_input(&Format);}
    static void Check(int result,const char* operation) {
        if(result<0) {char error[AV_ERROR_MAX_STRING_SIZE];av_strerror(result,error,sizeof(error));throw std::runtime_error(std::string(operation)+": "+error);}
    }
    void Open(const wchar_t* source,int rate,int channels) {
        if(!source || rate<8000 || rate>192000 || channels!=2 || !Frame || !Packet) {throw std::runtime_error("Invalid audio source configuration.");}
        Rate=rate;Channels=channels;
        int length=WideCharToMultiByte(CP_UTF8,WC_ERR_INVALID_CHARS,source,-1,nullptr,0,nullptr,nullptr);
        if(length<=1) {throw std::runtime_error("A valid media path is required.");}
        std::string path(length,'\0');WideCharToMultiByte(CP_UTF8,WC_ERR_INVALID_CHARS,source,-1,path.data(),length,nullptr,nullptr);path.resize(length-1);
        Check(avformat_open_input(&Format,path.c_str(),nullptr,nullptr),"Open audio source");Check(avformat_find_stream_info(Format,nullptr),"Probe audio source");
        StreamIndex=av_find_best_stream(Format,AVMEDIA_TYPE_AUDIO,-1,-1,nullptr,0);
        if(StreamIndex==AVERROR_STREAM_NOT_FOUND) {StreamIndex=-1;return;}
        Check(StreamIndex,"Find audio stream");AVStream* stream=Format->streams[StreamIndex];
        const AVCodec* implementation=avcodec_find_decoder(stream->codecpar->codec_id);if(!implementation) {throw std::runtime_error("Audio codec is unavailable.");}
        Codec=avcodec_alloc_context3(implementation);if(!Codec) {throw std::bad_alloc();}
        Check(avcodec_parameters_to_context(Codec,stream->codecpar),"Configure audio codec");Codec->pkt_timebase=stream->time_base;Check(avcodec_open2(Codec,implementation,nullptr),"Open audio codec");
        int visualIndex=av_find_best_stream(Format,AVMEDIA_TYPE_VIDEO,-1,-1,nullptr,0);
        AVStream* originStream=visualIndex>=0 ? Format->streams[visualIndex] : stream;
        Origin=originStream->start_time!=AV_NOPTS_VALUE ? av_rescale_q(originStream->start_time,originStream->time_base,{1,Rate}) : 0;
        Configure();
    }
    void Configure() {
        AVChannelLayout output;av_channel_layout_default(&output,Channels);
        Check(swr_alloc_set_opts2(&Converter,&output,AV_SAMPLE_FMT_FLT,Rate,&Codec->ch_layout,Codec->sample_fmt,Codec->sample_rate,0,nullptr),"Configure audio conversion");av_channel_layout_uninit(&output);
        if(Codec->ch_layout.nb_channels==1 && Channels==2) {double matrix[]={1.0,1.0};Check(swr_set_matrix(Converter,matrix,1),"Configure mono duplication");}
        Check(swr_init(Converter),"Initialize audio conversion");
    }
    void Reset() {
        AVStream* stream=Format->streams[StreamIndex];int64_t first=stream->start_time==AV_NOPTS_VALUE ? 0 : stream->start_time;
        Check(av_seek_frame(Format,StreamIndex,first,AVSEEK_FLAG_BACKWARD),"Reset audio cursor");avcodec_flush_buffers(Codec);av_frame_unref(Frame);av_packet_unref(Packet);
        swr_free(&Converter);Configure();Chunk.clear();ChunkStart=0;Cursor=0;Anchored=false;Draining=false;Ended=false;ConvertedEnd=false;
    }
    bool NextChunk() {
        if(ConvertedEnd) {return false;}
        for(;;) {
            int received=Ended ? AVERROR_EOF : avcodec_receive_frame(Codec,Frame);
            if(received==0 || received==AVERROR_EOF) {
                bool end=received==AVERROR_EOF;Ended=end;
                if(!Anchored && !end) {
                    if(Frame->best_effort_timestamp==AV_NOPTS_VALUE) {throw std::runtime_error("Audio frame has no presentation timestamp.");}
                    Cursor=av_rescale_q(Frame->best_effort_timestamp,Format->streams[StreamIndex]->time_base,{1,Rate})-Origin;Anchored=true;
                }
                int capacity=swr_get_out_samples(Converter,end ? 0 : Frame->nb_samples);if(capacity<=0) {capacity=32;}
                if(capacity>1048576) {throw std::runtime_error("Audio source frame exceeds the bounded conversion buffer.");}
                Chunk.resize(static_cast<size_t>(capacity)*Channels);uint8_t* output=reinterpret_cast<uint8_t*>(Chunk.data());
                int converted=swr_convert(Converter,&output,capacity,end ? nullptr : const_cast<const uint8_t**>(Frame->extended_data),end ? 0 : Frame->nb_samples);
                Check(converted,"Convert audio samples");av_frame_unref(Frame);Chunk.resize(static_cast<size_t>(converted)*Channels);ChunkStart=Cursor;Cursor+=converted;
                if(converted>0) {return true;}
                if(end) {ConvertedEnd=true;return false;}
                continue;
            }
            if(received!=AVERROR(EAGAIN)) {Check(received,"Decode audio frame");}
            if(Draining) {throw std::runtime_error("Audio decoder requested packets after drain.");}
            int result;do {av_packet_unref(Packet);result=av_read_frame(Format,Packet);} while(result>=0 && Packet->stream_index!=StreamIndex);
            if(result==AVERROR_EOF) {Check(avcodec_send_packet(Codec,nullptr),"Drain audio codec");Draining=true;}
            else {Check(result,"Read audio packet");Check(avcodec_send_packet(Codec,Packet),"Send audio packet");av_packet_unref(Packet);}
        }
    }
    int Read(int64_t first,int count,float* output) {
        if(first<0 || count<0 || count>65536 || !output) {throw std::runtime_error("Audio interval is outside supported block limits.");}
        std::fill(output,output+static_cast<size_t>(count)*Channels,0.0f);if(StreamIndex<0) {return 0;}
        if(Anchored && first<ChunkStart) {Reset();}
        int written=0;int64_t requestedEnd=first+count;
        while(written<count) {
            int64_t chunkEnd=ChunkStart+static_cast<int64_t>(Chunk.size()/Channels);
            if(Chunk.empty() || chunkEnd<=first+written) {if(!NextChunk()) {break;}continue;}
            if(ChunkStart>=requestedEnd) {break;}
            if(first+written<ChunkStart) {written+=static_cast<int>(std::min<int64_t>(ChunkStart-(first+written),count-written));continue;}
            int take=static_cast<int>(std::min<int64_t>(chunkEnd-(first+written),count-written));
            std::copy_n(Chunk.data()+static_cast<size_t>(first+written-ChunkStart)*Channels,static_cast<size_t>(take)*Channels,output+static_cast<size_t>(written)*Channels);written+=take;
        }
        return written;
    }
};
extern "C" __declspec(dllexport) void* __cdecl he_audio_decoder_create(const wchar_t* source,int rate,int channels,int* hasAudio) {
    try {if(!hasAudio) {throw std::runtime_error("Audio metadata output is required.");}auto decoder=std::make_unique<NativeAudioDecoder>();decoder->Open(source,rate,channels);*hasAudio=decoder->StreamIndex>=0;return decoder.release();}catch(const std::exception& error) {LastError=error.what();return nullptr;}
}
extern "C" __declspec(dllexport) int __cdecl he_audio_decoder_read(void* decoder,int64_t first,int count,float* output) {
    try {if(!decoder) {throw std::runtime_error("Audio cursor is required.");}return static_cast<NativeAudioDecoder*>(decoder)->Read(first,count,output);}catch(const std::exception& error) {LastError=error.what();return -1;}
}
extern "C" __declspec(dllexport) void __cdecl he_audio_decoder_destroy(void* decoder) {delete static_cast<NativeAudioDecoder*>(decoder);}
