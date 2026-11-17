using System;
using System.IO;

namespace helengine.ui.format {
    /// <summary>Owns a serialization stream and the writer that borrows it, disposing them in dependency order.</summary>
    sealed class UiBinaryOutput : IDisposable {
        /// <summary>Owned fixed-length byte storage, kept alive until the writer has been disposed.</summary>
        [NativeOwnedMember] MemoryStream Source;
        /// <summary>Owned primitive writer over Source.</summary>
        [NativeOwnedMember] BinaryWriter WriterValue;
        /// <summary>Allocates the measured wire length and cleans partial construction on failure.</summary>
        public UiBinaryOutput(int length) {
            try { Source = new MemoryStream(); Source.SetLength(length); WriterValue = new BinaryWriter(Source); }
            catch { Dispose(); throw; }
        }
        /// <summary>Borrows the writer only while this output remains alive.</summary>
        [NativeBorrowedReturn] public BinaryWriter Writer { get { return WriterValue; } }
        /// <summary>Returns the number of bytes emitted to the measured stream.</summary>
        public long Position { get { return Source.Position; } }
        /// <summary>Flushes the writer and returns an independent caller-owned copy of the completed bytes.</summary>
        [NativeOwnedReturn] public byte[] ToArray() { WriterValue.Flush(); return Source.ToArray(); }
        /// <summary>Disposes the borrowing writer before releasing its stream.</summary>
        public void Dispose() { NativeOwnership.DisposeAndRelease(ref WriterValue); NativeOwnership.DisposeAndRelease(ref Source); }
    }
}
