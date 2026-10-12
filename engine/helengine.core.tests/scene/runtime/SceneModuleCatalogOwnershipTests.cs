namespace helengine.core.tests.scene.runtime {
    /// <summary>Verifies dependency metadata belongs to copied catalog entries rather than external constructor arguments.</summary>
    public sealed class SceneModuleCatalogOwnershipTests {
        /// <summary>Ensures catalog teardown releases its copied metadata once while preserving caller-owned entries and arrays.</summary>
        [Fact]
        public void CatalogOwnsIndependentEntriesAndReleasesCopiedDependencies() {
            string[] required = ["gameplay"];
            RuntimeSceneCatalogEntry original = new("game", "cooked/scenes/game.hasset", required);
            RuntimeSceneCatalogEntry[] borrowedEntries = [original];
            RuntimeSceneCatalog catalog = new(borrowedEntries);
            RuntimeSceneCatalogEntry copied = Assert.Single(catalog.Entries);
            Assert.NotSame(borrowedEntries, catalog.Entries);
            Assert.NotSame(original, copied);
            Assert.NotSame(required, copied.RequiredCodeModuleIds);
            Assert.NotSame(original.RequiredCodeModuleIds, copied.RequiredCodeModuleIds);
            required[0] = "changed";
            Assert.Equal(["gameplay"], copied.RequiredCodeModuleIds);
            original.RequiredCodeModuleIds[0] = "caller-change";
            Assert.Equal(["gameplay"], copied.RequiredCodeModuleIds);
            copied.RequiredCodeModuleIds[0] = "copy-change";
            Assert.Equal(["caller-change"], original.RequiredCodeModuleIds);
            catalog.Dispose();
            catalog.Dispose();
            Assert.Null(catalog.Entries);
            Assert.Null(copied.RequiredCodeModuleIds);
            Assert.Same(original, Assert.Single(borrowedEntries));
            Assert.Equal(["caller-change"], original.RequiredCodeModuleIds);
            original.Dispose();
            original.Dispose();
            Assert.Null(original.RequiredCodeModuleIds);
        }

        /// <summary>Ensures the empty owned array and lookup map can be released repeatedly without claiming ownership of the borrowed constructor array.</summary>
        [Fact]
        public void EmptyCatalogDisposesTwiceAndPreservesBorrowedInput() {
            RuntimeSceneCatalogEntry[] borrowedEntries = [];
            RuntimeSceneCatalog catalog = new(borrowedEntries);
            Assert.NotSame(borrowedEntries, catalog.Entries);
            Assert.Empty(catalog.Entries);
            catalog.Dispose();
            catalog.Dispose();
            Assert.Null(catalog.Entries);
            Assert.Empty(borrowedEntries);
        }
    }
}
