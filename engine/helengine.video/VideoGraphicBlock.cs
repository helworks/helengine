namespace helengine.video {
    /// <summary>
    /// One laid-out block of a graphic: an item or the separator after an item, with its measured size and its center
    /// in output pixels once arranged.
    /// </summary>
    public sealed class VideoGraphicBlock {
        /// <summary>
        /// Gets the item index, or for a separator the index of the item before it.
        /// </summary>
        public int ItemIndex { get; }

        /// <summary>
        /// Gets whether the block holds the separator after <see cref="ItemIndex"/> rather than the item itself.
        /// </summary>
        public bool IsSeparator { get; }

        /// <summary>
        /// Gets the text the block measures.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Gets the font size of the block relative to the item font size.
        /// </summary>
        public double FontScale { get; }

        /// <summary>
        /// Gets or sets the measured advance width in output pixels at the arranged size.
        /// </summary>
        public double Width { get; set; }

        /// <summary>
        /// Gets or sets the measured line box height in output pixels at the arranged size.
        /// </summary>
        public double Height { get; set; }

        /// <summary>
        /// Gets or sets the horizontal center in output pixels.
        /// </summary>
        public double CenterX { get; set; }

        /// <summary>
        /// Gets or sets the vertical center in output pixels.
        /// </summary>
        public double CenterY { get; set; }

        /// <summary>
        /// Creates one block before measurement.
        /// </summary>
        /// <param name="itemIndex">Item index, or the item before a separator.</param>
        /// <param name="isSeparator">Whether the block is a separator.</param>
        /// <param name="text">Text to measure.</param>
        /// <param name="fontScale">Font size relative to the item font size.</param>
        public VideoGraphicBlock(int itemIndex, bool isSeparator, string text, double fontScale) {
            ItemIndex = itemIndex;
            IsSeparator = isSeparator;
            Text = text;
            FontScale = fontScale;
        }
    }
}
