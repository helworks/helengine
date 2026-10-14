namespace helengine.baseplatform.Player {
    /// <summary>
    /// Converts <see cref="PlayerWindowScale"/> to and from the <c>window-scale</c> Play setting text (<c>1x</c> to <c>4x</c>, <c>fit</c>).
    /// </summary>
    public static class PlayerWindowScaleText {
        /// <summary>
        /// Parses one validated <c>window-scale</c> value.
        /// </summary>
        /// <param name="text">Setting text.</param>
        /// <returns>The window scale.</returns>
        /// <exception cref="ArgumentException">Thrown when the text is not a window scale.</exception>
        public static PlayerWindowScale Parse(string text) {
            if (string.Equals(text, "1x", StringComparison.Ordinal)) {
                return PlayerWindowScale.One;
            } else if (string.Equals(text, "2x", StringComparison.Ordinal)) {
                return PlayerWindowScale.Two;
            } else if (string.Equals(text, "3x", StringComparison.Ordinal)) {
                return PlayerWindowScale.Three;
            } else if (string.Equals(text, "4x", StringComparison.Ordinal)) {
                return PlayerWindowScale.Four;
            } else if (string.Equals(text, "fit", StringComparison.Ordinal)) {
                return PlayerWindowScale.Fit;
            }

            throw new ArgumentException($"'{text}' is not a window scale; expected 1x, 2x, 3x, 4x or fit.", nameof(text));
        }

        /// <summary>
        /// Formats one window scale as its setting text.
        /// </summary>
        /// <param name="scale">Window scale.</param>
        /// <returns>The setting text.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for an undefined enum value.</exception>
        public static string Format(PlayerWindowScale scale) {
            switch (scale) {
                case PlayerWindowScale.One: return "1x";
                case PlayerWindowScale.Two: return "2x";
                case PlayerWindowScale.Three: return "3x";
                case PlayerWindowScale.Four: return "4x";
                case PlayerWindowScale.Fit: return "fit";
                default: throw new ArgumentOutOfRangeException(nameof(scale), scale, "Unknown window scale.");
            }
        }

        /// <summary>
        /// Returns the integer multiplier of a fixed window scale.
        /// </summary>
        /// <param name="scale">Fixed window scale.</param>
        /// <returns>1 to 4.</returns>
        /// <exception cref="ArgumentException">Thrown for <see cref="PlayerWindowScale.Fit"/>, whose multiplier depends on the monitor.</exception>
        public static int ToMultiplier(PlayerWindowScale scale) {
            if (scale == PlayerWindowScale.Fit) {
                throw new ArgumentException("The fit window scale has no fixed multiplier; compute it from the monitor work area.", nameof(scale));
            }

            return (int)scale + 1;
        }
    }
}
