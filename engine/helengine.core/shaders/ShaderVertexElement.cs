namespace helengine {
    /// <summary>
    /// Describes a single vertex input or output element in generated metadata.
    /// </summary>
    public class ShaderVertexElement {
        /// <summary>
        /// Initializes a new vertex element description.
        /// </summary>
        /// <param name="semantic">Semantic name such as POSITION or TEXCOORD.</param>
        /// <param name="index">Semantic index used for semantic arrays.</param>
        /// <param name="format">Type/format description, such as float3.</param>
        public ShaderVertexElement(string semantic, int index, string format) {
            if (string.IsNullOrWhiteSpace(semantic)) {
                throw new ArgumentException("Semantic must be provided.", nameof(semantic));
            }

            if (string.IsNullOrWhiteSpace(format)) {
                throw new ArgumentException("Format must be provided.", nameof(format));
            }

            if (index < 0) {
                throw new ArgumentOutOfRangeException(nameof(index), "Index cannot be negative.");
            }

            Semantic = semantic;
            Index = index;
            Format = format;
        }

        /// <summary>
        /// Gets the semantic name for the element.
        /// </summary>
        public string Semantic { get; }

        /// <summary>
        /// Gets the semantic index for the element.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Gets the format/type name for the element.
        /// </summary>
        public string Format { get; }
    }
}
