namespace helengine.editor {
    /// <summary>
    /// Identifies the visual layout used for a component property row.
    /// </summary>
    public enum ComponentPropertyRowKind {
        /// <summary>
        /// Header row used to label a component section.
        /// </summary>
        Header,
        /// <summary>
        /// Editable row for a Vector3 value.
        /// </summary>
        Vector3,
        /// <summary>
        /// Editable row for a material asset selection.
        /// </summary>
        Material,
        /// <summary>
        /// Editable row for a scalar value.
        /// </summary>
        Scalar,
        /// <summary>
        /// Read-only row for unsupported property types.
        /// </summary>
        ReadOnly
    }
}
