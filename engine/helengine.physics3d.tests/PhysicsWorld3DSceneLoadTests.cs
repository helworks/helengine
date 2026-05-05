namespace helengine.physics3d.tests {
    /// <summary>
    /// Verifies 3D physics components can be loaded from serialized scene assets and simulated through the runtime world.
    /// </summary>
    [Collection(Physics3DTestCollection.Name)]
    public sealed class PhysicsWorld3DSceneLoadTests {
        /// <summary>
        /// Ensures a serialized rigid body and box collider can be loaded through the runtime scene loader and simulated by the physics world.
        /// </summary>
        [Fact]
        public void LoadSceneAsset_WithPhysicsComponents_LoadsAndSimulatesDynamicGroundContact() {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(null, null, null);
            Physics3DRuntimeComponentRegistration.Register(core);

            SceneAsset sceneAsset = CreatePhysicsSceneAsset();
            IReadOnlyList<Entity> rootEntities = core.SceneLoadService.Load(sceneAsset);
            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(rootEntities);

            for (int index = 0; index < 60; index++) {
                world.Step(1.0 / 60.0);
            }

            Entity dynamicEntity = rootEntities[1];
            RigidBody3DComponent rigidBody = FindRigidBody(dynamicEntity);

            Assert.InRange(dynamicEntity.LocalPosition.Y, 0.49f, 0.51f);
            Assert.NotNull(rigidBody);
            Assert.InRange(rigidBody.LinearVelocity.Y, -0.0001f, 0.0001f);
        }

        /// <summary>
        /// Ensures a serialized rigid body and sphere collider can be loaded through the runtime scene loader and simulated by the physics world.
        /// </summary>
        [Fact]
        public void LoadSceneAsset_WithSpherePhysicsComponents_LoadsAndSimulatesDynamicGroundContact() {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(null, null, null);
            Physics3DRuntimeComponentRegistration.Register(core);

            SceneAsset sceneAsset = CreateSpherePhysicsSceneAsset();
            IReadOnlyList<Entity> rootEntities = core.SceneLoadService.Load(sceneAsset);
            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(rootEntities);

            for (int index = 0; index < 60; index++) {
                world.Step(1.0 / 60.0);
            }

            Entity dynamicEntity = rootEntities[1];
            RigidBody3DComponent rigidBody = FindRigidBody(dynamicEntity);

            Assert.InRange(dynamicEntity.LocalPosition.Y, 0.49f, 0.51f);
            Assert.NotNull(rigidBody);
            Assert.InRange(rigidBody.LinearVelocity.Y, -0.0001f, 0.0001f);
        }

        /// <summary>
        /// Ensures a serialized rigid body and capsule collider can be loaded through the runtime scene loader and simulated by the physics world.
        /// </summary>
        [Fact]
        public void LoadSceneAsset_WithCapsulePhysicsComponents_LoadsAndSimulatesDynamicGroundContact() {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(null, null, null);
            Physics3DRuntimeComponentRegistration.Register(core);

            SceneAsset sceneAsset = CreateCapsulePhysicsSceneAsset();
            IReadOnlyList<Entity> rootEntities = core.SceneLoadService.Load(sceneAsset);
            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(rootEntities);

            for (int index = 0; index < 60; index++) {
                world.Step(1.0 / 60.0);
            }

            Entity dynamicEntity = rootEntities[1];
            RigidBody3DComponent rigidBody = FindRigidBody(dynamicEntity);

            Assert.InRange(dynamicEntity.LocalPosition.Y, 0.99f, 1.01f);
            Assert.NotNull(rigidBody);
            Assert.InRange(rigidBody.LinearVelocity.Y, -0.0001f, 0.0001f);
        }

        /// <summary>
        /// Ensures a serialized kinematic pusher can be loaded through the runtime scene loader and displace a dynamic body along the ground.
        /// </summary>
        [Fact]
        public void LoadSceneAsset_WithKinematicMotionComponent_MovesThePusherAndPushesTheDynamicTarget() {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(null, null, null);
            Physics3DRuntimeComponentRegistration.Register(core);

            SceneAsset sceneAsset = CreateKinematicPushSceneAsset();
            IReadOnlyList<Entity> rootEntities = core.SceneLoadService.Load(sceneAsset);
            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(rootEntities);

            for (int index = 0; index < 60; index++) {
                world.Step(1.0 / 60.0);
            }

            Entity dynamicEntity = rootEntities[1];
            Entity pusherEntity = rootEntities[2];
            float pusherX = pusherEntity.LocalPosition.X;
            float dynamicX = dynamicEntity.LocalPosition.X;
            float dynamicY = dynamicEntity.LocalPosition.Y;

            Assert.True(pusherX > -2f, $"Expected the kinematic pusher to advance along X, but its X position was {pusherX}.");
            Assert.True(dynamicX > 1.5f, $"Expected the dynamic target to be displaced along X, but its X position was {dynamicX} while the pusher X position was {pusherX}.");
            Assert.InRange(dynamicY, 0.49f, 0.51f);
        }

        /// <summary>
        /// Ensures a serialized character controller can be loaded through the runtime scene loader and climb the authored slope ramp.
        /// </summary>
        [Fact]
        public void LoadSceneAsset_WithCharacterControllerComponent_ClimbsSlopeRamp() {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(null, null, null);
            Physics3DRuntimeComponentRegistration.Register(core);

            SceneAsset sceneAsset = CreateCharacterSlopeSceneAsset();
            IReadOnlyList<Entity> rootEntities = core.SceneLoadService.Load(sceneAsset);
            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(rootEntities);

            for (int index = 0; index < 180; index++) {
                world.Step(1.0 / 60.0);
            }

            Entity controllerEntity = rootEntities[2];

            Assert.True(controllerEntity.LocalPosition.X > 1.5f, $"Expected the character controller to advance along X, but its X position was {controllerEntity.LocalPosition.X}.");
            Assert.True(controllerEntity.LocalPosition.Y > 0.9f, $"Expected the character controller to climb the slope, but its Y position was {controllerEntity.LocalPosition.Y}.");
        }

        /// <summary>
        /// Ensures a serialized character controller does not treat an overly steep ramp as walkable support.
        /// </summary>
        [Fact]
        public void LoadSceneAsset_WithCharacterControllerComponent_DoesNotClimbSteepSlopeRamp() {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(null, null, null);
            Physics3DRuntimeComponentRegistration.Register(core);

            SceneAsset sceneAsset = CreateCharacterSteepSlopeSceneAsset();
            IReadOnlyList<Entity> rootEntities = core.SceneLoadService.Load(sceneAsset);
            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(rootEntities);

            for (int index = 0; index < 180; index++) {
                world.Step(1.0 / 60.0);
            }

            Entity controllerEntity = rootEntities[2];

            Assert.True(controllerEntity.LocalPosition.X < -0.5f, $"Expected the character controller to be blocked by the steep slope, but its X position was {controllerEntity.LocalPosition.X}.");
            Assert.True(controllerEntity.LocalPosition.Y < 0.85f, $"Expected the character controller to remain near ground height instead of climbing the steep slope, but its Y position was {controllerEntity.LocalPosition.Y}.");
        }

        /// <summary>
        /// Ensures a serialized character controller can climb a short authored stair sequence using bounded step-up movement.
        /// </summary>
        [Fact]
        public void LoadSceneAsset_WithCharacterControllerComponent_ClimbsLowSteps() {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(null, null, null);
            Physics3DRuntimeComponentRegistration.Register(core);

            SceneAsset sceneAsset = CreateCharacterStepsSceneAsset();
            IReadOnlyList<Entity> rootEntities = core.SceneLoadService.Load(sceneAsset);
            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(rootEntities);

            for (int index = 0; index < 240; index++) {
                world.Step(1.0 / 60.0);
            }

            Entity controllerEntity = rootEntities[5];

            Assert.True(controllerEntity.LocalPosition.X > 3.5f, $"Expected the character controller to climb across the low step sequence, but its X position was {controllerEntity.LocalPosition.X}.");
            Assert.True(controllerEntity.LocalPosition.Y > 1.65f, $"Expected the character controller to end above the stair sequence, but its Y position was {controllerEntity.LocalPosition.Y}.");
        }

        /// <summary>
        /// Ensures a serialized character controller standing on a kinematic platform is carried by the platform motion instead of being left behind.
        /// </summary>
        [Fact]
        public void LoadSceneAsset_WithCharacterControllerOnMovingPlatform_RidesTheKinematicPlatform() {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(null, null, null);
            Physics3DRuntimeComponentRegistration.Register(core);

            SceneAsset sceneAsset = CreateCharacterMovingPlatformRideSceneAsset();
            IReadOnlyList<Entity> rootEntities = core.SceneLoadService.Load(sceneAsset);
            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(rootEntities);

            for (int index = 0; index < 60; index++) {
                world.Step(1.0 / 60.0);
            }

            Entity controllerEntity = rootEntities[2];
            Entity platformEntity = rootEntities[1];

            Assert.True(platformEntity.LocalPosition.X > 1.4f, $"Expected the moving platform to advance along X, but its X position was {platformEntity.LocalPosition.X}.");
            Assert.True(controllerEntity.LocalPosition.X > 0.9f, $"Expected the character controller to ride the platform along X, but its X position was {controllerEntity.LocalPosition.X}.");
            Assert.InRange(controllerEntity.LocalPosition.Y, 1.66f, 1.69f);
        }

        /// <summary>
        /// Ensures a character controller carried upward by a moving platform does not tunnel through a low static ceiling.
        /// </summary>
        [Fact]
        public void LoadSceneAsset_WithCharacterControllerLiftedIntoCeiling_StopsBelowTheCeiling() {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(null, null, null);
            Physics3DRuntimeComponentRegistration.Register(core);

            SceneAsset sceneAsset = CreateCharacterCeilingLiftSceneAsset();
            IReadOnlyList<Entity> rootEntities = core.SceneLoadService.Load(sceneAsset);
            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(rootEntities);

            for (int index = 0; index < 60; index++) {
                world.Step(1.0 / 60.0);
            }

            Entity controllerEntity = rootEntities[3];
            Entity ceilingEntity = rootEntities[2];
            float controllerTop = controllerEntity.LocalPosition.Y + (0.75f * controllerEntity.LocalScale.Y);
            float ceilingBottom = ceilingEntity.LocalPosition.Y - (0.25f * ceilingEntity.LocalScale.Y);

            Assert.True(controllerEntity.LocalPosition.Y > 1.5f, $"Expected the platform to lift the controller upward, but its Y position was {controllerEntity.LocalPosition.Y}.");
            Assert.True(controllerTop <= ceilingBottom + 0.01f, $"Expected the controller to stop below the ceiling, but its top was {controllerTop} while the ceiling bottom was {ceilingBottom}.");
        }

        /// <summary>
        /// Creates one minimal scene asset that contains a static ground body and one dynamic box body.
        /// </summary>
        /// <returns>Scene asset ready for runtime loading.</returns>
        static SceneAsset CreatePhysicsSceneAsset() {
            return new SceneAsset {
                Id = "scenes/physics/test_scene_runtime_load.helen",
                RootEntities = new[] {
                    CreateBodyEntity("ground", new float3(0f, -0.5f, 0f), BodyKind3D.Static, false, new float3(8f, 1f, 8f)),
                    CreateBodyEntity("dynamic", new float3(0f, 2f, 0f), BodyKind3D.Dynamic, true, new float3(1f, 1f, 1f))
                },
                AssetReferences = Array.Empty<SceneAssetReference>()
            };
        }

        /// <summary>
        /// Creates one minimal scene asset that contains a static ground body and one dynamic sphere body.
        /// </summary>
        /// <returns>Scene asset ready for runtime loading.</returns>
        static SceneAsset CreateSpherePhysicsSceneAsset() {
            return new SceneAsset {
                Id = "scenes/physics/test_scene_runtime_sphere_load.helen",
                RootEntities = new[] {
                    CreateBodyEntity("ground", new float3(0f, -0.5f, 0f), BodyKind3D.Static, false, new float3(8f, 1f, 8f)),
                    CreateSphereBodyEntity("dynamicSphere", new float3(0f, 2f, 0f), BodyKind3D.Dynamic, true, 0.5f)
                },
                AssetReferences = Array.Empty<SceneAssetReference>()
            };
        }

        /// <summary>
        /// Creates one minimal scene asset that contains a static ground body and one dynamic capsule body.
        /// </summary>
        /// <returns>Scene asset ready for runtime loading.</returns>
        static SceneAsset CreateCapsulePhysicsSceneAsset() {
            return new SceneAsset {
                Id = "scenes/physics/test_scene_runtime_capsule_load.helen",
                RootEntities = new[] {
                    CreateBodyEntity("ground", new float3(0f, -0.5f, 0f), BodyKind3D.Static, false, new float3(8f, 1f, 8f)),
                    CreateCapsuleBodyEntity("dynamicCapsule", new float3(0f, 2.5f, 0f), BodyKind3D.Dynamic, true, 0.5f, 2f)
                },
                AssetReferences = Array.Empty<SceneAssetReference>()
            };
        }

        /// <summary>
        /// Creates one minimal scene asset that contains a static ground body, a dynamic target, and one kinematic pusher that follows a serialized motion path.
        /// </summary>
        /// <returns>Scene asset ready for runtime loading.</returns>
        static SceneAsset CreateKinematicPushSceneAsset() {
            return new SceneAsset {
                Id = "scenes/physics/test_scene_kinematic_runtime_load.helen",
                RootEntities = new[] {
                    CreateBodyEntity("ground", new float3(0f, -0.5f, 0f), BodyKind3D.Static, false, new float3(18f, 1f, 12f)),
                    CreateBodyEntity("target", new float3(1.5f, 0.5f, 0f), BodyKind3D.Dynamic, true, new float3(1f, 1f, 1f)),
                    CreateKinematicMoverEntity("pusher", new float3(-2f, 0.5f, 0f), new float3(1.5f, 1f, 1.5f), new float3(-2f, 0.5f, 0f), new float3(0.5f, 0.5f, 0f), 1.0d)
                },
                AssetReferences = Array.Empty<SceneAssetReference>()
            };
        }

        /// <summary>
        /// Creates one minimal slope scene asset that contains static ground, a rotated ramp, and one runtime-driven character controller.
        /// </summary>
        /// <returns>Scene asset ready for runtime loading.</returns>
        static SceneAsset CreateCharacterSlopeSceneAsset() {
            return new SceneAsset {
                Id = "scenes/physics/test_scene_character_controller_runtime_load.helen",
                RootEntities = new[] {
                    CreateBodyEntity("ground", new float3(0f, -0.5f, 0f), BodyKind3D.Static, false, new float3(14f, 1f, 14f)),
                    CreateStaticMeshBodyEntity("ramp", float3.Zero, BodyKind3D.Static, false, CreateRampCollisionData()),
                    CreateCharacterControllerEntity("controller", new float3(-4f, 0.75f, 0f), new float3(0.9f, 1.5f, 0.9f), new float3(1f, 0f, 0f), 3d)
                },
                AssetReferences = Array.Empty<SceneAssetReference>()
            };
        }

        /// <summary>
        /// Creates one minimal steep-slope scene asset that contains static ground, a steep rotated ramp, and one runtime-driven character controller.
        /// </summary>
        /// <returns>Scene asset ready for runtime loading.</returns>
        static SceneAsset CreateCharacterSteepSlopeSceneAsset() {
            return new SceneAsset {
                Id = "scenes/physics/test_scene_character_controller_steep_slope_runtime_load.helen",
                RootEntities = new[] {
                    CreateBodyEntity("ground", new float3(0f, -0.5f, 0f), BodyKind3D.Static, false, new float3(14f, 1f, 14f)),
                    CreateBodyEntity("ramp", new float3(2.25f, 1.1f, 0f), new float3(5f, 0.6f, 3f), CreateYawPitchRollDegrees(0d, 0d, 60d), BodyKind3D.Static, false),
                    CreateCharacterControllerEntity("controller", new float3(-4f, 0.75f, 0f), new float3(0.9f, 1.5f, 0.9f), new float3(1f, 0f, 0f), 3d)
                },
                AssetReferences = Array.Empty<SceneAssetReference>()
            };
        }

        /// <summary>
        /// Creates one minimal stair scene asset that contains static ground, a low step sequence, and one runtime-driven character controller.
        /// </summary>
        /// <returns>Scene asset ready for runtime loading.</returns>
        static SceneAsset CreateCharacterStepsSceneAsset() {
            return new SceneAsset {
                Id = "scenes/physics/test_scene_character_controller_steps_runtime_load.helen",
                RootEntities = new[] {
                    CreateBodyEntity("ground", new float3(0f, -0.5f, 0f), BodyKind3D.Static, false, new float3(16f, 1f, 12f)),
                    CreateBodyEntity("step01", new float3(0.75f, 0.15f, 0f), BodyKind3D.Static, false, new float3(1.5f, 0.3f, 3f)),
                    CreateBodyEntity("step02", new float3(2.25f, 0.45f, 0f), BodyKind3D.Static, false, new float3(1.5f, 0.9f, 3f)),
                    CreateBodyEntity("step03", new float3(3.75f, 0.75f, 0f), BodyKind3D.Static, false, new float3(1.5f, 1.5f, 3f)),
                    CreateBodyEntity("step04", new float3(5.25f, 1.05f, 0f), BodyKind3D.Static, false, new float3(1.5f, 2.1f, 3f)),
                    CreateCharacterControllerEntity("controller", new float3(-4.5f, 0.75f, 0f), new float3(0.9f, 1.5f, 0.9f), new float3(1f, 0f, 0f), 3d)
                },
                AssetReferences = Array.Empty<SceneAssetReference>()
            };
        }

        /// <summary>
        /// Creates one minimal moving-platform scene asset that contains a kinematic platform and a character controller standing on top of it.
        /// </summary>
        /// <returns>Scene asset ready for runtime loading.</returns>
        static SceneAsset CreateCharacterMovingPlatformRideSceneAsset() {
            return new SceneAsset {
                Id = "scenes/physics/test_scene_character_controller_moving_platform_runtime_load.helen",
                RootEntities = new[] {
                    CreateBodyEntity("ground", new float3(0f, -0.5f, 0f), BodyKind3D.Static, false, new float3(18f, 1f, 14f)),
                    CreateKinematicMoverEntity("platform", new float3(-0.5f, 0.75f, 0f), new float3(2.5f, 0.35f, 2.5f), new float3(-0.5f, 0.75f, 0f), new float3(3.5f, 0.75f, 0f), 2d),
                    CreateCharacterControllerEntity("controller", new float3(-0.5f, 1.675f, 0f), new float3(0.9f, 1.5f, 0.9f), float3.Zero, 0d)
                },
                AssetReferences = Array.Empty<SceneAssetReference>()
            };
        }

        /// <summary>
        /// Creates one minimal scene asset that lifts a character controller into a low ceiling using a vertical moving platform.
        /// </summary>
        /// <returns>Scene asset ready for runtime loading.</returns>
        static SceneAsset CreateCharacterCeilingLiftSceneAsset() {
            return new SceneAsset {
                Id = "scenes/physics/test_scene_character_controller_ceiling_lift_runtime_load.helen",
                RootEntities = new[] {
                    CreateBodyEntity("ground", new float3(0f, -0.5f, 0f), BodyKind3D.Static, false, new float3(12f, 1f, 12f)),
                    CreateKinematicMoverEntity("platform", new float3(0f, 0.75f, 0f), new float3(2.5f, 0.35f, 2.5f), new float3(0f, 0.75f, 0f), new float3(0f, 2.1f, 0f), 1d),
                    CreateBodyEntity("ceiling", new float3(0f, 2.8f, 0f), BodyKind3D.Static, false, new float3(4f, 0.5f, 4f)),
                    CreateCharacterControllerEntity("controller", new float3(0f, 1.675f, 0f), new float3(0.9f, 1.5f, 0.9f), float3.Zero, 0d)
                },
                AssetReferences = Array.Empty<SceneAssetReference>()
            };
        }

        /// <summary>
        /// Creates one serialized body entity that owns a rigid body and box collider payload.
        /// </summary>
        /// <param name="entityId">Stable scene entity id.</param>
        /// <param name="localPosition">Initial local position.</param>
        /// <param name="bodyKind">Rigid body kind to serialize.</param>
        /// <param name="useGravity">True when the rigid body should receive gravity.</param>
        /// <param name="boxSize">Full collider size.</param>
        /// <returns>Serialized scene entity.</returns>
        static SceneEntityAsset CreateBodyEntity(
            string entityId,
            float3 localPosition,
            BodyKind3D bodyKind,
            bool useGravity,
            float3 boxSize) {
            return CreateBodyEntity(entityId, localPosition, boxSize, float4.Identity, bodyKind, useGravity);
        }

        /// <summary>
        /// Creates one serialized body entity that owns a rigid body and box collider payload.
        /// </summary>
        /// <param name="entityId">Stable scene entity id.</param>
        /// <param name="localPosition">Initial local position.</param>
        /// <param name="boxSize">Full collider size.</param>
        /// <param name="localOrientation">Initial local orientation.</param>
        /// <param name="bodyKind">Rigid body kind to serialize.</param>
        /// <param name="useGravity">True when the rigid body should receive gravity.</param>
        /// <returns>Serialized scene entity.</returns>
        static SceneEntityAsset CreateBodyEntity(
            string entityId,
            float3 localPosition,
            float3 boxSize,
            float4 localOrientation,
            BodyKind3D bodyKind,
            bool useGravity) {
            if (string.IsNullOrWhiteSpace(entityId)) {
                throw new ArgumentException("Entity id must be provided.", nameof(entityId));
            }

            return new SceneEntityAsset {
                Id = entityId,
                Name = entityId,
                LocalPosition = localPosition,
                LocalScale = float3.One,
                LocalOrientation = localOrientation,
                Components = new[] {
                    CreateRigidBodyRecord(bodyKind, useGravity),
                    CreateBoxColliderRecord(boxSize)
                },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates one serialized kinematic mover entity that owns rigid-body, box-collider, and motion payloads.
        /// </summary>
        /// <param name="entityId">Stable scene entity id.</param>
        /// <param name="localPosition">Initial local position.</param>
        /// <param name="boxSize">Full collider size.</param>
        /// <param name="startLocalPosition">Motion path start position.</param>
        /// <param name="endLocalPosition">Motion path end position.</param>
        /// <param name="travelDurationSeconds">One-way travel duration in seconds.</param>
        /// <returns>Serialized scene entity.</returns>
        static SceneEntityAsset CreateKinematicMoverEntity(
            string entityId,
            float3 localPosition,
            float3 boxSize,
            float3 startLocalPosition,
            float3 endLocalPosition,
            double travelDurationSeconds) {
            if (string.IsNullOrWhiteSpace(entityId)) {
                throw new ArgumentException("Entity id must be provided.", nameof(entityId));
            }

            return new SceneEntityAsset {
                Id = entityId,
                Name = entityId,
                LocalPosition = localPosition,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = new[] {
                    CreateRigidBodyRecord(BodyKind3D.Kinematic, false),
                    CreateBoxColliderRecord(boxSize),
                    CreateKinematicMotionRecord(startLocalPosition, endLocalPosition, travelDurationSeconds, true)
                },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates one serialized character-controller entity that owns box-collider and controller payloads.
        /// </summary>
        /// <param name="entityId">Stable scene entity id.</param>
        /// <param name="localPosition">Initial local position.</param>
        /// <param name="boxSize">Full controller bounds.</param>
        /// <param name="desiredMoveDirection">Desired local move direction used by the controller.</param>
        /// <param name="moveSpeed">Horizontal move speed in world units per second.</param>
        /// <returns>Serialized scene entity.</returns>
        static SceneEntityAsset CreateCharacterControllerEntity(
            string entityId,
            float3 localPosition,
            float3 boxSize,
            float3 desiredMoveDirection,
            double moveSpeed) {
            if (string.IsNullOrWhiteSpace(entityId)) {
                throw new ArgumentException("Entity id must be provided.", nameof(entityId));
            }

            return new SceneEntityAsset {
                Id = entityId,
                Name = entityId,
                LocalPosition = localPosition,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = new[] {
                    CreateBoxColliderRecord(boxSize),
                    CreateCharacterControllerRecord(desiredMoveDirection, moveSpeed, 1d, 0.75d, 0.3d, 45d)
                },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates one serialized body entity that owns a rigid body and sphere collider payload.
        /// </summary>
        /// <param name="entityId">Stable scene entity id.</param>
        /// <param name="localPosition">Initial local position.</param>
        /// <param name="bodyKind">Rigid body kind to serialize.</param>
        /// <param name="useGravity">True when the rigid body should receive gravity.</param>
        /// <param name="radius">Sphere collider radius.</param>
        /// <returns>Serialized scene entity.</returns>
        static SceneEntityAsset CreateSphereBodyEntity(
            string entityId,
            float3 localPosition,
            BodyKind3D bodyKind,
            bool useGravity,
            float radius) {
            if (string.IsNullOrWhiteSpace(entityId)) {
                throw new ArgumentException("Entity id must be provided.", nameof(entityId));
            }

            return new SceneEntityAsset {
                Id = entityId,
                Name = entityId,
                LocalPosition = localPosition,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = new[] {
                    CreateRigidBodyRecord(bodyKind, useGravity),
                    CreateSphereColliderRecord(radius)
                },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates one serialized body entity that owns a rigid body and capsule collider payload.
        /// </summary>
        /// <param name="entityId">Stable scene entity id.</param>
        /// <param name="localPosition">Initial local position.</param>
        /// <param name="bodyKind">Rigid body kind to serialize.</param>
        /// <param name="useGravity">True when the rigid body should receive gravity.</param>
        /// <param name="radius">Capsule collider radius.</param>
        /// <param name="height">Capsule collider full height.</param>
        /// <returns>Serialized scene entity.</returns>
        static SceneEntityAsset CreateCapsuleBodyEntity(
            string entityId,
            float3 localPosition,
            BodyKind3D bodyKind,
            bool useGravity,
            float radius,
            float height) {
            if (string.IsNullOrWhiteSpace(entityId)) {
                throw new ArgumentException("Entity id must be provided.", nameof(entityId));
            }

            return new SceneEntityAsset {
                Id = entityId,
                Name = entityId,
                LocalPosition = localPosition,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = new[] {
                    CreateRigidBodyRecord(bodyKind, useGravity),
                    CreateCapsuleColliderRecord(radius, height)
                },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates one serialized body entity that owns a rigid body and cooked static-mesh collider payload.
        /// </summary>
        /// <param name="entityId">Stable scene entity id.</param>
        /// <param name="localPosition">Initial local position.</param>
        /// <param name="bodyKind">Rigid body kind to serialize.</param>
        /// <param name="useGravity">True when the rigid body should receive gravity.</param>
        /// <param name="collisionData">Cooked static-mesh collision data.</param>
        /// <returns>Serialized scene entity.</returns>
        static SceneEntityAsset CreateStaticMeshBodyEntity(
            string entityId,
            float3 localPosition,
            BodyKind3D bodyKind,
            bool useGravity,
            StaticMeshCollisionData3D collisionData) {
            if (string.IsNullOrWhiteSpace(entityId)) {
                throw new ArgumentException("Entity id must be provided.", nameof(entityId));
            }
            if (collisionData == null) {
                throw new ArgumentNullException(nameof(collisionData));
            }

            return new SceneEntityAsset {
                Id = entityId,
                Name = entityId,
                LocalPosition = localPosition,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = new[] {
                    CreateRigidBodyRecord(bodyKind, useGravity),
                    CreateStaticMeshColliderRecord(collisionData)
                },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates one serialized rigid body component record.
        /// </summary>
        /// <param name="bodyKind">Rigid body kind to serialize.</param>
        /// <param name="useGravity">True when gravity should be enabled.</param>
        /// <returns>Serialized scene component record.</returns>
        static SceneComponentAssetRecord CreateRigidBodyRecord(BodyKind3D bodyKind, bool useGravity) {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteByte((byte)bodyKind);
            writer.WriteByte(useGravity ? (byte)1 : (byte)0);
            writer.WriteSingle(1f);
            writer.WriteSingle(1f);
            writer.WriteFloat3(float3.Zero);

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.RigidBody3DComponent",
                ComponentIndex = 0,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Creates one serialized box collider component record.
        /// </summary>
        /// <param name="boxSize">Full collider size.</param>
        /// <returns>Serialized scene component record.</returns>
        static SceneComponentAssetRecord CreateBoxColliderRecord(float3 boxSize) {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteFloat3(boxSize);

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.BoxCollider3DComponent",
                ComponentIndex = 1,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Creates one serialized sphere collider component record.
        /// </summary>
        /// <param name="radius">Sphere collider radius.</param>
        /// <returns>Serialized scene component record.</returns>
        static SceneComponentAssetRecord CreateSphereColliderRecord(float radius) {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteSingle(radius);

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.SphereCollider3DComponent",
                ComponentIndex = 1,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Creates one serialized capsule collider component record.
        /// </summary>
        /// <param name="radius">Capsule collider radius.</param>
        /// <param name="height">Capsule collider full height.</param>
        /// <returns>Serialized scene component record.</returns>
        static SceneComponentAssetRecord CreateCapsuleColliderRecord(float radius, float height) {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteSingle(radius);
            writer.WriteSingle(height);

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.CapsuleCollider3DComponent",
                ComponentIndex = 1,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Creates one serialized static-mesh collider component record.
        /// </summary>
        /// <param name="collisionData">Cooked static-mesh collision data.</param>
        /// <returns>Serialized scene component record.</returns>
        static SceneComponentAssetRecord CreateStaticMeshColliderRecord(StaticMeshCollisionData3D collisionData) {
            if (collisionData == null) {
                throw new ArgumentNullException(nameof(collisionData));
            }

            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteInt32(collisionData.Vertices.Length);
            for (int index = 0; index < collisionData.Vertices.Length; index++) {
                writer.WriteFloat3(collisionData.Vertices[index]);
            }

            writer.WriteInt32(collisionData.Indices.Length);
            for (int index = 0; index < collisionData.Indices.Length; index++) {
                writer.WriteInt32(collisionData.Indices[index]);
            }

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.StaticMeshCollider3DComponent",
                ComponentIndex = 1,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Creates one serialized kinematic-motion component record.
        /// </summary>
        /// <param name="startLocalPosition">Motion path start position.</param>
        /// <param name="endLocalPosition">Motion path end position.</param>
        /// <param name="travelDurationSeconds">One-way travel duration in seconds.</param>
        /// <param name="pingPong">True when the motion should reverse at the path end.</param>
        /// <returns>Serialized scene component record.</returns>
        static SceneComponentAssetRecord CreateKinematicMotionRecord(
            float3 startLocalPosition,
            float3 endLocalPosition,
            double travelDurationSeconds,
            bool pingPong) {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteFloat3(startLocalPosition);
            writer.WriteFloat3(endLocalPosition);
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(travelDurationSeconds));
            writer.WriteByte(pingPong ? (byte)1 : (byte)0);

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.KinematicMotion3DComponent",
                ComponentIndex = 2,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Creates one serialized character-controller component record.
        /// </summary>
        /// <param name="desiredMoveDirection">Desired move direction used by the controller.</param>
        /// <param name="moveSpeed">Horizontal move speed in world units per second.</param>
        /// <param name="gravityScale">Gravity multiplier used by the controller.</param>
        /// <param name="stepHeight">Maximum rise the controller can snap upward in one step.</param>
        /// <param name="groundSnapDistance">Maximum downward snap distance used to keep the controller grounded.</param>
        /// <param name="maximumSlopeDegrees">Maximum walkable slope angle in degrees.</param>
        /// <returns>Serialized scene component record.</returns>
        static SceneComponentAssetRecord CreateCharacterControllerRecord(
            float3 desiredMoveDirection,
            double moveSpeed,
            double gravityScale,
            double stepHeight,
            double groundSnapDistance,
            double maximumSlopeDegrees) {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(2);
            writer.WriteFloat3(desiredMoveDirection);
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(moveSpeed));
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(gravityScale));
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(stepHeight));
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(groundSnapDistance));
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(maximumSlopeDegrees));

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.CharacterController3DComponent",
                ComponentIndex = 1,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Creates one quaternion from yaw, pitch, and roll angles expressed in degrees.
        /// </summary>
        /// <param name="yawDegrees">Yaw around the Y axis in degrees.</param>
        /// <param name="pitchDegrees">Pitch around the X axis in degrees.</param>
        /// <param name="rollDegrees">Roll around the Z axis in degrees.</param>
        /// <returns>Converted quaternion.</returns>
        static float4 CreateYawPitchRollDegrees(double yawDegrees, double pitchDegrees, double rollDegrees) {
            float4.CreateFromYawPitchRoll(
                (float)(yawDegrees * Math.PI / 180.0),
                (float)(pitchDegrees * Math.PI / 180.0),
                (float)(rollDegrees * Math.PI / 180.0),
                out float4 result);
            return result;
        }

        /// <summary>
        /// Creates one simple walkable ramp collision blob represented by two triangles.
        /// </summary>
        /// <returns>Cooked static-mesh collision data for a walkable ramp.</returns>
        static StaticMeshCollisionData3D CreateRampCollisionData() {
            return new StaticMeshCollisionData3D(
                new[] {
                    new float3(-5f, 0f, -1.5f),
                    new float3(5f, 3f, -1.5f),
                    new float3(5f, 3f, 1.5f),
                    new float3(-5f, 0f, 1.5f)
                },
                new[] {
                    0, 2, 1,
                    0, 3, 2
                });
        }

        /// <summary>
        /// Finds the rigid body component attached to one runtime entity.
        /// </summary>
        /// <param name="entity">Entity whose rigid body should be resolved.</param>
        /// <returns>Attached rigid body component.</returns>
        static RigidBody3DComponent FindRigidBody(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (entity.Components == null) {
                throw new InvalidOperationException("Entity components were not initialized.");
            }

            for (int index = 0; index < entity.Components.Count; index++) {
                if (entity.Components[index] is RigidBody3DComponent rigidBody) {
                    return rigidBody;
                }
            }

            throw new InvalidOperationException("Expected a rigid body component on the loaded runtime entity.");
        }
    }
}
