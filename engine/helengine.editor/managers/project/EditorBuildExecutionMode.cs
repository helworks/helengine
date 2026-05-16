namespace helengine.editor {
    /// <summary>
    /// Describes how one queued build should finish after the normal export/package phases complete.
    /// </summary>
    public enum EditorBuildExecutionMode {
        /// <summary>
        /// Produces the normal packaged runtime output and stops.
        /// </summary>
        Runtime = 0,

        /// <summary>
        /// Produces the normal packaged runtime output and then launches a host-debug runner.
        /// </summary>
        HostDebug = 1
    }
}
