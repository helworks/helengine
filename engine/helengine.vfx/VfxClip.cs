namespace helengine.vfx {
    /// <summary>
    /// Pairs a source color image sequence with a matching alpha mask image sequence.
    /// </summary>
    public class VfxClip {
        public ImageSequence Source { get; }
        public ImageSequence Mask { get; }

        public int FrameCount => Source.FrameCount;
        public int Width => Source.Width;
        public int Height => Source.Height;

        public VfxClip(ImageSequence source, ImageSequence mask) {
            if (source == null) {
                throw new ArgumentNullException(nameof(source));
            }
            if (mask == null) {
                throw new ArgumentNullException(nameof(mask));
            }
            if (source.FrameCount != mask.FrameCount) {
                throw new InvalidOperationException(
                    $"Source sequence has {source.FrameCount} frames but mask sequence has {mask.FrameCount} frames. They must match.");
            }
            if (source.Width != mask.Width || source.Height != mask.Height) {
                throw new InvalidOperationException(
                    $"Source sequence resolution {source.Width}x{source.Height} does not match mask sequence resolution {mask.Width}x{mask.Height}.");
            }

            Source = source;
            Mask = mask;
        }
    }
}
