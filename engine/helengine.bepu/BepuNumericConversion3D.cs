using System.Numerics;

namespace helengine {
    /// <summary>
    /// Converts between Helengine math values and the official BEPU numeric types.
    /// </summary>
    public static class BepuNumericConversion3D {
        /// <summary>
        /// Converts one Helengine vector into a <see cref="Vector3"/>.
        /// </summary>
        /// <param name="value">Helengine vector to convert.</param>
        /// <returns>Equivalent system vector.</returns>
        public static Vector3 ToSystemVector(float3 value) {
            return new Vector3(value.X, value.Y, value.Z);
        }

        /// <summary>
        /// Converts one system vector into a <see cref="float3"/>.
        /// </summary>
        /// <param name="value">System vector to convert.</param>
        /// <returns>Equivalent Helengine vector.</returns>
        public static float3 ToHelengineFloat3(Vector3 value) {
            return new float3(value.X, value.Y, value.Z);
        }

        /// <summary>
        /// Converts one Helengine quaternion into a <see cref="Quaternion"/>.
        /// </summary>
        /// <param name="value">Helengine quaternion to convert.</param>
        /// <returns>Equivalent system quaternion.</returns>
        public static Quaternion ToSystemQuaternion(float4 value) {
            return new Quaternion(value.X, value.Y, value.Z, value.W);
        }

        /// <summary>
        /// Converts one system quaternion into a <see cref="float4"/>.
        /// </summary>
        /// <param name="value">System quaternion to convert.</param>
        /// <returns>Equivalent Helengine quaternion.</returns>
        public static float4 ToHelengineFloat4(Quaternion value) {
            return new float4(value.X, value.Y, value.Z, value.W);
        }
    }
}
