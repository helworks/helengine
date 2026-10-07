using System.Text.Json;
namespace helengine.media.windows;
/// <summary>Renders all active visual layers through Helengine's own timeline and GPU composition.</summary>
public sealed class DirectX11MediaCompositor : IMediaCompositor {
    /// <summary>Render-session device borrowed by the compositor.</summary>
    readonly Device Device;
    /// <summary>Verified source resolver owned by the caller.</summary>
    readonly IMediaSourceResolver Resolver;
    /// <summary>Independent cursors cached per pinned media identity.</summary>
    readonly Dictionary<string,IMediaSource> Sources=new(StringComparer.Ordinal);
    /// <summary>Caption sources cached per layer identity.</summary>
    readonly Dictionary<string,CaptionLayerSource> Captions=new(StringComparer.Ordinal);
    /// <summary>Pinned media signatures prevent identity-only cache reuse.</summary>
    readonly Dictionary<string,string> SourceKeys=new(StringComparer.Ordinal);
    /// <summary>Text/style signatures prevent stale caption snapshots.</summary>
    readonly Dictionary<string,string> CaptionKeys=new(StringComparer.Ordinal);
    /// <summary>Compiled existing-effect passes reused across frames.</summary>
    readonly Dictionary<string,LegacyVfxPass> Effects=new(StringComparer.Ordinal);
    /// <summary>Lazy GPU pass, created only after contract validation succeeds.</summary>
    DirectX11LayerPass Pass;
    /// <summary>Reusable offscreen passes bounded by validated scene nesting and endpoints.</summary>
    readonly List<DirectX11LayerPass> ScenePasses=[];
    /// <summary>First free offscreen pass in the current frame.</summary>
    int NextScenePass;
    /// <summary>Marks session resources as disposed.</summary>
    bool Disposed;
    /// <summary>Retains explicit dependencies without creating GPU pipeline state.</summary>
    public DirectX11MediaCompositor(Device device,IMediaSourceResolver resolver) {Device=device ?? throw new ArgumentNullException(nameof(device));Resolver=resolver ?? throw new ArgumentNullException(nameof(resolver));}
    /// <summary>Validates input, selects active layers and renders a complete frame in stable layer order.</summary>
    public MediaVideoFrame Render(CompositionDocument document,MediaTime time,RenderSize size) {
        if(Disposed) {throw new ObjectDisposedException(nameof(DirectX11MediaCompositor));}
        var errors=CompositionValidator.Validate(document,WindowsMediaCapabilities.Describe());if(errors.Count>0) {throw new InvalidDataException(string.Join("; ",errors.Select(error=>error.Code+": "+error.Message)));}
        Pass ??= new(Device,new MediaShaderCompiler());Pass.Begin(size,MediaColor.Parse(document.BackgroundColor));
        NextScenePass=0;
        var evaluations=CompositionEvaluator.Evaluate(document,time).ToDictionary(item=>item.Layer.Id);var owned=document.Layers.SelectMany(layer=>layer.Members).ToHashSet(StringComparer.Ordinal);var hidden=new HashSet<string>(StringComparer.Ordinal);var active=new List<CompositionTransition>();
        foreach(var transition in document.Transitions) {
            if(time<transition.Start) {hidden.Add(transition.ToLayer);}else if(time>=transition.Start+transition.Duration) {hidden.Add(transition.FromLayer);}else {hidden.Add(transition.FromLayer);hidden.Add(transition.ToLayer);active.Add(transition);}
        }
        var roots=evaluations.Values.Where(item=>!owned.Contains(item.Layer.Id) && !hidden.Contains(item.Layer.Id)).ToList();
        var orders=roots.Select(item=>item.Layer.Order).Concat(active.Select(item=>Math.Max(evaluations[item.FromLayer].Layer.Order,evaluations[item.ToLayer].Layer.Order))).Distinct().Order().ToList();
        foreach(int order in orders) {
            foreach(var evaluation in roots.Where(item=>item.Layer.Order==order)) {DrawLayer(Pass,document,evaluation,evaluations,time,size);}
            foreach(var transition in active.Where(item=>Math.Max(evaluations[item.FromLayer].Layer.Order,evaluations[item.ToLayer].Layer.Order)==order)) {
                using var from=Scene(document,evaluations[transition.FromLayer],evaluations,time,size);using var to=Scene(document,evaluations[transition.ToLayer],evaluations,time,size);using var mixed=Pass.Crossfade(from,to,TransitionEvaluation.At(transition,time).Progress,time,size);
                Pass.Draw(new(new(){Id=transition.Id},MediaTime.Zero,MediaTime.Zero,new()),mixed,null,size);
            }
        }
        return Pass.Finish(time,size);
    }
    /// <summary>Reuses one independent target for each scene visit rather than allocating shader state per frame.</summary>
    DirectX11LayerPass TakeScenePass() {if(NextScenePass==ScenePasses.Count) {ScenePasses.Add(new(Device,new MediaShaderCompiler()));}return ScenePasses[NextScenePass++];}
    /// <summary>Renders an endpoint with all its transforms and children before transition interpolation.</summary>
    MediaVideoFrame Scene(CompositionDocument document,LayerEvaluation evaluation,Dictionary<string,LayerEvaluation> evaluations,MediaTime time,RenderSize size) {
        var scene=TakeScenePass();scene.Begin(size,new MediaColor(0,0,0,0));DrawLayer(scene,document,evaluation,evaluations,time,size);return scene.FinishLinear(time,size);
    }
    /// <summary>Draws a source or recursively composed group exactly once in its owning scene.</summary>
    void DrawLayer(DirectX11LayerPass target,CompositionDocument document,LayerEvaluation evaluation,Dictionary<string,LayerEvaluation> evaluations,MediaTime time,RenderSize size) {
        MediaVideoFrame original;
        if(evaluation.Layer.Kind=="group") {
            var group=TakeScenePass();group.Begin(size,new MediaColor(0,0,0,0));
            foreach(var child in evaluation.Layer.Members.Where(evaluations.ContainsKey).Select(id=>evaluations[id]).OrderBy(item=>item.Layer.Order)) {DrawLayer(group,document,child,evaluations,time,size);}original=group.FinishLinear(time,size);
        }else {original=Frame(document,evaluation,time,size);}
        using(original) {using var source=ApplyEffects(document,evaluation,original);MediaVideoFrame mask=null;try {if(evaluation.Layer.Mask!=null) {mask=Source(document,evaluation.Layer.Mask.MediaId).ReadVideoFrame(evaluation.SourceTime);}target.Draw(evaluation,source,mask,size);}finally {mask?.Dispose();}}
    }
    /// <summary>Produces a text or media frame without leaking ownership into the layer pass.</summary>
    MediaVideoFrame Frame(CompositionDocument document,LayerEvaluation evaluation,MediaTime time,RenderSize size) {
        if(evaluation.Layer.Kind=="text") {string key=document.Width+":"+JsonSerializer.Serialize(evaluation.Layer.Text);if(Captions.TryGetValue(evaluation.Layer.Id,out var old) && CaptionKeys[evaluation.Layer.Id]!=key) {old.Dispose();Captions.Remove(evaluation.Layer.Id);CaptionKeys.Remove(evaluation.Layer.Id);}if(!Captions.TryGetValue(evaluation.Layer.Id,out var caption)) {caption=new(evaluation.Layer.Text,document.Width,Device,(Resolver as WindowsMediaSourceResolver)?.AssetsRoot);Captions.Add(evaluation.Layer.Id,caption);CaptionKeys.Add(evaluation.Layer.Id,key);}return caption.Render(time,size);}
        return Source(document,evaluation.Layer.MediaId).ReadVideoFrame(evaluation.SourceTime);
    }
    /// <summary>Opens one independent source cursor only when an active layer needs it.</summary>
    IMediaSource Source(CompositionDocument document,string id) {
        var reference=document.Media.Single(media=>media.Id==id);string key=JsonSerializer.Serialize(reference);if(Sources.TryGetValue(id,out var old) && SourceKeys[id]!=key) {old.Dispose();Sources.Remove(id);SourceKeys.Remove(id);}if(!Sources.TryGetValue(id,out var source)) {source=Resolver.Open(reference);Sources.Add(id,source);SourceKeys.Add(id,key);}return source;
    }
    /// <summary>Releases media, text and GPU pass resources while preserving caller-owned dependencies.</summary>
    public void Dispose() {if(Disposed) {return;}Disposed=true;foreach(var caption in Captions.Values) {caption.Dispose();}foreach(var source in Sources.Values) {source.Dispose();}foreach(var effect in Effects.Values) {effect.Dispose();}foreach(var scene in ScenePasses) {scene.Dispose();}ScenePasses.Clear();Pass?.Dispose();Captions.Clear();Sources.Clear();Effects.Clear();SourceKeys.Clear();CaptionKeys.Clear();}
    /// <summary>Runs registered effects in order and returns an independent surface for the layer pass.</summary>
    MediaVideoFrame ApplyEffects(CompositionDocument document,LayerEvaluation evaluation,MediaVideoFrame original) {
        MediaVideoFrame current=original;bool ownsCurrent=false;
        try {
            foreach(var request in evaluation.Layer.Effects.Where(effect=>effect.Id!="transform.2d")) {
                var implementation=WindowsMediaCapabilities.Effects.Single(effect=>effect.Id==request.Id);
                var inputs=new List<MediaVideoFrame>();var owned=new List<MediaVideoFrame>();
                try {
                    for(int index=0;index<implementation.InputRoles.Count;index++) {
                        string role=implementation.InputRoles[index];var input=index==0?current:Source(document,request.Inputs[role]).ReadVideoFrame(evaluation.SourceTime);if(index!=0) {owned.Add(input);}
                        if(input.Surface is not DirectX11VideoSurface surface) {throw new InvalidOperationException("Effect input uses another GPU backend.");}
                        if(implementation.AlphaRequiredInputRoles.Contains(role) && !surface.HasStoredAlpha) {throw new InvalidDataException("Effect role requires actual source alpha: "+role);}
                        if(role!="Mask" && role!="RenderDepth") {input=Pass.ConvertToLinear(input);owned.Add(input);}inputs.Add(input);
                    }
                    if(!Effects.TryGetValue(request.Id,out var effectPass)) {effectPass=new(Device,implementation,new MediaShaderCompiler());Effects.Add(request.Id,effectPass);}
                    var output=effectPass.Render(request,inputs,evaluation.LocalTime.ToSeconds()/(evaluation.Layer.End-evaluation.Layer.Start).ToSeconds());if(ownsCurrent) {current.Dispose();}current=output;ownsCurrent=true;
                } finally {foreach(var input in owned) {input.Dispose();}}
            }
            if(ownsCurrent) {return current;}
            if(original.Surface is not DirectX11VideoSurface direct) {throw new InvalidOperationException("Source surface uses another GPU backend.");}
            Marshal.AddRef(direct.Texture.NativePointer);return new(original.Timestamp,original.Duration,new DirectX11VideoSurface(Device,new Texture2D(direct.Texture.NativePointer),direct.HasStoredAlpha,direct.IsLinear),original.RotationDegrees);
        } catch {if(ownsCurrent) {current.Dispose();}throw;}
    }
}
