using System.Globalization;
using System.Numerics;

namespace Helengine.PhysicsComparison {
    /// <summary>
    /// Writes body-state physics traces in a stable CSV format.
    /// </summary>
    public sealed class PhysicsTraceWriter : IDisposable {
        /// <summary>
        /// CSV stream receiving trace rows.
        /// </summary>
        readonly StreamWriter Writer;

        /// <summary>
        /// Initializes a trace writer for one output file.
        /// </summary>
        /// <param name="filePath">CSV file path to write.</param>
        public PhysicsTraceWriter(string filePath) {
            Writer = new StreamWriter(filePath);
            Writer.WriteLine("engine,step,time,body,positionX,positionY,positionZ,orientationX,orientationY,orientationZ,orientationW,linearVelocityX,linearVelocityY,linearVelocityZ,angularVelocityX,angularVelocityY,angularVelocityZ,linearForceApproxX,linearForceApproxY,linearForceApproxZ,angularForceApproxX,angularForceApproxY,angularForceApproxZ");
        }

        /// <summary>
        /// Writes one body-state sample.
        /// </summary>
        /// <param name="sample">Sample to write.</param>
        public void Write(PhysicsTraceSample sample) {
            Writer.Write(sample.EngineName);
            Writer.Write(",");
            Writer.Write(sample.StepIndex.ToString(CultureInfo.InvariantCulture));
            Writer.Write(",");
            Writer.Write(Format(sample.TimeSeconds));
            Writer.Write(",");
            Writer.Write(sample.BodyName);
            WriteVector(sample.Position);
            WriteQuaternion(sample.Orientation);
            WriteVector(sample.LinearVelocity);
            WriteVector(sample.AngularVelocity);
            WriteVector(sample.LinearForceApproximation);
            WriteVector(sample.AngularForceApproximation);
            Writer.WriteLine();
        }

        /// <summary>
        /// Releases the underlying file handle.
        /// </summary>
        public void Dispose() {
            Writer.Dispose();
        }

        /// <summary>
        /// Writes one vector as three CSV fields.
        /// </summary>
        /// <param name="value">Vector to write.</param>
        void WriteVector(Vector3 value) {
            Writer.Write(",");
            Writer.Write(Format(value.X));
            Writer.Write(",");
            Writer.Write(Format(value.Y));
            Writer.Write(",");
            Writer.Write(Format(value.Z));
        }

        /// <summary>
        /// Writes one quaternion as four CSV fields.
        /// </summary>
        /// <param name="value">Quaternion to write.</param>
        void WriteQuaternion(Quaternion value) {
            Writer.Write(",");
            Writer.Write(Format(value.X));
            Writer.Write(",");
            Writer.Write(Format(value.Y));
            Writer.Write(",");
            Writer.Write(Format(value.Z));
            Writer.Write(",");
            Writer.Write(Format(value.W));
        }

        /// <summary>
        /// Formats one floating-point value without culture-specific separators.
        /// </summary>
        /// <param name="value">Value to format.</param>
        /// <returns>Formatted value.</returns>
        static string Format(float value) {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
