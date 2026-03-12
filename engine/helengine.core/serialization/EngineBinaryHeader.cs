namespace helengine {
    /// <summary>
    /// Describes the standardized header written at the start of engine binary files.
    /// </summary>
    public class EngineBinaryHeader {
        /// <summary>
        /// Initializes a new binary header descriptor.
        /// </summary>
        /// <param name="endianness">Payload endianness.</param>
        /// <param name="version">Serializer version for the selected format.</param>
        /// <param name="formatId">Serializer format identifier.</param>
        /// <param name="recordKind">Logical record kind stored in the payload.</param>
        /// <param name="valueKind">Concrete value type stored in the payload.</param>
        public EngineBinaryHeader(
            EngineBinaryEndianness endianness,
            byte version,
            ushort formatId,
            ushort recordKind,
            ushort valueKind) {
            Endianness = endianness;
            Version = version;
            FormatId = formatId;
            RecordKind = recordKind;
            ValueKind = valueKind;
        }

        /// <summary>
        /// Gets the payload endianness.
        /// </summary>
        public EngineBinaryEndianness Endianness { get; }

        /// <summary>
        /// Gets the serializer version for the selected format.
        /// </summary>
        public byte Version { get; }

        /// <summary>
        /// Gets the serializer format identifier.
        /// </summary>
        public ushort FormatId { get; }

        /// <summary>
        /// Gets the logical record kind stored in the payload.
        /// </summary>
        public ushort RecordKind { get; }

        /// <summary>
        /// Gets the concrete value type stored in the payload.
        /// </summary>
        public ushort ValueKind { get; }
    }
}
