namespace helengine {
    /// <summary>
    /// Represents serialized metadata for a shader entry point.
    /// </summary>
    public class ShaderProgramAsset {
        /// <summary>
        /// Initializes a new shader program asset with empty collections.
        /// </summary>
        public ShaderProgramAsset() {
            Bindings = Array.Empty<ShaderBindingAsset>();
            Inputs = Array.Empty<ShaderVertexElementAsset>();
            Outputs = Array.Empty<ShaderVertexElementAsset>();
            Variants = Array.Empty<ShaderVariantAsset>();
        }

        /// <summary>
        /// Logical program name.
        /// </summary>
        public string Name;

        /// <summary>
        /// Pipeline stage for the entry point.
        /// </summary>
        public ShaderStage Stage;

        /// <summary>
        /// Entry point function name.
        /// </summary>
        public string EntryPoint;

        /// <summary>
        /// Resource bindings used by the program.
        /// </summary>
        public ShaderBindingAsset[] Bindings;

        /// <summary>
        /// Input signature elements.
        /// </summary>
        public ShaderVertexElementAsset[] Inputs;

        /// <summary>
        /// Output signature elements.
        /// </summary>
        public ShaderVertexElementAsset[] Outputs;

        /// <summary>
        /// Compile-time variants available for the program.
        /// </summary>
        public ShaderVariantAsset[] Variants;

        /// <summary>
        /// Builds a runtime program definition from the serialized asset.
        /// </summary>
        /// <returns>Program definition instance.</returns>
        public ShaderProgramDefinition ToDefinition() {
            Validate();

            ShaderBinding[] bindingDefinitions = BuildBindings();
            ShaderVertexElement[] inputDefinitions = BuildInputs();
            ShaderVertexElement[] outputDefinitions = BuildOutputs();
            ShaderVariant[] variantDefinitions = BuildVariants();
            return new ShaderProgramDefinition(Name, Stage, EntryPoint, bindingDefinitions, inputDefinitions, outputDefinitions, variantDefinitions);
        }

        /// <summary>
        /// Creates a program asset from a runtime program definition.
        /// </summary>
        /// <param name="definition">Program definition to convert.</param>
        /// <returns>Serialized program asset.</returns>
        public static ShaderProgramAsset FromDefinition(ShaderProgramDefinition definition) {
            if (definition == null) {
                throw new ArgumentNullException(nameof(definition));
            }

            ShaderProgramAsset asset = new ShaderProgramAsset {
                Name = definition.Name,
                Stage = definition.Stage,
                EntryPoint = definition.EntryPoint,
                Bindings = BuildBindingAssets(definition),
                Inputs = BuildInputAssets(definition),
                Outputs = BuildOutputAssets(definition),
                Variants = BuildVariantAssets(definition)
            };

            return asset;
        }

        /// <summary>
        /// Validates program asset data before conversion.
        /// </summary>
        void Validate() {
            if (string.IsNullOrWhiteSpace(Name)) {
                throw new InvalidOperationException("Program name must be provided.");
            } else if (string.IsNullOrWhiteSpace(EntryPoint)) {
                throw new InvalidOperationException("Program entry point must be provided.");
            } else if (Bindings == null) {
                throw new InvalidOperationException("Program bindings must be provided.");
            } else if (Inputs == null) {
                throw new InvalidOperationException("Program inputs must be provided.");
            } else if (Outputs == null) {
                throw new InvalidOperationException("Program outputs must be provided.");
            } else if (Variants == null) {
                throw new InvalidOperationException("Program variants must be provided.");
            }
        }

        /// <summary>
        /// Builds runtime binding definitions from serialized bindings.
        /// </summary>
        /// <returns>Array of binding definitions.</returns>
        ShaderBinding[] BuildBindings() {
            ShaderBinding[] definitions = new ShaderBinding[Bindings.Length];
            for (int i = 0; i < Bindings.Length; i++) {
                ShaderBindingAsset binding = Bindings[i];
                if (binding == null) {
                    throw new InvalidOperationException("Program bindings contain a null entry.");
                }

                definitions[i] = binding.ToBinding();
            }

            return definitions;
        }

