namespace helengine.editor {
    /// <summary>Reads authored asset-reference fields for dependency selection without importing their assets or instantiating components.</summary>
    sealed class SceneComponentReferenceCollectionReader : IScenePersistenceAssetValueReader {
        /// <summary>Receives each reference decoded by the standard reflected value walk.</summary>
        readonly Action<SceneAssetReference> ReferenceSink;

        /// <summary>Creates a reference-only reader for one component-selection collector.</summary>
        /// <param name="referenceSink">Callback recording authored references.</param>
        public SceneComponentReferenceCollectionReader(Action<SceneAssetReference> referenceSink) {
            ReferenceSink = referenceSink ?? throw new ArgumentNullException(nameof(referenceSink));
        }

        /// <summary>Consumes the standard authored reference framing for direct and asset-backed values.</summary>
        /// <param name="reader">Reader positioned at the next reflected value.</param>
        /// <param name="valueType">Persisted member or array element type.</param>
        /// <param name="assetValue">Null because selection never materializes assets.</param>
        /// <returns>True when an asset-backed value was consumed.</returns>
        public bool TryReadAssetValue(EngineBinaryReader reader, Type valueType, out object assetValue) {
            assetValue = null;
            if (valueType != typeof(SceneAssetReference) && !AutomaticComponentAssetReferenceSupport.IsSupportedAssetReferenceType(valueType)) return false;
            SceneAssetReference reference = SceneComponentBinaryFieldEncoding.ReadOptionalReference(reader);
            ReferenceSink(reference);
            return true;
        }
    }
}
