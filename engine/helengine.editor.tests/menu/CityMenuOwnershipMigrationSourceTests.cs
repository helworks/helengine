using Xunit;

namespace helengine.editor.tests.menu {
    /// <summary>
    /// Guards the rule that the demo-disc menu stack belongs to the city project rather than the engine.
    /// </summary>
    public sealed class CityMenuOwnershipMigrationSourceTests {
        /// <summary>
        /// Absolute repository root used by source-level migration checks.
        /// </summary>
        const string RepoRootPath = @"C:\dev\helworks\helengine";

        /// <summary>
        /// Absolute city project root used by source-level migration checks.
        /// </summary>
        const string CityProjectRootPath = @"C:\dev\helprojs\city";

        /// <summary>
        /// Verifies the engine FPS component no longer references menu-specific runtime components.
        /// </summary>
        [Fact]
        public void ReadFpsComponentSource_DoesNotReferenceMenuComponent() {
            string sourcePath = Path.Combine(RepoRootPath, "engine", "helengine.core", "components", "2d", "FPSComponent.cs");
            Assert.True(File.Exists(sourcePath), $"Expected source file at '{sourcePath}'.");

            string source = File.ReadAllText(sourcePath);

            Assert.DoesNotContain("MenuComponent", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies the city project owns the baked menu runtime component with a city serialized type id.
        /// </summary>
        [Fact]
        public void ReadCityMenuComponentSource_UsesCitySerializedTypeIds() {
            string sourcePath = Path.Combine(CityProjectRootPath, "assets", "codebase", "menu", "MenuComponent.cs");
            Assert.True(File.Exists(sourcePath), $"Expected source file at '{sourcePath}'.");

            string source = File.ReadAllText(sourcePath);

            Assert.Contains("public const string SerializedComponentTypeId = \"city.menu.MenuComponent, gameplay\";", source, StringComparison.Ordinal);
        }
    }
}
