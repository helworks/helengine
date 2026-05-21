namespace helengine.physics3d.tests {
    /// <summary>
    /// Verifies the cube-only contact resolver independently from the world host.
    /// </summary>
    [Collection(Physics3DTestCollection.Name)]
    public sealed class PrimitiveContactResolver3DTests : IDisposable {
        /// <summary>
        /// Initializes the minimal core services required for entity-backed contact tests.
        /// </summary>
        public PrimitiveContactResolver3DTests() {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Leaves the active core singleton attached after each test.
        /// </summary>
        public void Dispose() {
        }

        /// <summary>
        /// Ensures flat face contact creates a stable four-point patch.
        /// </summary>
        [Fact]
        public void TryResolveManifold_WithFlatFaceOverlap_ReportsFourPointPatch() {
            CubeBodyState3D dynamicBody = CreateBoxBodyState(new float3(0f, 0.99f, 0f), new float3(1f, 1f, 1f), BodyKind3D.Dynamic);
            CubeBodyState3D staticBody = CreateBoxBodyState(new float3(0f, 0f, 0f), new float3(4f, 1f, 4f), BodyKind3D.Static);

            bool resolved = CubeBoxContactResolver3D.TryResolveManifold(dynamicBody, staticBody, out CubeContactManifold3D manifold);

            Assert.True(resolved);
            Assert.Equal(4, manifold.ContactCount);
            Assert.InRange(manifold.Normal.Y, 0.99f, 1.01f);
            Assert.InRange(manifold.Point0.Position.Y, 0.49f, 0.51f);
            Assert.InRange(manifold.Point3.Position.Y, 0.49f, 0.51f);
        }

        /// <summary>
        /// Ensures rotated boxes that overlap in AABB but separate in OBB space are rejected.
        /// </summary>
        [Fact]
        public void TryResolveManifold_WithSeparatedRotatedBoxesInsideAabb_ReturnsFalse() {
            float3 rotationAxis = new float3(0f, 1f, 0f);
            float4.CreateFromAxisAngle(ref rotationAxis, (float)(Math.PI * 0.25d), out float4 orientation);
            CubeBodyState3D first = CreateBoxBodyState(float3.Zero, new float3(1f, 1f, 1f), BodyKind3D.Dynamic, orientation);
            CubeBodyState3D second = CreateBoxBodyState(new float3(1.42f, 0f, 0f), new float3(1f, 1f, 1f), BodyKind3D.Static);

            bool resolved = CubeBoxContactResolver3D.TryResolveManifold(first, second, out CubeContactManifold3D manifold);

            Assert.False(resolved);
            Assert.Null(manifold);
        }

        /// <summary>
        /// Creates one cube body state for contact resolver tests.
        /// </summary>
        /// <param name="position">Entity local position.</param>
        /// <param name="size">Authored full box size.</param>
        /// <param name="bodyKind">Rigid body simulation kind.</param>
        /// <returns>Initialized cube body state.</returns>
        static CubeBodyState3D CreateBoxBodyState(float3 position, float3 size, BodyKind3D bodyKind) {
            return CreateBoxBodyState(position, size, bodyKind, float4.Identity);
        }

        /// <summary>
        /// Creates one cube body state for contact resolver tests.
        /// </summary>
        /// <param name="position">Entity local position.</param>
        /// <param name="size">Authored full box size.</param>
        /// <param name="bodyKind">Rigid body simulation kind.</param>
        /// <param name="orientation">Entity local orientation.</param>
        /// <returns>Initialized cube body state.</returns>
        static CubeBodyState3D CreateBoxBodyState(float3 position, float3 size, BodyKind3D bodyKind, float4 orientation) {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();
            entity.LocalPosition = position;
            entity.LocalOrientation = orientation;
            RigidBody3DComponent rigidBody = new RigidBody3DComponent {
                BodyKind = bodyKind
            };
            BoxCollider3DComponent collider = new BoxCollider3DComponent {
                Size = size
            };
            entity.AddComponent(rigidBody);
            entity.AddComponent(collider);
            return new CubeBodyState3D(entity, rigidBody, collider, null);
        }
    }
}
