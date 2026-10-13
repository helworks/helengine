using System.Reflection;
using helengine.baseplatform.Player;

namespace helengine.player {
    /// <summary>
    /// Instantiates the platform player adapter named on the command line from the platform builder assembly.
    /// </summary>
    public static class PlayerPlatformAdapterLoader {
        /// <summary>
        /// Loads <paramref name="assemblyPath"/>, resolves <paramref name="typeName"/> inside it and creates one adapter instance.
        /// </summary>
        /// <param name="assemblyPath">Path to the platform builder assembly.</param>
        /// <param name="typeName">Full type name of the adapter implementation.</param>
        /// <returns>New, not yet initialized adapter instance.</returns>
        /// <exception cref="InvalidOperationException">The resolved type does not implement <see cref="IPlatformPlayerAdapter"/>.</exception>
        public static IPlatformPlayerAdapter Load(string assemblyPath, string typeName) {
            if (string.IsNullOrWhiteSpace(assemblyPath)) {
                throw new ArgumentException("Adapter assembly path must be provided.", nameof(assemblyPath));
            }
            if (string.IsNullOrWhiteSpace(typeName)) {
                throw new ArgumentException("Adapter type name must be provided.", nameof(typeName));
            }

            Assembly assembly = Assembly.LoadFrom(assemblyPath);
            Type adapterType = assembly.GetType(typeName, throwOnError: true);
            object instance = Activator.CreateInstance(adapterType);
            if (instance is IPlatformPlayerAdapter adapter) {
                return adapter;
            }

            throw new InvalidOperationException($"Type '{typeName}' from '{assemblyPath}' does not implement {nameof(IPlatformPlayerAdapter)}.");
        }
    }
}
