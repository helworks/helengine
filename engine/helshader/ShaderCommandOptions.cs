namespace helshader {
    /// <summary>
    /// Stores parsed command line options for helshader.
    /// </summary>
    public class ShaderCommandOptions {
        /// <summary>
        /// Initializes a new command options container.
        /// </summary>
        public ShaderCommandOptions() {
            Defines = new List<string>();
        }

        /// <summary>
        /// Gets or sets the command type.
        /// </summary>
        public ShaderCommandType Command { get; set; }

        /// <summary>
        /// Gets or sets the manifest path.
        /// </summary>
        public string ManifestPath { get; set; }

        /// <summary>
        /// Gets or sets a shader name filter.
        /// </summary>
        public string ShaderName { get; set; }

        /// <summary>
        /// Gets or sets a shader file filter.
        /// </summary>
        public string ShaderFile { get; set; }

        /// <summary>
        /// Gets or sets a target backend filter.
        /// </summary>
        public string Target { get; set; }

        /// <summary>
        /// Gets or sets a variant filter.
        /// </summary>
        public string Variant { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether all targets should be built.
        /// </summary>
        public bool AllTargets { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether modules should be emitted.
        /// </summary>
        public bool EmitModules { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a clean rebuild is requested.
        /// </summary>
        public bool Clean { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether verbose output is enabled.
        /// </summary>
        public bool Verbose { get; set; }

        /// <summary>
        /// Gets the list of global defines applied during compilation.
        /// </summary>
        public List<string> Defines { get; }
    }
}
