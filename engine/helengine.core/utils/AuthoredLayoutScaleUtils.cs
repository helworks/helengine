namespace helengine {
    /// <summary>
    /// Maps layout values written into a scaled subtree back into authored space for the editor. A value that still equals
    /// what the last scale pass wrote is viewport output and resolves to the captured authored value; any other value is a
    /// user edit made in the scaled preview and is divided back through the scale that pass used.
    /// </summary>
    public static class AuthoredLayoutScaleUtils {
        /// <summary>
        /// Resolves the authored value for one RealEvaluation field.
        /// </summary>
        /// <param name="live">Current live value.</param>
        /// <param name="applied">Value written by the last scale pass.</param>
        /// <param name="authored">Captured authored value.</param>
        /// <param name="scale">Scale used by the last scale pass.</param>
        /// <returns>Authored value that should be persisted and scaled next.</returns>
        public static RealEvaluation UnscaleFloat(RealEvaluation live, RealEvaluation applied, RealEvaluation authored, RealEvaluation scale) {
            if (live == applied || !(scale > 0d)) {
                return authored;
            }

            return (RealEvaluation)(live / scale);
        }

        /// <summary>
        /// Resolves the authored value for one integer field, keeping zero at zero and non-zero values at least one pixel.
        /// </summary>
        /// <param name="live">Current live value.</param>
        /// <param name="applied">Value written by the last scale pass.</param>
        /// <param name="authored">Captured authored value.</param>
        /// <param name="scale">Scale used by the last scale pass.</param>
        /// <returns>Authored value that should be persisted and scaled next.</returns>
        public static int UnscaleInt(int live, int applied, int authored, RealEvaluation scale) {
            if (live == applied || !(scale > 0d)) {
                return authored;
            }
            if (live == 0) {
                return 0;
            }

            return RealMath.Max(1, (int)RealMath.Round(live / scale));
        }

        /// <summary>
        /// Resolves the authored value for one integer vector, mapping each edited axis through its own scale.
        /// </summary>
        /// <param name="live">Current live value.</param>
        /// <param name="applied">Value written by the last scale pass.</param>
        /// <param name="authored">Captured authored value.</param>
        /// <param name="widthScale">Horizontal scale used by the last scale pass.</param>
        /// <param name="heightScale">Vertical scale used by the last scale pass.</param>
        /// <returns>Authored vector that should be persisted and scaled next.</returns>
        public static int2 UnscaleInt2(int2 live, int2 applied, int2 authored, RealEvaluation widthScale, RealEvaluation heightScale) {
            return new int2(
                UnscaleInt(live.X, applied.X, authored.X, widthScale),
                UnscaleInt(live.Y, applied.Y, authored.Y, heightScale));
        }

        /// <summary>
        /// Resolves the authored value for one position axis, removing the root displacement before unscaling an edit.
        /// </summary>
        /// <param name="live">Current live coordinate.</param>
        /// <param name="applied">Coordinate written by the last scale pass.</param>
        /// <param name="authored">Captured authored coordinate.</param>
        /// <param name="origin">Root displacement added by the last scale pass, or zero for non-root entities.</param>
        /// <param name="scale">Axis scale used by the last scale pass.</param>
        /// <returns>Authored coordinate that should be persisted and scaled next.</returns>
        public static RealEvaluation UnscalePosition(RealEvaluation live, RealEvaluation applied, RealEvaluation authored, RealEvaluation origin, RealEvaluation scale) {
            if (live == applied || !(scale > 0d)) {
                return authored;
            }

            return (RealEvaluation)((live - origin) / scale);
        }
    }
}
