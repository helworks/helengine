using System.Reflection;
using System.Runtime.Loader;

namespace helengine.editor {
    /// <summary>
    /// Collectible load context used to isolate and unload generated script assemblies.
    /// </summary>
    public sealed class EditorCollectibleScriptAssemblyLoadContext : AssemblyLoadContext {
        /// <summary>
        /// Dependency resolver rooted at the main assembly file loaded into this context.
        /// </summary>
        readonly AssemblyDependencyResolver Resolver;

        /// <summary>
        /// Initializes one collectible context for a single built script assembly.
        /// </summary>
        /// <param name="mainAssemblyPath">Absolute path to the main assembly to load.</param>
        public EditorCollectibleScriptAssemblyLoadContext(string mainAssemblyPath) : base(true) {
            if (string.IsNullOrWhiteSpace(mainAssemblyPath)) {
                throw new ArgumentException("Main assembly path must be provided.", nameof(mainAssemblyPath));
            }

            Resolver = new AssemblyDependencyResolver(mainAssemblyPath);
        }

        /// <summary>
        /// Resolves managed dependencies for the loaded scripting assembly.
        /// </summary>
        /// <param name="assemblyName">Requested dependency identity.</param>
        /// <returns>Loaded dependency assembly or null when the default context should resolve it.</returns>
        protected override Assembly Load(AssemblyName assemblyName) {
            if (assemblyName == null || string.IsNullOrWhiteSpace(assemblyName.Name)) {
                return null;
            }

            if (IsAlreadyLoadedInDefaultContext(assemblyName.Name)) {
                return null;
            }

            string assemblyPath = Resolver.ResolveAssemblyToPath(assemblyName);
            if (!string.IsNullOrWhiteSpace(assemblyPath)) {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }

        /// <summary>
        /// Resolves unmanaged dependencies for the loaded scripting assembly.
        /// </summary>
        /// <param name="unmanagedDllName">Requested native library name.</param>
        /// <returns>Loaded native library handle or zero when unresolved.</returns>
        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName) {
            if (string.IsNullOrWhiteSpace(unmanagedDllName)) {
                return IntPtr.Zero;
            }

            string unmanagedDllPath = Resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (!string.IsNullOrWhiteSpace(unmanagedDllPath)) {
                return LoadUnmanagedDllFromPath(unmanagedDllPath);
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Returns whether the supplied assembly name already exists in the default load context.
        /// </summary>
        /// <param name="assemblyName">Simple assembly name to inspect.</param>
        /// <returns>True when the assembly is already loaded outside this collectible context.</returns>
        bool IsAlreadyLoadedInDefaultContext(string assemblyName) {
            foreach (Assembly assembly in AssemblyLoadContext.Default.Assemblies) {
                if (string.Equals(assembly.GetName().Name, assemblyName, StringComparison.Ordinal)) {
                    return true;
                }
            }

            return false;
        }
    }
}
