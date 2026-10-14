using System.Globalization;

namespace helengine.editor {
    /// <summary>
    /// Machine-readable standard-output line the desktop player host writes once, right after its first frame is
    /// presented: <c>##helengine.play first-frame-ms=&lt;milliseconds&gt;</c>. The player also forwards every engine and
    /// game log message to stdout unprefixed, so the marker uses a sentinel no log message would plausibly start with.
    /// The editor recognizes it by that exact prefix, uses it to finish the Play startup timing summary and does not
    /// echo it as a <c>[player]</c> log line. Parsing never throws: the line comes from another process.
    /// </summary>
    public static class EditorPlayFirstFrameMarker {
        /// <summary>
        /// Exact prefix of the marker line; the milliseconds follow directly after the equals sign.
        /// </summary>
        public const string Prefix = "##helengine.play first-frame-ms=";

        /// <summary>
        /// Builds the marker line for the given time from player start to the first presented frame.
        /// </summary>
        /// <param name="milliseconds">Milliseconds from player start to the first presented frame.</param>
        /// <returns>The marker line, written with the invariant culture.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the milliseconds are negative.</exception>
        public static string Format(long milliseconds) {
            if (milliseconds < 0) {
                throw new ArgumentOutOfRangeException(nameof(milliseconds), milliseconds, "The first-frame time cannot be negative.");
            }

            return Prefix + milliseconds.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Reports whether a player output line claims to be the first-frame marker, valid or not.
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
        /// Reads the milliseconds from a marker line without throwing. Surrounding white space is tolerated; a sign,
        /// a fraction, other text or a value that does not fit a <see cref="long"/> makes the marker malformed.
        /// </summary>
        /// <param name="line">Line the player wrote.</param>
        /// <param name="milliseconds">Receives the milliseconds when the marker is well formed; zero otherwise.</param>
        /// <returns>True when the line is a well-formed marker.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the line is null.</exception>
        public static bool TryParseMilliseconds(string line, out long milliseconds) {
            if (!IsMarker(line)) {
                milliseconds = 0;
                return false;
            }

            return long.TryParse(
                line.AsSpan(Prefix.Length),
                NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                CultureInfo.InvariantCulture,
                out milliseconds);
        }
    }
}
