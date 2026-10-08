namespace helengine {
    /// <summary>
    /// Declares one value an edit fills when it uses a graphic template, such as the item list, the separator text or
    /// the accent item, together with the limits that keep the result readable.
    /// </summary>
    public class GraphicTemplateSlotAsset : IDisposable {
        /// <summary>
        /// Gets or sets the slot name edits use, such as <c>items</c>, <c>separator</c> or <c>accent_item</c>.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the explanation given to planners and editors.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets what the edit supplies for this slot.
        /// </summary>
        public GraphicTemplateSlotKind Kind { get; set; } = GraphicTemplateSlotKind.TextList;

        /// <summary>
        /// Gets or sets whether an edit must fill the slot.
        /// </summary>
        public bool Required { get; set; }

        /// <summary>
        /// Gets or sets the fewest entries a <see cref="GraphicTemplateSlotKind.TextList"/> slot accepts.
        /// </summary>
        public int MinCount { get; set; }

        /// <summary>
        /// Gets or sets the most entries a <see cref="GraphicTemplateSlotKind.TextList"/> slot accepts.
        /// </summary>
        public int MaxCount { get; set; }

        /// <summary>
        /// Gets or sets the longest text, in characters, one entry may have.
        /// </summary>
        public int MaxChars { get; set; }

        /// <summary>
        /// Gets or sets the text used when a <see cref="GraphicTemplateSlotKind.Text"/> slot is left empty.
        /// </summary>
        public string DefaultText { get; set; } = string.Empty;

        /// <summary>
        /// Releases nothing; a slot owns no nested allocations beyond its strings.
        /// </summary>
        public void Dispose() { }
    }
}
