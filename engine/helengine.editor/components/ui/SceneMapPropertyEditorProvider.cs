using System.Reflection;

namespace helengine.editor {
    /// <summary>
    /// Resolves the provider-backed custom editor used for scene-map dictionary properties.
    /// </summary>
    public sealed class SceneMapPropertyEditorProvider : IComponentPropertyEditorProvider {
        /// <summary>
        /// Stable editor type identifier for scene-map dictionary rows.
        /// </summary>
        public const string EditorTypeId = "SceneMap";

        /// <summary>
        /// Tries to create the nested editor descriptor for one reflected property.
        /// </summary>
        /// <param name="property">Reflected property metadata.</param>
        /// <param name="descriptor">Resolved descriptor when supported.</param>
        /// <returns>True when the property is the scene-map dictionary property.</returns>
        public bool TryCreateDescriptor(PropertyInfo property, out ComponentPropertyEditorDescriptor descriptor) {
            if (property == null) {
                throw new ArgumentNullException(nameof(property));
            }

            descriptor = null;
            if (property.DeclaringType != typeof(SceneMapComponent)) {
                return false;
            }
            if (!string.Equals(property.Name, nameof(SceneMapComponent.Mappings), StringComparison.Ordinal)) {
                return false;
            }
            if (property.PropertyType != typeof(Dictionary<string, string>)) {
                return false;
            }

            descriptor = new ComponentPropertyEditorDescriptor(
                property,
                ResolveDisplayName(property),
                EditorTypeId,
                ResolveOrder(property));
            return true;
        }

        /// <summary>
        /// Resolves the display label for one provider-backed property.
        /// </summary>
        /// <param name="property">Property metadata being inspected.</param>
        /// <returns>Display label shown by the inspector.</returns>
        string ResolveDisplayName(PropertyInfo property) {
            EditorPropertyDisplayNameAttribute attribute = property.GetCustomAttribute<EditorPropertyDisplayNameAttribute>(true);
            if (attribute != null) {
                return attribute.DisplayName;
            }

            return property.Name;
        }

        /// <summary>
        /// Resolves the explicit display order for one provider-backed property.
        /// </summary>
        /// <param name="property">Property metadata being inspected.</param>
        /// <returns>Display order used during sorting.</returns>
        int ResolveOrder(PropertyInfo property) {
            EditorPropertyOrderAttribute attribute = property.GetCustomAttribute<EditorPropertyOrderAttribute>(true);
            if (attribute != null) {
                return attribute.Order;
            }

            return int.MaxValue;
        }
    }
}
