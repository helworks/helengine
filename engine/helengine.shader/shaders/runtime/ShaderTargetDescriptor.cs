namespace helengine {
    /// <summary>
    /// Describes one shader compile target: the stable identifier stored in runtime shader packages and the preprocessor define that selects the matching API branch inside shader sources.
    /// </summary>
    public class ShaderTargetDescriptor {
        /// <summary>
        /// Initializes a descriptor for one compile target.
        /// </summary>
        /// <param name="target">Compile target the descriptor describes.</param>
        /// <param name="name">Stable lowercase identifier persisted in shader packages and build metadata.</param>
        /// <param name="defineName">Preprocessor define emitted so shader sources can branch on the active API.</param>
        public ShaderTargetDescriptor(ShaderCompileTarget target, string name, string defineName) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Target name is required.", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(defineName)) {
                throw new ArgumentException("Target define name is required.", nameof(defineName));
            }

            Target = target;
            Name = name;
            DefineName = defineName;
        }

        /// <summary>
        /// Gets the compile target this descriptor describes.
        /// </summary>
        public ShaderCompileTarget Target { get; }

        /// <summary>
        /// Gets the stable lowercase identifier used whenever a target is written to or parsed from persisted data.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the preprocessor define name emitted with value "1" while compiling for this target.
        /// </summary>
        public string DefineName { get; }
    }
}
