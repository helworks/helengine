namespace helengine.physics3d.tests {
    /// <summary>
    /// Verifies cube-only 3D physics components load from serialized scene assets.
    /// </summary>
    [Collection(Physics3DTestCollection.Name)]
    public sealed class PhysicsWorld3DSceneLoadTests {
        /// <summary>
        /// Ensures runtime registration attaches the cube-only default world.
        /// </summary>
        [Fact]
        public void Register_AttachesDefaultWorld() {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            try {
                core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));

                Physics3DRuntimeComponentRegistration.Register(core);

                PhysicsWorld3D world = Assert.IsType<PhysicsWorld3D>(core.PhysicsRuntime);
                Assert.Empty(world.BodyStates);
            } finally {
                core.Dispose();
            }
        }

        /// <summary>
        /// Ensures serialized rigid-body and box-collider components load and simulate through the cube runtime.
        /// </summary>
        [Fact]
        public void LoadSceneAsset_WithBoxPhysicsComponents_LoadsAndSimulatesDynamicGroundContact() {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            Physics3DRuntimeComponentRegistration.Register(core);

            SceneAsset sceneAsset = CreateBoxPhysicsSceneAsset();
            RuntimeSceneLoadService sceneLoadService = new RuntimeSceneLoadService(core.SceneAssetReferenceResolver, core.SceneRuntimeComponentRegistry);
            IReadOnlyList<Entity> rootEntities = sceneLoadService.Load(sceneAsset);
            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(rootEntities);

            for (int index = 0; index < 180; index++) {
                world.Step(1d / 60d);
            }

            Entity dynamicEntity = rootEntities[1];
            RigidBody3DComponent rigidBody = FindComponent<RigidBody3DComponent>(dynamicEntity);

            Assert.InRange(dynamicEntity.LocalPosition.Y, 0.99f, 1.02f);
            Assert.NotNull(rigidBody);
            Assert.InRange(rigidBody.LinearVelocity.Y, -0.0001f, 0.0001f);
        }

        /// <summary>
        /// Creates one serialized cube-only physics scene asset.
        /// </summary>
        /// <returns>Scene asset ready for runtime loading.</returns>
        static SceneAsset CreateBoxPhysicsSceneAsset() {
            return new SceneAsset {
                Id = "scenes/physics/cube_only_runtime_load.helen",
                RootEntities = new[] {
                    CreateBodyEntity("ground", new float3(0f, 0f, 0f), BodyKind3D.Static, false, new float3(8f, 1f, 8f)),
                    CreateBodyEntity("dynamic", new float3(0f, 3f, 0f), BodyKind3D.Dynamic, true, new float3(1f, 1f, 1f))
                },
                AssetReferences = Array.Empty<SceneAssetReference>()
            };
        }

        /// <summary>
        /// Creates one serialized rigid-body box entity.
        /// </summary>
        /// <param name="entityId">Stable fixture entity id.</param>
        /// <param name="localPosition">Initial local position.</param>
        /// <param name="bodyKind">Rigid body simulation kind.</param>
        /// <param name="useGravity">Whether gravity should affect the body.</param>
        /// <param name="boxSize">Full authored box size.</param>
        /// <returns>Serialized scene entity.</returns>
        static SceneEntityAsset CreateBodyEntity(string entityId, float3 localPosition, BodyKind3D bodyKind, bool useGravity, float3 boxSize) {
            return new SceneEntityAsset {
                Id = CreateSceneEntityId(entityId),
                Name = entityId,
                LocalPosition = localPosition,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = new[] {
                    CreateRigidBodyRecord(bodyKind, useGravity),
                    CreateBoxColliderRecord(boxSize)
                },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates one serialized rigid-body component record.
        /// </summary>
        /// <param name="bodyKind">Serialized rigid body kind.</param>
        /// <param name="useGravity">Serialized gravity flag.</param>
        /// <returns>Serialized scene component record.</returns>
        static SceneComponentAssetRecord CreateRigidBodyRecord(BodyKind3D bodyKind, bool useGravity) {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(2);
            writer.WriteByte((byte)bodyKind);
            writer.WriteByte(useGravity ? (byte)1 : (byte)0);
            writer.WriteSingle(1f);
            writer.WriteSingle(1f);
            writer.WriteFloat3(float3.Zero);
            writer.WriteFloat3(float3.Zero);

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.RigidBody3DComponent",
                ComponentIndex = 0,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Creates one serialized box-collider component record.
        /// </summary>
        /// <param name="boxSize">Full authored box size.</param>
        /// <returns>Serialized scene component record.</returns>
        static SceneComponentAssetRecord CreateBoxColliderRecord(float3 boxSize) {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(2);
            writer.WriteFloat3(boxSize);
            writer.WriteUInt16(1);
            writer.WriteUInt16(ushort.MaxValue);
            writer.WriteByte(0);

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.BoxCollider3DComponent",
                ComponentIndex = 1,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Finds one component of the requested type on an entity.
        /// </summary>
        /// <typeparam name="TComponent">Requested component type.</typeparam>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>Attached component when present; otherwise null.</returns>
        static TComponent FindComponent<TComponent>(Entity entity) where TComponent : Component {
            if (entity.Components == null) {
                return null;
            }

            for (int index = 0; index < entity.Components.Count; index++) {
                if (entity.Components[index] is TComponent component) {
                    return component;
                }
            }

            return null;
        }

        /// <summary>
        /// Creates a deterministic nonzero numeric scene entity id from one readable fixture id.
        /// </summary>
        /// <param name="entityId">Readable fixture entity id.</param>
        /// <returns>Stable numeric scene entity id.</returns>
        static uint CreateSceneEntityId(string entityId) {
            unchecked {
                uint hash = 2166136261u;
                for (int index = 0; index < entityId.Length; index++) {
                    hash ^= entityId[index];
                    hash *= 16777619u;
                }

                if (hash == 0u) {
                    return 1u;
                }

                return hash;
            }
        }
    }
}
