namespace helengine {
    /// <summary>
    /// Coordinates shared runtime audio playback and bus state over one platform backend.
    /// </summary>
    public sealed class AudioManager {
        readonly IAudioBackend Backend;
        readonly Dictionary<string, AudioBus> BusesById;
        readonly HashSet<int> ActiveVoiceIds;

        /// <summary>
        /// Initializes one audio manager over the supplied backend.
        /// </summary>
        /// <param name="backend">Platform-owned backend implementation.</param>
        public AudioManager(IAudioBackend backend) {
            Backend = backend ?? throw new ArgumentNullException(nameof(backend));
            BusesById = new Dictionary<string, AudioBus>(StringComparer.OrdinalIgnoreCase) {
                ["master"] = new AudioBus("master"),
                ["music"] = new AudioBus("music"),
                ["sfx"] = new AudioBus("sfx")
            };
            ActiveVoiceIds = new HashSet<int>();
        }

        /// <summary>
        /// Starts playback of one resolved audio asset using the supplied request.
        /// </summary>
        /// <param name="asset">Resolved audio asset to play.</param>
        /// <param name="request">Playback request parameters.</param>
        /// <returns>Backend-owned voice identifier.</returns>
        public int Play(AudioAsset asset, AudioPlaybackRequest request) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            AudioPlaybackRequest effectiveRequest = request ?? new AudioPlaybackRequest();
            string busId = string.IsNullOrWhiteSpace(effectiveRequest.BusId) ? "master" : effectiveRequest.BusId;
            GetOrCreateBus(busId);
            int voiceId = Backend.Play(asset, effectiveRequest);
            ActiveVoiceIds.Add(voiceId);
            return voiceId;
        }

        /// <summary>
        /// Stops one active voice when it is currently tracked.
        /// </summary>
        /// <param name="voiceId">Backend-owned voice identifier.</param>
        public void Stop(int voiceId) {
            if (!ActiveVoiceIds.Remove(voiceId)) {
                return;
            }

            Backend.Stop(voiceId);
        }

        /// <summary>
        /// Applies one gain update to the supplied bus.
        /// </summary>
        /// <param name="busId">Stable bus identifier.</param>
        /// <param name="gain">Linear gain multiplier.</param>
        public void SetBusGain(string busId, float gain) {
            AudioBus bus = GetOrCreateBus(busId);
            bus.Gain = gain;
            Backend.SetBusGain(bus.BusId, gain);
        }

        /// <summary>
        /// Applies one pause-state update to the supplied bus.
        /// </summary>
        /// <param name="busId">Stable bus identifier.</param>
        /// <param name="paused">True to pause; false to resume.</param>
        public void SetBusPaused(string busId, bool paused) {
            AudioBus bus = GetOrCreateBus(busId);
            bus.Paused = paused;
            Backend.SetBusPaused(bus.BusId, paused);
        }

        /// <summary>
        /// Advances backend maintenance and drops any voices that have already finished.
        /// </summary>
        public void Update() {
            Backend.Update();

            if (ActiveVoiceIds.Count == 0) {
                return;
            }

            List<int> completedVoiceIds = [];
            foreach (int voiceId in ActiveVoiceIds) {
                if (!Backend.IsPlaying(voiceId)) {
                    completedVoiceIds.Add(voiceId);
                }
            }

            for (int index = 0; index < completedVoiceIds.Count; index++) {
                ActiveVoiceIds.Remove(completedVoiceIds[index]);
            }
        }

        /// <summary>
        /// Returns one tracked bus, creating it on first use.
        /// </summary>
        /// <param name="busId">Stable bus identifier.</param>
        /// <returns>Tracked audio bus.</returns>
        AudioBus GetOrCreateBus(string busId) {
            if (string.IsNullOrWhiteSpace(busId)) {
                busId = "master";
            }

            if (BusesById.TryGetValue(busId, out AudioBus bus)) {
                return bus;
            }

            bus = new AudioBus(busId);
            BusesById.Add(busId, bus);
            return bus;
        }
    }
}
