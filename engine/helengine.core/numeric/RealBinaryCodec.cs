namespace helengine {
    /// <summary>Encodes explicit build numeric contracts independently of the host scalar representation.</summary>
    public static class RealBinaryCodec {
        /// <summary>Writes a fixed double transport value at the selected storage width and stream endianness.</summary>
        /// <param name="writer">Destination selected for the payload byte order.</param>
        /// <param name="value">Authoring or evaluated value to round at this stored destination.</param>
        /// <param name="policy">Numeric contract recorded for this payload.</param>
        public static void Write(EngineBinaryWriter writer, double value, NumericPrecisionPolicy policy) {
            ValidatePolicy(policy);
            if (writer == null) { throw new ArgumentNullException(nameof(writer)); }
            if (policy.StorageBits == 32) { writer.WriteSingle((float)value); }
            else { writer.WriteDouble(value); }
        }

        /// <summary>Reads the declared storage width without narrowing it to the managed host representation.</summary>
        /// <param name="reader">Source selected for the payload byte order.</param>
        /// <param name="policy">Validated numeric contract associated with this payload.</param>
        /// <returns>Decoded numeric value in fixed double transport.</returns>
        public static double Read(EngineBinaryReader reader, NumericPrecisionPolicy policy) {
            ValidatePolicy(policy);
            if (reader == null) { throw new ArgumentNullException(nameof(reader)); }
            return policy.StorageBits == 32 ? reader.ReadSingle() : reader.ReadDouble();
        }

        /// <summary>Writes the version and both widths before any precision-dependent payload bytes.</summary>
        /// <param name="writer">Destination for the numeric contract.</param>
        /// <param name="policy">Implemented numeric policy required by the following payload.</param>
        public static void WritePolicy(EngineBinaryWriter writer, NumericPrecisionPolicy policy) {
            ValidatePolicy(policy);
            if (writer == null) { throw new ArgumentNullException(nameof(writer)); }
            writer.WriteByte((byte)policy.RepresentationVersion);
            writer.WriteByte((byte)policy.StorageBits);
            writer.WriteByte((byte)policy.ArithmeticBits);
        }

        /// <summary>Reads an explicitly declared numeric contract for authoring or tooling without assuming host precision.</summary>
        /// <param name="reader">Source positioned at the three numeric contract bytes.</param>
        /// <returns>An owned immutable contract that the caller can retain for this payload.</returns>
        [NativeOwnedReturn]
        public static NumericPrecisionPolicy ReadPolicy(EngineBinaryReader reader) {
            if (reader == null) { throw new ArgumentNullException(nameof(reader)); }
            int version = reader.ReadByte();
            int storage = reader.ReadByte();
            int arithmetic = reader.ReadByte();
            if (version != NumericPrecisionPolicy.CurrentRepresentationVersion
                || storage != 16 && storage != 32 && storage != 64
                || arithmetic != 16 && arithmetic != 32 && arithmetic != 64) {
                throw new InvalidOperationException($"Numeric payload policy 'real-v{version}-s{storage}-a{arithmetic}' is unsupported.");
            }
            NumericPrecisionPolicy policy = new NumericPrecisionPolicy(storage, arithmetic, version);
            ValidatePolicy(policy);
            return policy;
        }

        /// <summary>Rejects unsupported or mismatched numeric contracts before callers can read their payload.</summary>
        /// <param name="reader">Source positioned at the numeric contract.</param>
        /// <param name="expected">Numeric policy required by the consumer.</param>
        /// <returns>The validated serialized numeric policy.</returns>
        public static NumericPrecisionPolicy ReadAndValidatePolicy(EngineBinaryReader reader, NumericPrecisionPolicy expected) {
            ValidatePolicy(expected);
            if (reader == null) { throw new ArgumentNullException(nameof(reader)); }
            int version = reader.ReadByte();
            int storage = reader.ReadByte();
            int arithmetic = reader.ReadByte();
            if (version != expected.RepresentationVersion || storage != expected.StorageBits || arithmetic != expected.ArithmeticBits) {
                throw new InvalidOperationException($"Numeric payload policy 'real-v{version}-s{storage}-a{arithmetic}' does not match required policy '{expected.Identity}'. Regenerate the payload.");
            }
            return expected;
        }

        /// <summary>Prevents a declared but unimplemented width from silently taking a different codec path.</summary>
        /// <param name="policy">Contract whose storage and arithmetic have to be implemented.</param>
        public static void ValidatePolicy(NumericPrecisionPolicy policy) {
            if (policy == null) { throw new ArgumentNullException(nameof(policy)); }
            else if (policy.StorageBits == 16 || policy.ArithmeticBits == 16) {
                throw new NotSupportedException($"Numeric codec '{policy.Identity}' has no implemented half-precision backend.");
            }
        }
    }
}
