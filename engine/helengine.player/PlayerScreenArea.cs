namespace helengine.player {
    /// <summary>
    /// A monitor work area in virtual-screen pixels (the monitor minus the taskbar).
    /// </summary>
    public sealed class PlayerScreenArea {
        /// <summary>
        /// Initializes one work area.
        /// </summary>
        /// <param name="left">Left edge in virtual-screen pixels.</param>
        /// <param name="top">Top edge in virtual-screen pixels.</param>
        /// <param name="width">Positive width.</param>
        /// <param name="height">Positive height.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the size is not positive.</exception>
        public PlayerScreenArea(int left, int top, int width, int height) {
            if (width <= 0) {
                throw new ArgumentOutOfRangeException(nameof(width), width, "A work area needs a positive width.");
            } else if (height <= 0) {
                throw new ArgumentOutOfRangeException(nameof(height), height, "A work area needs a positive height.");
            }

            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }

        /// <summary>Gets the left edge.</summary>
        public int Left { get; }

        /// <summary>Gets the top edge.</summary>
        public int Top { get; }

        /// <summary>Gets the width.</summary>
        public int Width { get; }

        /// <summary>Gets the height.</summary>
        public int Height { get; }
    }
}
