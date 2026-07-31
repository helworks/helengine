namespace helengine {
    /// <summary>
    /// Marks a persisted member as an ordinal payload extension that follows all existing members.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class ScenePersistenceAppendAttribute : Attribute {
    }
}
