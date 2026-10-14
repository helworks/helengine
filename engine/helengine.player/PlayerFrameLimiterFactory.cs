using helengine.baseplatform.Player;

namespace helengine.player {
    /// <summary>
    /// Chooses the frame limiter for the <c>frame-rate</c> Play setting.
    /// </summary>
    public static class PlayerFrameLimiterFactory {
        /// <summary>
        /// Creates the limiter for one Play.
        /// </summary>
        /// <param name="settings">Resolved Play settings.</param>
        /// <param name="clock">Clock a pacer uses.</param>
        /// <returns>A pacer at the target frame rate, or an unpaced limiter.</returns>
        public static IPlayerFrameLimiter Create(PlayerPlaySettings settings, IPlayerFrameClock clock) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (settings.FrameRateUnlimited) {
                return new PlayerUnpacedFrameLimiter();
            }

            return new PlayerFramePacer(settings.TargetFrameRate, clock);
        }
    }
}
