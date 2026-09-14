using helengine;

namespace helengine.core.tests.scene.runtime {
    /// <summary>
    /// Verifies the shared reference-counting semantics used by all scene-owned asset categories tracked by the runtime scene manager.
    /// </summary>
    public sealed class SceneOwnedAssetReferenceTableTests {
        [Fact]
        public void Release_WhenAssetWasRegisteredTwice_KeepsAssetTrackedAfterOneRelease() {
            List<object> released = new List<object>();
            SceneOwnedAssetReferenceTable<object> table = new SceneOwnedAssetReferenceTable<object>("test asset", asset => released.Add(asset));
            object asset = new object();

            table.Register(new object[] { asset });
            table.Register(new object[] { asset });
            table.Release(new object[] { asset });

            Assert.Equal(1, table.Count);
            Assert.Empty(released);
        }

        [Fact]
        public void Release_WhenAssetWasRegisteredTwice_FreesAssetAfterSecondRelease() {
            List<object> released = new List<object>();
            SceneOwnedAssetReferenceTable<object> table = new SceneOwnedAssetReferenceTable<object>("test asset", asset => released.Add(asset));
            object asset = new object();

            table.Register(new object[] { asset });
            table.Register(new object[] { asset });
            table.Release(new object[] { asset });
            table.Release(new object[] { asset });

            Assert.Equal(0, table.Count);
            Assert.Single(released);
            Assert.Same(asset, released[0]);
        }

        [Fact]
        public void Release_WhenAssetWasNeverRegistered_ThrowsInvalidOperationException() {
            SceneOwnedAssetReferenceTable<object> table = new SceneOwnedAssetReferenceTable<object>("test asset", asset => { });
            object asset = new object();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => table.Release(new object[] { asset }));

            Assert.Contains("test asset", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Register_WhenOwnedAssetsIsNull_ThrowsArgumentNullException() {
            SceneOwnedAssetReferenceTable<object> table = new SceneOwnedAssetReferenceTable<object>("test asset", asset => { });

            Assert.Throws<ArgumentNullException>(() => table.Register(null));
        }

        [Fact]
        public void Register_WhenListContainsNullEntries_SkipsThemWithoutTracking() {
            SceneOwnedAssetReferenceTable<object> table = new SceneOwnedAssetReferenceTable<object>("test asset", asset => { });

            table.Register(new object[] { null });

            Assert.Equal(0, table.Count);
        }
    }
}
