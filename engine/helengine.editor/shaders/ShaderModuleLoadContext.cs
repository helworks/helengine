using System.Reflection;
using System.Runtime.Loader;

namespace helengine.editor {
    /// <summary>
    /// Provides an unloadable load context for shader module assemblies.
    /// </summary>
    public sealed class ShaderModuleLoadContext : AssemblyLoadContext {
        /// <summary>
        /// Name of the core assembly that must be shared with the default context.
        /// </summary>
        const string CoreAssemblyName = "helengine.core";

        /// <summary>
        /// Absolute directory containing the shader module and its dependencies.
        /// </summary>
        readonly string moduleDirectory;

        /// <summary>
        /// Initializes a new load context rooted at the provided module directory.
        /// </summary>
        /// <param name="moduleDirectory">Directory containing the module assembly.</param>
        public ShaderModuleLoadContext(string moduleDirectory) : base(true) {
            if (string.IsNullOrWhiteSpace(moduleDirectory)) {
                throw new ArgumentException("Module directory must be provided.", nameof(moduleDirectory));
            }

            this.moduleDirectory = moduleDirectory;
        }

        /// <summary>
        /// Resolves assembly dependencies for the shader module.
        /// </summary>
        /// <param name="assemblyName">Assembly name being resolved.</param>
        /// <returns>The resolved assembly, or null when fallback resolution should be used.</returns>
        protected override Assembly Load(AssemblyName assemblyName) {
            if (assemblyName == null) {
                throw new ArgumentNullException(nameof(assemblyName));
            }

            if (string.Equals(assemblyName.Name, CoreAssemblyName, StringComparison.OrdinalIgnoreCase)) {
                return AssemblyLoadContext.Default.LoadFromAssemblyName(assemblyName);
            }

            if (string.IsNullOrWhiteSpace(assemblyName.Name)) {
                return null;
            }

            string candidatePath = Path.Combine(moduleDirectory, $"{assemblyName.Name}.dll");
            if (File.Exists(candidatePath)) {
                return LoadFromAssemblyPath(candidatePath);
            }

            return null;
        }
    }
}
