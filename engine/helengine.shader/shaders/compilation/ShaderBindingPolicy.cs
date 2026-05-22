namespace helengine {
    /// <summary>
    /// Defines how HLSL registers map to unified binding slots for cross-API shader reflection.
    /// </summary>
    public class ShaderBindingPolicy {
        /// <summary>
        /// Initializes a new binding policy.
        /// </summary>
        /// <param name="defaultSpace">Default register space used when one is not provided.</param>
        /// <param name="constantBufferShift">Slot shift applied to constant buffers.</param>
        /// <param name="textureShift">Slot shift applied to sampled textures.</param>
        /// <param name="samplerShift">Slot shift applied to samplers.</param>
        /// <param name="storageShift">Slot shift applied to storage buffers and storage textures.</param>
        public ShaderBindingPolicy(
            int defaultSpace,
            int constantBufferShift,
            int textureShift,
            int samplerShift,
            int storageShift) {
            if (defaultSpace < 0) {
                throw new ArgumentOutOfRangeException(nameof(defaultSpace), "Default space cannot be negative.");
            }

            if (constantBufferShift < 0) {
                throw new ArgumentOutOfRangeException(nameof(constantBufferShift), "Constant buffer shift cannot be negative.");
            }

            if (textureShift < 0) {
                throw new ArgumentOutOfRangeException(nameof(textureShift), "Texture shift cannot be negative.");
            }

            if (samplerShift < 0) {
                throw new ArgumentOutOfRangeException(nameof(samplerShift), "Sampler shift cannot be negative.");
            }

            if (storageShift < 0) {
                throw new ArgumentOutOfRangeException(nameof(storageShift), "Storage shift cannot be negative.");
            }

            DefaultSpace = defaultSpace;
            ConstantBufferShift = constantBufferShift;
            TextureShift = textureShift;
            SamplerShift = samplerShift;
            StorageShift = storageShift;
        }

        /// <summary>
        /// Gets the default register space used when none is specified.
        /// </summary>
        public int DefaultSpace { get; }

        /// <summary>
        /// Gets the shift applied to constant buffer bindings.
        /// </summary>
        public int ConstantBufferShift { get; }

        /// <summary>
        /// Gets the shift applied to sampled texture bindings.
        /// </summary>
        public int TextureShift { get; }

        /// <summary>
        /// Gets the shift applied to sampler bindings.
        /// </summary>
        public int SamplerShift { get; }

        /// <summary>
        /// Gets the shift applied to storage buffer and storage texture bindings.
        /// </summary>
        public int StorageShift { get; }

        /// <summary>
        /// Computes a unified binding slot for the given resource type and register index.
        /// </summary>
        /// <param name="type">Resource type to map.</param>
        /// <param name="registerIndex">Register index from HLSL reflection.</param>
        /// <returns>Unified binding slot.</returns>
        public int GetSlot(ShaderResourceType type, int registerIndex) {
            if (registerIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(registerIndex), "Register index cannot be negative.");
            }

            int shift = GetShift(type);
            return shift + registerIndex;
        }

        /// <summary>
        /// Returns the binding shift for the provided resource type.
        /// </summary>
        /// <param name="type">Resource type to map.</param>
        /// <returns>Shift value for the resource type.</returns>
        int GetShift(ShaderResourceType type) {
            switch (type) {
                case ShaderResourceType.ConstantBuffer:
                    return ConstantBufferShift;
                case ShaderResourceType.Texture2D:
                case ShaderResourceType.TextureCube:
                    return TextureShift;
                case ShaderResourceType.Sampler:
                    return SamplerShift;
                case ShaderResourceType.Buffer:
                case ShaderResourceType.StorageBuffer:
                case ShaderResourceType.StorageTexture2D:
                    return StorageShift;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), "Unsupported resource type.");
            }
        }
    }
}
