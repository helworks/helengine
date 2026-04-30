namespace helengine {
    /// <summary>
    /// Plays one animation clip at a time against the owning entity's local transform channels.
    /// </summary>
    public class AnimationPlayerComponent : UpdateComponent {
        AnimationClipAsset currentClip;
        float currentTime;
        bool isPlaying;
        bool isPaused;
        bool loop;
        float frameDeltaTime;
        float3 baseLocalPosition;
        float3 baseLocalScale;
        float4 baseLocalOrientation;

        /// <summary>
        /// Initializes a new player with a fixed default frame delta for update-loop playback.
        /// </summary>
        public AnimationPlayerComponent() {
            frameDeltaTime = 1f / 60f;
        }

        /// <summary>
        /// Gets the clip currently assigned to the player, if any.
        /// </summary>
        public AnimationClipAsset CurrentClip {
            get { return currentClip; }
        }

        /// <summary>
        /// Gets the current playback time in seconds.
        /// </summary>
        public float CurrentTime {
            get { return currentTime; }
        }

        /// <summary>
        /// Gets a value indicating whether the player is actively advancing playback time.
        /// </summary>
        public bool IsPlaying {
            get { return isPlaying; }
        }

        /// <summary>
        /// Gets a value indicating whether playback is paused while keeping the current clip assigned.
        /// </summary>
        public bool IsPaused {
            get { return isPaused; }
        }

        /// <summary>
        /// Gets or sets the fixed delta time applied when the component is advanced through the engine update loop.
        /// </summary>
        public float FrameDeltaTime {
            get { return frameDeltaTime; }
            set { frameDeltaTime = value; }
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

            currentClip = clip;
            currentTime = 0f;
            loop = shouldLoop;
            isPlaying = true;
            isPaused = false;
            baseLocalPosition = Parent.LocalPosition;
            baseLocalScale = Parent.LocalScale;
            baseLocalOrientation = Parent.LocalOrientation;
            ApplyCurrentPose();
            if (currentClip.Duration <= 0f) {
                CompletePlayback();
            }
        }

        /// <summary>
        /// Stops playback, restores the captured base transform, and clears the active clip assignment.
        /// </summary>
        public void Stop() {
            if (Parent != null) {
                Parent.LocalPosition = baseLocalPosition;
                Parent.LocalScale = baseLocalScale;
                Parent.LocalOrientation = baseLocalOrientation;
            }

            currentClip = null;
            currentTime = 0f;
            isPlaying = false;
            isPaused = false;
            loop = false;
        }

        /// <summary>
        /// Pauses playback without clearing the current clip assignment.
        /// </summary>
        public void Pause() {
            if (currentClip == null) {
                throw new InvalidOperationException("Cannot pause animation playback when no clip is active.");
            }

            isPaused = true;
            isPlaying = false;
        }

        /// <summary>
        /// Resumes playback from the current time after a pause.
        /// </summary>
        public void Resume() {
            if (currentClip == null) {
                throw new InvalidOperationException("Cannot resume animation playback when no clip is active.");
            }

            isPaused = false;
            isPlaying = true;
        }

        /// <summary>
        /// Seeks to a specific playback time and applies the resulting pose immediately.
        /// </summary>
        /// <param name="time">Target playback time in seconds.</param>
        public void Seek(float time) {
            if (currentClip == null) {
                throw new InvalidOperationException("Cannot seek animation playback when no clip is active.");
            }

            currentTime = ResolvePlaybackTime(time);
            ApplyCurrentPose();
        }

        /// <summary>
        /// Advances playback time by the supplied delta and applies the resulting pose.
        /// </summary>
        /// <param name="deltaTime">Time step in seconds.</param>
        public void Advance(float deltaTime) {
            if (!isPlaying || isPaused || currentClip == null) {
                return;
            }

            float nextTime = currentTime + deltaTime;
            if (!loop && nextTime >= currentClip.Duration) {
                currentTime = currentClip.Duration;
                ApplyCurrentPose();
                CompletePlayback();
                return;
            }

            currentTime = ResolvePlaybackTime(nextTime);
            ApplyCurrentPose();
        }

        /// <summary>
        /// Advances playback using the configured fixed frame delta.
        /// </summary>
        public override void Update() {
            base.Update();
            Advance(frameDeltaTime);
        }

        /// <summary>
        /// Resolves the effective playback time according to the active loop mode.
        /// </summary>
        /// <param name="time">Requested playback time in seconds.</param>
        /// <returns>Clamped or wrapped playback time.</returns>
        float ResolvePlaybackTime(float time) {
            if (currentClip == null || currentClip.Duration <= 0f) {
                return 0f;
            } else if (loop) {
                double duration = currentClip.Duration;
                double wrapped = time % duration;
                if (wrapped < 0d) {
                    wrapped += duration;
                }

                return (float)wrapped;
            } else if (time <= 0f) {
                return 0f;
            } else if (time >= currentClip.Duration) {
                return currentClip.Duration;
            }

            return time;
        }

        /// <summary>
        /// Applies the current clip pose to the owning entity's local transform.
        /// </summary>
        void ApplyCurrentPose() {
            if (Parent == null || currentClip == null) {
                return;
            }

            float3 resolvedPosition = baseLocalPosition;
            if (currentClip.PositionTracks.Length == 1) {
                resolvedPosition = AnimationClipEvaluator.EvaluatePositionTrack(currentClip.PositionTracks[0], currentTime);
            }

            if (currentClip.PositionOffsetTracks.Length == 1) {
                resolvedPosition += AnimationClipEvaluator.EvaluatePositionTrack(currentClip.PositionOffsetTracks[0], currentTime);
            }

            float3 resolvedScale = baseLocalScale;
            if (currentClip.ScaleTracks.Length == 1) {
                resolvedScale = AnimationClipEvaluator.EvaluatePositionTrack(currentClip.ScaleTracks[0], currentTime);
            }

            float4 resolvedOrientation = baseLocalOrientation;
            if (currentClip.RotationTracks.Length == 1) {
                resolvedOrientation = AnimationClipEvaluator.EvaluateRotationTrack(currentClip.RotationTracks[0], currentTime);
            }

            Parent.LocalPosition = resolvedPosition;
            Parent.LocalScale = resolvedScale;
            Parent.LocalOrientation = resolvedOrientation;
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
            isPlaying = false;
            isPaused = false;
        }
    }
}
