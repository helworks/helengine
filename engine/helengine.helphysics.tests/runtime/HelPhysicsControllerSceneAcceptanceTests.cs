namespace helengine {
    /// <summary>
    /// Exercises controller-only scene binding through the HelPhysics runtime adapter against authored support geometry.
    /// </summary>
    public sealed class HelPhysicsControllerSceneAcceptanceTests {
        /// <summary>
        /// Ensures a controller with a box collider binds without a rigid body, advances across a floor,
        /// climbs a walkable step, and remains grounded after fixed stepping.
        /// </summary>
        [Fact]
        public void BindScene_WithControllerAndStaticFloor_AdvancesControllerAcrossWalkableSupport() {
            using Core core = CreateCore();
            using HelPhysicsRuntime3D runtime = new HelPhysicsRuntime3D(core);
            Entity floor = CreateFloor(core);
            Entity step = CreateStep(core);
            Entity controllerEntity = CreateController(core, new float3(0f, 1.1f, 0f), 2d);

            runtime.BindScene(new[] { floor, step, controllerEntity });

            Assert.Equal(3, runtime.RegisteredBodyCount);
            float maximumObservedY = controllerEntity.Position.Y;
            for (int stepIndex = 0; stepIndex < 120; stepIndex++) {
                runtime.Step(core.PhysicsScheduler.StepSeconds);
                maximumObservedY = Math.Max(maximumObservedY, controllerEntity.Position.Y);
            }

            Assert.True(controllerEntity.Position.X > 3f);
            Assert.True(maximumObservedY >= 1.2f);
            Assert.InRange(controllerEntity.Position.Y, 0.7f, 0.8f);
        }

        /// <summary>
        /// Ensures the controller traverses the exact DemoDisc-style 18-degree static box ramp.
        /// </summary>
        [Fact]
        public void BindScene_WithDemoDisc18DegreeRamp_ClimbsRamp() {
            using Core core = CreateCore();
            using HelPhysicsRuntime3D runtime = new HelPhysicsRuntime3D(core);
            Entity floor = CreateFloor(core);
            Entity ramp = CreateDemoDiscRamp(core);
            Entity controllerEntity = CreateController(core, new float3(-4f, 0.75f, 0f), 3d);

            runtime.BindScene(new[] { floor, ramp, controllerEntity });

            for (int stepIndex = 0; stepIndex < 135; stepIndex++) {
                runtime.Step(core.PhysicsScheduler.StepSeconds);
            }

            Assert.InRange(controllerEntity.Position.X, 2.65f, 2.85f);
            Assert.InRange(controllerEntity.Position.Y, 1.75f, 2.15f);
        }

        /// <summary>
        /// Ensures a wall higher than the authored step height blocks forward controller traversal.
        /// </summary>
        [Fact]
        public void BindScene_WithWallAboveStepHeight_DoesNotCrossBarrier() {
            using Core core = CreateCore();
            using HelPhysicsRuntime3D runtime = new HelPhysicsRuntime3D(core);
            Entity floor = CreateFloor(core);
            Entity wall = CreateWall(core);
            Entity controllerEntity = CreateController(core, new float3(0f, 1.1f, 0f), 2d);

            runtime.BindScene(new[] { floor, wall, controllerEntity });

            for (int stepIndex = 0; stepIndex < 120; stepIndex++) {
                runtime.Step(core.PhysicsScheduler.StepSeconds);
            }

            Assert.True(
                controllerEntity.Position.X < 1f,
                $"Expected the controller to remain behind the tall wall, but its X position was {controllerEntity.Position.X}.");
        }

        /// <summary>
        /// Creates a core with the minimum services required by authored runtime entities.
        /// </summary>
        /// <returns>An initialized core.</returns>
        static Core CreateCore() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            return core;
        }

        /// <summary>
        /// Creates a static box whose top surface is the controller's walkable floor.
        /// </summary>
        static Entity CreateFloor(Core core) {
            return CreateStaticBox(core, new float3(0f, -0.5f, 0f), new float3(20f, 1f, 20f));
        }

        /// <summary>
        /// Creates a raised static box step within the controller path.
        /// </summary>
        static Entity CreateStep(Core core) {
            return CreateStaticBox(core, new float3(2.5f, 0.25f, 0f), new float3(1f, 0.5f, 8f));
        }

        /// <summary>
        /// Creates the DemoDisc ramp geometry: center (2.25,.6,0), size (5,.6,3), rolled 18 degrees.
        /// </summary>
        static Entity CreateDemoDiscRamp(Core core) {
            float4 orientation;
            float4.CreateFromAxisAngle(
                new float3(0f, 0f, 1f),
                (float)(18d * Math.PI / 180d),
                out orientation);
            Entity ramp = CreateStaticBox(core, new float3(2.25f, 0.6f, 0f), new float3(5f, 0.6f, 3f));
            ramp.LocalOrientation = orientation;
            return ramp;
        }

        /// <summary>
        /// Creates a static wall whose height exceeds the controller step height.
        /// </summary>
        static Entity CreateWall(Core core) {
            return CreateStaticBox(core, new float3(1.5f, 1.5f, 0f), new float3(0.5f, 3f, 8f));
        }

        /// <summary>
        /// Creates one static box entity.
        /// </summary>
        static Entity CreateStaticBox(Core core, float3 position, float3 size) {
            Entity box = new Entity(core) {
                LocalPosition = position
            };
            box.InitComponents();
            box.InitChildren();
            box.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            box.AddComponent(new BoxCollider3DComponent {
                Size = size
            });
            return box;
        }

        /// <summary>
        /// Creates a controller-only entity with authored box bounds and forward movement.
        /// </summary>
        static Entity CreateController(Core core, float3 position, double moveSpeed) {
            Entity controller = new Entity(core) {
                LocalPosition = position
            };
            controller.InitComponents();
            controller.InitChildren();
            controller.AddComponent(new BoxCollider3DComponent {
                Size = new float3(0.9f, 1.5f, 0.9f)
            });
            controller.AddComponent(new CharacterController3DComponent {
                DesiredMoveDirection = new float3(1f, 0f, 0f),
                MoveSpeed = moveSpeed,
                GravityScale = 1d,
                StepHeight = 0.75d,
                GroundSnapDistance = 0.3d,
                MaximumSlopeDegrees = 45d
            });
            return controller;
        }
    }
}
