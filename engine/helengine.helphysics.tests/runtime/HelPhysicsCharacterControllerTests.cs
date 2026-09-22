namespace helengine {
    /// <summary>
    /// Verifies the HelPhysics controller motion seam against authored box bounds and support samples.
    /// </summary>
    public sealed class HelPhysicsCharacterControllerTests {
        /// <summary>
        /// Ensures support surface height is converted to the controller center using half the effective box height.
        /// </summary>
        [Fact]
        public void ComputeMotion_GroundSupport_SnapsCenterAboveSurface() {
            Entity entity = CreateControllerEntity();
            CharacterController3DComponent controller = entity.Components.OfType<CharacterController3DComponent>().Single();
            HelPhysicsCharacterController3D resolver = new HelPhysicsCharacterController3D(entity, controller);
            HelPhysicsCharacterControllerMotion3D motion = resolver.ComputeMotion(
                new float3(0f, 1.1f, 0f), 0f, float3.Zero, 1d / 60d,
                new HelPhysicsCharacterControllerSupport3D(true, 0f, float3.Zero),
                new HelPhysicsCharacterControllerSupport3D(true, 0f, float3.Zero));
            Assert.Equal(1f, motion.Position.Y, 5);
            Assert.True(motion.IsGrounded);
            Assert.Equal(0f, motion.VerticalVelocity, 5);
        }

        /// <summary>
        /// Ensures authored step height accepts a rising walkable support while retaining the box center offset.
        /// </summary>
        [Fact]
        public void ComputeMotion_WalkableStep_ClimbsWithinStepHeight() {
            Entity entity = CreateControllerEntity();
            CharacterController3DComponent controller = entity.Components.OfType<CharacterController3DComponent>().Single();
            controller.StepHeight = 0.5d;
            HelPhysicsCharacterController3D resolver = new HelPhysicsCharacterController3D(entity, controller);
            HelPhysicsCharacterControllerMotion3D motion = resolver.ComputeMotion(
                new float3(0f, 1f, 0f), 0f, float3.Zero, 1d / 60d,
                new HelPhysicsCharacterControllerSupport3D(true, 0f, float3.Zero),
                new HelPhysicsCharacterControllerSupport3D(true, 0.4f, float3.Zero));
            Assert.Equal(1.4f, motion.Position.Y, 5);
            Assert.True(motion.IsGrounded);
        }

        /// <summary>
        /// Ensures support normals reject slopes steeper than authored maximum walkable slope.
        /// </summary>
        [Fact]
        public void ComputeMotion_SteepSupport_DoesNotGround() {
            Entity entity = CreateControllerEntity();
            CharacterController3DComponent controller = entity.Components.OfType<CharacterController3DComponent>().Single();
            controller.MaximumSlopeDegrees = 45d;
            HelPhysicsCharacterController3D resolver = new HelPhysicsCharacterController3D(entity, controller);
            float slopeNormalY = 0.5f;
            HelPhysicsCharacterControllerMotion3D motion = resolver.ComputeMotion(
                new float3(0f, 1f, 0f), 0f, float3.Zero, 1d / 60d,
                new HelPhysicsCharacterControllerSupport3D(true, 0f, float3.Zero),
                new HelPhysicsCharacterControllerSupport3D(true, 0f, float3.Zero, new float3(0.8660254f, slopeNormalY, 0f)));
            Assert.False(motion.IsGrounded);
            Assert.Equal(1f, motion.Position.Y, 5);
        }

        /// <summary>
        /// Ensures non-finite authored input fails before a motion result can be published.
        /// </summary>
        [Fact]
        public void ComputeMotion_NonFiniteInput_Throws() {
            Entity entity = CreateControllerEntity();
            CharacterController3DComponent controller = entity.Components.OfType<CharacterController3DComponent>().Single();
            controller.DesiredMoveDirection = new float3(float.NaN, 0f, 0f);
            HelPhysicsCharacterController3D resolver = new HelPhysicsCharacterController3D(entity, controller);
            Assert.Throws<InvalidOperationException>(() => resolver.ComputeMotion(
                new float3(0f, 1f, 0f), 0f, float3.Zero, 1d / 60d,
                new HelPhysicsCharacterControllerSupport3D(false, 0f, float3.Zero),
                new HelPhysicsCharacterControllerSupport3D(false, 0f, float3.Zero)));
        }

        static Entity CreateControllerEntity() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            Entity entity = new Entity(core) { LocalPosition = new float3(0f, 1f, 0f) };
            entity.InitComponents();
            entity.InitChildren();
            entity.AddComponent(new CharacterController3DComponent());
            entity.AddComponent(new BoxCollider3DComponent { Size = new float3(1f, 2f, 1f) });
            return entity;
        }
    }
}
