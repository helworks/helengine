namespace helengine.editor {
    /// <summary>
    /// Provides helper methods for shader package path construction.
    /// </summary>
    public static class ShaderPackagePaths {
        /// <summary>
        /// Package file extension used for serialized shader packages.
        /// </summary>
        public const string PackageExtension = ".shader.asset";

        /// <summary>
        /// Builds the package path for a shader and target.
        /// </summary>
        /// <param name="outputDirectory">Directory where packages are written.</param>
        /// <param name="shaderName">Logical shader name.</param>
        /// <param name="target">Target backend to include in the file name.</param>
        /// <returns>Absolute path to the package file.</returns>
        public static string GetPackagePath(string outputDirectory, string shaderName, ShaderCompileTarget target) {
            if (string.IsNullOrWhiteSpace(outputDirectory)) {
                throw new ArgumentException("Output directory must be provided.", nameof(outputDirectory));
            }

            if (string.IsNullOrWhiteSpace(shaderName)) {
                throw new ArgumentException("Shader name must be provided.", nameof(shaderName));
            }

            string targetName = ShaderTargetNames.GetTargetName(target);
            string fileName = string.Concat(shaderName, ".", targetName, PackageExtension);
            return Path.GetFullPath(Path.Combine(outputDirectory, fileName));
        }
    }
}
