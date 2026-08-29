namespace helengine.editor {
    /// <summary>
    /// Serialized material common and platform documents staged by an authoring transaction.
    /// </summary>
    internal sealed class EditorGeneratedMaterialSettingsPayload {
        public byte[] CommonBytes { get; init; }

        public IReadOnlyDictionary<string, byte[]> OverrideBytesBySuffix { get; init; }
    }
}
