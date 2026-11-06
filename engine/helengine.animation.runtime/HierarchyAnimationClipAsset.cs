namespace helengine.animation.runtime {
    /// <summary>
    /// A hierarchical animation clip (<c>.hanimclip</c>): one <see cref="HierarchyAnimationTrack"/> per animated entity
    /// of a hierarchy, played by <see cref="HierarchyAnimationPlayerComponent"/>. Unlike core's
    /// <see cref="AnimationClipAsset"/>, which animates only the entity carrying its player, each track here drives a
    /// different descendant of the player's entity.
    /// </summary>
    public class HierarchyAnimationClipAsset : Asset, IDisposable {
        /// <summary>
        /// File extension of packaged hierarchy animation clips.
        /// </summary>
        public const string FileExtension = ".hanimclip";

        /// <summary>
        /// Version of the payload layout written after the HELE header.
        /// </summary>
        public const byte LayoutVersion = 1;

        /// <summary>
        /// Gets or sets the clip length in seconds; playback and seeking stay inside <c>[0, Duration]</c>.
        /// </summary>
        public double Duration { get; set; }

        /// <summary>
        /// Gets or sets the tracks, one per animated entity.
        /// </summary>
        public HierarchyAnimationTrack[] Tracks { get; set; } = Array.Empty<HierarchyAnimationTrack>();

        /// <summary>
        /// Releases the tracks and their keys. Each player owns the clip it loaded and disposes it with itself.
        /// </summary>
        public void Dispose() {
            HierarchyAnimationTrack[] tracks = Tracks;
            Tracks = null;
            DisposeOwnedTracks(tracks);
        }

        /// <summary>
        /// Disposes and deletes clip-owned tracks and their container while preserving the shared empty-array singleton.
        /// </summary>
        /// <param name="tracks">Tracks owned by this clip.</param>
        static void DisposeOwnedTracks(HierarchyAnimationTrack[] tracks) {
            if (tracks == null || ReferenceEquals(tracks, Array.Empty<HierarchyAnimationTrack>())) {
                return;
            }

            for (int index = 0; index < tracks.Length; index++) {
                NativeOwnership.DisposeAndDelete(tracks[index]);
            }
            NativeOwnership.Delete(tracks);
        }
    }
}
