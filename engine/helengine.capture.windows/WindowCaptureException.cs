namespace helengine.capture.windows;
/// <summary>Preserves the originating native operation, diagnostic and HRESULT.</summary>
public sealed class WindowCaptureException : ExternalException {
    /// <summary>Creates a managed error from a complete operation-owned native diagnostic.</summary>
    internal WindowCaptureException(string operation, string message, int hresult) : base($"{operation}: {message}", hresult) { Operation = operation; }
    /// <summary>The native API operation reporting this failure.</summary>
    public string Operation { get; }
}
