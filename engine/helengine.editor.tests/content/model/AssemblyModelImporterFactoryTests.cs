namespace helengine.editor.tests.content.model {
    /// <summary>
    /// Verifies metadata-driven model importer activation.
    /// </summary>
    public sealed class AssemblyModelImporterFactoryTests {
        /// <summary>
        /// Ensures the factory does not eagerly load the Assimp backend before importer creation and that importer creation leaves the backend available.
        /// </summary>
        [Fact]
        public void CreateImporter_WhenAssimpBackendIsDeferred_LoadsAssemblyOnDemand() {
            string assemblyName = "helengine.editor.assimp";
            bool wasLoadedBeforeFactoryCreation = AppDomain.CurrentDomain.GetAssemblies()
                .Any(assembly => string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));

            IModelImporterFactory factory = new AssemblyModelImporterFactory(
                assemblyName,
                "helengine.editor.assimp.HelengineAssimpImporter");

            bool wasLoadedAfterFactoryCreation = AppDomain.CurrentDomain.GetAssemblies()
                .Any(assembly => string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));

            Assert.Equal(wasLoadedBeforeFactoryCreation, wasLoadedAfterFactoryCreation);

            IModelImporter importer = factory.CreateImporter();

            Assert.NotNull(importer);
            Assert.True(
                AppDomain.CurrentDomain.GetAssemblies()
                    .Any(assembly => string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
