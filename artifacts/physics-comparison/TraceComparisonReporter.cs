using System.Globalization;
using System.Numerics;

namespace Helengine.PhysicsComparison {
    /// <summary>
    /// Compares BEPU and helengine traces and writes a concise summary.
    /// </summary>
    public sealed class TraceComparisonReporter {
        /// <summary>
        /// Stable body names emitted by the eight-box tower runners.
        /// </summary>
        static readonly string[] BodyNames = [
            "box01",
            "box02",
            "box03",
            "box04",
            "box05",
            "box06",
            "box07",
            "box08"
        ];

        /// <summary>
        /// Writes the comparison summary file.
        /// </summary>
        /// <param name="outputDirectoryPath">Directory that receives the summary.</param>
        /// <param name="bepuSamples">BEPU trace samples.</param>
        /// <param name="helengineSamples">Helengine trace samples.</param>
        public void WriteReport(string outputDirectoryPath, IReadOnlyList<PhysicsTraceSample> bepuSamples, IReadOnlyList<PhysicsTraceSample> helengineSamples) {
            string reportPath = Path.Combine(outputDirectoryPath, "comparison-summary.txt");
            using StreamWriter writer = new StreamWriter(reportPath);
            for (int index = 0; index < BodyNames.Length; index++) {
                WriteFinalBodyComparison(writer, BodyNames[index], bepuSamples, helengineSamples);
            }

            WriteFinalOverlapSummary(writer, "bepu", bepuSamples);
            WriteFinalOverlapSummary(writer, "helengine", helengineSamples);
            WriteFirstDivergence(writer, "box08", bepuSamples, helengineSamples);
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
            writer.WriteLine("  bepu.position=" + Format(bepuFinal.Position) + " velocity=" + Format(bepuFinal.LinearVelocity) + " angular=" + Format(bepuFinal.AngularVelocity));
            writer.WriteLine("  helengine.position=" + Format(helengineFinal.Position) + " velocity=" + Format(helengineFinal.LinearVelocity) + " angular=" + Format(helengineFinal.AngularVelocity));
            writer.WriteLine("  delta.position=" + Format(helengineFinal.Position - bepuFinal.Position) + " delta.velocity=" + Format(helengineFinal.LinearVelocity - bepuFinal.LinearVelocity) + " delta.angular=" + Format(helengineFinal.AngularVelocity - bepuFinal.AngularVelocity));
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
                float angularVelocityDelta = Vector3.Distance(bepuSample.AngularVelocity, helengineSample.AngularVelocity);
                if (positionDelta > 0.25f || angularVelocityDelta > 0.5f) {
                    writer.WriteLine("First material " + bodyName + " divergence");
                    writer.WriteLine("  step=" + bepuSample.StepIndex.ToString(CultureInfo.InvariantCulture) + " time=" + bepuSample.TimeSeconds.ToString("R", CultureInfo.InvariantCulture));
                    writer.WriteLine("  positionDelta=" + positionDelta.ToString("R", CultureInfo.InvariantCulture) + " angularVelocityDelta=" + angularVelocityDelta.ToString("R", CultureInfo.InvariantCulture));
                    writer.WriteLine("  bepu.position=" + Format(bepuSample.Position) + " helengine.position=" + Format(helengineSample.Position));
                    writer.WriteLine("  bepu.forceApprox=" + Format(bepuSample.LinearForceApproximation) + " helengine.forceApprox=" + Format(helengineSample.LinearForceApproximation));
                    return;
                }
            }

            writer.WriteLine("No material divergence found for " + bodyName + ".");
        }

        /// <summary>
        /// Writes whether the final tower contains any overlapping unit-box pairs for one engine trace.
        /// </summary>
        /// <param name="writer">Report writer.</param>
        /// <param name="engineName">Engine name being reported.</param>
        /// <param name="samples">Trace samples to inspect.</param>
        static void WriteFinalOverlapSummary(StreamWriter writer, string engineName, IReadOnlyList<PhysicsTraceSample> samples) {
            writer.WriteLine("Final overlap summary " + engineName);
            bool foundOverlap = false;
            for (int firstIndex = 0; firstIndex < BodyNames.Length; firstIndex++) {
                PhysicsTraceSample first = FindFinalSample(samples, BodyNames[firstIndex]);
                for (int secondIndex = firstIndex + 1; secondIndex < BodyNames.Length; secondIndex++) {
                    PhysicsTraceSample second = FindFinalSample(samples, BodyNames[secondIndex]);
                    if (AreUnitBoxesOverlapping(first.Position, second.Position)) {
                        foundOverlap = true;
                        writer.WriteLine("  overlap " + first.BodyName + " " + second.BodyName + " first=" + Format(first.Position) + " second=" + Format(second.Position));
                    }
                }
            }

            if (!foundOverlap) {
                writer.WriteLine("  no unit-box overlaps");
            }

            writer.WriteLine();
        }

        /// <summary>
        /// Returns whether two unit axis-aligned boxes overlap on all three axes.
        /// </summary>
        /// <param name="firstPosition">First unit-box center.</param>
        /// <param name="secondPosition">Second unit-box center.</param>
        /// <returns>True when the two unit boxes overlap.</returns>
        static bool AreUnitBoxesOverlapping(Vector3 firstPosition, Vector3 secondPosition) {
            return Math.Abs(firstPosition.X - secondPosition.X) < 0.999f &&
                Math.Abs(firstPosition.Y - secondPosition.Y) < 0.999f &&
                Math.Abs(firstPosition.Z - secondPosition.Z) < 0.999f;
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
