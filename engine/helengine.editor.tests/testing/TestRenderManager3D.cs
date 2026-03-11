using helengine;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// Records model-build requests for editor gizmo factory tests.
    /// </summary>
    public class TestRenderManager3D : RenderManager3D {
        /// <summary>
        /// Captured raw model assets passed through the build API.
        /// </summary>
        readonly List<ModelAsset> BuiltModelAssetsValue;

        /// <summary>
        /// Initializes a new test render manager.
        /// </summary>
        public TestRenderManager3D() {
            BuiltModelAssetsValue = new List<ModelAsset>();
        }

        /// <summary>
        /// Gets the raw model assets that were passed to the renderer.
        /// </summary>
        public IReadOnlyList<ModelAsset> BuiltModelAssets => BuiltModelAssetsValue;

        /// <summary>
        /// Records the supplied model asset and returns a placeholder runtime model.
        /// </summary>
        /// <param name="data">Raw model data to capture.</param>
        /// <returns>Placeholder runtime model for test assertions.</returns>
        public override RuntimeModel BuildModelFromRaw(ModelAsset data) {
            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

            BuiltModelAssetsValue.Add(data);
            return new TestRuntimeModel();
        }
    }
}
