namespace helengine {
    /// <summary>
    /// Describes a compiled shader variant and its define set.
    /// </summary>
    public class ShaderVariant : IDisposable {
        /// <summary>
        /// Stores the define list backing the variant.
        /// </summary>
        [NativeOwnedMember]
        string[] DefinesValue;

        /// <summary>
        /// Initializes a new shader variant description.
        /// </summary>
        /// <param name="name">Variant identifier used for selection.</param>
        /// <param name="defines">Preprocessor defines used for this variant.</param>
        public ShaderVariant(string name, [NativeTakesOwnership] string[] defines) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Variant name must be provided.", nameof(name));
            }

            if (defines == null) {
                throw new ArgumentNullException(nameof(defines));
            }

            Name = name;
            DefinesValue = defines;
        }

        /// <summary>
        /// Gets the variant name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the define list used to compile this variant.
        /// </summary>
        [NativeBorrowedReturn]
        public string[] Defines {
            get {
                return DefinesValue;
            }
        }

        /// <summary>
        /// Releases the owned define-array container while leaving aliased define strings with their original owners.
        /// </summary>
        public void Dispose() {
            NativeOwnership.Release(ref DefinesValue);
        }
    }
}
