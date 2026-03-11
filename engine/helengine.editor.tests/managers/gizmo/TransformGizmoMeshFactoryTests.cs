using helengine;
using helengine.editor;
using Xunit;

namespace helengine.editor.tests.managers.gizmo {
    /// <summary>
    /// Verifies primitive mesh generation used by editor transform gizmos.
    /// </summary>
    public class TransformGizmoMeshFactoryTests {
        /// <summary>
        /// Tolerance used when comparing generated tube-ring bounds.
        /// </summary>
        const float FloatTolerance = 0.001f;

        /// <summary>
        /// Ensures the tube-ring generator produces the expected vertex and index counts for the selected segment layout.
        /// </summary>
        [Fact]
        public void CreateTubeRing_GeneratesExpectedVertexAndIndexCounts() {
            ModelAsset model = TransformGizmoMeshFactory.CreateTubeRing(0.9f, 1.0f, 0.08f, 16);

            Assert.Equal(16 * 8, model.Positions.Length);
            Assert.Equal(16 * 8, model.Normals.Length);
            Assert.Equal(16 * 8, model.TexCoords.Length);
            Assert.Equal(16 * 48, model.Indices16.Length);
        }

        /// <summary>
        /// Ensures the tube-ring generator produces the expected inner and outer radii around the ring.
        /// </summary>
        [Fact]
        public void CreateTubeRing_UsesRequestedInnerAndOuterRadii() {
            ModelAsset model = TransformGizmoMeshFactory.CreateTubeRing(0.9f, 1.0f, 0.08f, 32);

            double minRadius = double.MaxValue;
            double maxRadius = double.MinValue;
            for (int positionIndex = 0; positionIndex < model.Positions.Length; positionIndex++) {
                float3 position = model.Positions[positionIndex];
                double radius = Math.Sqrt((position.X * position.X) + (position.Z * position.Z));
                minRadius = Math.Min(minRadius, radius);
                maxRadius = Math.Max(maxRadius, radius);
            }

            Assert.InRange(Math.Abs(minRadius - 0.9f), 0f, FloatTolerance);
            Assert.InRange(Math.Abs(maxRadius - 1.0f), 0f, FloatTolerance);
        }

        /// <summary>
        /// Ensures the tube-ring generator places vertices at the requested top and bottom heights.
        /// </summary>
        [Fact]
        public void CreateTubeRing_UsesRequestedHeight() {
            ModelAsset model = TransformGizmoMeshFactory.CreateTubeRing(0.9f, 1.0f, 0.08f, 24);

            float minY = float.MaxValue;
            float maxY = float.MinValue;
            for (int positionIndex = 0; positionIndex < model.Positions.Length; positionIndex++) {
                float y = model.Positions[positionIndex].Y;
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }

            Assert.InRange(Math.Abs(minY + 0.04f), 0f, FloatTolerance);
            Assert.InRange(Math.Abs(maxY - 0.04f), 0f, FloatTolerance);
        }

        /// <summary>
        /// Ensures the tube-ring generator rejects an outer radius that does not exceed the inner radius.
        /// </summary>
        [Fact]
        public void CreateTubeRing_ThrowsWhenOuterRadiusDoesNotExceedInnerRadius() {
            Assert.Throws<ArgumentOutOfRangeException>(() => TransformGizmoMeshFactory.CreateTubeRing(1.0f, 1.0f, 0.08f, 24));
        }

        /// <summary>
        /// Ensures the box generator produces the expected vertex and index counts.
        /// </summary>
        [Fact]
        public void CreateBox_GeneratesExpectedVertexAndIndexCounts() {
            ModelAsset model = TransformGizmoMeshFactory.CreateBox(0.2f, 0.3f, 0.4f);

            Assert.Equal(24, model.Positions.Length);
            Assert.Equal(24, model.Normals.Length);
            Assert.Equal(24, model.TexCoords.Length);
            Assert.Equal(36, model.Indices16.Length);
        }

        /// <summary>
        /// Ensures the box generator places the box on the positive Y side of its local origin.
        /// </summary>
        [Fact]
        public void CreateBox_UsesRequestedDimensionsWithBaseAtZero() {
            ModelAsset model = TransformGizmoMeshFactory.CreateBox(0.2f, 0.3f, 0.4f);

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            float minZ = float.MaxValue;
            float maxZ = float.MinValue;
            for (int positionIndex = 0; positionIndex < model.Positions.Length; positionIndex++) {
                float3 position = model.Positions[positionIndex];
                minX = Math.Min(minX, position.X);
                maxX = Math.Max(maxX, position.X);
                minY = Math.Min(minY, position.Y);
                maxY = Math.Max(maxY, position.Y);
                minZ = Math.Min(minZ, position.Z);
                maxZ = Math.Max(maxZ, position.Z);
            }

            Assert.InRange(Math.Abs(minX + 0.1f), 0f, FloatTolerance);
            Assert.InRange(Math.Abs(maxX - 0.1f), 0f, FloatTolerance);
            Assert.InRange(Math.Abs(minY - 0.0f), 0f, FloatTolerance);
            Assert.InRange(Math.Abs(maxY - 0.3f), 0f, FloatTolerance);
            Assert.InRange(Math.Abs(minZ + 0.2f), 0f, FloatTolerance);
            Assert.InRange(Math.Abs(maxZ - 0.2f), 0f, FloatTolerance);
        }
    }
}
