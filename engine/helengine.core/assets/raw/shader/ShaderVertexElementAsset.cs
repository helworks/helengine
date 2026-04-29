namespace helengine {
    /// <summary>
    /// Represents serialized data for a shader input/output vertex element.
    /// </summary>
    public class ShaderVertexElementAsset {
        /// <summary>
        /// Semantic name such as POSITION or TEXCOORD.
        /// </summary>
        public string Semantic;

        /// <summary>
        /// Semantic index for the element.
        /// </summary>
        public int Index;

        /// <summary>
        /// Type or format name such as float3.
        /// </summary>
        public string Format;

        /// <summary>
        /// Builds a runtime vertex element definition from serialized data.
        /// </summary>
        /// <returns>Vertex element definition.</returns>
        public ShaderVertexElement ToVertexElement() {
            Validate();
            return new ShaderVertexElement(Semantic, Index, Format);
        }

        /// <summary>
        /// Creates a serialized vertex element asset from a runtime definition.
        /// </summary>
        /// <param name="element">Vertex element definition to convert.</param>
        /// <returns>Serialized vertex element asset.</returns>
        public static ShaderVertexElementAsset FromVertexElement(ShaderVertexElement element) {
            if (element == null) {
                throw new ArgumentNullException(nameof(element));
            }

            ShaderVertexElementAsset asset = new ShaderVertexElementAsset {
                Semantic = element.Semantic,
                Index = element.Index,
                Format = element.Format
            };

            return asset;
        }

        /// <summary>
        /// Validates vertex element data before conversion.
        /// </summary>
        void Validate() {
            if (string.IsNullOrWhiteSpace(Semantic)) {
                throw new InvalidOperationException("Vertex element semantic must be provided.");
            } else if (string.IsNullOrWhiteSpace(Format)) {
                throw new InvalidOperationException("Vertex element format must be provided.");
            } else if (Index < 0) {
                throw new InvalidOperationException("Vertex element index cannot be negative.");
            }
        }
    }
}
