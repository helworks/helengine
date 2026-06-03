using System.Text;

namespace helengine {
    /// <summary>
    /// Formats reduced-BEPU differential trace records into compact line-oriented text that can be compared across runtimes.
    /// </summary>
    public static class BepuDifferentialTraceWriter3D {
        /// <summary>
        /// Stores the fixed decimal scale used to serialize floating-point values without culture-sensitive formatting APIs.
        /// </summary>
        const long TraceFloatScale = 1000000000L;

        /// <summary>
        /// Formats one differential trace record into one deterministic schema line.
        /// </summary>
        /// <param name="record">Trace record to format.</param>
        /// <returns>One compact line without a trailing newline.</returns>
        public static string WriteLine(BepuDifferentialTraceRecord3D record) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }

            StringBuilder builder = new StringBuilder();
            builder.Append("frame=");
            builder.Append(record.Frame);
            builder.Append(" phase=");
            builder.Append(GetPhaseName(record.Phase));
            builder.Append(" body_handle=");
            builder.Append(record.BodyHandle);
            builder.Append(" body_index=");
            builder.Append(record.BodyIndex);

            if (record.BundleIndex >= 0) {
                builder.Append(" bundle_index=");
                builder.Append(record.BundleIndex);
            }

            if (record.ConstraintBatchIndex >= 0) {
                builder.Append(" constraint_batch=");
                builder.Append(record.ConstraintBatchIndex);
            }

            if (record.TypeBatchIndex >= 0) {
                builder.Append(" type_batch=");
                builder.Append(record.TypeBatchIndex);
            }

            if (record.BodySlotIndex >= 0) {
                builder.Append(" body_slot=");
                builder.Append(record.BodySlotIndex);
            }

            if (!string.IsNullOrEmpty(record.EncodedReferences)) {
                builder.Append(" encoded_refs=");
                builder.Append(record.EncodedReferences);
            }

            if (!string.IsNullOrEmpty(record.IntegrationMask)) {
                builder.Append(" integration_mask=");
                builder.Append(record.IntegrationMask);
            }

            builder.Append(" position=");
            builder.Append(FormatFloat3(record.Position));
            builder.Append(" orientation=");
            builder.Append(FormatFloat4(record.Orientation));
            builder.Append(" linear_velocity=");
            builder.Append(FormatFloat3(record.LinearVelocity));
            builder.Append(" angular_velocity=");
            builder.Append(FormatFloat3(record.AngularVelocity));
            return builder.ToString();
        }

        /// <summary>
        /// Returns the canonical lowercase phase token used in differential trace lines.
        /// </summary>
        /// <param name="phase">Phase value to convert.</param>
        /// <returns>Canonical schema token for the provided phase.</returns>
        static string GetPhaseName(BepuDifferentialTracePhase3D phase) {
            if (phase == BepuDifferentialTracePhase3D.IntegrateVelocityCallback) {
                return "integrate_velocity_callback";
            }
            else if (phase == BepuDifferentialTracePhase3D.IntegrationResponsibilityAssignment) {
                return "integration_responsibility_assignment";
            }
            else if (phase == BepuDifferentialTracePhase3D.GatherAndIntegrateBefore) {
                return "gather_and_integrate_before";
            }
            else if (phase == BepuDifferentialTracePhase3D.GatherAndIntegrateAfter) {
                return "gather_and_integrate_after";
            }
            else if (phase == BepuDifferentialTracePhase3D.TwoBodySolveBefore) {
                return "two_body_solve_before";
            }
            else if (phase == BepuDifferentialTracePhase3D.TwoBodySolveAfter) {
                return "two_body_solve_after";
            }
            else if (phase == BepuDifferentialTracePhase3D.SyncSnapshot) {
                return "sync_snapshot";
            }

            throw new InvalidOperationException($"Unsupported BEPU differential trace phase '{phase}'.");
        }

        /// <summary>
        /// Formats one three-component vector using compact invariant numeric tokens.
        /// </summary>
        /// <param name="value">Vector to format.</param>
        /// <returns>Compact tuple-like text suitable for one trace line.</returns>
        static string FormatFloat3(float3 value) {
            return $"({FormatFloat(value.X)},{FormatFloat(value.Y)},{FormatFloat(value.Z)})";
        }

        /// <summary>
        /// Formats one four-component vector using compact invariant numeric tokens.
        /// </summary>
        /// <param name="value">Vector to format.</param>
        /// <returns>Compact tuple-like text suitable for one trace line.</returns>
        static string FormatFloat4(float4 value) {
            return $"({FormatFloat(value.X)},{FormatFloat(value.Y)},{FormatFloat(value.Z)},{FormatFloat(value.W)})";
        }

        /// <summary>
        /// Formats one scalar using invariant compact precision suitable for managed-versus-native comparison.
        /// </summary>
        /// <param name="value">Scalar value to format.</param>
        /// <returns>Compact invariant scalar text.</returns>
        static string FormatFloat(float value) {
            if (value == 0f) {
                return "0";
            }

            long scaledValue = (long)System.Math.Round(System.Math.Abs((double)value) * TraceFloatScale);
            long wholePart = scaledValue / TraceFloatScale;
            long fractionalPart = scaledValue % TraceFloatScale;
            StringBuilder builder = new StringBuilder();
            if (value < 0f) {
                builder.Append('-');
            }

            builder.Append((int)wholePart);
            if (fractionalPart == 0L) {
                return builder.ToString();
            }

            builder.Append('.');
            AppendFractionDigits(builder, fractionalPart);
            return builder.ToString();
        }

        /// <summary>
        /// Appends one trimmed fixed-scale fractional component using exactly the digits required by the stored scale.
        /// </summary>
        /// <param name="builder">Destination builder receiving the fractional digits.</param>
        /// <param name="fractionalPart">Scaled fractional component to serialize.</param>
        static void AppendFractionDigits(StringBuilder builder, long fractionalPart) {
            long trimmedFractionalPart = fractionalPart;
            int lastDigitIndex = 8;
            while (lastDigitIndex >= 0 && trimmedFractionalPart % 10L == 0L) {
                trimmedFractionalPart /= 10L;
                lastDigitIndex--;
            }

            long divisor = TraceFloatScale / 10L;
            for (int digitIndex = 0; digitIndex <= lastDigitIndex; digitIndex++) {
                long digit = fractionalPart / divisor;
                builder.Append((char)('0' + digit));
                fractionalPart -= digit * divisor;
                divisor /= 10L;
            }
        }
    }
}
