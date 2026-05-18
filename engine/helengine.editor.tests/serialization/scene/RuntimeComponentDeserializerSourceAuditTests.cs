using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Locks native ownership cleanup for runtime component deserializers that materialize transient scene asset references.
    /// </summary>
    public sealed class RuntimeComponentDeserializerSourceAuditTests {
        /// <summary>
        /// Ensures runtime text, sprite, debug, fps, and mesh deserializers release transient scene asset references after resolving runtime assets.
        /// </summary>
        [Fact]
        public void Deserialize_whenRuntimeComponentDeserializersResolveTransientSceneAssetReferences_releasesTransientReferencesThroughNativeOwnership() {
            AssertDeserializerContains(
                "RuntimeTextComponentDeserializer.cs",
                "NativeOwnership.Delete(fontReference);");
            AssertDeserializerContains(
                "RuntimeSpriteComponentDeserializer.cs",
                "NativeOwnership.Delete(textureReference);");
            AssertDeserializerContains(
                "RuntimeDebugComponentDeserializer.cs",
                "NativeOwnership.Delete(fontReference);");
            AssertDeserializerContains(
                "RuntimeFPSComponentDeserializer.cs",
                "NativeOwnership.Delete(fontReference);");
            AssertDeserializerContains(
                "RuntimeMeshComponentDeserializer.cs",
                "NativeOwnership.Delete(modelReference);");
            AssertDeserializerContains(
                "RuntimeMeshComponentDeserializer.cs",
                "NativeOwnership.DeleteItemsAndRelease(ref materialReferences);");
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
