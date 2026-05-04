namespace helengine;

/// <summary>
/// Captures text input characters reported during a frame.
/// </summary>
public struct InputTextState {
    /// <summary>
    /// Gets or sets the buffered text characters for the frame.
    /// </summary>
    public char[] Characters { get; set; }

    /// <summary>
    /// Gets or sets the number of valid characters in <see cref="Characters"/>.
    /// </summary>
    public int CharacterCount { get; set; }
}
