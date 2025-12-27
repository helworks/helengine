using System.Text.Json;

namespace helengine.editor {
    /// <summary>
    /// Loads and indexes shader entries from a manifest for quick editor lookup.
    /// </summary>
    public class ShaderManifestIndex {
        /// <summary>
        /// Stores the manifest entries indexed by absolute source path.
        /// </summary>
        readonly Dictionary<string, ShaderManifestIndexEntry> entriesBySourcePath;

        /// <summary>
        /// Stores the manifest entries indexed by shader name.
        /// </summary>
        readonly Dictionary<string, ShaderManifestIndexEntry> entriesByName;

        /// <summary>
        /// Stores all manifest entries in insertion order.
        /// </summary>
        readonly ShaderManifestIndexEntry[] entries;

        /// <summary>
        /// Initializes a new manifest index.
        /// </summary>
        /// <param name="rootPath">Absolute shader root path.</param>
        /// <param name="moduleOutputPath">Absolute module output path.</param>
        /// <param name="entries">Indexed shader entries.</param>
        public ShaderManifestIndex(string rootPath, string moduleOutputPath, ShaderManifestIndexEntry[] entries) {
            if (string.IsNullOrWhiteSpace(rootPath)) {
                throw new ArgumentException("Root path must be provided.", nameof(rootPath));
            }

            if (string.IsNullOrWhiteSpace(moduleOutputPath)) {
                throw new ArgumentException("Module output path must be provided.", nameof(moduleOutputPath));
            }

            if (entries == null) {
                throw new ArgumentNullException(nameof(entries));
            }

            RootPath = rootPath;
            ModuleOutputPath = moduleOutputPath;
            this.entries = entries;

            entriesBySourcePath = new Dictionary<string, ShaderManifestIndexEntry>(StringComparer.OrdinalIgnoreCase);
            entriesByName = new Dictionary<string, ShaderManifestIndexEntry>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < entries.Length; i++) {
                ShaderManifestIndexEntry entry = entries[i];
                entriesBySourcePath[entry.SourcePath] = entry;
                entriesByName[entry.Name] = entry;
            }
        }

        /// <summary>
        /// Gets the absolute shader root path.
        /// </summary>
        public string RootPath { get; }

        /// <summary>
        /// Gets the absolute module output path.
        /// </summary>
        public string ModuleOutputPath { get; }

        /// <summary>
        /// Gets the shader entries loaded from the manifest.
        /// </summary>
        public IReadOnlyList<ShaderManifestIndexEntry> Entries {
            get {
                return entries;
            }
        }

