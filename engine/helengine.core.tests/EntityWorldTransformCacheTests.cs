using helengine;
using Xunit;

namespace helengine.core.tests {
    /// <summary>
    /// Verifies the cached world transforms on <see cref="Entity"/> stay identical to the uncached parent-chain composition through
    /// deep hierarchies, later parent edits, and reparenting.
    /// </summary>
    public sealed class EntityWorldTransformCacheTests {
        [Fact]
        public void Deep_hierarchy_world_transforms_match_the_uncached_parent_chain_composition() {
            Core core = CreateInitializedCore();
            Entity root = CreateEntity(core, new float3(1f, 2f, 3f), new float3(2f, 2f, 2f), CreateRotation(float3.UnitY, 0.5f));
            Entity child = CreateEntity(core, new float3(0.5f, -1f, 4f), new float3(0.5f, 3f, 1f), CreateRotation(float3.UnitX, 1.1f));
            Entity grandChild = CreateEntity(core, new float3(-2f, 0.25f, 1f), new float3(1.5f, 0.5f, 2f), CreateRotation(float3.UnitZ, -0.7f));
            Entity greatGrandChild = CreateEntity(core, new float3(3f, 3f, -3f), new float3(1f, 2f, 0.5f), CreateRotation(float3.UnitY, 2.2f));

            root.AddChild(child);
            child.AddChild(grandChild);
            grandChild.AddChild(greatGrandChild);

            AssertWorldTransformMatchesUncachedComposition(root);
            AssertWorldTransformMatchesUncachedComposition(child);
            AssertWorldTransformMatchesUncachedComposition(grandChild);
            AssertWorldTransformMatchesUncachedComposition(greatGrandChild);
        }

        [Fact]
        public void World_transform_refreshes_after_the_parent_moves_following_a_child_read() {
            Core core = CreateInitializedCore();
            Entity root = CreateEntity(core, new float3(1f, 0f, 0f), float3.One, float4.Identity);
            Entity child = CreateEntity(core, new float3(2f, 0f, 0f), float3.One, float4.Identity);
            root.AddChild(child);

            Assert.Equal(3f, child.Position.X, 4);

            root.Position = new float3(10f, 0f, 0f);

            Assert.Equal(12f, child.Position.X, 4);
            AssertWorldTransformMatchesUncachedComposition(child);
        }

        [Fact]
        public void World_transform_refreshes_for_grandchildren_after_an_ancestor_moves() {
            Core core = CreateInitializedCore();
            Entity root = CreateEntity(core, new float3(1f, 1f, 1f), new float3(2f, 2f, 2f), CreateRotation(float3.UnitY, 0.3f));
            Entity child = CreateEntity(core, new float3(1f, 0f, 0f), new float3(3f, 3f, 3f), CreateRotation(float3.UnitX, 0.4f));
            Entity grandChild = CreateEntity(core, new float3(0f, 1f, 0f), new float3(0.5f, 0.5f, 0.5f), CreateRotation(float3.UnitZ, 0.6f));
            root.AddChild(child);
            child.AddChild(grandChild);

            AssertWorldTransformMatchesUncachedComposition(grandChild);

            root.LocalScale = new float3(4f, 1f, 0.25f);
            root.LocalOrientation = CreateRotation(float3.UnitZ, 1.25f);
            root.LocalPosition = new float3(-5f, 6f, 7f);

            AssertWorldTransformMatchesUncachedComposition(child);
            AssertWorldTransformMatchesUncachedComposition(grandChild);
        }

