namespace helengine {
    /// <summary>
    /// Represents a target-specific precompiled shader package.
    /// </summary>
    public class ShaderModulePackage {
        /// <summary>
        /// Stores the target name for binary lookup.
        /// </summary>
        readonly string targetName;

        /// <summary>
        /// Initializes a new shader module package.
        /// </summary>
        /// <param name="target">Compilation target for this package.</param>
        /// <param name="rootPath">Root directory containing the package data.</param>
        /// <param name="definition">Shader module definition describing contents.</param>
        public ShaderModulePackage(
            ShaderCompileTarget target,
            string rootPath,
            ShaderModuleDefinition definition) {
            if (string.IsNullOrWhiteSpace(rootPath)) {
                throw new ArgumentException("Package root path must be provided.", nameof(rootPath));
            }

            if (definition == null) {
                throw new ArgumentNullException(nameof(definition));
            }

            Target = target;
            RootPath = rootPath;
            Definition = definition;
            targetName = ShaderTargetNames.GetTargetName(target);
        }

        /// <summary>
        /// Gets the compilation target for this package.
        /// </summary>
        public ShaderCompileTarget Target { get; }

        /// <summary>
        /// Gets the root directory containing the package data.
        /// </summary>
        public string RootPath { get; }

        /// <summary>
        /// Gets the shader module definition describing the package.
        /// </summary>
        public ShaderModuleDefinition Definition { get; }

        /// <summary>
        /// Locates the compiled binary for the requested program and variant.
        /// </summary>
        /// <param name="programName">Program name to locate.</param>
        /// <param name="variant">Variant name to locate.</param>
        /// <returns>Matching binary descriptor.</returns>
        public ShaderProgramBinary GetBinary(string programName, string variant) {
            return Definition.GetBinary(programName, targetName, variant);
        }

        /// <summary>
        /// Resolves the absolute path to a compiled binary for the requested program and variant.
        /// </summary>
        /// <param name="programName">Program name to locate.</param>
        /// <param name="variant">Variant name to locate.</param>
        /// <returns>Absolute binary path.</returns>
        public string GetBinaryPath(string programName, string variant) {
            ShaderProgramBinary binary = GetBinary(programName, variant);
            if (string.IsNullOrWhiteSpace(binary.Path)) {
                throw new InvalidOperationException("The shader binary does not include a file path.");
            }

            if (Path.IsPathRooted(binary.Path)) {
                return binary.Path;
            }

            return Path.Combine(RootPath, binary.Path);
        }

        /// <summary>
        /// Resolves the compiled bytecode for the requested program and variant.
        /// </summary>
        /// <param name="programName">Program name to locate.</param>
        /// <param name="variant">Variant name to locate.</param>
        /// <returns>Compiled shader bytecode.</returns>
        public byte[] GetBinaryData(string programName, string variant) {
            ShaderProgramBinary binary = GetBinary(programName, variant);
            if (binary.Bytecode != null && binary.Bytecode.Length > 0) {
                return binary.Bytecode;
            }

            string path = GetBinaryPath(programName, variant);
            ContentManager contentManager = new ContentManager(RootPath);
            RawByteContent binaryContent = contentManager.Load<RawByteContent>(path);
            return binaryContent.Bytes;
        }
    }
}
