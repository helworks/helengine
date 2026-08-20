namespace helengine.editor.tests {
    /// <summary>
    /// Verifies UVW map projections rewrite model texture coordinates deterministically.
    /// </summary>
    public sealed class ModelUvwMapProcessorTests {
        /// <summary>
        /// Ensures world-plane projection maps world positions onto the requested axes divided by the scale.
        /// </summary>
        [Fact]
        public void ApplyWorldMap_WithXzPlane_ProjectsWorldPositionsOverScale() {
            ModelAsset model = ModelUtils.GenerateCubeMesh(float3.Zero, float3.One);

            ModelUvwMapProcessor.ApplyWorldMap(model, ModelUvwMapProcessor.PlaneXZ, 2d, new float3(10f, 0f, 4f), float4.Identity, new float3(2f, 2f, 2f));

            for (int index = 0; index < model.Positions.Length; index++) {
                float3 world = new float3(10f, 0f, 4f) + model.Positions[index] * 2f;
                Assert.Equal(world.X / 2f, model.TexCoords[index].X, 3);
                Assert.Equal(world.Z / 2f, model.TexCoords[index].Y, 3);
            }
        }

        /// <summary>
        /// Ensures world-plane projection honors the entity orientation when composing world positions.
        /// </summary>
        [Fact]
        public void ApplyWorldMap_WithRotation_UsesRotatedWorldPositions() {
            ModelAsset model = ModelUtils.GenerateCubeMesh(float3.Zero, float3.One);
            float3 yAxis = new float3(0f, 1f, 0f);
            float4.CreateFromAxisAngle(ref yAxis, (float)(Math.PI / 2d), out float4 quarterTurn);

            ModelUvwMapProcessor.ApplyWorldMap(model, ModelUvwMapProcessor.PlaneXZ, 1d, float3.Zero, quarterTurn, float3.One);

            for (int index = 0; index < model.Positions.Length; index++) {
                float3 world = float4.RotateVector(model.Positions[index], quarterTurn);
                Assert.Equal(world.X, model.TexCoords[index].X, 3);
                Assert.Equal(world.Z, model.TexCoords[index].Y, 3);
            }
        }

        /// <summary>
        /// Ensures box projection assigns per-face texture coordinates and splits shared vertices between differently facing triangles.
        /// </summary>
        [Fact]
        public void ApplyBoxMap_WithCube_SplitsVerticesAndProjectsPerDominantAxis() {
            ModelAsset model = ModelUtils.GenerateCubeMesh(float3.Zero, float3.One);
            int originalVertexCount = model.Positions.Length;
            int originalIndexCount = model.Indices16.Length;

            ModelUvwMapProcessor.ApplyBoxMap(model, 1d);

            Assert.Equal(originalIndexCount, model.Indices16.Length);
            Assert.True(model.Positions.Length >= originalVertexCount);
            Assert.Equal(model.Positions.Length, model.TexCoords.Length);
            Assert.Equal(model.Positions.Length, model.Normals.Length);

            for (int index = 0; index + 2 < model.Indices16.Length; index += 3) {
                float3 a = model.Positions[model.Indices16[index]];
                float3 b = model.Positions[model.Indices16[index + 1]];
                float3 c = model.Positions[model.Indices16[index + 2]];
                float3 normal = float3.Cross(b - a, c - a);
                float2 uvA = model.TexCoords[model.Indices16[index]];
                float2 uvB = model.TexCoords[model.Indices16[index + 1]];
                float2 uvC = model.TexCoords[model.Indices16[index + 2]];
                float2 span = new float2(
                    Math.Max(Math.Max(uvA.X, uvB.X), uvC.X) - Math.Min(Math.Min(uvA.X, uvB.X), uvC.X),
                    Math.Max(Math.Max(uvA.Y, uvB.Y), uvC.Y) - Math.Min(Math.Min(uvA.Y, uvB.Y), uvC.Y));
                Assert.True(normal.LengthSquared() > 0.000001f);
                Assert.True(span.X > 0.0001f || span.Y > 0.0001f, $"Triangle at index {index} projected to a degenerate UV span.");
            }
        }

        /// <summary>
        /// Ensures invalid scales and planes are rejected before any mutation.
        /// </summary>
        [Fact]
        public void Apply_WithInvalidArguments_Throws() {
            ModelAsset model = ModelUtils.GenerateCubeMesh(float3.Zero, float3.One);

            Assert.Throws<ArgumentOutOfRangeException>(() => ModelUvwMapProcessor.ApplyBoxMap(model, 0d));
            Assert.Throws<ArgumentException>(() => ModelUvwMapProcessor.ApplyWorldMap(model, "XW", 1d, float3.Zero, float4.Identity, float3.One));
        }
    }
}
