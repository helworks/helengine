using helengine;

namespace helshader {
    /// <summary>
    /// Stores shader program metadata used for code generation.
    /// </summary>
    public class ShaderProgramData {
        /// <summary>
        /// Initializes a new shader program data container.
        /// </summary>
        /// <param name="name">Program name.</param>
        /// <param name="stage">Shader stage.</param>
        /// <param name="entryPoint">Entry point name.</param>
        /// <param name="bindings">Resource bindings.</param>
        /// <param name="inputs">Input signature elements.</param>
        /// <param name="outputs">Output signature elements.</param>
        /// <param name="variants">Variant definitions.</param>
        public ShaderProgramData(
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
            Bindings = bindings;
            Inputs = inputs;
            Outputs = outputs;
            Variants = variants;
        }

        /// <summary>
        /// Gets the program name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the shader stage.
        /// </summary>
        public ShaderStage Stage { get; }

        /// <summary>
        /// Gets the entry point name.
        /// </summary>
        public string EntryPoint { get; }

        /// <summary>
        /// Gets the resource bindings.
        /// </summary>
        public ShaderBinding[] Bindings { get; }

        /// <summary>
        /// Gets the input signature elements.
        /// </summary>
        public ShaderVertexElement[] Inputs { get; }

        /// <summary>
        /// Gets the output signature elements.
        /// </summary>
        public ShaderVertexElement[] Outputs { get; }

        /// <summary>
        /// Gets the variant definitions.
        /// </summary>
        public ShaderVariant[] Variants { get; }
    }
}
