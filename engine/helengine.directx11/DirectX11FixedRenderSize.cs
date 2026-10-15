using System.Globalization;

namespace helengine.directx11 {
    /// <summary>
    /// Rules for a window that renders at a fixed size and is magnified on present. The client height is an integer
    /// multiple k of the render height, and the client width is the render width times the display pixel aspect times k,
    /// rounded to the nearest pixel, and never narrower than the render width. With square pixels both axes use the same
    /// integer multiple, so point sampling turns every rendered pixel into an identical block; with non-square pixels (a
    /// console mode shown 4:3) every rendered row is repeated k times and the columns are stretched to the display aspect.
    /// A non-integer column stretch r = client width / render width is magnified sharp-bilinear by
    /// <see cref="DirectX11MagnifyPass"/>, which needs r of at least 1 so each client pixel overlaps at most two columns.
    /// </summary>
    public static class DirectX11FixedRenderSize {
        /// <summary>
        /// Resolves the client width that shows a render width at one multiple and pixel aspect, rounding half away from
        /// zero. This is the only implementation of the width rule; the player's window layout calls it too.
        /// </summary>
        /// <param name="renderWidth">Fixed render width.</param>
        /// <param name="multiplier">Integer vertical magnification (at least 1).</param>
        /// <param name="pixelAspect">Displayed width of one rendered pixel divided by its displayed height.</param>
        /// <returns>The client width in pixels.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for a non-positive width or multiplier, or a non-finite or non-positive pixel aspect.</exception>
        public static int ResolveClientWidth(int renderWidth, int multiplier, double pixelAspect) {
            if (renderWidth <= 0) {
                throw new ArgumentOutOfRangeException(nameof(renderWidth), renderWidth, "The render width must be positive.");
            } else if (multiplier <= 0) {
                throw new ArgumentOutOfRangeException(nameof(multiplier), multiplier, "The magnification must be positive.");
            } else if (!double.IsFinite(pixelAspect) || pixelAspect <= 0d) {
                throw new ArgumentOutOfRangeException(nameof(pixelAspect), pixelAspect, "The pixel aspect must be finite and positive.");
            }

            return (int)Math.Round(renderWidth * pixelAspect * multiplier, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Validates the magnification and returns its integer multiplier.
        /// </summary>
        /// <param name="clientWidth">Window client width.</param>
        /// <param name="clientHeight">Window client height.</param>
        /// <param name="renderWidth">Fixed render width.</param>
        /// <param name="renderHeight">Fixed render height.</param>
        /// <param name="pixelAspect">Displayed width of one rendered pixel divided by its displayed height; 1 for square pixels.</param>
        /// <returns>The integer multiplier (at least 1).</returns>
        /// <exception cref="ArgumentException">Thrown when a size is not positive, the height is not an integer multiple, the width does not match the pixel aspect, or the client is narrower than the render.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the pixel aspect is not finite and positive.</exception>
        public static int ValidateMagnification(int clientWidth, int clientHeight, int renderWidth, int renderHeight, double pixelAspect) {
            if (clientWidth <= 0 || clientHeight <= 0 || renderWidth <= 0 || renderHeight <= 0) {
                throw new ArgumentException("Client and render sizes must be positive.");
            } else if (!double.IsFinite(pixelAspect) || pixelAspect <= 0d) {
                throw new ArgumentOutOfRangeException(nameof(pixelAspect), pixelAspect, "The pixel aspect must be finite and positive.");
            } else if (clientHeight % renderHeight != 0) {
                throw new ArgumentException($"Client height {clientHeight} is not an integer multiple of render height {renderHeight}.");
            }

            int multiplier = clientHeight / renderHeight;
            int expectedWidth = ResolveClientWidth(renderWidth, multiplier, pixelAspect);
            if (clientWidth != expectedWidth) {
                throw new ArgumentException(
                    $"Client {clientWidth}x{clientHeight} does not show render size {renderWidth}x{renderHeight} at pixel aspect "
                    + pixelAspect.ToString("0.####", CultureInfo.InvariantCulture) + $": at {multiplier}x the client width must be {expectedWidth}.");
            } else if (clientWidth < renderWidth) {
                throw new ArgumentException(
                    $"Client width {clientWidth} is narrower than render width {renderWidth}: magnification would squeeze rendered "
                    + "columns, so use a larger multiple whose client width is at least the render width.");
            }

            return multiplier;
        }
    }
}
