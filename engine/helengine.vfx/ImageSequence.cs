namespace helengine.vfx {
    /// <summary>
    /// An ordered sequence of image frame file paths that make up one clip.
    /// </summary>
    public class ImageSequence {
        public IReadOnlyList<string> FramePaths { get; }
        public int Width { get; }
        public int Height { get; }
        public double? FrameRate { get; }

        public int FrameCount => FramePaths.Count;

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
