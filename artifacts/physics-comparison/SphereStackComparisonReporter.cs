using System.Globalization;
using System.Numerics;

namespace Helengine.PhysicsComparison {
    /// <summary>
    /// Compares BEPU and helengine sphere-stack traces and writes the timing divergence summary.
    /// </summary>
    public sealed class SphereStackComparisonReporter {
        /// <summary>
        /// Stable body names emitted by the eight-sphere stack runners.
        /// </summary>
        static readonly string[] BodyNames = [
            "sphere01",
            "sphere02",
            "sphere03",
            "sphere04",
            "sphere05",
            "sphere06",
            "sphere07",
            "sphere08"
        ];

        /// <summary>
        /// Writes the sphere-stack comparison summary file.
        /// </summary>
        /// <param name="outputDirectoryPath">Directory that receives the summary.</param>
        /// <param name="bepuSamples">BEPU trace samples.</param>
        /// <param name="helengineSamples">Helengine trace samples.</param>
        public void WriteReport(string outputDirectoryPath, IReadOnlyList<PhysicsTraceSample> bepuSamples, IReadOnlyList<PhysicsTraceSample> helengineSamples) {
            string reportPath = Path.Combine(outputDirectoryPath, "sphere-comparison-summary.txt");
            using StreamWriter writer = new StreamWriter(reportPath);
            for (int index = 0; index < BodyNames.Length; index++) {
                WriteFinalBodyComparison(writer, BodyNames[index], bepuSamples, helengineSamples);
            }

            WriteFallTimingComparison(writer, bepuSamples, helengineSamples);
            WriteFirstDivergence(writer, "sphere08", bepuSamples, helengineSamples);
        }

        /// <summary>
        /// Writes final state comparison for one body.
        /// </summary>
        /// <param name="writer">Report writer.</param>
        /// <param name="bodyName">Body label to compare.</param>
        /// <param name="bepuSamples">BEPU trace samples.</param>
        /// <param name="helengineSamples">Helengine trace samples.</param>
        static void WriteFinalBodyComparison(StreamWriter writer, string bodyName, IReadOnlyList<PhysicsTraceSample> bepuSamples, IReadOnlyList<PhysicsTraceSample> helengineSamples) {
            PhysicsTraceSample bepuFinal = FindFinalSample(bepuSamples, bodyName);
            PhysicsTraceSample helengineFinal = FindFinalSample(helengineSamples, bodyName);
            writer.WriteLine("Final " + bodyName);
            writer.WriteLine("  bepu.position=" + Format(bepuFinal.Position) + " velocity=" + Format(bepuFinal.LinearVelocity));
            writer.WriteLine("  helengine.position=" + Format(helengineFinal.Position) + " velocity=" + Format(helengineFinal.LinearVelocity));
            writer.WriteLine("  delta.position=" + Format(helengineFinal.Position - bepuFinal.Position) + " delta.velocity=" + Format(helengineFinal.LinearVelocity - bepuFinal.LinearVelocity));
            writer.WriteLine();
        }

        /// <summary>
        /// Writes the first time each sphere crosses selected height thresholds.
        /// </summary>
        /// <param name="writer">Report writer.</param>
        /// <param name="bepuSamples">BEPU trace samples.</param>
        /// <param name="helengineSamples">Helengine trace samples.</param>
        static void WriteFallTimingComparison(StreamWriter writer, IReadOnlyList<PhysicsTraceSample> bepuSamples, IReadOnlyList<PhysicsTraceSample> helengineSamples) {
            writer.WriteLine("Fall timing");
            for (int index = 0; index < BodyNames.Length; index++) {
                string bodyName = BodyNames[index];
                float initialHeight = 0.5f + index;
                float threshold = initialHeight - 0.5f;
                PhysicsTraceSample bepuCrossing = FindFirstBelowHeight(bepuSamples, bodyName, threshold);
                PhysicsTraceSample helengineCrossing = FindFirstBelowHeight(helengineSamples, bodyName, threshold);
                writer.WriteLine("  " + bodyName + " below " + threshold.ToString("R", CultureInfo.InvariantCulture) +
                    " bepu=" + FormatOptionalTime(bepuCrossing) +
                    " helengine=" + FormatOptionalTime(helengineCrossing));
            }

            writer.WriteLine();
        }

