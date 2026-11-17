namespace helengine.ui.tests {
    /// <summary>Non-seekable input returning three bytes per read, with observable ownership.</summary>
    public sealed class PartialReadStream : Stream {
        /// <summary>Read-only fixture storage.</summary>
        readonly byte[] Bytes;
        /// <summary>Next unread byte offset.</summary>
        int Offset;
        /// <summary>Tracks whether the processor improperly closes its caller's stream.</summary>
        public bool WasDisposed;
        /// <summary>Uses an already bounded fixture array.</summary>
        public PartialReadStream(byte[] bytes) { Bytes = bytes; }
        /// <summary>Reports readable until explicitly disposed.</summary>
        public override bool CanRead => !WasDisposed;
        /// <summary>Disallows seeking so the processor cannot rely on Length.</summary>
        public override bool CanSeek => false;
        /// <summary>Disallows mutation of fixture bytes.</summary>
        public override bool CanWrite => false;
        /// <summary>No length is available on this input.</summary>
        public override long Length => throw new NotSupportedException();
        /// <summary>No position API is available on this input.</summary>
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        /// <summary>Returns up to three bytes until EOF.</summary>
        public override int Read(byte[] buffer, int offset, int count) { int take = Math.Min(3, Math.Min(count, Bytes.Length - Offset)); Array.Copy(Bytes, Offset, buffer, offset, take); Offset += take; return take; }
        /// <summary>Read-only input has nothing to flush.</summary>
        public override void Flush() { }
        /// <summary>Seeking is deliberately unavailable.</summary>
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        /// <summary>Resizing is deliberately unavailable.</summary>
        public override void SetLength(long value) { throw new NotSupportedException(); }
        /// <summary>Writing is deliberately unavailable.</summary>
        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
        /// <summary>Records caller-owned disposal.</summary>
        protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); }
    }
}
