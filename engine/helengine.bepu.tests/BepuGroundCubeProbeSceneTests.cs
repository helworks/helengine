namespace helengine.bepu.tests {
    /// <summary>
    /// Verifies the generated city ground-cube probe scene keeps stable entity transforms when loaded through the BEPU-backed runtime.
    /// </summary>
    public sealed class BepuGroundCubeProbeSceneTests {
        /// <summary>
        /// Absolute path to the cooked city ground-cube probe scene asset used by the current Windows diagnostic build.
        /// </summary>
        const string GroundCubeProbeScenePath = @"C:\dev\helprojs\city\windows-build-20260602-ground-cube-probe-direct\cooked\scenes\rendering\ground_cube_probe.hasset";

        /// <summary>
        /// Ensures the cooked ground-cube probe scene preserves authored scale and finite pose values while the dynamic cube falls onto the ground.
        /// </summary>
        [Fact]
        public void LoadGroundCubeProbeScene_WhenBoundToBepu_PreservesFiniteTransformsAndAuthoredScaleDuringFall() {
            Assert.True(File.Exists(GroundCubeProbeScenePath), "Expected the generated city ground-cube probe scene asset to exist.");

            using FileStream stream = File.OpenRead(GroundCubeProbeScenePath);
            SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            SceneAsset physicsOnlySceneAsset = CreatePhysicsOnlyProbeSceneAsset(sceneAsset);

            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            BepuRuntimeComponentRegistration.Register(core);

            RuntimeSceneLoadService sceneLoadService = new RuntimeSceneLoadService(core.SceneAssetReferenceResolver, core.SceneRuntimeComponentRegistry);
            IReadOnlyList<Entity> rootEntities = sceneLoadService.Load(physicsOnlySceneAsset);
            BepuRuntimeComponentRegistration.HandleLoadedScene(core, rootEntities);
            BepuPhysicsWorld3D world = Assert.IsType<BepuPhysicsWorld3D>(core.PhysicsRuntime);

            Entity groundEntity = rootEntities[0];
            Entity cubeEntity = rootEntities[1];

            AssertFloat3Equal(new float3(15f, 1f, 15f), groundEntity.LocalScale);
            AssertFloat3Equal(float3.One, cubeEntity.LocalScale);
            AssertFinitePose(groundEntity);
            AssertFinitePose(cubeEntity);

            for (int index = 0; index < 180; index++) {
                world.Step(1.0d / 60.0d);
            }

            AssertFloat3Equal(new float3(15f, 1f, 15f), groundEntity.LocalScale);
            AssertFloat3Equal(float3.One, cubeEntity.LocalScale);
            AssertFinitePose(groundEntity);
            AssertFinitePose(cubeEntity);
            Assert.InRange(cubeEntity.LocalPosition.Y, 0.49f, 0.56f);
        }
        /// <summary>
        /// Asserts two authored float3 values are equal within a tiny tolerance.
        /// </summary>
        /// <param name="expected">Expected authored value.</param>
        /// <param name="actual">Runtime value under test.</param>
        static void AssertFloat3Equal(float3 expected, float3 actual) {
            Assert.InRange(actual.X, expected.X - 0.0001f, expected.X + 0.0001f);
            Assert.InRange(actual.Y, expected.Y - 0.0001f, expected.Y + 0.0001f);
            Assert.InRange(actual.Z, expected.Z - 0.0001f, expected.Z + 0.0001f);
        }

        /// <summary>
        /// Reduces the generated probe scene to only the ground and cube physics payloads required for BEPU runtime verification.
        /// </summary>
        /// <param name="sceneAsset">Generated authored probe scene asset.</param>
        /// <returns>Physics-only clone of the probe scene.</returns>
        static SceneAsset CreatePhysicsOnlyProbeSceneAsset(SceneAsset sceneAsset) {
            if (sceneAsset == null) {
                throw new ArgumentNullException(nameof(sceneAsset));
            }

            return new SceneAsset {
                Id = sceneAsset.Id,
                RootEntities = new[] {
                    CreatePhysicsOnlyEntity(sceneAsset.RootEntities[2]),
                    CreatePhysicsOnlyEntity(sceneAsset.RootEntities[3])
                },
                AssetReferences = Array.Empty<SceneAssetReference>()
            };
        }

        /// <summary>
        /// Clones one serialized probe entity while keeping only the rigid-body and box-collider payload records.
        /// </summary>
        /// <param name="sourceEntity">Serialized entity to reduce.</param>
        /// <returns>Physics-only entity clone.</returns>
        static SceneEntityAsset CreatePhysicsOnlyEntity(SceneEntityAsset sourceEntity) {
            if (sourceEntity == null) {
                throw new ArgumentNullException(nameof(sourceEntity));
            }

            List<SceneComponentAssetRecord> keptComponents = new List<SceneComponentAssetRecord>();
            for (int index = 0; index < sourceEntity.Components.Length; index++) {
                SceneComponentAssetRecord component = sourceEntity.Components[index];
                if (string.Equals(component.ComponentTypeId, "helengine.RigidBody3DComponent", StringComparison.Ordinal)
                    || string.Equals(component.ComponentTypeId, "helengine.BoxCollider3DComponent", StringComparison.Ordinal)) {
                    keptComponents.Add(new SceneComponentAssetRecord {
                        ComponentKey = component.ComponentKey,
                        ComponentIndex = component.ComponentIndex,
                        ComponentTypeId = component.ComponentTypeId,
                        Payload = component.Payload
                    });
                }
            }

            return new SceneEntityAsset {
                Id = sourceEntity.Id,
                Name = sourceEntity.Name,
                IsStatic = sourceEntity.IsStatic,
                LocalPosition = sourceEntity.LocalPosition,
                LocalScale = sourceEntity.LocalScale,
                LocalOrientation = sourceEntity.LocalOrientation,
                Components = keptComponents.ToArray(),
                PlatformComponentOverrides = Array.Empty<SceneEntityPlatformComponentOverrideAsset>(),
                PlatformTransformOverrides = Array.Empty<SceneEntityPlatformTransformOverrideAsset>(),
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Asserts one entity pose contains only finite position and orientation components.
        /// </summary>
        /// <param name="entity">Entity whose pose should be validated.</param>
        static void AssertFinitePose(Entity entity) {
            Assert.True(float.IsFinite(entity.LocalPosition.X));
            Assert.True(float.IsFinite(entity.LocalPosition.Y));
            Assert.True(float.IsFinite(entity.LocalPosition.Z));
            Assert.True(float.IsFinite(entity.LocalOrientation.X));
            Assert.True(float.IsFinite(entity.LocalOrientation.Y));
            Assert.True(float.IsFinite(entity.LocalOrientation.Z));
            Assert.True(float.IsFinite(entity.LocalOrientation.W));
        }
    }
}

