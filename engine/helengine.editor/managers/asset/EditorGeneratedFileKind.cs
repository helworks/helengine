namespace helengine.editor {
    /// <summary>
    /// Identity-less project files that may be staged by a generated authoring
    /// transaction. The bytes and caller-supplied prior hash are the complete
    /// publication contract.
    /// </summary>
    public enum EditorGeneratedFileKind {
        Source,
        ImportSettings,
        Cache,
        IdentityMetadata
    }
}
