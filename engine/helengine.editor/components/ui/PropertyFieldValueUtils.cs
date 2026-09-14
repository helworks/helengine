using System.Globalization;

namespace helengine.editor {
    /// <summary>
    /// Provides the shared invariant-culture parsing and formatting used by every numeric property editor in the
    /// inspector, so the transform panel and the reflected component inspector agree on how field text is read
    /// back and how values are rendered into text boxes and their change-detection caches.
    /// </summary>
    public static class PropertyFieldValueUtils {
        /// <summary>
        /// Numeric format applied to every editable double shown in a property field.
        /// </summary>
        const string NumberFormat = "0.###";

        /// <summary>
        /// Formats a double for display in a property text field using invariant culture.
        /// </summary>
        /// <param name="value">Value to format.</param>
        /// <returns>Formatted text with at most three fractional digits.</returns>
        public static string FormatDouble(double value) {
            return value.ToString(NumberFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parses a numeric property field using invariant culture.
        /// </summary>
        /// <param name="text">Text to parse.</param>
        /// <param name="value">Parsed numeric value, or zero when parsing fails.</param>
        /// <returns>True when the text holds a parsable number.</returns>
        public static bool TryReadNumber(string text, out double value) {
            if (string.IsNullOrWhiteSpace(text)) {
                value = 0.0;
                return false;
            }

            return double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        }

        /// <summary>
        /// Parses the first three text boxes of a vector field group.
        /// </summary>
        /// <param name="fields">Field group holding the X, Y and Z text boxes.</param>
        /// <param name="x">Parsed X value.</param>
        /// <param name="y">Parsed Y value.</param>
        /// <param name="z">Parsed Z value.</param>
        /// <returns>True when the group is populated and every component parses.</returns>
        public static bool TryReadVector(TextBoxComponent[] fields, out double x, out double y, out double z) {
            x = 0.0;
            y = 0.0;
            z = 0.0;

            if (fields == null || fields.Length < 3) {
                return false;
            }

            if (!TryReadNumber(fields[0].Text, out x)) {
                return false;
            }
            if (!TryReadNumber(fields[1].Text, out y)) {
                return false;
            }
            if (!TryReadNumber(fields[2].Text, out z)) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Parses the first four text boxes of a vector field group.
        /// </summary>
        /// <param name="fields">Field group holding the X, Y, Z and W text boxes.</param>
        /// <param name="x">Parsed X value.</param>
        /// <param name="y">Parsed Y value.</param>
        /// <param name="z">Parsed Z value.</param>
        /// <param name="w">Parsed W value.</param>
        /// <returns>True when the group is populated and every component parses.</returns>
        public static bool TryReadVector4(TextBoxComponent[] fields, out double x, out double y, out double z, out double w) {
            x = 0.0;
            y = 0.0;
            z = 0.0;
            w = 0.0;

            if (fields == null || fields.Length < 4) {
                return false;
            }

            if (!TryReadNumber(fields[0].Text, out x)) {
                return false;
            }
            if (!TryReadNumber(fields[1].Text, out y)) {
                return false;
            }
            if (!TryReadNumber(fields[2].Text, out z)) {
                return false;
            }
            if (!TryReadNumber(fields[3].Text, out w)) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Writes three formatted components into a vector field group and its change-detection cache.
        /// </summary>
        /// <param name="fields">Field group receiving the formatted text.</param>
        /// <param name="cache">Cache receiving the same text so later edits can be detected.</param>
        /// <param name="x">X value to write.</param>
        /// <param name="y">Y value to write.</param>
        /// <param name="z">Z value to write.</param>
        public static void WriteVectorFields(TextBoxComponent[] fields, string[] cache, double x, double y, double z) {
            string xText = FormatDouble(x);
            string yText = FormatDouble(y);
            string zText = FormatDouble(z);

            fields[0].Text = xText;
            fields[1].Text = yText;
            fields[2].Text = zText;

            cache[0] = xText;
            cache[1] = yText;
            cache[2] = zText;
        }

        /// <summary>
        /// Writes four formatted components into a vector field group and its change-detection cache.
        /// </summary>
        /// <param name="fields">Field group receiving the formatted text.</param>
        /// <param name="cache">Cache receiving the same text so later edits can be detected.</param>
        /// <param name="x">X value to write.</param>
        /// <param name="y">Y value to write.</param>
        /// <param name="z">Z value to write.</param>
        /// <param name="w">W value to write.</param>
        public static void WriteVector4Fields(TextBoxComponent[] fields, string[] cache, double x, double y, double z, double w) {
            string xText = FormatDouble(x);
            string yText = FormatDouble(y);
            string zText = FormatDouble(z);
            string wText = FormatDouble(w);

            fields[0].Text = xText;
            fields[1].Text = yText;
            fields[2].Text = zText;
            fields[3].Text = wText;

            cache[0] = xText;
            cache[1] = yText;
            cache[2] = zText;
            cache[3] = wText;
        }
    }
}