        /// <summary>
        /// Builds runtime input definitions from serialized inputs.
        /// </summary>
        /// <returns>Array of input definitions.</returns>
        ShaderVertexElement[] BuildInputs() {
            ShaderVertexElement[] definitions = new ShaderVertexElement[Inputs.Length];
            for (int i = 0; i < Inputs.Length; i++) {
                ShaderVertexElementAsset element = Inputs[i];
                if (element == null) {
                    throw new InvalidOperationException("Program inputs contain a null entry.");
                }

                definitions[i] = element.ToVertexElement();
            }

            return definitions;
        }

        /// <summary>
        /// Builds runtime output definitions from serialized outputs.
        /// </summary>
        /// <returns>Array of output definitions.</returns>
        ShaderVertexElement[] BuildOutputs() {
            ShaderVertexElement[] definitions = new ShaderVertexElement[Outputs.Length];
            for (int i = 0; i < Outputs.Length; i++) {
                ShaderVertexElementAsset element = Outputs[i];
                if (element == null) {
                    throw new InvalidOperationException("Program outputs contain a null entry.");
                }

                definitions[i] = element.ToVertexElement();
            }

            return definitions;
        }

        /// <summary>
        /// Builds runtime variant definitions from serialized variants.
        /// </summary>
        /// <returns>Array of variant definitions.</returns>
        ShaderVariant[] BuildVariants() {
            ShaderVariant[] definitions = new ShaderVariant[Variants.Length];
            for (int i = 0; i < Variants.Length; i++) {
                ShaderVariantAsset variant = Variants[i];
                if (variant == null) {
                    throw new InvalidOperationException("Program variants contain a null entry.");
                }

                definitions[i] = variant.ToVariant();
            }

            return definitions;
        }

        /// <summary>
        /// Builds binding assets from a runtime program definition.
        /// </summary>
        /// <param name="definition">Program definition to read.</param>
        /// <returns>Array of binding assets.</returns>
        static ShaderBindingAsset[] BuildBindingAssets(ShaderProgramDefinition definition) {
            IReadOnlyList<ShaderBinding> bindings = definition.Bindings;
            ShaderBindingAsset[] assets = new ShaderBindingAsset[bindings.Count];
            for (int i = 0; i < bindings.Count; i++) {
                assets[i] = ShaderBindingAsset.FromBinding(bindings[i]);
            }

            return assets;
        }

        /// <summary>
        /// Builds input assets from a runtime program definition.
        /// </summary>
        /// <param name="definition">Program definition to read.</param>
        /// <returns>Array of input assets.</returns>
        static ShaderVertexElementAsset[] BuildInputAssets(ShaderProgramDefinition definition) {
            IReadOnlyList<ShaderVertexElement> inputs = definition.Inputs;
            ShaderVertexElementAsset[] assets = new ShaderVertexElementAsset[inputs.Count];
            for (int i = 0; i < inputs.Count; i++) {
                assets[i] = ShaderVertexElementAsset.FromVertexElement(inputs[i]);
            }

            return assets;
        }

        /// <summary>
        /// Builds output assets from a runtime program definition.
        /// </summary>
        /// <param name="definition">Program definition to read.</param>
        /// <returns>Array of output assets.</returns>
        static ShaderVertexElementAsset[] BuildOutputAssets(ShaderProgramDefinition definition) {
            IReadOnlyList<ShaderVertexElement> outputs = definition.Outputs;
            ShaderVertexElementAsset[] assets = new ShaderVertexElementAsset[outputs.Count];
            for (int i = 0; i < outputs.Count; i++) {
                assets[i] = ShaderVertexElementAsset.FromVertexElement(outputs[i]);
            }

            return assets;
        }

        /// <summary>
        /// Builds variant assets from a runtime program definition.
        /// </summary>
        /// <param name="definition">Program definition to read.</param>
        /// <returns>Array of variant assets.</returns>
        static ShaderVariantAsset[] BuildVariantAssets(ShaderProgramDefinition definition) {
            IReadOnlyList<ShaderVariant> variants = definition.Variants;
            ShaderVariantAsset[] assets = new ShaderVariantAsset[variants.Count];
            for (int i = 0; i < variants.Count; i++) {
                assets[i] = ShaderVariantAsset.FromVariant(variants[i]);
            }

            return assets;
        }
    }
}
