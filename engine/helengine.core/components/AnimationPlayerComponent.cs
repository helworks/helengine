namespace helengine {
    /// <summary>
    /// Plays one animation clip at a time against the owning entity's local transform channels.
    /// </summary>
    public class AnimationPlayerComponent : UpdateComponent {
        AnimationClipAsset ClipValue;
        AnimationClipAsset CurrentClipValue;
        float CurrentTimeValue;
        bool IsPlayingValue;
        bool IsPausedValue;
        bool Loop;
        bool PlayAutomaticallyValue;
        bool ShouldLoopValue;
        float FrameDeltaTimeValue;
        float3 BaseLocalPosition;
        /// <summary>Borrowed viewport resolved on attachment/preparation, never searched on animation frames.</summary>
        ViewportComponent LayoutViewport;
        /// <summary>Borrowed legacy fit when it is the nearest scaling owner.</summary>
        ReferenceCanvasFitComponent LayoutFit;
        /// <summary>Single-pass owners reuse the existing playback base in prepared coordinates.</summary>
        bool BasePositionIsPrepared;
        /// <summary>A newly attached authored instance must wait for its owner's one layout pass.</summary>
        bool AwaitingLayoutPreparation;
        float3 BaseLocalScale;
        float4 BaseLocalOrientation;

        /// <summary>
        /// Initializes a new player with a fallback frame delta used only when no active core timing state is available.
        /// </summary>
        public AnimationPlayerComponent() {
            FrameDeltaTimeValue = 1f / 60f;
        }

        /// <summary>
        /// Gets or sets the authored clip that can be started automatically when the component joins an entity hierarchy.
        /// </summary>
        public AnimationClipAsset Clip {
            get { return ClipValue; }
            set { ClipValue = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the authored clip should begin automatically during component lifecycle initialization.
        /// </summary>
        public bool PlayAutomatically {
            get { return PlayAutomaticallyValue; }
            set { PlayAutomaticallyValue = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether automatic playback of the authored clip should loop.
        /// </summary>
        public bool ShouldLoop {
            get { return ShouldLoopValue; }
            set { ShouldLoopValue = value; }
        }

        /// <summary>
        /// Gets the clip currently assigned to the player, if any.
        /// </summary>
        public AnimationClipAsset CurrentClip {
            get { return CurrentClipValue; }
        }

        /// <summary>
        /// Gets the current playback time in seconds.
        /// </summary>
        public float CurrentTime {
            get { return CurrentTimeValue; }
        }

        /// <summary>
        /// Gets a value indicating whether the player is actively advancing playback time.
        /// </summary>
        public bool IsPlaying {
            get { return IsPlayingValue; }
        }

        /// <summary>
        /// Gets a value indicating whether playback is paused while keeping the current clip assigned.
        /// </summary>
        public bool IsPaused {
            get { return IsPausedValue; }
        }

        /// <summary>
        /// Gets or sets the fallback delta time applied only when the component updates without one active core timing source.
        /// </summary>
        public float FrameDeltaTime {
            get { return FrameDeltaTimeValue; }
            set { FrameDeltaTimeValue = value; }
        }

        /// <summary>
        /// Starts automatic playback as soon as the component is attached when the player was configured with an authored clip.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);
            RefreshLayoutOwner();
            TryPlayConfiguredClip();
        }

        /// <summary>
        /// Replays the authored clip after entity-hierarchy initialization so playback captures the final initialized base transform.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentInitialized(Entity entity) {
            base.ComponentInitialized(entity);
            TryPlayConfiguredClip();
        }

        /// <summary>Releases borrowed layout aliases before this animator leaves its hierarchy.</summary>
        public override void ComponentRemoved(Entity entity) {
            LayoutViewport = null;
            LayoutFit = null;
            AwaitingLayoutPreparation = false;
            base.ComponentRemoved(entity);
        }

        /// <summary>Clears cached layout aliases without taking ownership of the scene or shared clip.</summary>
        public override void Dispose() {
            LayoutViewport = null;
            LayoutFit = null;
            base.Dispose();
        }

        /// <summary>
        /// Starts playback of a new clip and captures the entity's current local transform as the playback base.
        /// </summary>
        /// <param name="clip">Clip to play.</param>
        /// <param name="shouldLoop">True to wrap time at the clip duration; otherwise false.</param>
        public void Play(AnimationClipAsset clip, bool shouldLoop) {
            if (clip == null) {
                throw new ArgumentNullException(nameof(clip));
            } else if (Parent == null) {
                throw new InvalidOperationException("AnimationPlayerComponent must be added to an entity before playback can begin.");
            }

            ValidateClip(clip);

            CurrentClipValue = clip;
            CurrentTimeValue = 0f;
            Loop = shouldLoop;
            IsPlayingValue = true;
            IsPausedValue = false;
            RefreshLayoutOwner();
            BaseLocalPosition = Parent.LocalPosition;
            BasePositionIsPrepared = HasLayoutOwner() && (GetLayoutMode() == ViewportLayoutProcessingMode.Cook
                || (!AwaitingLayoutPreparation && HasLayoutFactors() && GetLayoutMode() == ViewportLayoutProcessingMode.OnLoad));
            if (HasLayoutOwner() && HasLayoutFactors() && !AwaitingLayoutPreparation && GetLayoutMode() == ViewportLayoutProcessingMode.Dynamic) {
                BaseLocalPosition = InverseLayoutPosition(BaseLocalPosition);
            }
            BaseLocalScale = Parent.LocalScale;
            BaseLocalOrientation = Parent.LocalOrientation;
            ApplyCurrentPose();
            if (CurrentClipValue.Duration <= 0f) {
                CompletePlayback();
            }
        }

        /// <summary>
        /// Stops playback, restores the captured base transform, and clears the active clip assignment.
        /// </summary>
        public void Stop() {
            if (Parent != null) {
                Parent.LocalPosition = HasLayoutOwner() && HasLayoutFactors() && !BasePositionIsPrepared ? ResolveLayoutPosition(BaseLocalPosition, false) : BaseLocalPosition;
                Parent.LocalScale = BaseLocalScale;
                Parent.LocalOrientation = BaseLocalOrientation;
            }

            CurrentClipValue = null;
            CurrentTimeValue = 0f;
            IsPlayingValue = false;
            IsPausedValue = false;
            Loop = false;
        }

        /// <summary>
        /// Pauses playback without clearing the current clip assignment.
        /// </summary>
        public void Pause() {
            if (CurrentClipValue == null) {
                throw new InvalidOperationException("Cannot pause animation playback when no clip is active.");
            }

            IsPausedValue = true;
            IsPlayingValue = false;
        }

        /// <summary>
        /// Resumes playback from the current time after a pause.
        /// </summary>
        public void Resume() {
            if (CurrentClipValue == null) {
                throw new InvalidOperationException("Cannot resume animation playback when no clip is active.");
            }

            IsPausedValue = false;
            IsPlayingValue = true;
        }

        /// <summary>
        /// Seeks to a specific playback time and applies the resulting pose immediately.
        /// </summary>
        /// <param name="time">Target playback time in seconds.</param>
        public void Seek(float time) {
            if (CurrentClipValue == null) {
                throw new InvalidOperationException("Cannot seek animation playback when no clip is active.");
            }

            CurrentTimeValue = ResolvePlaybackTime(time);
            ApplyCurrentPose();
        }

        /// <summary>
        /// Recomputes the captured playback base from the entity's current local transform while preserving the currently sampled pose.
        /// </summary>
        public void RebaseCurrentPoseToLocalTransform() {
            if (Parent == null || CurrentClipValue == null) {
                return;
            }

            RefreshLayoutOwner();
            if (CurrentClipValue.PositionTracks.Length == 0) {
                float3 offset = CurrentClipValue.PositionOffsetTracks.Length == 1
                    ? AnimationClipEvaluator.EvaluatePositionTrack(CurrentClipValue.PositionOffsetTracks[0], CurrentTimeValue) : float3.Zero;
                if (HasLayoutOwner() && HasLayoutFactors()) {
                    BasePositionIsPrepared = GetLayoutMode() != ViewportLayoutProcessingMode.Dynamic;
                    BaseLocalPosition = BasePositionIsPrepared
                        ? Parent.LocalPosition - ResolveLayoutPosition(offset, true)
                        : InverseLayoutPosition(Parent.LocalPosition) - offset;
                } else {
                    BaseLocalPosition = Parent.LocalPosition - offset;
                    BasePositionIsPrepared = false;
                }
            }

            if (CurrentClipValue.ScaleTracks.Length == 0) {
                BaseLocalScale = Parent.LocalScale;
            }

            if (CurrentClipValue.RotationTracks.Length == 0) {
                BaseLocalOrientation = Parent.LocalOrientation;
            }
        }

        /// <summary>
        /// Advances playback time by the supplied delta and applies the resulting pose.
        /// </summary>
        /// <param name="deltaTime">Time step in seconds.</param>
        public void Advance(float deltaTime) {
            if (!IsPlayingValue || IsPausedValue || CurrentClipValue == null) {
                return;
            }

            float nextTime = CurrentTimeValue + deltaTime;
            if (!Loop && nextTime >= CurrentClipValue.Duration) {
                CurrentTimeValue = CurrentClipValue.Duration;
                ApplyCurrentPose();
                CompletePlayback();
                return;
            }

            CurrentTimeValue = ResolvePlaybackTime(nextTime);
            ApplyCurrentPose();
        }

        /// <summary>
        /// Advances playback using the current core frame delta when available and otherwise falls back to the configured local frame delta.
        /// </summary>
        public override void Update() {
            base.Update();
            Core core = OwnerCore;
            if (core != null) {
                Advance(core.DeltaTime);
                return;
            }

            Advance(FrameDeltaTimeValue);
        }

        /// <summary>
        /// Resolves the effective playback time according to the active loop mode.
        /// </summary>
        /// <param name="time">Requested playback time in seconds.</param>
        /// <returns>Clamped or wrapped playback time.</returns>
        float ResolvePlaybackTime(float time) {
            if (CurrentClipValue == null || CurrentClipValue.Duration <= 0f) {
                return 0f;
            } else if (Loop) {
                double duration = CurrentClipValue.Duration;
                double wrapped = time % duration;
                if (wrapped < 0d) {
                    wrapped += duration;
                }

                return (float)wrapped;
            } else if (time <= 0f) {
                return 0f;
            } else if (time >= CurrentClipValue.Duration) {
                return CurrentClipValue.Duration;
            }

            return time;
        }

        /// <summary>
        /// Applies the current clip pose to the owning entity's local transform.
        /// </summary>
        void ApplyCurrentPose() {
            if (Parent == null || CurrentClipValue == null) {
                return;
            }

            bool canWritePosition = !HasLayoutOwner() || (HasLayoutFactors() && !AwaitingLayoutPreparation);
            float3 resolvedPosition = EvaluateCurrentPosition(canWritePosition && HasLayoutOwner());

            float3 resolvedScale = BaseLocalScale;
            if (CurrentClipValue.ScaleTracks.Length == 1) {
                resolvedScale = AnimationClipEvaluator.EvaluatePositionTrack(CurrentClipValue.ScaleTracks[0], CurrentTimeValue);
            }

            float4 resolvedOrientation = BaseLocalOrientation;
            if (CurrentClipValue.RotationTracks.Length == 1) {
                resolvedOrientation = AnimationClipEvaluator.EvaluateRotationTrack(CurrentClipValue.RotationTracks[0], CurrentTimeValue);
            }

            if (canWritePosition) { Parent.LocalPosition = resolvedPosition; }
            Parent.LocalScale = resolvedScale;
            Parent.LocalOrientation = resolvedOrientation;
        }

        /// <summary>Resolves position channels in their declared space without modifying the shared clip.</summary>
        float3 EvaluateCurrentPosition(bool useLayout) {
            float3 position = BaseLocalPosition;
            bool absolute = CurrentClipValue.PositionTracks.Length == 1;
            if (absolute) { position = AnimationClipEvaluator.EvaluatePositionTrack(CurrentClipValue.PositionTracks[0], CurrentTimeValue); }
            float3 offset = CurrentClipValue.PositionOffsetTracks.Length == 1
                ? AnimationClipEvaluator.EvaluatePositionTrack(CurrentClipValue.PositionOffsetTracks[0], CurrentTimeValue) : float3.Zero;
            if (useLayout) {
                if (absolute || !BasePositionIsPrepared) { return ResolveLayoutPosition(position + offset, false); }
                return position + ResolveLayoutPosition(offset, true);
            }
            return position + offset;
        }

        /// <summary>Refreshes borrowed owner aliases only at hierarchy and lifecycle boundaries.</summary>
        internal void RefreshLayoutOwner() {
            ViewportComponent previousViewport = LayoutViewport;
            ReferenceCanvasFitComponent previousFit = LayoutFit;
            LayoutViewport = null;
            LayoutFit = null;
            Entity current = Parent;
            while (current != null) {
                if (current.Components != null) {
                    for (int index = 0; index < current.Components.Count; index++) {
                        if (current.Components[index] is ReferenceCanvasFitComponent fit) { LayoutFit = fit; break; }
                    }
                    if (LayoutFit != null) { break; }
                    for (int index = 0; index < current.Components.Count; index++) {
                        if (current.Components[index] is ViewportComponent viewport && viewport.ScalingMode != ViewportComponent.NoScalingMode) { LayoutViewport = viewport; break; }
                    }
                    if (LayoutViewport != null) { break; }
                }
                current = current.Parent;
            }
            if ((!ReferenceEquals(previousViewport, LayoutViewport) || !ReferenceEquals(previousFit, LayoutFit))
                && HasLayoutOwner() && Parent != null && !Parent.IsInitialized) { AwaitingLayoutPreparation = true; }
        }

        /// <summary>Converts the existing playback base once for single-pass UI, then applies the current pose.</summary>
        internal void PrepareLayout() {
            RefreshLayoutOwner();
            AwaitingLayoutPreparation = false;
            if (CurrentClipValue == null || !HasLayoutOwner() || !HasLayoutFactors()) { return; }
            if (GetLayoutMode() != ViewportLayoutProcessingMode.Dynamic && !BasePositionIsPrepared) {
                if (GetLayoutMode() != ViewportLayoutProcessingMode.Cook) { BaseLocalPosition = ResolveLayoutPosition(BaseLocalPosition, false); }
                BasePositionIsPrepared = true;
            }
            ApplyCurrentPose();
        }

        /// <summary>Drops aliases before a hierarchy-owned layout component is destroyed.</summary>
        internal void LayoutOwnerRemoved(Component owner) {
            if (ReferenceEquals(LayoutViewport, owner)) { LayoutViewport = null; }
            if (ReferenceEquals(LayoutFit, owner)) { LayoutFit = null; }
            if (!HasLayoutOwner()) { AwaitingLayoutPreparation = false; }
        }

        /// <summary>Identifies an active UI scaling owner without performing a type search.</summary>
        bool HasLayoutOwner() { return LayoutViewport != null || LayoutFit != null; }

        /// <summary>Returns the single shared prepared factors from the cached owner.</summary>
        ViewportLayoutMetrics GetLayoutMetrics() {
            ViewportLayoutMetrics metrics = LayoutViewport != null ? LayoutViewport.PreparedLayoutMetrics : LayoutFit.PreparedLayoutMetrics;
            Entity owner = LayoutViewport != null ? LayoutViewport.Parent : LayoutFit.Parent;
            if (!ReferenceEquals(Parent, owner)) { metrics.Origin = new float2(0f, 0f); }
            return metrics;
        }

        /// <summary>Distinguishes not-yet-prepared owners from a valid identity factor.</summary>
        bool HasLayoutFactors() {
            if (!HasLayoutOwner()) { return false; }
            ViewportLayoutMetrics metrics = GetLayoutMetrics();
            return metrics.ScaleX > 0d && metrics.ScaleY > 0d;
        }

        /// <summary>Uses the viewport policy inherited by the nearest legacy owner when necessary.</summary>
        ViewportLayoutProcessingMode GetLayoutMode() {
            return LayoutViewport != null ? LayoutViewport.LayoutProcessingMode : LayoutFit.EffectiveLayoutProcessingMode;
        }

        /// <summary>Composes an authored track position or offset with its prepared owner exactly once.</summary>
        float3 ResolveLayoutPosition(float3 position, bool offset) {
            ViewportLayoutMetrics metrics = GetLayoutMetrics();
            if (offset) { metrics.Origin = new float2(0f, 0f); }
            return ViewportLayoutApplier.ResolvePosition(position, metrics);
        }

        /// <summary>Recovers an authored playback base when a dynamic animation starts on an already laid-out entity.</summary>
        float3 InverseLayoutPosition(float3 position) {
            ViewportLayoutMetrics metrics = GetLayoutMetrics();
            return new float3((float)((position.X - metrics.Origin.X) / metrics.ScaleX), (float)((position.Y - metrics.Origin.Y) / metrics.ScaleY), position.Z);
        }

        /// <summary>
        /// Starts authored automatic playback when the component was configured to do so.
        /// </summary>
        void TryPlayConfiguredClip() {
            if (!PlayAutomaticallyValue) {
                return;
            } else if (ClipValue == null) {
                throw new InvalidOperationException("AnimationPlayerComponent requires one authored Clip asset before automatic playback can begin.");
            }

            if (ReferenceEquals(CurrentClipValue, ClipValue) && CurrentTimeValue == 0f && IsPlayingValue && !IsPausedValue
                && Parent.LocalPosition == EvaluateCurrentPosition(HasLayoutOwner() && HasLayoutFactors() && !AwaitingLayoutPreparation)) {
                BaseLocalScale = Parent.LocalScale;
                BaseLocalOrientation = Parent.LocalOrientation;
                Loop = ShouldLoopValue;
                return;
            }
            Play(ClipValue, ShouldLoopValue);
        }

        /// <summary>
        /// Validates that the first runtime slice can unambiguously bind the clip to one entity transform.
        /// </summary>
        /// <param name="clip">Clip to validate.</param>
        void ValidateClip(AnimationClipAsset clip) {
            if (clip.Duration < 0f) {
                throw new InvalidOperationException("Animation clips cannot declare a negative duration.");
            } else if (clip.PositionTracks.Length > 1 || clip.PositionOffsetTracks.Length > 1 || clip.ScaleTracks.Length > 1 || clip.RotationTracks.Length > 1) {
                throw new InvalidOperationException("Animation clips can currently bind only one track per transform channel.");
            }
        }

        /// <summary>
        /// Finishes playback after a non-looping clip reaches its end while keeping the evaluated final pose applied.
        /// </summary>
        void CompletePlayback() {
            IsPlayingValue = false;
            IsPausedValue = false;
        }
    }
}
