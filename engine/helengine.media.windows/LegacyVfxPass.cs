using System.Globalization;
using System.Text.Json;
using SharpDX.Direct3D;
using helengine.vfx;
using D3DBuffer=SharpDX.Direct3D11.Buffer;
namespace helengine.media.windows;
/// <summary>Runs an existing engine VFX shader over GPU sources without sequence files or FFmpeg effects.</summary>
public sealed class LegacyVfxPass : IDisposable {
    /// <summary>Session device borrowed by the shader pass.</summary>
    readonly Device Device;
    /// <summary>Registered implementation whose own resolver defines shader parameter slots.</summary>
    readonly IVfxEffect Effect;
    /// <summary>Original fullscreen vertex shader.</summary>
    readonly VertexShader Vertex;
    /// <summary>Original effect pixel shader.</summary>
    readonly PixelShader Pixel;
    /// <summary>Original shared VFX constant layout.</summary>
    readonly D3DBuffer Constants;
    /// <summary>Original bilinear clamped input sampler.</summary>
    readonly SamplerState Sampler;
    /// <summary>Fullscreen triangle rasterizer state.</summary>
    readonly RasterizerState Raster;
    /// <summary>Compiles package-owned shader code using the engine's shared compiler.</summary>
    public LegacyVfxPass(Device device,IVfxEffect effect,MediaShaderCompiler compiler) {
        Device=device;Effect=effect;Vertex=new(device,compiler.Compile(effect.ShaderResourcePath,effect.VertexEntryPoint,ShaderStage.Vertex));Pixel=new(device,compiler.Compile(effect.ShaderResourcePath,effect.PixelEntryPoint,ShaderStage.Pixel));
        Constants=new(device,new BufferDescription{SizeInBytes=VfxFrameConstants.TotalFloatCount*4,Usage=ResourceUsage.Dynamic,BindFlags=BindFlags.ConstantBuffer,CpuAccessFlags=CpuAccessFlags.Write});Sampler=new(device,new SamplerStateDescription{Filter=Filter.MinMagMipLinear,AddressU=TextureAddressMode.Clamp,AddressV=TextureAddressMode.Clamp,AddressW=TextureAddressMode.Clamp,MaximumLod=float.MaxValue});Raster=new(device,new RasterizerStateDescription{CullMode=CullMode.None,FillMode=FillMode.Solid,IsDepthClipEnabled=true});
    }
    /// <summary>Runs one validated effect frame, preserving the existing linear HDR shader semantics.</summary>
    public MediaVideoFrame Render(MediaEffect request,IReadOnlyList<MediaVideoFrame> inputs,double progress) {
        var primary=inputs[0];var raw=request.Parameters.ToDictionary(parameter=>parameter.Key,parameter=>Raw(parameter.Value),StringComparer.Ordinal);float[] slots=Effect.ResolveParameterSlots(raw);
        if(slots.Any(value=>!float.IsFinite(value))) {throw new InvalidDataException("Effect resolved a non-finite shader parameter.");}
        var size=new RenderSize(primary.Surface.Width,primary.Surface.Height);var description=new Texture2DDescription{Width=size.Width,Height=size.Height,MipLevels=1,ArraySize=1,Format=Format.R16G16B16A16_Float,SampleDescription=new(1,0),Usage=ResourceUsage.Default,BindFlags=BindFlags.RenderTarget|BindFlags.ShaderResource};var output=new Texture2D(Device,description);var views=new List<ShaderResourceView>();
        try {
            foreach(var input in inputs) {if(input.Surface is not DirectX11VideoSurface surface) {throw new InvalidOperationException("Effect source belongs to another backend.");}views.Add(new(Device,surface.Texture));}
            using var target=new RenderTargetView(Device,output);var context=Device.ImmediateContext;float[] values=VfxFrameConstants.Build((float)Math.Clamp(progress,0,1),size.Width,size.Height,slots);var mapped=context.MapSubresource(Constants,0,MapMode.WriteDiscard,MapFlags.None);try {Marshal.Copy(values,0,mapped.DataPointer,values.Length);}finally {context.UnmapSubresource(Constants,0);}
            context.OutputMerger.SetTargets(target);context.OutputMerger.SetBlendState(null);context.InputAssembler.PrimitiveTopology=PrimitiveTopology.TriangleList;context.Rasterizer.State=Raster;context.Rasterizer.SetViewport(0,0,size.Width,size.Height);context.VertexShader.Set(Vertex);context.PixelShader.Set(Pixel);context.PixelShader.SetConstantBuffer(0,Constants);context.PixelShader.SetSampler(0,Sampler);
            for(int index=0;index<views.Count;index++) {context.PixelShader.SetShaderResource(index,views[index]);}context.Draw(3,0);for(int index=0;index<views.Count;index++) {context.PixelShader.SetShaderResource(index,null);}context.OutputMerger.SetTargets(Array.Empty<RenderTargetView>());
            return new(primary.Timestamp,primary.Duration,new DirectX11VideoSurface(Device,output,true,true),primary.RotationDegrees);
        } catch {output.Dispose();throw;}finally {foreach(var view in views) {view.Dispose();}}
    }
    /// <summary>Converts already-validated data to the existing resolver's invariant parameter representation.</summary>
    static string Raw(JsonElement value) => value.ValueKind switch {JsonValueKind.String=>value.GetString(),JsonValueKind.Number=>value.GetDouble().ToString("R",CultureInfo.InvariantCulture),JsonValueKind.Array=>string.Join(",",value.EnumerateArray().Select(component=>component.GetDouble().ToString("R",CultureInfo.InvariantCulture))),_=>throw new InvalidDataException("Unsupported shader parameter shape.")};
    /// <summary>Releases the pass's compiled shaders and reusable pipeline state.</summary>
    public void Dispose() {Vertex.Dispose();Pixel.Dispose();Constants.Dispose();Sampler.Dispose();Raster.Dispose();}
}
