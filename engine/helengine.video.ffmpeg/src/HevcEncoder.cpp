#include "HevcEncoder.h"
#include "EncoderFailure.h"
#include <limits>
HevcEncoder::~HevcEncoder() {
    av_frame_free(&Frame); av_packet_free(&Packet); avcodec_free_context(&Codec);
    if(Format) { if(Format->pb) { avio_closep(&Format->pb); } avformat_free_context(Format); }
}
void HevcEncoder::Open(ID3D11Device* device,const wchar_t* path,const he_video_encoder_options& options) {
    EncoderFailure::Require(device && options.size==sizeof(options) && options.source_width>0 && options.source_height>0 && options.source_width<=16384 && options.source_height<=16384 && options.bitrate>0 && options.max_bitrate>=options.bitrate,"Invalid HEVC encoder options or device.");
    auto outputPath=EncoderFailure::Utf8(path); Pool.Open(device,options.source_width,options.source_height);
    const AVCodec* implementation=avcodec_find_encoder_by_name("hevc_nvenc"); EncoderFailure::Require(implementation!=nullptr,"Required encoder hevc_nvenc is unavailable.");
    Codec=avcodec_alloc_context3(implementation); Frame=av_frame_alloc(); Packet=av_packet_alloc(); if(!Codec || !Frame || !Packet) { throw std::bad_alloc(); }
    Codec->width=(options.source_width+1)&~1; Codec->height=(options.source_height+1)&~1; Codec->pix_fmt=AV_PIX_FMT_D3D11; Codec->time_base={1,60}; Codec->framerate={60,1}; Codec->gop_size=120; Codec->max_b_frames=0;
    Codec->bit_rate=options.bitrate; Codec->rc_max_rate=options.max_bitrate; Codec->rc_buffer_size=static_cast<int>(std::min<int64_t>(options.max_bitrate,std::numeric_limits<int>::max()));
    Codec->color_range=AVCOL_RANGE_MPEG; Codec->colorspace=AVCOL_SPC_BT709; Codec->color_primaries=AVCOL_PRI_BT709; Codec->color_trc=AVCOL_TRC_BT709;
    Codec->hw_frames_ctx=av_buffer_ref(Pool.Frames); if(!Codec->hw_frames_ctx) { throw std::bad_alloc(); }
    EncoderFailure::Check(avformat_alloc_output_context2(&Format,nullptr,"mp4",outputPath.c_str()),"Allocate MP4 muxer"); if(!Format) { throw std::bad_alloc(); }
    if(Format->oformat->flags&AVFMT_GLOBALHEADER) { Codec->flags|=AV_CODEC_FLAG_GLOBAL_HEADER; }
    AVDictionary* settings=nullptr; av_dict_set(&settings,"preset","p5",0); av_dict_set(&settings,"tune","hq",0); av_dict_set(&settings,"profile","main",0); av_dict_set(&settings,"rc","vbr",0); av_dict_set(&settings,"rc-lookahead","0",0); av_dict_set(&settings,"surfaces","8",0); av_dict_set(&settings,"delay","0",0);
    int opened=avcodec_open2(Codec,implementation,&settings); bool leftover=av_dict_count(settings)!=0; av_dict_free(&settings); EncoderFailure::Check(opened,"Open required hevc_nvenc encoder"); EncoderFailure::Require(!leftover,"NVENC refused an encoding option.");
    Stream=avformat_new_stream(Format,nullptr); if(!Stream) { throw std::bad_alloc(); } Stream->time_base={1,60}; Stream->avg_frame_rate={60,1};
    EncoderFailure::Check(avcodec_parameters_from_context(Stream->codecpar,Codec),"Copy HEVC parameters"); Stream->codecpar->codec_tag=MKTAG('h','v','c','1');
    EncoderFailure::Check(avio_open(&Format->pb,outputPath.c_str(),AVIO_FLAG_WRITE),"Open MP4 output");
    AVDictionary* muxSettings=nullptr; av_dict_set(&muxSettings,"movflags","+faststart",0); int header=avformat_write_header(Format,&muxSettings); bool muxLeftover=av_dict_count(muxSettings)!=0; av_dict_free(&muxSettings); EncoderFailure::Check(header,"Write MP4 header"); EncoderFailure::Require(!muxLeftover,"MP4 muxer refused faststart.");
}
void HevcEncoder::Receive(bool draining) {
    for(;;) {
        int result=avcodec_receive_packet(Codec,Packet);
        if(result==AVERROR_EOF) { return; }
        if(result==AVERROR(EAGAIN)) { EncoderFailure::Require(!draining,"Encoder did not drain to EOF."); return; }
        EncoderFailure::Check(result,"Receive HEVC packet");
        if(Packet->duration<=0) { Packet->duration=1; }
        av_packet_rescale_ts(Packet,Codec->time_base,Stream->time_base); Packet->stream_index=Stream->index;
        int written=av_interleaved_write_frame(Format,Packet); av_packet_unref(Packet); EncoderFailure::Check(written,"Write MP4 packet");
    }
}
void HevcEncoder::Submit(ID3D11Texture2D* texture,int64_t index) {
    EncoderFailure::Require(!Completed && !Faulted,"Encoder is complete or faulted."); EncoderFailure::Require(index==NextIndex,"Frame index must be consecutive, starting at zero.");
    // Invalid borrowed input remains recoverable; codec/mux failures poison the session.
    try { Pool.Convert(texture,Frame); } catch(...) { av_frame_unref(Frame); throw; }
    Frame->pts=index; Frame->duration=1; Frame->color_range=Codec->color_range; Frame->colorspace=Codec->colorspace; Frame->color_primaries=Codec->color_primaries; Frame->color_trc=Codec->color_trc;
    try {
        int sent=avcodec_send_frame(Codec,Frame);
        if(sent==AVERROR(EAGAIN)) { Receive(false); sent=avcodec_send_frame(Codec,Frame); }
        EncoderFailure::Check(sent,"Submit HEVC GPU frame"); av_frame_unref(Frame); NextIndex++; Receive(false);
    } catch(...) { av_frame_unref(Frame); Faulted=true; throw; }
}
void HevcEncoder::Complete() {
    if(Completed) { return; } EncoderFailure::Require(!Faulted && NextIndex>0,"Cannot finalize an empty or faulted encoder.");
    try { EncoderFailure::Check(avcodec_send_frame(Codec,nullptr),"Drain HEVC encoder"); Receive(true); EncoderFailure::Check(av_write_trailer(Format),"Finalize MP4 trailer"); EncoderFailure::Check(avio_closep(&Format->pb),"Close finalized MP4"); Completed=true; }
    catch(...) { Faulted=true; throw; }
}
