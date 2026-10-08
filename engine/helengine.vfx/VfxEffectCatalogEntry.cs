namespace helengine.vfx {
    /// <summary>
    /// Pairs one validated effect definition with the directory its pass shader paths are resolved against: the engine
    /// base directory for built-in effects, or a project's <c>assets</c> directory for project effects.
    /// </summary>
    public sealed class VfxEffectCatalogEntry {
        /// <summary>
        /// Gets the validated effect definition.
        /// </summary>
        public EffectAsset Effect { get; }

        /// <summary>
        /// Gets the absolute directory pass shader paths are relative to.
        /// </summary>
        public string ShaderRoot { get; }

        /// <summary>
        /// Gets whether the effect came from a user project rather than the engine.
        /// </summary>
        public bool IsProjectEffect { get; }

        /// <summary>
        /// Initializes one catalog entry.
        /// </summary>
        /// <param name="effect">Validated effect definition.</param>
        /// <param name="shaderRoot">Absolute directory pass shader paths are relative to.</param>
        /// <param name="isProjectEffect">Whether the effect came from a user project.</param>
        public VfxEffectCatalogEntry(EffectAsset effect, string shaderRoot, bool isProjectEffect) {
            if (effect == null) {
                throw new ArgumentNullException(nameof(effect));
            }
            if (string.IsNullOrWhiteSpace(shaderRoot)) {
                throw new ArgumentException("Shader root must be provided.", nameof(shaderRoot));
            }
            Effect = effect;
            ShaderRoot = Path.GetFullPath(shaderRoot);
            IsProjectEffect = isProjectEffect;
        }

        /// <summary>
        /// Resolves a pass shader path to an absolute file path inside <see cref="ShaderRoot"/>.
        /// </summary>
        /// <param name="pass">Pass whose shader is resolved.</param>
        /// <returns>Absolute shader file path.</returns>
        public string ResolveShaderPath(EffectPassAsset pass) {
            string path = Path.GetFullPath(Path.Combine(ShaderRoot, pass.ShaderPath));
            string root = ShaderRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidDataException($"Effect '{Effect.EffectId}' shader '{pass.ShaderPath}' escapes its shader root.");
            }
            return path;
        }
    }
}
