namespace helengine.vfx.captions;

/// <summary>A display token with optional timing and an explicit line-break marker.</summary>
sealed class CaptionRenderWord {
    /// <summary>Creates a token using original times, or a phrase-only token when end is zero.</summary>
    public CaptionRenderWord(string text, double start = 0, double end = 0) {
        Text = text;
        Start = start;
        End = end;
    }

    /// <summary>Gets display text; a newline requests an explicit wrap.</summary>
    public string Text { get; }
    /// <summary>Gets the original start or zero for phrase-only text.</summary>
    public double Start { get; }
    /// <summary>Gets the original end or zero for phrase-only text.</summary>
    public double End { get; }
}
