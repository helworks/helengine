namespace helengine {
    /// <summary>
    /// Describes a compiled shader variant and its define set.
    /// </summary>
    public class ShaderVariant {
        /// <summary>
        /// Stores the define list backing the variant.
        /// </summary>
        readonly string[] defines;

        /// <summary>
        /// Initializes a new shader variant description.
        /// </summary>
        /// <param name="name">Variant identifier used for selection.</param>
        /// <param name="defines">Preprocessor defines used for this variant.</param>
        public ShaderVariant(string name, string[] defines) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Variant name must be provided.", nameof(name));
            }

            if (defines == null) {
                throw new ArgumentNullException(nameof(defines));
            }

            Name = name;
            this.defines = defines;
        }

        /// <summary>
        /// Gets the variant name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the define list used to compile this variant.
        /// </summary>
        public IReadOnlyList<string> Defines {
            get {
                return defines;
            }
        }
    }
}