        [Fact]
        public void World_transform_refreshes_after_reparenting_a_previously_read_subtree() {
            Core core = CreateInitializedCore();
            Entity firstParent = CreateEntity(core, new float3(1f, 0f, 0f), new float3(2f, 2f, 2f), CreateRotation(float3.UnitY, 0.2f));
            Entity secondParent = CreateEntity(core, new float3(-9f, 4f, 2f), new float3(0.5f, 3f, 1f), CreateRotation(float3.UnitX, 1.4f));
            Entity child = CreateEntity(core, new float3(1f, 1f, 1f), new float3(1f, 1f, 1f), CreateRotation(float3.UnitZ, 0.9f));
            Entity grandChild = CreateEntity(core, new float3(0f, 2f, 0f), new float3(2f, 1f, 1f), float4.Identity);
            child.AddChild(grandChild);
            firstParent.AddChild(child);

            AssertWorldTransformMatchesUncachedComposition(grandChild);
            float3 firstWorldPosition = grandChild.Position;

            firstParent.RemoveChild(child);
            AssertWorldTransformMatchesUncachedComposition(child);
            AssertWorldTransformMatchesUncachedComposition(grandChild);

            secondParent.AddChild(child);

            Assert.NotEqual(firstWorldPosition.X, grandChild.Position.X, 4);
            AssertWorldTransformMatchesUncachedComposition(child);
            AssertWorldTransformMatchesUncachedComposition(grandChild);
        }

        [Fact]
        public void Scale_and_rotation_composition_matches_the_previous_world_transform_formula() {
            Core core = CreateInitializedCore();
            Entity root = CreateEntity(core, new float3(4f, -2f, 0.5f), new float3(1.5f, 2.5f, 0.75f), CreateRotation(new float3(0.3f, 0.6f, 0.2f), 1.9f));
            Entity child = CreateEntity(core, new float3(-1.25f, 3f, 2f), new float3(2f, 0.5f, 4f), CreateRotation(new float3(0.9f, 0.1f, 0.4f), -0.8f));
            root.AddChild(child);

            float3 expectedScale = child.LocalScale * root.Scale;
            float4 childOrientation = child.LocalOrientation;
            float4 rootOrientation = root.Orientation;
            float4.Concatenate(ref childOrientation, ref rootOrientation, out float4 expectedOrientation);
            float3 scaledLocal = child.LocalPosition * root.Scale;
            float3 rotatedLocal = float4.RotateVector(scaledLocal, root.Orientation);
            float3 expectedPosition = rotatedLocal + root.Position;

            AssertFloat3Equal(expectedScale, child.Scale);
            AssertFloat4Equal(expectedOrientation, child.Orientation);
            AssertFloat3Equal(expectedPosition, child.Position);
        }

        [Fact]
        public void Local_transform_writes_invalidate_the_entity_own_cached_world_transform() {
            Core core = CreateInitializedCore();
            Entity entity = CreateEntity(core, new float3(1f, 1f, 1f), float3.One, float4.Identity);

            Assert.Equal(1f, entity.Position.X, 4);

            entity.Position = new float3(5f, 1f, 1f);
            Assert.Equal(5f, entity.Position.X, 4);

            entity.LocalScale = new float3(3f, 3f, 3f);
            Assert.Equal(3f, entity.Scale.X, 4);

            entity.LocalOrientation = CreateRotation(float3.UnitY, 1f);
            AssertFloat4Equal(entity.LocalOrientation, entity.Orientation);
        }

        /// <summary>
        /// Asserts every cached world component of one entity equals the value the previous uncached parent-chain walk produced.
        /// </summary>
        /// <param name="entity">Entity whose world transform should be validated.</param>
        static void AssertWorldTransformMatchesUncachedComposition(Entity entity) {
            AssertFloat3Equal(ComputeUncachedWorldScale(entity), entity.Scale);
            AssertFloat4Equal(ComputeUncachedWorldOrientation(entity), entity.Orientation);
            AssertFloat3Equal(ComputeUncachedWorldPosition(entity), entity.Position);
        }

        /// <summary>
        /// Reproduces the previous world-position getter: walk the parent chain and recompose on every read.
        /// </summary>
        /// <param name="entity">Entity whose world position should be recomposed.</param>
        /// <returns>World position produced by the uncached formula.</returns>
        static float3 ComputeUncachedWorldPosition(Entity entity) {
            float3 pos = entity.LocalPosition;
            Entity parent = entity.Parent;
            if (parent != null) {
                float3 scaledLocal = pos * ComputeUncachedWorldScale(parent);
                float3 rotatedLocal = float4.RotateVector(scaledLocal, ComputeUncachedWorldOrientation(parent));
                pos = rotatedLocal + ComputeUncachedWorldPosition(parent);
            }

            return pos;
        }

