using System.Reflection;

namespace helengine.editor {
    /// <summary>
    /// Provides custom editor descriptors for complex reflected component properties.
    /// </summary>
    public interface IComponentPropertyEditorProvider {
        /// <summary>
        /// Tries to create a custom editor descriptor for one reflected property.
        /// </summary>
        /// <param name="property">Reflected property metadata.</param>
        /// <param name="descriptor">Resolved custom editor descriptor when supported.</param>
        /// <returns>True when the property is handled by this provider.</returns>
        bool TryCreateDescriptor(PropertyInfo property, out ComponentPropertyEditorDescriptor descriptor);
    }
}
