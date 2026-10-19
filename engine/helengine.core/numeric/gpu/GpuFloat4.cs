using System.Runtime.InteropServices;

namespace helengine {
    /// <summary>Fixed IEEE32 GPU packet components, independent of engine numeric storage and arithmetic precision.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct GpuFloat4 {
        /// <summary>Fixed single-precision X GPU component.</summary>
        public float X;
        /// <summary>Fixed single-precision Y GPU component.</summary>
        public float Y;
        /// <summary>Fixed single-precision Z GPU component.</summary>
        public float Z;
        /// <summary>Fixed single-precision W GPU component.</summary>
        public float W;

        /// <summary>Converts engine components explicitly at the fixed GPU packet boundary.</summary>
        /// <param name="value">Engine aggregate whose configured storage is preserved until this conversion.</param>
        /// <returns>The shader or vertex input's IEEE32 packet value.</returns>
        public static GpuFloat4 FromEngine(float4 value) {
            return new GpuFloat4 {
                X = value.X.ToSingle(),
                Y = value.Y.ToSingle(),
                Z = value.Z.ToSingle(),
                W = value.W.ToSingle()
            };
        }

        /// <summary>Explicitly converts an engine aggregate to the fixed GPU format.</summary>
        /// <param name="value">Engine value crossing the GPU boundary.</param>
        public static explicit operator GpuFloat4(float4 value) { return FromEngine(value); }

        /// <summary>Expands a GPU packet explicitly when CPU geometry code needs its components.</summary>
        /// <param name="value">Fixed GPU aggregate.</param>
        public static explicit operator float4(GpuFloat4 value) { return new float4(value.X, value.Y, value.Z, value.W); }
    }
}
