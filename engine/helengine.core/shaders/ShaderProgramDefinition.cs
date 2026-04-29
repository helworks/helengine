namespace helengine {
    /// <summary>
    /// Describes a single shader entry point and its binding layout metadata.
    /// </summary>
    public class ShaderProgramDefinition {
        /// <summary>
        /// Stores the binding list for the shader program.
        /// </summary>
        readonly ShaderBinding[] bindings;

        /// <summary>
        /// Stores the input signature elements for the shader program.
        /// </summary>
        readonly ShaderVertexElement[] inputs;

        /// <summary>
        /// Stores the output signature elements for the shader program.
        /// </summary>
        readonly ShaderVertexElement[] outputs;

        /// <summary>
        /// Stores the variant definitions for the shader program.
        /// </summary>
        readonly ShaderVariant[] variants;

        /// <summary>
        /// Initializes a new shader program definition.
        /// </summary>
        /// <param name="name">Friendly program name.</param>
        /// <param name="stage">Pipeline stage for the entry point.</param>
        /// <param name="entryPoint">Entry point function name.</param>
        /// <param name="bindings">Resource bindings used by the program.</param>
        /// <param name="inputs">Input signature elements.</param>
        /// <param name="outputs">Output signature elements.</param>
        /// <param name="variants">Compile-time variants for the program.</param>
        public ShaderProgramDefinition(
            string name,
            ShaderStage stage,
            string entryPoint,
            ShaderBinding[] bindings,
            ShaderVertexElement[] inputs,
            ShaderVertexElement[] outputs,
            ShaderVariant[] variants) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Program name must be provided.", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(entryPoint)) {
                throw new ArgumentException("Entry point must be provided.", nameof(entryPoint));
            }

            if (bindings == null) {
                throw new ArgumentNullException(nameof(bindings));
            }

            if (inputs == null) {
                throw new ArgumentNullException(nameof(inputs));
            }

            if (outputs == null) {
                throw new ArgumentNullException(nameof(outputs));
            }

            if (variants == null) {
                throw new ArgumentNullException(nameof(variants));
            }

            Name = name;
            Stage = stage;
            EntryPoint = entryPoint;
            this.bindings = bindings;
            this.inputs = inputs;
            this.outputs = outputs;
            this.variants = variants;
        }

        /// <summary>
        /// Gets the program name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the pipeline stage for this program.
        /// </summary>
        public ShaderStage Stage { get; }

        /// <summary>
        /// Gets the entry point function name.
        /// </summary>
        public string EntryPoint { get; }

        /// <summary>
        /// Gets the resource bindings used by the program.
        /// </summary>
        public IReadOnlyList<ShaderBinding> Bindings {
            get {
                return bindings;
            }
        }

        /// <summary>
        /// Gets the input signature elements.
        /// </summary>
        public IReadOnlyList<ShaderVertexElement> Inputs {
            get {
                return inputs;
            }
        }

        /// <summary>
        /// Gets the output signature elements.
        /// </summary>
        public IReadOnlyList<ShaderVertexElement> Outputs {
            get {
                return outputs;
            }
        }

        /// <summary>
        /// Gets the compile-time variants for this program.
        /// </summary>
        public IReadOnlyList<ShaderVariant> Variants {
            get {
                return variants;
            }
        }
    }
}
