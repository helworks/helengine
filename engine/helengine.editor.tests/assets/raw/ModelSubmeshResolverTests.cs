using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies resolved model submesh arrays have an unambiguous caller-owned native lifetime.
    /// </summary>
    public sealed class ModelSubmeshResolverTests {
        /// <summary>
        /// Ensures authored submesh metadata is returned in a distinct array instead of exposing the asset-owned array.
        /// </summary>
        [Fact]
        public void ResolveAssetSubmeshes_WithAuthoredSubmeshes_ReturnsDistinctArray() {
            ModelSubmeshAsset authoredSubmesh = new ModelSubmeshAsset {
                MaterialSlotName = "default",
                IndexStart = 0,
                IndexCount = 3
            };
            ModelAsset asset = new ModelAsset {
                Positions = new[] { float3.Zero, float3.One, new float3(0f, 1f, 0f) },
                Submeshes = new[] { authoredSubmesh }
            };

            ModelSubmeshAsset[] resolved = ModelSubmeshResolver.ResolveAssetSubmeshes(asset);

            Assert.NotSame(asset.Submeshes, resolved);
            Assert.Same(authoredSubmesh, Assert.Single(resolved));
        }

        /// <summary>
        /// Ensures models without drawable elements receive independent empty result arrays.
        /// </summary>
        [Fact]
        public void ResolveAssetSubmeshes_WithoutElements_ReturnsDistinctEmptyArrays() {
            ModelAsset asset = new ModelAsset {
                Positions = new float3[0],
                Submeshes = new ModelSubmeshAsset[0]
            };

            ModelSubmeshAsset[] first = ModelSubmeshResolver.ResolveAssetSubmeshes(asset);
            ModelSubmeshAsset[] second = ModelSubmeshResolver.ResolveAssetSubmeshes(asset);

            Assert.Empty(first);
            Assert.Empty(second);
            Assert.NotSame(first, second);
        }
    }
}
