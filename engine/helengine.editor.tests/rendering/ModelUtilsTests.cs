using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies procedurally generated primitive model assets used by editor and runtime built-in content.
    /// </summary>
    public sealed class ModelUtilsTests {
        /// <summary>
        /// Ensures the generated plane primitive lies on the world XZ plane and faces upward so it can receive overhead lighting.
        /// </summary>
        [Fact]
        public void GeneratePlaneMesh_WhenUsingIdentityArguments_CreatesUpwardFacingPlaneAtOrigin() {
            ModelAsset model = ModelUtils.GeneratePlaneMesh(float3.Zero, float3.One);

            Assert.Equal(4, model.Positions.Length);
            Assert.Equal(4, model.Normals.Length);
            Assert.Equal(6, model.Indices16.Length);
            for (int index = 0; index < model.Positions.Length; index++) {
                Assert.Equal(0f, model.Positions[index].Y);
                Assert.Equal(0f, model.Normals[index].X);
                Assert.Equal(1f, model.Normals[index].Y);
                Assert.Equal(0f, model.Normals[index].Z);
            }

            Assert.Equal((ushort)0, model.Indices16[0]);
            Assert.Equal((ushort)2, model.Indices16[1]);
            Assert.Equal((ushort)1, model.Indices16[2]);
            Assert.Equal((ushort)2, model.Indices16[3]);
            Assert.Equal((ushort)0, model.Indices16[4]);
            Assert.Equal((ushort)3, model.Indices16[5]);
        }

        /// <summary>
        /// Ensures the generated plane primitive winding produces triangle normals that agree with the authored upward-facing vertex normals.
        /// </summary>
        [Fact]
        public void GeneratePlaneMesh_WhenUsingIdentityArguments_WindsTrianglesToMatchUpwardNormals() {
            ModelAsset model = ModelUtils.GeneratePlaneMesh(float3.Zero, float3.One);

            AssertAllTrianglesFollowVertexNormals(model);
        }

        /// <summary>
        /// Ensures every generated cube face uses triangle winding that agrees with the authored per-face vertex normals.
        /// </summary>
        [Fact]
        public void GenerateCubeMesh_WhenUsingIdentityArguments_WindsTrianglesToMatchFaceNormals() {
            ModelAsset model = ModelUtils.GenerateCubeMesh(float3.Zero, float3.One);

            AssertAllTrianglesFollowVertexNormals(model);
        }

        /// <summary>
        /// Ensures the generated cube primitive uses unit-size bounds centered at the origin so authored entity scale values map directly to final cube dimensions.
        /// </summary>
        [Fact]
        public void GenerateCubeMesh_WhenUsingIdentityArguments_CreatesUnitCubeCenteredAtOrigin() {
            ModelAsset model = ModelUtils.GenerateCubeMesh(float3.Zero, float3.One);

            Assert.Equal(24, model.Positions.Length);

            for (int index = 0; index < model.Positions.Length; index++) {
                Assert.InRange(model.Positions[index].X, -0.5f, 0.5f);
                Assert.InRange(model.Positions[index].Y, -0.5f, 0.5f);
                Assert.InRange(model.Positions[index].Z, -0.5f, 0.5f);
            }
        }

        /// <summary>
        /// Verifies that every indexed triangle in one generated model asset has a geometric face normal aligned with its authored vertex normals.
        /// </summary>
        /// <param name="model">Generated model asset to validate.</param>
        void AssertAllTrianglesFollowVertexNormals(ModelAsset model) {
            Assert.NotNull(model);
            Assert.NotNull(model.Positions);
            Assert.NotNull(model.Normals);
            Assert.NotNull(model.Indices16);

            for (int triangleStart = 0; triangleStart < model.Indices16.Length; triangleStart += 3) {
                ushort index0 = model.Indices16[triangleStart];
                ushort index1 = model.Indices16[triangleStart + 1];
                ushort index2 = model.Indices16[triangleStart + 2];

                float3 position0 = model.Positions[index0];
                float3 position1 = model.Positions[index1];
                float3 position2 = model.Positions[index2];
                float3 edge1 = position1 - position0;
                float3 edge2 = position2 - position0;
                float3 geometricNormal = float3.Normalize(float3.Cross(edge1, edge2));
                float3 authoredNormal = float3.Normalize(model.Normals[index0]);
                float alignment = float3.Dot(geometricNormal, authoredNormal);

                Assert.True(
                    alignment > 0.5f,
                    $"Triangle starting at index {triangleStart} winds against its authored normal. Alignment={alignment}.");
            }
        }
    }
}
