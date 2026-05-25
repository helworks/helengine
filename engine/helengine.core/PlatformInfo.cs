namespace helengine {
    /// <summary>
    /// Describes the runtime platform identity and builder-stamped version for one running core instance.
    /// </summary>
    public sealed class PlatformInfo {
        /// <summary>
        /// Initializes one immutable runtime platform metadata record.
        /// </summary>
        /// <param name="name">Stable platform identifier, such as windows or an external package-owned platform id.</param>
        /// <param name="version">Builder-stamped platform version string reported by the running artifact.</param>
        public PlatformInfo(string name, string version) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Platform name is required.", nameof(name));
            }
            if (string.IsNullOrWhiteSpace(version)) {
                throw new ArgumentException("Platform version is required.", nameof(version));
            }

            Name = name;
            Version = version;
        }

        /// <summary>
        /// Gets the stable platform identifier for the running artifact.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the builder-stamped version string for the running artifact.
        /// </summary>
        public string Version { get; }
    }
}
