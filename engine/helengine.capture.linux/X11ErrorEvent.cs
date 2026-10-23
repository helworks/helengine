namespace helengine.capture.linux;

/// <summary>Matches XErrorEvent so asynchronous failures can become managed capture errors.</summary>
[StructLayout(LayoutKind.Sequential)]
struct X11ErrorEvent {
    /// <summary>X11 event type.</summary>
    internal int Type;
    /// <summary>Display connection that received the failure.</summary>
    internal nint Display;
    /// <summary>Failed resource identifier and request serial.</summary>
    internal nuint ResourceId,Serial;
    /// <summary>Protocol error, major request opcode and extension minor opcode.</summary>
    internal byte ErrorCode,RequestCode,MinorCode;
}
