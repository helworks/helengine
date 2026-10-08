using helengine.media;
using SharpDX.Direct3D11;
using SharpDX.Direct3D;
using System.Security.Cryptography;
using System.Drawing;
using System.Drawing.Imaging;
namespace helengine.media.windows.tests;
/// <summary>Validates owned sources, independent cursors and timestamp-aligned PCM conversion.</summary>
public sealed class MediaSourceTests {
    /// <summary>Workspace-owned media fixture directory shared by explicit test assets.</summary>
    const string Root="C:/dev/helworks/builds/helengine/media-composition/fixtures";
    /// <summary>PNG preserves straight alpha and EXIF orientation normalizes JPEG dimensions.</summary>
    [Fact] public void ImageOrientationAndAlphaPreserved() {
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);
        using(var bitmap=new Bitmap(2,3)) {bitmap.SetPixel(0,0,Color.FromArgb(64,255,0,0));bitmap.Save(Path.Combine(Root,"alpha.png"),ImageFormat.Png);}
        using var resolver=new WindowsMediaSourceResolver(Root,device);
        using var source=resolver.Open(Reference("alpha.png","image",2,3));
        using var frame=source.ReadVideoFrame(MediaTime.Zero);
        var pixels=frame.Surface.ReadRgba().ToArray();
        Assert.Equal(255,pixels[0]);Assert.Equal(0,pixels[1]);Assert.Equal(64,pixels[3]);
        WriteOrientedJpeg();
        using var oriented=resolver.Open(Reference("oriented.jpg","image",3,2));
        using var orientedFrame=oriented.ReadVideoFrame(MediaTime.Zero);
        Assert.Equal(2,orientedFrame.Surface.Width);Assert.Equal(3,orientedFrame.Surface.Height);
    }
    /// <summary>Printing resolution must not rescale pixel content inside the uploaded texture.</summary>
    [Theory]
    [InlineData("jpg",96)]
    [InlineData("jpg",300)]
    [InlineData("png",300)]
    public void ImagePrintingDpiDoesNotChangePixelContent(string extension,int dpi) {
        string name="pixel-size-"+dpi+"."+extension;
        using(var image=new Bitmap(30,40)) {image.SetResolution(dpi,dpi);using(var graphics=Graphics.FromImage(image)) {graphics.Clear(Color.Red);}image.Save(Path.Combine(Root,name),extension=="jpg"?ImageFormat.Jpeg:ImageFormat.Png);}
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);
        using var resolver=new WindowsMediaSourceResolver(Root,device);
        using var source=resolver.Open(Reference(name,"image",30,40));
        using var frame=source.ReadVideoFrame(MediaTime.Zero);
        var pixels=frame.Surface.ReadRgba().ToArray();int corner=(39*30+29)*4;
        Assert.Equal(30,frame.Surface.Width);Assert.Equal(40,frame.Surface.Height);
        Assert.InRange(pixels[corner],250,255);Assert.Equal(255,pixels[corner+3]);
    }
    /// <summary>A seek in one source cursor cannot change another cursor's frame data.</summary>
    [Fact] public void DifferentCursorsDoNotShareSeekState() {
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);
        using var resolver=new WindowsMediaSourceResolver(Root,device);
        using var first=resolver.Open(Reference("bframes.mp4","video",64,48));
        using var second=resolver.Open(Reference("bframes.mp4","video",64,48));
        using var before=first.ReadVideoFrame(new(1,5));var expected=before.Surface.ReadRgba().ToArray();
        using var later=second.ReadVideoFrame(new(7,5));
        using var repeated=first.ReadVideoFrame(new(1,5));
        Assert.Equal(expected,repeated.Surface.ReadRgba().ToArray());
    }
    /// <summary>Embedded sound is indexed against the same normalized video origin.</summary>
    [Fact] public void VideoAudioUsesSameSourceOrigin() {
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);
        using var resolver=new WindowsMediaSourceResolver(Root,device);
        using var source=resolver.Open(Reference("av-offset.mp4","video",64,48));
        using var frame=source.ReadVideoFrame(new(1,4));
        var audio=source.ReadAudio(12000,2048,new(48000,2));
        Assert.InRange(frame.Timestamp.ToSeconds(),.2,.3);
        Assert.Equal(12000,audio.FirstSample);Assert.Equal(2048,audio.SampleCount);
        Assert.True(audio.Samples.ToArray().Any(x=>Math.Abs(x)>.01));
    }
    /// <summary>Authored mono 44.1 kHz audio becomes stereo 48 kHz without level changes between channels.</summary>
    [Fact] public void Mono44100BecomesStereo48000() {
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);
        using var resolver=new WindowsMediaSourceResolver(Root,device);
        using var source=resolver.Open(Reference("mono.wav","audio",0,0));
        var block=source.ReadAudio(4800,4096,new(48000,2));
        Assert.Equal(8192,block.Samples.Length);
        var samples=block.Samples.ToArray();
        Assert.True(samples.Any(x=>Math.Abs(x)>.1));
        Assert.True(Enumerable.Range(0,4096).All(i=>Math.Abs(samples[2*i]-samples[2*i+1])<1e-6));
        var repeated=source.ReadAudio(4800,4096,new(48000,2));Assert.Equal(samples,repeated.Samples.ToArray());
    }
    /// <summary>A video without an audio stream yields explicitly identified silence.</summary>
    [Fact] public void MissingAudioIsExplicitSilence() {
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);
        using var resolver=new WindowsMediaSourceResolver(Root,device);
        using var source=resolver.Open(Reference("bframes.mp4","video",64,48));
        var block=source.ReadAudio(0,128,new(48000,2));
        Assert.True(block.IsSilence);Assert.All(block.Samples.ToArray(),sample=>Assert.Equal(0,sample));
    }
    /// <summary>Root ownership and byte identity are checked before opening a source.</summary>
    [Fact] public void RejectsStaleHashOrEscapedPath() {
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);
        using var resolver=new WindowsMediaSourceResolver(Root,device);
        var reference=Reference("bframes.mp4","video",64,48);reference.Sha256=new string('b',64);
        Assert.Throws<InvalidDataException>(()=>resolver.Open(reference));
        reference.Path="../other.mp4";Assert.Throws<InvalidDataException>(()=>resolver.Open(reference));
    }
    /// <summary>Builds a pinned source reference from an authored fixture.</summary>
    static MediaReference Reference(string name,string kind,int width,int height) => new() {Id=name,Kind=kind,Path=name,Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Root,name)))).ToLowerInvariant(),Width=width,Height=height,Duration=kind=="image"?MediaTime.Zero:new(2,1)};
    /// <summary>Creates an EXIF orientation fixture without using user media.</summary>
    static void WriteOrientedJpeg() {
        using var bitmap=new Bitmap(3,2);using var memory=new MemoryStream();bitmap.Save(memory,ImageFormat.Jpeg);
        byte[] jpeg=memory.ToArray();
        byte[] exif=[0xff,0xe1,0,34,0x45,0x78,0x69,0x66,0,0,0x49,0x49,42,0,8,0,0,0,1,0,0x12,1,3,0,1,0,0,0,6,0,0,0,0,0,0,0];
        File.WriteAllBytes(Path.Combine(Root,"oriented.jpg"),jpeg.Take(2).Concat(exif).Concat(jpeg.Skip(2)).ToArray());
    }
    /// <summary>Hardware surfaces are converted to independent RGBA output entirely on the GPU.</summary>
    [Fact] public void HardwareRgbaFrameOutlivesSourceCursor() {
        using var device=new Device(DriverType.Hardware,DeviceCreationFlags.BgraSupport);
        using var resolver=new WindowsMediaSourceResolver(Root,device);
        var source=resolver.Open(Reference("bframes.mp4","video",64,48));
        using var frame=source.ReadVideoFrame(new(1,5));source.Dispose();
        var pixels=frame.Surface.ReadRgba().ToArray();
        Assert.True(pixels.Where((value,index)=>index%4!=3).Any(value=>value>32));
        Assert.True(pixels.Where((value,index)=>index%4==3).All(value=>value==255));
    }
    /// <summary>Authored video orientation travels with frames for the compositor's normalized layout.</summary>
    [Fact] public void VideoRotationMetadataChangesDisplayDimensions() {
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);
        using var resolver=new WindowsMediaSourceResolver(Root,device);
        using var source=resolver.Open(Reference("rotated.mp4","video",64,48));
        using var frame=source.ReadVideoFrame(MediaTime.Zero);
        Assert.Equal(48,frame.DisplayWidth);Assert.Equal(64,frame.DisplayHeight);
        Assert.Equal(90,frame.RotationDegrees);
    }
}
