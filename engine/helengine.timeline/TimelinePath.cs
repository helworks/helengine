using System.Globalization;

namespace helengine.timeline {
    /// <summary>
    /// Builds the JSON-path-like locations used in timeline diagnostics, such as <c>tracks[2].clips[0].keyframes[1].time</c>,
    /// and formats numbers for diagnostic messages the same way everywhere.
    /// </summary>
    static class TimelinePath {
        /// <summary>
        /// Appends a property name to a path.
        /// </summary>
        /// <param name="path">Parent path; empty for the document root.</param>
        /// <param name="name">snake_case property name.</param>
        /// <returns>The child path.</returns>
        public static string Member(string path, string name) {
            if (path.Length == 0) {
                return name;
            }
            return path + "." + name;
        }

        /// <summary>
        /// Appends an array index to a path.
        /// </summary>
        /// <param name="path">Path of the array.</param>
        /// <param name="index">Zero-based element index.</param>
        /// <returns>The element path.</returns>
        public static string Index(string path, int index) {
            return path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
        }

        /// <summary>
        /// Formats a number compactly and culture-independently for messages.
        /// </summary>
        /// <param name="value">Number to format.</param>
        /// <returns>Text such as <c>0.25</c>, <c>NaN</c> or <c>Infinity</c>.</returns>
        public static string Number(double value) {
            if (double.IsNaN(value)) {
                return "NaN";
            } else if (double.IsPositiveInfinity(value)) {
                return "Infinity";
            } else if (double.IsNegativeInfinity(value)) {
                return "-Infinity";
            }
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }
    }
}
