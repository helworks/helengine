namespace helengine.capture.linux;

/// <summary>Owns native capture resources for one explicit window and validates identity on readback.</summary>
interface ILinuxWindowSource : IDisposable {
    /// <summary>Fixed initial client-area width.</summary>
    int Width {get;}
    /// <summary>Fixed initial client-area height.</summary>
    int Height {get;}
    /// <summary>Returns a new opaque RGBA allocation after validating PID, visibility and geometry.</summary>
    byte[] ReadRgba();
}
