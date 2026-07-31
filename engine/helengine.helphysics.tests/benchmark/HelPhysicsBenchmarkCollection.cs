namespace helengine {
    /// <summary>
    /// Serializes process-wide collection and timing work so benchmark samples cannot overlap unrelated test collections.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class HelPhysicsBenchmarkCollection {
        /// <summary>
        /// Stores the stable xUnit collection name used by benchmark contract tests.
        /// </summary>
        public const string Name = "HelPhysicsBenchmarkTests";
    }
}
