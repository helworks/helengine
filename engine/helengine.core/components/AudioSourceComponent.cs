namespace helengine {
    /// <summary>
    /// Plays one authored audio clip through the shared runtime audio manager.
    /// </summary>
    public sealed class AudioSourceComponent : UpdateComponent {
        AudioAsset ClipValue;
        bool PlayOnStartValue = true;
        bool LoopValue;
        string BusIdValue = "master";
        float GainValue = 1f;
        int ActiveVoiceIdValue = -1;

        /// <summary>
        /// Gets or sets the authored clip referenced by this source.
        /// </summary>
        public AudioAsset Clip {
            get { return ClipValue; }
            set { ClipValue = value; }
        }

        /// <summary>
        /// Gets or sets whether playback should begin automatically once the component joins an initialized hierarchy.
        /// </summary>
        public bool PlayOnStart {
            get { return PlayOnStartValue; }
            set { PlayOnStartValue = value; }
        }

        /// <summary>
        /// Gets or sets whether playback should loop regardless of the clip default.
        /// </summary>
        public bool Loop {
            get { return LoopValue; }
            set { LoopValue = value; }
        }

        /// <summary>
        /// Gets or sets the target mixer bus identifier.
        /// </summary>
        public string BusId {
            get { return BusIdValue; }
            set { BusIdValue = string.IsNullOrWhiteSpace(value) ? "master" : value; }
        }

        /// <summary>
        /// Gets or sets the linear gain multiplier applied to playback.
        /// </summary>
        public float Gain {
            get { return GainValue; }
            set { GainValue = value; }
        }

        /// <summary>
        /// Gets the backend-owned voice identifier for the most recent automatic playback, or -1 when idle.
        /// </summary>
        public int ActiveVoiceId => ActiveVoiceIdValue;

        /// <summary>
        /// Starts authored playback when the component is attached to an enabled entity.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);
            TryPlayConfiguredClip();
        }

        /// <summary>
        /// Rechecks automatic playback after hierarchy initialization so scene-load flows can start audio once every component is materialized.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentInitialized(Entity entity) {
            base.ComponentInitialized(entity);
            TryPlayConfiguredClip();
        }

        /// <summary>
        /// Stops any active voice when the component is detached.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);
            Stop();
        }

        /// <summary>
        /// Starts playback immediately using the current clip and authored playback settings.
        /// </summary>
        /// <returns>Backend-owned voice identifier.</returns>
        public int Play() {
            if (ClipValue == null) {
                throw new InvalidOperationException("AudioSourceComponent requires one Clip asset before playback can begin.");
            }

            AudioManager audioManager = OwnerCore?.AudioManager;
            if (audioManager == null) {
                return -1;
            }

            ActiveVoiceIdValue = audioManager.Play(ClipValue, new AudioPlaybackRequest {
                BusId = BusIdValue,
                Loop = LoopValue || ClipValue.DefaultLoop,
                Gain = GainValue
            });
            return ActiveVoiceIdValue;
        }

        /// <summary>
        /// Stops the current playback voice when one is active.
        /// </summary>
        public void Stop() {
            if (ActiveVoiceIdValue < 0) {
                return;
            }

            AudioManager audioManager = OwnerCore?.AudioManager;
            if (audioManager != null) {
                audioManager.Stop(ActiveVoiceIdValue);
            }

            ActiveVoiceIdValue = -1;
        }

        /// <summary>
        /// Starts authored playback exactly once when the component is configured for automatic playback.
        /// </summary>
        void TryPlayConfiguredClip() {
            if (!PlayOnStartValue || ClipValue == null || ActiveVoiceIdValue >= 0) {
                return;
            }

            Play();
        }
    }
}
