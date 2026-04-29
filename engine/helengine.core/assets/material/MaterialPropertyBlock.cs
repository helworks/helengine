namespace helengine {
    /// <summary>
    /// Stores resolved runtime values for the shader bindings exposed by a material layout.
    /// </summary>
    public class MaterialPropertyBlock {
        /// <summary>
        /// Layout that defines which bindings may be stored by this property block.
        /// </summary>
        readonly MaterialLayout LayoutValue;
        /// <summary>
        /// Runtime textures assigned to the layout's texture bindings.
        /// </summary>
        readonly RuntimeTexture[] TextureValues;
        /// <summary>
        /// Packed constant-buffer payloads assigned to the layout's constant-buffer bindings.
        /// </summary>
        readonly byte[][] ConstantBufferValues;

        /// <summary>
        /// Initializes a new property block for the supplied material layout.
        /// </summary>
        /// <param name="layout">Material layout that defines the allowed bindings.</param>
        public MaterialPropertyBlock(MaterialLayout layout) {
            LayoutValue = layout ?? throw new ArgumentNullException(nameof(layout));
            TextureValues = new RuntimeTexture[layout.TextureBindings.Length];
            ConstantBufferValues = new byte[layout.ConstantBufferBindings.Length][];
        }

        /// <summary>
        /// Gets the material layout that defines the allowed bindings.
        /// </summary>
        public MaterialLayout Layout => LayoutValue;

        /// <summary>
        /// Assigns a runtime texture to the supplied texture-binding name.
        /// </summary>
        /// <param name="bindingName">Texture-binding name to update.</param>
        /// <param name="texture">Texture value to assign, or <c>null</c> to clear the binding.</param>
        public void SetTexture(string bindingName, RuntimeTexture texture) {
            int bindingIndex = LayoutValue.FindTextureBindingIndex(bindingName);
            if (bindingIndex < 0) {
                throw new InvalidOperationException($"Texture binding '{bindingName}' was not found on the material layout.");
            }

            SetTexture(bindingIndex, texture);
        }

        /// <summary>
        /// Assigns a runtime texture to a texture-binding index.
        /// </summary>
        /// <param name="bindingIndex">Texture-binding index to update.</param>
        /// <param name="texture">Texture value to assign, or <c>null</c> to clear the binding.</param>
        public void SetTexture(int bindingIndex, RuntimeTexture texture) {
            ValidateTextureBindingIndex(bindingIndex);
            TextureValues[bindingIndex] = texture;
        }

        /// <summary>
        /// Reads the runtime texture assigned to a texture-binding index.
        /// </summary>
        /// <param name="bindingIndex">Texture-binding index to read.</param>
        /// <returns>Assigned runtime texture, or <c>null</c> when the binding is unset.</returns>
        public RuntimeTexture GetTexture(int bindingIndex) {
            ValidateTextureBindingIndex(bindingIndex);
            return TextureValues[bindingIndex];
        }

        /// <summary>
        /// Reads the first non-null texture assigned anywhere on the property block.
        /// </summary>
        /// <param name="texture">Resolved runtime texture when one is assigned.</param>
        /// <returns>True when a texture was assigned; otherwise false.</returns>
        public bool TryGetFirstTexture(out RuntimeTexture texture) {
            for (int bindingIndex = 0; bindingIndex < TextureValues.Length; bindingIndex++) {
                RuntimeTexture value = TextureValues[bindingIndex];
                if (value == null) {
                    continue;
                }

                texture = value;
                return true;
            }

            texture = null;
            return false;
        }

        /// <summary>
        /// Assigns packed constant-buffer data to the supplied binding name.
        /// </summary>
        /// <param name="bindingName">Constant-buffer binding name to update.</param>
        /// <param name="data">Packed buffer payload whose size must match the binding size, or <c>null</c> to clear the binding.</param>
        public void SetConstantBufferData(string bindingName, byte[] data) {
            int bindingIndex = LayoutValue.FindConstantBufferBindingIndex(bindingName);
            if (bindingIndex < 0) {
                throw new InvalidOperationException($"Constant buffer binding '{bindingName}' was not found on the material layout.");
            }

            SetConstantBufferData(bindingIndex, data);
        }

        /// <summary>
        /// Assigns packed constant-buffer data to a constant-buffer binding index.
        /// </summary>
        /// <param name="bindingIndex">Constant-buffer binding index to update.</param>
        /// <param name="data">Packed buffer payload whose size must match the binding size, or <c>null</c> to clear the binding.</param>
        public void SetConstantBufferData(int bindingIndex, byte[] data) {
            ValidateConstantBufferBindingIndex(bindingIndex);

            if (data == null) {
                ConstantBufferValues[bindingIndex] = null;
                return;
            }

            MaterialLayoutBinding binding = LayoutValue.ConstantBufferBindings[bindingIndex];
            if (data.Length != binding.Size) {
                throw new InvalidOperationException(
                    $"Constant buffer binding '{binding.Name}' expects {binding.Size} bytes but received {data.Length}.");
            }

            byte[] copiedData = new byte[data.Length];
            Array.Copy(data, copiedData, data.Length);
            ConstantBufferValues[bindingIndex] = copiedData;
        }

        /// <summary>
        /// Reads the packed constant-buffer payload assigned to a constant-buffer binding index.
        /// </summary>
        /// <param name="bindingIndex">Constant-buffer binding index to read.</param>
        /// <returns>Copied packed payload, or <c>null</c> when the binding is unset.</returns>
        public byte[] GetConstantBufferData(int bindingIndex) {
            ValidateConstantBufferBindingIndex(bindingIndex);

            byte[] data = ConstantBufferValues[bindingIndex];
            if (data == null) {
                return null;
            }

            byte[] copiedData = new byte[data.Length];
            Array.Copy(data, copiedData, data.Length);
            return copiedData;
        }

        /// <summary>
        /// Copies matching texture and constant-buffer values from another property block.
        /// </summary>
        /// <param name="source">Source property block whose values should be copied.</param>
        public void CopyMatchingValuesFrom(MaterialPropertyBlock source) {
            if (source == null) {
                throw new ArgumentNullException(nameof(source));
            }

            CopyMatchingTextureValuesFrom(source);
            CopyMatchingConstantBufferValuesFrom(source);
        }

        /// <summary>
        /// Validates that a texture-binding index exists on the layout.
        /// </summary>
        /// <param name="bindingIndex">Texture-binding index to validate.</param>
        void ValidateTextureBindingIndex(int bindingIndex) {
            if (bindingIndex < 0 || bindingIndex >= TextureValues.Length) {
                throw new ArgumentOutOfRangeException(nameof(bindingIndex), "Texture binding index is outside the material layout.");
            }
        }

        /// <summary>
        /// Validates that a constant-buffer binding index exists on the layout.
        /// </summary>
        /// <param name="bindingIndex">Constant-buffer binding index to validate.</param>
        void ValidateConstantBufferBindingIndex(int bindingIndex) {
            if (bindingIndex < 0 || bindingIndex >= ConstantBufferValues.Length) {
                throw new ArgumentOutOfRangeException(nameof(bindingIndex), "Constant-buffer binding index is outside the material layout.");
            }
        }

        /// <summary>
        /// Copies matching texture values from another property block.
        /// </summary>
        /// <param name="source">Source property block whose texture values should be copied.</param>
        void CopyMatchingTextureValuesFrom(MaterialPropertyBlock source) {
            for (int textureIndex = 0; textureIndex < TextureValues.Length; textureIndex++) {
                MaterialLayoutBinding binding = LayoutValue.TextureBindings[textureIndex];
                int sourceBindingIndex = source.Layout.FindTextureBindingIndex(binding.Name);
                if (sourceBindingIndex < 0) {
                    continue;
                }

                RuntimeTexture sourceTexture = source.GetTexture(sourceBindingIndex);
                if (sourceTexture == null) {
                    continue;
                }

                TextureValues[textureIndex] = sourceTexture;
            }
        }

        /// <summary>
        /// Copies matching constant-buffer values from another property block.
        /// </summary>
        /// <param name="source">Source property block whose constant-buffer values should be copied.</param>
        void CopyMatchingConstantBufferValuesFrom(MaterialPropertyBlock source) {
            for (int constantBufferIndex = 0; constantBufferIndex < ConstantBufferValues.Length; constantBufferIndex++) {
                MaterialLayoutBinding binding = LayoutValue.ConstantBufferBindings[constantBufferIndex];
                int sourceBindingIndex = source.Layout.FindConstantBufferBindingIndex(binding.Name);
                if (sourceBindingIndex < 0) {
                    continue;
                }

                MaterialLayoutBinding sourceBinding = source.Layout.ConstantBufferBindings[sourceBindingIndex];
                if (sourceBinding.Size != binding.Size) {
                    continue;
                }

                byte[] sourceData = source.GetConstantBufferData(sourceBindingIndex);
                if (sourceData == null) {
                    continue;
                }

                ConstantBufferValues[constantBufferIndex] = sourceData;
            }
        }
    }
}
