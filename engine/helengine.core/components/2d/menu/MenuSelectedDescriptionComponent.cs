namespace helengine {
    /// <summary>
    /// Marks the text entity that should display the currently selected item description for one baked menu panel.
    /// </summary>
    public class MenuSelectedDescriptionComponent : Component {
        /// <summary>
        /// Current payload version used by scene persistence for the selected-description marker.
        /// </summary>
        public const byte CurrentVersion = 1;

        /// <summary>
        /// Stable serialized component type id used by the selected-description marker.
        /// </summary>
        public const string SerializedComponentTypeId = "helengine.MenuSelectedDescriptionComponent";
    }
}
