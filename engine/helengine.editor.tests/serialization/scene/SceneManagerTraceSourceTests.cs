using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies that scene-manager transition diagnostics distinguish tracking, owned-asset registration, and scene-loaded event boundaries.
    /// </summary>
    public sealed class SceneManagerTraceSourceTests {
        /// <summary>
        /// Ensures the immediate scene-load path emits checkpoints after each remaining startup-scene commit boundary.
        /// </summary>
        [Fact]
        public void LoadSceneImmediate_whenTrackingMaterializedScene_emitsPostMaterializationTraceStages() {
            string sourcePath = Path.Combine(TestSourceRepositoryLocator.ResolveHelEngineRootPath(), "engine", "helengine.core", "scene", "runtime", "SceneManager.cs");
            string source = File.ReadAllText(sourcePath);

            Assert.Contains("LoadSceneImmediateAfterLoadedSceneRecordListAdd", source, StringComparison.Ordinal);
            Assert.Contains("LoadSceneImmediateAfterLoadedSceneRecordDictionaryAdd", source, StringComparison.Ordinal);
            Assert.Contains("LoadSceneImmediateBeforeRegisterOwnedTextures", source, StringComparison.Ordinal);
            Assert.Contains("LoadSceneImmediateBeforeRegisterOwnedFonts", source, StringComparison.Ordinal);
            Assert.Contains("LoadSceneImmediateBeforeRegisterOwnedAudio", source, StringComparison.Ordinal);
            Assert.Contains("LoadSceneImmediateBeforeRegisterOwnedModels", source, StringComparison.Ordinal);
            Assert.Contains("LoadSceneImmediateBeforeRegisterOwnedMaterials", source, StringComparison.Ordinal);
            Assert.Contains("LoadSceneImmediateAfterRegisterOwnedAssets", source, StringComparison.Ordinal);
            Assert.Contains("LoadSceneImmediateBeforeSceneLoadedEvent", source, StringComparison.Ordinal);
            Assert.Contains("LoadSceneImmediateAfterSceneLoadedEvent", source, StringComparison.Ordinal);
        }
    }
}
