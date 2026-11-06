namespace helengine.editor {
    /// <summary>
    /// Owner of one key in the generic data block of import-settings sidecars: reads and writes the typed value stored under the key.
    /// Every payload starts with a version byte that the owner controls; <see cref="SidecarDataEntryRegistry"/> writes
    /// <see cref="CurrentVersion"/> before the owner's data and hands the stored version to <see cref="Read"/>.
    /// </summary>
    public interface ISidecarDataEntryDefinition {
        /// <summary>
        /// Gets the key the owner's entry is stored under. Prefix it with the owner's namespace (for example <c>helengine.scene.import</c>).
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Gets the type of the value the owner reads and writes.
        /// </summary>
        Type ValueType { get; }

        /// <summary>
        /// Gets the payload version the owner writes. The owner bumps it when its payload layout changes.
        /// </summary>
        byte CurrentVersion { get; }

        /// <summary>
        /// Writes the owner's data, which follows the version byte.
        /// </summary>
        /// <param name="writer">Little-endian writer positioned after the version byte.</param>
        /// <param name="value">Value of type <see cref="ValueType"/>.</param>
        void Write(EngineBinaryWriter writer, object value);

        /// <summary>
        /// Reads the owner's data. Throws <see cref="InvalidDataException"/> for a version or content the owner cannot read.
        /// </summary>
        /// <param name="reader">Little-endian reader positioned after the version byte.</param>
        /// <param name="version">Version byte stored with the payload.</param>
        /// <returns>Value of type <see cref="ValueType"/>.</returns>
        object Read(EngineBinaryReader reader, byte version);
    }
}
