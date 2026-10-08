namespace helengine {
    /// <summary>
    /// Describes what an edit supplies for one graphic template slot.
    /// </summary>
    public enum GraphicTemplateSlotKind {
        /// <summary>
        /// An ordered list of short texts, such as the terms of a comparison; each entry becomes one animated item.
        /// </summary>
        TextList = 0,

        /// <summary>
        /// One short text, such as the separator drawn between items.
        /// </summary>
        Text = 1,

        /// <summary>
        /// A zero-based index into the item list, such as the item that receives the accent.
        /// </summary>
        ItemIndex = 2
    }
}
