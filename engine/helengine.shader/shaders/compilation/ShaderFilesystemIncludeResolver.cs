namespace helengine {
    /// <summary>
    /// Resolves shader includes from the filesystem using the requesting file directory.
    /// </summary>
    public class ShaderFilesystemIncludeResolver : IShaderIncludeResolver {
        /// <summary>
        /// Stores the root directory used to resolve absolute include paths.
        /// </summary>
        readonly string rootDirectory;
        /// <summary>
        /// Content manager used to read include source text.
        /// </summary>
        readonly ContentManager IncludeContentManager;

        /// <summary>
        /// Initializes a new filesystem include resolver.
        /// </summary>
        /// <param name="rootDirectory">Root directory used to resolve include paths.</param>
        public ShaderFilesystemIncludeResolver(string rootDirectory) {
            if (string.IsNullOrWhiteSpace(rootDirectory)) {
                throw new ArgumentException("Root directory must be provided.", nameof(rootDirectory));
            }

            if (!Directory.Exists(rootDirectory)) {
                throw new DirectoryNotFoundException("Include root directory does not exist.");
            }

            this.rootDirectory = rootDirectory;
            IncludeContentManager = new ContentManager(new HostFileSystemContentStreamSource(rootDirectory));
            TextContentManagerConfiguration.Configure(IncludeContentManager);
        }

        /// <summary>
        /// Resolves an include path from the filesystem.
        /// </summary>
        /// <param name="requestingFile">Path of the file that requested the include.</param>
        /// <param name="includePath">Include path as written in the shader source.</param>
        /// <returns>Resolved include contents.</returns>
        public ShaderIncludeResult Resolve(string requestingFile, string includePath) {
            if (string.IsNullOrWhiteSpace(requestingFile)) {
                throw new ArgumentException("Requesting file must be provided.", nameof(requestingFile));
            }

            if (string.IsNullOrWhiteSpace(includePath)) {
                throw new ArgumentException("Include path must be provided.", nameof(includePath));
            }

            string resolvedPath = ResolvePath(requestingFile, includePath);
            TextContent sourceContent = IncludeContentManager.Load<TextContent>(resolvedPath);
            return new ShaderIncludeResult(resolvedPath, sourceContent.Text);
        }

        /// <summary>
        /// Resolves the include file path using the requesting file and root directory.
        /// </summary>
        /// <param name="requestingFile">Path of the file that requested the include.</param>
        /// <param name="includePath">Include path as written in the shader source.</param>
        /// <returns>Resolved absolute path.</returns>
        string ResolvePath(string requestingFile, string includePath) {
            if (Path.IsPathRooted(includePath)) {
                if (!File.Exists(includePath)) {
                    throw new FileNotFoundException("Include file does not exist.", includePath);
                }

                return includePath;
            }

            string requestingDirectory = Path.GetDirectoryName(requestingFile);
            if (string.IsNullOrWhiteSpace(requestingDirectory)) {
                throw new InvalidOperationException("Requesting file path does not include a directory.");
            }

            string candidate = Path.Combine(requestingDirectory, includePath);
            if (File.Exists(candidate)) {
                return candidate;
            }

            string rootedCandidate = Path.Combine(rootDirectory, includePath);
            if (File.Exists(rootedCandidate)) {
                return rootedCandidate;
            }

            throw new FileNotFoundException("Include file does not exist.", includePath);
        }
    }
}
