namespace helengine.editor {
    /// <summary>
    /// Exposes one scene-load scope that records authored references repaired by the editor.
    /// </summary>
    public interface IEditorAssetReferenceHealingResolver {
        /// <summary>Starts recording reference replacements for one load.</summary>
        void BeginReferenceHealing();
        /// <summary>Completes recording and returns old-to-canonical replacements.</summary>
        IReadOnlyDictionary<SceneAssetReference, SceneAssetReference> CompleteReferenceHealing();
        /// <summary>Cancels recording and discards replacements.</summary>
        IReadOnlyDictionary<SceneAssetReference, SceneAssetReference> CancelReferenceHealing();
    }
}
