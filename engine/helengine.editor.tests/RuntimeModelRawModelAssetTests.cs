using helengine.editor.tests.testing;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies raw model data retained by runtime models for load-time geometry preparation.
    /// </summary>
    public sealed class RuntimeModelRawModelAssetTests {
        /// <summary>
        /// Ensures one renderer can retain raw geometry beside its render-ready runtime model.
        /// </summary>
        [Fact]
        public void SetRawModelAsset_WhenModelIsSupplied_RetainsTheSuppliedAsset() {
            TestRuntimeModel runtimeModel = new TestRuntimeModel();
            ModelAsset rawModelAsset = new ModelAsset();

            runtimeModel.SetRawModelAsset(rawModelAsset);

            Assert.Same(rawModelAsset, runtimeModel.RawModelAsset);
        }
    }
}
