namespace helengine {
    /// <summary>
    /// Describes a member within a constant buffer layout.
    /// </summary>
    public class ShaderConstantMember {
        /// <summary>
        /// Initializes a new constant buffer member description.
        /// </summary>
        /// <param name="name">Member name as declared in the shader.</param>
        /// <param name="type">Type name such as float4x4.</param>
        /// <param name="offset">Byte offset of the member within the constant buffer.</param>
        /// <param name="size">Size in bytes occupied by the member.</param>
        public ShaderConstantMember(string name, string type, int offset, int size) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Member name must be provided.", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(type)) {
                throw new ArgumentException("Member type must be provided.", nameof(type));
            }

            if (offset < 0) {
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative.");
            }

            if (size < 0) {
                throw new ArgumentOutOfRangeException(nameof(size), "Size cannot be negative.");
            }

            Name = name;
            Type = type;
            Offset = offset;
            Size = size;
        }

        /// <summary>
        /// Gets the member name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the member type name.
        /// </summary>
        public string Type { get; }

        /// <summary>
        /// Gets the byte offset of the member.
        /// </summary>
        public int Offset { get; }

        /// <summary>
        /// Gets the member size in bytes.
        /// </summary>
        public int Size { get; }
    }
}
