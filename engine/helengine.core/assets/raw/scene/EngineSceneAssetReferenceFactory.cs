namespace helengine {
    /// <summary>
    /// Creates sanctioned engine-generated scene asset references.
    /// </summary>
    public static class EngineSceneAssetReferenceFactory {
        /// <summary>
        /// Stable provider identifier used by engine-generated scene asset references.
        /// </summary>
        public const string ProviderIdValue = "engine";

        /// <summary>
        /// Stable relative path for the generated cube model reference.
        /// </summary>
        public const string CubeRelativePath = "Engine/Models/Cube";

        /// <summary>
        /// Stable relative path for the generated plane model reference.
        /// </summary>
        public const string PlaneRelativePath = "Engine/Models/Plane";

        /// <summary>
        /// Stable relative path for the generated sphere model reference.
        /// </summary>
        public const string SphereRelativePath = "Engine/Models/Sphere";

        /// <summary>
        /// Stable relative path for the generated standard material reference.
        /// </summary>
        public const string StandardMaterialRelativePath = "Engine/Materials/Standard";

        /// <summary>
        /// Stable generated asset id for the engine standard material reference.
        /// </summary>
        public const string StandardMaterialAssetId = "engine:material:standard";

        /// <summary>
        /// Creates the generated engine cube model reference.
        /// </summary>
        /// <returns>Validated generated cube model reference.</returns>
        public static SceneAssetReference CreateCubeModel() {
            return CreateGenerated(CubeRelativePath, ModelUtils.GeneratedCubeModelId);
        }

        /// <summary>
        /// Creates the generated engine plane model reference.
        /// </summary>
        /// <returns>Validated generated plane model reference.</returns>
        public static SceneAssetReference CreatePlaneModel() {
            return CreateGenerated(PlaneRelativePath, ModelUtils.GeneratedPlaneModelId);
        }

        /// <summary>
        /// Creates the generated engine sphere model reference.
        /// </summary>
        /// <returns>Validated generated sphere model reference.</returns>
        public static SceneAssetReference CreateSphereModel() {
            return CreateGenerated(SphereRelativePath, ModelUtils.GeneratedSphereModelId);
        }

        /// <summary>
        /// Creates the generated engine standard material reference.
        /// </summary>
        /// <returns>Validated generated standard material reference.</returns>
        public static SceneAssetReference CreateStandardMaterial() {
            return CreateGenerated(StandardMaterialRelativePath, StandardMaterialAssetId);
        }

        /// <summary>
        /// Creates one validated engine-generated scene asset reference.
        /// </summary>
        /// <param name="relativePath">Generated relative path.</param>
        /// <param name="assetId">Generated asset id.</param>
        /// <returns>Validated generated scene asset reference.</returns>
        static SceneAssetReference CreateGenerated(string relativePath, string assetId) {
            return new SceneAssetReference(SceneAssetReferenceSourceKind.Generated, relativePath, ProviderIdValue, assetId);
        }
    }
}
