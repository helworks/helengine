namespace helengine {
    /// <summary>
    /// Describes a preprocessor define used during shader compilation.
    /// </summary>
    public class ShaderDefine {
        /// <summary>
        /// Initializes a new shader define.
        /// </summary>
        /// <param name="name">Define name.</param>
        /// <param name="value">Define value or an empty string for value-less defines.</param>
        public ShaderDefine(string name, string value) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Define name must be provided.", nameof(name));
            }

            if (value == null) {
                throw new ArgumentNullException(nameof(value));
            }

            Name = name;
            Value = value;
        }

        /// <summary>
        /// Gets the define name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the define value or an empty string for value-less defines.
        /// </summary>
        public string Value { get; }
    }
}
