namespace helengine.vfx {
    /// <summary>
    /// Pairs a source color image sequence with a matching alpha mask image sequence.
    /// </summary>
    public class VfxClip {
        /// <summary>
        /// Sequence carrying the subject's color data.
        /// </summary>
        public ImageSequence Source { get; }

        /// <summary>
        /// Sequence carrying the matte whose alpha channel keys the subject out of its plate.
        /// </summary>
        public ImageSequence Mask { get; }

        /// <summary>
        /// Number of frames the clip spans; guaranteed identical for source and mask.
        /// </summary>
        public int FrameCount => Source.FrameCount;

        /// <summary>
        /// Pixel width of the clip; guaranteed identical for source and mask.
        /// </summary>
        public int Width => Source.Width;

        /// <summary>
        /// Pixel height of the clip; guaranteed identical for source and mask.
        /// </summary>
        public int Height => Source.Height;

        /// <summary>
        /// Pairs a source and mask sequence, rejecting any frame-count or resolution mismatch up front.
        /// </summary>
        /// <param name="source">Sequence carrying the subject's color data.</param>
        /// <param name="mask">Sequence carrying the matte alpha for the same frames.</param>
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
