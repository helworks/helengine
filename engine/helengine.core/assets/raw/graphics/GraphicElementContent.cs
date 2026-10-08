namespace helengine {
    /// <summary>
    /// Selects what one template element draws.
    /// </summary>
    public enum GraphicElementContent {
        /// <summary>
        /// The text of the item the element belongs to.
        /// </summary>
        ItemText = 0,

        /// <summary>
        /// The separator text supplied by the edit or the separator slot default.
        /// </summary>
        SeparatorText = 1,

        /// <summary>
        /// A solid bar as wide as the item text, such as a strike-through line; drawn without glyphs.
        /// </summary>
        Bar = 2
    }
}
