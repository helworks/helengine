using helengine.editor;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies validation compatibility for generated scene asset references across authored and packaged runtime path shapes.
    /// </summary>
    public sealed class SceneAssetReferenceValidationServiceTests {
        /// <summary>
        /// Ensures current file-backed references accept their authored identity and content hash.
        /// </summary>
        [Fact]
        public void ValidateTypedReference_WhenFileReferenceIsCanonical_AcceptsReference() {
            SceneAssetReference reference = global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateCurrentFileSystem("Models/Arrow.obj");

            SceneAssetReferenceValidationService.ValidateAuthoredTypedReference(typeof(RuntimeModel), reference, "test model reference");
        }

        /// <summary>
        /// Ensures path-only file references cannot enter current editor persistence.
        /// </summary>
        [Fact]
        public void ValidateAuthoredTypedReference_WhenFileReferenceIsPathOnly_RejectsReference() {
            SceneAssetReference reference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemModel("Models/Arrow.obj");

            Assert.Throws<InvalidOperationException>(() =>
                SceneAssetReferenceValidationService.ValidateAuthoredTypedReference(typeof(RuntimeModel), reference, "test model reference"));
        }

        /// <summary>
        /// Ensures cooked runtime payloads can retain their path-only reference contract.
        /// </summary>
        [Fact]
        public void ValidateTypedReference_WhenFileReferenceIsPackagedPathOnly_AcceptsReference() {
            SceneAssetReference reference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemModel("cooked/models/arrow.hasset");

            SceneAssetReferenceValidationService.ValidateTypedReference(typeof(RuntimeModel), reference, "packaged model reference");
        }

        /// <summary>
        /// Ensures packaged generated model references remain valid when the active platform runtime contract requires rooted packaged paths.
        /// </summary>
        [Fact]
        public void ValidateTypedReference_WhenGeneratedModelUsesRootedPackagedPath_AcceptsReference() {
            SceneAssetReference reference = global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateSerialized(
                SceneAssetReferenceSourceKind.Generated,
                "/cooked/engine/models/cube.hasset",
                "engine",
                global::helengine.ModelUtils.GeneratedCubeModelId);

            SceneAssetReferenceValidationService.ValidateTypedReference(typeof(RuntimeModel), reference, "test model reference");
        }

        /// <summary>
        /// Ensures packaged generated material references remain valid when the active platform runtime contract requires rooted packaged paths.
        /// </summary>
        [Fact]
        public void ValidateTypedReference_WhenGeneratedMaterialUsesRootedPackagedPath_AcceptsReference() {
            SceneAssetReference reference = global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateSerialized(
                SceneAssetReferenceSourceKind.Generated,
                "/cooked/engine/materials/standard.hasset",
                "engine",
                global::helengine.EngineSceneAssetReferenceFactory.StandardMaterialAssetId);

            SceneAssetReferenceValidationService.ValidateTypedReference(typeof(RuntimeMaterial), reference, "test material reference");
        }
    }
}
