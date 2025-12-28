namespace helengine.editor {
    /// <summary>
    /// Represents a loaded shader package and its definition.
    /// </summary>
    public sealed class ShaderPackageHandle : IDisposable {
        /// <summary>
        /// Loaded shader module package.
        /// </summary>
        readonly ShaderModulePackage package;

        /// <summary>
        /// Absolute path to the package file.
        /// </summary>
        readonly string packagePath;

        /// <summary>
        /// Tracks whether the handle has been unloaded.
        /// </summary>
        bool isUnloaded;

        /// <summary>
        /// Initializes a new shader package handle.
        /// </summary>
        /// <param name="package">Loaded module package.</param>
        /// <param name="packagePath">Absolute path to the package file.</param>
        public ShaderPackageHandle(ShaderModulePackage package, string packagePath) {
            if (package == null) {
                throw new ArgumentNullException(nameof(package));
            }

            if (string.IsNullOrWhiteSpace(packagePath)) {
                throw new ArgumentException("Package path must be provided.", nameof(packagePath));
            }

            this.package = package;
            this.packagePath = packagePath;
        }

        /// <summary>
        /// Gets the loaded shader module definition.
        /// </summary>
        public ShaderModuleDefinition Definition {
            get {
                return package.Definition;
            }
        }

        /// <summary>
        /// Gets the loaded shader module package.
        /// </summary>
        public ShaderModulePackage Package {
            get {
                return package;
            }
        }

        /// <summary>
        /// Gets the absolute path to the package file.
        /// </summary>
        public string PackagePath {
            get {
                return packagePath;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the handle has been unloaded.
        /// </summary>
        public bool IsUnloaded {
            get {
                return isUnloaded;
            }
        }

        /// <summary>
        /// Unloads the package handle.
        /// </summary>
        public void Unload() {
            if (isUnloaded) {
                return;
            }

            isUnloaded = true;
        }

        /// <summary>
        /// Releases resources associated with the package handle.
        /// </summary>
        public void Dispose() {
            Unload();
        }
    }
}
