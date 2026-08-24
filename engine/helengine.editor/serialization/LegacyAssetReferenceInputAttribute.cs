namespace helengine.editor {
    /// <summary>
    /// Marks a string property retained only as a legacy asset-reference migration input.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class LegacyAssetReferenceInputAttribute : Attribute {
    }
}
