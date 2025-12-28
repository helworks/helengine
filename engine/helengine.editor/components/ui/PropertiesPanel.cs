namespace helengine.editor {
    /// <summary>
    /// Dockable panel intended to show editable properties for the current selection.
    /// </summary>
    public class PropertiesPanel : DockableEntity {
        /// <summary>
        /// Initializes a new properties panel with the provided font.
        /// </summary>
        /// <param name="font">Font used for the title bar.</param>
        public PropertiesPanel(FontAsset font) : base(font) {
            Title = "Property Manager";
            MinSize = new int2(220, 160);
        }
    }
}
