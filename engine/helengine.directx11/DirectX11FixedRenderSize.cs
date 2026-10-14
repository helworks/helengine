namespace helengine.directx11 {
    /// <summary>
    /// Rules for a window that renders at a fixed size and is magnified on present: the client must be the same integer
    /// multiple of the render size on both axes, so point sampling turns every rendered pixel into an identical square block.
    /// </summary>
    public static class DirectX11FixedRenderSize {
        /// <summary>
        /// Validates the magnification and returns its integer multiplier.
        /// </summary>
        /// <param name="clientWidth">Window client width.</param>
        /// <param name="clientHeight">Window client height.</param>
        /// <param name="renderWidth">Fixed render width.</param>
        /// <param name="renderHeight">Fixed render height.</param>
        /// <returns>The integer multiplier (at least 1).</returns>
        /// <exception cref="ArgumentException">Thrown when the sizes are not positive or not an equal integer multiple.</exception>
        public static int ValidateMagnification(int clientWidth, int clientHeight, int renderWidth, int renderHeight) {
            if (clientWidth <= 0 || clientHeight <= 0 || renderWidth <= 0 || renderHeight <= 0) {
                throw new ArgumentException("Client and render sizes must be positive.");
            } else if (clientWidth % renderWidth != 0 || clientHeight % renderHeight != 0) {
                throw new ArgumentException($"Client {clientWidth}x{clientHeight} is not an integer multiple of render size {renderWidth}x{renderHeight}.");
            } else if (clientWidth / renderWidth != clientHeight / renderHeight) {
                throw new ArgumentException($"Client {clientWidth}x{clientHeight} magnifies render size {renderWidth}x{renderHeight} by different amounts horizontally and vertically.");
            }

            return clientWidth / renderWidth;
        }
    }
}
