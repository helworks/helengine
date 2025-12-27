using helengine;

namespace helshader {
    /// <summary>
    /// Describes a single shader compilation job.
    /// </summary>
    public class ShaderCompileItem {
        /// <summary>
        /// Initializes a new shader compile item.
        /// </summary>
        /// <param name="shaderName">Shader module name.</param>
        /// <param name="sourcePath">Absolute source path.</param>
        /// <param name="outputPath">Absolute output path.</param>
        /// <param name="entryPoint">Entry point function name.</param>
        /// <param name="profile">Target shader profile.</param>
        /// <param name="stage">Shader stage.</param>
        /// <param name="target">Target backend.</param>
        /// <param name="variant">Variant name.</param>
        /// <param name="defines">Preprocessor defines.</param>
        /// <param name="includeDirs">Include search directories.</param>
        public ShaderCompileItem(
            string shaderName,
            string sourcePath,
            string outputPath,
            string entryPoint,
            string profile,
            ShaderStage stage,
            string target,
            string variant,
            string[] defines,
            string[] includeDirs) {
            if (string.IsNullOrWhiteSpace(shaderName)) {
                throw new ArgumentException("Shader name must be provided.", nameof(shaderName));
            }

            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            if (string.IsNullOrWhiteSpace(outputPath)) {
                throw new ArgumentException("Output path must be provided.", nameof(outputPath));
            }

            if (string.IsNullOrWhiteSpace(entryPoint)) {
                throw new ArgumentException("Entry point must be provided.", nameof(entryPoint));
            }

            if (string.IsNullOrWhiteSpace(profile)) {
                throw new ArgumentException("Profile must be provided.", nameof(profile));
            }

            if (string.IsNullOrWhiteSpace(target)) {
                throw new ArgumentException("Target must be provided.", nameof(target));
            }

            if (string.IsNullOrWhiteSpace(variant)) {
                throw new ArgumentException("Variant must be provided.", nameof(variant));
            }

            if (defines == null) {
                throw new ArgumentNullException(nameof(defines));
            }

            if (includeDirs == null) {
                throw new ArgumentNullException(nameof(includeDirs));
            }

            ShaderName = shaderName;
            SourcePath = sourcePath;
            OutputPath = outputPath;
            EntryPoint = entryPoint;
            Profile = profile;
            Stage = stage;
            Target = target;
            Variant = variant;
            Defines = defines;
            IncludeDirs = includeDirs;
        }

        /// <summary>
        /// Gets the shader module name.
        /// </summary>
        public string ShaderName { get; }

        /// <summary>
        /// Gets the absolute shader source path.
        /// </summary>
        public string SourcePath { get; }

        /// <summary>
        /// Gets the absolute output path.
        /// </summary>
        public string OutputPath { get; }

        /// <summary>
        /// Gets the entry point name.
        /// </summary>
        public string EntryPoint { get; }

        /// <summary>
        /// Gets the shader profile name.
        /// </summary>
        public string Profile { get; }

        /// <summary>
        /// Gets the shader stage.
        /// </summary>
        public ShaderStage Stage { get; }

        /// <summary>
        /// Gets the target backend.
        /// </summary>
        public string Target { get; }

        /// <summary>
        /// Gets the variant name.
        /// </summary>
        public string Variant { get; }

        /// <summary>
        /// Gets the preprocessor defines applied during compilation.
        /// </summary>
        public string[] Defines { get; }

        /// <summary>
        /// Gets the include search directories.
        /// </summary>
        public string[] IncludeDirs { get; }
    }
}
