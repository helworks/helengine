namespace helengine.media;
/// <summary>Validates timed text without loading fonts or allocating render resources.</summary>
public static class TextLayerValidator {
    /// <summary>Collects invalid cues or genuine word alignment errors.</summary>
    public static IReadOnlyList<CompositionDiagnostic> Validate(VisualLayer layer,MediaTime duration,string path) {
        var errors=new List<CompositionDiagnostic>();
        if(layer.Text==null || layer.Text.Cues==null || layer.Text.Cues.Count==0 || layer.Text.Cues.Count>8192) {errors.Add(new(){Code="invalid_text",Path=path,Message="Text layer needs a bounded list of cues."});return errors;}
        if(layer.Text.Style.ValueKind!=JsonValueKind.Object) {errors.Add(new(){Code="invalid_text_style",Path=path,Message="Text requires a data-only caption style."});return errors;}
        foreach(var property in layer.Text.Style.EnumerateObject().Where(property=>property.Name.Equals("FontFile",StringComparison.OrdinalIgnoreCase))) {if(property.Value.ValueKind!=JsonValueKind.String || property.Value.GetString() is string font && font.Length>0 && (Path.IsPathRooted(font) || font.Contains(':') || font.Replace('\\','/').Split('/').Contains(".."))) {errors.Add(new(){Code="invalid_font_path",Path=path,Message="Caption fonts require relative owned assets paths."});}}
        foreach(var cue in layer.Text.Cues) {
            if(cue==null || string.IsNullOrWhiteSpace(cue.Text) || cue.Text.Length>65536 || cue.Start.Denominator<=0 || cue.End.Denominator<=0 || duration.Denominator<=0 || cue.Start<MediaTime.Zero || cue.End<=cue.Start || cue.End>duration || cue.Words==null) {errors.Add(new(){Code="invalid_text",Path=path,Message="Caption cue needs text and a valid absolute interval."});continue;}
            MediaTime previous=cue.Start;
            foreach(var word in cue.Words) {
                if(word==null || string.IsNullOrWhiteSpace(word.Text) || word.Start.Denominator<=0 || word.End.Denominator<=0 || word.Start<previous || word.Start<cue.Start || word.End<word.Start || word.End>cue.End) {errors.Add(new(){Code="invalid_text",Path=path,Message="Word alignment is invalid."});break;}
                previous=word.Start;
            }
        }
        return errors;
    }
}
