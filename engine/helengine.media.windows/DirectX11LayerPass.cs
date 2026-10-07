using SharpDX;
using SharpDX.Direct3D;
using D3DBuffer=SharpDX.Direct3D11.Buffer;
namespace helengine.media.windows;
/// <summary>Reuses shader pipeline state and a linear premultiplied composition target across frames.</summary>
public sealed class DirectX11LayerPass : IDisposable {
    /// <summary>Session-owned device borrowed by this pass.</summary>
    readonly Device Device;
    /// <summary>Shared fullscreen triangle shader.</summary>
    readonly VertexShader Vertex;
    /// <summary>Layer sampling, transform, padding and mask shader.</summary>
    readonly PixelShader Layer;
    /// <summary>Final linear-premultiplied to straight-sRGB conversion shader.</summary>
    readonly PixelShader Final;
    /// <summary>Source color conversion pass for existing linear-HDR VFX shaders.</summary>
    readonly PixelShader LinearSource;
    /// <summary>Final straight-linear group output conversion.</summary>
    readonly PixelShader FinalLinear;
    /// <summary>Equal-weight premultiplied scene interpolation.</summary>
    readonly PixelShader CrossfadeShader;
    /// <summary>Bounded frame-specific transform constants.</summary>
    readonly D3DBuffer Constants;
    /// <summary>Premultiplied linear alpha-over blend state.</summary>
    readonly BlendState Blend;
    /// <summary>Fullscreen geometry has no culled face.</summary>
    readonly RasterizerState Raster;
    /// <summary>Reusable linear HDR target; recreated only when preview resolution changes.</summary>
    Texture2D Target;
    /// <summary>Target view owned alongside the target.</summary>
    RenderTargetView TargetView;
    /// <summary>Target sampling view used for final output conversion.</summary>
    ShaderResourceView TargetSample;
    /// <summary>Marks pipeline resources as released.</summary>
    bool Disposed;
    /// <summary>Compiles the three entry points before allocating pipeline objects.</summary>
    public DirectX11LayerPass(Device device,MediaShaderCompiler compiler) {
        Device=device;
        byte[] vertex=compiler.Compile("media/shaders/MediaComposite.hlsl","FullscreenVS",ShaderStage.Vertex);
        byte[] layer=compiler.Compile("media/shaders/MediaComposite.hlsl","LayerPS",ShaderStage.Pixel);
        byte[] final=compiler.Compile("media/shaders/MediaComposite.hlsl","FinalPS",ShaderStage.Pixel);
        Vertex=new(device,vertex);Layer=new(device,layer);Final=new(device,final);LinearSource=new(device,compiler.Compile("media/shaders/MediaComposite.hlsl","LinearSourcePS",ShaderStage.Pixel));
        FinalLinear=new(device,compiler.Compile("media/shaders/MediaComposite.hlsl","FinalLinearPS",ShaderStage.Pixel));CrossfadeShader=new(device,compiler.Compile("media/shaders/MediaCrossfade.hlsl","CrossfadePS",ShaderStage.Pixel));
        Constants=new(device,new BufferDescription {SizeInBytes=128,Usage=ResourceUsage.Dynamic,BindFlags=BindFlags.ConstantBuffer,CpuAccessFlags=CpuAccessFlags.Write});
        var blend=new BlendStateDescription();blend.RenderTarget[0].IsBlendEnabled=true;blend.RenderTarget[0].SourceBlend=BlendOption.One;blend.RenderTarget[0].DestinationBlend=BlendOption.InverseSourceAlpha;blend.RenderTarget[0].BlendOperation=BlendOperation.Add;blend.RenderTarget[0].SourceAlphaBlend=BlendOption.One;blend.RenderTarget[0].DestinationAlphaBlend=BlendOption.InverseSourceAlpha;blend.RenderTarget[0].AlphaBlendOperation=BlendOperation.Add;blend.RenderTarget[0].RenderTargetWriteMask=ColorWriteMaskFlags.All;Blend=new(device,blend);
        Raster=new(device,new RasterizerStateDescription {CullMode=CullMode.None,FillMode=FillMode.Solid,IsDepthClipEnabled=true});
    }
    /// <summary>Starts one frame by clearing a correctly sized linear composition target.</summary>
    public void Begin(RenderSize size,MediaColor background) {
        if(Disposed) {throw new ObjectDisposedException(nameof(DirectX11LayerPass));}
        if(Target==null || Target.Description.Width!=size.Width || Target.Description.Height!=size.Height) {
            TargetSample?.Dispose();TargetView?.Dispose();Target?.Dispose();Target=new(Device,Description(size,Format.R16G16B16A16_Float));TargetView=new(Device,Target);TargetSample=new(Device,Target);
        }
        var context=Device.ImmediateContext;context.PixelShader.SetShaderResource(2,null);context.OutputMerger.SetTargets(TargetView);
        context.ClearRenderTargetView(TargetView,new SharpDX.Mathematics.Interop.RawColor4((float)(MediaColor.ToLinear(background.Red)*background.Alpha),(float)(MediaColor.ToLinear(background.Green)*background.Alpha),(float)(MediaColor.ToLinear(background.Blue)*background.Alpha),(float)background.Alpha));
        context.InputAssembler.PrimitiveTopology=PrimitiveTopology.TriangleList;context.Rasterizer.State=Raster;context.Rasterizer.SetViewport(0,0,size.Width,size.Height);context.VertexShader.Set(Vertex);
    }
    /// <summary>Samples one independently animated source into the linear target with optional coverage masking.</summary>
    public void Draw(LayerEvaluation evaluation,MediaVideoFrame source,MediaVideoFrame mask,RenderSize size) {
        if(source.Surface is not DirectX11VideoSurface surface) {throw new InvalidOperationException("Media surface belongs to another graphics backend.");}
        var restore=Device.ImmediateContext;restore.OutputMerger.SetTargets(TargetView);restore.InputAssembler.PrimitiveTopology=PrimitiveTopology.TriangleList;restore.Rasterizer.State=Raster;restore.Rasterizer.SetViewport(0,0,size.Width,size.Height);restore.VertexShader.Set(Vertex);
        var viewport=evaluation.Layer.Viewport;double width=viewport.Width*size.Width;double height=viewport.Height*size.Height;var transform=evaluation.Transform;
        var mapping=PresentationTransform.Resolve(source.DisplayWidth,source.DisplayHeight,width,height,evaluation.Layer.Fit,transform.Zoom,transform.FocusX,transform.FocusY);var padding=MediaColor.Parse(evaluation.Layer.PaddingColor);
        float[] values=[(float)(viewport.X*size.Width),(float)(viewport.Y*size.Height),(float)width,(float)height,(float)mapping.ImageX,(float)mapping.ImageY,(float)mapping.FittedWidth,(float)mapping.FittedHeight,(float)mapping.CropX,(float)mapping.CropY,(float)mapping.CropWidth,(float)mapping.CropHeight,(float)transform.PositionX,(float)transform.PositionY,(float)transform.ScaleX,(float)transform.ScaleY,(float)(transform.RotationDegrees*Math.PI/180),(float)transform.Opacity,source.RotationDegrees,evaluation.Layer.ClipToViewport?1:0,(float)(MediaColor.ToLinear(padding.Red)*padding.Alpha),(float)(MediaColor.ToLinear(padding.Green)*padding.Alpha),(float)(MediaColor.ToLinear(padding.Blue)*padding.Alpha),(float)padding.Alpha,mask!=null?1:0,evaluation.Layer.Mask?.Channel=="luma"?1:0,evaluation.Layer.Mask?.Inverted==true?1:0,mask?.RotationDegrees??0,surface.IsLinear?1:0,0,0,0];
        var context=Device.ImmediateContext;var mapped=context.MapSubresource(Constants,0,MapMode.WriteDiscard,MapFlags.None);try {Marshal.Copy(values,0,mapped.DataPointer,values.Length);}finally {context.UnmapSubresource(Constants,0);}
        using var view=new ShaderResourceView(Device,surface.Texture);ShaderResourceView maskView=null;
        try {
            if(mask!=null) {if(mask.Surface is not DirectX11VideoSurface maskSurface) {throw new InvalidOperationException("Mask surface belongs to another backend.");}maskView=new(Device,maskSurface.Texture);}
            context.Rasterizer.SetViewport(0,0,size.Width,size.Height);context.InputAssembler.PrimitiveTopology=PrimitiveTopology.TriangleList;context.Rasterizer.State=Raster;context.VertexShader.Set(Vertex);context.OutputMerger.SetTargets(TargetView);context.OutputMerger.SetBlendState(Blend);context.PixelShader.Set(Layer);context.PixelShader.SetConstantBuffer(0,Constants);context.PixelShader.SetShaderResource(0,view);context.PixelShader.SetShaderResource(1,maskView);context.Draw(3,0);
        } finally {context.PixelShader.SetShaderResource(0,null);context.PixelShader.SetShaderResource(1,null);maskView?.Dispose();}
    }
    /// <summary>Writes an independently owned straight-alpha sRGB frame without downloading the composition.</summary>
    public MediaVideoFrame Finish(MediaTime time,RenderSize size) {
        var output=new Texture2D(Device,Description(size,Format.R8G8B8A8_UNorm));
        try {using var view=new RenderTargetView(Device,output);var context=Device.ImmediateContext;context.OutputMerger.SetTargets(view);context.OutputMerger.SetBlendState(null);context.PixelShader.Set(Final);context.PixelShader.SetShaderResource(2,TargetSample);context.Draw(3,0);context.PixelShader.SetShaderResource(2,null);context.OutputMerger.SetTargets(Array.Empty<RenderTargetView>());return new(time,MediaTime.Zero,new DirectX11VideoSurface(Device,output));}
        catch {output.Dispose();throw;}
    }
    /// <summary>Returns independently owned straight-linear group pixels without an encoded intermediate.</summary>
    public MediaVideoFrame FinishLinear(MediaTime time,RenderSize size) {
        var output=new Texture2D(Device,Description(size,Format.R16G16B16A16_Float));
        try {using var view=new RenderTargetView(Device,output);var context=Device.ImmediateContext;context.OutputMerger.SetTargets(view);context.OutputMerger.SetBlendState(null);context.PixelShader.Set(FinalLinear);context.PixelShader.SetShaderResource(2,TargetSample);context.Draw(3,0);context.PixelShader.SetShaderResource(2,null);context.OutputMerger.SetTargets(Array.Empty<RenderTargetView>());return new(time,MediaTime.Zero,new DirectX11VideoSurface(Device,output,true,true));}catch {output.Dispose();throw;}
    }
    /// <summary>Interpolates complete straight-linear scene surfaces through premultiplied color.</summary>
    public MediaVideoFrame Crossfade(MediaVideoFrame from,MediaVideoFrame to,double progress,MediaTime time,RenderSize size) {
        var output=new Texture2D(Device,Description(size,Format.R16G16B16A16_Float));
        try {using var view=new RenderTargetView(Device,output);using var a=new ShaderResourceView(Device,((DirectX11VideoSurface)from.Surface).Texture);using var b=new ShaderResourceView(Device,((DirectX11VideoSurface)to.Surface).Texture);var context=Device.ImmediateContext;float[] values=new float[32];values[0]=(float)progress;var mapped=context.MapSubresource(Constants,0,MapMode.WriteDiscard,MapFlags.None);try {Marshal.Copy(values,0,mapped.DataPointer,32);}finally {context.UnmapSubresource(Constants,0);}context.InputAssembler.PrimitiveTopology=PrimitiveTopology.TriangleList;context.Rasterizer.State=Raster;context.Rasterizer.SetViewport(0,0,size.Width,size.Height);context.VertexShader.Set(Vertex);context.OutputMerger.SetTargets(view);context.OutputMerger.SetBlendState(null);context.PixelShader.Set(CrossfadeShader);context.PixelShader.SetConstantBuffer(0,Constants);context.PixelShader.SetShaderResources(0,a,b);context.Draw(3,0);context.PixelShader.SetShaderResources(0,new ShaderResourceView[]{null,null});context.OutputMerger.SetTargets(Array.Empty<RenderTargetView>());return new(time,MediaTime.Zero,new DirectX11VideoSurface(Device,output,true,true));}catch {output.Dispose();throw;}
    }
    /// <summary>Releases all session-owned target and pipeline resources.</summary>
    public void Dispose() {if(Disposed) {return;}Disposed=true;Device.ImmediateContext.ClearState();TargetSample?.Dispose();TargetView?.Dispose();Target?.Dispose();Vertex.Dispose();Layer.Dispose();Final.Dispose();LinearSource.Dispose();FinalLinear.Dispose();CrossfadeShader.Dispose();Constants.Dispose();Blend.Dispose();Raster.Dispose();}
    /// <summary>Defines a bounded shader-readable render target.</summary>
    static Texture2DDescription Description(RenderSize size,Format format) => new(){Width=size.Width,Height=size.Height,MipLevels=1,ArraySize=1,Format=format,SampleDescription=new(1,0),Usage=ResourceUsage.Default,BindFlags=BindFlags.RenderTarget|BindFlags.ShaderResource};
    /// <summary>Converts a color input to straight linear HDR without changing geometry or source timing.</summary>
    public MediaVideoFrame ConvertToLinear(MediaVideoFrame frame) {
        if(frame.Surface is not DirectX11VideoSurface source) {throw new InvalidOperationException("VFX color surface belongs to another backend.");}
        var size=new RenderSize(source.Width,source.Height);var output=new Texture2D(Device,Description(size,Format.R16G16B16A16_Float));
        try {
            using var view=new RenderTargetView(Device,output);using var sample=new ShaderResourceView(Device,source.Texture);float[] values=new float[32];values[28]=source.IsLinear?1:0;
            var context=Device.ImmediateContext;var mapped=context.MapSubresource(Constants,0,MapMode.WriteDiscard,MapFlags.None);try {Marshal.Copy(values,0,mapped.DataPointer,32);}finally {context.UnmapSubresource(Constants,0);}
            context.InputAssembler.PrimitiveTopology=PrimitiveTopology.TriangleList;context.Rasterizer.State=Raster;context.OutputMerger.SetTargets(view);context.OutputMerger.SetBlendState(null);context.Rasterizer.SetViewport(0,0,size.Width,size.Height);context.VertexShader.Set(Vertex);context.PixelShader.Set(LinearSource);context.PixelShader.SetConstantBuffer(0,Constants);context.PixelShader.SetShaderResource(0,sample);context.Draw(3,0);context.PixelShader.SetShaderResource(0,null);context.OutputMerger.SetTargets(Array.Empty<RenderTargetView>());
            return new(frame.Timestamp,frame.Duration,new DirectX11VideoSurface(Device,output,source.HasStoredAlpha,true),frame.RotationDegrees);
        } catch {output.Dispose();throw;}
    }
}
