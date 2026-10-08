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
    /// <summary>Hashes one fixture.</summary>
    static string Hash(string name)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Root,name)))).ToLowerInvariant();
    /// <summary>Reads one packed RGBA pixel.</summary>
    static byte[] Pixel(MediaVideoFrame frame,int x,int y)=>frame.Surface.ReadRgba().Slice((y*frame.Surface.Width+x)*4,4).ToArray();
}
