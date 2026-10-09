namespace helengine.core.tests.audio {
    /// <summary>
    /// Audio backend double that cannot seek: it records every request it receives so tests can inspect what the manager
    /// forwarded.
    /// </summary>
    class RecordingAudioBackend : IAudioBackend {
        /// <summary>
        /// Next voice id handed out by <see cref="Play"/>.
        /// </summary>
        int NextVoiceId;

        /// <summary>
        /// Gets the requests received by <see cref="Play"/>, in call order.
        /// </summary>
        public List<AudioPlaybackRequest> Requests { get; } = new List<AudioPlaybackRequest>();

        /// <summary>
        /// Records the request and returns a fresh voice id.
        /// </summary>
        /// <param name="asset">Asset to play (unused).</param>
        /// <param name="request">Request to record.</param>
        /// <returns>A new voice id.</returns>
        public int Play(AudioAsset asset, AudioPlaybackRequest request) {
            Requests.Add(request);
            NextVoiceId++;
            return NextVoiceId;
        }

        /// <summary>
        /// Ignores stop requests.
        /// </summary>
        /// <param name="voiceId">Voice to stop.</param>
        public void Stop(int voiceId) {
        }

        /// <summary>
        /// Ignores bus gain changes.
        /// </summary>
        /// <param name="busId">Bus id.</param>
        /// <param name="gain">New gain.</param>
        public void SetBusGain(string busId, float gain) {
        }

        /// <summary>
        /// Ignores bus pause changes.
        /// </summary>
        /// <param name="busId">Bus id.</param>
        /// <param name="paused">Whether the bus is paused.</param>
        public void SetBusPaused(string busId, bool paused) {
        }

        /// <summary>
        /// Reports every voice as playing.
        /// </summary>
        /// <param name="voiceId">Voice to query.</param>
        /// <returns>Always true.</returns>
        public bool IsPlaying(int voiceId) {
            return true;
        }

        /// <summary>
        /// Does nothing; the double has no device to service.
        /// </summary>
        public void Update() {
        }
    }
}
