namespace helengine.tools.buildwaiter {
    /// <summary>
    /// Represents the platform build state written beside final output artifacts.
    /// </summary>
    public sealed class BuildStateDocument {
        /// <summary>
        /// Gets or sets the unique build invocation identifier.
        /// </summary>
        public string BuildId { get; set; }

        /// <summary>
        /// Gets or sets the authored project path used by the build.
        /// </summary>
        public string ProjectPath { get; set; }

        /// <summary>
        /// Gets or sets the requested target platform.
        /// </summary>
        public string Platform { get; set; }

        /// <summary>
        /// Gets or sets the requested build profile.
        /// </summary>
        public string BuildProfile { get; set; }

        /// <summary>
        /// Gets or sets the editor build configuration.
        /// </summary>
        public string Configuration { get; set; }

        /// <summary>
        /// Gets or sets the UTC time at which the build invocation started.
        /// </summary>
        public DateTimeOffset StartedUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC time at which the build invocation completed.
        /// </summary>
        public DateTimeOffset? CompletedUtc { get; set; }

        /// <summary>
        /// Gets or sets the running or terminal build status.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Gets or sets the terminal build exit code.
        /// </summary>
        public int? ExitCode { get; set; }
    }
}