        /// <summary>
        /// Writes the first step where the compared body diverges materially.
        /// </summary>
        /// <param name="writer">Report writer.</param>
        /// <param name="bodyName">Body label to compare.</param>
        /// <param name="bepuSamples">BEPU trace samples.</param>
        /// <param name="helengineSamples">Helengine trace samples.</param>
        static void WriteFirstDivergence(StreamWriter writer, string bodyName, IReadOnlyList<PhysicsTraceSample> bepuSamples, IReadOnlyList<PhysicsTraceSample> helengineSamples) {
            for (int index = 0; index < bepuSamples.Count; index++) {
                PhysicsTraceSample bepuSample = bepuSamples[index];
                if (bepuSample.BodyName != bodyName) {
                    continue;
                }

                PhysicsTraceSample helengineSample = FindSample(helengineSamples, bodyName, bepuSample.StepIndex);
                float positionDelta = Vector3.Distance(bepuSample.Position, helengineSample.Position);
                float velocityDelta = Vector3.Distance(bepuSample.LinearVelocity, helengineSample.LinearVelocity);
                if (positionDelta > 0.25f || velocityDelta > 0.5f) {
                    writer.WriteLine("First material " + bodyName + " divergence");
                    writer.WriteLine("  step=" + bepuSample.StepIndex.ToString(CultureInfo.InvariantCulture) + " time=" + bepuSample.TimeSeconds.ToString("R", CultureInfo.InvariantCulture));
                    writer.WriteLine("  positionDelta=" + positionDelta.ToString("R", CultureInfo.InvariantCulture) + " velocityDelta=" + velocityDelta.ToString("R", CultureInfo.InvariantCulture));
                    writer.WriteLine("  bepu.position=" + Format(bepuSample.Position) + " helengine.position=" + Format(helengineSample.Position));
                    writer.WriteLine("  bepu.velocity=" + Format(bepuSample.LinearVelocity) + " helengine.velocity=" + Format(helengineSample.LinearVelocity));
                    return;
                }
            }

            writer.WriteLine("No material divergence found for " + bodyName + ".");
        }

        /// <summary>
        /// Finds the final sample for a body.
        /// </summary>
        /// <param name="samples">Samples to inspect.</param>
        /// <param name="bodyName">Body label to find.</param>
        /// <returns>Final sample for the body.</returns>
        static PhysicsTraceSample FindFinalSample(IReadOnlyList<PhysicsTraceSample> samples, string bodyName) {
            for (int index = samples.Count - 1; index >= 0; index--) {
                if (samples[index].BodyName == bodyName) {
                    return samples[index];
                }
            }

            throw new InvalidOperationException("Trace does not contain body '" + bodyName + "'.");
        }

        /// <summary>
        /// Finds the first sample where one body is below the supplied height.
        /// </summary>
        /// <param name="samples">Samples to inspect.</param>
        /// <param name="bodyName">Body label to find.</param>
        /// <param name="height">Height threshold.</param>
        /// <returns>First matching sample, or null when the body never crosses the threshold.</returns>
        static PhysicsTraceSample FindFirstBelowHeight(IReadOnlyList<PhysicsTraceSample> samples, string bodyName, float height) {
            for (int index = 0; index < samples.Count; index++) {
                if (samples[index].BodyName == bodyName && samples[index].Position.Y < height) {
                    return samples[index];
                }
            }

            return null;
        }

        /// <summary>
        /// Finds a sample for one body and step.
        /// </summary>
        /// <param name="samples">Samples to inspect.</param>
        /// <param name="bodyName">Body label to find.</param>
        /// <param name="stepIndex">Step index to find.</param>
        /// <returns>Matching sample.</returns>
        static PhysicsTraceSample FindSample(IReadOnlyList<PhysicsTraceSample> samples, string bodyName, int stepIndex) {
            for (int index = 0; index < samples.Count; index++) {
                if (samples[index].BodyName == bodyName && samples[index].StepIndex == stepIndex) {
                    return samples[index];
                }
            }

            throw new InvalidOperationException("Trace does not contain body '" + bodyName + "' at step " + stepIndex.ToString(CultureInfo.InvariantCulture) + ".");
        }

        /// <summary>
        /// Formats an optional crossing sample time.
        /// </summary>
        /// <param name="sample">Crossing sample, or null when no crossing occurred.</param>
        /// <returns>Formatted time text.</returns>
        static string FormatOptionalTime(PhysicsTraceSample sample) {
            if (sample == null) {
                return "never";
            }

            return sample.TimeSeconds.ToString("R", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Formats one vector for report text.
        /// </summary>
        /// <param name="value">Vector to format.</param>
        /// <returns>Formatted vector.</returns>
        static string Format(Vector3 value) {
            return "(" +
                value.X.ToString("R", CultureInfo.InvariantCulture) + ", " +
                value.Y.ToString("R", CultureInfo.InvariantCulture) + ", " +
                value.Z.ToString("R", CultureInfo.InvariantCulture) + ")";
        }
    }
}
