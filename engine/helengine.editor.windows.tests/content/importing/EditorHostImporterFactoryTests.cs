using System.Reflection;

namespace helengine.editor.windows.tests.content.importing {
    /// <summary>
    /// Verifies metadata-only host importer registration.
    /// </summary>
    public sealed class EditorHostImporterFactoryTests {
        /// <summary>
        /// Ensures the editor host does not construct the Assimp importer directly during registration.
        /// </summary>
        [Fact]
        public void CreateDefault_WhenCalled_KeepsAssimpRegistrationLazy() {
            Assembly appAssembly = Assembly.Load("helengine.editor.app");
            Type factoryType = appAssembly.GetType("helengine.editor.app.EditorHostImporterFactory", true) ?? throw new InvalidOperationException("EditorHostImporterFactory was not found.");
            MethodInfo createDefaultMethod = factoryType.GetMethod("CreateDefault", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new InvalidOperationException("CreateDefault was not found.");
            object registrationsObject = createDefaultMethod.Invoke(null, Array.Empty<object>()) ?? throw new InvalidOperationException("CreateDefault returned null.");
            IReadOnlyList<IAssetImporterRegistration> registrations = (IReadOnlyList<IAssetImporterRegistration>)registrationsObject;
            ModelImporterRegistration modelRegistration = Assert.IsType<ModelImporterRegistration>(registrations.Single(registration => registration is ModelImporterRegistration));

            Assert.IsType<LazyModelImporter>(modelRegistration.Importer);
        }
    }
}
