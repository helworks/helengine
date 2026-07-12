using NAudio.Wave;

namespace helengine {
    /// <summary>
    /// Stores one active Windows playback voice and its runtime routing metadata.
    /// </summary>
    internal sealed class WindowsAudioVoice : IDisposable {
        /// <summary>
        /// Initializes one tracked playback voice.
        /// </summary>
        /// <param name="output">Wave output that owns playback state.</param>
        /// <param name="stream">Wave stream bound to the output.</param>
        /// <param name="busId">Stable bus identifier for routing updates.</param>
        /// <param name="baseGain">Per-voice gain multiplier applied before bus gain.</param>
        public WindowsAudioVoice(WaveOutEvent output, WaveStream stream, string busId, float baseGain) {
            Output = output ?? throw new ArgumentNullException(nameof(output));
            Stream = stream ?? throw new ArgumentNullException(nameof(stream));
            BusId = string.IsNullOrWhiteSpace(busId) ? "master" : busId;
            BaseGain = baseGain;
        }

        /// <summary>
        /// Gets the bound wave output that owns playback state.
        /// </summary>
        public WaveOutEvent Output { get; }

        /// <summary>
        /// Gets the wave stream bound to the output.
        /// </summary>
        public WaveStream Stream { get; }

        /// <summary>
        /// Gets the stable routing bus identifier for this voice.
        /// </summary>
        public string BusId { get; }

        /// <summary>
        /// Gets the per-voice gain multiplier applied before bus gain.
        /// </summary>
        public float BaseGain { get; }

        /// <summary>
        /// Stops playback and releases the bound output and stream.
        /// </summary>
        public void Dispose() {
            Output.Stop();
            Output.Dispose();
            Stream.Dispose();
        }
    }
}
