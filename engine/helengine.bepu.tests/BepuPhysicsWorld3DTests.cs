namespace helengine.bepu.tests {
    /// <summary>
    /// Verifies the new BEPU-backed world package can be constructed by tests.
    /// </summary>
    public sealed class BepuPhysicsWorld3DTests : IDisposable {
        /// <summary>
        /// Initializes the minimal core services required for entity-backed world tests.
        /// </summary>
        public BepuPhysicsWorld3DTests() {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Ends one world test lifecycle.
        /// </summary>
        public void Dispose() {
        }

        /// <summary>
        /// Ensures the BEPU-backed physics world type can be instantiated.
        /// </summary>
        [Fact]
        public void CreateDefault_ConstructsWorldInstance() {
            BepuPhysicsWorld3D world = BepuPhysicsWorld3D.CreateDefault();

            Assert.NotNull(world);
        }

        /// <summary>
        /// Ensures box collider components can be translated into supported runtime shapes.
        /// </summary>
        [Fact]
        public void CreateBoxShape_WithBoxCollider_ReturnsBoxShape() {
            BoxCollider3DComponent collider = new BoxCollider3DComponent {
                Size = new float3(2f, 4f, 6f)
            };

            BepuPhysics.Collidables.Box boxShape = BepuShapeFactory3D.CreateBoxShape(collider);

            Assert.Equal(2f, boxShape.Width);
            Assert.Equal(4f, boxShape.Height);
            Assert.Equal(6f, boxShape.Length);
        }

        /// <summary>
        /// Ensures sphere collider components can be translated into supported runtime shapes.
        /// </summary>
        [Fact]
        public void CreateSphereShape_WithSphereCollider_ReturnsSphereShape() {
            SphereCollider3DComponent collider = new SphereCollider3DComponent {
                Radius = 0.75f
            };

            BepuPhysics.Collidables.Sphere sphereShape = BepuShapeFactory3D.CreateSphereShape(collider);

            Assert.Equal(0.75f, sphereShape.Radius);
        }

        /// <summary>
        /// Ensures scene binding registers supported rigid-body entities.
        /// </summary>
        [Fact]
        public void BindScene_WithDynamicBoxEntity_RegistersOneRuntimeBody() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = true,
                Mass = 1d
            });
            entity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(1f, 1f, 1f)
            });

            BepuPhysicsWorld3D world = BepuPhysicsWorld3D.CreateDefault();
            world.BindScene(new[] { entity });

            Assert.Equal(1, world.RegisteredBodyCount);
        }

        /// <summary>
        /// Ensures a dynamic box falls and remains above a static ground box after contact resolution.
        /// </summary>
        [Fact]
        public void Step_WithDynamicBoxAboveStaticGround_FallsAndResolvesGroundContact() {
            Entity groundEntity = CreateStaticBoxEntity(new float3(0f, -0.5f, 0f), new float3(8f, 1f, 8f));
            Entity dynamicEntity = CreateDynamicBoxEntity(new float3(0f, 2f, 0f), new float3(1f, 1f, 1f));

            BepuPhysicsWorld3D world = BepuPhysicsWorld3D.CreateDefault();
            world.BindScene(new[] { groundEntity, dynamicEntity });

            for (int index = 0; index < 120; index++) {
                world.Step(1.0 / 60.0);
            }

            Assert.True(dynamicEntity.LocalPosition.Y < 2f);
            Assert.InRange(dynamicEntity.LocalPosition.Y, 0.49f, 0.55f);
        }

        /// <summary>
        /// Ensures a dynamic sphere falls and remains above a static ground box after contact resolution.
        /// </summary>
        [Fact]
        public void Step_WithDynamicSphereAboveStaticGround_FallsAndResolvesGroundContact() {
            Entity groundEntity = CreateStaticBoxEntity(new float3(0f, -0.5f, 0f), new float3(8f, 1f, 8f));
            Entity dynamicEntity = CreateDynamicSphereEntity(new float3(0f, 2f, 0f), 0.5f);

            BepuPhysicsWorld3D world = BepuPhysicsWorld3D.CreateDefault();
            world.BindScene(new[] { groundEntity, dynamicEntity });

            for (int index = 0; index < 120; index++) {
                world.Step(1.0 / 60.0);
            }

            Assert.True(dynamicEntity.LocalPosition.Y < 2f);
            Assert.InRange(dynamicEntity.LocalPosition.Y, 0.49f, 0.55f);
        }

        /// <summary>
        /// Ensures one simple two-box stack remains vertically supported above the ground.
        /// </summary>
        [Fact]
        public void Step_WithTwoDynamicBoxesAboveStaticGround_ResolvesSimpleStack() {
            Entity groundEntity = CreateStaticBoxEntity(new float3(0f, -0.5f, 0f), new float3(8f, 1f, 8f));
            Entity lowerBoxEntity = CreateDynamicBoxEntity(new float3(0f, 1f, 0f), new float3(1f, 1f, 1f));
            Entity upperBoxEntity = CreateDynamicBoxEntity(new float3(0f, 2f, 0f), new float3(1f, 1f, 1f));

            BepuPhysicsWorld3D world = BepuPhysicsWorld3D.CreateDefault();
            world.BindScene(new[] { groundEntity, lowerBoxEntity, upperBoxEntity });

            for (int index = 0; index < 180; index++) {
                world.Step(1.0 / 60.0);
            }

            Assert.InRange(lowerBoxEntity.LocalPosition.Y, 0.49f, 0.56f);
            Assert.InRange(upperBoxEntity.LocalPosition.Y, 1.48f, 1.56f);
        }

        /// <summary>
        /// Ensures a half-unit-overhung four-box tower does not remain unrealistically balanced when simulated by the BEPU-backed runtime.
        /// </summary>
        [Fact]
        public void Step_WithHalfUnitOffsetFourBoxTower_TopplesInsteadOfRemainingFullyStacked() {
            Entity groundEntity = CreateStaticBoxEntity(new float3(0f, -0.5f, 0f), new float3(8f, 1f, 8f));
            Entity firstBoxEntity = CreateDynamicBoxEntity(new float3(0f, 1f, 0f), new float3(1f, 1f, 1f));
            Entity secondBoxEntity = CreateDynamicBoxEntity(new float3(0.5f, 2f, 0f), new float3(1f, 1f, 1f));
            Entity thirdBoxEntity = CreateDynamicBoxEntity(new float3(1.0f, 3f, 0f), new float3(1f, 1f, 1f));
            Entity fourthBoxEntity = CreateDynamicBoxEntity(new float3(1.5f, 4f, 0f), new float3(1f, 1f, 1f));

            BepuPhysicsWorld3D world = BepuPhysicsWorld3D.CreateDefault();
            world.BindScene(new[] { groundEntity, firstBoxEntity, secondBoxEntity, thirdBoxEntity, fourthBoxEntity });

            for (int index = 0; index < 240; index++) {
                world.Step(1.0 / 60.0);
            }

            bool fourthBoxStayedStacked =
                fourthBoxEntity.LocalPosition.Y > 3.3f &&
                Math.Abs(fourthBoxEntity.LocalPosition.X - 1.5f) < 0.2f;
            bool thirdBoxStayedStacked =
                thirdBoxEntity.LocalPosition.Y > 2.3f &&
                Math.Abs(thirdBoxEntity.LocalPosition.X - 1.0f) < 0.2f;

            Assert.False(
                fourthBoxStayedStacked && thirdBoxStayedStacked,
                $"Expected the overhung tower to topple, but box03 ended at ({thirdBoxEntity.LocalPosition.X}, {thirdBoxEntity.LocalPosition.Y}, {thirdBoxEntity.LocalPosition.Z}) and box04 ended at ({fourthBoxEntity.LocalPosition.X}, {fourthBoxEntity.LocalPosition.Y}, {fourthBoxEntity.LocalPosition.Z}).");
        }

        /// <summary>
        /// Ensures one sphere can remain supported on top of another sphere above static ground.
        /// </summary>
        [Fact]
        public void Step_WithTwoDynamicSpheresAboveStaticGround_ResolvesSimpleStack() {
            Entity groundEntity = CreateStaticBoxEntity(new float3(0f, -0.5f, 0f), new float3(8f, 1f, 8f));
            Entity lowerSphereEntity = CreateDynamicSphereEntity(new float3(0f, 0.5f, 0f), 0.5f);
            Entity upperSphereEntity = CreateDynamicSphereEntity(new float3(0f, 1.5f, 0f), 0.5f);

            BepuPhysicsWorld3D world = BepuPhysicsWorld3D.CreateDefault();
            world.BindScene(new[] { groundEntity, lowerSphereEntity, upperSphereEntity });

            for (int index = 0; index < 180; index++) {
                world.Step(1.0 / 60.0);
            }

            Assert.InRange(lowerSphereEntity.LocalPosition.Y, 0.49f, 0.56f);
            Assert.InRange(upperSphereEntity.LocalPosition.Y, 1.48f, 1.56f);
        }

        /// <summary>
        /// Ensures a falling sphere resolves against a supported dynamic box.
        /// </summary>
        [Fact]
        public void Step_WithDynamicSphereAboveDynamicBox_ResolvesBoxSphereContact() {
            Entity groundEntity = CreateStaticBoxEntity(new float3(0f, -0.5f, 0f), new float3(8f, 1f, 8f));
            Entity boxEntity = CreateDynamicBoxEntity(new float3(0f, 1f, 0f), new float3(1f, 1f, 1f));
            Entity sphereEntity = CreateDynamicSphereEntity(new float3(0f, 2f, 0f), 0.5f);

            BepuPhysicsWorld3D world = BepuPhysicsWorld3D.CreateDefault();
            world.BindScene(new[] { groundEntity, boxEntity, sphereEntity });

            for (int index = 0; index < 180; index++) {
                world.Step(1.0 / 60.0);
            }

            Assert.InRange(boxEntity.LocalPosition.Y, 0.49f, 0.56f);
            Assert.InRange(sphereEntity.LocalPosition.Y, 1.48f, 1.56f);
        }

        /// <summary>
        /// Creates one static box entity for BEPU-backed world tests.
        /// </summary>
        /// <param name="position">Authored box center position.</param>
        /// <param name="size">Full box size.</param>
        /// <returns>Configured static box entity.</returns>
        static Entity CreateStaticBoxEntity(float3 position, float3 size) {
            Entity entity = new Entity();
            entity.LocalPosition = position;
            entity.InitComponents();
            entity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            entity.AddComponent(new BoxCollider3DComponent {
                Size = size
            });
            return entity;
        }

        /// <summary>
        /// Creates one dynamic box entity for BEPU-backed world tests.
        /// </summary>
        /// <param name="position">Authored box center position.</param>
        /// <param name="size">Full box size.</param>
        /// <returns>Configured dynamic box entity.</returns>
        static Entity CreateDynamicBoxEntity(float3 position, float3 size) {
            Entity entity = new Entity();
            entity.LocalPosition = position;
            entity.InitComponents();
            entity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = true,
                Mass = 1d
            });
            entity.AddComponent(new BoxCollider3DComponent {
                Size = size
            });
            return entity;
        }

        /// <summary>
        /// Creates one dynamic sphere entity for BEPU-backed world tests.
        /// </summary>
        /// <param name="position">Authored sphere center position.</param>
        /// <param name="radius">Sphere radius.</param>
        /// <returns>Configured dynamic sphere entity.</returns>
        static Entity CreateDynamicSphereEntity(float3 position, float radius) {
            Entity entity = new Entity();
            entity.LocalPosition = position;
            entity.InitComponents();
            entity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = true,
                Mass = 1d
            });
            entity.AddComponent(new SphereCollider3DComponent {
                Radius = radius
            });
            return entity;
        }
    }
}
