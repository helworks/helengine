namespace helengine {
    /// <summary>
    /// Groups shader programs and binaries for a single shader module.
    /// </summary>
    public class ShaderModuleDefinition {
        /// <summary>
        /// Stores the shader program definitions for this module.
        /// </summary>
        readonly ShaderProgramDefinition[] programs;

        /// <summary>
        /// Stores the compiled binaries for this module.
        /// </summary>
        readonly ShaderProgramBinary[] binaries;

        /// <summary>
        /// Initializes a new shader module definition.
        /// </summary>
        /// <param name="name">Module name.</param>
        /// <param name="programs">Shader program definitions.</param>
        /// <param name="binaries">Compiled binaries for all targets and variants.</param>
        public ShaderModuleDefinition(
            string name,
            ShaderProgramDefinition[] programs,
            ShaderProgramBinary[] binaries) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Module name must be provided.", nameof(name));
            }

            if (programs == null) {
                throw new ArgumentNullException(nameof(programs));
            }

            if (programs.Length == 0) {
                throw new ArgumentException("At least one program definition is required.", nameof(programs));
            }

            if (binaries == null) {
                throw new ArgumentNullException(nameof(binaries));
            }

            Name = name;
            this.programs = programs;
            this.binaries = binaries;
        }

        /// <summary>
        /// Gets the module name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the shader program definitions.
        /// </summary>
        public IReadOnlyList<ShaderProgramDefinition> Programs {
            get {
                return programs;
            }
        }

        /// <summary>
        /// Gets the compiled binaries for the module.
        /// </summary>
        public IReadOnlyList<ShaderProgramBinary> Binaries {
            get {
                return binaries;
            }
        }

        /// <summary>
        /// Returns the program definition matching the requested name.
        /// </summary>
        /// <param name="programName">Program name to locate.</param>
        /// <returns>Matching program definition.</returns>
        public ShaderProgramDefinition GetProgram(string programName) {
            if (string.IsNullOrWhiteSpace(programName)) {
                throw new ArgumentException("Program name must be provided.", nameof(programName));
            }

            for (int i = 0; i < programs.Length; i++) {
                ShaderProgramDefinition program = programs[i];
                if (string.Equals(program.Name, programName, StringComparison.Ordinal)) {
                    return program;
                }
            }

            throw new InvalidOperationException("No shader program was found for the requested name.");
        }

        /// <summary>
        /// Attempts to locate a program definition by name.
        /// </summary>
        /// <param name="programName">Program name to locate.</param>
        /// <param name="program">Matching program definition when found.</param>
        /// <returns>True when the program is found.</returns>
        public bool TryGetProgram(string programName, out ShaderProgramDefinition program) {
            if (string.IsNullOrWhiteSpace(programName)) {
                throw new ArgumentException("Program name must be provided.", nameof(programName));
            }

            for (int i = 0; i < programs.Length; i++) {
                ShaderProgramDefinition candidate = programs[i];
                if (string.Equals(candidate.Name, programName, StringComparison.Ordinal)) {
                    program = candidate;
                    return true;
                }
            }

            program = null;
            return false;
        }

        /// <summary>
        /// Returns the compiled binary for the requested program, target, and variant.
        /// </summary>
        /// <param name="programName">Program name to locate.</param>
        /// <param name="target">Target backend identifier.</param>
        /// <param name="variant">Variant name.</param>
        /// <returns>Matching binary descriptor.</returns>
        public ShaderProgramBinary GetBinary(string programName, string target, string variant) {
            ShaderProgramBinary binary;
            if (TryGetBinary(programName, target, variant, out binary)) {
                return binary;
            }

            throw new InvalidOperationException("No compiled shader binary was found for the requested selection.");
        }

        /// <summary>
        /// Attempts to locate a compiled binary for the requested program, target, and variant.
        /// </summary>
        /// <param name="programName">Program name to locate.</param>
        /// <param name="target">Target backend identifier.</param>
        /// <param name="variant">Variant name.</param>
        /// <param name="binary">Matching binary descriptor when found.</param>
        /// <returns>True when a matching binary is found.</returns>
        public bool TryGetBinary(string programName, string target, string variant, out ShaderProgramBinary binary) {
            if (string.IsNullOrWhiteSpace(programName)) {
                throw new ArgumentException("Program name must be provided.", nameof(programName));
            }

            if (string.IsNullOrWhiteSpace(target)) {
                throw new ArgumentException("Target must be provided.", nameof(target));
            }

            if (string.IsNullOrWhiteSpace(variant)) {
                throw new ArgumentException("Variant must be provided.", nameof(variant));
            }

            for (int i = 0; i < binaries.Length; i++) {
                ShaderProgramBinary candidate = binaries[i];
                if (!string.Equals(candidate.ProgramName, programName, StringComparison.Ordinal)) {
                    continue;
                }

                if (!string.Equals(candidate.Target, target, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                if (string.Equals(candidate.Variant, variant, StringComparison.Ordinal)) {
                    binary = candidate;
                    return true;
                }
            }

            binary = null;
            return false;
        }
    }
}
