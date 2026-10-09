namespace helengine.timeline {
    /// <summary>
    /// Describes what a binding slot is bound to when the timeline plays.
    /// </summary>
    public enum TimelineSlotKind {
        /// <summary>
        /// A scene entity (game) or a generic element; accepts every track kind that needs a slot.
        /// </summary>
        Entity = 0,

        /// <summary>
        /// A text element, such as one term of a comparison in a motion graphic.
        /// </summary>
        Text = 1,

        /// <summary>
        /// An image or video element.
        /// </summary>
        Media = 2,

        /// <summary>
        /// A solid-color rectangle, such as a strike bar or a highlight box.
        /// </summary>
        Rect = 3
    }
}
