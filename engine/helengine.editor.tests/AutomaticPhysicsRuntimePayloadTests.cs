using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies automatic reflected physics component payloads still materialize live runtime physics bodies.
    /// </summary>
    public sealed class AutomaticPhysicsRuntimePayloadTests {
        /// <summary>
        /// Ensures one stacked automatic-payload rigid body falls under gravity after scene loading and world binding.
        /// </summary>
        [Fact]
        public void Load_WhenAutomaticPhysicsPayloadsDescribeStackedBoxes_DynamicUpperBoxFalls() {
            SceneAsset sceneAsset = new SceneAsset {
                Id = "Scenes/TestAutomaticPhysics.helen",
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
                        LocalPosition = new float3(0.9f, 3f, 0f),
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
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
            Physics3DRuntimeComponentRegistration.Register(core);

            RuntimeSceneLoadService sceneLoadService = new RuntimeSceneLoadService(core.SceneAssetReferenceResolver, core.SceneRuntimeComponentRegistry);
            IReadOnlyList<Entity> rootEntities = sceneLoadService.Load(sceneAsset);
            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(rootEntities);

            Entity upperBoxEntity = rootEntities[2];
            float initialY = upperBoxEntity.LocalPosition.Y;

            for (int index = 0; index < 60; index++) {
                world.Step(1.0 / 60.0);
            }

            Assert.True(upperBoxEntity.LocalPosition.Y < initialY - 0.25f, $"Expected the upper automatic-payload box to fall, but its Y position only moved from {initialY} to {upperBoxEntity.LocalPosition.Y}.");
        }

        /// <summary>
        /// Ensures the authored stacked-boxes scene shape still collides with the floor instead of tunneling through it after automatic physics packaging changes.
        /// </summary>
        [Fact]
        public void Load_WhenAutomaticPhysicsPayloadsMatchAuthoredStackedBoxes_BoxesDoNotFallThroughFloor() {
            SceneAsset sceneAsset = new SceneAsset {
                Id = "Scenes/TestAutomaticPhysicsAuthoredScale.helen",
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Ground",
                        LocalPosition = new float3(0f, -0.5f, 0f),
                        LocalScale = new float3(14f, 1f, 14f),
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateAutomaticRigidBodyRecord(BodyKind3D.Static, false),
                            CreateAutomaticBoxColliderRecord(float3.One, false)
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
                        LocalPosition = new float3(0.9f, 3f, 0f),
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
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
            Physics3DRuntimeComponentRegistration.Register(core);

            RuntimeSceneLoadService sceneLoadService = new RuntimeSceneLoadService(core.SceneAssetReferenceResolver, core.SceneRuntimeComponentRegistry);
            IReadOnlyList<Entity> rootEntities = sceneLoadService.Load(sceneAsset);
            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(rootEntities);

            Entity lowerBoxEntity = rootEntities[1];
            Entity upperBoxEntity = rootEntities[2];

            for (int index = 0; index < 360; index++) {
                world.Step(1.0 / 60.0);
            }

            Assert.True(lowerBoxEntity.LocalPosition.Y > -0.1f, $"Expected the lower box to remain above the floor, but its Y position became {lowerBoxEntity.LocalPosition.Y}.");
            Assert.True(upperBoxEntity.LocalPosition.Y > 0.4f, $"Expected the upper box to remain supported by the stack, but its Y position became {upperBoxEntity.LocalPosition.Y}.");
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
