namespace helengine.editor {
    /// <summary>
    /// Owner of the <see cref="GeneratedSceneImportStamp.SidecarDataKey"/> entry in import-settings sidecars. Payload version 1, after
    /// the version byte: the importer id, the source checksum, an int32 contributor count and the contributor ids in execution order.
    /// </summary>
    public sealed class GeneratedSceneImportStampEntryDefinition : ISidecarDataEntryDefinition {
        /// <summary>
        /// Payload version this owner writes and reads.
        /// </summary>
        public const byte Version = 1;

        /// <summary>
        /// Gets the key the stamp is stored under.
        /// </summary>
        public string Key => GeneratedSceneImportStamp.SidecarDataKey;

        /// <summary>
        /// Gets the stamp type.
        /// </summary>
        public Type ValueType => typeof(GeneratedSceneImportStamp);

        /// <summary>
        /// Gets the payload version this owner writes.
        /// </summary>
        public byte CurrentVersion => Version;

        /// <summary>
        /// Writes the importer id, the source checksum and the ordered contributor ids.
        /// </summary>
        /// <param name="writer">Writer positioned after the version byte.</param>
        /// <param name="value">Stamp to write.</param>
        public void Write(EngineBinaryWriter writer, object value) {
            GeneratedSceneImportStamp stamp = (GeneratedSceneImportStamp)value;
            if (string.IsNullOrWhiteSpace(stamp.ImporterId) || string.IsNullOrWhiteSpace(stamp.SourceChecksum) || stamp.ContributorIds == null) {
                throw new InvalidOperationException("A scene import stamp needs an importer id, a source checksum and the contributor ids.");
            }

            writer.WriteString(stamp.ImporterId);
            writer.WriteString(stamp.SourceChecksum);
            writer.WriteInt32(stamp.ContributorIds.Length);
            for (int index = 0; index < stamp.ContributorIds.Length; index++) {
                writer.WriteString(stamp.ContributorIds[index]);
            }
        }

        /// <summary>
        /// Reads a version 1 stamp and checks that every field is present.
        /// </summary>
        /// <param name="reader">Reader positioned after the version byte.</param>
        /// <param name="version">Version byte stored with the payload.</param>
        /// <returns>The decoded stamp.</returns>
        public object Read(EngineBinaryReader reader, byte version) {
            if (version != Version) {
                throw new InvalidDataException($"scene import stamp version {version} is not supported, this editor reads version {Version}");
            }

            GeneratedSceneImportStamp stamp = new GeneratedSceneImportStamp {
                ImporterId = reader.ReadString(),
                SourceChecksum = reader.ReadString()
            };
            int contributorCount = reader.ReadInt32();
            if (string.IsNullOrWhiteSpace(stamp.ImporterId) || string.IsNullOrWhiteSpace(stamp.SourceChecksum) || contributorCount < 0) {
                throw new InvalidDataException("the scene import stamp is missing required fields");
            }

            stamp.ContributorIds = new string[contributorCount];
            for (int index = 0; index < contributorCount; index++) {
                stamp.ContributorIds[index] = reader.ReadString();
            }

            return stamp;
        }
    }
}
