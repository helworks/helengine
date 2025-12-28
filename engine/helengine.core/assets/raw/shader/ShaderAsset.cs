namespace helengine {
    /// <summary>
    /// Represents a serialized shader module package for a specific target backend.
    /// </summary>
    [ProtoBuf.ProtoContract]
    public class ShaderAsset : Asset {
        /// <summary>
        /// Friendly module name for the shader package.
        /// </summary>
        [ProtoBuf.ProtoMember(1)]
        public string Name;

        /// <summary>
        /// Target name associated with the shader package.
        /// </summary>
        [ProtoBuf.ProtoMember(2)]
        public string TargetName;

        /// <summary>
        /// Shader program definitions included in the package.
        /// </summary>
        [ProtoBuf.ProtoMember(3)]
        public ShaderProgramAsset[] Programs;

        /// <summary>
        /// Compiled shader binaries included in the package.
        /// </summary>
        [ProtoBuf.ProtoMember(4)]
        public ShaderBinaryAsset[] Binaries;

        /// <summary>
        /// Builds a shader module definition from the serialized asset data.
        /// </summary>
        /// <returns>Constructed shader module definition.</returns>
        public ShaderModuleDefinition BuildDefinition() {
            Validate();

            ShaderProgramDefinition[] programDefinitions = BuildProgramDefinitions();
            ShaderProgramBinary[] binaryDefinitions = BuildBinaryDefinitions();
            return new ShaderModuleDefinition(Name, programDefinitions, binaryDefinitions);
        }

        /// <summary>
        /// Creates a shader asset from a module definition and target filter.
        /// </summary>
        /// <param name="definition">Source module definition.</param>
        /// <param name="target">Target backend to include.</param>
        /// <returns>Serialized shader asset instance.</returns>
        public static ShaderAsset FromDefinition(ShaderModuleDefinition definition, ShaderCompileTarget target) {
            if (definition == null) {
                throw new ArgumentNullException(nameof(definition));
            }

            string targetName = ShaderTargetNames.GetTargetName(target);
            ShaderProgramAsset[] programs = BuildProgramAssets(definition);
            ShaderBinaryAsset[] binaries = BuildBinaryAssets(definition, targetName);

            ShaderAsset asset = new ShaderAsset {
                Id = definition.Name,
                Name = definition.Name,
                TargetName = targetName,
                Programs = programs,
                Binaries = binaries
            };

            return asset;
        }

        /// <summary>
        /// Validates the serialized asset data before conversion.
        /// </summary>
        void Validate() {
            if (string.IsNullOrWhiteSpace(Name)) {
                throw new InvalidOperationException("Shader asset name must be provided.");
            }

            if (string.IsNullOrWhiteSpace(TargetName)) {
                throw new InvalidOperationException("Shader asset target name must be provided.");
            }

            if (Programs == null) {
                throw new InvalidOperationException("Shader asset programs must be provided.");
            }

            if (Programs.Length == 0) {
                throw new InvalidOperationException("Shader asset must include at least one program.");
            }

            if (Binaries == null) {
                throw new InvalidOperationException("Shader asset binaries must be provided.");
            }

            if (Binaries.Length == 0) {
                throw new InvalidOperationException("Shader asset must include at least one binary.");
            }
        }

        /// <summary>
        /// Builds program definitions from the serialized program assets.
        /// </summary>
        /// <returns>Array of shader program definitions.</returns>
        ShaderProgramDefinition[] BuildProgramDefinitions() {
            ShaderProgramDefinition[] definitions = new ShaderProgramDefinition[Programs.Length];
            for (int i = 0; i < Programs.Length; i++) {
                ShaderProgramAsset program = Programs[i];
                if (program == null) {
                    throw new InvalidOperationException("Shader asset contains a null program entry.");
                }

                definitions[i] = program.ToDefinition();
            }

            return definitions;
        }

        /// <summary>
        /// Builds binary definitions from the serialized binary assets.
        /// </summary>
        /// <returns>Array of shader program binaries.</returns>
        ShaderProgramBinary[] BuildBinaryDefinitions() {
            ShaderProgramBinary[] binaries = new ShaderProgramBinary[Binaries.Length];
            for (int i = 0; i < Binaries.Length; i++) {
                ShaderBinaryAsset binary = Binaries[i];
                if (binary == null) {
                    throw new InvalidOperationException("Shader asset contains a null binary entry.");
                }

                binaries[i] = binary.ToBinary();
            }

            return binaries;
        }

        /// <summary>
        /// Builds shader program assets from a module definition.
        /// </summary>
        /// <param name="definition">Module definition to convert.</param>
        /// <returns>Array of program assets.</returns>
        static ShaderProgramAsset[] BuildProgramAssets(ShaderModuleDefinition definition) {
            IReadOnlyList<ShaderProgramDefinition> programs = definition.Programs;
            ShaderProgramAsset[] assets = new ShaderProgramAsset[programs.Count];
            for (int i = 0; i < programs.Count; i++) {
                ShaderProgramDefinition program = programs[i];
                assets[i] = ShaderProgramAsset.FromDefinition(program);
            }

            return assets;
        }

        /// <summary>
        /// Builds shader binary assets from a module definition and target filter.
        /// </summary>
        /// <param name="definition">Module definition to convert.</param>
        /// <param name="targetName">Target name to filter binaries.</param>
        /// <returns>Array of binary assets.</returns>
        static ShaderBinaryAsset[] BuildBinaryAssets(ShaderModuleDefinition definition, string targetName) {
            IReadOnlyList<ShaderProgramBinary> binaries = definition.Binaries;
            List<ShaderBinaryAsset> assets = new List<ShaderBinaryAsset>();
            for (int i = 0; i < binaries.Count; i++) {
                ShaderProgramBinary binary = binaries[i];
                if (!string.Equals(binary.Target, targetName, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                assets.Add(ShaderBinaryAsset.FromBinary(binary));
            }

            if (assets.Count == 0) {
                throw new InvalidOperationException("Shader module does not contain binaries for the requested target.");
            }

            return assets.ToArray();
        }
    }
}
