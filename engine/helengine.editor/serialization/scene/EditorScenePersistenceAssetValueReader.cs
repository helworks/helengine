namespace helengine.editor {
    /// <summary>
    /// Decodes the asset-backed values that appear inside editor-authored reflected component payloads.
    /// Editor payloads carry raw scene asset references so the descriptor can heal them against the project's save-state before
    /// resolving them, therefore this reader decodes only the reference itself and leaves every asset-backed member type to the
    /// descriptor's member-level restore path.
    /// </summary>
    public sealed class EditorScenePersistenceAssetValueReader : IScenePersistenceAssetValueReader {
        /// <summary>
        /// Decodes one raw editor-authored scene asset reference and declines every other member type.
        /// </summary>
        /// <param name="reader">Reader positioned at the member payload.</param>
        /// <param name="valueType">Runtime value type expected for the payload.</param>
        /// <param name="value">Decoded scene asset reference when the member stores one.</param>
        /// <returns>True when the member type was consumed as one raw scene asset reference; otherwise false.</returns>
        public bool TryReadAssetValue(EngineBinaryReader reader, Type valueType, out object value) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }
            if (valueType == null) {
                throw new ArgumentNullException(nameof(valueType));
            }
            if (valueType == typeof(SceneAssetReference)) {
                value = SceneComponentBinaryFieldEncoding.ReadOptionalReference(reader);
                return true;
            }

            value = null;
            return false;
        }
    }
}
