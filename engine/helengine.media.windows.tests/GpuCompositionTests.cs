using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text.Json;
using helengine;
using helengine.media;
using helengine.vfx;
using SharpDX.Direct3D11;
using SharpDX.Direct3D;
namespace helengine.media.windows.tests;
/// <summary>Checks actual GPU-composed pixels, independent text and normalized preview layout.</summary>
public sealed class GpuCompositionTests {
    /// <summary>Visible build-owned assets, independent of uploaded user material.</summary>
    const string Root="C:/dev/helworks/builds/helengine/media-composition/fixtures";
    /// <summary>Alpha-over is performed in linear color with stable layer order.</summary>
    [Fact] public void OverlayOrderAndAlpha() {
        var red=Solid("gpu-red.png",Color.Red);var blue=Solid("gpu-blue.png",Color.Blue);
        var document=Document(red,blue);document.Layers[1].Transform.Opacity=.5;
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);
        using var resolver=new WindowsMediaSourceResolver(Root,device);using var compositor=new DirectX11MediaCompositor(device,resolver);
        using var frame=compositor.Render(document,new(1,2),new(32,32));var pixel=Pixel(frame,16,16);
        Assert.InRange(pixel[0],187,189);Assert.Equal(0,pixel[1]);Assert.InRange(pixel[2],187,189);Assert.Equal(255,pixel[3]);
    }
    /// <summary>A grayscale mask changes source opacity before blending with the background.</summary>
    [Fact] public void MaskIsAppliedBeforeComposite() {
        var white=Solid("gpu-white.png",Color.White);var gray=Solid("gpu-mask.png",Color.FromArgb(128,128,128));
        var document=Document(white);document.Media.Add(gray);document.Layers[0].Mask=new(){MediaId=gray.Id,Channel="luma"};
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);
        using var resolver=new WindowsMediaSourceResolver(Root,device);using var compositor=new DirectX11MediaCompositor(device,resolver);
        using var frame=compositor.Render(document,new(1,2),new(32,32));var pixel=Pixel(frame,16,16);
        Assert.InRange(pixel[0],187,189);Assert.Equal(255,pixel[3]);
    }
    /// <summary>Caption cues use composition time rather than layer-relative time.</summary>
    [Fact] public void CaptionAtAbsoluteTime() {
        var document=Document();document.Layers.Add(new(){Id="caption",Kind="text",Start=MediaTime.Zero,End=new(3,1),Text=new(){Cues=[new(){Text="OI",Start=new(1,1),End=new(2,1)}],Style=JsonSerializer.SerializeToElement(new{FontFamily="Segoe UI",FontSize=18,Bold=true,Uppercase=false,TextColor="#FFFFFF",Animation="None",OutlineWidth=0,ShadowOffset=0,CenterY=.5})}});
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);
        using var resolver=new WindowsMediaSourceResolver(Root,device);using var compositor=new DirectX11MediaCompositor(device,resolver);
        using var empty=compositor.Render(document,new(1,2),new(32,32));Assert.All(empty.Surface.ReadRgba().ToArray().Where((value,index)=>index%4!=3),value=>Assert.Equal(0,value));
        using var text=compositor.Render(document,new(3,2),new(32,32));Assert.Contains(text.Surface.ReadRgba().ToArray().Where((value,index)=>index%4!=3),value=>value>64);
    }
    /// <summary>Zoomed source pixels may leave the presentation rectangle and remain visible on the render canvas.</summary>
    [Theory]
    [InlineData(false,255)]
    [InlineData(true,0)]
    public void ZoomUsesCanvasBoundsUnlessViewportClippingWasExplicit(bool clipToViewport,int expectedRed) {
        var document=Document(Solid("gpu-zoom-overflow.png",Color.Red));
        document.Layers[0].Viewport=new(){X=.25,Y=.25,Width=.5,Height=.5};
        document.Layers[0].ClipToViewport=clipToViewport;
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);
        using var resolver=new WindowsMediaSourceResolver(Root,device);
        using var compositor=new DirectX11MediaCompositor(device,resolver);
        using var before=compositor.Render(document,MediaTime.Zero,new(64,64));
        Assert.Equal(0,Pixel(before,12,32)[0]);
        document.Layers[0].Transform.Zoom=1.5;
        using var after=compositor.Render(document,MediaTime.Zero,new(64,64));
        Assert.Equal(expectedRed,Pixel(after,12,32)[0]);
        Assert.Equal(255,Pixel(after,32,32)[0]);
    }
    /// <summary>Disabling an implicit crop does not paint opaque padding across unrelated canvas pixels.</summary>
    [Fact] public void UnclippedZoomKeepsPaddingInsideItsPresentationArea() {
        var document=Document(Solid("gpu-zoom-padding.png",Color.Red));
        document.Layers[0].Viewport=new(){X=.25,Y=.25,Width=.5,Height=.5};
        document.Layers[0].ClipToViewport=false;
        document.Layers[0].Transform.Zoom=1.5;
        document.Layers[0].PaddingColor="#00FF00FF";
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);
        using var resolver=new WindowsMediaSourceResolver(Root,device);
        using var compositor=new DirectX11MediaCompositor(device,resolver);
        using var frame=compositor.Render(document,MediaTime.Zero,new(64,64));
        Assert.Equal(0,Pixel(frame,2,32)[0]);
        Assert.Equal(0,Pixel(frame,2,32)[1]);
    }
    /// <summary>A manually requested crop keeps its existing transformed local bounds for covered media.</summary>
    [Fact] public void ExplicitViewportCropKeepsItsTransformedBounds() {
        const string name="gpu-explicit-cover-crop.png";
        using(var image=new Bitmap(32,16)) {using(var graphics=Graphics.FromImage(image)) {graphics.Clear(Color.Red);}image.Save(Path.Combine(Root,name),ImageFormat.Png);}
        var document=Document(Reference(name,32,16));
        document.Layers[0].Viewport=new(){X=.25,Y=.25,Width=.5,Height=.5};
        document.Layers[0].Fit="cover";document.Layers[0].ClipToViewport=true;
        document.Layers[0].Transform.ScaleX=.5;document.Layers[0].Transform.ScaleY=.5;
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);
        using var resolver=new WindowsMediaSourceResolver(Root,device);
        using var compositor=new DirectX11MediaCompositor(device,resolver);
        using var frame=compositor.Render(document,MediaTime.Zero,new(64,64));
        Assert.Equal(0,Pixel(frame,18,32)[0]);
        Assert.Equal(255,Pixel(frame,32,32)[0]);
    }
    /// <summary>A covered image is sized by its presentation rectangle without masking its outer source pixels.</summary>
    [Fact] public void UnclippedCoverKeepsSourceEdgesVisibleWithoutZoom() {
        const string name="gpu-unclipped-cover.png";
        using(var image=new Bitmap(32,16)) {using(var graphics=Graphics.FromImage(image)) {graphics.Clear(Color.Red);}image.Save(Path.Combine(Root,name),ImageFormat.Png);}
        var document=Document(Reference(name,32,16));
        document.Layers[0].Viewport=new(){X=.25,Y=.25,Width=.5,Height=.5};
        document.Layers[0].Fit="cover";document.Layers[0].ClipToViewport=false;
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);
        using var resolver=new WindowsMediaSourceResolver(Root,device);
        using var compositor=new DirectX11MediaCompositor(device,resolver);
        using var frame=compositor.Render(document,MediaTime.Zero,new(64,64));
        Assert.Equal(255,Pixel(frame,12,32)[0]);
        Assert.Equal(0,Pixel(frame,12,2)[0]);
    }
    /// <summary>Changing preview resolution retains normalized layer position and size.</summary>
    [Fact] public void PreviewUsesProportionalLayout() {
        var document=Document(Solid("gpu-white-layout.png",Color.White));document.Layers[0].Viewport=new(){X=.25,Y=.25,Width=.5,Height=.5};
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);
        using var resolver=new WindowsMediaSourceResolver(Root,device);using var compositor=new DirectX11MediaCompositor(device,resolver);
        using var small=compositor.Render(document,MediaTime.Zero,new(32,32));using var large=compositor.Render(document,MediaTime.Zero,new(64,64));
        Assert.Equal(Pixel(small,16,16),Pixel(large,32,32));Assert.Equal(255,Pixel(small,16,16)[0]);Assert.Equal(0,Pixel(small,2,2)[0]);Assert.Equal(0,Pixel(large,4,4)[0]);
    }
    /// <summary>Filtering premultiplied linear samples keeps transparent edges free of dark fringes.</summary>
    [Fact] public void TransparentEdgeHasNoDarkHalo() {
        using(var image=new Bitmap(2,1)) {image.SetPixel(0,0,Color.Red);image.SetPixel(1,0,Color.FromArgb(0,0,0,0));image.Save(Path.Combine(Root,"gpu-edge.png"),ImageFormat.Png);}
        var reference=Reference("gpu-edge.png",2,1);var document=Document(reference);document.BackgroundColor="#00000000";
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);
        using var resolver=new WindowsMediaSourceResolver(Root,device);using var compositor=new DirectX11MediaCompositor(device,resolver);
        using var frame=compositor.Render(document,MediaTime.Zero,new(32,32));var pixel=Pixel(frame,16,16);
        Assert.InRange(pixel[3],100,150);Assert.Equal(255,pixel[0]);Assert.Equal(0,pixel[1]);
    }
    /// <summary>Capability validation fails before an invalid source path can be opened or drawn.</summary>
    [Fact] public void UnknownEffectFailsBeforeGpuAllocation() {
        var document=Document(new MediaReference{Id="missing",Kind="image",Path="missing.png",Sha256=new string('a',64),Width=16,Height=16});document.Layers[0].Effects.Add(new(){Id="unknown",Version=1});
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);
        using var resolver=new WindowsMediaSourceResolver(Root,device);using var compositor=new DirectX11MediaCompositor(device,resolver);
        var error=Assert.Throws<InvalidDataException>(()=>compositor.Render(document,MediaTime.Zero,new(32,32)));Assert.Contains("unknown_effect",error.Message);
    }
    /// <summary>Portable caption styles cannot read a font outside the host-owned assets root.</summary>
    [Fact] public void CaptionFontCannotEscapeAssetsRoot() {
        var document=Document();document.Layers.Add(new(){Id="caption",Kind="text",Start=MediaTime.Zero,End=new(3,1),Text=new(){Cues=[new(){Text="OI",Start=MediaTime.Zero,End=new(3,1)}],Style=JsonSerializer.SerializeToElement(new{FontFamily="Arial",FontFile="C:/Windows/Fonts/arial.ttf",FontSize=18,Animation="None"})}});
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);using var resolver=new WindowsMediaSourceResolver(Root,device);using var compositor=new DirectX11MediaCompositor(device,resolver);Assert.Throws<InvalidDataException>(()=>compositor.Render(document,new(1,1),new(32,32)));
    }
    /// <summary>Creates a small solid-color RGBA fixture.</summary>
    static MediaReference Solid(string name,Color color) {using var image=new Bitmap(16,16);using(var graphics=Graphics.FromImage(image)) {graphics.Clear(color);}image.Save(Path.Combine(Root,name),ImageFormat.Png);return Reference(name,16,16);}
    /// <summary>Builds the pinned reference to a test raster.</summary>
    static MediaReference Reference(string name,int width,int height) => new(){Id=name,Kind="image",Path=name,Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Root,name)))).ToLowerInvariant(),Width=width,Height=height};
    /// <summary>Creates a three-second black composition with sources in declaration order.</summary>
    static CompositionDocument Document(params MediaReference[] references) => new(){Id="gpu",Revision=1,Width=32,Height=32,Duration=new(3,1),FrameRate=new(24,1),BackgroundColor="#000000FF",Audio=new(){Enabled=false},Media=references.ToList(),Layers=references.Select((media,index)=>new VisualLayer{Id="layer-"+index,MediaId=media.Id,Start=MediaTime.Zero,End=new(3,1),Order=index}).ToList()};
    /// <summary>Reads one packed RGBA pixel for user-visible rendering assertions.</summary>
    static byte[] Pixel(MediaVideoFrame frame,int x,int y) => frame.Surface.ReadRgba().Slice((y*frame.Surface.Width+x)*4,4).ToArray();
    /// <summary>Display-matrix rotation is applied on GPU before aspect fitting.</summary>
    [Fact] public void RotatedVideoPixelsUseDisplayOrientation() {
        var reference=new MediaReference{Id="rotated",Kind="video",Path="rotated.mp4",Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Root,"rotated.mp4")))).ToLowerInvariant(),Width=64,Height=48,Duration=new(2,1)};
        var document=Document(reference);document.Width=48;document.Height=64;document.Duration=new(2,1);document.Layers[0].End=new(2,1);document.Layers[0].SourceOut=new(2,1);
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);
        using var resolver=new WindowsMediaSourceResolver(Root,device);using var source=resolver.Open(reference);
        using var raw=source.ReadVideoFrame(MediaTime.Zero);byte[] expected=Pixel(raw,53,8);
        using var compositor=new DirectX11MediaCompositor(device,resolver);using var output=compositor.Render(document,MediaTime.Zero,new(48,64));byte[] actual=Pixel(output,8,10);
        Assert.True(Enumerable.Range(0,4).All(channel=>Math.Abs(actual[channel]-expected[channel])<=1));
    }
    /// <summary>A newly pinned source cannot be replaced by a cached source with the same logical id.</summary>
    [Fact] public void SourceRevisionChangesCannotReuseOldPixels() {
        var red=Solid("gpu-revision-red.png",Color.Red);var blue=Solid("gpu-revision-blue.png",Color.Blue);blue.Id=red.Id;
        var document=Document(red);
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);
        using var resolver=new WindowsMediaSourceResolver(Root,device);using var compositor=new DirectX11MediaCompositor(device,resolver);
        using var before=compositor.Render(document,MediaTime.Zero,new(32,32));Assert.Equal(255,Pixel(before,16,16)[0]);
        document.Media[0]=blue;document.Revision++;
        using var after=compositor.Render(document,MediaTime.Zero,new(32,32));Assert.Equal(255,Pixel(after,16,16)[2]);Assert.Equal(0,Pixel(after,16,16)[0]);
    }
    /// <summary>An existing HLSL effect is selectable from the composition without an EXR conversion.</summary>
    [Fact] public void ExistingRainbowShaderRunsInsideComposition() {
        var red=Solid("gpu-rainbow-source.png",Color.Red);var mask=Solid("gpu-rainbow-mask.png",Color.White);
        var document=Document(red);document.Media.Add(mask);
        document.Layers[0].Effects.Add(new(){Id="rainbow-expand",Version=1,Inputs=new(){["Mask"]=mask.Id},Parameters=new(){["HueCyclesPerClip"]=JsonSerializer.SerializeToElement(1),["StartScale"]=JsonSerializer.SerializeToElement(1),["EndScale"]=JsonSerializer.SerializeToElement(1)}});
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);
        using var resolver=new WindowsMediaSourceResolver(Root,device);using var compositor=new DirectX11MediaCompositor(device,resolver);
        using var frame=compositor.Render(document,new(1,1),new(32,32));var pixel=Pixel(frame,16,16);
        Assert.InRange(pixel[0],85,87);Assert.InRange(pixel[1],112,114);Assert.Equal(255,pixel[2]);
    }
    /// <summary>A project-authored two-pass effect (tint into a half-size target, then a 3-tap copy) runs inside composition.</summary>
    [Fact] public void ProjectMultiPassEffectRunsInsideComposition() {
        string project=Path.Combine(Root,"project-effects",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(project,"assets","shaders"));
        try {
            File.WriteAllText(Path.Combine(project,"project.heproj"),"{}");
            File.WriteAllText(Path.Combine(project,"assets","shaders","TestTint.hlsl"),"""
#include "shaders/common/VfxCommon.hlsli"
Texture2D InputTexture : register(t0);
float4 TintPS(PSInput input) : SV_TARGET { float4 color = InputTexture.Sample(LinearClampSampler, input.UV); return float4(Params1.rgb * color.a, color.a); }
float4 CopyPS(PSInput input) : SV_TARGET { float2 offset = TexelSize * PassConstants.xy; return (InputTexture.Sample(LinearClampSampler, input.UV - offset) + InputTexture.Sample(LinearClampSampler, input.UV) + InputTexture.Sample(LinearClampSampler, input.UV + offset)) / 3.0; }
""");
            VfxEffectFile.Save(Path.Combine(project,"assets","tint.heffect"),new EffectAsset{EffectId="test-tint",DisplayName="Test Tint",Inputs=[new("Source",false)],Targets=[new("Half",.5f,EffectTargetFormat.Rgba16Float)],
                Passes=[new(){ShaderPath="shaders/TestTint.hlsl",PixelEntryPoint="TintPS",Reads=["Source"],Writes="Half"},new(){ShaderPath="shaders/TestTint.hlsl",PixelEntryPoint="CopyPS",Reads=["Half"],Writes=EffectAsset.OutputTargetName,PassConstants=new(1,0,0,0)}],
                Parameters=[new(){Name="Tint",Type=EffectParameterType.Color,DefaultValue=new(1,1,1,1),Minimum=0,Maximum=1,Slot=4}]});
            var red=Solid("gpu-project-effect-source.png",Color.Red);var document=Document(red);
            document.Layers[0].Effects.Add(new(){Id="test-tint",Version=1,Parameters=new(){["Tint"]=JsonSerializer.SerializeToElement(new[]{0.0,1.0,0.0})}});
            var catalog=VfxEffectCatalog.CreateForProject(project);
            using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);
            using var resolver=new WindowsMediaSourceResolver(Root,device);using var compositor=new DirectX11MediaCompositor(device,resolver,catalog);
            using var frame=compositor.Render(document,new(1,1),new(32,32));var pixel=Pixel(frame,16,16);
            Assert.Equal(0,pixel[0]);Assert.Equal(255,pixel[1]);Assert.Equal(0,pixel[2]);Assert.Equal(255,pixel[3]);
            Assert.Throws<InvalidDataException>(()=>new DirectX11MediaCompositor(device,resolver).Render(document,new(1,1),new(32,32)));
        } finally {Directory.Delete(project,true);}
    }
    /// <summary>Explicit diagnostics readback of an internal linear surface returns packed encoded RGBA.</summary>
    [Fact] public void LinearSurfaceReadbackIsRgba8() {
        var reference=Solid("gpu-linear-readback.png",Color.FromArgb(16,128,255));
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);
        using var resolver=new WindowsMediaSourceResolver(Root,device);using var source=resolver.Open(reference);
        using var original=source.ReadVideoFrame(MediaTime.Zero);using var pass=new DirectX11LayerPass(device,new MediaShaderCompiler());
        using var linear=pass.ConvertToLinear(original);var pixel=Pixel(linear,8,8);
        Assert.InRange(pixel[0],15,17);Assert.InRange(pixel[1],127,129);Assert.Equal(255,pixel[2]);Assert.Equal(255,pixel[3]);
    }
}
