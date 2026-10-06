namespace helengine.vfx.captions;

/// <summary>A spoken word with the original transcription times, expressed in seconds.</summary>
public sealed class CaptionWord {
    /// <summary>Creates a word without estimating or changing its alignment.</summary>
    public CaptionWord(string text, double start, double end) {
        if (string.IsNullOrWhiteSpace(text) || !double.IsFinite(start) || !double.IsFinite(end) || start < 0 || end < start) {
            throw new FormatException("Each word needs text and finite times satisfying 0 <= start <= end; zero-length punctuation stays unhighlighted.");
        }
        Text = text.Trim();
        Start = start;
        End = end;
    }

    /// <summary>Gets the word and its punctuation.</summary>
    public string Text { get; }
    /// <summary>Gets the inclusive start time.</summary>
    public double Start { get; }
    /// <summary>Gets the exclusive end time.</summary>
    public double End { get; }
}
