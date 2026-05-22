namespace helengine {
    /// <summary>
    /// Describes one shader resource binding that is exposed through a material layout.
    /// </summary>
    public class MaterialLayoutBinding {
        /// <summary>
        /// Initializes a new material-layout binding description.
        /// </summary>
        /// <param name="name">Binding name as declared by the shader.</param>
        /// <param name="resourceType">Shader resource type exposed by the binding.</param>
        /// <param name="set">Logical descriptor set or register space.</param>
        /// <param name="slot">Logical binding slot within the set or space.</param>
        /// <param name="size">Binding size in bytes for constant buffers, otherwise zero.</param>
        public MaterialLayoutBinding(
            string name,
            ShaderResourceType resourceType,
            int set,
            int slot,
            int size) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Binding name must be provided.", nameof(name));
            }

            if (set < 0) {
                throw new ArgumentOutOfRangeException(nameof(set), "Binding set cannot be negative.");
            }

            if (slot < 0) {
                throw new ArgumentOutOfRangeException(nameof(slot), "Binding slot cannot be negative.");
            }

            if (size < 0) {
                throw new ArgumentOutOfRangeException(nameof(size), "Binding size cannot be negative.");
            }

            Name = name;
            ResourceType = resourceType;
            Set = set;
            Slot = slot;
            Size = size;
        }

        /// <summary>
        /// Gets the binding name as declared by the shader.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the shader resource type exposed by the binding.
        /// </summary>
        public ShaderResourceType ResourceType { get; }

        /// <summary>
        /// Gets the logical descriptor set or register space.
        /// </summary>
        public int Set { get; }

        /// <summary>
        /// Gets the logical binding slot within the set or space.
        /// </summary>
        public int Slot { get; }

        /// <summary>
        /// Gets the binding size in bytes for constant buffers, otherwise zero.
        /// </summary>
        public int Size { get; }
    }
}
