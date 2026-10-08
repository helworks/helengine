using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text.Json;
using helengine.media;
using helengine.vfx;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
namespace helengine.media.windows.tests;
/// <summary>Exercises real linear-light transitions of complete scene groups.</summary>
public sealed class GpuTransitionTests {
    /// <summary>Owned synthetic raster directory.</summary>
    const string Root="C:/dev/helworks/builds/helengine/media-composition/fixtures";
    /// <summary>A midpoint blends premultiplied scene outputs equally, without alpha-over darkening.</summary>
    [Fact] public void CrossfadeEndpointsAndMidpoint() {
        var document=Document();using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);
        using var resolver=new WindowsMediaSourceResolver(Root,device);using var compositor=new DirectX11MediaCompositor(device,resolver);
        using var start=compositor.Render(document,new(1,1),new(32,32));Assert.Equal(new byte[]{255,0,0,255},Pixel(start,16,16));
        using var middle=compositor.Render(document,new(3,2),new(32,32));var pixel=Pixel(middle,16,16);Assert.InRange(pixel[0],187,189);Assert.InRange(pixel[2],187,189);Assert.Equal(255,pixel[3]);
        using var end=compositor.Render(document,new(2,1),new(32,32));Assert.Equal(new byte[]{0,0,255,255},Pixel(end,16,16));
    }
    /// <summary>Dip to color shows the chosen color, not either scene, in the middle of its hold.</summary>
    [Fact] public void DipToColorPassesThroughTheColor() {
        var document=Document();var transition=document.Transitions[0];transition.EffectId="dip-to-color";
        transition.Parameters["Easing"]=JsonSerializer.SerializeToElement("Linear");transition.Parameters["Hold"]=JsonSerializer.SerializeToElement(.2);transition.Parameters["Color"]=JsonSerializer.SerializeToElement(new[]{0.0,1.0,0.0});
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);using var resolver=new WindowsMediaSourceResolver(Root,device);using var compositor=new DirectX11MediaCompositor(device,resolver);
        using var middle=compositor.Render(document,new(3,2),new(32,32));Assert.Equal(new byte[]{0,255,0,255},Pixel(middle,16,16));
        using var end=compositor.Render(document,new(2,1),new(32,32));Assert.Equal(new byte[]{0,0,255,255},Pixel(end,16,16));
    }
    /// <summary>Halfway through a leftward push, the outgoing scene fills the left half and the incoming scene the right half.</summary>
    [Fact] public void PushSplitsTheFrameAtItsMidpoint() {
        var document=Document();var transition=document.Transitions[0];transition.EffectId="push";
        transition.Parameters["Easing"]=JsonSerializer.SerializeToElement("Linear");transition.Parameters["Direction"]=JsonSerializer.SerializeToElement("Left");
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);using var resolver=new WindowsMediaSourceResolver(Root,device);using var compositor=new DirectX11MediaCompositor(device,resolver);
        using var middle=compositor.Render(document,new(3,2),new(32,32));
        Assert.Equal(new byte[]{255,0,0,255},Pixel(middle,6,16));Assert.Equal(new byte[]{0,0,255,255},Pixel(middle,26,16));
    }
    /// <summary>Every built-in transition compiles, renders an opaque midpoint and lands exactly on the incoming scene.</summary>
    [Fact] public void EveryBuiltInTransitionRenders() {
        var document=Document();using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);using var resolver=new WindowsMediaSourceResolver(Root,device);using var compositor=new DirectX11MediaCompositor(device,resolver);
        foreach(var effect in BuiltInVfxTransitions.All()) {
            document.Transitions[0].EffectId=effect.EffectId;
            using var middle=compositor.Render(document,new(3,2),new(32,32));Assert.Equal(255,Pixel(middle,16,16)[3]);
            using var end=compositor.Render(document,new(2,1),new(32,32));Assert.Equal(new byte[]{0,0,255,255},Pixel(end,16,16));
        }
    }
    /// <summary>A group's overlay participates in the transition and is never rendered a second time.</summary>
    [Fact] public void GroupTransitionIncludesItsOverlays() {
        var document=Document();var green=Solid("transition-green.png",Color.Lime);document.Media.Add(green);
        document.Layers.Add(new(){Id="overlay",MediaId=green.Id,Order=1,Start=MediaTime.Zero,End=new(3,1),Viewport=new(){X=.25,Y=.25,Width=.5,Height=.5}});
        document.Layers.Add(new(){Id="group",Kind="group",Members=["red","overlay"],Start=MediaTime.Zero,End=new(3,1)});document.Transitions[0].FromLayer="group";
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);using var resolver=new WindowsMediaSourceResolver(Root,device);using var compositor=new DirectX11MediaCompositor(device,resolver);
        using var middle=compositor.Render(document,new(3,2),new(32,32));var center=Pixel(middle,16,16);Assert.Equal(0,center[0]);Assert.InRange(center[1],187,189);Assert.InRange(center[2],187,189);
        using var end=compositor.Render(document,new(2,1),new(32,32));Assert.Equal(new byte[]{0,0,255,255},Pixel(end,16,16));
    }
    /// <summary>Creates opaque red and blue scenes with an explicit one-second overlap.</summary>
    static CompositionDocument Document() {var red=Solid("transition-red.png",Color.Red);var blue=Solid("transition-blue.png",Color.Blue);return new(){Id="transition",Revision=1,Width=32,Height=32,FrameRate=new(24,1),Duration=new(3,1),BackgroundColor="#000000FF",Audio=new(){Enabled=false},Media=[red,blue],Layers=[new(){Id="red",MediaId=red.Id,Start=MediaTime.Zero,End=new(3,1)},new(){Id="blue",MediaId=blue.Id,Order=2,Start=MediaTime.Zero,End=new(3,1)}],Transitions=[new(){Id="fade",FromLayer="red",ToLayer="blue",Start=new(1,1),Duration=new(1,1)}]};}
    /// <summary>Writes and pins an owned solid-color fixture.</summary>
    static MediaReference Solid(string name,Color color) {using var bitmap=new Bitmap(16,16);using(var graphics=Graphics.FromImage(bitmap)){graphics.Clear(color);}bitmap.Save(Path.Combine(Root,name),ImageFormat.Png);return new(){Id=name,Kind="image",Path=name,Width=16,Height=16,Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Root,name)))).ToLowerInvariant()};}
    /// <summary>Downloads a single pixel at the diagnostics boundary.</summary>
    static byte[] Pixel(MediaVideoFrame frame,int x,int y)=>frame.Surface.ReadRgba().Slice((y*frame.Surface.Width+x)*4,4).ToArray();
}
