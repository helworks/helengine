namespace helengine.editor.tests {
    /// <summary>
    /// Test cooker that claims one extension, writes a marker asset and returns a distinctive packaged reference.
    /// </summary>
    public sealed class FakePackagedAssetCooker : IPackagedAssetCooker {
        /// <summary>
        /// Build-relative path the cooker writes and returns as the packaged reference.
        /// </summary>
        public const string CookedRelativePath = "cooked/fake/clip.hfakecooked";

        /// <summary>
        /// Extension this cooker claims, including the leading dot.
        /// </summary>
        readonly string ClaimedExtension;

        /// <summary>
        /// References received by <see cref="Cook"/> in call order.
        /// </summary>
        public readonly List<SceneAssetReference> ReceivedReferences = new List<SceneAssetReference>();

        /// <summary>
        /// Contexts received by <see cref="Cook"/> in call order.
        /// </summary>
        public readonly List<PackagedAssetCookContext> ReceivedContexts = new List<PackagedAssetCookContext>();

        /// <summary>
        /// Creates a cooker for one extension.
        /// </summary>
        /// <param name="extension">Extension including the leading dot.</param>
        public FakePackagedAssetCooker(string extension) {
            ClaimedExtension = extension;
        }

        /// <summary>
        /// Gets the extension this cooker claims.
        /// </summary>
        public string Extension {
            get { return ClaimedExtension; }
        }

        /// <summary>
        /// Records the call, writes a marker asset and returns the packaged reference.
        /// </summary>
        /// <param name="reference">Authored reference being cooked.</param>
        /// <param name="context">Packaging context for the current build.</param>
        /// <returns>Packaged reference the scene should keep.</returns>
        public SceneAssetReference Cook(SceneAssetReference reference, PackagedAssetCookContext context) {
            ReceivedReferences.Add(reference);
            ReceivedContexts.Add(context);
            context.WriteAsset(CookedRelativePath, new TextAsset { Text = "cooked" });
            return context.CreatePackagedReference(CookedRelativePath);
        }
    }
}
