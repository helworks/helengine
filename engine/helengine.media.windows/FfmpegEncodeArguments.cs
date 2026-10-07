using System.Globalization;
namespace helengine.media.windows;
/// <summary>Builds host-controlled encoding arguments without editorial filters, trimming, overlays or mixing.</summary>
public static class FfmpegEncodeArguments {
    /// <summary>Declares raw RGBA and optional mixed PCM, then exact codec and container profiles.</summary>
    public static IReadOnlyList<string> Build(CompositionDocument document,OutputProfile profile,string videoPipe,string audioPipe,string output) {
        var args=new List<string>{"-hide_banner","-loglevel","error","-nostdin","-y","-thread_queue_size","2","-probesize","32","-analyzeduration","0","-f","rawvideo","-pixel_format","rgba","-video_size",profile.Width+"x"+profile.Height,"-framerate",document.FrameRate.Numerator+"/"+document.FrameRate.Denominator,"-i",videoPipe};
        if(document.Audio.Enabled) {args.AddRange(["-thread_queue_size","4","-probesize","32","-analyzeduration","0","-f","f32le","-ar",document.Audio.SampleRate.ToString(CultureInfo.InvariantCulture),"-ac",document.Audio.Channels.ToString(CultureInfo.InvariantCulture),"-i",audioPipe]);}
        args.AddRange(["-map","0:v:0"]);if(document.Audio.Enabled) {args.AddRange(["-map","1:a:0"]);}
        if(profile.Id=="mp4-h264-aac.v1") {args.AddRange(["-c:v","libx264","-preset","fast","-crf","20","-pix_fmt","yuv420p"]);if(document.Audio.Enabled) {args.AddRange(["-c:a","aac","-b:a","192k"]);}args.AddRange(["-movflags","+faststart","-f","mp4"]);}
        else if(profile.Id=="mov-prores4444-pcm.v1") {args.AddRange(["-c:v","prores_ks","-profile:v","4","-pix_fmt","yuva444p10le","-alpha_bits","16"]);if(document.Audio.Enabled) {args.AddRange(["-c:a","pcm_f32le"]);}args.AddRange(["-f","mov"]);}else {throw new InvalidDataException("Unknown encode profile.");}args.Add(output);return args;
    }
}
