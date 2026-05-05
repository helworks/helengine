using System.Reflection;

namespace helengine.editor.windows.tests.content.textures {
    /// <summary>
    /// Verifies metadata-driven texture importer activation.
    /// </summary>
    public sealed class AssemblyTextureImporterFactoryTests {
        /// <summary>
        /// Ensures the backend assembly is not loaded until the importer is first created.
        /// </summary>
        [Fact]
        public void CreateImporter_WhenBackendAssemblyIsDeferred_LoadsAssemblyOnDemand() {
            string assemblyName = "helengine.editor.windows.gdiimporter";
            Assert.DoesNotContain(
                AppDomain.CurrentDomain.GetAssemblies(),
                assembly => string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));

            ITextureImporterFactory factory = new AssemblyTextureImporterFactory(
                assemblyName,
                "helengine.editor.GDITextureImporter");

            ITextureImporter importer = factory.CreateImporter();

            Assert.NotNull(importer);
            Assert.Contains(
                AppDomain.CurrentDomain.GetAssemblies(),
                assembly => string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
