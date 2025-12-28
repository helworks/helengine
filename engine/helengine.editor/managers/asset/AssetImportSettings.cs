namespace helengine.editor {
    /// <summary>
    /// Stores import configuration for a single source asset file.
    /// </summary>
    [ProtoBuf.ProtoContract]
    public class AssetImportSettings {
        /// <summary>
        /// Gets or sets the importer identifier used for this asset.
        /// </summary>
        [ProtoBuf.ProtoMember(1)]
        public string ImporterId { get; set; }

        /// <summary>
        /// Gets or sets the checksum used to track the source asset content.
        /// </summary>
        [ProtoBuf.ProtoMember(2)]
        public string SourceChecksum { get; set; }

        /// <summary>
        /// Gets or sets the asset identifier associated with the imported output.
        /// </summary>
        [ProtoBuf.ProtoMember(3)]
        public string AssetId { get; set; }
    }
}
