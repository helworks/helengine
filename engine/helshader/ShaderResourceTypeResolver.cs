using helengine;

namespace helshader {
    /// <summary>
    /// Maps resource type strings to shader resource type enums.
    /// </summary>
    public class ShaderResourceTypeResolver {
        /// <summary>
        /// Parses a resource type string to a shader resource type enum.
        /// </summary>
        /// <param name="type">Resource type string.</param>
        /// <returns>Parsed shader resource type.</returns>
        public ShaderResourceType Parse(string type) {
            if (string.IsNullOrWhiteSpace(type)) {
                throw new ArgumentException("Resource type must be provided.", nameof(type));
            }

            if (string.Equals(type, "cbuffer", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "constantbuffer", StringComparison.OrdinalIgnoreCase)) {
                return ShaderResourceType.ConstantBuffer;
            }

            if (string.Equals(type, "texture2d", StringComparison.OrdinalIgnoreCase)) {
                return ShaderResourceType.Texture2D;
            }

            if (string.Equals(type, "texturecube", StringComparison.OrdinalIgnoreCase)) {
                return ShaderResourceType.TextureCube;
            }

            if (string.Equals(type, "sampler", StringComparison.OrdinalIgnoreCase)) {
                return ShaderResourceType.Sampler;
            }

            if (string.Equals(type, "buffer", StringComparison.OrdinalIgnoreCase)) {
                return ShaderResourceType.Buffer;
            }

            if (string.Equals(type, "storagebuffer", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "rwbuffer", StringComparison.OrdinalIgnoreCase)) {
                return ShaderResourceType.StorageBuffer;
            }

            if (string.Equals(type, "storagetexture2d", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "rwtexture2d", StringComparison.OrdinalIgnoreCase)) {
                return ShaderResourceType.StorageTexture2D;
            }

            throw new InvalidOperationException($"Unsupported resource type '{type}'.");
        }
    }
}
