namespace helengine.animation.runtime {
    /// <summary>
    /// Plays a <see cref="HierarchyAnimationClipAsset"/> on a hierarchy. The clip is loaded from <see cref="ClipPath"/>
    /// and each track drives the entity bound by the reference at the same index of <see cref="TrackTargets"/>. Both are
    /// resolved once when the component is initialized (scene loading binds the references before that), so the per-frame
    /// path does no lookups or allocation. <see cref="Seek"/> applies a time immediately and is stable when repeated, so
    /// tools can scrub; <see cref="Update"/> advances the time only while <see cref="Playing"/>.
    /// </summary>
    [RuntimeFeatureRequirement("animation.hierarchy", "Plays hierarchical animation clips imported from glTF.")]
    public sealed class HierarchyAnimationPlayerComponent : UpdateComponent {
        /// <summary>
        /// Backing field of <see cref="ClipPath"/>.
        /// </summary>
        string ClipPathValue = string.Empty;

        /// <summary>
        /// Backing field of <see cref="TrackTargets"/>.
        /// </summary>
        SceneEntityReference[] TrackTargetsValue = Array.Empty<SceneEntityReference>();

        /// <summary>
        /// Backing field of <see cref="Speed"/>.
        /// </summary>
        double SpeedValue = 1d;

        /// <summary>
        /// Backing field of <see cref="Time"/>.
        /// </summary>
        double TimeValue;

        /// <summary>
        /// Clip loaded from <see cref="ClipPath"/> at initialization; null before then.
        /// </summary>
        [NativeOwnedMember]
        HierarchyAnimationClipAsset Clip;

        /// <summary>
        /// Entity driven by each track, resolved from <see cref="TrackTargets"/> at initialization. The array is owned; the
        /// entities are borrowed from the scene.
        /// </summary>
        [NativeOwnedMember]
        Entity[] Targets;

        /// <summary>
        /// Gets or sets the packaged relative path of the clip, loaded through the
        /// <see cref="AnimationRuntimeRegistration.ClipProcessorId"/> processor when the component is initialized.
        /// </summary>
        public string ClipPath {
            get { return ClipPathValue; }
            set {
                if (value == null) {
                    throw new ArgumentNullException(nameof(value));
                }

                ClipPathValue = value;
            }
        }

        /// <summary>
        /// Gets or sets one entity reference per clip track, in track order. Importers bind each reference to the entity at
        /// the track's <see cref="HierarchyAnimationTrack.TargetPath"/>; scene loading resolves it.
        /// </summary>
        public SceneEntityReference[] TrackTargets {
            get { return TrackTargetsValue; }
            set {
                if (value == null) {
                    throw new ArgumentNullException(nameof(value));
                }

                TrackTargetsValue = value;
            }
        }

        /// <summary>
        /// Gets or sets whether frames advance the time. A clip that does not <see cref="Loop"/> stops at its end (or at its
        /// start when playing backwards) and clears this flag; call <see cref="Seek"/> with 0 before playing it again.
        /// </summary>
        public bool Playing { get; set; }

        /// <summary>
        /// Gets or sets whether playback wraps to the start past the end (and to the end before the start when the speed is
        /// negative). Without it playback stops at the clip boundary, clears <see cref="Playing"/> and stays there until
        /// <see cref="Seek"/> moves it.
        /// </summary>
        public bool Loop { get; set; }

        /// <summary>
        /// Gets or sets the playback rate multiplied with the frame time; 1 is real time and negative values play backwards.
        /// </summary>
        public double Speed {
            get { return SpeedValue; }
            set {
                if (double.IsNaN(value) || double.IsInfinity(value)) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Speed must be finite.");
                }

                SpeedValue = value;
            }
        }

        /// <summary>
        /// Gets the current time in seconds, always inside <c>[0, Duration]</c> of the clip.
        /// </summary>
        public double Time {
            get { return TimeValue; }
        }

        /// <summary>
        /// Loads the clip and resolves the track targets, then registers for updates.
        /// </summary>
        /// <param name="entity">Entity that owns the component.</param>
        public override void ComponentInitialized(Entity entity) {
            Bind();
            base.ComponentInitialized(entity);
        }

