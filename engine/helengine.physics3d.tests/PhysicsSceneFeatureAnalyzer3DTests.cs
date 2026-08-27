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
        /// Ensures the code-generation feature analyzer rejects the removed standalone rigid-body and box-collider payload formats.
        /// </summary>
        [Fact]
        public void Analyze_WithSerializedStandalonePhysicsPayloads_ThrowsUnsupportedPayloadVersion() {
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

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => PhysicsSceneFeatureAnalyzer3D.Analyze(sceneAsset));

            Assert.Contains("Unsupported", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures a legacy size-only box-collider payload is rejected instead of defaulting filtering and trigger state.
        /// </summary>
        [Fact]
        public void Analyze_WithSerializedBoxColliderSizeOnlyPayload_ThrowsUnsupportedMemberCount() {
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

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => PhysicsSceneFeatureAnalyzer3D.Analyze(sceneAsset));

            Assert.Contains("member", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures a reflected box-collider payload with a non-current member count is rejected instead of falling back to the size-only layout.
        /// </summary>
        [Fact]
        public void Analyze_WithAutomaticBoxColliderPayloadMissingMember_ThrowsUnsupportedMemberCount() {
            SceneAsset sceneAsset = new SceneAsset {
                Id = "Scenes/PhysicsMalformedAutomaticBoxCollider.helen",
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1,
                        Name = "Ground",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateCurrentRigidBodyRecord(BodyKind3D.Static, false),
                            CreateAutomaticBoxColliderRecordWithMemberCount(6)
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => PhysicsSceneFeatureAnalyzer3D.Analyze(sceneAsset));

            Assert.Contains("member", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures a current rigid-body payload missing its trailing sleep fields is rejected rather than analyzed from a prefix.
        /// </summary>
        [Fact]
        public void Analyze_WithTruncatedCurrentRigidBodyPayload_ThrowsCurrentFormatError() {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => PhysicsSceneFeatureAnalyzer3D.Analyze(CreateSingleComponentScene(CreateCurrentRigidBodyRecordWithoutSleepFields())));

            Assert.Contains("truncated", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("regenerate", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures a current box-collider payload missing its trailing physical fields is rejected rather than analyzed from a prefix.
        /// </summary>
        [Fact]
        public void Analyze_WithTruncatedCurrentBoxColliderPayload_ThrowsCurrentFormatError() {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => PhysicsSceneFeatureAnalyzer3D.Analyze(CreateSingleComponentScene(CreateCurrentBoxColliderRecordWithoutTrailingFields())));

            Assert.Contains("truncated", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("regenerate", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures a current sphere-collider payload missing its trailing physical fields is rejected rather than analyzed from a prefix.
        /// </summary>
        [Fact]
        public void Analyze_WithTruncatedCurrentSphereColliderPayload_ThrowsCurrentFormatError() {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => PhysicsSceneFeatureAnalyzer3D.Analyze(CreateSingleComponentScene(CreateCurrentSphereColliderRecordWithoutTrailingFields())));

            Assert.Contains("truncated", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("regenerate", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures a current capsule-collider payload missing its trailing physical fields is rejected rather than analyzed from a prefix.
        /// </summary>
        [Fact]
        public void Analyze_WithTruncatedCurrentCapsuleColliderPayload_ThrowsCurrentFormatError() {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => PhysicsSceneFeatureAnalyzer3D.Analyze(CreateSingleComponentScene(CreateCurrentCapsuleColliderRecordWithoutTrailingFields())));

            Assert.Contains("truncated", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("regenerate", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures a current static-mesh collider payload missing its trailing physical fields is rejected rather than analyzed from a prefix.
        /// </summary>
        [Fact]
        public void Analyze_WithTruncatedCurrentStaticMeshColliderPayload_ThrowsCurrentFormatError() {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => PhysicsSceneFeatureAnalyzer3D.Analyze(CreateSingleComponentScene(CreateCurrentStaticMeshColliderRecordWithoutTrailingFields())));

            Assert.Contains("truncated", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("regenerate", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures a current payload with bytes beyond the exact schema is rejected instead of silently accepting trailing data.
        /// </summary>
        [Fact]
        public void Analyze_WithTrailingCurrentColliderPayloadBytes_ThrowsCurrentFormatError() {
            SceneComponentAssetRecord record = CreateBoxColliderRecord(float3.One, false);
            record.Payload = AppendPayloadByte(record.Payload, 0x7f);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => PhysicsSceneFeatureAnalyzer3D.Analyze(CreateSingleComponentScene(record)));

            Assert.Contains("trailing", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("regenerate", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures the analyzer no longer contains the removed legacy box-collider payload version marker.
        /// </summary>
        [Fact]
        public void PhysicsSceneFeatureAnalyzer3D_DoesNotExposeLegacyBoxColliderPayloadVersion() {
            Assert.Null(typeof(PhysicsSceneFeatureAnalyzer3D).GetField(
                "LegacyBoxColliderPayloadVersion",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
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
            return CreateCurrentRigidBodyRecord(bodyKind, useGravity);
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
        /// Creates one current automatic rigid-body component record with every reflected member present.
        /// </summary>
        /// <param name="bodyKind">Rigid-body participation mode to encode.</param>
        /// <param name="useGravity">True when gravity should be enabled.</param>
        /// <returns>Current automatic rigid-body scene record.</returns>
        static SceneComponentAssetRecord CreateCurrentRigidBodyRecord(BodyKind3D bodyKind, bool useGravity) {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteInt32(8);
            writer.WriteFloat3(float3.Zero);
            writer.WriteInt32((int)bodyKind);
            writer.WriteDouble(1d);
            writer.WriteFloat3(float3.Zero);
            writer.WriteDouble(1d);
            writer.WriteByte(useGravity ? (byte)1 : (byte)0);
            writer.WriteDouble(0.5d);
            writer.WriteInt32(10);

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.RigidBody3DComponent",
                ComponentIndex = 0,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Creates a current rigid-body payload that stops after the pre-sleep reflected members.
        /// </summary>
        /// <returns>Truncated rigid-body scene record.</returns>
        static SceneComponentAssetRecord CreateCurrentRigidBodyRecordWithoutSleepFields() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteInt32(8);
            writer.WriteFloat3(float3.Zero);
            writer.WriteInt32((int)BodyKind3D.Static);
            writer.WriteDouble(1d);
            writer.WriteFloat3(float3.Zero);
            writer.WriteDouble(1d);
            writer.WriteByte(0);

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
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteInt32(7);
            writer.WriteUInt16(1);
            writer.WriteUInt16(ushort.MaxValue);
            writer.WriteDouble(0.4d);
            writer.WriteByte(isTrigger ? (byte)1 : (byte)0);
            writer.WriteDouble(0d);
            writer.WriteFloat3(size);
            writer.WriteDouble(0.6d);

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.BoxCollider3DComponent",
                ComponentIndex = 1,
                Payload = stream.ToArray()
            };
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
        /// Creates a current box-collider payload that stops after the trigger member.
        /// </summary>
        /// <returns>Truncated box-collider scene record.</returns>
        static SceneComponentAssetRecord CreateCurrentBoxColliderRecordWithoutTrailingFields() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteInt32(7);
            writer.WriteUInt16(1);
            writer.WriteUInt16(ushort.MaxValue);
            writer.WriteDouble(0.4d);
            writer.WriteByte(0);

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.BoxCollider3DComponent",
                ComponentIndex = 1,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Creates one malformed automatic box-collider record with a caller-selected reflected member count.
        /// </summary>
        /// <param name="memberCount">Member count to encode in the automatic payload header.</param>
        /// <returns>Box-collider scene record with an intentionally non-current member count.</returns>
        static SceneComponentAssetRecord CreateAutomaticBoxColliderRecordWithMemberCount(int memberCount) {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteInt32(memberCount);
            writer.WriteUInt16(1);
            writer.WriteUInt16(ushort.MaxValue);
            writer.WriteDouble(0.4d);
            writer.WriteByte(0);
            writer.WriteDouble(0d);
            writer.WriteFloat3(float3.One);
            writer.WriteDouble(0.6d);

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
            writer.WriteInt32(7);
            writer.WriteUInt16(1);
            writer.WriteUInt16(ushort.MaxValue);
            writer.WriteDouble(0.4d);
            writer.WriteByte(0);
            writer.WriteSingle(radius);
            writer.WriteDouble(0d);
            writer.WriteDouble(0.6d);

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.SphereCollider3DComponent",
                ComponentIndex = 1,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Creates a current sphere-collider payload that stops after the trigger member.
        /// </summary>
        /// <returns>Truncated sphere-collider scene record.</returns>
        static SceneComponentAssetRecord CreateCurrentSphereColliderRecordWithoutTrailingFields() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteInt32(7);
            writer.WriteUInt16(1);
            writer.WriteUInt16(ushort.MaxValue);
            writer.WriteDouble(0.4d);
            writer.WriteByte(0);

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
            writer.WriteInt32(8);
            writer.WriteUInt16(1);
            writer.WriteUInt16(ushort.MaxValue);
            writer.WriteDouble(0.4d);
            writer.WriteSingle(height);
            writer.WriteByte(0);
            writer.WriteSingle(radius);
            writer.WriteDouble(0d);
            writer.WriteDouble(0.6d);

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.CapsuleCollider3DComponent",
                ComponentIndex = 1,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Creates a current capsule-collider payload that stops after the trigger member.
        /// </summary>
        /// <returns>Truncated capsule-collider scene record.</returns>
        static SceneComponentAssetRecord CreateCurrentCapsuleColliderRecordWithoutTrailingFields() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteInt32(8);
            writer.WriteUInt16(1);
            writer.WriteUInt16(ushort.MaxValue);
            writer.WriteDouble(0.4d);
            writer.WriteSingle(2f);
            writer.WriteByte(0);

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.CapsuleCollider3DComponent",
                ComponentIndex = 1,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Creates a current static-mesh collider payload with optional reference fields omitted and trailing friction fields absent.
        /// </summary>
        /// <returns>Truncated static-mesh collider scene record.</returns>
        static SceneComponentAssetRecord CreateCurrentStaticMeshColliderRecordWithoutTrailingFields() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteInt32(8);
            writer.WriteByte(0);
            writer.WriteUInt16(1);
            writer.WriteUInt16(ushort.MaxValue);
            writer.WriteByte(0);
            writer.WriteDouble(0.4d);
            writer.WriteByte(0);

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.StaticMeshCollider3DComponent",
                ComponentIndex = 1,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Creates one scene asset containing one serialized component record.
        /// </summary>
        /// <param name="record">Serialized component record.</param>
        /// <returns>Single-record scene asset.</returns>
        static SceneAsset CreateSingleComponentScene(SceneComponentAssetRecord record) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }

            return new SceneAsset {
                Id = "Scenes/PhysicsTruncatedPayload.helen",
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1,
                        Name = "Payload",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] { record },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };
        }

        /// <summary>
        /// Appends one byte to a serialized component payload.
        /// </summary>
        /// <param name="payload">Current component payload.</param>
        /// <param name="value">Trailing byte to append.</param>
        /// <returns>Payload with one appended byte.</returns>
        static byte[] AppendPayloadByte(byte[] payload, byte value) {
            if (payload == null) {
                throw new ArgumentNullException(nameof(payload));
            }

            byte[] result = new byte[payload.Length + 1];
            Buffer.BlockCopy(payload, 0, result, 0, payload.Length);
            result[^1] = value;
            return result;
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

