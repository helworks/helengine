namespace helengine.player {
    /// <summary>
    /// The computed Play window: integer multiplier, client size and outer-window position.
    /// </summary>
    public sealed class PlayerWindowLayoutResult {
        /// <summary>
        /// Initializes one layout.
        /// </summary>
        /// <param name="multiplier">Integer magnification of the render size.</param>
        /// <param name="clientWidth">Client width in pixels.</param>
        /// <param name="clientHeight">Client height in pixels.</param>
        /// <param name="left">Outer window left edge.</param>
        /// <param name="top">Outer window top edge.</param>
        /// <param name="exceedsWorkArea">Whether the outer window is larger than the work area.</param>
        public PlayerWindowLayoutResult(int multiplier, int clientWidth, int clientHeight, int left, int top, bool exceedsWorkArea) {
            Multiplier = multiplier;
            ClientWidth = clientWidth;
            ClientHeight = clientHeight;
            Left = left;
            Top = top;
            ExceedsWorkArea = exceedsWorkArea;
        }

        /// <summary>Gets the integer magnification of the render size.</summary>
        public int Multiplier { get; }

        /// <summary>Gets the client width.</summary>
        public int ClientWidth { get; }

        /// <summary>Gets the client height.</summary>
        public int ClientHeight { get; }

        /// <summary>Gets the outer window left edge.</summary>
        public int Left { get; }

        /// <summary>Gets the outer window top edge.</summary>
        public int Top { get; }

        /// <summary>Gets whether the outer window is larger than the work area.</summary>
        public bool ExceedsWorkArea { get; }
    }
}
