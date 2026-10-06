namespace helengine.vfx.captions;

/// <summary>A caption segment with optional genuine word alignment.</summary>
public sealed class CaptionCue {
    /// <summary>Validates a segment and preserves its optional ordered words.</summary>
    public CaptionCue(string text, double start, double end, IEnumerable<CaptionWord> words = null) {
        if (string.IsNullOrWhiteSpace(text) || !double.IsFinite(start) || !double.IsFinite(end) || start < 0 || end <= start) {
            throw new FormatException("Each caption needs text and finite times satisfying 0 <= start < end.");
        }
        Text = text.Trim();
        Start = start;
        End = end;
        CaptionWord[] ordered = (words ?? Array.Empty<CaptionWord>()).ToArray();
        for (int index = 0; index < ordered.Length; index++) {
            CaptionWord word = ordered[index] ?? throw new FormatException("Caption words cannot be null.");
            if (word.Start < start - 0.001 || word.End > end + 0.001 || (index > 0 && word.Start < ordered[index - 1].Start)) {
                throw new FormatException("Word times must be ordered and contained in their caption segment.");
            }
        }
        Words = Array.AsReadOnly(ordered);
    }

    /// <summary>Gets the segment text, including original line breaks.</summary>
    public string Text { get; }
    /// <summary>Gets the inclusive segment start.</summary>
    public double Start { get; }
    /// <summary>Gets the exclusive segment end.</summary>
    public double End { get; }
    /// <summary>Gets original word timestamps; an empty list means phrase timing only.</summary>
    public IReadOnlyList<CaptionWord> Words { get; }
}
