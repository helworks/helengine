namespace helengine.editor.tests.content.model {
    /// <summary>
    /// Verifies metadata-driven model importer activation.
    /// </summary>
    public sealed class AssemblyModelImporterFactoryTests {
        /// <summary>
        /// Ensures the Assimp backend assembly is not loaded until the importer is first created.
        /// </summary>
        [Fact]
        public void CreateImporter_WhenAssimpBackendIsDeferred_LoadsAssemblyOnDemand() {
            string assemblyName = "helengine.editor.assimp";
            Assert.DoesNotContain(
                AppDomain.CurrentDomain.GetAssemblies(),
                assembly => string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));

            IModelImporterFactory factory = new AssemblyModelImporterFactory(
                assemblyName,
                "helengine.editor.assimp.HelengineAssimpImporter");

            IModelImporter importer = factory.CreateImporter();

            Assert.NotNull(importer);
            Assert.Contains(
                AppDomain.CurrentDomain.GetAssemblies(),
                assembly => string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
