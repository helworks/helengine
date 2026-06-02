using System.Globalization;
using System.Numerics;

namespace Helengine.PhysicsComparison {
    /// <summary>
    /// Compares BEPU and helengine frame-replay traces for the authored four-box dynamic stack scene.
    /// </summary>
    public sealed class FrameReplayComparisonReporter {
        /// <summary>
        /// Stable body names emitted by the dynamic-stack-boxes replay runners.
        /// </summary>
        static readonly string[] BodyNames = [
            "box01",
            "box02",
            "box03",
            "box04"
        ];

        /// <summary>
        /// Writes the frame-replay comparison summary file.
        /// </summary>
        /// <param name="outputDirectoryPath">Directory receiving the summary.</param>
        /// <param name="bepuSamples">BEPU frame samples.</param>
        /// <param name="helengineSamples">Helengine frame samples.</param>
        /// <param name="reportFileName">Summary file name to write.</param>
        public void WriteReport(string outputDirectoryPath, IReadOnlyList<PhysicsTraceSample> bepuSamples, IReadOnlyList<PhysicsTraceSample> helengineSamples, string reportFileName) {
            if (string.IsNullOrWhiteSpace(reportFileName)) {
                throw new ArgumentException("Report file name must be provided.", nameof(reportFileName));
            }

            string reportPath = Path.Combine(outputDirectoryPath, reportFileName);
            using StreamWriter writer = new StreamWriter(reportPath);
            for (int index = 0; index < BodyNames.Length; index++) {
                WriteFinalBodyComparison(writer, BodyNames[index], bepuSamples, helengineSamples);
            }

            WriteFirstMaterialDivergence(writer, bepuSamples, helengineSamples);
        }

        /// <summary>
        /// Writes final state comparison for one body.
        /// </summary>
        /// <param name="writer">Report writer.</param>
        /// <param name="bodyName">Body label to compare.</param>
        /// <param name="bepuSamples">BEPU frame samples.</param>
        /// <param name="helengineSamples">Helengine frame samples.</param>
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
        /// Writes the earliest host frame where any body diverges materially.
        /// </summary>
        /// <param name="writer">Report writer.</param>
        /// <param name="bepuSamples">BEPU frame samples.</param>
        /// <param name="helengineSamples">Helengine frame samples.</param>
        static void WriteFirstMaterialDivergence(StreamWriter writer, IReadOnlyList<PhysicsTraceSample> bepuSamples, IReadOnlyList<PhysicsTraceSample> helengineSamples) {
            for (int frameIndex = 0; frameIndex < CountFrames(bepuSamples); frameIndex++) {
                for (int bodyIndex = 0; bodyIndex < BodyNames.Length; bodyIndex++) {
                    string bodyName = BodyNames[bodyIndex];
                    PhysicsTraceSample bepuSample = FindSample(bepuSamples, bodyName, frameIndex);
                    PhysicsTraceSample helengineSample = FindSample(helengineSamples, bodyName, frameIndex);
                    float positionDelta = Vector3.Distance(bepuSample.Position, helengineSample.Position);
                    float velocityDelta = Vector3.Distance(bepuSample.LinearVelocity, helengineSample.LinearVelocity);
                    if (positionDelta > 0.05f || velocityDelta > 0.1f) {
                        writer.WriteLine("First material frame divergence");
                        writer.WriteLine("  frame=" + frameIndex.ToString(CultureInfo.InvariantCulture) + " time=" + bepuSample.TimeSeconds.ToString("R", CultureInfo.InvariantCulture) + " body=" + bodyName);
                        writer.WriteLine("  positionDelta=" + positionDelta.ToString("R", CultureInfo.InvariantCulture) + " velocityDelta=" + velocityDelta.ToString("R", CultureInfo.InvariantCulture));
                        writer.WriteLine("  bepu.position=" + Format(bepuSample.Position) + " helengine.position=" + Format(helengineSample.Position));
                        writer.WriteLine("  bepu.velocity=" + Format(bepuSample.LinearVelocity) + " helengine.velocity=" + Format(helengineSample.LinearVelocity));
                        return;
                    }
                }
            }

            writer.WriteLine("No material frame divergence found.");
        }

        /// <summary>
        /// Finds the number of host frames represented in the trace.
        /// </summary>
        /// <param name="samples">Samples to inspect.</param>
        /// <returns>Host-frame count.</returns>
        static int CountFrames(IReadOnlyList<PhysicsTraceSample> samples) {
            int highestFrameIndex = -1;
            for (int index = 0; index < samples.Count; index++) {
                if (samples[index].StepIndex > highestFrameIndex) {
                    highestFrameIndex = samples[index].StepIndex;
                }
            }

            return highestFrameIndex + 1;
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
        /// Finds a sample for one body and frame.
        /// </summary>
        /// <param name="samples">Samples to inspect.</param>
        /// <param name="bodyName">Body label to find.</param>
        /// <param name="frameIndex">Frame index to find.</param>
        /// <returns>Matching sample.</returns>
        static PhysicsTraceSample FindSample(IReadOnlyList<PhysicsTraceSample> samples, string bodyName, int frameIndex) {
            for (int index = 0; index < samples.Count; index++) {
                if (samples[index].BodyName == bodyName && samples[index].StepIndex == frameIndex) {
                    return samples[index];
                }
            }

            throw new InvalidOperationException("Trace does not contain body '" + bodyName + "' at frame " + frameIndex.ToString(CultureInfo.InvariantCulture) + ".");
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
