using System.Globalization;
using helengine.media;
namespace helengine.media.linux;
/// <summary>Defines the host-controlled CPU RGBA to silent Main HEVC contract without shell interpolation.</summary>
public static class FfmpegEncodeArguments {
    /// <summary>Exact versioned profile accepted by this backend.</summary>
    public const string ProfileId="mp4-hevc-silent.v1";
    /// <summary>Software encoder whose actual availability is checked before accepting frames.</summary>
    public const string EncoderName="libx265";
    /// <summary>Explicit NVIDIA hardware encoder requiring successful driver and codec initialization.</summary>
    public const string NvidiaEncoderName="hevc_nvenc";
    /// <summary>Builds raw stdin input and explicitly tagged SDR709 MP4 with one allowlisted encoder and no automatic fallback.</summary>
    public static IReadOnlyList<string> Build(CompositionDocument document,OutputProfile profile,string output,string encoderName=EncoderName) {
        Validate(document,profile);ValidateEncoder(encoderName);
        var arguments=new List<string> {"-hide_banner","-loglevel","error","-nostdin","-y","-filter_threads","2","-f","rawvideo","-pixel_format","rgba",
            "-video_size",profile.Width.ToString(CultureInfo.InvariantCulture)+"x"+profile.Height.ToString(CultureInfo.InvariantCulture),
            "-framerate","60/1","-i","pipe:0","-map","0:v:0","-an","-sn","-dn","-c:v",encoderName};
        if(encoderName==EncoderName) {
            arguments.AddRange(["-preset","fast","-x265-params","keyint=120:min-keyint=120:scenecut=0:bframes=0:open-gop=0:colorprim=bt709:transfer=bt709:colormatrix=bt709:range=limited:log-level=warning:pools=2:frame-threads=2"]);
        }else {
            arguments.AddRange(["-gpu","0","-preset","p4","-tune","ll","-rc","vbr","-rc-lookahead","0","-no-scenecut","1","-forced-idr","1","-zerolatency","1","-delay","0","-surfaces","4"]);
        }
        arguments.AddRange(["-profile:v","main","-pix_fmt","yuv420p","-tag:v","hvc1","-r","60/1","-fps_mode","cfr",
            "-b:v","40M","-maxrate","60M","-bufsize","120M","-g","120","-bf","0",
            "-vf","scale=in_range=full:out_range=limited:out_color_matrix=bt709,setparams=range=limited:color_primaries=bt709:color_trc=bt709:colorspace=bt709",
            "-color_range","tv","-color_primaries","bt709","-color_trc","bt709","-colorspace","bt709",
            "-movflags","+faststart","-video_track_timescale","60000","-f","mp4",output]);
        return arguments;
    }
    /// <summary>Rejects unknown codec names instead of passing arbitrary FFmpeg encoders or silently selecting another backend.</summary>
    public static void ValidateEncoder(string encoderName) {
        if(encoderName is not (EncoderName or NvidiaEncoderName)) {throw new ArgumentException("Encoder must be libx265 or hevc_nvenc.",nameof(encoderName));}
    }
    /// <summary>Rejects audio, incompatible dimensions or timing before starting any child process.</summary>
    public static void Validate(CompositionDocument document,OutputProfile profile) {
        ArgumentNullException.ThrowIfNull(document);ArgumentNullException.ThrowIfNull(profile);
        if(profile.Id!=ProfileId) {throw new InvalidDataException("Linux encoder requires "+ProfileId+".");}
        OutputProfile.Resolve(profile.Id,profile.Width,profile.Height);
        if(document.Audio==null || document.Audio.Enabled || document.AudioClips.Count!=0) {throw new InvalidDataException("The silent HEVC profile rejects audio and audio clips.");}
        if(document.Width!=profile.Width || document.Height!=profile.Height) {throw new InvalidDataException("Composition and HEVC output dimensions must match.");}
        if(document.FrameRate!=new MediaTime(60,1) || document.Duration<=MediaTime.Zero) {throw new InvalidDataException("Silent HEVC requires CFR 60/1 and a positive duration.");}
    }
}
