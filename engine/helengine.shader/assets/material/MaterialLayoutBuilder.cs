namespace helengine {
    /// <summary>
    /// Resolves a shared material layout from a material asset and the shader metadata it references.
    /// </summary>
    public static class MaterialLayoutBuilder {
        /// <summary>
        /// Name used by the engine-managed transform constant buffer.
        /// </summary>
        const string TransformBufferName = "TransformBuffer";

        /// <summary>
        /// Builds a material layout from the supplied material and shader assets.
        /// </summary>
        /// <param name="materialAsset">Material asset that selects the shader programs and render state.</param>
        /// <param name="shaderAsset">Shader asset that exposes the selected program bindings.</param>
        /// <returns>Resolved shared material layout.</returns>
        public static MaterialLayout Build(MaterialAsset materialAsset, ShaderAsset shaderAsset) {
            if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            }

            if (shaderAsset == null) {
                throw new ArgumentNullException(nameof(shaderAsset));
            }

            ShaderProgramAsset vertexProgram = FindProgram(shaderAsset, materialAsset.VertexProgram, ShaderStage.Vertex);
            ShaderProgramAsset pixelProgram = FindProgram(shaderAsset, materialAsset.PixelProgram, ShaderStage.Pixel);
            List<MaterialLayoutBinding> textureBindings = new List<MaterialLayoutBinding>();
            List<MaterialLayoutBinding> constantBufferBindings = new List<MaterialLayoutBinding>();
            List<MaterialLayoutBinding> samplerBindings = new List<MaterialLayoutBinding>();
            Dictionary<string, MaterialLayoutBinding> bindingByKey = new Dictionary<string, MaterialLayoutBinding>(StringComparer.Ordinal);

            AddBindings(vertexProgram, textureBindings, constantBufferBindings, samplerBindings, bindingByKey);
            AddBindings(pixelProgram, textureBindings, constantBufferBindings, samplerBindings, bindingByKey);

            MaterialRenderState renderState = materialAsset.RenderState ?? throw new InvalidOperationException("Material render state must be provided.");
            return new MaterialLayout(
                materialAsset.ShaderAssetId ?? string.Empty,
                materialAsset.VertexProgram ?? string.Empty,
                materialAsset.PixelProgram ?? string.Empty,
                materialAsset.Variant ?? string.Empty,
                renderState.Clone(),
                textureBindings.ToArray(),
                constantBufferBindings.ToArray(),
                samplerBindings.ToArray());
        }

        /// <summary>
        /// Locates a shader program with the supplied name and stage.
        /// </summary>
        /// <param name="shaderAsset">Shader asset to inspect.</param>
        /// <param name="programName">Program name to resolve.</param>
        /// <param name="stage">Program stage to resolve.</param>
        /// <returns>Resolved shader program asset.</returns>
        static ShaderProgramAsset FindProgram(ShaderAsset shaderAsset, string programName, ShaderStage stage) {
            if (shaderAsset.Programs == null) {
                throw new InvalidOperationException("Shader asset programs must be provided.");
            }

            for (int programIndex = 0; programIndex < shaderAsset.Programs.Length; programIndex++) {
                ShaderProgramAsset program = shaderAsset.Programs[programIndex];
                if (program == null) {
                    continue;
                }

                if (!string.Equals(program.Name, programName, StringComparison.Ordinal)) {
                    continue;
                } else if (program.Stage != stage) {
                    continue;
                } else {
                    return program;
                }
            }

            throw new InvalidOperationException($"Shader program '{programName}' with stage '{stage}' was not found.");
        }

        /// <summary>
        /// Adds exposed material bindings from one shader program into the shared layout lists.
        /// </summary>
        /// <param name="program">Shader program whose bindings should be added.</param>
        /// <param name="textureBindings">Texture-binding list being built.</param>
        /// <param name="constantBufferBindings">Constant-buffer binding list being built.</param>
        /// <param name="samplerBindings">Sampler-binding list being built.</param>
        /// <param name="bindingByKey">Lookup used to merge duplicate bindings across stages.</param>
        static void AddBindings(
            ShaderProgramAsset program,
            [NativeNoEscape] List<MaterialLayoutBinding> textureBindings,
            [NativeNoEscape] List<MaterialLayoutBinding> constantBufferBindings,
            [NativeNoEscape] List<MaterialLayoutBinding> samplerBindings,
            [NativeNoEscape] Dictionary<string, MaterialLayoutBinding> bindingByKey) {
            if (program == null) {
                throw new ArgumentNullException(nameof(program));
            }

            if (program.Bindings == null) {
                throw new InvalidOperationException("Shader program bindings must be provided.");
            }

            for (int bindingIndex = 0; bindingIndex < program.Bindings.Length; bindingIndex++) {
                ShaderBindingAsset binding = program.Bindings[bindingIndex];
                if (binding == null) {
                    throw new InvalidOperationException("Shader program bindings contain a null entry.");
                }

                if (IsEngineManagedBinding(binding)) {
                    continue;
                }

                MaterialLayoutBinding layoutBinding = new MaterialLayoutBinding(
                    binding.Name,
                    binding.Type,
                    binding.Set,
                    binding.Slot,
                    binding.Size);
                string bindingKey = BuildBindingKey(layoutBinding);
                if (bindingByKey.TryGetValue(bindingKey, out MaterialLayoutBinding existingBinding)) {
                    ValidateMatchingBinding(existingBinding, layoutBinding);
                    NativeOwnership.Delete(layoutBinding);
                    continue;
                }

                bindingByKey[bindingKey] = layoutBinding;
                AddBindingToCategory(layoutBinding, textureBindings, constantBufferBindings, samplerBindings);
            }
        }

        /// <summary>
        /// Determines whether a binding is managed by the engine rather than material data.
        /// </summary>
        /// <param name="binding">Binding to evaluate.</param>
        /// <returns>True when the binding is engine-managed; otherwise false.</returns>
        static bool IsEngineManagedBinding(ShaderBindingAsset binding) {
            if (binding == null) {
                throw new ArgumentNullException(nameof(binding));
            }

            return binding.Type == ShaderResourceType.ConstantBuffer &&
                   binding.Slot == 0 &&
                   string.Equals(binding.Name, TransformBufferName, StringComparison.Ordinal);
        }

        /// <summary>
        /// Builds a stable key used to merge duplicate bindings across multiple shader stages.
        /// </summary>
        /// <param name="binding">Binding whose merge key should be built.</param>
        /// <returns>Stable merge key.</returns>
        static string BuildBindingKey(MaterialLayoutBinding binding) {
            if (binding == null) {
                throw new ArgumentNullException(nameof(binding));
            }

            return string.Concat(
                ((int)binding.ResourceType).ToString(),
                "|",
                binding.Name);
        }

        /// <summary>
        /// Validates that duplicate bindings contributed by different shader stages describe the same layout.
        /// </summary>
        /// <param name="existingBinding">Previously registered binding.</param>
        /// <param name="newBinding">Binding being merged into the layout.</param>
        static void ValidateMatchingBinding(MaterialLayoutBinding existingBinding, MaterialLayoutBinding newBinding) {
            if (existingBinding == null) {
                throw new ArgumentNullException(nameof(existingBinding));
            }

            if (newBinding == null) {
                throw new ArgumentNullException(nameof(newBinding));
            }

            if (existingBinding.ResourceType != newBinding.ResourceType) {
                throw new InvalidOperationException($"Material binding '{existingBinding.Name}' uses conflicting resource types across shader stages.");
            } else if (existingBinding.Set != newBinding.Set) {
                throw new InvalidOperationException($"Material binding '{existingBinding.Name}' uses conflicting sets across shader stages.");
            } else if (existingBinding.Slot != newBinding.Slot) {
                throw new InvalidOperationException($"Material binding '{existingBinding.Name}' uses conflicting slots across shader stages.");
            } else if (existingBinding.Size != newBinding.Size) {
                throw new InvalidOperationException($"Material binding '{existingBinding.Name}' uses conflicting sizes across shader stages.");
            }
        }

        /// <summary>
        /// Adds one resolved layout binding to the correct binding category.
        /// </summary>
        /// <param name="binding">Resolved layout binding to categorize.</param>
        /// <param name="textureBindings">Texture-binding list being built.</param>
        /// <param name="constantBufferBindings">Constant-buffer binding list being built.</param>
        /// <param name="samplerBindings">Sampler-binding list being built.</param>
        static void AddBindingToCategory(
            MaterialLayoutBinding binding,
            [NativeNoEscape] List<MaterialLayoutBinding> textureBindings,
            [NativeNoEscape] List<MaterialLayoutBinding> constantBufferBindings,
            [NativeNoEscape] List<MaterialLayoutBinding> samplerBindings) {
            if (binding == null) {
                throw new ArgumentNullException(nameof(binding));
            }

            if (binding.ResourceType == ShaderResourceType.Texture2D || binding.ResourceType == ShaderResourceType.TextureCube) {
                textureBindings.Add(binding);
                return;
            } else if (binding.ResourceType == ShaderResourceType.ConstantBuffer) {
                constantBufferBindings.Add(binding);
                return;
            } else if (binding.ResourceType == ShaderResourceType.Sampler) {
                samplerBindings.Add(binding);
                return;
            }

            throw new InvalidOperationException(
                $"Shader resource type '{binding.ResourceType}' is not supported by the material layout builder.");
        }
    }
}
