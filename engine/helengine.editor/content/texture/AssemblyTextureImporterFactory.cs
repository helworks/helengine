using System.Reflection;

namespace helengine.editor {
    /// <summary>
    /// Creates texture importers by loading a backend assembly and activating a named type on demand.
    /// </summary>
    public sealed class AssemblyTextureImporterFactory : ITextureImporterFactory {
        /// <summary>
        /// Simple assembly name resolved when the importer is first created.
        /// </summary>
        readonly string AssemblyName;

        /// <summary>
        /// Fully qualified importer type name resolved inside the target assembly.
        /// </summary>
        readonly string TypeName;

        /// <summary>
        /// Initializes a new factory for one assembly-qualified importer type.
        /// </summary>
        /// <param name="assemblyName">Simple assembly name that contains the importer type.</param>
        /// <param name="typeName">Fully qualified importer type name.</param>
        public AssemblyTextureImporterFactory(string assemblyName, string typeName) {
            if (string.IsNullOrWhiteSpace(assemblyName)) {
                throw new ArgumentException("Assembly name must be provided.", nameof(assemblyName));
            }

            if (string.IsNullOrWhiteSpace(typeName)) {
                throw new ArgumentException("Type name must be provided.", nameof(typeName));
            }

            AssemblyName = assemblyName;
            TypeName = typeName;
        }

        /// <summary>
        /// Creates one texture importer by loading the backend assembly and activating the configured type.
        /// </summary>
        /// <returns>Concrete texture importer instance.</returns>
        public ITextureImporter CreateImporter() {
            Assembly assembly = Assembly.Load(AssemblyName);
            Type importerType = assembly.GetType(TypeName, true) ?? throw new InvalidOperationException($"Texture importer type '{TypeName}' was not found in '{AssemblyName}'.");
            object instance = Activator.CreateInstance(importerType) ?? throw new InvalidOperationException($"Texture importer type '{TypeName}' could not be constructed.");
            if (instance is ITextureImporter importer) {
                return importer;
            }

            throw new InvalidOperationException($"Texture importer type '{TypeName}' does not implement {nameof(ITextureImporter)}.");
        }
    }
}
