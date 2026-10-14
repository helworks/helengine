using helengine.baseplatform.Player;

namespace helengine.player {
    /// <summary>
    /// Computes the Play window from the render size, the <c>window-scale</c> setting and the chosen monitor's work area.
    /// <c>fit</c> picks the largest integer multiple whose whole window (client plus borders) fits, never below 1x. A fixed
    /// scale is honoured even when it does not fit; such a window opens at the work-area origin.
    /// </summary>
    public static class PlayerWindowLayout {
        /// <summary>Largest multiplier fit considers.</summary>
        public const int MaximumMultiplier = 16;

        /// <summary>
        /// Computes the window layout.
        /// </summary>
        /// <param name="renderSize">Platform render size.</param>
        /// <param name="scale">Window scale setting.</param>
        /// <param name="workArea">Work area of the chosen monitor.</param>
        /// <param name="nonClientWidth">Horizontal border size the window adds around the client.</param>
        /// <param name="nonClientHeight">Vertical border and caption size the window adds around the client.</param>
        /// <returns>The layout.</returns>
        public static PlayerWindowLayoutResult Compute(PlayerRenderSize renderSize, PlayerWindowScale scale, PlayerScreenArea workArea, int nonClientWidth, int nonClientHeight) {
            if (renderSize == null) {
                throw new ArgumentNullException(nameof(renderSize));
            } else if (workArea == null) {
                throw new ArgumentNullException(nameof(workArea));
            } else if (nonClientWidth < 0) {
                throw new ArgumentOutOfRangeException(nameof(nonClientWidth), nonClientWidth, "Window borders cannot be negative.");
            } else if (nonClientHeight < 0) {
                throw new ArgumentOutOfRangeException(nameof(nonClientHeight), nonClientHeight, "Window borders cannot be negative.");
            }

            int multiplier;
            if (scale == PlayerWindowScale.Fit) {
                multiplier = 1;
                for (int candidate = 2; candidate <= MaximumMultiplier; candidate++) {
                    if (!Fits(renderSize, candidate, workArea, nonClientWidth, nonClientHeight)) {
                        break;
                    }
                    multiplier = candidate;
                }
            } else {
                multiplier = PlayerWindowScaleText.ToMultiplier(scale);
            }

            int clientWidth = renderSize.Width * multiplier;
            int clientHeight = renderSize.Height * multiplier;
            int outerWidth = clientWidth + nonClientWidth;
            int outerHeight = clientHeight + nonClientHeight;
            bool exceeds = !Fits(renderSize, multiplier, workArea, nonClientWidth, nonClientHeight);
            int left = exceeds ? workArea.Left : workArea.Left + (workArea.Width - outerWidth) / 2;
            int top = exceeds ? workArea.Top : workArea.Top + (workArea.Height - outerHeight) / 2;
            return new PlayerWindowLayoutResult(multiplier, clientWidth, clientHeight, left, top, exceeds);
        }

        /// <summary>
        /// Reports whether the outer window at one multiplier fits the work area.
        /// </summary>
        /// <param name="renderSize">Render size.</param>
        /// <param name="multiplier">Candidate multiplier.</param>
        /// <param name="workArea">Work area.</param>
        /// <param name="nonClientWidth">Horizontal border size.</param>
        /// <param name="nonClientHeight">Vertical border and caption size.</param>
        /// <returns>True when the window fits.</returns>
        static bool Fits(PlayerRenderSize renderSize, int multiplier, PlayerScreenArea workArea, int nonClientWidth, int nonClientHeight) {
            return renderSize.Width * multiplier + nonClientWidth <= workArea.Width
                && renderSize.Height * multiplier + nonClientHeight <= workArea.Height;
        }
    }
}
