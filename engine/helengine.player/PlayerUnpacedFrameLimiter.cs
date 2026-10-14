namespace helengine.player {
    /// <summary>
    /// Frame limiter for <c>frame-rate: unlimited</c>: never waits, so the loop runs as fast as the machine allows.
    /// </summary>
    public sealed class PlayerUnpacedFrameLimiter : IPlayerFrameLimiter {
        /// <summary>Does nothing; an unpaced loop needs no start time.</summary>
        public void Start() {
        }

        /// <summary>Returns immediately.</summary>
        public void WaitForNextFrame() {
        }
    }
}