        /// <summary>
        /// Reproduces the previous world-scale getter: multiply the local scale through the parent chain on every read.
        /// </summary>
        /// <param name="entity">Entity whose world scale should be recomposed.</param>
        /// <returns>World scale produced by the uncached formula.</returns>
        static float3 ComputeUncachedWorldScale(Entity entity) {
            float3 sca = entity.LocalScale;
            Entity parent = entity.Parent;
            if (parent != null) {
                sca *= ComputeUncachedWorldScale(parent);
            }

            return sca;
        }

        /// <summary>
        /// Reproduces the previous world-orientation getter: concatenate the local rotation through the parent chain on every read.
        /// </summary>
        /// <param name="entity">Entity whose world orientation should be recomposed.</param>
        /// <returns>World orientation produced by the uncached formula.</returns>
        static float4 ComputeUncachedWorldOrientation(Entity entity) {
            float4 ori = entity.LocalOrientation;
            Entity parent = entity.Parent;
            if (parent != null) {
                float4 parentOrientation = ComputeUncachedWorldOrientation(parent);
                float4.Concatenate(ref ori, ref parentOrientation, out ori);
            }

            return ori;
        }

        /// <summary>
        /// Asserts two three-component vectors match within the tolerance used across these transform tests.
        /// </summary>
        /// <param name="expected">Expected vector.</param>
        /// <param name="actual">Actual vector.</param>
        static void AssertFloat3Equal(float3 expected, float3 actual) {
            Assert.Equal(expected.X, actual.X, 4);
            Assert.Equal(expected.Y, actual.Y, 4);
            Assert.Equal(expected.Z, actual.Z, 4);
        }

        /// <summary>
        /// Asserts two quaternions match within the tolerance used across these transform tests.
        /// </summary>
        /// <param name="expected">Expected quaternion.</param>
        /// <param name="actual">Actual quaternion.</param>
        static void AssertFloat4Equal(float4 expected, float4 actual) {
            Assert.Equal(expected.X, actual.X, 4);
            Assert.Equal(expected.Y, actual.Y, 4);
            Assert.Equal(expected.Z, actual.Z, 4);
            Assert.Equal(expected.W, actual.W, 4);
        }

        /// <summary>
        /// Builds one normalized axis-angle rotation for hierarchy composition coverage.
        /// </summary>
        /// <param name="axis">Rotation axis; normalized before use.</param>
        /// <param name="angle">Rotation angle in radians.</param>
        /// <returns>Rotation quaternion.</returns>
        static float4 CreateRotation(float3 axis, float angle) {
            float3 normalized = float3.Normalize(axis);
            float4.CreateFromAxisAngle(ref normalized, angle, out float4 rotation);
            return rotation;
        }

        /// <summary>
        /// Creates one entity with initialized child and component collections and the supplied local transform.
        /// </summary>
        /// <param name="core">Owning core.</param>
        /// <param name="localPosition">Local position to assign.</param>
        /// <param name="localScale">Local scale to assign.</param>
        /// <param name="localOrientation">Local orientation to assign.</param>
        /// <returns>Configured entity.</returns>
        static Entity CreateEntity(Core core, float3 localPosition, float3 localScale, float4 localOrientation) {
            Entity entity = new Entity(core);
            entity.InitComponents();
            entity.InitChildren();
            entity.LocalPosition = localPosition;
            entity.LocalScale = localScale;
            entity.LocalOrientation = localOrientation;
            return entity;
        }

        /// <summary>
        /// Creates one headless core whose object manager is materialized so entities can register against it.
        /// </summary>
        /// <returns>Initialized core instance.</returns>
        static Core CreateInitializedCore() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            return core;
        }
    }
}
