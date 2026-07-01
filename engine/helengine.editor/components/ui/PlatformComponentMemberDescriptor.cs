namespace helengine.editor {
    /// <summary>
    /// Describes one synthetic platform-specific component member row shown by the default inspector.
    /// </summary>
    public sealed class PlatformComponentMemberDescriptor {
        /// <summary>
        /// Initializes one synthetic platform-specific component member descriptor.
        /// </summary>
        /// <param name="definition">Underlying builder-owned synthetic member definition.</param>
        /// <param name="rowKind">Default inspector row kind used to render the member.</param>
        /// <param name="valueType">Managed runtime type edited by the row.</param>
        public PlatformComponentMemberDescriptor(
            helengine.baseplatform.Definitions.PlatformComponentMemberDefinition definition,
            ComponentPropertyRowKind rowKind,
            Type valueType) {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
            RowKind = rowKind;
        }

        /// <summary>
        /// Gets the underlying builder-owned synthetic member definition.
        /// </summary>
        public helengine.baseplatform.Definitions.PlatformComponentMemberDefinition Definition { get; }

        /// <summary>
        /// Gets the default inspector row kind used to render the member.
        /// </summary>
        public ComponentPropertyRowKind RowKind { get; }

        /// <summary>
        /// Gets the managed runtime type edited by the row.
        /// </summary>
        public Type ValueType { get; }
    }
}
