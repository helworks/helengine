using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Locks native ownership cleanup for the remaining explicit runtime component deserializers that still materialize transient scene asset references.
    /// </summary>
    public sealed class RuntimeComponentDeserializerSourceAuditTests {
        /// <summary>
        /// Ensures the explicit mesh deserializer releases transient scene asset references after resolving runtime assets.
        /// </summary>
        [Fact]
        public void Deserialize_whenExplicitRuntimeMeshDeserializerResolvesTransientSceneAssetReferences_releasesTransientReferencesThroughNativeOwnership() {
            AssertDeserializerContains(
                "RuntimeMeshComponentDeserializer.cs",
                "NativeOwnership.Delete(modelReference);");
            AssertDeserializerContains(
                "RuntimeMeshComponentDeserializer.cs",
                "NativeOwnership.DeleteItemsAndRelease(ref materialReferences);");
        }

        /// <summary>
        /// Ensures the explicit sprite deserializer releases the transient texture scene reference after resolving the runtime texture.
        /// </summary>
        [Fact]
        public void Deserialize_whenExplicitRuntimeSpriteDeserializerResolvesTransientSceneAssetReference_releasesTheTransientReferenceThroughNativeOwnership() {
            AssertDeserializerContains(
                "RuntimeSpriteComponentDeserializer.cs",
                "NativeOwnership.Delete(textureReference);");
        }

        /// <summary>
        /// Ensures one runtime component deserializer source file contains the required native-ownership cleanup contract.
        /// </summary>
        /// <param name="fileName">Deserializer source file name relative to the runtime scene folder.</param>
        /// <param name="expectedSnippet">Required ownership-cleanup snippet.</param>
        static void AssertDeserializerContains(string fileName, string expectedSnippet) {
            string sourcePath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "helengine.core",
                "scene",
                "runtime",
                fileName));
            string sourceText = File.ReadAllText(sourcePath);

            Assert.Contains(expectedSnippet, sourceText);
        }
    }
}
