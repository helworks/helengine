using System.Globalization;
using System.Text.Json;
namespace helengine.media.windows;
/// <summary>Runs one catalog effect over composition GPU frames through the shared multi-pass executor.</summary>
public sealed class MediaEffectPass : IDisposable {
    /// <summary>Session device borrowed by the effect.</summary>
    readonly Device Device;
    /// <summary>Executor holding the effect's compiled passes and intermediate targets.</summary>
    readonly DirectX11EffectExecutor Executor;
    /// <summary>Compiles the effect's passes for the session device.</summary>
    /// <param name="device">Session device.</param>
    /// <param name="entry">Catalog entry of the effect.</param>
    /// <param name="compiler">Shared effect shader compiler.</param>
    public MediaEffectPass(Device device,VfxEffectCatalogEntry entry,DirectX11EffectShaderCompiler compiler) {Device=device;Executor=new(device,entry,compiler);}
    /// <summary>Resolves the request parameters and renders the effect into a new linear float surface sized like the main input.</summary>
    /// <param name="request">Composition effect request carrying parameters.</param>
    /// <param name="inputs">Frames in the effect's input order; the first is the main input.</param>
    /// <param name="progress">Layer progress in [0, 1].</param>
    /// <returns>New frame owning the rendered surface.</returns>
    public MediaVideoFrame Render(MediaEffect request,IReadOnlyList<MediaVideoFrame> inputs,double progress) {
        var primary=inputs[0];var raw=request.Parameters.ToDictionary(parameter=>parameter.Key,parameter=>Raw(parameter.Value),StringComparer.Ordinal);float[] slots=VfxParameterSlotResolver.Resolve(Executor.Effect,raw);
        int width=primary.Surface.Width,height=primary.Surface.Height;var output=new Texture2D(Device,new Texture2DDescription{Width=width,Height=height,MipLevels=1,ArraySize=1,Format=Format.R16G16B16A16_Float,SampleDescription=new(1,0),Usage=ResourceUsage.Default,BindFlags=BindFlags.RenderTarget|BindFlags.ShaderResource});var views=new List<ShaderResourceView>();
        try {
            foreach(var input in inputs) {if(input.Surface is not DirectX11VideoSurface surface) {throw new InvalidOperationException("Effect source belongs to another backend.");}views.Add(new(Device,surface.Texture));}
            using var target=new RenderTargetView(Device,output);Executor.Execute(views,width,height,slots,(float)Math.Clamp(progress,0,1),target);
            return new(primary.Timestamp,primary.Duration,new DirectX11VideoSurface(Device,output,true,true),primary.RotationDegrees);
        } catch {output.Dispose();throw;}finally {foreach(var view in views) {view.Dispose();}}
    }
    /// <summary>Converts a composition JSON parameter into the textual form the slot resolver parses.</summary>
    /// <param name="value">Parameter value from the composition.</param>
    /// <returns>Invariant text: a number, a name, true/false or comma-separated components.</returns>
    static string Raw(JsonElement value) => value.ValueKind switch {JsonValueKind.String=>value.GetString(),JsonValueKind.True=>"true",JsonValueKind.False=>"false",JsonValueKind.Number=>value.GetDouble().ToString("R",CultureInfo.InvariantCulture),JsonValueKind.Array=>string.Join(",",value.EnumerateArray().Select(component=>component.GetDouble().ToString("R",CultureInfo.InvariantCulture))),_=>throw new InvalidDataException("Unsupported shader parameter shape.")};
    /// <summary>Releases the executor's GPU resources.</summary>
    public void Dispose() {Executor.Dispose();}
}
