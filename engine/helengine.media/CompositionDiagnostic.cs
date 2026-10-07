namespace helengine.media;
/// <summary>Reports a validation failure with an actionable code and location.</summary>
public sealed class CompositionDiagnostic {
    /// <summary>Stable error code for UI and CLI consumers.</summary>
    public string Code { get; set; } = "";
    /// <summary>JSON path identifying the invalid property.</summary>
    public string Path { get; set; } = "";
    /// <summary>Human-readable explanation of the failure.</summary>
    public string Message { get; set; } = "";
}
