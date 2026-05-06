using System.Reflection;

namespace helengine.editor {
    /// <summary>
    /// Describes one property that can be rendered by the default reflected component inspector.
    /// </summary>
    public class ReflectedComponentPropertyDescriptor {
        /// <summary>
        /// Initializes a new reflected property descriptor.
        /// </summary>
        /// <param name="property">Reflected property metadata.</param>
        /// <param name="displayName">Display label shown in the inspector.</param>
        /// <param name="rowKind">Row kind used to render the property.</param>
        /// <param name="order">Explicit display order.</param>
        public ReflectedComponentPropertyDescriptor(PropertyInfo property, string displayName, ComponentPropertyRowKind rowKind, int order) {
            Property = property ?? throw new ArgumentNullException(nameof(property));
            if (string.IsNullOrWhiteSpace(displayName)) {
                throw new ArgumentException("Display name must be provided.", nameof(displayName));
            }

            DisplayName = displayName;
            RowKind = rowKind;
            Order = order;
        }

        /// <summary>
        /// Initializes a new provider-backed reflected property descriptor.
        /// </summary>
        /// <param name="property">Reflected property metadata.</param>
        /// <param name="displayName">Display label shown in the inspector.</param>
        /// <param name="customEditor">Custom editor descriptor used to render the property.</param>
        /// <param name="order">Explicit display order.</param>
        public ReflectedComponentPropertyDescriptor(PropertyInfo property, string displayName, ComponentPropertyEditorDescriptor customEditor, int order) {
            Property = property ?? throw new ArgumentNullException(nameof(property));
            if (string.IsNullOrWhiteSpace(displayName)) {
                throw new ArgumentException("Display name must be provided.", nameof(displayName));
            }
            if (customEditor == null) {
                throw new ArgumentNullException(nameof(customEditor));
            }

            DisplayName = displayName;
            CustomEditor = customEditor;
            RowKind = ComponentPropertyRowKind.CustomSection;
            Order = order;
        }

        /// <summary>
        /// Gets the reflected property metadata.
        /// </summary>
        public PropertyInfo Property { get; }

        /// <summary>
        /// Gets the display label shown in the inspector.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the row kind used to render the property.
        /// </summary>
        public ComponentPropertyRowKind RowKind { get; }

        /// <summary>
        /// Gets the provider-backed custom editor descriptor when one is used.
        /// </summary>
        public ComponentPropertyEditorDescriptor CustomEditor { get; }

        /// <summary>
        /// Gets a value indicating whether this descriptor is rendered by a provider-backed custom editor.
        /// </summary>
        public bool IsCustomEditor => CustomEditor != null;

        /// <summary>
        /// Gets the explicit display order.
        /// </summary>
        public int Order { get; }
    }
}
