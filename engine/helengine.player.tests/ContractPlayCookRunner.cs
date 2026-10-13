using helengine.editor;

namespace helengine.player.tests {
    /// <summary>
    /// Minimal cook step for the editor-to-player argument contract test: every cook succeeds with fixed content and
    /// startup scene values.
    /// </summary>
    public sealed class ContractPlayCookRunner : IEditorPlayCookRunner {
        /// <summary>Content root reported by every cook.</summary>
        public const string ContentRootPath = @"C:\project\cache\play\ps2-staging";

        /// <summary>Startup scene identifier reported by every cook.</summary>
        public const string StartupSceneId = "scene-cube-test";

        /// <summary>Startup scene path, relative to the cooked layout, reported by every cook.</summary>
        public const string StartupSceneCookedRelativePath = "cooked/scenes/cube_test.hasset";

        /// <summary>
        /// Returns a successful cook naming the fixed content root and startup scene.
        /// </summary>
        /// <param name="platformId">Platform the session service asked to cook; unused.</param>
        /// <returns>The successful cook outcome.</returns>
        public EditorPlayCookResult Cook(string platformId) {
            return new EditorPlayCookResult(true, "Cooked.", ContentRootPath, StartupSceneId, StartupSceneCookedRelativePath);
        }
    }
}
