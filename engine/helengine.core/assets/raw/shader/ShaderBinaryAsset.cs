namespace helengine {
    /// <summary>
    /// Represents serialized data for a compiled shader binary.
    /// </summary>
    public class ShaderBinaryAsset {
        /// <summary>
        /// Name of the shader program this binary represents.
        /// </summary>
        public string ProgramName;

        /// <summary>
        /// Pipeline stage for the compiled binary.
        /// </summary>
        public ShaderStage Stage;

        /// <summary>
        /// Target backend name associated with the binary.
        /// </summary>
        public string TargetName;

        /// <summary>
        /// Variant name that produced the binary.
        /// </summary>
        public string Variant;

        /// <summary>
        /// Compiled shader bytecode payload.
        /// </summary>
        public byte[] Bytecode;

        /// <summary>
        /// Builds a runtime shader binary definition from serialized data.
        /// </summary>
        /// <returns>Shader binary definition.</returns>
        public ShaderProgramBinary ToBinary() {
            Validate();
            return new ShaderProgramBinary(ProgramName, Stage, TargetName, Variant, Bytecode);
        }

        /// <summary>
        /// Creates a serialized shader binary asset from a runtime definition.
        /// </summary>
        /// <param name="binary">Shader binary definition to convert.</param>
        /// <returns>Serialized shader binary asset.</returns>
        public static ShaderBinaryAsset FromBinary(ShaderProgramBinary binary) {
            if (binary == null) {
                throw new ArgumentNullException(nameof(binary));
            }

            if (binary.Bytecode == null || binary.Bytecode.Length == 0) {
                throw new InvalidOperationException("Shader binary must include embedded bytecode for packaging.");
            }

            ShaderBinaryAsset asset = new ShaderBinaryAsset {
                ProgramName = binary.ProgramName,
                Stage = binary.Stage,
                TargetName = binary.Target,
                Variant = binary.Variant,
                Bytecode = binary.Bytecode
            };

            return asset;
        }

        /// <summary>
        /// Validates binary data before conversion.
        /// </summary>
        void Validate() {
            if (string.IsNullOrWhiteSpace(ProgramName)) {
                throw new InvalidOperationException("Binary program name must be provided.");
            } else if (string.IsNullOrWhiteSpace(TargetName)) {
                throw new InvalidOperationException("Binary target name must be provided.");
            } else if (string.IsNullOrWhiteSpace(Variant)) {
                throw new InvalidOperationException("Binary variant name must be provided.");
            } else if (Bytecode == null || Bytecode.Length == 0) {
                throw new InvalidOperationException("Binary bytecode payload must be provided.");
            }
        }
    }
}
