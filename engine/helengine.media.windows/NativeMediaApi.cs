namespace helengine.media.windows;
/// <summary>Typed codec-source ABI; no timeline composition or track mixing is executed natively.</summary>
static class NativeMediaApi {
    /// <summary>Shared installed native codec library name.</summary>
    const string Library="helengine.video.ffmpeg";
    /// <summary>Creates a PCM cursor with explicit output rate and channels.</summary>
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Unicode,ExactSpelling=true)]
    internal static extern IntPtr he_audio_decoder_create([MarshalAs(UnmanagedType.LPWStr)]string source,int sampleRate,int channels,out int hasAudio);
    /// <summary>Reads at most the requested bounded number of converted sample frames.</summary>
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl,ExactSpelling=true)]
    internal static extern int he_audio_decoder_read(IntPtr decoder,long firstSample,int count,[Out]float[] samples);
    /// <summary>Releases PCM codec and resampling state.</summary>
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl,ExactSpelling=true)]
    internal static extern void he_audio_decoder_destroy(IntPtr decoder);
    /// <summary>Reads the current thread's native failure diagnostic immediately after the failed call.</summary>
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl,ExactSpelling=true)]
    static extern IntPtr he_media_last_error();
    /// <summary>Converts a native UTF-8 failure message into a managed diagnostic.</summary>
    internal static string Error() => Marshal.PtrToStringUTF8(he_media_last_error()) ?? "Native media operation failed.";
}
