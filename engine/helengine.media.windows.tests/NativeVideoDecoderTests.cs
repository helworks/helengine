using helengine.directx11.video;
using SharpDX.Direct3D11;
using SharpDX.Direct3D;
namespace helengine.media.windows.tests;
/// <summary>Decodes real B-frame and variable-rate fixtures through the packaged native backend.</summary>
public sealed class NativeVideoDecoderTests {
    /// <summary>All delayed frames are drained and seeking works even after reaching EOF.</summary>
    [Theory][InlineData("bframes")][InlineData("vfr")][InlineData("offset")]
    public void DecodesThroughEndAndSeeksByPts(string name) {
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);
        using var decoder=new DirectX11VideoDecoder(new(device,Fixture(name),VideoDecoderHardwareMode.DisableHardware));
        var times=new List<TimeSpan>();
        while(decoder.TryGetNextFrame(out var frame)) { using(frame) { times.Add(frame.Timestamp);Assert.Equal(64,frame.Width);Assert.Equal(48,frame.Height); } }
        Assert.Equal(20,times.Count); Assert.True(Enumerable.Range(1,times.Count-1).All(index=>times[index-1]<=times[index]));
        decoder.Seek(TimeSpan.FromSeconds(1));decoder.Flush();
        Assert.True(decoder.TryGetNextFrame(out var afterSeek));
        using(afterSeek) {Assert.InRange(afterSeek.Timestamp.TotalSeconds,0,1.2);}
    }
    /// <summary>A native error has a distinct return code from normal end of file.</summary>
    [Fact] public void NativeErrorDoesNotBecomeEof() {
        Assert.Equal(-1,NativeDecoderTestExports.he_video_decoder_try_get_frame(IntPtr.Zero,IntPtr.Zero));
    }
    /// <summary>Disposing a decoder with borrowed live frames is rejected until they are released.</summary>
    [Fact] public void FramesReleasedBeforeDecoder() {
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);
        var decoder=new DirectX11VideoDecoder(new(device,Fixture("bframes"),VideoDecoderHardwareMode.DisableHardware));
        Assert.True(decoder.TryGetNextFrame(out var frame));
        Assert.Throws<InvalidOperationException>(()=>decoder.Dispose());
        frame.Dispose();frame.Dispose();decoder.Dispose();decoder.Dispose();
    }
    /// <summary>A software D3D device cannot satisfy explicitly required hardware decoding.</summary>
    [Fact] public void RequireHardwareDoesNotSilentlyFallBack() {
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);
        var error=Assert.Throws<InvalidOperationException>(()=>new DirectX11VideoDecoder(new(device,Fixture("bframes"),VideoDecoderHardwareMode.RequireHardware)));
        Assert.Contains("Hardware required",error.Message);
    }
    /// <summary>Finds the authored fixture in the visible workspace-owned build directory.</summary>
    static string Fixture(string name) => Path.Combine("C:/dev/helworks/builds/helengine/media-composition/fixtures",name+".mp4");
    /// <summary>Probing dimensions and duration does not allocate a graphics device.</summary>
    [Fact] public void ProbeDoesNotRequireGpu() {
        var info=VideoFileProbe.Probe(Fixture("bframes"));
        Assert.Equal(64,info.Width);Assert.Equal(48,info.Height);
        Assert.InRange(info.Duration.TotalSeconds,1.9,2.1);
    }
    /// <summary>Explicit GPU decoding returns a GPU-resident planar surface on the installed device.</summary>
    [Fact] public void HardwareSurfaceStaysOnGpu() {
        using var device=new Device(DriverType.Hardware,DeviceCreationFlags.BgraSupport);
        using var decoder=new DirectX11VideoDecoder(new(device,Fixture("bframes"),VideoDecoderHardwareMode.RequireHardware));
        Assert.True(decoder.StreamInfo.IsHardwareAccelerated);
        Assert.True(decoder.TryGetNextFrame(out var frame));
        using(frame) {Assert.Equal(helengine.directx11.video.VideoFrameFormat.Nv12,frame.Format);}
    }
}
