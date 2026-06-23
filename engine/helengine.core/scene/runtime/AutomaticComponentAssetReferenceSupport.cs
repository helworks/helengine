namespace helengine {
    /// <summary>
    /// Provides shared detection and runtime resolution for automatically persisted asset-backed component members.
    /// </summary>
    public static class AutomaticComponentAssetReferenceSupport {
#if !HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION
        /// <summary>
        /// Message used when one reflected member type is not supported by automatic runtime asset-reference restoration.
        /// </summary>
        const string UnsupportedAssetReferenceTypeMessage = "Automatic component asset-reference support does not handle the supplied member type.";

        /// <summary>
        /// Returns whether the supplied reflected member type is persisted through scene asset references instead of direct value encoding.
        /// </summary>
        /// <param name="valueType">Reflected member type to inspect.</param>
        /// <returns>True when the member type is backed by one scene asset reference; otherwise false.</returns>
        public static bool IsSupportedAssetReferenceType(Type valueType) {
            if (valueType == null) {
                return false;
            }

            return valueType == typeof(FontAsset)
                || valueType == typeof(RuntimeTexture)
                || valueType == typeof(RuntimeModel)
                || valueType == typeof(RuntimeMaterial)
                || valueType == typeof(AnimationClipAsset);
        }

        /// <summary>
        /// Returns whether the supplied reflected member type is a supported one-dimensional array whose elements are persisted through scene asset references.
        /// </summary>
        /// <param name="valueType">Reflected member type to inspect.</param>
        /// <returns>True when the member type is a supported one-dimensional asset-reference array; otherwise false.</returns>
        public static bool IsSupportedAssetReferenceArrayType(Type valueType) {
            if (valueType == null || !valueType.IsArray || valueType.GetArrayRank() != 1) {
                return false;
            }

            Type elementType = valueType.GetElementType();
            return IsSupportedAssetReferenceType(elementType);
        }

        /// <summary>
        /// Builds the stable save-state key used for one reflected asset-backed member.
        /// </summary>
        /// <param name="memberName">Reflected member name.</param>
        /// <returns>Stable save-state key for the member.</returns>
        public static string BuildReferenceName(string memberName) {
            if (string.IsNullOrWhiteSpace(memberName)) {
                throw new ArgumentException("Member name must be provided.", nameof(memberName));
            }

            return memberName;
        }

        /// <summary>
        /// Builds one stable indexed save-state key used for an asset-backed array member element.
        /// </summary>
        /// <param name="memberName">Reflected array member name.</param>
        /// <param name="index">Zero-based array index.</param>
        /// <returns>Stable save-state key for the indexed array element.</returns>
        public static string BuildIndexedReferenceName(string memberName, int index) {
            if (string.IsNullOrWhiteSpace(memberName)) {
                throw new ArgumentException("Member name must be provided.", nameof(memberName));
            }
            if (index < 0) {
                throw new ArgumentOutOfRangeException(nameof(index), "Indexed asset-reference keys require a non-negative array index.");
            }

            return string.Concat(memberName, "[", index.ToString(), "]");
        }

        /// <summary>
        /// Resolves one packaged scene asset reference back into the runtime asset required by one reflected member type.
        /// </summary>
        /// <param name="valueType">Reflected member type being restored.</param>
        /// <param name="reference">Packaged scene asset reference to resolve.</param>
        /// <param name="referenceResolver">Runtime resolver used to rebuild packaged assets.</param>
        /// <returns>Resolved runtime asset or null when the reference is null.</returns>
        public static object ResolveRuntimeAssetReference(Type valueType, SceneAssetReference reference, RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (valueType == null) {
                throw new ArgumentNullException(nameof(valueType));
            }
            if (!IsSupportedAssetReferenceType(valueType)) {
                throw new InvalidOperationException(UnsupportedAssetReferenceTypeMessage);
            }
            if (reference == null) {
                return null;
            }
            if (referenceResolver == null) {
                throw new ArgumentNullException(nameof(referenceResolver));
            }

            if (valueType == typeof(FontAsset)) {
                return referenceResolver.ResolveFont(reference);
            }
            if (valueType == typeof(RuntimeTexture)) {
                return referenceResolver.ResolveTexture(reference);
            }
            if (valueType == typeof(RuntimeModel)) {
                return referenceResolver.ResolveModel(reference);
            }
            if (valueType == typeof(RuntimeMaterial)) {
                return referenceResolver.ResolveMaterial(reference);
            }
            if (valueType == typeof(AnimationClipAsset)) {
                return referenceResolver.ResolveAnimationClip(reference);
            }

            throw new InvalidOperationException(UnsupportedAssetReferenceTypeMessage);
        }
#endif
    }
}
