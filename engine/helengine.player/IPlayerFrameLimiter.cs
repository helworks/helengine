namespace helengine.player {
    /// <summary>
    /// Limits how fast the player's frame loop runs.
    /// </summary>
    public interface IPlayerFrameLimiter {
        /// <summary>Begins limiting; called once before the first frame.</summary>
        void Start();

        /// <summary>Waits, if needed, until the next frame may start.</summary>
        void WaitForNextFrame();
    }
}
