using System.Globalization;

namespace helengine.bepu.tests {
    /// <summary>
    /// Parses one shared reduced-BEPU differential trace line into a structured record for test-side comparison.
    /// </summary>
    public static class BepuDifferentialTraceParser {
        /// <summary>
        /// Parses one differential trace line produced by the shared schema writer.
        /// </summary>
        /// <param name="traceLine">Line-oriented schema text to parse.</param>
        /// <returns>Structured differential trace record.</returns>
        public static BepuDifferentialTraceRecord3D ParseLine(string traceLine) {
            if (string.IsNullOrWhiteSpace(traceLine)) {
                throw new ArgumentException("Differential trace text cannot be null or whitespace.", nameof(traceLine));
            }

            Dictionary<string, string> values = ParseKeyValuePairs(traceLine);
            BepuDifferentialTraceRecord3D record = new BepuDifferentialTraceRecord3D();
            record.Frame = ParseRequiredInt(values, "frame");
            record.Phase = ParsePhase(ParseRequiredString(values, "phase"));
            record.BodyHandle = ParseRequiredInt(values, "body_handle");
            record.BodyIndex = ParseRequiredInt(values, "body_index");
            record.BundleIndex = ParseOptionalInt(values, "bundle_index");
            record.ConstraintBatchIndex = ParseOptionalInt(values, "constraint_batch");
            record.TypeBatchIndex = ParseOptionalInt(values, "type_batch");
            record.BodySlotIndex = ParseOptionalInt(values, "body_slot");
            record.EncodedReferences = ParseOptionalString(values, "encoded_refs");
            record.IntegrationMask = ParseOptionalString(values, "integration_mask");
            record.Position = ParseFloat3(ParseRequiredString(values, "position"));
            record.Orientation = ParseFloat4(ParseRequiredString(values, "orientation"));
            record.LinearVelocity = ParseFloat3(ParseRequiredString(values, "linear_velocity"));
            record.AngularVelocity = ParseFloat3(ParseRequiredString(values, "angular_velocity"));
            return record;
        }

        /// <summary>
        /// Parses one trace line into ordered key-value pairs while preserving comma-containing vector payloads.
        /// </summary>
        /// <param name="traceLine">Trace line to parse.</param>
        /// <returns>Dictionary of parsed schema tokens.</returns>
        static Dictionary<string, string> ParseKeyValuePairs(string traceLine) {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
            int index = 0;
            while (index < traceLine.Length) {
                SkipWhitespace(traceLine, ref index);
                if (index >= traceLine.Length) {
                    break;
                }

                int equalsIndex = traceLine.IndexOf('=', index);
                if (equalsIndex < 0) {
                    throw new FormatException($"Missing '=' after schema key in differential trace line '{traceLine}'.");
                }

                string key = traceLine.Substring(index, equalsIndex - index);
                index = equalsIndex + 1;
                string value = ReadValue(traceLine, ref index);
                values[key] = value;
            }

            return values;
        }

        /// <summary>
        /// Reads one schema value, preserving balanced parenthesized vector payloads.
        /// </summary>
        /// <param name="traceLine">Trace line containing the current value.</param>
        /// <param name="index">Current read offset, advanced to the next key or line end.</param>
        /// <returns>Raw value text for the current key.</returns>
        static string ReadValue(string traceLine, ref int index) {
            int start = index;
            int parenthesisDepth = 0;
            while (index < traceLine.Length) {
                char current = traceLine[index];
                if (current == '(') {
                    parenthesisDepth++;
                }
                else if (current == ')') {
                    parenthesisDepth--;
                }
                else if (current == ' ' && parenthesisDepth == 0) {
                    break;
                }

                index++;
            }

            string value = traceLine.Substring(start, index - start);
            SkipWhitespace(traceLine, ref index);
            return value;
        }

        /// <summary>
        /// Advances the read offset beyond any ASCII spaces between schema tokens.
        /// </summary>
        /// <param name="traceLine">Trace line being parsed.</param>
        /// <param name="index">Current read offset to update.</param>
        static void SkipWhitespace(string traceLine, ref int index) {
            while (index < traceLine.Length && traceLine[index] == ' ') {
                index++;
            }
        }

