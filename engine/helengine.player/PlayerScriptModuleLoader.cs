using System.Reflection;

namespace helengine.player {
    /// <summary>
    /// Loads the game's compiled script modules into the default load context so that persisted component types
    /// referenced by the cooked scene can be resolved by reflection when the scene is loaded.
    /// </summary>
    public static class PlayerScriptModuleLoader {
        /// <summary>
        /// Loads every script module laid out as <c>&lt;modulesRootPath&gt;/&lt;moduleId&gt;/&lt;moduleId&gt;.dll</c>.
        /// Assemblies go into the default, non-collectible context through <see cref="Assembly.LoadFrom(string)"/>, which is what
        /// the persisted component type resolver scans; any <c>.pdb</c> next to a module is picked up by an attached debugger.
        /// An empty modules folder is valid and loads nothing.
        /// </summary>
        /// <param name="modulesRootPath">Root folder that holds one sub-folder per script module.</param>
        /// <exception cref="DirectoryNotFoundException">The modules root folder does not exist.</exception>
        /// <exception cref="FileNotFoundException">A module folder does not contain its <c>&lt;moduleId&gt;.dll</c>.</exception>
        public static void LoadAll(string modulesRootPath) {
            if (string.IsNullOrWhiteSpace(modulesRootPath)) {
                throw new ArgumentException("Script modules root path must be provided.", nameof(modulesRootPath));
            }
            if (!Directory.Exists(modulesRootPath)) {
                throw new DirectoryNotFoundException($"Script modules root '{modulesRootPath}' does not exist.");
            }

            string[] moduleDirectories = Directory.GetDirectories(modulesRootPath);
            Array.Sort(moduleDirectories, StringComparer.Ordinal);
            for (int index = 0; index < moduleDirectories.Length; index++) {
                string moduleId = Path.GetFileName(moduleDirectories[index]);
                string assemblyPath = Path.Combine(moduleDirectories[index], moduleId + ".dll");
                if (!File.Exists(assemblyPath)) {
                    throw new FileNotFoundException($"Script module '{moduleId}' is missing its assembly.", assemblyPath);
                }

                Assembly.LoadFrom(assemblyPath);
            }
        }
    }
}
