namespace helengine.vfx {
    /// <summary>
    /// Groups the named input image sequences one effect run needs (e.g. a subject's color plate and
    /// matte, or a subject plus a 3D render's color and depth), keyed by the same role names the effect
    /// declares in <see cref="IVfxEffect.InputRoles"/>.
    /// </summary>
    public class VfxClip {
        /// <summary>
        /// Every input sequence, keyed by role name.
        /// </summary>
        public IReadOnlyDictionary<string, ImageSequence> Sequences { get; }

        /// <summary>
        /// Number of frames the clip spans; guaranteed identical across every sequence.
        /// </summary>
        public int FrameCount { get; }

        /// <summary>
        /// Pixel width of the clip; guaranteed identical across every sequence.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Pixel height of the clip; guaranteed identical across every sequence.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Groups a set of named sequences into one clip, rejecting any frame-count or resolution
        /// mismatch between them up front.
        /// </summary>
        /// <param name="sequences">Input sequences keyed by role name; must contain at least one entry.</param>
        public VfxClip(IReadOnlyDictionary<string, ImageSequence> sequences) {
            if (sequences == null) {
                throw new ArgumentNullException(nameof(sequences));
            }
            if (sequences.Count == 0) {
                throw new ArgumentException("VfxClip must be given at least one input sequence.", nameof(sequences));
            }

            string firstRole = null;
            ImageSequence firstSequence = null;
            foreach (KeyValuePair<string, ImageSequence> entry in sequences) {
                if (firstSequence == null) {
                    firstRole = entry.Key;
                    firstSequence = entry.Value;
                    continue;
                }
                if (entry.Value.FrameCount != firstSequence.FrameCount) {
                    throw new InvalidOperationException(
                        $"Input '{entry.Key}' has {entry.Value.FrameCount} frames but input '{firstRole}' has {firstSequence.FrameCount} frames. Every input sequence must match.");
                }
                if (entry.Value.Width != firstSequence.Width || entry.Value.Height != firstSequence.Height) {
                    throw new InvalidOperationException(
                        $"Input '{entry.Key}' resolution {entry.Value.Width}x{entry.Value.Height} does not match input '{firstRole}' resolution {firstSequence.Width}x{firstSequence.Height}.");
                }
            }

            Sequences = sequences;
            FrameCount = firstSequence.FrameCount;
            Width = firstSequence.Width;
            Height = firstSequence.Height;
        }

        /// <summary>
        /// Looks up one input sequence by its role name.
        /// </summary>
        /// <param name="role">Role name to look up, matching an entry in <see cref="IVfxEffect.InputRoles"/>.</param>
        /// <returns>The sequence registered for that role.</returns>
        public ImageSequence GetSequence(string role) {
            if (!Sequences.TryGetValue(role, out ImageSequence sequence)) {
                throw new InvalidOperationException($"VfxClip has no input sequence for role '{role}'.");
            }
            return sequence;
        }
    }
}
