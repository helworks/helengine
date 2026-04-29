using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies model index data resolution for 16-bit and 32-bit mesh assets.
    /// </summary>
    public class ModelAssetIndexDataTests {
        /// <summary>
        /// Ensures 32-bit model assets resolve to 32-bit index metadata.
        /// </summary>
        [Fact]
        public void Resolve_WhenAssetUses32BitIndices_Returns32BitMetadata() {
            ModelAsset asset = new ModelAsset {
                Positions = new[] {
                    float3.Zero,
                    float3.One,
                    new float3(2f, 2f, 2f)
                },
                Normals = new[] {
                    new float3(0f, 1f, 0f),
                    new float3(0f, 1f, 0f),
                    new float3(0f, 1f, 0f)
                },
                TexCoords = new[] {
                    new float2(0f, 0f),
                    new float2(1f, 1f),
                    new float2(2f, 2f)
                },
                Indices32 = new uint[] { 0u, 1u, 2u }
            };

            ModelAssetIndexData indexData = ModelAssetIndexData.Resolve(asset);

            Assert.True(indexData.Uses32BitIndices);
            Assert.Equal(3, indexData.IndexCount);
            Assert.Null(indexData.Indices16);
            Assert.Equal(new uint[] { 0u, 1u, 2u }, indexData.Indices32);
        }

        /// <summary>
        /// Ensures ambiguous model assets with both index buffers populated are rejected.
        /// </summary>
        [Fact]
        public void Resolve_WhenAssetDefinesBothIndexBuffers_Throws() {
            ModelAsset asset = new ModelAsset {
                Positions = new[] { float3.Zero },
                Normals = new[] { new float3(0f, 1f, 0f) },
                TexCoords = new[] { new float2(0f, 0f) },
                Indices16 = new ushort[] { 0 },
                Indices32 = new uint[] { 0u }
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                ModelAssetIndexData.Resolve(asset));
            Assert.Contains("both", exception.Message);
        }
    }
}
