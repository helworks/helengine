namespace helengine {
    /// <summary>
    /// Marks a component member as runtime-only so the reflected scene persistence fallback does not include it in saved payloads.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class ScenePersistenceIgnoreAttribute : Attribute {
    }
}
