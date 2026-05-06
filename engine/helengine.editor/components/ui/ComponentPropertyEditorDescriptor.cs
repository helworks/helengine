using System.Reflection;

namespace helengine.editor {
    /// <summary>
    /// Describes one provider-backed custom property editor.
    /// </summary>
    public class ComponentPropertyEditorDescriptor {
        /// <summary>
        /// Initializes a new custom property editor descriptor.
        /// </summary>
        /// <param name="property">Reflected property metadata.</param>
        /// <param name="displayName">Display label shown in the inspector.</param>
        /// <param name="editorTypeId">Stable editor type identifier.</param>
        /// <param name="order">Display order used during sorting.</param>
        public ComponentPropertyEditorDescriptor(PropertyInfo property, string displayName, string editorTypeId, int order) {
            Property = property ?? throw new ArgumentNullException(nameof(property));
            if (string.IsNullOrWhiteSpace(displayName)) {
                throw new ArgumentException("Display name must be provided.", nameof(displayName));
            }
            if (string.IsNullOrWhiteSpace(editorTypeId)) {
                throw new ArgumentException("Editor type id must be provided.", nameof(editorTypeId));
            }

            DisplayName = displayName;
            EditorTypeId = editorTypeId;
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
        /// Gets the stable editor type identifier.
        /// </summary>
        public string EditorTypeId { get; }

        /// <summary>
        /// Gets the display order used during sorting.
        /// </summary>
        public int Order { get; }
    }
}
