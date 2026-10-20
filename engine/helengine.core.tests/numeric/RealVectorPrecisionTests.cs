using System.Runtime.InteropServices;

namespace helengine.core.tests.numeric {
    /// <summary>Checks that engine vectors and matrices store selected-width identifiable engine scalars.</summary>
    public sealed class RealVectorPrecisionTests {
        /// <summary>Vector and matrix fields retain the Real identity used by native lowering and serialized fields.</summary>
        [Theory]
        [InlineData(typeof(float2), 2)]
        [InlineData(typeof(float3), 3)]
        [InlineData(typeof(float4), 4)]
        [InlineData(typeof(float4x4), 16)]
        public void StoredFieldsFollowBuildPolicy(Type type, int components) {
            var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            Assert.Equal(components, fields.Length);
            Assert.All(fields, field => Assert.Equal(typeof(Real), field.FieldType));
            Assert.Equal(components * RealRepresentation.StorageBits / 8, Marshal.SizeOf(type));
        }
        /// <summary>Scalar vector results remain evaluation values until an actual stored destination receives them.</summary>
        [Fact]
        public void DotProductKeepsEvaluationPrecision() {
            double large = RealRepresentation.StorageBits == 16 ? 2048d : 16777216d;
            float3 left = new float3(large, 1d, 0d);
            float3 right = new float3(1d, 1d, 0d);
            double difference = (double)(float3.Dot(left, right) - Real.FromDouble(large));
            bool retainsUnit = RealRepresentation.StorageBits == 16
                ? RealRepresentation.ArithmeticBits > 16 : RealRepresentation.ArithmeticBits > 32;
            Assert.Equal(retainsUnit ? 1d : 0d, difference);
        }
    }
}
