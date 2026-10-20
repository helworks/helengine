namespace helengine.recording.windows;
/// <summary>Owns the single D3D11 device shared by capture and encoding for a recording.</summary>
interface IRecordingDevice : IDisposable {
    /// <summary>Borrowed device pointer valid until disposal.</summary>
    IntPtr Handle { get; }
    /// <summary>Actual DXGI adapter description.</summary>
    string Name { get; }
    /// <summary>Actual DXGI adapter locally unique identifier.</summary>
    long Luid { get; }
}
