namespace helshader {
    /// <summary>
    /// Validates shader manifest data for required fields and consistency.
    /// </summary>
    public class ShaderManifestValidator {
        /// <summary>
        /// Validates the supplied shader manifest.
        /// </summary>
        /// <param name="manifest">Manifest to validate.</param>
        public void Validate(ShaderManifest manifest) {
            if (manifest == null) {
                throw new ArgumentNullException(nameof(manifest));
            }

            EnsureString(manifest.Root, "root");
            EnsureOutput(manifest.Output);
            EnsureTargets(manifest.Targets);
            EnsureProfiles(manifest.Profiles, manifest.Targets);
            EnsureTools(manifest.Tools, manifest.Targets);
            EnsureShaders(manifest.Shaders);
        }

        /// <summary>
        /// Ensures the output configuration is valid.
        /// </summary>
        /// <param name="output">Output configuration.</param>
        void EnsureOutput(ShaderManifestOutput output) {
            if (output == null) {
                throw new InvalidOperationException("Manifest output configuration is required.");
            }

            EnsureString(output.BinaryDir, "output.binaryDir");
            EnsureString(output.ReflectionDir, "output.reflectionDir");
            EnsureString(output.CodegenDir, "output.codegenDir");
            EnsureString(output.ModuleDir, "output.moduleDir");
            EnsureString(output.MslDir, "output.mslDir");
            EnsureString(output.DebugDir, "output.debugDir");
        }

        /// <summary>
        /// Ensures the target list is valid.
        /// </summary>
        /// <param name="targets">Target list.</param>
        void EnsureTargets(string[] targets) {
            if (targets == null || targets.Length == 0) {
                throw new InvalidOperationException("Manifest must declare at least one target.");
            }
        }

        /// <summary>
        /// Ensures the profile map is valid.
        /// </summary>
        /// <param name="profiles">Profile map.</param>
        void EnsureProfiles(Dictionary<string, ShaderManifestProfile> profiles, string[] targets) {
            if (profiles == null || profiles.Count == 0) {
                throw new InvalidOperationException("Manifest profiles are required.");
            }

            if (targets == null || targets.Length == 0) {
                throw new InvalidOperationException("Manifest targets are required to validate profiles.");
            }

            for (int i = 0; i < targets.Length; i++) {
                string target = targets[i];
                if (!profiles.ContainsKey(target)) {
                    throw new InvalidOperationException($"Profile mapping for target '{target}' is missing.");
                }
            }
        }

        /// <summary>
        /// Ensures required tool paths are present for the configured targets.
        /// </summary>
        /// <param name="tools">Tool path configuration.</param>
        /// <param name="targets">Target list.</param>
        void EnsureTools(ShaderManifestTools tools, string[] targets) {
            if (tools == null) {
                throw new InvalidOperationException("Manifest tool paths are required.");
            }

            bool needsFxc = false;
            bool needsDxc = false;
            bool needsSpirvCross = false;

            for (int i = 0; i < targets.Length; i++) {
                string target = targets[i];
                if (string.Equals(target, "dx9", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(target, "dx11", StringComparison.OrdinalIgnoreCase)) {
                    needsFxc = true;
                } else if (string.Equals(target, "dx12", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(target, "vulkan", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(target, "metal", StringComparison.OrdinalIgnoreCase)) {
                    needsDxc = true;
                }

                if (string.Equals(target, "metal", StringComparison.OrdinalIgnoreCase)) {
                    needsSpirvCross = true;
                }
            }

            if (needsFxc) {
                EnsureString(tools.Fxc, "tools.fxc");
            }

            if (needsDxc) {
                EnsureString(tools.Dxc, "tools.dxc");
            }

            if (needsSpirvCross) {
                EnsureString(tools.SpirvCross, "tools.spirvCross");
            }
        }

        /// <summary>
        /// Ensures shader entries are valid.
        /// </summary>
        /// <param name="shaders">Shader entry list.</param>
        void EnsureShaders(ShaderManifestShader[] shaders) {
            if (shaders == null || shaders.Length == 0) {
                throw new InvalidOperationException("Manifest must include at least one shader entry.");
            }

            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < shaders.Length; i++) {
                ShaderManifestShader shader = shaders[i];
                if (shader == null) {
                    throw new InvalidOperationException("Shader entry cannot be null.");
                }

                EnsureString(shader.Name, "shaders.name");
                EnsureString(shader.File, "shaders.file");
                EnsureEntries(shader.Entries, shader.Name);
                EnsureVariants(shader.Variants, shader.Name);

                if (!names.Add(shader.Name)) {
                    throw new InvalidOperationException($"Duplicate shader name '{shader.Name}' in manifest.");
                }
            }
        }

        /// <summary>
        /// Ensures entry points are defined.
        /// </summary>
        /// <param name="entries">Entry point list.</param>
        /// <param name="shaderName">Parent shader name.</param>
        void EnsureEntries(ShaderManifestEntryPoint[] entries, string shaderName) {
            if (entries == null || entries.Length == 0) {
                throw new InvalidOperationException($"Shader '{shaderName}' must define at least one entry point.");
            }

            for (int i = 0; i < entries.Length; i++) {
                ShaderManifestEntryPoint entry = entries[i];
                if (entry == null) {
                    throw new InvalidOperationException($"Shader '{shaderName}' entry cannot be null.");
                }

                EnsureString(entry.Stage, $"shaders[{shaderName}].entries.stage");
                EnsureString(entry.Entry, $"shaders[{shaderName}].entries.entry");
            }
        }

        /// <summary>
        /// Ensures variant definitions are present.
        /// </summary>
        /// <param name="variants">Variant list.</param>
        /// <param name="shaderName">Parent shader name.</param>
        void EnsureVariants(ShaderManifestVariant[] variants, string shaderName) {
            if (variants == null || variants.Length == 0) {
                throw new InvalidOperationException($"Shader '{shaderName}' must define at least one variant.");
            }

            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < variants.Length; i++) {
                ShaderManifestVariant variant = variants[i];
                if (variant == null) {
                    throw new InvalidOperationException($"Shader '{shaderName}' variant cannot be null.");
                }

                EnsureString(variant.Name, $"shaders[{shaderName}].variants.name");
                if (variant.Defines == null) {
                    throw new InvalidOperationException($"Shader '{shaderName}' variant '{variant.Name}' must define a defines array.");
                }

                if (!names.Add(variant.Name)) {
                    throw new InvalidOperationException($"Shader '{shaderName}' contains duplicate variant '{variant.Name}'.");
                }
            }
        }

        /// <summary>
        /// Ensures a string property is non-empty.
        /// </summary>
        /// <param name="value">Value to validate.</param>
        /// <param name="name">Property name for error messages.</param>
        void EnsureString(string value, string name) {
            if (string.IsNullOrWhiteSpace(value)) {
                throw new InvalidOperationException($"Manifest property '{name}' is required.");
            }
        }
    }
}
