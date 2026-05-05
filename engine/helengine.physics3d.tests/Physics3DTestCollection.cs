namespace helengine.physics3d.tests {
    /// <summary>
    /// Groups 3D physics tests that rely on the global core singleton so they do not run in parallel.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class Physics3DTestCollection {
        /// <summary>
        /// Stable xUnit collection name used by 3D physics tests that require exclusive access to the core singleton.
        /// </summary>
        public const string Name = "Physics3DCoreTests";
    }
}
