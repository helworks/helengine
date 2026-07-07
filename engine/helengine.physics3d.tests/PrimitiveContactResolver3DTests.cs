namespace helengine.physics3d.tests {
    /// <summary>
    /// Verifies primitive contact resolver classes can evaluate direct overlap queries independently from the world host.
    /// </summary>
    [Collection(Physics3DTestCollection.Name)]
    public sealed class PrimitiveContactResolver3DTests : IDisposable {
        /// <summary>
        /// Initializes the minimal core services required for entity-backed primitive contact tests.
        /// </summary>
        public PrimitiveContactResolver3DTests() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Leaves the active core singleton attached after each test.
        /// </summary>
        public void Dispose() {
        }

        /// <summary>
        /// Ensures the box-box resolver reports one positive penetration when two boxes overlap.
        /// </summary>
        [Fact]
        public void TryResolveContact_WithOverlappingBoxes_ReturnsPositivePenetration() {
            BodyState3D first = CreateBoxBodyState(new float3(-0.2f, 0f, 0f), new float3(1f, 1f, 1f));
            BodyState3D second = CreateBoxBodyState(new float3(0.2f, 0f, 0f), new float3(1f, 1f, 1f));

            bool found = BoxBoxContactResolver3D.TryResolveContact(first, second, out float penetration, out int axisIndex);

            Assert.True(found);
            Assert.True(penetration > 0f);
            Assert.InRange(axisIndex, 0, 2);
        }

        /// <summary>
        /// Ensures the sphere-sphere resolver reports one positive penetration and unit normal when two spheres overlap.
        /// </summary>
        [Fact]
        public void TryResolveContact_WithOverlappingSpheres_ReturnsPositivePenetration() {
            BodyState3D first = CreateSphereBodyState(new float3(-0.2f, 0f, 0f), 0.5f);
            BodyState3D second = CreateSphereBodyState(new float3(0.2f, 0f, 0f), 0.5f);

            bool found = SphereSphereContactResolver3D.TryResolveContact(first, second, out float3 collisionNormal, out float penetration);

            Assert.True(found);
            Assert.True(penetration > 0f);
            Assert.InRange(float3.Dot(collisionNormal, collisionNormal), 0.99f, 1.01f);
        }

        /// <summary>
        /// Ensures the sphere-box resolver reports one positive penetration when a sphere overlaps a box.
        /// </summary>
        [Fact]
        public void TryResolveContact_WithOverlappingSphereAndBox_ReturnsPositivePenetration() {
            BodyState3D sphere = CreateSphereBodyState(new float3(0f, 0f, 0f), 0.5f);
            BodyState3D box = CreateBoxBodyState(new float3(0.3f, 0f, 0f), new float3(1f, 1f, 1f));

            bool found = SphereBoxContactResolver3D.TryResolveContact(sphere, box, out float3 collisionNormal, out float penetration);

            Assert.True(found);
            Assert.True(penetration > 0f);
            Assert.InRange(float3.Dot(collisionNormal, collisionNormal), 0.99f, 1.01f);
        }

        /// <summary>
        /// Ensures the capsule-box resolver reports one positive penetration when a capsule overlaps a box.
        /// </summary>
        [Fact]
        public void TryResolveContact_WithOverlappingCapsuleAndBox_ReturnsPositivePenetration() {
            BodyState3D capsule = CreateCapsuleBodyState(new float3(0f, 0f, 0f), 0.5f, 2f);
            BodyState3D box = CreateBoxBodyState(new float3(0.3f, 0f, 0f), new float3(1f, 1f, 1f));

            bool found = CapsuleBoxContactResolver3D.TryResolveContact(capsule, box, out float3 collisionNormal, out float penetration);

            Assert.True(found);
            Assert.True(penetration > 0f);
            Assert.InRange(float3.Dot(collisionNormal, collisionNormal), 0.99f, 1.01f);
        }

        /// <summary>
        /// Ensures the capsule-sphere resolver reports one positive penetration when a capsule overlaps a sphere.
        /// </summary>
        [Fact]
        public void TryResolveContact_WithOverlappingCapsuleAndSphere_ReturnsPositivePenetration() {
            BodyState3D capsule = CreateCapsuleBodyState(new float3(0f, 0f, 0f), 0.5f, 2f);
            BodyState3D sphere = CreateSphereBodyState(new float3(0.2f, 0f, 0f), 0.5f);

            bool found = CapsuleSphereContactResolver3D.TryResolveContact(capsule, sphere, out float3 collisionNormal, out float penetration);

            Assert.True(found);
            Assert.True(penetration > 0f);
            Assert.InRange(float3.Dot(collisionNormal, collisionNormal), 0.99f, 1.01f);
        }

        /// <summary>
        /// Ensures the capsule-capsule resolver reports one positive penetration when two capsules overlap.
        /// </summary>
        [Fact]
        public void TryResolveContact_WithOverlappingCapsules_ReturnsPositivePenetration() {
            BodyState3D first = CreateCapsuleBodyState(new float3(-0.2f, 0f, 0f), 0.5f, 2f);
            BodyState3D second = CreateCapsuleBodyState(new float3(0.2f, 0f, 0f), 0.5f, 2f);

            bool found = CapsuleCapsuleContactResolver3D.TryResolveContact(first, second, out float3 collisionNormal, out float penetration);

            Assert.True(found);
            Assert.True(penetration > 0f);
            Assert.InRange(float3.Dot(collisionNormal, collisionNormal), 0.99f, 1.01f);
        }

        /// <summary>
        /// Creates one initialized box-backed body state for resolver testing.
        /// </summary>
        /// <param name="position">Initial body position.</param>
        /// <param name="size">Full box size.</param>
        /// <returns>Initialized box-backed body state.</returns>
        static BodyState3D CreateBoxBodyState(float3 position, float3 size) {
            Entity entity = CreateEntity(position);
            RigidBody3DComponent rigidBody = new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = false,
                Mass = 1d
            };
            BoxCollider3DComponent collider = new BoxCollider3DComponent {
                Size = size
            };
            entity.AddComponent(rigidBody);
            entity.AddComponent(collider);
            return new BodyState3D(entity, rigidBody, collider, null);
        }

        /// <summary>
        /// Creates one initialized sphere-backed body state for resolver testing.
        /// </summary>
        /// <param name="position">Initial body position.</param>
        /// <param name="radius">Sphere radius.</param>
        /// <returns>Initialized sphere-backed body state.</returns>
        static BodyState3D CreateSphereBodyState(float3 position, float radius) {
            Entity entity = CreateEntity(position);
            RigidBody3DComponent rigidBody = new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = false,
                Mass = 1d
            };
            SphereCollider3DComponent collider = new SphereCollider3DComponent {
                Radius = radius
            };
            entity.AddComponent(rigidBody);
            entity.AddComponent(collider);
            return new BodyState3D(entity, rigidBody, collider, null);
        }

        /// <summary>
        /// Creates one initialized capsule-backed body state for resolver testing.
        /// </summary>
        /// <param name="position">Initial body position.</param>
        /// <param name="radius">Capsule radius.</param>
        /// <param name="height">Capsule full height.</param>
        /// <returns>Initialized capsule-backed body state.</returns>
        static BodyState3D CreateCapsuleBodyState(float3 position, float radius, float height) {
            Entity entity = CreateEntity(position);
            RigidBody3DComponent rigidBody = new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = false,
                Mass = 1d
            };
            CapsuleCollider3DComponent collider = new CapsuleCollider3DComponent {
                Radius = radius,
                Height = height
            };
            entity.AddComponent(rigidBody);
            entity.AddComponent(collider);
            return new BodyState3D(entity, rigidBody, collider, null);
        }

        /// <summary>
        /// Creates one initialized entity suitable for direct resolver tests.
        /// </summary>
        /// <param name="localPosition">Initial local position.</param>
        /// <returns>Initialized entity.</returns>
        static Entity CreateEntity(float3 localPosition) {
            Entity entity = new Entity {
                LocalPosition = localPosition,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity
            };
            entity.InitComponents();
            entity.InitChildren();
            return entity;
        }
    }
}

