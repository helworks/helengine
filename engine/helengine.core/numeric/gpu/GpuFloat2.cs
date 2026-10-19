using System.Runtime.InteropServices;

namespace helengine {
    /// <summary>Fixed IEEE32 GPU packet components, independent of engine numeric storage and arithmetic precision.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct GpuFloat2 {
        /// <summary>Fixed single-precision X GPU component.</summary>
        public float X;
        /// <summary>Fixed single-precision Y GPU component.</summary>
        public float Y;

        /// <summary>Converts engine components explicitly at the fixed GPU packet boundary.</summary>
        /// <param name="value">Engine aggregate whose configured storage is preserved until this conversion.</param>
        /// <returns>The shader or vertex input's IEEE32 packet value.</returns>
        public static GpuFloat2 FromEngine(float2 value) {
            return new GpuFloat2 {
                X = value.X.ToSingle(),
                Y = value.Y.ToSingle()
            };
        }

        /// <summary>Explicitly converts an engine aggregate to the fixed GPU format.</summary>
        /// <param name="value">Engine value crossing the GPU boundary.</param>
        public static explicit operator GpuFloat2(float2 value) { return FromEngine(value); }

        /// <summary>Expands a GPU packet explicitly when CPU geometry code needs its components.</summary>
        /// <param name="value">Fixed GPU aggregate.</param>
        public static explicit operator float2(GpuFloat2 value) { return new float2(value.X, value.Y); }
    }
}
