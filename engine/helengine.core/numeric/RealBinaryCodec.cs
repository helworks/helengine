namespace helengine {
    /// <summary>Encodes explicit build numeric contracts independently of the host scalar representation.</summary>
    public static class RealBinaryCodec {
        /// <summary>Writes a fixed double transport value at the selected storage width and stream endianness.</summary>
        /// <param name="writer">Destination selected for the payload byte order.</param>
        /// <param name="value">Authoring or evaluated value to round at this stored destination.</param>
        /// <param name="policy">Numeric contract recorded for this payload.</param>
        public static void Write(EngineBinaryWriter writer, double value, NumericPrecisionPolicy policy) {
            RequireImplemented(policy);
            if (writer == null) { throw new ArgumentNullException(nameof(writer)); }
            if (policy.StorageBits == 32) { writer.WriteSingle((float)value); }
            else { writer.WriteDouble(value); }
        }

        /// <summary>Reads the declared storage width without narrowing it to the managed host representation.</summary>
        /// <param name="reader">Source selected for the payload byte order.</param>
        /// <param name="policy">Validated numeric contract associated with this payload.</param>
        /// <returns>Decoded numeric value in fixed double transport.</returns>
        public static double Read(EngineBinaryReader reader, NumericPrecisionPolicy policy) {
            RequireImplemented(policy);
            if (reader == null) { throw new ArgumentNullException(nameof(reader)); }
            return policy.StorageBits == 32 ? reader.ReadSingle() : reader.ReadDouble();
        }

        /// <summary>Writes the version and both widths before any precision-dependent payload bytes.</summary>
        /// <param name="writer">Destination for the numeric contract.</param>
        /// <param name="policy">Implemented numeric policy required by the following payload.</param>
        public static void WritePolicy(EngineBinaryWriter writer, NumericPrecisionPolicy policy) {
            RequireImplemented(policy);
            if (writer == null) { throw new ArgumentNullException(nameof(writer)); }
            writer.WriteByte((byte)policy.RepresentationVersion);
            writer.WriteByte((byte)policy.StorageBits);
            writer.WriteByte((byte)policy.ArithmeticBits);
        }

        /// <summary>Rejects unsupported or mismatched numeric contracts before callers can read their payload.</summary>
        /// <param name="reader">Source positioned at the numeric contract.</param>
        /// <param name="expected">Numeric policy required by the consumer.</param>
        /// <returns>The validated serialized numeric policy.</returns>
        public static NumericPrecisionPolicy ReadAndValidatePolicy(EngineBinaryReader reader, NumericPrecisionPolicy expected) {
            RequireImplemented(expected);
            if (reader == null) { throw new ArgumentNullException(nameof(reader)); }
            int version = reader.ReadByte();
            int storage = reader.ReadByte();
            int arithmetic = reader.ReadByte();
            if (version != expected.RepresentationVersion || storage != expected.StorageBits || arithmetic != expected.ArithmeticBits) {
                throw new InvalidOperationException($"Numeric payload policy 'real-v{version}-s{storage}-a{arithmetic}' does not match required policy '{expected.Identity}'. Regenerate the payload.");
            }
            return new NumericPrecisionPolicy(storage, arithmetic, version);
        }

        /// <summary>Prevents a declared but unimplemented width from silently taking a different codec path.</summary>
        /// <param name="policy">Contract whose storage and arithmetic have to be implemented.</param>
        static void RequireImplemented(NumericPrecisionPolicy policy) {
            if (policy == null) { throw new ArgumentNullException(nameof(policy)); }
            else if (policy.StorageBits == 16 || policy.ArithmeticBits == 16) {
                throw new NotSupportedException($"Numeric codec '{policy.Identity}' has no implemented half-precision backend.");
            }
        }
    }
}
