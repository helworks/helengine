using System.Runtime.InteropServices;

namespace helengine.core.tests.numeric {
    /// <summary>Verifies that changing engine precision cannot change fixed GPU IEEE32 packet formats.</summary>
    public sealed class FixedGpuNumericLayoutTests {
        /// <summary>GPU aggregate layouts remain exactly their specified single-precision sizes in every engine build.</summary>
        [Fact]
        public void LayoutRemainsIeee32() {
            Assert.Equal(8, Marshal.SizeOf<GpuFloat2>());
            Assert.Equal(12, Marshal.SizeOf<GpuFloat3>());
            Assert.Equal(16, Marshal.SizeOf<GpuFloat4>());
            Assert.Equal(64, Marshal.SizeOf<GpuFloat4x4>());
        }

        /// <summary>The named GPU boundary rounds wide engine components explicitly to the shader's declared IEEE32 format.</summary>
        [Fact]
        public void NamedBoundaryConvertsEngineValues() {
            float4 source = new float4(16777217d, 2d, 3d, 4d);
            GpuFloat4 packet = GpuFloat4.FromEngine(source);
            Assert.Equal(16777216f, packet.X);
            Assert.Equal(2f, packet.Y);
            Assert.Equal(3f, packet.Z);
            Assert.Equal(4f, packet.W);
            Assert.Equal(RealRepresentation.StorageBits == 64 ? 16777217d : 16777216d, source.X.ToDouble());
        }
    }
}
