namespace helengine {
    /// <summary>Connects a render proxy to the authored entity whose hierarchy determines equal-depth composition.</summary>
    public interface IRenderHierarchySource {
        /// <summary>Gets the authored entity represented by this render proxy.</summary>
        Entity SourceEntity { get; }
    }
}