        /// <summary>
        /// Attempts to locate a manifest entry by its absolute source path.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the shader source file.</param>
        /// <param name="entry">Matching entry when found.</param>
        /// <returns>True when an entry is found.</returns>
        public bool TryGetBySourcePath(string sourcePath, out ShaderManifestIndexEntry entry) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            return entriesBySourcePath.TryGetValue(sourcePath, out entry);
        }

        /// <summary>
        /// Attempts to locate a manifest entry by shader name.
        /// </summary>
        /// <param name="shaderName">Shader name to locate.</param>
        /// <param name="entry">Matching entry when found.</param>
        /// <returns>True when an entry is found.</returns>
        public bool TryGetByName(string shaderName, out ShaderManifestIndexEntry entry) {
            if (string.IsNullOrWhiteSpace(shaderName)) {
                throw new ArgumentException("Shader name must be provided.", nameof(shaderName));
            }

            return entriesByName.TryGetValue(shaderName, out entry);
        }

        /// <summary>
        /// Loads and indexes a shader manifest from the specified path.
        /// </summary>
        /// <param name="manifestPath">Absolute path to the manifest JSON.</param>
        /// <returns>Constructed manifest index.</returns>
        public static ShaderManifestIndex Load(string manifestPath) {
            if (string.IsNullOrWhiteSpace(manifestPath)) {
                throw new ArgumentException("Manifest path must be provided.", nameof(manifestPath));
            }

            if (!File.Exists(manifestPath)) {
                throw new FileNotFoundException("Shader manifest was not found.", manifestPath);
            }

            string json = File.ReadAllText(manifestPath);
            JsonDocument document = JsonDocument.Parse(json);
            using (document) {
                JsonElement rootElement = document.RootElement;
                string manifestDirectory = Path.GetDirectoryName(manifestPath);
                if (string.IsNullOrWhiteSpace(manifestDirectory)) {
                    throw new InvalidOperationException("Manifest directory could not be resolved.");
                }

                string rootPath = ResolveRootPath(manifestDirectory, rootElement);
                if (!Directory.Exists(rootPath)) {
                    throw new DirectoryNotFoundException($"Shader root path was not found: {rootPath}");
                }
                string moduleOutputPath = ResolveModuleOutputPath(manifestDirectory, rootElement, rootPath);
                ShaderManifestIndexEntry[] entries = ReadEntries(rootElement, rootPath, moduleOutputPath);
                return new ShaderManifestIndex(rootPath, moduleOutputPath, entries);
            }
        }

        /// <summary>
        /// Resolves the manifest root path from JSON content.
        /// </summary>
        /// <param name="manifestDirectory">Directory that contains the manifest.</param>
        /// <param name="rootElement">Manifest JSON root element.</param>
        /// <returns>Absolute root path.</returns>
        static string ResolveRootPath(string manifestDirectory, JsonElement rootElement) {
            string rootValue = ReadRequiredString(rootElement, "root");
            if (Path.IsPathRooted(rootValue)) {
                return rootValue;
            }

            return Path.GetFullPath(Path.Combine(manifestDirectory, rootValue));
        }

        /// <summary>
        /// Resolves the module output path from JSON content.
        /// </summary>
        /// <param name="manifestDirectory">Directory that contains the manifest.</param>
        /// <param name="rootElement">Manifest JSON root element.</param>
        /// <param name="rootPath">Resolved shader root path.</param>
        /// <returns>Absolute module output path.</returns>
        static string ResolveModuleOutputPath(string manifestDirectory, JsonElement rootElement, string rootPath) {
            JsonElement outputElement = ReadRequiredObject(rootElement, "output");
            string moduleDir = ReadRequiredString(outputElement, "moduleDir");
            if (Path.IsPathRooted(moduleDir)) {
                return moduleDir;
            }

            if (moduleDir.StartsWith("..", StringComparison.Ordinal)) {
                return Path.GetFullPath(Path.Combine(manifestDirectory, moduleDir));
            }

            return Path.GetFullPath(Path.Combine(rootPath, moduleDir));
        }

        /// <summary>
        /// Reads shader entries from the manifest.
        /// </summary>
        /// <param name="rootElement">Manifest JSON root element.</param>
        /// <param name="rootPath">Resolved shader root path.</param>
        /// <param name="moduleOutputPath">Resolved module output path.</param>
        /// <returns>Array of manifest entries.</returns>
        static ShaderManifestIndexEntry[] ReadEntries(JsonElement rootElement, string rootPath, string moduleOutputPath) {
            JsonElement shadersElement = ReadRequiredArray(rootElement, "shaders");
            List<ShaderManifestIndexEntry> entries = new List<ShaderManifestIndexEntry>();
            foreach (JsonElement shaderElement in shadersElement.EnumerateArray()) {
                string name = ReadRequiredString(shaderElement, "name");
                string file = ReadRequiredString(shaderElement, "file");

                string sourcePath = Path.GetFullPath(Path.Combine(rootPath, file));
                string moduleAssemblyPath = Path.Combine(moduleOutputPath, $"{name}.shader.dll");
                entries.Add(new ShaderManifestIndexEntry(name, file, sourcePath, moduleAssemblyPath));
            }

            if (entries.Count == 0) {
                throw new InvalidOperationException("Manifest did not define any shader entries.");
            }

            return entries.ToArray();
        }

        /// <summary>
        /// Reads a required string property from a JSON object.
        /// </summary>
        /// <param name="element">JSON object element.</param>
        /// <param name="propertyName">Property name to read.</param>
        /// <returns>Property value.</returns>
        static string ReadRequiredString(JsonElement element, string propertyName) {
            JsonElement property = ReadRequiredProperty(element, propertyName);
            string value = property.GetString();
            if (string.IsNullOrWhiteSpace(value)) {
                throw new InvalidOperationException($"Manifest property '{propertyName}' must be a non-empty string.");
            }

            return value;
        }

        /// <summary>
        /// Reads a required object property from a JSON object.
        /// </summary>
        /// <param name="element">JSON object element.</param>
        /// <param name="propertyName">Property name to read.</param>
        /// <returns>Property value.</returns>
        static JsonElement ReadRequiredObject(JsonElement element, string propertyName) {
            JsonElement property = ReadRequiredProperty(element, propertyName);
            if (property.ValueKind != JsonValueKind.Object) {
                throw new InvalidOperationException($"Manifest property '{propertyName}' must be an object.");
            }

            return property;
        }

        /// <summary>
        /// Reads a required array property from a JSON object.
        /// </summary>
        /// <param name="element">JSON object element.</param>
        /// <param name="propertyName">Property name to read.</param>
        /// <returns>Property value.</returns>
        static JsonElement ReadRequiredArray(JsonElement element, string propertyName) {
            JsonElement property = ReadRequiredProperty(element, propertyName);
            if (property.ValueKind != JsonValueKind.Array) {
                throw new InvalidOperationException($"Manifest property '{propertyName}' must be an array.");
            }

            return property;
        }

        /// <summary>
        /// Reads a required property from a JSON object.
        /// </summary>
        /// <param name="element">JSON object element.</param>
        /// <param name="propertyName">Property name to read.</param>
        /// <returns>Property value.</returns>
        static JsonElement ReadRequiredProperty(JsonElement element, string propertyName) {
            if (!element.TryGetProperty(propertyName, out JsonElement property)) {
                throw new InvalidOperationException($"Manifest property '{propertyName}' is required.");
            }

            return property;
        }
    }
}
