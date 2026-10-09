namespace helengine.timeline.runtime.tests {
    /// <summary>
    /// Audio backend double that cannot seek. It records each play as <c>start@offset</c> (offset copied at call time,
    /// because the player reuses its request objects) and each stop.
    /// </summary>
    public class RecordingAudioBackend : IAudioBackend {
        /// <summary>
        /// Next voice id handed out.
        /// </summary>
        int NextVoiceId;

        /// <summary>
        /// Gets the start offsets of every play, in call order.
        /// </summary>
        public List<float> PlayedOffsets { get; } = new List<float>();

        /// <summary>
        /// Gets the gains of every play, in call order.
        /// </summary>
        public List<float> PlayedGains { get; } = new List<float>();

        /// <summary>
        /// Gets the voice ids stopped, in call order.
        /// </summary>
        public List<int> StoppedVoices { get; } = new List<int>();

        /// <summary>
        /// Records a play and returns a new voice id.
        /// </summary>
        /// <param name="asset">Asset to play.</param>
        /// <param name="request">Playback request.</param>
        /// <returns>New voice id.</returns>
        public int Play(AudioAsset asset, AudioPlaybackRequest request) {
            PlayedOffsets.Add(request.StartOffsetSeconds);
            PlayedGains.Add(request.Gain);
            NextVoiceId++;
            return NextVoiceId;
        }

        /// <summary>
        /// Records a stop.
        /// </summary>
        /// <param name="voiceId">Voice to stop.</param>
        public void Stop(int voiceId) {
            StoppedVoices.Add(voiceId);
        }

        /// <summary>
        /// Ignores bus gains.
        /// </summary>
        /// <param name="busId">Bus id.</param>
        /// <param name="gain">Gain.</param>
        public void SetBusGain(string busId, float gain) {
        }

        /// <summary>
        /// Ignores bus pauses.
        /// </summary>
        /// <param name="busId">Bus id.</param>
        /// <param name="paused">Paused flag.</param>
        public void SetBusPaused(string busId, bool paused) {
        }

        /// <summary>
        /// Reports every voice as playing.
        /// </summary>
        /// <param name="voiceId">Voice id.</param>
        /// <returns>Always true.</returns>
        public bool IsPlaying(int voiceId) {
            return true;
        }

        /// <summary>
        /// Does nothing.
        /// </summary>
        public void Update() {
        }
    }
}
