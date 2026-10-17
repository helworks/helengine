using helengine;

namespace helengine.core.tests {
    /// <summary>Exercises matrix-only reads so legacy decomposed-cache state cannot hide stale affine transforms.</summary>
    public sealed class EntityWorldMatrixCacheTests {
        /// <summary>Checks that both public and local transform setters refresh a descendant previously read only as a matrix.</summary>
        /// <param name="setter">Transform setter used after warming the matrix cache.</param>
        [Theory]
        [InlineData(0)] [InlineData(1)] [InlineData(2)]
        [InlineData(3)] [InlineData(4)] [InlineData(5)]
        public void MatrixOnlyRead_AncestorTransformChanges_RefreshesDescendant(int setter) {
            using Core core = CreateCore();
            Entity parent = new Entity(core);
            parent.InitChildren();
            Entity child = new Entity(core) { LocalPosition = new float3(3f, 4f, 0f) };
            parent.AddChild(child);
            float4x4 before = child.WorldTransformMatrix;
            float4 rotation = new float4(0f, 0f, (float)Math.Sqrt(.5d), (float)Math.Sqrt(.5d));
            if (setter == 0) {
                parent.Position = new float3(10f, 20f, 0f);
            } else if (setter == 1) {
                parent.LocalPosition = new float3(10f, 20f, 0f);
            } else if (setter == 2) {
                parent.Scale = new float3(2f, 3f, 1f);
            } else if (setter == 3) {
                parent.LocalScale = new float3(2f, 3f, 1f);
            } else if (setter == 4) {
                parent.Orientation = rotation;
            } else {
                parent.LocalOrientation = rotation;
            }
            float4x4 after = child.WorldTransformMatrix;
            if (setter < 2) {
                Assert.Equal(13f, after.M41, 4);
                Assert.Equal(24f, after.M42, 4);
            } else if (setter < 4) {
                Assert.Equal(6f, after.M41, 4);
                Assert.Equal(12f, after.M42, 4);
            } else {
                Assert.Equal(-4f, after.M41, 4);
                Assert.Equal(3f, after.M42, 4);
            }
            Assert.NotEqual(before.M41, after.M41);
        }

        /// <summary>Checks subtree invalidation when a matrix-warmed child is detached and reattached under another parent.</summary>
        [Fact]
        public void MatrixOnlyRead_Reparenting_RefreshesEntireSubtree() {
            using Core core = CreateCore();
            Entity first = new Entity(core) { LocalPosition = new float3(10f, 0f, 0f) };
            Entity second = new Entity(core) { LocalPosition = new float3(100f, 0f, 0f) };
            Entity child = new Entity(core) { LocalPosition = new float3(5f, 0f, 0f) };
            Entity leaf = new Entity(core) { LocalPosition = new float3(2f, 0f, 0f) };
            first.InitChildren(); second.InitChildren(); child.InitChildren();
            first.AddChild(child); child.AddChild(leaf);
            Assert.Equal(17f, leaf.WorldTransformMatrix.M41);
            first.RemoveChild(child);
            Assert.Equal(7f, leaf.WorldTransformMatrix.M41);
            second.AddChild(child);
            Assert.Equal(107f, leaf.WorldTransformMatrix.M41);
            second.LocalPosition = new float3(200f, 0f, 0f);
            Assert.Equal(207f, leaf.WorldTransformMatrix.M41);
        }

        /// <summary>Initializes the headless runtime required by entity hierarchy lifecycle.</summary>
        /// <returns>Core owning the test entities.</returns>
        static Core CreateCore() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            return core;
        }
    }
}
