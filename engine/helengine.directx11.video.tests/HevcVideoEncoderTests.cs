using System.Runtime.InteropServices;

namespace helengine.directx11.video.tests;
/// <summary>Verifies the managed ownership and validation boundary against the real native encoder.</summary>
public sealed class HevcVideoEncoderTests {
    /// <summary>Wrong packing would pass invalid bitrates and pointers across the ABI.</summary>
    [Fact] public void NativeLayoutsMatchAbi() {
        Assert.Equal(32, Marshal.SizeOf<HevcEncoderNativeOptions>());
        Assert.Equal(16, Marshal.OffsetOf<HevcEncoderNativeOptions>(nameof(HevcEncoderNativeOptions.Bitrate)).ToInt32());
        Assert.Equal(24, Marshal.OffsetOf<HevcEncoderNativeOptions>(nameof(HevcEncoderNativeOptions.MaxBitrate)).ToInt32());
        Assert.Equal(1028, Marshal.SizeOf<HevcEncoderNativeError>());
    }
    /// <summary>Invalid inputs fail before a native device could be dereferenced.</summary>
    [Fact] public void ValidatesBeforeAllocating() {
        Assert.Throws<ArgumentException>(() => HevcVideoEncoder.Open(IntPtr.Zero, "x", new()));
        Assert.Throws<ArgumentOutOfRangeException>(() => HevcVideoEncoder.Open((IntPtr)1, "x", new() { SourceWidth = 0, SourceHeight = 480 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => HevcVideoEncoder.Open((IntPtr)1, "x", new() { SourceWidth = 640, SourceHeight = 480, MaxBitrate = 1 }));
        Assert.Throws<ArgumentException>(() => HevcVideoEncoder.Open((IntPtr)1, " ", new() { SourceWidth = 640, SourceHeight = 480 }));
    }
    /// <summary>Submitting a borrowed texture cannot require its producer to retain it afterward.</summary>
    [Fact] public void SubmissionOutlivesProducerLease() {
        string path = Path.Combine("C:\\temp\\helengine-window-recording", "managed posse ç.mp4");
        using var device = EncoderTestGpu.Open();
        using (var encoder = HevcVideoEncoder.Open(device.NativePointer, path, new() { SourceWidth = 640, SourceHeight = 480 })) {
            for (int i = 0; i < 60; i++) { using var texture = EncoderTestGpu.Texture(device); encoder.Submit(texture.NativePointer, i); }
            encoder.Complete(); encoder.Complete(); Assert.Equal(60, encoder.SubmittedFrames); Assert.Equal(640, encoder.OutputWidth);
        }
        var info = VideoFileProbe.Probe(path); Assert.Equal(60, info.FrameRate); Assert.Equal(TimeSpan.FromSeconds(1), info.Duration);
    }
    /// <summary>Dispose is an abort, and the facade never publishes or accepts post-disposal work.</summary>
    [Fact] public void CompletesOnceAndAbortsWithoutPublishing() {
        using var device = EncoderTestGpu.Open(); string path = "C:\\temp\\helengine-window-recording\\managed-abort.mp4";
        var encoder = HevcVideoEncoder.Open(device.NativePointer, path, new() { SourceWidth = 640, SourceHeight = 480 });
        encoder.Dispose(); encoder.Dispose(); Assert.Throws<ObjectDisposedException>(() => encoder.Complete());
        Assert.Throws<ObjectDisposedException>(() => encoder.Submit((IntPtr)1, 0));
        Assert.Throws<InvalidOperationException>(() => VideoFileProbe.Probe(path));
    }
    /// <summary>Each native failure keeps its original code and useful operation text.</summary>
    [Fact] public void PreservesNativeDiagnostic() {
        using var device = EncoderTestGpu.Open();
        var error = Assert.Throws<HevcVideoEncoderException>(() => HevcVideoEncoder.Open(device.NativePointer, "C:\\temp\\helengine-window-recording\\missing\\x.mp4", new() { SourceWidth = 640, SourceHeight = 480 }));
        Assert.NotEqual(0, error.NativeCode); Assert.Contains("MP4", error.Message);
    }
    /// <summary>Relative native loading must work when the process is outside the checkout.</summary>
    [Fact] public void LoadsNativeDependenciesFromPackage() {
        string previous = Environment.CurrentDirectory;
        try {
            Environment.CurrentDirectory = "C:\\temp\\helengine-window-recording";
            using var device = EncoderTestGpu.Open();
            using var encoder = HevcVideoEncoder.Open(device.NativePointer, "managed relative ç.mp4", new() { SourceWidth = 640, SourceHeight = 480 });
            using var texture = EncoderTestGpu.Texture(device); encoder.Submit(texture.NativePointer, 0); encoder.Complete();
            Assert.Equal(640, VideoFileProbe.Probe("managed relative ç.mp4").Width);
        } finally { Environment.CurrentDirectory = previous; }
    }
}
