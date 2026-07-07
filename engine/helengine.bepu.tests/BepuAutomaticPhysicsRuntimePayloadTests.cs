namespace helengine.bepu.tests {
    /// <summary>
    /// Verifies automatic reflected physics component payloads can materialize live BEPU-backed runtime bodies.
    /// </summary>
    public sealed class BepuAutomaticPhysicsRuntimePayloadTests {
        /// <summary>
        /// Ensures a simple automatic-payload box stack falls and remains supported after scene loading.
        /// </summary>
        [Fact]
        public void Load_WhenAutomaticPhysicsPayloadsDescribeSupportedBoxStack_BodiesRemainSupported() {
            SceneAsset sceneAsset = new SceneAsset {
                Id = "Scenes/TestAutomaticBepuPhysics.helen",
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Ground",
                        LocalPosition = new float3(0f, -0.5f, 0f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateAutomaticRigidBodyRecord(BodyKind3D.Static, false),
                            CreateAutomaticBoxColliderRecord(new float3(14f, 1f, 14f), false)
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = 2u,
                        Name = "LowerBox",
                        LocalPosition = new float3(0f, 1f, 0f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateAutomaticRigidBodyRecord(BodyKind3D.Dynamic, true),
                            CreateAutomaticBoxColliderRecord(float3.One, false)
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = 3u,
                        Name = "UpperBox",
                        LocalPosition = new float3(0f, 2f, 0f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateAutomaticRigidBodyRecord(BodyKind3D.Dynamic, true),
                            CreateAutomaticBoxColliderRecord(float3.One, false)
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };

            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            BepuRuntimeComponentRegistration.Register(core);

            RuntimeSceneLoadService sceneLoadService = new RuntimeSceneLoadService(core.SceneAssetReferenceResolver, core.SceneRuntimeComponentRegistry);
            IReadOnlyList<Entity> rootEntities = sceneLoadService.Load(sceneAsset);
            BepuPhysicsWorld3D world = (BepuPhysicsWorld3D)core.PhysicsRuntime;
            world.BindScene(rootEntities);

            Entity lowerBoxEntity = rootEntities[1];
            Entity upperBoxEntity = rootEntities[2];

            for (int index = 0; index < 180; index++) {
                world.Step(1.0 / 60.0);
            }

            Assert.InRange(lowerBoxEntity.LocalPosition.Y, 0.49f, 0.56f);
            Assert.InRange(upperBoxEntity.LocalPosition.Y, 1.48f, 1.56f);
        }

        /// <summary>
        /// Creates one automatic reflected rigid-body payload record using the unified built-in component persistence layout.
        /// </summary>
        /// <param name="bodyKind">Rigid-body kind that should be serialized.</param>
        /// <param name="useGravity">Whether the body should use gravity.</param>
        /// <returns>Rigid-body scene component payload record.</returns>
        static SceneComponentAssetRecord CreateAutomaticRigidBodyRecord(BodyKind3D bodyKind, bool useGravity) {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteInt32(6);
            writer.WriteFloat3(float3.Zero);
            writer.WriteInt32((int)bodyKind);
            writer.WriteDouble(1.0);
            writer.WriteFloat3(float3.Zero);
            writer.WriteDouble(1.0);
            writer.WriteByte(useGravity ? (byte)1 : (byte)0);

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.RigidBody3DComponent",
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Creates one automatic reflected box-collider payload record using the unified built-in component persistence layout.
        /// </summary>
        /// <param name="size">Authored collider size.</param>
        /// <param name="isTrigger">Whether the collider should be a trigger volume.</param>
        /// <returns>Box-collider scene component payload record.</returns>
        static SceneComponentAssetRecord CreateAutomaticBoxColliderRecord(float3 size, bool isTrigger) {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteInt32(7);
            writer.WriteUInt16(1);
            writer.WriteUInt16(ushort.MaxValue);
            writer.WriteDouble(0.5);
            writer.WriteByte(isTrigger ? (byte)1 : (byte)0);
            writer.WriteDouble(0.0);
            writer.WriteFloat3(size);
            writer.WriteDouble(0.5);

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.BoxCollider3DComponent",
                Payload = stream.ToArray()
            };
        }
    }
}

