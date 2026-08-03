namespace helengine.vfx {
    /// <summary>
    /// An ordered sequence of image frame file paths that make up one clip.
    /// </summary>
    public class ImageSequence {
        /// <summary>
        /// Absolute frame file paths in playback order (lowest frame index first).
        /// </summary>
        public IReadOnlyList<string> FramePaths { get; }

        /// <summary>
        /// Pixel width every frame in the sequence is expected to have.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Pixel height every frame in the sequence is expected to have.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Optional playback frame rate; null when the source did not carry timing information.
        /// </summary>
        public double? FrameRate { get; }

        /// <summary>
        /// Number of frames in the sequence.
        /// </summary>
        public int FrameCount => FramePaths.Count;

        /// <summary>
        /// Initializes a sequence from an already ordered set of frame paths and its shared resolution.
        /// </summary>
        /// <param name="framePaths">Frame file paths in playback order; must contain at least one entry.</param>
        /// <param name="width">Pixel width shared by every frame.</param>
        /// <param name="height">Pixel height shared by every frame.</param>
        /// <param name="frameRate">Optional playback frame rate when known.</param>
        public ImageSequence(IReadOnlyList<string> framePaths, int width, int height, double? frameRate = null) {
            if (framePaths == null || framePaths.Count == 0) {
                throw new ArgumentException("Image sequence must contain at least one frame.", nameof(framePaths));
            }
            if (width <= 0) {
                throw new ArgumentOutOfRangeException(nameof(width), "Image sequence width must be positive.");
            }
            if (height <= 0) {
                throw new ArgumentOutOfRangeException(nameof(height), "Image sequence height must be positive.");
            }

            FramePaths = framePaths;
            Width = width;
            Height = height;
            FrameRate = frameRate;
        }
    }
}