        /// <summary>
        /// Advances the time by the core's frame time while playing and applies the clip.
        /// </summary>
        public override void Update() {
            base.Update();
            if (!Playing) {
                return;
            }

            Advance(OwnerCore.DeltaTime * SpeedValue);
        }

        /// <summary>
        /// Jumps to a time, clamped to <c>[0, Duration]</c>, and applies the clip immediately without advancing playback.
        /// </summary>
        /// <param name="seconds">Target time in seconds.</param>
        public void Seek(double seconds) {
            if (double.IsNaN(seconds)) {
                throw new ArgumentOutOfRangeException(nameof(seconds), "Seek time must be a number.");
            }

            RequireBound();
            double time = seconds;
            if (time < 0d) {
                time = 0d;
            } else if (time > Clip.Duration) {
                time = Clip.Duration;
            }

            TimeValue = time;
            HierarchyAnimationEvaluator.Apply(Clip, Targets, TimeValue);
        }

        /// <summary>
        /// Releases the loaded clip and the resolved target table.
        /// </summary>
        public override void Dispose() {
            NativeOwnership.Release(ref Targets);
            NativeOwnership.DisposeAndRelease(ref Clip);
            base.Dispose();
        }

        /// <summary>
        /// Moves the time by a signed amount, wrapping when looping and otherwise stopping at the boundary it moves into, then
        /// applies it. A zero amount (the first frame's delta) never stops playback.
        /// </summary>
        /// <param name="seconds">Signed clip seconds to move.</param>
        void Advance(double seconds) {
            RequireBound();
            double duration = Clip.Duration;
            double time = TimeValue + seconds;
            if (Loop && duration > 0d) {
                time -= Math.Floor(time / duration) * duration;
            } else if (seconds > 0d && time >= duration) {
                time = duration;
                Playing = false;
            } else if (seconds < 0d && time <= 0d) {
                time = 0d;
                Playing = false;
            }

            TimeValue = time;
            HierarchyAnimationEvaluator.Apply(Clip, Targets, TimeValue);
        }

        /// <summary>
        /// Loads the clip and resolves one entity per track, failing when the counts differ or a reference is unresolved, then
        /// applies the pose at time 0 so <see cref="Time"/> always matches the shown pose.
        /// </summary>
        void Bind() {
            if (string.IsNullOrEmpty(ClipPathValue)) {
                throw new InvalidOperationException("HierarchyAnimationPlayerComponent needs a ClipPath before it is initialized.");
            }

            NativeOwnership.Release(ref Targets);
            AdoptClip(OwnerCore.GetContentManager().Load<HierarchyAnimationClipAsset>(ClipPathValue, AnimationRuntimeRegistration.ClipProcessorId));
            int trackCount = Clip.Tracks.Length;
            if (TrackTargetsValue.Length != trackCount) {
                throw new InvalidOperationException($"Hierarchy animation clip '{ClipPathValue}' has {trackCount} tracks, but the player binds {TrackTargetsValue.Length} track targets.");
            }

            Targets = new Entity[trackCount];
            for (int index = 0; index < trackCount; index++) {
                SceneEntityReference reference = TrackTargetsValue[index];
                if (reference == null || reference.ResolvedEntity == null) {
                    throw new InvalidOperationException($"Hierarchy animation clip '{ClipPathValue}' track {index} targets '{Clip.Tracks[index].TargetPath}', but its entity reference is not resolved.");
                }

                Targets[index] = reference.ResolvedEntity;
            }

            TimeValue = 0d;
            HierarchyAnimationEvaluator.Apply(Clip, Targets, TimeValue);
        }

        /// <summary>
        /// Takes ownership of a freshly loaded clip, releasing any previous one.
        /// </summary>
        /// <param name="clip">Clip whose ownership transfers to this player.</param>
        void AdoptClip([NativeTakesOwnership] HierarchyAnimationClipAsset clip) {
            if (clip == null) {
                throw new ArgumentNullException(nameof(clip));
            }

            NativeOwnership.DisposeAndRelease(ref Clip);
            Clip = clip;
        }

        /// <summary>
        /// Fails when the component has not been initialized, so there is no clip to play.
        /// </summary>
        void RequireBound() {
            if (Clip == null || Targets == null) {
                throw new InvalidOperationException("HierarchyAnimationPlayerComponent must be initialized on an entity before it can play or seek.");
            }
        }
    }
}
