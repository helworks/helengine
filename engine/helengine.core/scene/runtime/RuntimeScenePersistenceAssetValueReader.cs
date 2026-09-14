#if !HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION
namespace helengine {
    /// <summary>
    /// Restores the asset-backed members of packaged scripted-component payloads by decoding the encoded scene asset references
    /// and resolving them through the runtime scene asset resolver that owns the packaged assets.
    /// </summary>
    public sealed class RuntimeScenePersistenceAssetValueReader : IScenePersistenceAssetValueReader {
        /// <summary>
        /// Runtime resolver used to rebuild packaged assets, or null when the payload is expected to carry no resolvable references.
        /// </summary>
        readonly RuntimeSceneAssetReferenceResolver ReferenceResolver;

        /// <summary>
        /// Initializes one runtime asset-value reader bound to the resolver that owns the packaged scene assets.
        /// </summary>
        /// <param name="referenceResolver">Runtime resolver used to rebuild packaged assets; null when the payload carries no resolvable references.</param>
        public RuntimeScenePersistenceAssetValueReader(RuntimeSceneAssetReferenceResolver referenceResolver) {
            ReferenceResolver = referenceResolver;
        }

        /// <summary>
        /// Decodes raw scene asset references, asset-backed members and asset-backed arrays into the runtime assets they name.
        /// </summary>
        /// <param name="reader">Reader positioned at the member payload.</param>
        /// <param name="valueType">Runtime value type expected for the payload.</param>
        /// <param name="value">Decoded asset-backed value when the member type is handled by this reader.</param>
        /// <returns>True when the member type was consumed as one asset-backed value; otherwise false.</returns>
        public bool TryReadAssetValue(EngineBinaryReader reader, Type valueType, out object value) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }
            if (valueType == null) {
                throw new ArgumentNullException(nameof(valueType));
            }

            if (valueType == typeof(SceneAssetReference)) {
                value = SceneAssetReferenceFactory.ReadOptionalReference(reader);
                return true;
            }
            if (AutomaticComponentAssetReferenceSupport.IsSupportedAssetReferenceType(valueType)) {
                SceneAssetReference reference = SceneAssetReferenceFactory.ReadOptionalReference(reader);
                value = AutomaticComponentAssetReferenceSupport.ResolveRuntimeAssetReference(valueType, reference, ReferenceResolver);
                return true;
            }
            if (AutomaticComponentAssetReferenceSupport.IsSupportedAssetReferenceArrayType(valueType)) {
                value = ReadAssetReferenceArrayValue(reader, valueType);
                return true;
            }

            value = null;
            return false;
        }

        /// <summary>
        /// Reads one supported packaged asset-reference array and resolves each element back into the runtime assets required by
        /// the array element type.
        /// </summary>
        /// <param name="reader">Reader positioned at the encoded reference-array payload.</param>
        /// <param name="valueType">Runtime array type expected for the payload.</param>
        /// <returns>Resolved runtime asset array or null when the payload omitted the reference array.</returns>
        object ReadAssetReferenceArrayValue(EngineBinaryReader reader, Type valueType) {
            int length = reader.ReadInt32();
            if (length == -1) {
                return null;
            }
            if (length < -1) {
                throw new InvalidOperationException("Asset-reference array length cannot be negative.");
            }

            Type elementType = valueType.GetElementType() ?? throw new InvalidOperationException($"Asset-reference array type '{valueType.FullName}' must expose one element type.");
            Array resolvedValues = Array.CreateInstance(elementType, length);
            for (int index = 0; index < length; index++) {
                SceneAssetReference reference = SceneAssetReferenceFactory.ReadOptionalReference(reader);
                if (reference == null) {
                    continue;
                }

                resolvedValues.SetValue(AutomaticComponentAssetReferenceSupport.ResolveRuntimeAssetReference(elementType, reference, ReferenceResolver), index);
            }

            return resolvedValues;
        }
    }
}
#endif
