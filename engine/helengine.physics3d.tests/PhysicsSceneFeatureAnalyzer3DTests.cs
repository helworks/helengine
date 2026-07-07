namespace helengine.physics3d.tests {
    /// <summary>
    /// Verifies scene feature analysis reports the compact 3D physics interaction set required by one authored scene.
    /// </summary>
    [Collection(Physics3DTestCollection.Name)]
    public sealed class PhysicsSceneFeatureAnalyzer3DTests : IDisposable {
        /// <summary>
        /// Initializes the minimal core services required for entity-backed feature-analysis tests.
        /// </summary>
        public PhysicsSceneFeatureAnalyzer3DTests() {
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
        /// Ensures a dynamic sphere above one cooked mesh reports only the sphere-static-mesh interaction path.
        /// </summary>
        [Fact]
        public void Analyze_WithDynamicSphereAndStaticMesh_ReportsSphereStaticMeshFeature() {
            Entity meshEntity = CreateEntity(float3.Zero);
            meshEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            meshEntity.AddComponent(new StaticMeshCollider3DComponent {
                CollisionData = CreateFlatGroundCollisionData()
            });

            Entity sphereEntity = CreateEntity(new float3(0f, 2f, 0f));
            sphereEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = true,
                Mass = 1d
            });
            sphereEntity.AddComponent(new SphereCollider3DComponent {
                Radius = 0.5f
            });

            PhysicsSceneFeatureFlags3D features = PhysicsSceneFeatureAnalyzer3D.Analyze(new[] { meshEntity, sphereEntity });

            Assert.True((features & PhysicsSceneFeatureFlags3D.SphereStaticMeshContact) != 0);
            Assert.True((features & PhysicsSceneFeatureFlags3D.BoxStaticMeshContact) == 0);
            Assert.True((features & PhysicsSceneFeatureFlags3D.CapsuleStaticMeshContact) == 0);
            Assert.True((features & PhysicsSceneFeatureFlags3D.CharacterController) == 0);
        }

        /// <summary>
        /// Ensures a mixed authored scene reports the expected primitive, character-controller, trigger, and kinematic features.
        /// </summary>
        [Fact]
        public void Analyze_WithMixedScene_ReportsExpectedFeatureFlags() {
            Entity staticBoxEntity = CreateEntity(float3.Zero);
            staticBoxEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            staticBoxEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(4f, 1f, 4f)
            });

            Entity dynamicBoxEntity = CreateEntity(new float3(0f, 2f, 0f));
            dynamicBoxEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = true,
                Mass = 1d
            });
            dynamicBoxEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(1f, 1f, 1f)
            });

            Entity dynamicSphereEntity = CreateEntity(new float3(2f, 2f, 0f));
            dynamicSphereEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = true,
                Mass = 1d
            });
            dynamicSphereEntity.AddComponent(new SphereCollider3DComponent {
                Radius = 0.5f
            });

            Entity kinematicCapsuleEntity = CreateEntity(new float3(-2f, 2f, 0f));
            kinematicCapsuleEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Kinematic,
                UseGravity = false
            });
            kinematicCapsuleEntity.AddComponent(new CapsuleCollider3DComponent {
                Radius = 0.5f,
                Height = 2f
            });
            kinematicCapsuleEntity.AddComponent(new KinematicMotion3DComponent {
                StartLocalPosition = new float3(-2f, 2f, 0f),
                EndLocalPosition = new float3(2f, 2f, 0f),
                TravelDurationSeconds = 1d,
                PingPong = true
            });

            Entity triggerEntity = CreateEntity(new float3(0f, 1f, 2f));
            triggerEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            triggerEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(2f, 2f, 2f),
                IsTrigger = true
            });

            Entity controllerEntity = CreateEntity(new float3(0f, 1f, -2f));
            controllerEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(1f, 2f, 1f)
            });
            controllerEntity.AddComponent(new CharacterController3DComponent {
                DesiredMoveDirection = new float3(1f, 0f, 0f),
                MoveSpeed = 3d
            });

            PhysicsSceneFeatureFlags3D features = PhysicsSceneFeatureAnalyzer3D.Analyze(new[] {
                staticBoxEntity,
                dynamicBoxEntity,
                dynamicSphereEntity,
                kinematicCapsuleEntity,
                triggerEntity,
                controllerEntity
            });

            Assert.True((features & PhysicsSceneFeatureFlags3D.BoxBoxContact) != 0);
            Assert.True((features & PhysicsSceneFeatureFlags3D.SphereBoxContact) != 0);
            Assert.True((features & PhysicsSceneFeatureFlags3D.CapsuleBoxContact) != 0);
            Assert.True((features & PhysicsSceneFeatureFlags3D.CapsuleSphereContact) != 0);
            Assert.True((features & PhysicsSceneFeatureFlags3D.KinematicMotion) != 0);
            Assert.True((features & PhysicsSceneFeatureFlags3D.TriggerEvents) != 0);
            Assert.True((features & PhysicsSceneFeatureFlags3D.CharacterController) != 0);
            Assert.True((features & PhysicsSceneFeatureFlags3D.CharacterControllerBodySupport) != 0);
            Assert.True((features & PhysicsSceneFeatureFlags3D.CharacterControllerStaticMeshSupport) == 0);
            Assert.True((features & PhysicsSceneFeatureFlags3D.SphereStaticMeshContact) == 0);
        }

        /// <summary>
        /// Ensures serialized scene records can be analyzed without materializing runtime entities first.
        /// </summary>
        [Fact]
        public void Analyze_WithSerializedSceneAsset_ReportsExpectedFeatureFlags() {
            SceneAsset sceneAsset = new SceneAsset {
                Id = "Scenes/PhysicsSerialized.helen",
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1,
                        Name = "Ground",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateRigidBodyRecord(BodyKind3D.Static, false),
                            CreateBoxColliderRecord(new float3(8f, 1f, 8f), false)
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = 2,
                        Name = "DynamicSphere",
                        LocalPosition = new float3(0f, 2f, 0f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateRigidBodyRecord(BodyKind3D.Dynamic, true),
                            CreateSphereColliderRecord(0.5f)
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = 3,
                        Name = "TriggerVolume",
                        LocalPosition = new float3(0f, 1f, 2f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateRigidBodyRecord(BodyKind3D.Static, false),
                            CreateBoxColliderRecord(new float3(2f, 2f, 2f), true)
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = 4,
                        Name = "KinematicPlatform",
                        LocalPosition = new float3(-2f, 0.5f, 0f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateRigidBodyRecord(BodyKind3D.Kinematic, false),
                            CreateCapsuleColliderRecord(0.5f, 2f),
                            CreateKinematicMotionRecord()
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = 5,
                        Name = "Controller",
                        LocalPosition = new float3(0f, 1f, -2f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateBoxColliderRecord(new float3(1f, 2f, 1f), false),
                            CreateCharacterControllerRecord()
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };

            PhysicsSceneFeatureFlags3D features = PhysicsSceneFeatureAnalyzer3D.Analyze(sceneAsset);

            Assert.True((features & PhysicsSceneFeatureFlags3D.SphereBoxContact) != 0);
            Assert.True((features & PhysicsSceneFeatureFlags3D.CapsuleSphereContact) != 0);
            Assert.True((features & PhysicsSceneFeatureFlags3D.KinematicMotion) != 0);
            Assert.True((features & PhysicsSceneFeatureFlags3D.TriggerEvents) != 0);
            Assert.True((features & PhysicsSceneFeatureFlags3D.CharacterController) != 0);
            Assert.True((features & PhysicsSceneFeatureFlags3D.CharacterControllerBodySupport) != 0);
        }

        /// <summary>
        /// Ensures the code-generation feature analyzer accepts current rigid-body scene payloads.
        /// </summary>
        [Fact]
        public void Analyze_WithSerializedRigidBodyVersion2_ReportsBoxContactFeature() {
            SceneAsset sceneAsset = new SceneAsset {
                Id = "Scenes/PhysicsSerializedVersion2.helen",
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1,
                        Name = "Ground",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateRigidBodyRecord(BodyKind3D.Static, false, 2),
                            CreateBoxColliderRecord(new float3(8f, 1f, 8f), false)
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = 2,
                        Name = "DynamicBox",
                        LocalPosition = new float3(0f, 2f, 0f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateRigidBodyRecord(BodyKind3D.Dynamic, true, 2),
                            CreateBoxColliderRecord(new float3(1f, 1f, 1f), false)
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };

            PhysicsSceneFeatureFlags3D features = PhysicsSceneFeatureAnalyzer3D.Analyze(sceneAsset);

            Assert.True((features & PhysicsSceneFeatureFlags3D.BoxBoxContact) != 0);
        }

        /// <summary>
        /// Ensures legacy serialized box-collider payloads remain analyzable while older scenes are still present in projects.
        /// </summary>
        [Fact]
        public void Analyze_WithSerializedBoxColliderVersion1_ReportsBoxContactFeature() {
            SceneAsset sceneAsset = new SceneAsset {
                Id = "Scenes/PhysicsSerializedLegacyBoxCollider.helen",
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1,
                        Name = "Ground",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateRigidBodyRecord(BodyKind3D.Static, false, 1),
                            CreateBoxColliderRecord(new float3(8f, 1f, 8f), false, 1)
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = 2,
                        Name = "DynamicBox",
                        LocalPosition = new float3(0f, 2f, 0f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateRigidBodyRecord(BodyKind3D.Dynamic, true, 1),
                            CreateBoxColliderRecord(new float3(1f, 1f, 1f), false, 1)
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };

            PhysicsSceneFeatureFlags3D features = PhysicsSceneFeatureAnalyzer3D.Analyze(sceneAsset);

            Assert.True((features & PhysicsSceneFeatureFlags3D.BoxBoxContact) != 0);
        }

        /// <summary>
        /// Creates one initialized entity suitable for scene feature analysis tests.
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
        /// Creates one simple flat cooked floor represented by two triangles.
        /// </summary>
        /// <returns>Cooked static-mesh collision data for one flat floor.</returns>
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

        /// <summary>
        /// Creates one serialized rigid-body component record.
        /// </summary>
        /// <param name="bodyKind">Rigid-body participation mode to encode.</param>
        /// <param name="useGravity">True when gravity should be enabled.</param>
        /// <returns>Serialized rigid-body scene record.</returns>
        static SceneComponentAssetRecord CreateRigidBodyRecord(BodyKind3D bodyKind, bool useGravity) {
            return CreateRigidBodyRecord(bodyKind, useGravity, 1);
        }

        /// <summary>
        /// Creates one serialized rigid-body component record with a specific payload version.
        /// </summary>
        /// <param name="bodyKind">Rigid-body participation mode to encode.</param>
        /// <param name="useGravity">True when gravity should be enabled.</param>
        /// <param name="version">Rigid-body payload format version to encode.</param>
        /// <returns>Serialized rigid-body scene record.</returns>
        static SceneComponentAssetRecord CreateRigidBodyRecord(BodyKind3D bodyKind, bool useGravity, byte version) {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(version);
            writer.WriteByte((byte)bodyKind);
            writer.WriteByte(useGravity ? (byte)1 : (byte)0);
            writer.WriteSingle(1f);
            writer.WriteSingle(1f);
            writer.WriteFloat3(float3.Zero);
            if (version >= 2) {
                writer.WriteFloat3(float3.Zero);
            }

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.RigidBody3DComponent",
                ComponentIndex = 0,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Creates one serialized box-collider component record.
        /// </summary>
        /// <param name="size">Full collider size to encode.</param>
        /// <param name="isTrigger">True when the collider should be encoded as a trigger.</param>
        /// <returns>Serialized box-collider scene record.</returns>
        static SceneComponentAssetRecord CreateBoxColliderRecord(float3 size, bool isTrigger) {
            return CreateBoxColliderRecord(size, isTrigger, 2);
        }

        /// <summary>
        /// Creates one serialized box-collider component record with a specific payload version.
        /// </summary>
        /// <param name="size">Full collider size to encode.</param>
        /// <param name="isTrigger">True when the collider should be encoded as a trigger.</param>
        /// <param name="version">Box-collider payload format version to encode.</param>
        /// <returns>Serialized box-collider scene record.</returns>
        static SceneComponentAssetRecord CreateBoxColliderRecord(float3 size, bool isTrigger, byte version) {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(version);
            writer.WriteFloat3(size);
            if (version >= 2) {
                writer.WriteUInt16(1);
                writer.WriteUInt16(ushort.MaxValue);
                writer.WriteByte(isTrigger ? (byte)1 : (byte)0);
            }

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.BoxCollider3DComponent",
                ComponentIndex = 1,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Creates one serialized sphere-collider component record.
        /// </summary>
        /// <param name="radius">Sphere collider radius to encode.</param>
        /// <returns>Serialized sphere-collider scene record.</returns>
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
        /// Creates one serialized capsule-collider component record.
        /// </summary>
        /// <param name="radius">Capsule collider radius to encode.</param>
        /// <param name="height">Capsule collider full height to encode.</param>
        /// <returns>Serialized capsule-collider scene record.</returns>
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
        /// Creates one serialized kinematic-motion component record.
        /// </summary>
        /// <returns>Serialized kinematic-motion scene record.</returns>
        static SceneComponentAssetRecord CreateKinematicMotionRecord() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteFloat3(new float3(-2f, 0.5f, 0f));
            writer.WriteFloat3(new float3(0.5f, 0.5f, 0f));
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(1d));
            writer.WriteByte(1);

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.KinematicMotion3DComponent",
                ComponentIndex = 2,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Creates one serialized character-controller component record.
        /// </summary>
        /// <returns>Serialized character-controller scene record.</returns>
        static SceneComponentAssetRecord CreateCharacterControllerRecord() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteFloat3(new float3(1f, 0f, 0f));
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(3d));
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(1d));
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(0.75d));
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(0.3d));

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.CharacterController3DComponent",
                ComponentIndex = 1,
                Payload = stream.ToArray()
            };
        }
    }
}

