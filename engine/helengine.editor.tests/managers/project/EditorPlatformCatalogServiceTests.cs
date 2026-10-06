using helengine.platforms;

namespace helengine.editor.tests.managers.project {
    /// <summary>
    /// Verifies optional inspector metadata does not require every project platform to be installed.
    /// </summary>
    public sealed class EditorPlatformCatalogServiceTests {
        /// <summary>
        /// An absent builder is a normal result for metadata lookup, while an explicit build lookup still fails.
        /// </summary>
        [Fact]
        public void ResolveSelectionModel_WhenPlatformIsUnavailable_ReturnsNullWithoutThrowing() {
            EditorPlatformCatalogService catalog = new EditorPlatformCatalogService(Array.Empty<AvailablePlatformDescriptor>());
            Assert.Null(catalog.ResolveSelectionModel("dc"));
            Assert.Throws<InvalidOperationException>(() => catalog.Resolve("dc"));
        }

        /// <summary>
        /// A catalog entry may exist even when its platform payload or builder has not been installed locally.
        /// </summary>
        [Theory]
        [InlineData(false, "uninstalled-builder.dll")]
        [InlineData(true, "")]
        public void ResolveSelectionModel_WhenBuilderIsNotInstalled_ReturnsNull(bool installed, string assemblyPath) {
            EditorPlatformCatalogService catalog = new EditorPlatformCatalogService(new[] {
                new AvailablePlatformDescriptor("dc", "Dreamcast", assemblyPath, isInstalled: installed)
            });
            Assert.Null(catalog.ResolveSelectionModel("dc"));
        }
    }
}
