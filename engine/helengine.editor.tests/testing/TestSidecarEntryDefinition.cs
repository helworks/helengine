namespace helengine.editor.tests.testing {
    /// <summary>
    /// Owner of the test sidecar data entry: one string after the version byte. Only version <see cref="Version"/> is readable.
    /// </summary>
    internal sealed class TestSidecarEntryDefinition : ISidecarDataEntryDefinition {
        /// <summary>
        /// Key the test entry is stored under.
        /// </summary>
        public const string KeyValue = "helengine.tests.entry";

        /// <summary>
        /// Payload version the test owner writes and reads.
        /// </summary>
        public const byte Version = 3;

        /// <summary>
        /// Gets the key the test entry is stored under.
        /// </summary>
        public string Key => KeyValue;

        /// <summary>
        /// Gets the typed value of the entry.
        /// </summary>
        public Type ValueType => typeof(TestSidecarEntry);

        /// <summary>
        /// Gets the payload version the owner writes.
        /// </summary>
        public byte CurrentVersion => Version;

        /// <summary>
        /// Writes the entry text.
        /// </summary>
        /// <param name="writer">Writer positioned after the version byte.</param>
        /// <param name="value">Entry to write.</param>
        public void Write(EngineBinaryWriter writer, object value) {
            writer.WriteString(((TestSidecarEntry)value).Text);
        }

        /// <summary>
        /// Reads the entry text and rejects every version except <see cref="Version"/>.
        /// </summary>
        /// <param name="reader">Reader positioned after the version byte.</param>
        /// <param name="version">Version byte the payload carries.</param>
        /// <returns>The decoded entry.</returns>
        public object Read(EngineBinaryReader reader, byte version) {
            if (version != Version) {
                throw new InvalidDataException($"Unsupported test entry version {version}.");
            }

            return new TestSidecarEntry { Text = reader.ReadString() };
        }
    }
}
