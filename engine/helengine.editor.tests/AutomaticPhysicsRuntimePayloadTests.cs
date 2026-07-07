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
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
            BepuRuntimeComponentRegistration.Register(core);
            BepuPhysicsWorld3D world = BepuRuntimeComponentRegistration.CreateRuntimeWorld(core);
            BepuRuntimeComponentRegistration.AttachRuntimeWorld(core, world);

            RuntimeSceneLoadService sceneLoadService = new RuntimeSceneLoadService(core.SceneAssetReferenceResolver, core.SceneRuntimeComponentRegistry);
            IReadOnlyList<Entity> rootEntities = sceneLoadService.Load(sceneAsset);
            Assert.Same(world, core.PhysicsRuntime);
            world.BindScene(rootEntities);

            Entity upperBoxEntity = rootEntities[2];
            float initialY = upperBoxEntity.LocalPosition.Y;

            for (int index = 0; index < 60; index++) {
                world.Step(1.0 / 60.0);
            }

            Assert.True(upperBoxEntity.LocalPosition.Y < initialY - 0.25f, $"Expected the upper automatic-payload box to fall, but its Y position only moved from {initialY} to {upperBoxEntity.LocalPosition.Y}.");
        }

        /// <summary>
        /// Creates one automatic reflected rigid-body payload record using the unified built-in component persistence layout.
        /// </summary>
        /// <param name="bodyKind">Rigid-body kind that should be serialized.</param>
        /// <param name="useGravity">Whether the body should use gravity.</param>
        /// <returns>Rigid-body scene component payload record.</returns>
        static SceneComponentAssetRecord CreateAutomaticRigidBodyRecord(BodyKind3D bodyKind, bool useGravity) {
            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.RigidBody3DComponent",
                Payload = WriteAutomaticRuntimeComponentPayload(
                    new RigidBody3DComponent {
                        AngularVelocity = float3.Zero,
                        BodyKind = bodyKind,
                        GravityScale = 1d,
                        LinearVelocity = float3.Zero,
                        Mass = 1d,
                        UseGravity = useGravity
                    })
            };
        }

        /// <summary>
        /// Creates one automatic reflected box-collider payload record using the unified built-in component persistence layout.
        /// </summary>
        /// <param name="size">Authored collider size.</param>
        /// <param name="isTrigger">Whether the collider should be a trigger volume.</param>
        /// <returns>Box-collider scene component payload record.</returns>
        static SceneComponentAssetRecord CreateAutomaticBoxColliderRecord(float3 size, bool isTrigger) {
            BoxCollider3DComponent component = new BoxCollider3DComponent {
                CollisionLayer = 1,
                CollisionMask = ushort.MaxValue,
                DynamicFriction = 0.4d,
                IsTrigger = isTrigger,
                Restitution = 0d,
                Size = size,
                StaticFriction = 0.6d
            };

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.BoxCollider3DComponent",
                Payload = WriteAutomaticRuntimeComponentPayload(component)
            };
        }

        /// <summary>
        /// Serializes one component into the automatic runtime payload shape consumed by the generic runtime scene loader.
        /// </summary>
        /// <param name="component">Component instance to serialize.</param>
        /// <returns>Serialized automatic runtime payload bytes.</returns>
        static byte[] WriteAutomaticRuntimeComponentPayload(Component component) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }

            ScriptComponentReflectionSchemaBuilder schemaBuilder = new ScriptComponentReflectionSchemaBuilder();
            ScriptComponentReflectionSchema schema = schemaBuilder.Build(component.GetType());
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion);
            writer.WriteInt32(schema.Members.Count);
            for (int index = 0; index < schema.Members.Count; index++) {
                ScriptComponentReflectionMember member = schema.Members[index];
                AutomaticScriptComponentPersistenceDescriptor.WriteSupportedMemberValue(writer, member, component, null);
            }

            return stream.ToArray();
        }
    }
}
