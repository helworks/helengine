namespace helengine.recording.windows.tests;
/// <summary>Models an exclusively created destination including a partial write failure.</summary>
sealed class RecordingTestWriteStream : MemoryStream {
    /// <summary>Persists this stream's bytes when its owner closes it.</summary>
    readonly Action<byte[]> Save;
    /// <summary>Whether the first copy write must fail after creating partial bytes.</summary>
    readonly bool FailWrite;
    /// <summary>Creates a test destination stream.</summary>
    internal RecordingTestWriteStream(Action<byte[]> save, bool failWrite) { Save = save; FailWrite = failWrite; }
    /// <summary>Copies data or reproduces a storage failure after a partial write.</summary>
    public override void Write(byte[] buffer, int offset, int count) {
        if (FailWrite) { base.Write(buffer, offset, Math.Min(count, 2)); throw new IOException("copy failure"); }
        base.Write(buffer, offset, count);
    }
    /// <summary>Closes the exclusively owned destination, keeping its current bytes.</summary>
    protected override void Dispose(bool disposing) { if (disposing) { Save(ToArray()); } base.Dispose(disposing); }
}
