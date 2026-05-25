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
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
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
        /// Ensures tangential contact friction applies rotational motion to a dynamic box.
        /// </summary>
        [Fact]
        public void Step_WithSlidingDynamicBoxOnHighFrictionGround_RotatesFromContactTorque() {
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
                LinearVelocity = new float3(2f, -1f, 0f)
            };
            dynamicEntity.AddComponent(dynamicBody);
            dynamicEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(1f, 1f, 1f),
                StaticFriction = 1d,
                DynamicFriction = 1d
            });

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(new[] { groundEntity, dynamicEntity });

            for (int index = 0; index < 5; index++) {
                world.Step(1.0 / 60.0);
            }

            Assert.NotEqual(float4.Identity, dynamicEntity.LocalOrientation);
        }

        /// <summary>
        /// Ensures a rotated dynamic box resolves against its rotated support point instead of sinking to its unrotated half-height.
        /// </summary>
        [Fact]
        public void Step_WithRotatedDynamicBoxOnStaticGround_UsesOrientedBoxSupport() {
            Entity groundEntity = CreateEntity(new float3(0f, -0.5f, 0f));
            groundEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            groundEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(8f, 1f, 8f)
            });

            float4.CreateFromYawPitchRoll(0f, 0f, (float)(50d * Math.PI / 180d), out float4 initialOrientation);
            Entity dynamicEntity = CreateEntity(new float3(0f, 0.51f, 0f));
            dynamicEntity.LocalOrientation = initialOrientation;
            RigidBody3DComponent dynamicBody = new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = false,
                Mass = 1d,
                LinearVelocity = new float3(0f, -1f, 0f)
            };
            dynamicEntity.AddComponent(dynamicBody);
            dynamicEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(1f, 1f, 1f)
            });

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(new[] { groundEntity, dynamicEntity });

            world.Step(1.0 / 60.0);

            Assert.True(dynamicEntity.LocalPosition.Y > 0.68f, $"Expected rotated cube support to keep the visual cube above the floor, but center Y was {dynamicEntity.LocalPosition.Y}.");
            Assert.NotEqual(float3.Zero, dynamicBody.AngularVelocity);
        }

        /// <summary>
        /// Ensures a tilted default dynamic box damps contact rocking instead of continuing to hop for several seconds.
        /// </summary>
        [Fact]
        public void Step_WithRotatedDynamicBoxUsingDefaultMaterials_DampsRockingAfterContact() {
            Entity groundEntity = CreateEntity(new float3(0f, -0.5f, 0f));
            groundEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            groundEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(8f, 1f, 8f)
            });

            float4.CreateFromYawPitchRoll(0f, 0f, (float)(50d * Math.PI / 180d), out float4 initialOrientation);
            Entity dynamicEntity = CreateEntity(new float3(0f, 4f, 0f));
            dynamicEntity.LocalOrientation = initialOrientation;
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

            for (int index = 0; index < 240; index++) {
                world.Step(1.0 / 60.0);
            }

            double angularSpeedSquared =
                (dynamicBody.AngularVelocity.X * dynamicBody.AngularVelocity.X) +
                (dynamicBody.AngularVelocity.Y * dynamicBody.AngularVelocity.Y) +
                (dynamicBody.AngularVelocity.Z * dynamicBody.AngularVelocity.Z);
            Assert.InRange(dynamicBody.LinearVelocity.Y, -0.05f, 0.05f);
            Assert.True(angularSpeedSquared < 0.01d, $"Expected angular motion to damp below 0.1 rad/s, but angular velocity was {dynamicBody.AngularVelocity}.");
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
        /// Ensures the authored eight-sphere tower keeps falling near the BEPU reference instead of freezing after the first dynamic contact.
        /// </summary>
        [Fact]
        public void Step_WithCityEightSphereTower_DoesNotFreezeTopSphereOnFirstDynamicContact() {
            Entity groundEntity = CreateEntity(new float3(0f, -0.5f, 0f));
            groundEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            groundEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(16f, 1f, 14f)
            });

            Entity[] spheres = new Entity[8];
            RigidBody3DComponent[] bodies = new RigidBody3DComponent[spheres.Length];
            for (int sphereIndex = 0; sphereIndex < spheres.Length; sphereIndex++) {
                float staggerX = sphereIndex % 2 == 0 ? 0f : 0.08f;
                float staggerZ = sphereIndex % 3 == 0 ? -0.06f : 0.06f;
                bodies[sphereIndex] = CreateDynamicBody();
                spheres[sphereIndex] = CreateDynamicSphereEntity(new float3(staggerX, 0.5f + sphereIndex, staggerZ), bodies[sphereIndex]);
            }

            Entity[] rootEntities = new Entity[spheres.Length + 1];
            rootEntities[0] = groundEntity;
            for (int index = 0; index < spheres.Length; index++) {
                rootEntities[index + 1] = spheres[index];
            }

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(rootEntities);

            for (int stepIndex = 0; stepIndex < 8; stepIndex++) {
                world.Step(1.0 / 60.0);
            }

            Assert.True(bodies[7].LinearVelocity.Y < -0.5f, $"Expected the top sphere to keep falling like BEPU after early dynamic contact, but its velocity was {bodies[7].LinearVelocity}.");
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
            Assert.NotEqual(float3.Zero, dynamicBody.AngularVelocity);
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
        /// Ensures separated dynamic boxes fall into a stable vertical stack without sharing the same physical volume.
        /// </summary>
        [Fact]
        public void Step_WithSeparatedDynamicBoxesAboveStaticGround_SettlesWithoutVerticalOverlap() {
            Entity groundEntity = CreateEntity(new float3(0f, -0.5f, 0f));
            groundEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            groundEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(14f, 1f, 14f)
            });

            Entity firstBoxEntity = CreateDynamicBoxEntity(new float3(0f, 4f, 0f));
            Entity secondBoxEntity = CreateDynamicBoxEntity(new float3(0f, 5.75f, 0f));
            Entity thirdBoxEntity = CreateDynamicBoxEntity(new float3(0f, 7.5f, 0f));
            Entity fourthBoxEntity = CreateDynamicBoxEntity(new float3(0f, 9.25f, 0f));

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(new[] {
                groundEntity,
                firstBoxEntity,
                secondBoxEntity,
                thirdBoxEntity,
                fourthBoxEntity
            });

            for (int index = 0; index < 360; index++) {
                world.Step(1.0 / 60.0);
            }

            Assert.True(firstBoxEntity.LocalPosition.Y >= 0.49f, $"Expected the first box to stay above the ground plane, but position was {firstBoxEntity.LocalPosition}.");
            AssertNoUnitBoxOverlaps(new[] {
                firstBoxEntity,
                secondBoxEntity,
                thirdBoxEntity,
                fourthBoxEntity
            });
        }

        /// <summary>
        /// Ensures a small resting support impulse does not keep adding angular velocity to a stable two-box stack forever.
        /// </summary>
        [Fact]
        public void Step_WithStableTwoBoxStack_DampsRestingAngularMotion() {
            Entity groundEntity = CreateEntity(new float3(0f, -0.5f, 0f));
            groundEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            groundEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(14f, 1f, 14f)
            });

            RigidBody3DComponent firstBody = CreateDynamicBody();
            Entity firstBoxEntity = CreateDynamicBoxEntity(new float3(-0.34f, 0.6f, -0.06f), firstBody);
            RigidBody3DComponent secondBody = CreateDynamicBody();
            Entity secondBoxEntity = CreateDynamicBoxEntity(new float3(-0.34f, 1.72f, -0.06f), secondBody);

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(new[] {
                groundEntity,
                firstBoxEntity,
                secondBoxEntity
            });

            for (int index = 0; index < 720; index++) {
                world.Step(1.0 / 60.0);
            }

            Assert.True(ResolveAngularSpeedSquared(firstBody) < 0.01d, $"Expected the lower box to stop spinning, but angular velocity was {firstBody.AngularVelocity}.");
            Assert.True(ResolveAngularSpeedSquared(secondBody) < 0.01d, $"Expected the upper box to stop spinning, but angular velocity was {secondBody.AngularVelocity}.");
            AssertNoUnitBoxOverlaps(new[] {
                firstBoxEntity,
                secondBoxEntity
            });
        }

        /// <summary>
        /// Ensures a dynamic box supported only near one bottom edge receives enough normal torque to tip off the support.
        /// </summary>
        [Fact]
        public void Step_WithEdgeSupportedBox_StartsTippingAroundTheSupportEdge() {
            Entity supportEntity = CreateEntity(new float3(-0.34f, 0.5f, -0.06f));
            supportEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            supportEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(1f, 1f, 1f)
            });

            RigidBody3DComponent dynamicBody = CreateDynamicBody();
            Entity dynamicEntity = CreateDynamicBoxEntity(new float3(0.50f, 1.62f, 0.06f), dynamicBody);

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(new[] {
                supportEntity,
                dynamicEntity
            });

            for (int index = 0; index < 180; index++) {
                world.Step(1.0 / 60.0);
            }

            double uprightAlignment = ResolveUprightAlignment(dynamicEntity);
            Assert.True(uprightAlignment < 0.98d, $"Expected the edge-supported box to tip instead of staying upright, but its up alignment was {uprightAlignment}, angular velocity was {dynamicBody.AngularVelocity}, and position was {dynamicEntity.LocalPosition}.");
        }

        /// <summary>
        /// Ensures a cube whose center of mass is outside a narrow support patch begins tipping promptly instead of hovering in a slow balanced state.
        /// </summary>
        [Fact]
        public void Step_WithMostlyUnsupportedOffsetBox_TipsPromptly() {
            Entity supportEntity = CreateEntity(new float3(0f, 0.5f, 0f));
            supportEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            supportEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(1f, 1f, 1f)
            });

            RigidBody3DComponent dynamicBody = CreateDynamicBody();
            Entity dynamicEntity = CreateDynamicBoxEntity(new float3(0.9f, 1.52f, 0f), dynamicBody);

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(new[] {
                supportEntity,
                dynamicEntity
            });

            for (int index = 0; index < 90; index++) {
                world.Step(1.0 / 60.0);
            }

            double uprightAlignment = ResolveUprightAlignment(dynamicEntity);
            Assert.True(uprightAlignment < 0.95d, $"Expected the unsupported cube to tip promptly, but its up alignment was {uprightAlignment}, angular velocity was {dynamicBody.AngularVelocity}, and position was {dynamicEntity.LocalPosition}.");
        }

        /// <summary>
        /// Ensures the authored stacked-box demo setup tips the offset upper cube and then lets it settle instead of spinning forever.
        /// </summary>
        [Fact]
        public void Step_WithCityOffsetStackedBoxScene_SettlesTheGreenCubeAfterFall() {
            Entity groundEntity = CreateEntity(new float3(0f, -0.5f, 0f));
            groundEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            groundEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(14f, 1f, 14f),
                StaticFriction = 1d,
                DynamicFriction = 1d
            });

            RigidBody3DComponent lowerBody = CreateDynamicBody();
            Entity lowerBoxEntity = CreateDynamicBoxEntity(new float3(0f, 1f, 0f), lowerBody);
            SetBoxFriction(lowerBoxEntity, 1d, 1d);
            RigidBody3DComponent upperBody = CreateDynamicBody();
            Entity upperBoxEntity = CreateDynamicBoxEntity(new float3(0.9f, 3f, 0f), upperBody);
            SetBoxFriction(upperBoxEntity, 1d, 1d);

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(new[] {
                groundEntity,
                lowerBoxEntity,
                upperBoxEntity
            });

            for (int index = 0; index < 1200; index++) {
                world.Step(1.0 / 60.0);
            }

            Assert.InRange(upperBoxEntity.LocalPosition.Y, 0.45f, 2.1f);
            Assert.True(ResolveAngularSpeedSquared(upperBody) < 100d, $"Expected the offset upper cube to avoid runaway rotation after settling, but angular velocity was {upperBody.AngularVelocity}, position was {upperBoxEntity.LocalPosition}, and orientation was {upperBoxEntity.LocalOrientation}.");
        }

        /// <summary>
        /// Ensures the first offset-box stack contact does not inject a large horizontal launch impulse.
        /// </summary>
        [Fact]
        public void Step_WithCityOffsetStackedBoxScene_KeepsInitialContactImpulseNearReference() {
            Entity groundEntity = CreateEntity(new float3(0f, -0.5f, 0f));
            groundEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            groundEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(14f, 1f, 14f),
                StaticFriction = 1d,
                DynamicFriction = 1d
            });

            Entity lowerBoxEntity = CreateDynamicBoxEntity(new float3(0f, 1f, 0f), CreateDynamicBody());
            SetBoxFriction(lowerBoxEntity, 1d, 1d);
            RigidBody3DComponent upperBody = CreateDynamicBody();
            Entity upperBoxEntity = CreateDynamicBoxEntity(new float3(0.9f, 3f, 0f), upperBody);
            SetBoxFriction(upperBoxEntity, 1d, 1d);

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(new[] {
                groundEntity,
                lowerBoxEntity,
                upperBoxEntity
            });

            for (int index = 0; index < 33; index++) {
                world.Step(1.0 / 60.0);
            }

            Assert.InRange(upperBody.LinearVelocity.X, 0f, 0.5f);
            Assert.InRange(upperBoxEntity.LocalPosition.Y, 1.45f, 1.52f);
        }

        /// <summary>
        /// Ensures the authored eight-box tower never finishes with two solid cube volumes occupying the same space.
        /// </summary>
        [Fact]
        public void Step_WithCityEightBoxTower_SettlesWithoutAnyBoxBoxOverlap() {
            Entity groundEntity = CreateEntity(new float3(0f, -0.5f, 0f));
            groundEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            groundEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(18f, 1f, 18f),
                StaticFriction = 1d,
                DynamicFriction = 1d
            });

            Entity[] boxes = new[] {
                CreateDynamicBoxEntity(new float3(0f, 1f, 0f)),
                CreateDynamicBoxEntity(new float3(0.9f, 3f, 0f)),
                CreateDynamicBoxEntity(new float3(-0.45f, 5f, 0f)),
                CreateDynamicBoxEntity(new float3(0.45f, 7f, 0f)),
                CreateDynamicBoxEntity(new float3(-0.25f, 9f, 0f)),
                CreateDynamicBoxEntity(new float3(0.25f, 11f, 0f)),
                CreateDynamicBoxEntity(new float3(-0.1f, 13f, 0f)),
                CreateDynamicBoxEntity(new float3(0.1f, 15f, 0f))
            };

            for (int index = 0; index < boxes.Length; index++) {
                SetBoxFriction(boxes[index], 1d, 1d);
            }

            Entity[] rootEntities = new Entity[boxes.Length + 1];
            rootEntities[0] = groundEntity;
            for (int index = 0; index < boxes.Length; index++) {
                rootEntities[index + 1] = boxes[index];
            }

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(rootEntities);

            for (int index = 0; index < 1200; index++) {
                world.Step(1.0 / 60.0);
            }

            AssertNoUnitBoxOverlaps(boxes);
        }

        /// <summary>
        /// Ensures the authored eight-box tower advances continuously instead of hiding large post-pose projection jumps.
        /// </summary>
        [Fact]
        public void Step_WithCityEightBoxTower_DoesNotApplyLargePoseJumps() {
            Entity groundEntity = CreateEntity(new float3(0f, -0.5f, 0f));
            groundEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            groundEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(18f, 1f, 18f),
                StaticFriction = 1d,
                DynamicFriction = 1d
            });

            Entity[] boxes = new[] {
                CreateDynamicBoxEntity(new float3(0f, 1f, 0f)),
                CreateDynamicBoxEntity(new float3(0.9f, 3f, 0f)),
                CreateDynamicBoxEntity(new float3(-0.45f, 5f, 0f)),
                CreateDynamicBoxEntity(new float3(0.45f, 7f, 0f)),
                CreateDynamicBoxEntity(new float3(-0.25f, 9f, 0f)),
                CreateDynamicBoxEntity(new float3(0.25f, 11f, 0f)),
                CreateDynamicBoxEntity(new float3(-0.1f, 13f, 0f)),
                CreateDynamicBoxEntity(new float3(0.1f, 15f, 0f))
            };

            for (int index = 0; index < boxes.Length; index++) {
                SetBoxFriction(boxes[index], 1d, 1d);
            }

            Entity[] rootEntities = new Entity[boxes.Length + 1];
            rootEntities[0] = groundEntity;
            for (int index = 0; index < boxes.Length; index++) {
                rootEntities[index + 1] = boxes[index];
            }

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(rootEntities);
            float3[] previousPositions = CapturePositions(boxes);
            double largestStepDistance = 0d;

            for (int stepIndex = 0; stepIndex < 600; stepIndex++) {
                world.Step(1.0 / 60.0);
                for (int boxIndex = 0; boxIndex < boxes.Length; boxIndex++) {
                    float3 currentPosition = boxes[boxIndex].LocalPosition;
                    float3 previousPosition = previousPositions[boxIndex];
                    double deltaX = currentPosition.X - previousPosition.X;
                    double deltaY = currentPosition.Y - previousPosition.Y;
                    double deltaZ = currentPosition.Z - previousPosition.Z;
                    double stepDistance = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ));
                    largestStepDistance = Math.Max(largestStepDistance, stepDistance);
                    previousPositions[boxIndex] = currentPosition;
                }
            }

            Assert.True(largestStepDistance <= 0.36d, $"Expected the tower to move continuously, but the largest one-step displacement was {largestStepDistance}.");
        }

        /// <summary>
        /// Ensures the authored eight-box tower does not inject runaway angular energy while fallback contacts are active.
        /// </summary>
        [Fact]
        public void Step_WithCityEightBoxTower_DoesNotCreateRunawayAngularVelocity() {
            Entity groundEntity = CreateEntity(new float3(0f, -0.5f, 0f));
            groundEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            groundEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(18f, 1f, 18f),
                StaticFriction = 1d,
                DynamicFriction = 1d
            });

            RigidBody3DComponent[] bodies = new[] {
                CreateDynamicBody(),
                CreateDynamicBody(),
                CreateDynamicBody(),
                CreateDynamicBody(),
                CreateDynamicBody(),
                CreateDynamicBody(),
                CreateDynamicBody(),
                CreateDynamicBody()
            };
            Entity[] boxes = new[] {
                CreateDynamicBoxEntity(new float3(0f, 1f, 0f), bodies[0]),
                CreateDynamicBoxEntity(new float3(0.9f, 3f, 0f), bodies[1]),
                CreateDynamicBoxEntity(new float3(-0.45f, 5f, 0f), bodies[2]),
                CreateDynamicBoxEntity(new float3(0.45f, 7f, 0f), bodies[3]),
                CreateDynamicBoxEntity(new float3(-0.25f, 9f, 0f), bodies[4]),
                CreateDynamicBoxEntity(new float3(0.25f, 11f, 0f), bodies[5]),
                CreateDynamicBoxEntity(new float3(-0.1f, 13f, 0f), bodies[6]),
                CreateDynamicBoxEntity(new float3(0.1f, 15f, 0f), bodies[7])
            };

            for (int index = 0; index < boxes.Length; index++) {
                SetBoxFriction(boxes[index], 1d, 1d);
            }

            Entity[] rootEntities = new Entity[boxes.Length + 1];
            rootEntities[0] = groundEntity;
            for (int index = 0; index < boxes.Length; index++) {
                rootEntities[index + 1] = boxes[index];
            }

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(rootEntities);
            double largestAngularSpeedSquared = 0d;

            for (int stepIndex = 0; stepIndex < 1200; stepIndex++) {
                world.Step(1.0 / 60.0);
                for (int bodyIndex = 0; bodyIndex < bodies.Length; bodyIndex++) {
                    largestAngularSpeedSquared = Math.Max(largestAngularSpeedSquared, ResolveAngularSpeedSquared(bodies[bodyIndex]));
                }
            }

            Assert.True(largestAngularSpeedSquared <= 169d, $"Expected tower angular speed to remain bounded, but peak squared angular speed was {largestAngularSpeedSquared}.");
            for (int bodyIndex = 0; bodyIndex < bodies.Length; bodyIndex++) {
                Assert.True(ResolveAngularSpeedSquared(bodies[bodyIndex]) <= 1d, $"Expected box {bodyIndex + 1} to settle without visible end-state spin, but angular velocity was {bodies[bodyIndex].AngularVelocity}.");
            }
        }

        /// <summary>
        /// Ensures the authored eight-box tower collapses like the BEPU reference instead of preserving an unstable vertical stack.
        /// </summary>
        [Fact]
        public void Step_WithCityEightBoxTower_CollapsesUnstableStackNearBepuTopology() {
            Entity groundEntity = CreateEntity(new float3(0f, -0.5f, 0f));
            groundEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            groundEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(18f, 1f, 18f),
                StaticFriction = 1d,
                DynamicFriction = 1d
            });

            Entity[] boxes = new[] {
                CreateDynamicBoxEntity(new float3(0f, 1f, 0f)),
                CreateDynamicBoxEntity(new float3(0.9f, 3f, 0f)),
                CreateDynamicBoxEntity(new float3(-0.45f, 5f, 0f)),
                CreateDynamicBoxEntity(new float3(0.45f, 7f, 0f)),
                CreateDynamicBoxEntity(new float3(-0.25f, 9f, 0f)),
                CreateDynamicBoxEntity(new float3(0.25f, 11f, 0f)),
                CreateDynamicBoxEntity(new float3(-0.1f, 13f, 0f)),
                CreateDynamicBoxEntity(new float3(0.1f, 15f, 0f))
            };

            for (int index = 0; index < boxes.Length; index++) {
                SetBoxFriction(boxes[index], 1d, 1d);
            }

            Entity[] rootEntities = new Entity[boxes.Length + 1];
            rootEntities[0] = groundEntity;
            for (int index = 0; index < boxes.Length; index++) {
                rootEntities[index + 1] = boxes[index];
            }

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(rootEntities);

            for (int stepIndex = 0; stepIndex < 1200; stepIndex++) {
                world.Step(1.0 / 60.0);
            }

            int highBoxCount = 0;
            for (int index = 0; index < boxes.Length; index++) {
                if (boxes[index].LocalPosition.Y > 2.25f) {
                    highBoxCount++;
                }
            }

            Assert.True(highBoxCount <= 2, $"Expected the BEPU reference collapse topology to leave at most two boxes above the low pile, but {highBoxCount} boxes were still high.");
            Assert.True(boxes[7].LocalPosition.Y < 3.25f, $"Expected the top box to fall near the BEPU reference, but it ended at {boxes[7].LocalPosition}.");
        }

        /// <summary>
        /// Ensures a tilted dynamic box still resolves against another dynamic box instead of bypassing dynamic-dynamic contact.
        /// </summary>
        [Fact]
        public void Step_WithTiltedDynamicBoxes_SeparatesTheBoxVolumes() {
            float4.CreateFromYawPitchRoll(0f, 0f, (float)(35d * Math.PI / 180d), out float4 tiltedOrientation);
            Entity firstBoxEntity = CreateDynamicBoxEntity(new float3(0f, 0f, 0f));
            firstBoxEntity.LocalOrientation = tiltedOrientation;
            Entity secondBoxEntity = CreateDynamicBoxEntity(new float3(0.35f, 0.1f, 0f));

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(new[] {
                firstBoxEntity,
                secondBoxEntity
            });

            float3[] previousPositions = CapturePositions(new[] {
                firstBoxEntity,
                secondBoxEntity
            });
            double largestStepDistance = 0d;
            for (int stepIndex = 0; stepIndex < 60; stepIndex++) {
                world.Step(1.0 / 60.0);
                Entity[] boxes = new[] {
                    firstBoxEntity,
                    secondBoxEntity
                };
                for (int boxIndex = 0; boxIndex < boxes.Length; boxIndex++) {
                    float3 currentPosition = boxes[boxIndex].LocalPosition;
                    float3 previousPosition = previousPositions[boxIndex];
                    double deltaX = currentPosition.X - previousPosition.X;
                    double deltaY = currentPosition.Y - previousPosition.Y;
                    double deltaZ = currentPosition.Z - previousPosition.Z;
                    double stepDistance = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ));
                    largestStepDistance = Math.Max(largestStepDistance, stepDistance);
                    previousPositions[boxIndex] = currentPosition;
                }
            }

            AssertNoUnitBoxOverlaps(new[] {
                firstBoxEntity,
                secondBoxEntity
            });
            Assert.True(largestStepDistance <= 0.5d, $"Expected tilted overlap recovery to stay bounded, but the largest one-step displacement was {largestStepDistance}.");
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
        /// Creates one initialized dynamic unit box suitable for stack-settling physics tests.
        /// </summary>
        /// <param name="localPosition">Initial local position.</param>
        /// <returns>Initialized dynamic box entity.</returns>
        static Entity CreateDynamicBoxEntity(float3 localPosition) {
            return CreateDynamicBoxEntity(localPosition, CreateDynamicBody());
        }

        /// <summary>
        /// Creates one initialized dynamic unit box around an already retained rigid-body component reference.
        /// </summary>
        /// <param name="localPosition">Initial local position.</param>
        /// <param name="body">Rigid body component that should drive the box.</param>
        /// <returns>Initialized dynamic box entity.</returns>
        static Entity CreateDynamicBoxEntity(float3 localPosition, RigidBody3DComponent body) {
            if (body == null) {
                throw new ArgumentNullException(nameof(body));
            }

            Entity entity = CreateEntity(localPosition);
            entity.AddComponent(body);
            entity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(1f, 1f, 1f)
            });
            return entity;
        }

        /// <summary>
        /// Creates one initialized dynamic sphere around an already retained rigid-body component reference.
        /// </summary>
        /// <param name="localPosition">Initial local position.</param>
        /// <param name="body">Rigid body component that should drive the sphere.</param>
        /// <returns>Initialized dynamic sphere entity.</returns>
        static Entity CreateDynamicSphereEntity(float3 localPosition, RigidBody3DComponent body) {
            if (body == null) {
                throw new ArgumentNullException(nameof(body));
            }

            Entity entity = CreateEntity(localPosition);
            entity.AddComponent(body);
            entity.AddComponent(new SphereCollider3DComponent {
                Radius = 0.5f
            });
            return entity;
        }

        /// <summary>
        /// Applies explicit friction values to a dynamic box test entity so scene-specific material behavior is not hidden behind defaults.
        /// </summary>
        /// <param name="entity">Entity containing the box collider.</param>
        /// <param name="staticFriction">Static friction coefficient to assign.</param>
        /// <param name="dynamicFriction">Dynamic friction coefficient to assign.</param>
        static void SetBoxFriction(Entity entity, double staticFriction, double dynamicFriction) {
            for (int index = 0; index < entity.Components.Count; index++) {
                if (entity.Components[index] is BoxCollider3DComponent collider) {
                    collider.StaticFriction = staticFriction;
                    collider.DynamicFriction = dynamicFriction;
                    return;
                }
            }

            throw new InvalidOperationException("Entity does not contain a box collider component.");
        }

        /// <summary>
        /// Creates one default dynamic rigid body used by scene-free box simulation tests.
        /// </summary>
        /// <returns>Dynamic rigid body configured with gravity and unit mass.</returns>
        static RigidBody3DComponent CreateDynamicBody() {
            return new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = true,
                Mass = 1d
            };
        }

        /// <summary>
        /// Computes the squared angular speed for one rigid body without requiring a vector helper allocation.
        /// </summary>
        /// <param name="body">Rigid body whose angular speed should be measured.</param>
        /// <returns>Squared angular speed in radians squared per second squared.</returns>
        static double ResolveAngularSpeedSquared(RigidBody3DComponent body) {
            if (body == null) {
                throw new ArgumentNullException(nameof(body));
            }

            return (body.AngularVelocity.X * body.AngularVelocity.X) +
                (body.AngularVelocity.Y * body.AngularVelocity.Y) +
                (body.AngularVelocity.Z * body.AngularVelocity.Z);
        }

        /// <summary>
        /// Measures how closely one entity's local up axis matches world up after simulation.
        /// </summary>
        /// <param name="entity">Entity whose orientation should be measured.</param>
        /// <returns>Dot product between the entity up axis and world up.</returns>
        static double ResolveUprightAlignment(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            float3 up = float4.RotateVector(new float3(0f, 1f, 0f), entity.LocalOrientation);
            return up.Y;
        }

        /// <summary>
        /// Verifies every unit box pair is separated along at least one axis after simulation settles.
        /// </summary>
        /// <param name="boxes">Dynamic unit boxes to compare.</param>
        static void AssertNoUnitBoxOverlaps(IReadOnlyList<Entity> boxes) {
            if (boxes == null) {
                throw new ArgumentNullException(nameof(boxes));
            }

            for (int firstIndex = 0; firstIndex < boxes.Count; firstIndex++) {
                for (int secondIndex = firstIndex + 1; secondIndex < boxes.Count; secondIndex++) {
                    float3 firstPosition = boxes[firstIndex].LocalPosition;
                    float3 secondPosition = boxes[secondIndex].LocalPosition;
                    bool separated =
                        Math.Abs(firstPosition.X - secondPosition.X) >= 0.999f ||
                        Math.Abs(firstPosition.Y - secondPosition.Y) >= 0.999f ||
                        Math.Abs(firstPosition.Z - secondPosition.Z) >= 0.999f;
                    Assert.True(
                        separated,
                        $"Expected boxes {firstIndex + 1} and {secondIndex + 1} to be separated, but their centers were {firstPosition} and {secondPosition}.");
                }
            }
        }

        /// <summary>
        /// Captures the current local positions for a set of entities so step-to-step displacement can be measured.
        /// </summary>
        /// <param name="entities">Entities whose positions should be captured.</param>
        /// <returns>Position snapshot in entity order.</returns>
        static float3[] CapturePositions(IReadOnlyList<Entity> entities) {
            if (entities == null) {
                throw new ArgumentNullException(nameof(entities));
            }

            float3[] positions = new float3[entities.Count];
            for (int index = 0; index < entities.Count; index++) {
                positions[index] = entities[index].LocalPosition;
            }

            return positions;
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
