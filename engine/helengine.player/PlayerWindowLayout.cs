using helengine.baseplatform.Player;
using helengine.directx11;

namespace helengine.player {
    /// <summary>
    /// Computes the Play window from the render size, the <c>window-scale</c> setting and the chosen monitor's work area.
    /// At multiple k the client is <c>round(W * pixelAspect * k)</c> x <c>H * k</c>, so a framebuffer with non-square
    /// pixels is shown at its display aspect. The minimum multiple is the smallest k whose client width drops no rendered
    /// column (1 for square pixels). <c>fit</c> starts at the minimum multiple and picks the largest multiple whose whole
    /// window (client plus borders) fits. An explicit scale below the minimum multiple is raised to it; an explicit scale
    /// is otherwise honoured even when it does not fit, and such a window opens at the work-area origin.
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

            int minimumMultiplier = ResolveMinimumMultiplier(renderSize);
            int multiplier;
            if (scale == PlayerWindowScale.Fit) {
                multiplier = minimumMultiplier;
                for (int candidate = minimumMultiplier + 1; candidate <= MaximumMultiplier; candidate++) {
                    if (!Fits(renderSize, candidate, workArea, nonClientWidth, nonClientHeight)) {
                        break;
                    }
                    multiplier = candidate;
                }
            } else {
                multiplier = Math.Max(PlayerWindowScaleText.ToMultiplier(scale), minimumMultiplier);
            }

            int clientWidth = DirectX11FixedRenderSize.ResolveClientWidth(renderSize.Width, multiplier, renderSize.PixelAspect);
            int clientHeight = renderSize.Height * multiplier;
            int outerWidth = clientWidth + nonClientWidth;
            int outerHeight = clientHeight + nonClientHeight;
            bool exceeds = !Fits(renderSize, multiplier, workArea, nonClientWidth, nonClientHeight);
            int left = exceeds ? workArea.Left : workArea.Left + (workArea.Width - outerWidth) / 2;
            int top = exceeds ? workArea.Top : workArea.Top + (workArea.Height - outerHeight) / 2;
            return new PlayerWindowLayoutResult(multiplier, clientWidth, clientHeight, left, top, exceeds);
        }

        /// <summary>
        /// Resolves the smallest multiple whose client width is at least the render width, so point sampling drops no
        /// rendered column. Square pixels and pixel aspects of 1 or more always give 1.
        /// </summary>
        /// <param name="renderSize">Platform render size and pixel aspect.</param>
        /// <returns>The minimum multiple.</returns>
        /// <exception cref="ArgumentException">Thrown when no multiple up to <see cref="MaximumMultiplier"/> is wide enough.</exception>
        public static int ResolveMinimumMultiplier(PlayerRenderSize renderSize) {
            if (renderSize == null) {
                throw new ArgumentNullException(nameof(renderSize));
            }

            for (int candidate = 1; candidate <= MaximumMultiplier; candidate++) {
                if (DirectX11FixedRenderSize.ResolveClientWidth(renderSize.Width, candidate, renderSize.PixelAspect) >= renderSize.Width) {
                    return candidate;
                }
            }

            throw new ArgumentException($"Render size {renderSize.Width}x{renderSize.Height} needs more than {MaximumMultiplier}x to show every pixel column at its pixel aspect.", nameof(renderSize));
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
            return DirectX11FixedRenderSize.ResolveClientWidth(renderSize.Width, multiplier, renderSize.PixelAspect) + nonClientWidth <= workArea.Width
                && renderSize.Height * multiplier + nonClientHeight <= workArea.Height;
        }
    }
}
