using System.Reflection;

namespace helengine.editor {
    /// <summary>
    /// Captures one property mutation at the moment an inspector control commits its value.
    /// </summary>
    public sealed class ComponentPropertyEditRequest {
        /// <summary>
        /// Initializes an immutable property edit request with its target and persistence scope.
        /// </summary>
        /// <param name="ownerEntity">Editor entity that owns the edited component, or null when no entity history is available.</param>
        /// <param name="commonComponent">Common component used as the stable override identity.</param>
        /// <param name="targetComponent">Effective component instance receiving the value.</param>
        /// <param name="saveComponent">Hidden save component that stores override metadata, or null for common-only edits.</param>
        /// <param name="property">Writable property metadata resolved by the inspector descriptor.</param>
        /// <param name="memberName">Stable member name shown to the mutation boundary.</param>
        /// <param name="value">Value to assign to the property.</param>
        /// <param name="scope">Platform or platform/environment scope receiving the edit.</param>
        /// <param name="propertyPath">Serialized override path for the edited member.</param>
        /// <param name="isReadOnly">Whether the source inspector is currently read-only.</param>
        public ComponentPropertyEditRequest(
            EditorEntity ownerEntity,
            Component commonComponent,
            Component targetComponent,
            EntitySaveComponent saveComponent,
            PropertyInfo property,
            string memberName,
            object value,
            EditorOverrideScope scope,
            string propertyPath,
            bool isReadOnly) {
            if (property == null) {
                throw new ArgumentNullException(nameof(property));
            }
            if (string.IsNullOrWhiteSpace(memberName)) {
                throw new ArgumentException("Member name must be provided.", nameof(memberName));
            }

            OwnerEntity = ownerEntity;
            CommonComponent = commonComponent;
            TargetComponent = targetComponent ?? throw new ArgumentNullException(nameof(targetComponent));
            SaveComponent = saveComponent;
            Property = property;
            MemberName = memberName;
            Value = value;
            Scope = scope;
            PropertyPath = propertyPath;
            IsReadOnly = isReadOnly;
        }

        /// <summary>
        /// Gets the editor entity whose history should contain this mutation.
        /// </summary>
        public EditorEntity OwnerEntity { get; }

        /// <summary>
        /// Gets the common component that identifies the override payload.
        /// </summary>
        public Component CommonComponent { get; }

        /// <summary>
        /// Gets the effective component instance receiving the value.
        /// </summary>
        public Component TargetComponent { get; }

        /// <summary>
        /// Gets the hidden component that stores platform override state.
        /// </summary>
        public EntitySaveComponent SaveComponent { get; }

        /// <summary>
        /// Gets the writable property metadata supplied by the inspector descriptor.
        /// </summary>
        public PropertyInfo Property { get; }

        /// <summary>
        /// Gets the stable component member name.
        /// </summary>
        public string MemberName { get; }

        /// <summary>
        /// Gets the value to assign.
        /// </summary>
        public object Value { get; }

        /// <summary>
        /// Gets the override scope captured when the edit began.
        /// </summary>
        public EditorOverrideScope Scope { get; }

        /// <summary>
        /// Gets the serialized property path used to mark platform overrides.
        /// </summary>
        public string PropertyPath { get; }

        /// <summary>
        /// Gets whether applying the request is prohibited by the current inspector mode.
        /// </summary>
        public bool IsReadOnly { get; }
    }
}
