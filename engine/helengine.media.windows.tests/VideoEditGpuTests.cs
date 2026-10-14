using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text.Json;
using helengine.media;
using helengine.video;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
namespace helengine.media.windows.tests;
/// <summary>Renders compiled helengine.video edits on the GPU, end to end.</summary>
public sealed class VideoEditGpuTests {
    /// <summary>Visible build-owned assets, independent of uploaded user material.</summary>
    const string Root="C:/dev/helworks/builds/helengine/media-composition/fixtures";
    /// <summary>A take that ends at its media end pushes into a blue image: the take holds its last frame and the push splits the frame.</summary>
    [Fact] public void TakeWithoutHandlePushesIntoImage() {
        using(var bitmap=new Bitmap(16,16)){using(var graphics=Graphics.FromImage(bitmap)){graphics.Clear(Color.Blue);}bitmap.Save(Path.Combine(Root,"video-edit-blue.png"),ImageFormat.Png);}
        var edit=new VideoEdit{Id="gpu-edit",Format=new(){Width=32,Height=32,BackgroundColor="#000000FF"},
            Media=[new(){Id="clip",Kind="video",Path="rotated.mp4",Sha256=Hash("rotated.mp4"),Width=64,Height=48,DurationSec=2},new(){Id="blue",Kind="image",Path="video-edit-blue.png",Sha256=Hash("video-edit-blue.png"),Width=16,Height=16}],
            Scenes=[
                new(){Id="take",Duration=new(){Mode="from_take"},Take=new(){Media="clip",InSec=1.5,OutSec=2}},
                new(){Id="image",Duration=new(){Mode="fixed",Sec=1},Entry=new(){Effect="push",Version=1,DurationSec=.4,Parameters=new(StringComparer.Ordinal){["Easing"]=JsonSerializer.SerializeToElement("Linear"),["Direction"]=JsonSerializer.SerializeToElement("Left")}},
                    Layers=[new(){Id="picture",Kind="media",Media="blue",Fit="cover"}]}]};
        var catalog=WindowsMediaCapabilities.Describe();
        var result=VideoEditCompiler.Compile(edit,new(){Capabilities=catalog});
        Assert.False(result.HasErrors,string.Join("; ",result.Diagnostics.Select(item=>item.Code+" "+item.Path+" "+item.Message)));
        Assert.True(result.Composition.Layers.Single(layer=>layer.Id=="take-take").HoldLastFrame);
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);using var resolver=new WindowsMediaSourceResolver(Root,device);using var compositor=new DirectX11MediaCompositor(device,resolver);
        using var middle=compositor.Render(result.Composition,MediaTime.FromSeconds(.7),new(32,32));
        Assert.Equal(new byte[]{0,0,255,255},Pixel(middle,28,16));
        using var end=compositor.Render(result.Composition,MediaTime.FromSeconds(1.2),new(32,32));
        Assert.Equal(new byte[]{0,0,255,255},Pixel(end,16,16));
    }
    /// <summary>A take with a video luma matte keeps the speaker where the matte is white and shows the replacement image or the blurred take elsewhere.</summary>
    [Fact] public void MattedTakeShowsReplacementOrBlurBehindTheSpeaker() {
        using(var bitmap=new Bitmap(16,16)){using(var graphics=Graphics.FromImage(bitmap)){graphics.Clear(Color.Lime);}bitmap.Save(Path.Combine(Root,"video-edit-green.png"),ImageFormat.Png);}
        Ffmpeg("-f lavfi -i color=c=blue:s=64x48:d=2:r=30,drawbox=x=0:y=0:w=32:h=48:color=red:t=fill -pix_fmt yuv420p video-edit-bg-take.mp4");
        Ffmpeg("-f lavfi -i color=c=black:s=32x24:d=2:r=30,drawbox=x=0:y=0:w=8:h=24:color=white:t=fill -pix_fmt yuv420p video-edit-bg-matte.mp4");
        VideoEdit Edit(string mode)=>new(){Id="gpu-background",Format=new(){Width=64,Height=48,BackgroundColor="#000000FF"},
            Media=[new(){Id="take",Kind="video",Path="video-edit-bg-take.mp4",Sha256=Hash("video-edit-bg-take.mp4"),Width=64,Height=48,DurationSec=2,Matte="matte"},
                new(){Id="matte",Kind="video",Path="video-edit-bg-matte.mp4",Sha256=Hash("video-edit-bg-matte.mp4"),Width=32,Height=24,DurationSec=2},
                new(){Id="green",Kind="image",Path="video-edit-green.png",Sha256=Hash("video-edit-green.png"),Width=16,Height=16}],
            Scenes=[new(){Id="scene",Duration=new(){Mode="fixed",Sec=1},Take=new(){Media="take",InSec=0,OutSec=1},Background=new(){Mode=mode,Radius=24,Media="green"}}]};
        var catalog=WindowsMediaCapabilities.Describe();
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);using var resolver=new WindowsMediaSourceResolver(Root,device);using var compositor=new DirectX11MediaCompositor(device,resolver);
        var replaced=VideoEditCompiler.Compile(Edit("replace"),new(){Capabilities=catalog});
        Assert.False(replaced.HasErrors,string.Join("; ",replaced.Diagnostics.Select(item=>item.Code+" "+item.Path+" "+item.Message)));
        using(var frame=compositor.Render(replaced.Composition,MediaTime.FromSeconds(.5),new(64,48))){
            AssertNear(new byte[]{255,0,0},Pixel(frame,6,24));AssertNear(new byte[]{0,255,0},Pixel(frame,24,24));AssertNear(new byte[]{0,255,0},Pixel(frame,56,24));
        }
        var blurred=VideoEditCompiler.Compile(Edit("blur"),new(){Capabilities=catalog});
        Assert.False(blurred.HasErrors,string.Join("; ",blurred.Diagnostics.Select(item=>item.Code+" "+item.Path+" "+item.Message)));
        using(var frame=compositor.Render(blurred.Composition,MediaTime.FromSeconds(.5),new(64,48))){
            AssertNear(new byte[]{255,0,0},Pixel(frame,6,24));
            var masked=Pixel(frame,24,24);Assert.True(masked[2]>40&&masked[0]<230,"Matte-black pixel next to the red/blue edge must show the blurred take, got "+string.Join(",",masked));
        }
    }
    /// <summary>A take of three jump-cut segments at 2x shows each segment's colour in its slice of the scene: red, then green, then blue.</summary>
    [Fact] public void SegmentedTakeAtSpeedJumpsBetweenSegments() {
        Ffmpeg("-f lavfi -i color=c=red:s=32x32:d=1:r=30 -f lavfi -i color=c=lime:s=32x32:d=1:r=30 -f lavfi -i color=c=blue:s=32x32:d=1:r=30 -filter_complex \"[0][1][2]concat=n=3:v=1:a=0\" -pix_fmt yuv420p video-edit-segments.mp4");
        var edit=new VideoEdit{Id="gpu-segments",Format=new(){Width=32,Height=32,BackgroundColor="#000000FF"},
            Media=[new(){Id="take",Kind="video",Path="video-edit-segments.mp4",Sha256=Hash("video-edit-segments.mp4"),Width=32,Height=32,DurationSec=3}],
            Scenes=[new(){Id="scene",Duration=new(){Mode="from_take"},Take=new(){Media="take",InSec=.1,OutSec=2.9,Speed=2,Segments=[new(){InSec=.1,OutSec=.9},new(){InSec=1.1,OutSec=1.5},new(){InSec=2.1,OutSec=2.9}]}}]};
        var catalog=WindowsMediaCapabilities.Describe();
        var result=VideoEditCompiler.Compile(edit,new(){Capabilities=catalog});
        Assert.False(result.HasErrors,string.Join("; ",result.Diagnostics.Select(item=>item.Code+" "+item.Path+" "+item.Message)));
        Assert.Equal(1,result.Composition.Duration.ToSeconds(),6);
        Assert.Empty(CompositionValidator.Validate(result.Composition,catalog));
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);using var resolver=new WindowsMediaSourceResolver(Root,device);using var compositor=new DirectX11MediaCompositor(device,resolver);
        // 0.2 s plays source 0.5 (red); 0.5 s is 0.1 s into the second piece, source 1.3 (green); 0.8 s is source 2.5 (blue).
        foreach((double time,byte[] colour) in new[]{(.2,new byte[]{255,0,0}),(.5,new byte[]{0,255,0}),(.8,new byte[]{0,0,255})}) {
            using var frame=compositor.Render(result.Composition,MediaTime.FromSeconds(time),new(32,32));AssertNear(colour,Pixel(frame,16,16));
        }
    }
    /// <summary>Runs ffmpeg in the fixture folder.</summary>
    static void Ffmpeg(string arguments) {
        using var process=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("C:/env/ffmpeg-8.1.1-full_build-shared/bin/ffmpeg.exe","-y -loglevel error "+arguments){WorkingDirectory=Root,RedirectStandardError=true})!;
        string error=process.StandardError.ReadToEnd();process.WaitForExit();Assert.True(process.ExitCode==0,error);
    }
    /// <summary>Asserts a pixel's red, green and blue are within a codec tolerance of the expected colour.</summary>
    static void AssertNear(byte[] expected,byte[] actual){for(int channel=0;channel<3;channel++){Assert.True(Math.Abs(expected[channel]-actual[channel])<=40,$"Expected {string.Join(",",expected)} but got {string.Join(",",actual)}");}}
    /// <summary>Hashes one fixture.</summary>
    static string Hash(string name)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Root,name)))).ToLowerInvariant();
    /// <summary>Reads one packed RGBA pixel.</summary>
    static byte[] Pixel(MediaVideoFrame frame,int x,int y)=>frame.Surface.ReadRgba().Slice((y*frame.Surface.Width+x)*4,4).ToArray();
}