        /// <summary>
        /// Parses one required integer field.
        /// </summary>
        /// <param name="values">Schema token dictionary.</param>
        /// <param name="key">Required field name.</param>
        /// <returns>Parsed integer value.</returns>
        static int ParseRequiredInt(Dictionary<string, string> values, string key) {
            return int.Parse(ParseRequiredString(values, key), CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parses one optional integer field and returns `-1` when the field is absent.
        /// </summary>
        /// <param name="values">Schema token dictionary.</param>
        /// <param name="key">Optional field name.</param>
        /// <returns>Parsed integer value or `-1` when absent.</returns>
        static int ParseOptionalInt(Dictionary<string, string> values, string key) {
            if (!values.TryGetValue(key, out string value)) {
                return -1;
            }

            return int.Parse(value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parses one required string field and throws when the field is absent.
        /// </summary>
        /// <param name="values">Schema token dictionary.</param>
        /// <param name="key">Required field name.</param>
        /// <returns>Raw field value.</returns>
        static string ParseRequiredString(Dictionary<string, string> values, string key) {
            if (!values.TryGetValue(key, out string value) || string.IsNullOrEmpty(value)) {
                throw new FormatException($"Missing required differential trace field '{key}'.");
            }

            return value;
        }

        /// <summary>
        /// Parses one optional string field and returns an empty string when the field is absent.
        /// </summary>
        /// <param name="values">Schema token dictionary.</param>
        /// <param name="key">Optional field name.</param>
        /// <returns>Raw field value or an empty string when absent.</returns>
        static string ParseOptionalString(Dictionary<string, string> values, string key) {
            if (!values.TryGetValue(key, out string value)) {
                return string.Empty;
            }

            return value;
        }

        /// <summary>
        /// Parses one shared phase token back into the corresponding enum value.
        /// </summary>
        /// <param name="phaseName">Phase token to parse.</param>
        /// <returns>Parsed phase enum value.</returns>
        static BepuDifferentialTracePhase3D ParsePhase(string phaseName) {
            if (phaseName == "integrate_velocity_callback") {
                return BepuDifferentialTracePhase3D.IntegrateVelocityCallback;
            }
            else if (phaseName == "integration_responsibility_assignment") {
                return BepuDifferentialTracePhase3D.IntegrationResponsibilityAssignment;
            }
            else if (phaseName == "gather_and_integrate_before") {
                return BepuDifferentialTracePhase3D.GatherAndIntegrateBefore;
            }
            else if (phaseName == "gather_and_integrate_after") {
                return BepuDifferentialTracePhase3D.GatherAndIntegrateAfter;
            }
            else if (phaseName == "two_body_solve_before") {
                return BepuDifferentialTracePhase3D.TwoBodySolveBefore;
            }
            else if (phaseName == "two_body_solve_after") {
                return BepuDifferentialTracePhase3D.TwoBodySolveAfter;
            }
            else if (phaseName == "sync_snapshot") {
                return BepuDifferentialTracePhase3D.SyncSnapshot;
            }

            throw new FormatException($"Unsupported differential trace phase token '{phaseName}'.");
        }

        /// <summary>
        /// Parses one compact three-component vector token.
        /// </summary>
        /// <param name="value">Vector token to parse.</param>
        /// <returns>Parsed three-component float vector.</returns>
        static float3 ParseFloat3(string value) {
            string[] parts = SplitVector(value, 3);
            return new float3(
                float.Parse(parts[0], CultureInfo.InvariantCulture),
                float.Parse(parts[1], CultureInfo.InvariantCulture),
                float.Parse(parts[2], CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Parses one compact four-component vector token.
        /// </summary>
        /// <param name="value">Vector token to parse.</param>
        /// <returns>Parsed four-component float vector.</returns>
        static float4 ParseFloat4(string value) {
            string[] parts = SplitVector(value, 4);
            return new float4(
                float.Parse(parts[0], CultureInfo.InvariantCulture),
                float.Parse(parts[1], CultureInfo.InvariantCulture),
                float.Parse(parts[2], CultureInfo.InvariantCulture),
                float.Parse(parts[3], CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Splits one parenthesized vector token into the required number of scalar components.
        /// </summary>
        /// <param name="value">Vector token to split.</param>
        /// <param name="expectedCount">Expected scalar component count.</param>
        /// <returns>Array of raw scalar component tokens.</returns>
        static string[] SplitVector(string value, int expectedCount) {
            if (value.Length < 2 || value[0] != '(' || value[value.Length - 1] != ')') {
                throw new FormatException($"Vector token '{value}' is not wrapped in parentheses.");
            }

            string[] parts = value.Substring(1, value.Length - 2).Split(',');
            if (parts.Length != expectedCount) {
                throw new FormatException($"Vector token '{value}' contained {parts.Length} parts instead of {expectedCount}.");
            }

            return parts;
        }
    }
}
