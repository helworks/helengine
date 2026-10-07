namespace helengine.directx11.video {
    /// <summary>Reads native video metadata without creating playback or graphics resources.</summary>
    public static class VideoFileProbe {
        /// <summary>Returns source dimensions, rate and duration or an explicit probe error.</summary>
        public static VideoStreamInfo Probe(string sourcePath) {
            if (string.IsNullOrWhiteSpace(sourcePath)) { throw new ArgumentException("Video source path is required.",nameof(sourcePath)); }
            if (FfmpegNativeApi.he_video_probe(sourcePath,out var info)==0) { throw new InvalidOperationException("Video probe failed: "+FfmpegNativeApi.LastError()); }
            if (info.Width<=0 || info.Height<=0 || !double.IsFinite(info.FrameRate) || info.FrameRate<=0 || info.DurationTicks<0) { throw new InvalidOperationException("Video probe returned invalid stream metadata."); }
            return new(info.Width,info.Height,info.FrameRate,TimeSpan.FromTicks(info.DurationTicks),VideoFrameFormat.Rgba8,false);
        }
    }
}
