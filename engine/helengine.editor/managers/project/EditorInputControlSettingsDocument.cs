namespace helengine.editor {
    /// <summary>
    /// Represents one persisted physical control reference used by project-shared input settings.
    /// </summary>
    public sealed class EditorInputControlSettingsDocument {
        /// <summary>
        /// Gets or sets the family that owns the configured control.
        /// </summary>
        public InputDeviceKind DeviceKind { get; set; }

        /// <summary>
        /// Gets or sets the data shape exposed by the configured control.
        /// </summary>
        public InputControlKind ControlKind { get; set; }

        /// <summary>
        /// Gets or sets the zero-based device index within the selected family.
        /// </summary>
        public int DeviceIndex { get; set; }

        /// <summary>
        /// Gets or sets the zero-based control index within the selected device.
        /// </summary>
        public int ControlIndex { get; set; }
    }
}
