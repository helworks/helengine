using System.Globalization;

namespace Helengine.PhysicsComparison {
    /// <summary>
    /// Writes reconstructed helengine box-box contact state for comparison against BEPU contact manifolds.
    /// </summary>
    public sealed class HelengineContactTraceWriter : IDisposable {
        /// <summary>
        /// CSV stream receiving reconstructed contact rows.
        /// </summary>
        readonly StreamWriter Writer;

        /// <summary>
        /// Initializes a helengine contact trace writer.
        /// </summary>
        /// <param name="filePath">CSV path to write.</param>
        public HelengineContactTraceWriter(string filePath) {
            Writer = new StreamWriter(filePath);
            Writer.WriteLine("step,pair,hasManifold,contactCount,penetration,normalX,normalY,normalZ,hasOrientedManifold,orientedContactCount,orientedPenetration,orientedNormalX,orientedNormalY,orientedNormalZ,axisContact,axisPenetration,axisIndex,firstUpY,secondUpY,firstPositionX,firstPositionY,secondPositionX,secondPositionY");
        }

        /// <summary>
        /// Writes the reconstructed contact state for one body pair.
        /// </summary>
        /// <param name="stepIndex">Simulation step index.</param>
        /// <param name="pairName">Human-readable pair label.</param>
        /// <param name="first">First body state.</param>
        /// <param name="second">Second body state.</param>
        /// <param name="speculativeContactMargin">Speculative contact margin used to reconstruct contacts.</param>
        public void Write(int stepIndex, string pairName, helengine.BodyState3D first, helengine.BodyState3D second, float speculativeContactMargin) {
            bool hasManifold = helengine.BoxBoxContactResolver3D.TryResolveManifold(first, second, speculativeContactMargin, out helengine.BoxBoxContactManifold3D manifold);
            bool hasOrientedManifold = helengine.BoxBoxContactResolver3D.TryResolveOrientedManifold(first, second, speculativeContactMargin, out helengine.BoxBoxContactManifold3D orientedManifold);
            bool hasAxisContact = helengine.BoxBoxContactResolver3D.TryResolveContact(first, second, speculativeContactMargin, out float axisPenetration, out int axisIndex);
            Writer.Write(stepIndex.ToString(CultureInfo.InvariantCulture));
            Writer.Write(",");
            Writer.Write(pairName);
            Writer.Write(",");
            Writer.Write(hasManifold ? "true" : "false");
            Writer.Write(",");
            Writer.Write(hasManifold ? manifold.ContactCount.ToString(CultureInfo.InvariantCulture) : "0");
            Writer.Write(",");
            Writer.Write(Format(hasManifold ? manifold.Penetration : 0f));
            Writer.Write(",");
            Writer.Write(Format(hasManifold ? manifold.Normal.X : 0f));
            Writer.Write(",");
            Writer.Write(Format(hasManifold ? manifold.Normal.Y : 0f));
            Writer.Write(",");
            Writer.Write(Format(hasManifold ? manifold.Normal.Z : 0f));
            Writer.Write(",");
            Writer.Write(hasOrientedManifold ? "true" : "false");
            Writer.Write(",");
            Writer.Write(hasOrientedManifold ? orientedManifold.ContactCount.ToString(CultureInfo.InvariantCulture) : "0");
            Writer.Write(",");
            Writer.Write(Format(hasOrientedManifold ? orientedManifold.Penetration : 0f));
            Writer.Write(",");
            Writer.Write(Format(hasOrientedManifold ? orientedManifold.Normal.X : 0f));
            Writer.Write(",");
            Writer.Write(Format(hasOrientedManifold ? orientedManifold.Normal.Y : 0f));
            Writer.Write(",");
            Writer.Write(Format(hasOrientedManifold ? orientedManifold.Normal.Z : 0f));
            Writer.Write(",");
            Writer.Write(hasAxisContact ? "true" : "false");
            Writer.Write(",");
            Writer.Write(Format(hasAxisContact ? axisPenetration : 0f));
            Writer.Write(",");
            Writer.Write(axisIndex.ToString(CultureInfo.InvariantCulture));
            Writer.Write(",");
            Writer.Write(Format(ResolveUpY(first)));
            Writer.Write(",");
            Writer.Write(Format(ResolveUpY(second)));
            Writer.Write(",");
            Writer.Write(Format(first.Position.X));
            Writer.Write(",");
            Writer.Write(Format(first.Position.Y));
            Writer.Write(",");
            Writer.Write(Format(second.Position.X));
            Writer.Write(",");
            Writer.Write(Format(second.Position.Y));
            Writer.WriteLine();
        }

        /// <summary>
        /// Releases the underlying file handle.
        /// </summary>
        public void Dispose() {
            Writer.Dispose();
        }

        /// <summary>
        /// Resolves the world-space Y component of one body's local up axis.
        /// </summary>
        /// <param name="bodyState">Body state to inspect.</param>
        /// <returns>World Y component of local up.</returns>
        static float ResolveUpY(helengine.BodyState3D bodyState) {
            helengine.float3 up = helengine.float4.RotateVector(new helengine.float3(0f, 1f, 0f), bodyState.Orientation);
            return up.Y;
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
