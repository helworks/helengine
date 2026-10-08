namespace helengine {
    /// <summary>
    /// Represents one keyframe-based animation clip containing multiple typed transform tracks.
    /// </summary>
    public class AnimationClipAsset : Asset, IDisposable {
        /// <summary>
        /// Gets or sets the authored clip duration in seconds.
        /// </summary>
        public float Duration { get; set; }

        /// <summary>
        /// Gets or sets the absolute position tracks stored by this clip.
        /// </summary>
        public PositionKeyframeTrackAsset[] PositionTracks { get; set; } = Array.Empty<PositionKeyframeTrackAsset>();

        /// <summary>
        /// Gets or sets the additive position-offset tracks stored by this clip.
        /// </summary>
        public PositionOffsetKeyframeTrackAsset[] PositionOffsetTracks { get; set; } = Array.Empty<PositionOffsetKeyframeTrackAsset>();

        /// <summary>
        /// Gets or sets the absolute scale tracks stored by this clip.
        /// </summary>
        public ScaleKeyframeTrackAsset[] ScaleTracks { get; set; } = Array.Empty<ScaleKeyframeTrackAsset>();

        /// <summary>
        /// Gets or sets the absolute rotation tracks stored by this clip.
        /// </summary>
        public RotationKeyframeTrackAsset[] RotationTracks { get; set; } = Array.Empty<RotationKeyframeTrackAsset>();

        /// <summary>
        /// Gets or sets the platform-authored override payloads that specialize this clip per target platform.
        /// </summary>
        public AnimationClipPlatformOverrideAsset[] PlatformOverrides { get; set; } = Array.Empty<AnimationClipPlatformOverrideAsset>();

        /// <summary>Releases the clip's deserialized tree after all animators borrowing it have been disposed.</summary>
        public virtual void Dispose() {
            PositionKeyframeTrackAsset[] positions = PositionTracks;
            PositionOffsetKeyframeTrackAsset[] offsets = PositionOffsetTracks;
            ScaleKeyframeTrackAsset[] scales = ScaleTracks;
            RotationKeyframeTrackAsset[] rotations = RotationTracks;
            AnimationClipPlatformOverrideAsset[] overrides = PlatformOverrides;
            string[] formerIds = FormerAuthoringAssetIds;
            PositionTracks = null;
            PositionOffsetTracks = null;
            ScaleTracks = null;
            RotationTracks = null;
            PlatformOverrides = null;
            FormerAuthoringAssetIds = null;
            DisposeOwnedTracks(positions);
            DisposeOwnedTracks(offsets);
            DisposeOwnedTracks(scales);
            DisposeOwnedTracks(rotations);
            DisposeOwnedTracks(overrides);
            DeleteOwnedArray(formerIds);
        }

        /// <summary>Disposes a clip-owned track tree while preserving the shared empty-array singleton.</summary>
        /// <typeparam name="T">Disposable track or platform override type.</typeparam>
        /// <param name="values">Deserialized array owned by this clip.</param>
        internal static void DisposeOwnedTracks<T>(T[] values) where T : class, IDisposable {
            if (values == null || ReferenceEquals(values, Array.Empty<T>())) {
                return;
            }
            for (int index = 0; index < values.Length; index++) {
                NativeOwnership.DisposeAndDelete(values[index]);
            }
            NativeOwnership.Delete(values);
        }

        /// <summary>Deletes keyframe DTOs and their container without deleting value-type vector data.</summary>
        /// <typeparam name="T">Position or rotation keyframe DTO type.</typeparam>
        /// <param name="values">Keyframes owned by one deserialized track.</param>
        internal static void DeleteOwnedKeyframes<T>(T[] values) where T : class {
            if (values == null || ReferenceEquals(values, Array.Empty<T>())) {
                return;
            }
            for (int index = 0; index < values.Length; index++) {
                NativeOwnership.Delete(values[index]);
            }
            NativeOwnership.Delete(values);
        }

        /// <summary>Deletes an owned array container while preserving its borrowed elements and the empty singleton.</summary>
        /// <typeparam name="T">Value or borrowed reference type stored in the container.</typeparam>
        /// <param name="values">Deserialized container owned by the clip.</param>
        internal static void DeleteOwnedArray<T>(T[] values) {
            if (values == null || ReferenceEquals(values, Array.Empty<T>())) {
                return;
            }
            NativeOwnership.Delete(values);
        }
    }
}
