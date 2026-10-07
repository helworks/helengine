namespace helengine.media;
/// <summary>Rejects invalid executable inputs before media decoding or GPU allocation.</summary>
public static class CompositionValidator {
    /// <summary>Collects protocol, timing, identity, reference and capability errors.</summary>
    public static IReadOnlyList<CompositionDiagnostic> Validate(CompositionDocument document,MediaCapabilities capabilities) {
        ArgumentNullException.ThrowIfNull(document); ArgumentNullException.ThrowIfNull(capabilities);
        var errors=new List<CompositionDiagnostic>();
        if (document.Schema!="helengine.media.composition.v1") { Add(errors,"invalid_schema","schema","Unsupported composition version."); }
        if (string.IsNullOrWhiteSpace(document.Id) || document.Revision<1) { Add(errors,"invalid_identity","id","Composition requires identity and positive revision."); }
        try {MediaColor.Parse(document.BackgroundColor);} catch(InvalidDataException) {Add(errors,"invalid_color","background_color","Canvas color is invalid.");}
        if (document.Width<16 || document.Height<16 || document.Width>8192 || document.Height>8192 || (long)document.Width*document.Height>33554432) { Add(errors,"invalid_dimensions","width","Output dimensions are outside supported allocation limits."); }
        if (document.Audio==null || document.Audio.SampleRate<8000 || document.Audio.SampleRate>192000 || document.Audio.Channels!=2) { Add(errors,"invalid_audio","audio","Output requires a valid rate and stereo channels."); }
        if (!ValidTime(document.Duration) || document.Duration<=MediaTime.Zero || !ValidTime(document.FrameRate) || document.FrameRate<=MediaTime.Zero || document.FrameRate>new MediaTime(240,1)) { Add(errors,"invalid_timing","duration","Duration and frame rate must be positive rational values."); }
        if (document.Media==null || document.Layers==null || document.AudioClips==null || document.Transitions==null) { Add(errors,"invalid_collection","media","Timeline collections cannot be null."); return errors; }
        if (document.Media.Count>4096 || document.Layers.Count>16384 || document.AudioClips.Count>16384 || document.Transitions.Count>8192) { Add(errors,"too_many_items","layers","Composition exceeds item limits."); return errors; }
        var media=new HashSet<string>(StringComparer.Ordinal);
        for (int i=0;i<document.Media.Count;i++) {
            MediaReference item=document.Media[i]; string path=$"media[{i}]";
            if (item==null) { Add(errors,"invalid_item",path,"Media item cannot be null."); continue; }
            CheckIdentity(errors,media,item.Id,path);
            if (item.Kind!="image" && item.Kind!="video" && item.Kind!="audio") { Add(errors,"invalid_media",path+".kind","Unknown media kind."); }
            if (string.IsNullOrWhiteSpace(item.Path) || System.IO.Path.IsPathRooted(item.Path) || item.Path.Replace('\\','/').Split('/').Any(p=>p=="..")) { Add(errors,"invalid_path",path+".path","Media requires a relative owned path."); }
            if (item.Sha256==null || item.Sha256.Length!=64 || item.Sha256.Any(c=>!Uri.IsHexDigit(c))) { Add(errors,"invalid_hash",path+".sha256","Media requires a SHA-256 hash."); }
            if ((item.Kind=="image" || item.Kind=="video") && (item.Width<1 || item.Height<1 || item.Width>16384 || item.Height>16384)) { Add(errors,"invalid_media",path,"Media dimensions are invalid."); }
            if ((item.Kind=="video" || item.Kind=="audio") && (!ValidTime(item.Duration) || item.Duration<=MediaTime.Zero)) { Add(errors,"invalid_media",path+".duration","Timed media requires a positive duration."); }
        }
        var items=new HashSet<string>(StringComparer.Ordinal);
        for (int i=0;i<document.Layers.Count;i++) {
            VisualLayer layer=document.Layers[i]; string path=$"layers[{i}]";
            if (layer==null) { Add(errors,"invalid_item",path,"Layer cannot be null."); continue; }
            CheckIdentity(errors,items,layer.Id,path);
            if(layer.Kind=="media") {CheckMedia(errors,media,layer.MediaId,path);}
            else if(layer.Kind=="group") { }
            else if(layer.Kind=="text") {errors.AddRange(TextLayerValidator.Validate(layer,document.Duration,path));}
            else {Add(errors,"invalid_layer",path+".kind","Unsupported layer kind.");}
            if(layer.Mask!=null) {CheckMedia(errors,media,layer.Mask.MediaId,path+".mask");if(layer.Mask.Channel is not ("alpha" or "luma")) {Add(errors,"invalid_mask",path,"Mask channel is unsupported.");}}
            try {MediaColor.Parse(layer.PaddingColor);} catch(InvalidDataException) {Add(errors,"invalid_color",path,"Padding color is invalid.");}
            CheckInterval(errors,layer.Start,layer.End,document.Duration,path);
            if (ValidTime(layer.Start) && ValidTime(layer.End)) {errors.AddRange(AnimationValidator.Validate(layer,path));}
            if (layer.Effects==null) { Add(errors,"invalid_collection",path+".effects","Effects cannot be null."); continue; }
            errors.AddRange(EffectInputValidator.Validate(layer.Effects,capabilities,media,path+".effects"));
        }
        var layers=new HashSet<string>(items,StringComparer.Ordinal);
        for (int i=0;i<document.AudioClips.Count;i++) {
            AudioClip clip=document.AudioClips[i]; string path=$"audio_clips[{i}]";
            if (clip==null) { Add(errors,"invalid_item",path,"Audio clip cannot be null."); continue; }
            CheckIdentity(errors,items,clip.Id,path); CheckMedia(errors,media,clip.MediaId,path);
            CheckInterval(errors,clip.Start,clip.End,document.Duration,path);
            if (!double.IsFinite(clip.Gain) || clip.Gain<0 || clip.Gain>16) { Add(errors,"invalid_gain",path+".gain","Gain must be finite and in 0..16."); }
        }
        for (int i=0;i<document.Transitions.Count;i++) {
            CompositionTransition transition=document.Transitions[i]; string path=$"transitions[{i}]";
            if (transition==null) { Add(errors,"invalid_item",path,"Transition cannot be null."); continue; }
            CheckIdentity(errors,items,transition.Id,path);
            if (!layers.Contains(transition.FromLayer) || !layers.Contains(transition.ToLayer) || transition.FromLayer==transition.ToLayer) { Add(errors,"missing_layer",path,"Transition requires two distinct visual layers."); }
            if (!capabilities.Supports(transition.EffectId,transition.EffectVersion)) { Add(errors,"unknown_effect",path,"Transition effect version is unsupported."); }
            if (!ValidTime(transition.Duration) || transition.Duration<=MediaTime.Zero) { Add(errors,"invalid_interval",path,"Transition duration must be positive."); }
            else if (ValidTime(transition.Start)) { CheckInterval(errors,transition.Start,transition.Start+transition.Duration,document.Duration,path); }
            else { Add(errors,"invalid_interval",path,"Transition start must be valid."); }
        }
        errors.AddRange(VisualSourceValidator.Validate(document));
        errors.AddRange(AudioClipValidator.Validate(document));
        errors.AddRange(TransitionValidator.Validate(document,capabilities));
        return errors;
    }
    /// <summary>Rejects empty or duplicated identities in a single reference namespace.</summary>
    static void CheckIdentity(List<CompositionDiagnostic> errors,HashSet<string> ids,string id,string path) {
        if (string.IsNullOrWhiteSpace(id)) { Add(errors,"invalid_identity",path,"Item requires a stable identity."); }
        else if (!ids.Add(id)) { Add(errors,"duplicate_id",path,"Item identity is duplicated."); }
    }
    /// <summary>Checks that an item resolves an existing declared source.</summary>
    static void CheckMedia(List<CompositionDiagnostic> errors,HashSet<string> media,string id,string path) {
        if (id==null || !media.Contains(id)) { Add(errors,"missing_media",path+".media_id","Referenced media is absent."); }
    }
    /// <summary>Validates a finite rational interval against the declared timeline end.</summary>
    static void CheckInterval(List<CompositionDiagnostic> errors,MediaTime start,MediaTime end,MediaTime duration,string path) {
        if (!ValidTime(start) || !ValidTime(end) || !ValidTime(duration) || start<MediaTime.Zero || end<=start || end>duration) { Add(errors,"invalid_interval",path,"Interval must be positive and inside the timeline."); }
    }
    /// <summary>Checks the denominator without throwing so malformed inputs have diagnostics.</summary>
    static bool ValidTime(MediaTime time) => time.Denominator>0;
    /// <summary>Appends a structured error for downstream UI and CLI reporting.</summary>
    static void Add(List<CompositionDiagnostic> errors,string code,string path,string message) => errors.Add(new(){Code=code,Path=path,Message=message});
}
