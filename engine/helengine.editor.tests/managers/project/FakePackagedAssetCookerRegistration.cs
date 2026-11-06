namespace helengine.editor.tests {
    /// <summary>
    /// Test registration that adds one packaged asset cooker to the host registry.
    /// </summary>
    public sealed class FakePackagedAssetCookerRegistration : IAssetImporterRegistration {
        /// <summary>
        /// Cooker registered when the host applies this registration.
        /// </summary>
        readonly IPackagedAssetCooker Cooker;

        /// <summary>
        /// Creates a registration for one cooker.
        /// </summary>
        /// <param name="cooker">Cooker to register.</param>
        public FakePackagedAssetCookerRegistration(IPackagedAssetCooker cooker) {
            Cooker = cooker ?? throw new ArgumentNullException(nameof(cooker));
        }

        /// <summary>
        /// Registers the cooker with the host registry.
        /// </summary>
        /// <param name="registry">Registry to register with.</param>
        public void Register(AssetImporterRegistry registry) {
            registry.RegisterPackagedAssetCooker(Cooker);
        }
    }
}
