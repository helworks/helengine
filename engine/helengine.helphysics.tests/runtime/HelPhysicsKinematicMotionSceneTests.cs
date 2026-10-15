namespace helengine {
    /// <summary>Exercises authored kinematic paths through real scene binding and fixed stepping.</summary>
    [Collection("HelPhysicsSceneBindingCoreTests")]
    public sealed class HelPhysicsKinematicMotionSceneTests {
        /// <summary>Checks interpolation, endpoint clamping, and ping-pong reversal at fixed-step boundaries.</summary>
        [Theory]
        [InlineData(false, 30, 0.5)]
        [InlineData(false, 90, 1.0)]
        [InlineData(true, 90, 0.5)]
        public void Step_AuthoredPath_PublishesExpectedLocalPosition(bool pingPong, int steps, double expectedX) {
            using Core core = CreateCore();
            using HelPhysicsRuntime3D runtime = new HelPhysicsRuntime3D(core);
            Entity platform = CreatePlatform(core, pingPong);
            runtime.BindScene(new[] { platform });
            for (int index = 0; index < steps; index++) runtime.Step(core.PhysicsScheduler.StepSeconds);
            Assert.InRange(platform.LocalPosition.X, (float)expectedX - 0.002f, (float)expectedX + 0.002f);
        }

        /// <summary>Checks local path interpretation beneath a translated, rotated, scaled parent.</summary>
        [Fact]
        public void Step_ParentedPath_TransformsLocalTargetIntoWorldSpace() {
            using Core core = CreateCore();
            using HelPhysicsRuntime3D runtime = new HelPhysicsRuntime3D(core);
            Entity parent = HelPhysicsTestSceneFactory3D.CreateEntity(core, new float3(10f, 0f, 0f));
            parent.LocalScale = new float3(2f, 2f, 2f);
            float4.CreateFromAxisAngle(new float3(0f, 0f, 1f), (float)(Math.PI / 2d), out float4 rotation);
            parent.LocalOrientation = rotation;
            Entity platform = CreatePlatform(core, false);
            parent.AddChild(platform);
            runtime.BindScene(new[] { parent });
            for (int index = 0; index < 30; index++) runtime.Step(core.PhysicsScheduler.StepSeconds);
            Assert.InRange(platform.LocalPosition.X, 0.498f, 0.502f);
            Assert.InRange(platform.Position.X, 9.998f, 10.002f);
            Assert.InRange(platform.Position.Y, 0.998f, 1.002f);
        }

        /// <summary>Checks that invalid path input rejects the batch without advancing elapsed time or entity output.</summary>
        [Fact]
        public void Step_InvalidPath_RejectsBeforeOutputAndRecoversAtFirstSample() {
            using Core core = CreateCore();
            using HelPhysicsRuntime3D runtime = new HelPhysicsRuntime3D(core);
            Entity platform = CreatePlatform(core, false);
            KinematicMotion3DComponent path = (KinematicMotion3DComponent)platform.Components[2];
            runtime.BindScene(new[] { platform });
            path.EndLocalPosition = new float3(float.NaN, 0f, 0f);
            Assert.Throws<InvalidOperationException>(() => runtime.Step(core.PhysicsScheduler.StepSeconds));
            Assert.Equal(float3.Zero, platform.LocalPosition);
            path.EndLocalPosition = new float3(1f, 0f, 0f);
            runtime.Step(core.PhysicsScheduler.StepSeconds);
            Assert.InRange(platform.LocalPosition.X, 0.016f, 0.017f);
        }

        /// <summary>Checks that displacement velocity from the path pushes a dynamic box through the existing solver.</summary>
        [Fact]
        public void Step_PathPusher_TransfersMotionToDynamicBox() {
            using Core core = CreateCore();
            using HelPhysicsRuntime3D runtime = new HelPhysicsRuntime3D(core);
            Entity platform = CreatePlatform(core, false);
            ((KinematicMotion3DComponent)platform.Components[2]).EndLocalPosition = new float3(3f, 0f, 0f);
            Entity box = HelPhysicsTestSceneFactory3D.CreateBoxEntity(core, new float3(1.5f, 0f, 0f), float3.One, BodyKind3D.Dynamic);
            ((RigidBody3DComponent)box.Components[0]).UseGravity = false;
            runtime.BindScene(new[] { platform, box });
            for (int index = 0; index < 60; index++) runtime.Step(core.PhysicsScheduler.StepSeconds);
            Assert.True(box.Position.X > 3.5f, $"Dynamic box remained at {box.Position.X}.");
        }

        /// <summary>Checks that a stationary controller follows a moving kinematic support using its actual body velocity.</summary>
        [Fact]
        public void Step_PathPlatform_CarriesStationaryController() {
            using Core core = CreateCore();
            using HelPhysicsRuntime3D runtime = new HelPhysicsRuntime3D(core);
            Entity platform = CreatePlatform(core, false);
            ((BoxCollider3DComponent)platform.Components[1]).Size = new float3(6f, 1f, 6f);
            Entity controller = HelPhysicsTestSceneFactory3D.CreateEntity(core, new float3(0f, 1.25f, 0f));
            controller.AddComponent(new BoxCollider3DComponent { Size = new float3(0.9f, 1.5f, 0.9f) });
            controller.AddComponent(new CharacterController3DComponent { DesiredMoveDirection = float3.Zero, MoveSpeed = 0d });
            runtime.BindScene(new[] { platform, controller });
            for (int index = 0; index < 30; index++) runtime.Step(core.PhysicsScheduler.StepSeconds);
            Assert.InRange(controller.Position.X, 0.47f, 0.51f);
            Assert.InRange(controller.Position.Y, 1.24f, 1.26f);
        }

        /// <summary>Checks that binder disposal retires its synchronizer and repeated disposal remains safe.</summary>
        [Fact]
        public void Dispose_Binder_RetiresSynchronizerBeforeFurtherStepping() {
            using Core core = CreateCore();
            using HelPhysicsRuntime3D runtime = new HelPhysicsRuntime3D(core);
            runtime.BindScene(new[] { CreatePlatform(core, false) });
            HelPhysicsSceneBinder3D binder = runtime.Binder;
            HelPhysicsEntitySynchronizer3D synchronizer = binder.Synchronizer;
            binder.Dispose();
            binder.Dispose();
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => synchronizer.Step());
            Assert.Contains("disposed", exception.Message);
        }

        /// <summary>Creates an initialized core with the same fixed scheduler used by runtime scenes.</summary>
        static Core CreateCore() {
            Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory) });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            return core;
        }

        /// <summary>Creates a kinematic box with a one-second local path from zero to positive X.</summary>
        static Entity CreatePlatform(Core core, bool pingPong) {
            Entity platform = HelPhysicsTestSceneFactory3D.CreateBoxEntity(core, float3.Zero, float3.One, BodyKind3D.Kinematic);
            platform.AddComponent(new KinematicMotion3DComponent { StartLocalPosition = float3.Zero, EndLocalPosition = new float3(1f, 0f, 0f), TravelDurationSeconds = 1d, PingPong = pingPong });
            return platform;
        }
    }
}
