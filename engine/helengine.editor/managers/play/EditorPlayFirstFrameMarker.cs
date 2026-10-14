using System.Globalization;

namespace helengine.editor {
    /// <summary>
    /// Machine-readable standard-output line the desktop player writes once, right after its first frame is presented:
    /// <c>first-frame &lt;milliseconds since the player process started&gt;</c>. The editor recognizes it by its exact
    /// prefix, uses it to finish the Play startup timing summary and does not echo it as a <c>[player]</c> log line.
    /// </summary>
    public static class EditorPlayFirstFrameMarker {
        /// <summary>
        /// Exact prefix of the marker line, including the separating space.
        /// </summary>
        public const string Prefix = "first-frame ";

        /// <summary>
        /// Builds the marker line for the given time since the player process started.
        /// </summary>
        /// <param name="millisecondsSinceProcessStart">Milliseconds from process start to the first presented frame.</param>
        /// <returns>The marker line, written with the invariant culture.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the milliseconds are negative.</exception>
        public static string Format(long millisecondsSinceProcessStart) {
            if (millisecondsSinceProcessStart < 0) {
                throw new ArgumentOutOfRangeException(nameof(millisecondsSinceProcessStart), millisecondsSinceProcessStart, "The first-frame time cannot be negative.");
            }

            return Prefix + millisecondsSinceProcessStart.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Reports whether a player output line is the first-frame marker.
        /// </summary>
        /// <param name="line">Line the player wrote.</param>
        /// <returns>True when the line starts with <see cref="Prefix"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the line is null.</exception>
        public static bool IsMarker(string line) {
            if (line == null) {
                throw new ArgumentNullException(nameof(line));
            }

            return line.StartsWith(Prefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// Reads the milliseconds since process start from a marker line.
        /// </summary>
        /// <param name="line">Marker line the player wrote.</param>
        /// <returns>Milliseconds from the player process start to its first presented frame.</returns>
        /// <exception cref="ArgumentException">Thrown when the line is not a marker line.</exception>
        /// <exception cref="FormatException">Thrown when the marker does not carry a non-negative whole number.</exception>
        public static long ParseMilliseconds(string line) {
            if (!IsMarker(line)) {
                throw new ArgumentException("The line is not a first-frame marker.", nameof(line));
            }

            string value = line.Substring(Prefix.Length);
            long milliseconds = long.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);
            return milliseconds;
        }
    }
}
