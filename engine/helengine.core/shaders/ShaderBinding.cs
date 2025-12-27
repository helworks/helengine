namespace helengine {
    /// <summary>
    /// Describes a shader resource binding in generated metadata.
    /// </summary>
    public class ShaderBinding {
        /// <summary>
        /// Stores constant buffer member metadata when this binding is a constant buffer.
        /// </summary>
        readonly ShaderConstantMember[] members;

        /// <summary>
        /// Initializes a new shader binding description.
        /// </summary>
        /// <param name="name">Resource name as declared in the shader.</param>
        /// <param name="type">Resource type classification.</param>
        /// <param name="set">Logical descriptor set or register space.</param>
        /// <param name="slot">Binding slot within the set or space.</param>
        /// <param name="size">Size in bytes for constant buffers; otherwise zero.</param>
        /// <param name="members">Constant buffer members; provide an empty array for non-constant buffers.</param>
        public ShaderBinding(string name, ShaderResourceType type, int set, int slot, int size, ShaderConstantMember[] members) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Binding name must be provided.", nameof(name));
            }

            if (set < 0) {
                throw new ArgumentOutOfRangeException(nameof(set), "Set cannot be negative.");
            }

            if (slot < 0) {
                throw new ArgumentOutOfRangeException(nameof(slot), "Slot cannot be negative.");
            }

            if (size < 0) {
                throw new ArgumentOutOfRangeException(nameof(size), "Size cannot be negative.");
            }

            if (members == null) {
                throw new ArgumentNullException(nameof(members));
            }

            Name = name;
            Type = type;
            Set = set;
            Slot = slot;
            Size = size;
            this.members = members;
        }

        /// <summary>
        /// Gets the resource name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the resource type classification.
        /// </summary>
        public ShaderResourceType Type { get; }

        /// <summary>
        /// Gets the logical descriptor set or register space.
        /// </summary>
        public int Set { get; }

        /// <summary>
        /// Gets the binding slot within the set or space.
        /// </summary>
        public int Slot { get; }

        /// <summary>
        /// Gets the resource size in bytes for constant buffers.
        /// </summary>
        public int Size { get; }

        /// <summary>
        /// Gets the constant buffer member list.
        /// </summary>
        public IReadOnlyList<ShaderConstantMember> Members {
            get {
                return members;
            }
        }
    }
}
