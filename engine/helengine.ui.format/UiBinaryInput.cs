using System;
using System.IO;
using System.Text;

namespace helengine.ui.format {
    /// <summary>Length-checked little-endian primitive reads; platform stream EOF behavior never decides admission.</summary>
    sealed class UiBinaryInput : IDisposable {
        /// <summary>Owned wrapper, retired before the stream it borrows.</summary>
        [NativeOwnedMember] BinaryReader Reader;
        /// <summary>Owns the stream storage independently of its borrowing reader.</summary>
        [NativeOwnedMember] MemoryStream Source;
        /// <summary>Exact caller-provided input size, independent of runtime stream capacity.</summary>
        readonly int Length;
        /// <summary>Number of bytes consumed by validated primitive reads.</summary>
        int Position;
        /// <summary>Creates an isolated read cursor over the caller's byte array.</summary>
        public UiBinaryInput([NativeNoEscape] byte[] bytes) {
            Source = new MemoryStream(); bool initialized = false;
            try { Source.Write(bytes, 0, bytes.Length); Source.Position = 0; Reader = new BinaryReader(Source); Length = bytes.Length; initialized = true; }
            finally { if (!initialized) { NativeOwnership.DisposeAndRelease(ref Source); } }
        }
        /// <summary>Returns unread bytes within the original input.</summary>
        public int Remaining { get { return Length - Position; } }
        /// <summary>Checks remaining bytes before invoking any platform read.</summary>
        void Take(int count) { if (count < 0 || count > Remaining) { throw new FormatException("Truncated UI data."); } Position += count; }
        /// <summary>Reads a wire byte.</summary>
        public byte Byte() { Take(1); return Reader.ReadByte(); }
        /// <summary>Reads a wire uint16.</summary>
        public ushort UInt16() { Take(2); return Reader.ReadUInt16(); }
        /// <summary>Reads a wire uint32.</summary>
        public uint UInt32() { Take(4); return Reader.ReadUInt32(); }
        /// <summary>Reads a wire int32.</summary>
        public int Int32() { Take(4); return Reader.ReadInt32(); }
        /// <summary>Reads a finite wire float32.</summary>
        public float Single() { Take(4); float value = Reader.ReadSingle(); UiValidation.Finite(value); return value; }
        /// <summary>Accepts only canonical boolean bytes.</summary>
        public bool Boolean() { byte value = Byte(); if (value > 1) { throw new FormatException("Invalid UI boolean."); } return value == 1; }
        /// <summary>Reads a bounded string only after checking its encoded length and Unicode validity.</summary>
        public string Text() {
            uint count = UInt32(); if (count > UiFormatLimits.StringBytes || count > Remaining) { throw new FormatException("Invalid UI string byte length."); }
            Take((int)count); byte[] bytes = Reader.ReadBytes((int)count);
            try { UiUtf8.Validate(bytes); return Encoding.UTF8.GetString(bytes); } finally { NativeOwnership.Release(ref bytes); }
        }
        /// <summary>Checks a collection count and the minimum possible record bytes before allocation.</summary>
        public int Count(uint count, int maximum, int minimumBytes) {
            if (count > maximum || count > Remaining / minimumBytes) { throw new FormatException("Invalid UI collection length."); } return (int)count;
        }
        /// <summary>Releases reader and its owned input stream.</summary>
        public void Dispose() { NativeOwnership.DisposeAndRelease(ref Reader); NativeOwnership.DisposeAndRelease(ref Source); }
    }
}
