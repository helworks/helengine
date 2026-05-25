using System.Globalization;
using System.Numerics;

namespace Helengine.PhysicsComparison {
    /// <summary>
    /// Writes BEPU contact manifold samples in CSV form.
    /// </summary>
    public sealed class ContactTraceWriter : IDisposable {
        /// <summary>
        /// CSV stream receiving contact rows.
        /// </summary>
        readonly StreamWriter Writer;

        /// <summary>
        /// Initializes a contact trace writer.
        /// </summary>
        /// <param name="filePath">CSV path to write.</param>
        public ContactTraceWriter(string filePath) {
            Writer = new StreamWriter(filePath);
            Writer.WriteLine("step,pairA,pairB,contactIndex,featureId,depth,offsetX,offsetY,offsetZ,normalX,normalY,normalZ");
        }

        /// <summary>
        /// Writes one contact row.
        /// </summary>
        /// <param name="stepIndex">Current simulation step.</param>
        /// <param name="pairA">First collidable handle value.</param>
        /// <param name="pairB">Second collidable handle value.</param>
        /// <param name="contactIndex">Contact index inside the manifold.</param>
        /// <param name="featureId">Persistent contact feature id.</param>
        /// <param name="depth">Penetration depth.</param>
        /// <param name="offset">Contact offset from collidable A.</param>
        /// <param name="normal">Contact normal from B toward A.</param>
        public void Write(int stepIndex, int pairA, int pairB, int contactIndex, int featureId, float depth, Vector3 offset, Vector3 normal) {
            Writer.Write(stepIndex.ToString(CultureInfo.InvariantCulture));
            Writer.Write(",");
            Writer.Write(pairA.ToString(CultureInfo.InvariantCulture));
            Writer.Write(",");
            Writer.Write(pairB.ToString(CultureInfo.InvariantCulture));
            Writer.Write(",");
            Writer.Write(contactIndex.ToString(CultureInfo.InvariantCulture));
            Writer.Write(",");
            Writer.Write(featureId.ToString(CultureInfo.InvariantCulture));
            Writer.Write(",");
            Writer.Write(Format(depth));
            Writer.Write(",");
            Writer.Write(Format(offset.X));
            Writer.Write(",");
            Writer.Write(Format(offset.Y));
            Writer.Write(",");
            Writer.Write(Format(offset.Z));
            Writer.Write(",");
            Writer.Write(Format(normal.X));
            Writer.Write(",");
            Writer.Write(Format(normal.Y));
            Writer.Write(",");
            Writer.Write(Format(normal.Z));
            Writer.WriteLine();
        }

        /// <summary>
        /// Releases the underlying file handle.
        /// </summary>
        public void Dispose() {
            Writer.Dispose();
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
