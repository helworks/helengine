namespace helengine.editor {
    /// <summary>
    /// Stores panel type registrations that can be created through the workspace UI.
    /// </summary>
    public sealed class EditorWorkspacePanelRegistry {
        /// <summary>
        /// Panel descriptors keyed by stable type identifier.
        /// </summary>
        readonly Dictionary<string, EditorWorkspacePanelTypeDescriptor> descriptorsByTypeId = new Dictionary<string, EditorWorkspacePanelTypeDescriptor>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Registers one panel type descriptor.
        /// </summary>
        /// <param name="descriptor">Panel descriptor to register.</param>
        public void Register(EditorWorkspacePanelTypeDescriptor descriptor) {
            if (descriptor == null) {
                throw new ArgumentNullException(nameof(descriptor));
            }

            descriptorsByTypeId[descriptor.PanelTypeId] = descriptor;
        }

        /// <summary>
        /// Gets one registered panel descriptor by type identifier.
        /// </summary>
        /// <param name="panelTypeId">Stable panel type identifier.</param>
        /// <returns>Registered panel descriptor.</returns>
        public EditorWorkspacePanelTypeDescriptor GetDescriptor(string panelTypeId) {
            if (string.IsNullOrWhiteSpace(panelTypeId)) {
                throw new ArgumentException("Panel type identifier must be provided.", nameof(panelTypeId));
            }
            if (!descriptorsByTypeId.TryGetValue(panelTypeId, out EditorWorkspacePanelTypeDescriptor descriptor)) {
                throw new InvalidOperationException($"Panel type '{panelTypeId}' is not registered.");
            }

            return descriptor;
        }

        /// <summary>
        /// Attempts to resolve one registered panel descriptor by type identifier.
        /// </summary>
        /// <param name="panelTypeId">Stable panel type identifier.</param>
        /// <param name="descriptor">Resolved descriptor when the type is registered.</param>
        /// <returns>True when the panel type is registered; otherwise false.</returns>
        public bool TryGetDescriptor(string panelTypeId, out EditorWorkspacePanelTypeDescriptor descriptor) {
            if (string.IsNullOrWhiteSpace(panelTypeId)) {
                throw new ArgumentException("Panel type identifier must be provided.", nameof(panelTypeId));
            }

            return descriptorsByTypeId.TryGetValue(panelTypeId, out descriptor);
        }
    }
}
