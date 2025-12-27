using System.Reflection;

namespace helengine.editor {
    /// <summary>
    /// Loads generated shader module assemblies and extracts their definitions.
    /// </summary>
    public class ShaderModuleLoader {
        /// <summary>
        /// Fully qualified type name that generated shader modules must expose.
        /// </summary>
        const string ModuleTypeName = "helengine.HelengineShaderModule";

        /// <summary>
        /// Loads a shader module from the specified assembly path.
        /// </summary>
        /// <param name="assemblyPath">Absolute path to the module assembly.</param>
        /// <returns>A handle for the loaded shader module.</returns>
        public ShaderModuleHandle LoadModule(string assemblyPath) {
            if (string.IsNullOrWhiteSpace(assemblyPath)) {
                throw new ArgumentException("Assembly path must be provided.", nameof(assemblyPath));
            }

            if (!File.Exists(assemblyPath)) {
                throw new FileNotFoundException("Shader module assembly was not found.", assemblyPath);
            }

            string moduleDirectory = Path.GetDirectoryName(assemblyPath);
            if (string.IsNullOrWhiteSpace(moduleDirectory)) {
                throw new InvalidOperationException("Unable to resolve the module directory.");
            }

            var loadContext = new ShaderModuleLoadContext(moduleDirectory);
            try {
                Assembly assembly;
                using (FileStream assemblyStream = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                    assembly = loadContext.LoadFromStream(assemblyStream);
                }

                Type moduleType = assembly.GetType(ModuleTypeName, true, false);
                if (moduleType == null) {
                    throw new InvalidOperationException("Shader module type was not found in the assembly.");
                }

                if (!typeof(IShaderModule).IsAssignableFrom(moduleType)) {
                    throw new InvalidOperationException("Shader module type does not implement IShaderModule.");
                }

                object instance = Activator.CreateInstance(moduleType);
                if (instance == null) {
                    throw new InvalidOperationException("Failed to create the shader module instance.");
                }

                var module = (IShaderModule)instance;
                ShaderModuleDefinition definition = module.BuildDefinition(moduleDirectory);
                return new ShaderModuleHandle(loadContext, assemblyPath, definition);
            } catch {
                loadContext.Unload();
                throw;
            }
        }
    }
}
