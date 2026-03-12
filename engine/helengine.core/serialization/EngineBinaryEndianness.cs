namespace helengine {
    /// <summary>
    /// Identifies the byte ordering used for a binary payload.
    /// </summary>
    public enum EngineBinaryEndianness : byte {
        /// <summary>
        /// Multibyte values are written least-significant byte first.
        /// </summary>
        LittleEndian = 1,

        /// <summary>
        /// Multibyte values are written most-significant byte first.
        /// </summary>
        BigEndian = 2
    }
}
