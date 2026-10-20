namespace helengine.directx11.video.tests;
/// <summary>Exercises the existing decoder ABI after extending the shared DLL with encoding.</summary>
public sealed class VideoDecoderRegressionTests {
    /// <summary>Probe, read, seek and release continue to work on a newly encoded clip.</summary>
    [Fact] public void ExistingProbeAndDecoderStillReadEncodedClip() {
        using var device = EncoderTestGpu.Open(); string path = "C:\\temp\\helengine-window-recording\\decode-regression.mp4";
        using (var encoder = HevcVideoEncoder.Open(device.NativePointer, path, new() { SourceWidth = 640, SourceHeight = 480 })) {
            using var texture = EncoderTestGpu.Texture(device);
            for (int index = 0; index < 12; index++) { encoder.Submit(texture.NativePointer, index); }
            encoder.Complete();
        }
        Assert.Equal(640, VideoFileProbe.Probe(path).Width);
        var decoder = FfmpegNativeApi.he_video_decoder_create(device.NativePointer, path, VideoDecoderHardwareMode.DisableHardware, out var info);
        Assert.NotEqual(IntPtr.Zero, decoder);
        try {
            Assert.Equal(480, info.Height); int count = 0;
            while (FfmpegNativeApi.he_video_decoder_try_get_frame(decoder, out var frame) != 0) {
                Assert.NotEqual(IntPtr.Zero, frame.Texture); count++; FfmpegNativeApi.he_video_decoder_release_frame(decoder, ref frame);
            }
            Assert.Equal(12, count); Assert.NotEqual(0, FfmpegNativeApi.he_video_decoder_seek(decoder, 0));
            Assert.NotEqual(0, FfmpegNativeApi.he_video_decoder_try_get_frame(decoder, out var afterSeek)); FfmpegNativeApi.he_video_decoder_release_frame(decoder, ref afterSeek);
        } finally { FfmpegNativeApi.he_video_decoder_destroy(decoder); }
    }
}
