namespace helengine.capture.linux.tests;

/// <summary>Produces independent known frames and explicit failures without a display server.</summary>
sealed class FakeLinuxWindowSource : ILinuxWindowSource {
    /// <summary>Number of arrays returned so later captures differ from earlier owned frames.</summary>
    internal int Reads;
    /// <summary>Records native resource closure.</summary>
    internal bool Disposed;
    /// <summary>Injects a read failure rather than substituting black or stale pixels.</summary>
    internal bool Fail;
    /// <summary>Single-pixel test geometry.</summary>
    public int Width=>1;
    /// <summary>Single-row test geometry.</summary>
    public int Height=>1;
    /// <summary>Returns a new allocation or propagates an explicit source failure.</summary>
    public byte[] ReadRgba() {
        if(Fail) { throw new InvalidOperationException("Window destroyed."); }
        Reads++;return [(byte)Reads,2,3,255];
    }
    /// <summary>Records disposal independently of previously returned allocations.</summary>
    public void Dispose()=>Disposed=true;
}
