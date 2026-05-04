namespace helengine.physics3d.tests {
    /// <summary>
    /// Verifies the first runtime 3D physics world behaviors around dynamic rigid bodies and static colliders.
    /// </summary>
    [Collection(Physics3DTestCollection.Name)]
    public sealed class PhysicsWorld3DDynamicsTests : IDisposable {
        /// <summary>
        /// Initializes the minimal core services required for entity-backed physics tests.
        /// </summary>
        public PhysicsWorld3DDynamicsTests() {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(null, null, null);
        }

        /// <summary>
        /// Detaches the active core singleton after each test so later tests start from a clean runtime.
        /// </summary>
        public void Dispose() {
        }

        /// <summary>
        /// Ensures a dynamic box falls under gravity and settles on top of one static ground box.
        /// </summary>
        [Fact]
        public void Step_WithDynamicBoxAboveStaticGround_FallsAndResolvesGroundContact() {
            Entity groundEntity = CreateEntity(new float3(0f, -0.5f, 0f));
            groundEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            groundEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(8f, 1f, 8f)
            });

            Entity dynamicEntity = CreateEntity(new float3(0f, 2f, 0f));
            RigidBody3DComponent dynamicBody = new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = true,
                Mass = 1d
            };
            dynamicEntity.AddComponent(dynamicBody);
            dynamicEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(1f, 1f, 1f)
            });

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(new[] { groundEntity, dynamicEntity });

            for (int index = 0; index < 120; index++) {
                world.Step(1.0 / 60.0);
            }

            Assert.True(dynamicEntity.LocalPosition.Y < 2f);
            Assert.InRange(dynamicEntity.LocalPosition.Y, 0.49f, 0.51f);
            Assert.InRange(dynamicBody.LinearVelocity.Y, -0.0001f, 0.0001f);
        }

        /// <summary>
        /// Ensures a dynamic box falls under gravity and settles on top of one cooked static mesh floor.
        /// </summary>
        [Fact]
        public void Step_WithDynamicBoxAboveStaticMeshGround_FallsAndResolvesGroundContact() {
            Entity meshEntity = CreateEntity(float3.Zero);
            meshEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            meshEntity.AddComponent(new StaticMeshCollider3DComponent {
                CollisionData = CreateFlatGroundCollisionData()
            });

            Entity dynamicEntity = CreateEntity(new float3(0f, 2f, 0f));
            RigidBody3DComponent dynamicBody = new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = true,
                Mass = 1d
            };
            dynamicEntity.AddComponent(dynamicBody);
            dynamicEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(1f, 1f, 1f)
            });

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(new[] { meshEntity, dynamicEntity });

            for (int index = 0; index < 120; index++) {
                world.Step(1.0 / 60.0);
            }

            Assert.True(dynamicEntity.LocalPosition.Y < 2f);
            Assert.InRange(dynamicEntity.LocalPosition.Y, 0.49f, 0.51f);
            Assert.InRange(dynamicBody.LinearVelocity.Y, -0.0001f, 0.0001f);
        }

        /// <summary>
        /// Ensures a dynamic sphere falls under gravity and settles on top of one static ground box.
        /// </summary>
        [Fact]
        public void Step_WithDynamicSphereAboveStaticGround_FallsAndResolvesGroundContact() {
            Entity groundEntity = CreateEntity(new float3(0f, -0.5f, 0f));
            groundEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            groundEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(8f, 1f, 8f)
            });

            Entity dynamicEntity = CreateEntity(new float3(0f, 2f, 0f));
            RigidBody3DComponent dynamicBody = new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = true,
                Mass = 1d
            };
            dynamicEntity.AddComponent(dynamicBody);
            dynamicEntity.AddComponent(new SphereCollider3DComponent {
                Radius = 0.5f
            });

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(new[] { groundEntity, dynamicEntity });

            for (int index = 0; index < 120; index++) {
                world.Step(1.0 / 60.0);
            }

            Assert.True(dynamicEntity.LocalPosition.Y < 2f);
            Assert.InRange(dynamicEntity.LocalPosition.Y, 0.49f, 0.51f);
            Assert.InRange(dynamicBody.LinearVelocity.Y, -0.0001f, 0.0001f);
        }

        /// <summary>
        /// Ensures restitution reflects one dynamic sphere away from one static ground box after contact.
        /// </summary>
        [Fact]
        public void Step_WithRestitutionOnDynamicSphereAndStaticGround_BouncesUpwardAfterContact() {
            Entity groundEntity = CreateEntity(new float3(0f, -0.5f, 0f));
            groundEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            groundEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(8f, 1f, 8f),
                Restitution = 1d
            });

            Entity dynamicEntity = CreateEntity(new float3(0f, 0.51f, 0f));
            RigidBody3DComponent dynamicBody = new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = false,
                Mass = 1d,
                LinearVelocity = new float3(0f, -1f, 0f)
            };
            dynamicEntity.AddComponent(dynamicBody);
            dynamicEntity.AddComponent(new SphereCollider3DComponent {
                Radius = 0.5f,
                Restitution = 1d
            });

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(new[] { groundEntity, dynamicEntity });

            world.Step(1.0 / 60.0);

            Assert.True(dynamicBody.LinearVelocity.Y > 0.9f, $"Expected restitution to reverse the vertical velocity, but the final Y velocity was {dynamicBody.LinearVelocity.Y}.");
        }

        /// <summary>
        /// Ensures a dynamic sphere falls under gravity and settles on top of one cooked static mesh floor.
        /// </summary>
        [Fact]
        public void Step_WithDynamicSphereAboveStaticMeshGround_FallsAndResolvesGroundContact() {
            Entity meshEntity = CreateEntity(float3.Zero);
            meshEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            meshEntity.AddComponent(new StaticMeshCollider3DComponent {
                CollisionData = CreateFlatGroundCollisionData()
            });

            Entity dynamicEntity = CreateEntity(new float3(0f, 2f, 0f));
            RigidBody3DComponent dynamicBody = new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = true,
                Mass = 1d
            };
            dynamicEntity.AddComponent(dynamicBody);
            dynamicEntity.AddComponent(new SphereCollider3DComponent {
                Radius = 0.5f
            });

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(new[] { meshEntity, dynamicEntity });

            for (int index = 0; index < 120; index++) {
                world.Step(1.0 / 60.0);
            }

            Assert.True(dynamicEntity.LocalPosition.Y < 2f);
            Assert.InRange(dynamicEntity.LocalPosition.Y, 0.49f, 0.51f);
            Assert.InRange(dynamicBody.LinearVelocity.Y, -0.0001f, 0.0001f);
        }

        /// <summary>
        /// Ensures two overlapping dynamic spheres separate during one simulation step.
        /// </summary>
        [Fact]
        public void Step_WithOverlappingDynamicSpheres_SeparatesTheSphereCenters() {
            Entity firstSphereEntity = CreateEntity(new float3(-0.2f, 0f, 0f));
            firstSphereEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = false,
                Mass = 1d
            });
            firstSphereEntity.AddComponent(new SphereCollider3DComponent {
                Radius = 0.5f
            });

            Entity secondSphereEntity = CreateEntity(new float3(0.2f, 0f, 0f));
            secondSphereEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = false,
                Mass = 1d
            });
            secondSphereEntity.AddComponent(new SphereCollider3DComponent {
                Radius = 0.5f
            });

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(new[] { firstSphereEntity, secondSphereEntity });

            world.Step(1.0 / 60.0);

            float centerDistance = Math.Abs(secondSphereEntity.LocalPosition.X - firstSphereEntity.LocalPosition.X);
            Assert.True(centerDistance >= 0.999f, $"Expected the overlapping spheres to separate to at least one diameter, but their center distance was {centerDistance}.");
        }

        /// <summary>
        /// Ensures friction cancels small tangential motion when one dynamic sphere settles onto one static ground box.
        /// </summary>
        [Fact]
        public void Step_WithHighFrictionOnDynamicSphereAndStaticGround_CancelsSmallTangentialVelocity() {
            Entity groundEntity = CreateEntity(new float3(0f, -0.5f, 0f));
            groundEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            groundEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(8f, 1f, 8f),
                StaticFriction = 1d,
                DynamicFriction = 1d
            });

            Entity dynamicEntity = CreateEntity(new float3(0f, 0.51f, 0f));
            RigidBody3DComponent dynamicBody = new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = false,
                Mass = 1d,
                LinearVelocity = new float3(0.05f, -1f, 0f)
            };
            dynamicEntity.AddComponent(dynamicBody);
            dynamicEntity.AddComponent(new SphereCollider3DComponent {
                Radius = 0.5f,
                StaticFriction = 1d,
                DynamicFriction = 1d
            });

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(new[] { groundEntity, dynamicEntity });

            world.Step(1.0 / 60.0);

            Assert.InRange(dynamicBody.LinearVelocity.X, -0.0001f, 0.0001f);
        }

        /// <summary>
        /// Ensures collision layer filtering prevents one overlapping pair from resolving when the masks do not match.
        /// </summary>
        [Fact]
        public void Step_WithNonMatchingCollisionMasks_IgnoresTheOverlappingPair() {
            Entity firstSphereEntity = CreateEntity(new float3(-0.2f, 0f, 0f));
            firstSphereEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = false,
                Mass = 1d
            });
            firstSphereEntity.AddComponent(new SphereCollider3DComponent {
                Radius = 0.5f,
                CollisionLayer = 0b0000000000000001,
                CollisionMask = 0b0000000000000010
            });

            Entity secondSphereEntity = CreateEntity(new float3(0.2f, 0f, 0f));
            secondSphereEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = false,
                Mass = 1d
            });
            secondSphereEntity.AddComponent(new SphereCollider3DComponent {
                Radius = 0.5f,
                CollisionLayer = 0b0000000000000100,
                CollisionMask = 0b0000000000001000
            });

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(new[] { firstSphereEntity, secondSphereEntity });

            world.Step(1.0 / 60.0);

            float centerDistance = Math.Abs(secondSphereEntity.LocalPosition.X - firstSphereEntity.LocalPosition.X);
            Assert.True(centerDistance < 0.5f, $"Expected collision filtering to keep the non-matching pair unresolved, but their center distance was {centerDistance}.");
        }

        /// <summary>
        /// Ensures a dynamic capsule falls under gravity and settles on top of one static ground box.
        /// </summary>
        [Fact]
        public void Step_WithDynamicCapsuleAboveStaticGround_FallsAndResolvesGroundContact() {
            Entity groundEntity = CreateEntity(new float3(0f, -0.5f, 0f));
            groundEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            groundEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(8f, 1f, 8f)
            });

            Entity dynamicEntity = CreateEntity(new float3(0f, 2.5f, 0f));
            RigidBody3DComponent dynamicBody = new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = true,
                Mass = 1d
            };
            dynamicEntity.AddComponent(dynamicBody);
            dynamicEntity.AddComponent(new CapsuleCollider3DComponent {
                Radius = 0.5f,
                Height = 2f
            });

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(new[] { groundEntity, dynamicEntity });

            for (int index = 0; index < 120; index++) {
                world.Step(1.0 / 60.0);
            }

            Assert.True(dynamicEntity.LocalPosition.Y < 2.5f);
            Assert.InRange(dynamicEntity.LocalPosition.Y, 0.99f, 1.01f);
            Assert.InRange(dynamicBody.LinearVelocity.Y, -0.0001f, 0.0001f);
        }

        /// <summary>
        /// Ensures a dynamic capsule falls under gravity and settles on top of one cooked static mesh floor.
        /// </summary>
        [Fact]
        public void Step_WithDynamicCapsuleAboveStaticMeshGround_FallsAndResolvesGroundContact() {
            Entity meshEntity = CreateEntity(float3.Zero);
            meshEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            meshEntity.AddComponent(new StaticMeshCollider3DComponent {
                CollisionData = CreateFlatGroundCollisionData()
            });

            Entity dynamicEntity = CreateEntity(new float3(0f, 2.5f, 0f));
            RigidBody3DComponent dynamicBody = new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = true,
                Mass = 1d
            };
            dynamicEntity.AddComponent(dynamicBody);
            dynamicEntity.AddComponent(new CapsuleCollider3DComponent {
                Radius = 0.5f,
                Height = 2f
            });

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(new[] { meshEntity, dynamicEntity });

            for (int index = 0; index < 120; index++) {
                world.Step(1.0 / 60.0);
            }

            Assert.True(dynamicEntity.LocalPosition.Y < 2.5f);
            Assert.InRange(dynamicEntity.LocalPosition.Y, 0.99f, 1.01f);
            Assert.InRange(dynamicBody.LinearVelocity.Y, -0.0001f, 0.0001f);
        }

        /// <summary>
        /// Ensures two overlapping dynamic capsules separate during one simulation step.
        /// </summary>
        [Fact]
        public void Step_WithOverlappingDynamicCapsules_SeparatesTheCapsuleCenters() {
            Entity firstCapsuleEntity = CreateEntity(new float3(-0.2f, 0f, 0f));
            firstCapsuleEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = false,
                Mass = 1d
            });
            firstCapsuleEntity.AddComponent(new CapsuleCollider3DComponent {
                Radius = 0.5f,
                Height = 2f
            });

            Entity secondCapsuleEntity = CreateEntity(new float3(0.2f, 0f, 0f));
            secondCapsuleEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = false,
                Mass = 1d
            });
            secondCapsuleEntity.AddComponent(new CapsuleCollider3DComponent {
                Radius = 0.5f,
                Height = 2f
            });

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(new[] { firstCapsuleEntity, secondCapsuleEntity });

            world.Step(1.0 / 60.0);

            float centerDistance = Math.Abs(secondCapsuleEntity.LocalPosition.X - firstCapsuleEntity.LocalPosition.X);
            Assert.True(centerDistance >= 0.999f, $"Expected the overlapping capsules to separate to at least one diameter, but their center distance was {centerDistance}.");
        }

        /// <summary>
        /// Ensures the broadphase limits solver candidate pairs to nearby bodies instead of considering distant static boxes.
        /// </summary>
        [Fact]
        public void Step_WithSeparatedStaticBodies_OnlyProducesNearbyBroadphaseCandidatePairs() {
            Entity nearGroundEntity = CreateEntity(new float3(0f, -0.5f, 0f));
            nearGroundEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            nearGroundEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(8f, 1f, 8f)
            });

            Entity farGroundEntity = CreateEntity(new float3(200f, -0.5f, 0f));
            farGroundEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            farGroundEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(8f, 1f, 8f)
            });

            Entity dynamicEntity = CreateEntity(new float3(0f, 2f, 0f));
            dynamicEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = true,
                Mass = 1d
            });
            dynamicEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(1f, 1f, 1f)
            });

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(new[] { nearGroundEntity, farGroundEntity, dynamicEntity });

            world.Step(1.0 / 60.0);

            Assert.Equal(1, world.LastBroadphaseCandidatePairCount);
        }

        /// <summary>
        /// Ensures a character controller can climb a walkable cooked static mesh ramp using the static mesh support query path.
        /// </summary>
        [Fact]
        public void Step_WithCharacterControllerOnWalkableStaticMeshRamp_ClimbsTheMeshSlope() {
            Entity meshEntity = CreateEntity(float3.Zero);
            meshEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            meshEntity.AddComponent(new StaticMeshCollider3DComponent {
                CollisionData = CreateRampCollisionData()
            });

            Entity controllerEntity = CreateEntity(new float3(-4f, 0.75f, 0f));
            controllerEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(0.9f, 1.5f, 0.9f)
            });
            controllerEntity.AddComponent(new CharacterController3DComponent {
                DesiredMoveDirection = new float3(1f, 0f, 0f),
                MoveSpeed = 3d,
                GravityScale = 1d,
                StepHeight = 0.75d,
                GroundSnapDistance = 0.3d,
                MaximumSlopeDegrees = 45d
            });

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(new[] { meshEntity, controllerEntity });

            for (int index = 0; index < 180; index++) {
                world.Step(1.0 / 60.0);
            }

            Assert.True(controllerEntity.LocalPosition.X > 1.5f, $"Expected the character controller to advance along the static mesh ramp, but its X position was {controllerEntity.LocalPosition.X}.");
            Assert.True(controllerEntity.LocalPosition.Y > 0.9f, $"Expected the character controller to climb the static mesh ramp, but its Y position was {controllerEntity.LocalPosition.Y}.");
        }

        /// <summary>
        /// Ensures trigger colliders emit enter, stay, and exit overlap events without acting as solid contacts.
        /// </summary>
        [Fact]
        public void Step_WithTriggerOverlap_CollectsEnterStayAndExitEventsWithoutSolidResolution() {
            Entity triggerEntity = CreateEntity(float3.Zero);
            triggerEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            triggerEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(2f, 2f, 2f),
                IsTrigger = true
            });

            Entity sphereEntity = CreateEntity(new float3(0f, 0f, 0f));
            sphereEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = false,
                Mass = 1d
            });
            sphereEntity.AddComponent(new SphereCollider3DComponent {
                Radius = 0.5f
            });

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(new[] { triggerEntity, sphereEntity });

            world.Step(1.0 / 60.0);

            Assert.Single(world.TriggerEvents);
            Assert.Equal(TriggerEventKind3D.Enter, world.TriggerEvents[0].Kind);
            Assert.Same(triggerEntity, world.TriggerEvents[0].TriggerEntity);
            Assert.Same(sphereEntity, world.TriggerEvents[0].OtherEntity);
            Assert.InRange(sphereEntity.LocalPosition.X, -0.0001f, 0.0001f);

            world.Step(1.0 / 60.0);

            Assert.Single(world.TriggerEvents);
            Assert.Equal(TriggerEventKind3D.Stay, world.TriggerEvents[0].Kind);

            sphereEntity.LocalPosition = new float3(5f, 0f, 0f);
            world.Step(1.0 / 60.0);

            Assert.Single(world.TriggerEvents);
            Assert.Equal(TriggerEventKind3D.Exit, world.TriggerEvents[0].Kind);
        }

        /// <summary>
        /// Ensures cooked static mesh triggers emit overlap events for primitive rigid bodies without acting as solid contacts.
        /// </summary>
        [Fact]
        public void Step_WithStaticMeshTriggerOverlap_CollectsEnterEventWithoutSolidResolution() {
            Entity triggerMeshEntity = CreateEntity(float3.Zero);
            triggerMeshEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            triggerMeshEntity.AddComponent(new StaticMeshCollider3DComponent {
                CollisionData = CreateFlatGroundCollisionData(),
                IsTrigger = true
            });

            Entity sphereEntity = CreateEntity(new float3(0f, 0.25f, 0f));
            sphereEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = false,
                Mass = 1d
            });
            sphereEntity.AddComponent(new SphereCollider3DComponent {
                Radius = 0.5f
            });

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(new[] { triggerMeshEntity, sphereEntity });

            world.Step(1.0 / 60.0);

            Assert.Single(world.TriggerEvents);
            Assert.Equal(TriggerEventKind3D.Enter, world.TriggerEvents[0].Kind);
            Assert.Same(triggerMeshEntity, world.TriggerEvents[0].TriggerEntity);
            Assert.Same(sphereEntity, world.TriggerEvents[0].OtherEntity);
            Assert.InRange(sphereEntity.LocalPosition.Y, 0.24f, 0.26f);
        }

        /// <summary>
        /// Ensures character controllers collect trigger events against trigger rigid bodies after their movement step completes.
        /// </summary>
        [Fact]
        public void Step_WithCharacterControllerInsideTriggerBody_CollectsEnterEvent() {
            Entity triggerEntity = CreateEntity(float3.Zero);
            triggerEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            triggerEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(2f, 2f, 2f),
                IsTrigger = true
            });

            Entity controllerEntity = CreateEntity(float3.Zero);
            controllerEntity.AddComponent(new CharacterController3DComponent {
                GravityScale = 0d
            });
            controllerEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(1f, 2f, 1f)
            });

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(new[] { triggerEntity, controllerEntity });

            world.Step(1.0 / 60.0);

            Assert.Single(world.TriggerEvents);
            Assert.Equal(TriggerEventKind3D.Enter, world.TriggerEvents[0].Kind);
            Assert.Same(triggerEntity, world.TriggerEvents[0].TriggerEntity);
            Assert.Same(controllerEntity, world.TriggerEvents[0].OtherEntity);
        }

        /// <summary>
        /// Ensures character controllers collect trigger events against cooked static mesh triggers after their movement step completes.
        /// </summary>
        [Fact]
        public void Step_WithCharacterControllerInsideStaticMeshTrigger_CollectsEnterEvent() {
            Entity triggerMeshEntity = CreateEntity(float3.Zero);
            triggerMeshEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            triggerMeshEntity.AddComponent(new StaticMeshCollider3DComponent {
                CollisionData = CreateFlatGroundCollisionData(),
                IsTrigger = true
            });

            Entity controllerEntity = CreateEntity(new float3(0f, 0.49f, 0f));
            controllerEntity.AddComponent(new CharacterController3DComponent {
                GravityScale = 0d
            });
            controllerEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(1f, 1f, 1f)
            });

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(new[] { triggerMeshEntity, controllerEntity });

            world.Step(1.0 / 60.0);

            Assert.Single(world.TriggerEvents);
            Assert.Equal(TriggerEventKind3D.Enter, world.TriggerEvents[0].Kind);
            Assert.Same(triggerMeshEntity, world.TriggerEvents[0].TriggerEntity);
            Assert.Same(controllerEntity, world.TriggerEvents[0].OtherEntity);
        }

        /// <summary>
        /// Creates one initialized entity suitable for scene-free physics tests.
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

        /// <summary>
        /// Creates one simple walkable ramp collision blob represented by two triangles.
        /// </summary>
        /// <returns>Cooked static mesh collision data for a walkable ramp.</returns>
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
        /// Creates one simple flat cooked floor represented by two triangles.
        /// </summary>
        /// <returns>Cooked static mesh collision data for one flat floor.</returns>
        static StaticMeshCollisionData3D CreateFlatGroundCollisionData() {
            return new StaticMeshCollisionData3D(
                new[] {
                    new float3(-6f, 0f, -6f),
                    new float3(6f, 0f, -6f),
                    new float3(6f, 0f, 6f),
                    new float3(-6f, 0f, 6f)
                },
                new[] {
                    0, 2, 1,
                    0, 3, 2
                });
        }
    }
}
