namespace helengine {
    /// <summary>
    /// Defines the platform-owned audio backend contract used by the shared runtime audio manager.
    /// </summary>
    public interface IAudioBackend {
        /// <summary>
        /// Starts playback of one audio asset using the supplied runtime request parameters.
        /// </summary>
        /// <param name="asset">Resolved audio asset to play.</param>
        /// <param name="request">Runtime playback request metadata.</param>
        /// <returns>Backend-owned voice identifier.</returns>
        int Play(AudioAsset asset, AudioPlaybackRequest request);

        /// <summary>
        /// Stops one active backend voice.
        /// </summary>
        /// <param name="voiceId">Backend-owned voice identifier.</param>
        void Stop(int voiceId);

        /// <summary>
        /// Applies one linear gain value to a named mixer bus.
        /// </summary>
        /// <param name="busId">Stable bus identifier.</param>
        /// <param name="gain">Linear gain multiplier.</param>
        void SetBusGain(string busId, float gain);

        /// <summary>
        /// Pauses or resumes one named mixer bus.
        /// </summary>
        /// <param name="busId">Stable bus identifier.</param>
        /// <param name="paused">True to pause; false to resume.</param>
        void SetBusPaused(string busId, bool paused);

        /// <summary>
        /// Returns whether one backend voice is still active.
        /// </summary>
        /// <param name="voiceId">Backend-owned voice identifier.</param>
        /// <returns>True when the voice is still playing.</returns>
        bool IsPlaying(int voiceId);

        /// <summary>
        /// Advances backend-owned maintenance work that must run once per frame.
        /// </summary>
        void Update();
    }
}
