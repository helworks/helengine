using System.Runtime.InteropServices;

namespace helengine {
    /// <summary>Fixed IEEE32 GPU packet components, independent of engine numeric storage and arithmetic precision.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct GpuFloat4x4 {
        /// <summary>Fixed single-precision M11 GPU component.</summary>
        public float M11;
        /// <summary>Fixed single-precision M12 GPU component.</summary>
        public float M12;
        /// <summary>Fixed single-precision M13 GPU component.</summary>
        public float M13;
        /// <summary>Fixed single-precision M14 GPU component.</summary>
        public float M14;
        /// <summary>Fixed single-precision M21 GPU component.</summary>
        public float M21;
        /// <summary>Fixed single-precision M22 GPU component.</summary>
        public float M22;
        /// <summary>Fixed single-precision M23 GPU component.</summary>
        public float M23;
        /// <summary>Fixed single-precision M24 GPU component.</summary>
        public float M24;
        /// <summary>Fixed single-precision M31 GPU component.</summary>
        public float M31;
        /// <summary>Fixed single-precision M32 GPU component.</summary>
        public float M32;
        /// <summary>Fixed single-precision M33 GPU component.</summary>
        public float M33;
        /// <summary>Fixed single-precision M34 GPU component.</summary>
        public float M34;
        /// <summary>Fixed single-precision M41 GPU component.</summary>
        public float M41;
        /// <summary>Fixed single-precision M42 GPU component.</summary>
        public float M42;
        /// <summary>Fixed single-precision M43 GPU component.</summary>
        public float M43;
        /// <summary>Fixed single-precision M44 GPU component.</summary>
        public float M44;

        /// <summary>Converts engine components explicitly at the fixed GPU packet boundary.</summary>
        /// <param name="value">Engine aggregate whose configured storage is preserved until this conversion.</param>
        /// <returns>The shader or vertex input's IEEE32 packet value.</returns>
        public static GpuFloat4x4 FromEngine(float4x4 value) {
            return new GpuFloat4x4 {
                M11 = value.M11.ToSingle(),
                M12 = value.M12.ToSingle(),
                M13 = value.M13.ToSingle(),
                M14 = value.M14.ToSingle(),
                M21 = value.M21.ToSingle(),
                M22 = value.M22.ToSingle(),
                M23 = value.M23.ToSingle(),
                M24 = value.M24.ToSingle(),
                M31 = value.M31.ToSingle(),
                M32 = value.M32.ToSingle(),
                M33 = value.M33.ToSingle(),
                M34 = value.M34.ToSingle(),
                M41 = value.M41.ToSingle(),
                M42 = value.M42.ToSingle(),
                M43 = value.M43.ToSingle(),
                M44 = value.M44.ToSingle()
            };
        }

        /// <summary>Explicitly converts an engine aggregate to the fixed GPU format.</summary>
        /// <param name="value">Engine value crossing the GPU boundary.</param>
        public static explicit operator GpuFloat4x4(float4x4 value) { return FromEngine(value); }

        /// <summary>Expands a GPU packet explicitly when CPU geometry code needs its components.</summary>
        /// <param name="value">Fixed GPU aggregate.</param>
        public static explicit operator float4x4(GpuFloat4x4 value) { return new float4x4(value.M11, value.M12, value.M13, value.M14, value.M21, value.M22, value.M23, value.M24, value.M31, value.M32, value.M33, value.M34, value.M41, value.M42, value.M43, value.M44); }
    }
}
