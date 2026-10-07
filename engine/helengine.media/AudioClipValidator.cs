namespace helengine.media;
/// <summary>Preflights every audio source interval and envelope before decoder, GPU or encoder startup.</summary>
public static class AudioClipValidator {
    /// <summary>Reports invalid source kinds, unresolved handles and envelope ranges.</summary>
    public static IReadOnlyList<CompositionDiagnostic> Validate(CompositionDocument document) {
        var errors=new List<CompositionDiagnostic>();foreach(var clip in document.AudioClips.Where(item=>item!=null)) {
            var source=document.Media.FirstOrDefault(media=>media?.Id==clip.MediaId);if(source!=null && source.Kind is not ("audio" or "video")) {Add(errors,clip.Id,"Audio requires an audio or video source.");}
            if(clip.Start.Denominator<1 || clip.End.Denominator<1 || clip.SourceIn.Denominator<1 || clip.SourceOut.Denominator<1) {Add(errors,clip.Id,"Invalid rational source interval.");continue;}
            var duration=clip.End-clip.Start;if(clip.SourceIn<MediaTime.Zero || clip.SourceOut<=clip.SourceIn || clip.SourceOut-clip.SourceIn<duration || source!=null && source.Duration.Denominator>0 && clip.SourceOut>source.Duration) {Add(errors,clip.Id,"Source audio must cover the complete timeline interval.");}
            if(clip.Envelopes==null || clip.Envelopes.Any(envelope=>envelope==null || !envelope.IsValid(duration))) {Add(errors,clip.Id,"Invalid audio gain envelope.");}
        }return errors;
    }
    /// <summary>Appends a deterministic preflight diagnostic.</summary>
    static void Add(List<CompositionDiagnostic> errors,string id,string message)=>errors.Add(new(){Code="invalid_audio_clip",Path=id,Message=message});
}
