namespace helengine.media {
    /// <summary>
    /// Describes one value an edit fills when it uses a graphic template, with the limits that keep the result readable.
    /// </summary>
    public sealed class MediaGraphicSlotDescriptor {
        /// <summary>
        /// Gets or sets the slot name used in the edit, such as <c>items</c>.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Gets or sets the value shape: <c>text_list</c>, <c>text</c> or <c>item_index</c>.
        /// </summary>
        public string Kind { get; set; } = "text_list";

        /// <summary>
        /// Gets or sets the explanation given to planners and editors.
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Gets or sets whether the edit must fill the slot.
        /// </summary>
        public bool Required { get; set; }

        /// <summary>
        /// Gets or sets the fewest entries of a <c>text_list</c> slot.
        /// </summary>
        public int MinCount { get; set; }

        /// <summary>
        /// Gets or sets the most entries of a <c>text_list</c> slot.
        /// </summary>
        public int MaxCount { get; set; }

        /// <summary>
        /// Gets or sets the longest text one entry may have, in characters.
        /// </summary>
        public int MaxChars { get; set; }

        /// <summary>
        /// Gets or sets the text used when a <c>text</c> slot is left empty.
        /// </summary>
        public string Default { get; set; } = "";
    }
}
