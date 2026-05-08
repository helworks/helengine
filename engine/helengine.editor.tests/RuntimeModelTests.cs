using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies baseline runtime-model behavior used by renderer backends and editor test doubles.
    /// </summary>
    public sealed class RuntimeModelTests {
        /// <summary>
        /// Ensures runtime models always expose a non-null submesh collection.
        /// </summary>
        [Fact]
        public void Constructor_WhenModelIsCreated_InitializesAnEmptySubmeshCollection() {
            TestRuntimeModel model = new TestRuntimeModel();

            Assert.NotNull(model.Submeshes);
            Assert.Empty(model.Submeshes);
        }
    }
}
