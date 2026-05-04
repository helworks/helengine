namespace helengine.editor {
    /// <summary>
    /// Creates exportable scene assets for physics validation and demo playback.
    /// </summary>
    public sealed class PhysicsValidationSceneFactory {
        /// <summary>
        /// Stable generated provider identifier used for built-in primitive assets.
        /// </summary>
        const string GeneratedProviderId = EngineGeneratedAssetProvider.ProviderIdValue;

        /// <summary>
        /// Stable scene-asset source kind byte used for generated primitive references.
        /// </summary>
        const SceneAssetReferenceSourceKind GeneratedSourceKind = SceneAssetReferenceSourceKind.Generated;

        /// <summary>
        /// Stable render order assigned to generated debug geometry meshes.
        /// </summary>
        const byte DefaultMeshRenderOrder = 0;

        /// <summary>
        /// Stable camera draw order assigned to validation-scene cameras.
        /// </summary>
        const byte DefaultCameraDrawOrder = 0;

        /// <summary>
        /// Current payload version for serialized rigid-body component scene records.
        /// </summary>
        const byte RigidBodyComponentPayloadVersion = 1;

        /// <summary>
        /// Current payload version for serialized box-collider component scene records.
        /// </summary>
        const byte BoxColliderComponentPayloadVersion = 1;

        /// <summary>
        /// Current payload version for serialized kinematic-motion component scene records.
        /// </summary>
        const byte KinematicMotionComponentPayloadVersion = 1;

        /// <summary>
        /// Current payload version for serialized character-controller component scene records.
        /// </summary>
        const byte CharacterControllerComponentPayloadVersion = 1;

        /// <summary>
        /// Serialized rigid-body kind byte for static bodies.
        /// </summary>
        const byte StaticBodyKindCode = 0;

        /// <summary>
        /// Serialized rigid-body kind byte for dynamic bodies.
        /// </summary>
        const byte DynamicBodyKindCode = 2;

        /// <summary>
        /// Serialized rigid-body kind byte for kinematic bodies.
        /// </summary>
        const byte KinematicBodyKindCode = 1;

        /// <summary>
        /// Creates one fully-authored physics validation scene asset for the requested scene id.
        /// </summary>
        /// <param name="sceneId">Stable relative scene id to author.</param>
        /// <returns>Generated scene asset ready for serialization.</returns>
        public SceneAsset CreateSceneAsset(string sceneId) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }

            if (string.Equals(sceneId, PhysicsValidationSceneCatalog.CharacterSlopeSceneId, StringComparison.Ordinal)) {
                return CreateCharacterSlopeScene();
            } else if (string.Equals(sceneId, PhysicsValidationSceneCatalog.CharacterStepsSceneId, StringComparison.Ordinal)) {
                return CreateCharacterStepsScene();
            } else if (string.Equals(sceneId, PhysicsValidationSceneCatalog.CharacterMovingPlatformSceneId, StringComparison.Ordinal)) {
                return CreateCharacterMovingPlatformScene();
            } else if (string.Equals(sceneId, PhysicsValidationSceneCatalog.DynamicStackBoxesSceneId, StringComparison.Ordinal)) {
                return CreateDynamicStackBoxesScene();
            } else if (string.Equals(sceneId, PhysicsValidationSceneCatalog.DynamicSphereRampSceneId, StringComparison.Ordinal)) {
                return CreateDynamicSphereRampScene();
            } else if (string.Equals(sceneId, PhysicsValidationSceneCatalog.KinematicPushSceneId, StringComparison.Ordinal)) {
                return CreateKinematicPushScene();
            } else if (string.Equals(sceneId, PhysicsValidationSceneCatalog.MeshGroundStabilitySceneId, StringComparison.Ordinal)) {
                return CreateMeshGroundStabilityScene();
            } else if (string.Equals(sceneId, PhysicsValidationSceneCatalog.TriggerVolumeSceneId, StringComparison.Ordinal)) {
                return CreateTriggerVolumeScene();
            }

            throw new InvalidOperationException($"Unsupported physics validation scene id '{sceneId}'.");
        }

        /// <summary>
        /// Writes every known validation scene into the target project assets folder.
        /// </summary>
        /// <param name="projectRootPath">Absolute project root path that owns the `assets` directory.</param>
        public void WriteScenes(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            string assetsRootPath = Path.Combine(projectRootPath, "assets");
            if (!Directory.Exists(assetsRootPath)) {
                throw new DirectoryNotFoundException($"Physics validation scene export requires an assets directory at '{assetsRootPath}'.");
            }

            string[] sceneIds = PhysicsValidationSceneCatalog.GetSceneIds();
            for (int index = 0; index < sceneIds.Length; index++) {
                string sceneId = sceneIds[index];
                SceneAsset sceneAsset = CreateSceneAsset(sceneId);
                string fullPath = GetSceneFullPath(projectRootPath, sceneId);
                string directoryPath = Path.GetDirectoryName(fullPath);
                if (string.IsNullOrWhiteSpace(directoryPath)) {
                    throw new InvalidOperationException($"Could not resolve the directory path for scene '{sceneId}'.");
                }

                Directory.CreateDirectory(directoryPath);
                using FileStream stream = File.Create(fullPath);
                AssetSerializer.Serialize(stream, sceneAsset);
            }
        }

        /// <summary>
        /// Creates the character slope validation scene.
        /// </summary>
        /// <returns>Authored slope validation scene asset.</returns>
        SceneAsset CreateCharacterSlopeScene() {
            SceneEntityAsset scenarioEntity = CreateScenarioRoot(
                "character_slope.scenario",
                new[] {
                    CreatePhysicsBoxMeshEntity("character_slope.ground", "Ground", new float3(0f, -0.5f, 0f), new float3(14f, 1f, 14f), float4.Identity, StaticBodyKindCode, false),
                    CreatePhysicsBoxMeshEntity("character_slope.ramp", "SlopeRamp", new float3(2.25f, 0.6f, 0f), new float3(5f, 0.6f, 3f), CreateYawPitchRollDegrees(0.0, 0.0, 18.0), StaticBodyKindCode, false),
                    CreateCharacterControllerBoxMeshEntity("character_slope.controller", "CharacterController", new float3(-4f, 0.75f, 0f), new float3(0.9f, 1.5f, 0.9f), float4.Identity, new float3(1f, 0f, 0f), 3d, 1d, 0.75d, 0.3d),
                    CreateMarkerEntity("character_slope.spawn", "ControllerSpawn", new float3(-4f, 0.75f, 0f)),
                    CreateMarkerEntity("character_slope.goal", "SlopeGoal", new float3(4.25f, 1.75f, 0f))
                });
            SceneEntityAsset cameraEntity = CreateCameraEntity("character_slope.camera", new float3(8.5f, 5f, 7.5f), CreateYawPitchRollDegrees(-135.0, -18.0, 0.0));
            return CreateSceneAsset(PhysicsValidationSceneCatalog.CharacterSlopeSceneId, cameraEntity, scenarioEntity);
        }

        /// <summary>
        /// Creates the character steps validation scene.
        /// </summary>
        /// <returns>Authored steps validation scene asset.</returns>
        SceneAsset CreateCharacterStepsScene() {
            SceneEntityAsset scenarioEntity = CreateScenarioRoot(
                "character_steps.scenario",
                new[] {
                    CreateCubeMeshEntity("character_steps.ground", "Ground", new float3(0f, -0.5f, 0f), new float3(16f, 1f, 12f), float4.Identity),
                    CreateCubeMeshEntity("character_steps.step01", "Step01", new float3(0.75f, 0.15f, 0f), new float3(1.5f, 0.3f, 3f), float4.Identity),
                    CreateCubeMeshEntity("character_steps.step02", "Step02", new float3(2.25f, 0.45f, 0f), new float3(1.5f, 0.9f, 3f), float4.Identity),
                    CreateCubeMeshEntity("character_steps.step03", "Step03", new float3(3.75f, 0.75f, 0f), new float3(1.5f, 1.5f, 3f), float4.Identity),
                    CreateCubeMeshEntity("character_steps.step04", "Step04", new float3(5.25f, 1.05f, 0f), new float3(1.5f, 2.1f, 3f), float4.Identity),
                    CreateMarkerEntity("character_steps.spawn", "ControllerSpawn", new float3(-4.5f, 0.75f, 0f))
                });
            SceneEntityAsset cameraEntity = CreateCameraEntity("character_steps.camera", new float3(9f, 5.5f, 7f), CreateYawPitchRollDegrees(-138.0, -20.0, 0.0));
            return CreateSceneAsset(PhysicsValidationSceneCatalog.CharacterStepsSceneId, cameraEntity, scenarioEntity);
        }

        /// <summary>
        /// Creates the character moving-platform validation scene.
        /// </summary>
        /// <returns>Authored moving-platform validation scene asset.</returns>
        SceneAsset CreateCharacterMovingPlatformScene() {
            SceneEntityAsset scenarioEntity = CreateScenarioRoot(
                "character_moving_platform.scenario",
                new[] {
                    CreatePhysicsBoxMeshEntity("character_moving_platform.ground", "Ground", new float3(0f, -0.5f, 0f), new float3(18f, 1f, 14f), float4.Identity, StaticBodyKindCode, false),
                    CreatePhysicsBoxMeshEntity("character_moving_platform.gap_a", "GapEdgeA", new float3(-1.75f, 0.25f, 0f), new float3(4f, 0.5f, 4f), float4.Identity, StaticBodyKindCode, false),
                    CreatePhysicsBoxMeshEntity("character_moving_platform.gap_b", "GapEdgeB", new float3(4.75f, 0.25f, 0f), new float3(4f, 0.5f, 4f), float4.Identity, StaticBodyKindCode, false),
                    CreateKinematicPhysicsBoxMeshEntity(
                        "character_moving_platform.platform",
                        "MovingPlatform",
                        new float3(-0.5f, 0.75f, 0f),
                        new float3(2.5f, 0.35f, 2.5f),
                        float4.Identity,
                        new float3(-0.5f, 0.75f, 0f),
                        new float3(3.5f, 0.75f, 0f),
                        2d,
                        true),
                    CreateMarkerEntity("character_moving_platform.platform_start", "PlatformStart", new float3(-0.5f, 0.75f, 0f)),
                    CreateMarkerEntity("character_moving_platform.platform_end", "PlatformEnd", new float3(3.5f, 0.75f, 0f)),
                    CreateMarkerEntity("character_moving_platform.spawn", "ControllerSpawn", new float3(-5f, 0.75f, 0f))
                });
            SceneEntityAsset cameraEntity = CreateCameraEntity("character_moving_platform.camera", new float3(10f, 5.75f, 8f), CreateYawPitchRollDegrees(-140.0, -18.0, 0.0));
            return CreateSceneAsset(PhysicsValidationSceneCatalog.CharacterMovingPlatformSceneId, cameraEntity, scenarioEntity);
        }

        /// <summary>
        /// Creates the stacked dynamic-body validation scene.
        /// </summary>
        /// <returns>Authored stacked-box validation scene asset.</returns>
        SceneAsset CreateDynamicStackBoxesScene() {
            SceneEntityAsset scenarioEntity = CreateScenarioRoot(
                "dynamic_stack_boxes.scenario",
                new[] {
                    CreatePhysicsBoxMeshEntity("dynamic_stack_boxes.ground", "Ground", new float3(0f, -0.5f, 0f), new float3(14f, 1f, 14f), float4.Identity, StaticBodyKindCode, false),
                    CreatePhysicsBoxMeshEntity("dynamic_stack_boxes.box01", "StackBox01", new float3(0f, 0.5f, 0f), new float3(1f, 1f, 1f), float4.Identity, DynamicBodyKindCode, true),
                    CreatePhysicsBoxMeshEntity("dynamic_stack_boxes.box02", "StackBox02", new float3(0f, 1.5f, 0f), new float3(1f, 1f, 1f), float4.Identity, DynamicBodyKindCode, true),
                    CreatePhysicsBoxMeshEntity("dynamic_stack_boxes.box03", "StackBox03", new float3(0f, 2.5f, 0f), new float3(1f, 1f, 1f), float4.Identity, DynamicBodyKindCode, true),
                    CreatePhysicsBoxMeshEntity("dynamic_stack_boxes.box04", "StackBox04", new float3(0f, 3.5f, 0f), new float3(1f, 1f, 1f), float4.Identity, DynamicBodyKindCode, true),
                    CreateMarkerEntity("dynamic_stack_boxes.spawn", "DynamicSpawn", new float3(-2.5f, 1.5f, 0f))
                });
            SceneEntityAsset cameraEntity = CreateCameraEntity("dynamic_stack_boxes.camera", new float3(8f, 5.25f, 8f), CreateYawPitchRollDegrees(-135.0, -20.0, 0.0));
            return CreateSceneAsset(PhysicsValidationSceneCatalog.DynamicStackBoxesSceneId, cameraEntity, scenarioEntity);
        }

        /// <summary>
        /// Creates the sphere-ramp validation scene.
        /// </summary>
        /// <returns>Authored sphere-ramp validation scene asset.</returns>
        SceneAsset CreateDynamicSphereRampScene() {
            SceneEntityAsset scenarioEntity = CreateScenarioRoot(
                "dynamic_sphere_ramp.scenario",
                new[] {
                    CreateCubeMeshEntity("dynamic_sphere_ramp.ground", "Ground", new float3(0f, -0.5f, 0f), new float3(16f, 1f, 14f), float4.Identity),
                    CreateCubeMeshEntity("dynamic_sphere_ramp.ramp", "Ramp", new float3(2.5f, 0.8f, 0f), new float3(6f, 0.6f, 4f), CreateYawPitchRollDegrees(0.0, 0.0, -16.0)),
                    CreateMarkerEntity("dynamic_sphere_ramp.spawn", "SphereSpawn", new float3(-3.5f, 1.5f, 0f)),
                    CreateMarkerEntity("dynamic_sphere_ramp.goal", "RampGoal", new float3(5.5f, 1.75f, 0f))
                });
            SceneEntityAsset cameraEntity = CreateCameraEntity("dynamic_sphere_ramp.camera", new float3(9.5f, 5.5f, 8.5f), CreateYawPitchRollDegrees(-138.0, -18.0, 0.0));
            return CreateSceneAsset(PhysicsValidationSceneCatalog.DynamicSphereRampSceneId, cameraEntity, scenarioEntity);
        }

        /// <summary>
        /// Creates the kinematic push validation scene.
        /// </summary>
        /// <returns>Authored kinematic push validation scene asset.</returns>
        SceneAsset CreateKinematicPushScene() {
            SceneEntityAsset scenarioEntity = CreateScenarioRoot(
                "kinematic_push.scenario",
                new[] {
                    CreatePhysicsBoxMeshEntity("kinematic_push.ground", "Ground", new float3(0f, -0.5f, 0f), new float3(16f, 1f, 12f), float4.Identity, StaticBodyKindCode, false),
                    CreatePhysicsBoxMeshEntity("kinematic_push.block", "DynamicTarget", new float3(1.5f, 0.5f, 0f), new float3(1f, 1f, 1f), float4.Identity, DynamicBodyKindCode, true),
                    CreateKinematicPhysicsBoxMeshEntity(
                        "kinematic_push.pusher",
                        "KinematicPusher",
                        new float3(-2f, 0.5f, 0f),
                        new float3(1.5f, 1f, 1.5f),
                        float4.Identity,
                        new float3(-2f, 0.5f, 0f),
                        new float3(0.5f, 0.5f, 0f),
                        1d,
                        true),
                    CreateMarkerEntity("kinematic_push.start", "PusherStart", new float3(-3.5f, 0.5f, 0f)),
                    CreateMarkerEntity("kinematic_push.end", "PusherEnd", new float3(0.5f, 0.5f, 0f)),
                    CreateMarkerEntity("kinematic_push.dynamic_spawn", "DynamicSpawn", new float3(1.5f, 0.5f, 0f))
                });
            SceneEntityAsset cameraEntity = CreateCameraEntity("kinematic_push.camera", new float3(8.5f, 4.75f, 7.25f), CreateYawPitchRollDegrees(-135.0, -16.0, 0.0));
            return CreateSceneAsset(PhysicsValidationSceneCatalog.KinematicPushSceneId, cameraEntity, scenarioEntity);
        }

        /// <summary>
        /// Creates the static-mesh ground stability validation scene.
        /// </summary>
        /// <returns>Authored static-ground stability validation scene asset.</returns>
        SceneAsset CreateMeshGroundStabilityScene() {
            SceneEntityAsset scenarioEntity = CreateScenarioRoot(
                "mesh_ground_stability.scenario",
                new[] {
                    CreateCubeMeshEntity("mesh_ground_stability.base", "GroundBase", new float3(0f, -0.5f, 0f), new float3(20f, 1f, 14f), float4.Identity),
                    CreateCubeMeshEntity("mesh_ground_stability.section01", "StaticMeshGround01", new float3(-2.5f, 0.15f, 0f), new float3(3f, 0.3f, 4f), float4.Identity),
                    CreateCubeMeshEntity("mesh_ground_stability.section02", "StaticMeshGround02", new float3(0.5f, 0.35f, 0f), new float3(3f, 0.7f, 4f), float4.Identity),
                    CreateCubeMeshEntity("mesh_ground_stability.section03", "StaticMeshGround03", new float3(3.5f, 0.2f, 0f), new float3(3f, 0.4f, 4f), CreateYawPitchRollDegrees(0.0, 0.0, -6.0)),
                    CreateCubeMeshEntity("mesh_ground_stability.section04", "StaticMeshGround04", new float3(6.5f, 0.45f, 0f), new float3(3f, 0.9f, 4f), CreateYawPitchRollDegrees(0.0, 0.0, 5.0)),
                    CreateMarkerEntity("mesh_ground_stability.spawn", "WalkerSpawn", new float3(-5.5f, 0.75f, 0f))
                });
            SceneEntityAsset cameraEntity = CreateCameraEntity("mesh_ground_stability.camera", new float3(11f, 6f, 8.5f), CreateYawPitchRollDegrees(-140.0, -18.0, 0.0));
            return CreateSceneAsset(PhysicsValidationSceneCatalog.MeshGroundStabilitySceneId, cameraEntity, scenarioEntity);
        }

        /// <summary>
        /// Creates the trigger-volume validation scene.
        /// </summary>
        /// <returns>Authored trigger-volume validation scene asset.</returns>
        SceneAsset CreateTriggerVolumeScene() {
            SceneEntityAsset scenarioEntity = CreateScenarioRoot(
                "trigger_volume.scenario",
                new[] {
                    CreateCubeMeshEntity("trigger_volume.ground", "Ground", new float3(0f, -0.5f, 0f), new float3(18f, 1f, 12f), float4.Identity),
                    CreateCubeMeshEntity("trigger_volume.arch", "TriggerVolume", new float3(1.5f, 1.5f, 0f), new float3(2.5f, 3f, 2.5f), float4.Identity),
                    CreateMarkerEntity("trigger_volume.start", "PlayerPathStart", new float3(-5f, 0.75f, 0f)),
                    CreateMarkerEntity("trigger_volume.end", "PlayerPathEnd", new float3(5.5f, 0.75f, 0f))
                });
            SceneEntityAsset cameraEntity = CreateCameraEntity("trigger_volume.camera", new float3(9.5f, 5f, 7.5f), CreateYawPitchRollDegrees(-136.0, -18.0, 0.0));
            return CreateSceneAsset(PhysicsValidationSceneCatalog.TriggerVolumeSceneId, cameraEntity, scenarioEntity);
        }

        /// <summary>
        /// Creates the final scene asset wrapper shared by every validation scenario.
        /// </summary>
        /// <param name="sceneId">Stable relative scene id.</param>
        /// <param name="cameraEntity">Root camera entity.</param>
        /// <param name="scenarioEntity">Root scenario entity.</param>
        /// <returns>Scene asset ready for serialization.</returns>
        static SceneAsset CreateSceneAsset(
            string sceneId,
            SceneEntityAsset cameraEntity,
            SceneEntityAsset scenarioEntity) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }
            if (cameraEntity == null) {
                throw new ArgumentNullException(nameof(cameraEntity));
            }
            if (scenarioEntity == null) {
                throw new ArgumentNullException(nameof(scenarioEntity));
            }

            return new SceneAsset {
                Id = sceneId,
                AssetReferences = CreateAssetReferences(),
                RootEntities = new[] { cameraEntity, scenarioEntity }
            };
        }

        /// <summary>
        /// Creates the scenario root entity that owns the authored test geometry and markers.
        /// </summary>
        /// <param name="entityId">Stable serialized entity id.</param>
        /// <param name="children">Authored scenario children.</param>
        /// <returns>Scenario root entity.</returns>
        static SceneEntityAsset CreateScenarioRoot(string entityId, SceneEntityAsset[] children) {
            if (string.IsNullOrWhiteSpace(entityId)) {
                throw new ArgumentException("Scenario entity id must be provided.", nameof(entityId));
            }
            if (children == null) {
                throw new ArgumentNullException(nameof(children));
            }

            return new SceneEntityAsset {
                Id = entityId,
                Name = "Scenario",
                LocalPosition = float3.Zero,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = Array.Empty<SceneComponentAssetRecord>(),
                Children = children
            };
        }

        /// <summary>
        /// Creates one camera root entity for a validation scene.
        /// </summary>
        /// <param name="entityId">Stable serialized entity id.</param>
        /// <param name="position">Camera position.</param>
        /// <param name="orientation">Camera orientation.</param>
        /// <returns>Camera entity with a serialized camera component.</returns>
        static SceneEntityAsset CreateCameraEntity(string entityId, float3 position, float4 orientation) {
            if (string.IsNullOrWhiteSpace(entityId)) {
                throw new ArgumentException("Camera entity id must be provided.", nameof(entityId));
            }

            return new SceneEntityAsset {
                Id = entityId,
                Name = "Camera",
                LocalPosition = position,
                LocalScale = float3.One,
                LocalOrientation = orientation,
                Components = new[] { CreateCameraComponentRecord() },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates one mesh-backed cube entity for the validation scene.
        /// </summary>
        /// <param name="entityId">Stable serialized entity id.</param>
        /// <param name="name">Authored entity name.</param>
        /// <param name="position">Entity position.</param>
        /// <param name="scale">Entity scale.</param>
        /// <param name="orientation">Entity orientation.</param>
        /// <returns>Mesh-backed entity.</returns>
        static SceneEntityAsset CreateCubeMeshEntity(
            string entityId,
            string name,
            float3 position,
            float3 scale,
            float4 orientation) {
            if (string.IsNullOrWhiteSpace(entityId)) {
                throw new ArgumentException("Mesh entity id must be provided.", nameof(entityId));
            }
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Mesh entity name must be provided.", nameof(name));
            }

            return new SceneEntityAsset {
                Id = entityId,
                Name = name,
                LocalPosition = position,
                LocalScale = scale,
                LocalOrientation = orientation,
                Components = new[] { CreateMeshComponentRecord() },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates one mesh-backed box entity that also carries serialized 3D physics records.
        /// </summary>
        /// <param name="entityId">Stable serialized entity id.</param>
        /// <param name="name">Authored entity name.</param>
        /// <param name="position">Entity position.</param>
        /// <param name="scale">Entity scale and collider size.</param>
        /// <param name="orientation">Entity orientation.</param>
        /// <param name="bodyKindCode">Rigid-body participation mode byte to serialize.</param>
        /// <param name="useGravity">True when the serialized rigid body should receive gravity.</param>
        /// <returns>Mesh-backed entity with serialized rigid-body and box-collider records.</returns>
        static SceneEntityAsset CreatePhysicsBoxMeshEntity(
            string entityId,
            string name,
            float3 position,
            float3 scale,
            float4 orientation,
            byte bodyKindCode,
            bool useGravity) {
            if (string.IsNullOrWhiteSpace(entityId)) {
                throw new ArgumentException("Physics entity id must be provided.", nameof(entityId));
            }
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Physics entity name must be provided.", nameof(name));
            }

            return new SceneEntityAsset {
                Id = entityId,
                Name = name,
                LocalPosition = position,
                LocalScale = scale,
                LocalOrientation = orientation,
                Components = new[] {
                    CreateMeshComponentRecord(),
                    CreateRigidBodyComponentRecord(bodyKindCode, useGravity, 1d, 1d, float3.Zero, 1),
                    CreateBoxColliderComponentRecord(scale, 2)
                },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates one mesh-backed box entity that also carries serialized 3D kinematic-motion records.
        /// </summary>
        /// <param name="entityId">Stable serialized entity id.</param>
        /// <param name="name">Authored entity name.</param>
        /// <param name="position">Entity position.</param>
        /// <param name="scale">Entity scale and collider size.</param>
        /// <param name="orientation">Entity orientation.</param>
        /// <param name="startLocalPosition">Kinematic motion start position.</param>
        /// <param name="endLocalPosition">Kinematic motion end position.</param>
        /// <param name="travelDurationSeconds">One-way travel duration in seconds.</param>
        /// <param name="pingPong">True when the motion should reverse at the end.</param>
        /// <returns>Mesh-backed entity with serialized rigid-body, box-collider, and kinematic-motion records.</returns>
        static SceneEntityAsset CreateKinematicPhysicsBoxMeshEntity(
            string entityId,
            string name,
            float3 position,
            float3 scale,
            float4 orientation,
            float3 startLocalPosition,
            float3 endLocalPosition,
            double travelDurationSeconds,
            bool pingPong) {
            if (string.IsNullOrWhiteSpace(entityId)) {
                throw new ArgumentException("Physics entity id must be provided.", nameof(entityId));
            }
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Physics entity name must be provided.", nameof(name));
            }

            return new SceneEntityAsset {
                Id = entityId,
                Name = name,
                LocalPosition = position,
                LocalScale = scale,
                LocalOrientation = orientation,
                Components = new[] {
                    CreateMeshComponentRecord(),
                    CreateRigidBodyComponentRecord(KinematicBodyKindCode, false, 1d, 1d, float3.Zero, 1),
                    CreateBoxColliderComponentRecord(scale, 2),
                    CreateKinematicMotionComponentRecord(startLocalPosition, endLocalPosition, travelDurationSeconds, pingPong, 3)
                },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates one mesh-backed box entity that carries serialized 3D character-controller records.
        /// </summary>
        /// <param name="entityId">Stable serialized entity id.</param>
        /// <param name="name">Authored entity name.</param>
        /// <param name="position">Entity position.</param>
        /// <param name="scale">Entity scale and collider size.</param>
        /// <param name="orientation">Entity orientation.</param>
        /// <param name="desiredMoveDirection">Desired local move direction used by the controller.</param>
        /// <param name="moveSpeed">Horizontal move speed in world units per second.</param>
        /// <param name="gravityScale">Gravity multiplier used by the controller.</param>
        /// <param name="stepHeight">Maximum upward snap height used while climbing support surfaces.</param>
        /// <param name="groundSnapDistance">Maximum downward snap distance used to keep the controller grounded.</param>
        /// <returns>Mesh-backed entity with serialized box-collider and character-controller records.</returns>
        static SceneEntityAsset CreateCharacterControllerBoxMeshEntity(
            string entityId,
            string name,
            float3 position,
            float3 scale,
            float4 orientation,
            float3 desiredMoveDirection,
            double moveSpeed,
            double gravityScale,
            double stepHeight,
            double groundSnapDistance) {
            if (string.IsNullOrWhiteSpace(entityId)) {
                throw new ArgumentException("Character controller entity id must be provided.", nameof(entityId));
            }
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Character controller entity name must be provided.", nameof(name));
            }

            return new SceneEntityAsset {
                Id = entityId,
                Name = name,
                LocalPosition = position,
                LocalScale = scale,
                LocalOrientation = orientation,
                Components = new[] {
                    CreateMeshComponentRecord(),
                    CreateBoxColliderComponentRecord(scale, 1),
                    CreateCharacterControllerComponentRecord(desiredMoveDirection, moveSpeed, gravityScale, stepHeight, groundSnapDistance, 2)
                },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates one empty marker entity used as a future spawn, target, or motion reference.
        /// </summary>
        /// <param name="entityId">Stable serialized entity id.</param>
        /// <param name="name">Authored entity name.</param>
        /// <param name="position">Marker position.</param>
        /// <returns>Marker entity without components.</returns>
        static SceneEntityAsset CreateMarkerEntity(string entityId, string name, float3 position) {
            if (string.IsNullOrWhiteSpace(entityId)) {
                throw new ArgumentException("Marker entity id must be provided.", nameof(entityId));
            }
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Marker entity name must be provided.", nameof(name));
            }

            return new SceneEntityAsset {
                Id = entityId,
                Name = name,
                LocalPosition = position,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = Array.Empty<SceneComponentAssetRecord>(),
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates the shared generated-asset reference list used by validation scene mesh components.
        /// </summary>
        /// <returns>Stable generated asset reference list.</returns>
        static SceneAssetReference[] CreateAssetReferences() {
            return new[] {
                CreateGeneratedReference(EngineGeneratedAssetProvider.CubeRelativePath, EngineGeneratedModelCache.CubeAssetId),
                CreateGeneratedReference(EngineGeneratedAssetProvider.StandardMaterialRelativePath, EngineGeneratedMaterialCache.StandardAssetId)
            };
        }

        /// <summary>
        /// Creates one generated asset reference.
        /// </summary>
        /// <param name="relativePath">Generated asset relative path.</param>
        /// <param name="assetId">Stable generated asset id.</param>
        /// <returns>Generated scene asset reference.</returns>
        static SceneAssetReference CreateGeneratedReference(string relativePath, string assetId) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }
            if (string.IsNullOrWhiteSpace(assetId)) {
                throw new ArgumentException("Asset id must be provided.", nameof(assetId));
            }

            return new SceneAssetReference {
                SourceKind = GeneratedSourceKind,
                RelativePath = relativePath,
                ProviderId = GeneratedProviderId,
                AssetId = assetId
            };
        }

        /// <summary>
        /// Creates one serialized mesh component record that references the generated cube model and standard material.
        /// </summary>
        /// <returns>Serialized mesh component record.</returns>
        static SceneComponentAssetRecord CreateMeshComponentRecord() {
            SceneAssetReference modelReference = CreateGeneratedReference(EngineGeneratedAssetProvider.CubeRelativePath, EngineGeneratedModelCache.CubeAssetId);
            SceneAssetReference materialReference = CreateGeneratedReference(EngineGeneratedAssetProvider.StandardMaterialRelativePath, EngineGeneratedMaterialCache.StandardAssetId);

            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            WriteOptionalReference(writer, modelReference);
            WriteOptionalReference(writer, materialReference);
            writer.WriteByte(DefaultMeshRenderOrder);

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.MeshComponent",
                ComponentIndex = 0,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Creates one serialized camera component record using the editor scene-object layer mask.
        /// </summary>
        /// <returns>Serialized camera component record.</returns>
        static SceneComponentAssetRecord CreateCameraComponentRecord() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(2);
            writer.WriteByte(DefaultCameraDrawOrder);
            writer.WriteUInt16(EditorLayerMasks.SceneObjects);
            WriteFloat4(writer, new float4(0f, 0f, 1f, 1f));
            writer.WriteByte(1);
            WriteFloat4(writer, new float4(0f, 0f, 0f, 0f));
            writer.WriteByte(1);
            writer.WriteSingle(1f);
            writer.WriteByte(0);
            writer.WriteByte(0);
            writer.WriteByte((byte)DepthPrepassMode.Disabled);
            writer.WriteSingle(0f);
            writer.WriteByte((byte)PostProcessTier.Disabled);

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.CameraComponent",
                ComponentIndex = 0,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Creates one serialized rigid-body component record.
        /// </summary>
        /// <param name="bodyKindCode">Rigid-body participation mode byte to serialize.</param>
        /// <param name="useGravity">True when gravity should be enabled.</param>
        /// <param name="mass">Serialized authored mass value.</param>
        /// <param name="gravityScale">Serialized authored gravity scale.</param>
        /// <param name="linearVelocity">Serialized authored linear velocity.</param>
        /// <param name="componentIndex">Entity-local component order index.</param>
        /// <returns>Serialized rigid-body component record.</returns>
        static SceneComponentAssetRecord CreateRigidBodyComponentRecord(
            byte bodyKindCode,
            bool useGravity,
            double mass,
            double gravityScale,
            float3 linearVelocity,
            int componentIndex) {
            if (componentIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(componentIndex), "Component index must be non-negative.");
            }

            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(RigidBodyComponentPayloadVersion);
            writer.WriteByte(bodyKindCode);
            writer.WriteByte(useGravity ? (byte)1 : (byte)0);
            writer.WriteSingle((float)mass);
            writer.WriteSingle((float)gravityScale);
            writer.WriteFloat3(linearVelocity);

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.RigidBody3DComponent",
                ComponentIndex = componentIndex,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Creates one serialized box-collider component record.
        /// </summary>
        /// <param name="size">Serialized authored full collider size.</param>
        /// <param name="componentIndex">Entity-local component order index.</param>
        /// <returns>Serialized box-collider component record.</returns>
        static SceneComponentAssetRecord CreateBoxColliderComponentRecord(float3 size, int componentIndex) {
            if (componentIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(componentIndex), "Component index must be non-negative.");
            }

            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(BoxColliderComponentPayloadVersion);
            writer.WriteFloat3(size);

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.BoxCollider3DComponent",
                ComponentIndex = componentIndex,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Creates one serialized kinematic-motion component record.
        /// </summary>
        /// <param name="startLocalPosition">Motion path start position.</param>
        /// <param name="endLocalPosition">Motion path end position.</param>
        /// <param name="travelDurationSeconds">One-way travel duration in seconds.</param>
        /// <param name="pingPong">True when the motion should reverse at the end.</param>
        /// <param name="componentIndex">Entity-local component order index.</param>
        /// <returns>Serialized kinematic-motion component record.</returns>
        static SceneComponentAssetRecord CreateKinematicMotionComponentRecord(
            float3 startLocalPosition,
            float3 endLocalPosition,
            double travelDurationSeconds,
            bool pingPong,
            int componentIndex) {
            if (componentIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(componentIndex), "Component index must be non-negative.");
            }
            if (double.IsNaN(travelDurationSeconds) || double.IsInfinity(travelDurationSeconds) || travelDurationSeconds <= 0d) {
                throw new ArgumentOutOfRangeException(nameof(travelDurationSeconds), "Travel duration must be a finite value greater than zero.");
            }

            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(KinematicMotionComponentPayloadVersion);
            writer.WriteFloat3(startLocalPosition);
            writer.WriteFloat3(endLocalPosition);
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(travelDurationSeconds));
            writer.WriteByte(pingPong ? (byte)1 : (byte)0);

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.KinematicMotion3DComponent",
                ComponentIndex = componentIndex,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Creates one serialized character-controller component record.
        /// </summary>
        /// <param name="desiredMoveDirection">Desired planar move direction.</param>
        /// <param name="moveSpeed">Horizontal move speed in world units per second.</param>
        /// <param name="gravityScale">Gravity multiplier used by the controller.</param>
        /// <param name="stepHeight">Maximum upward snap height used while climbing support surfaces.</param>
        /// <param name="groundSnapDistance">Maximum downward snap distance used to keep the controller grounded.</param>
        /// <param name="componentIndex">Entity-local component order index.</param>
        /// <returns>Serialized character-controller component record.</returns>
        static SceneComponentAssetRecord CreateCharacterControllerComponentRecord(
            float3 desiredMoveDirection,
            double moveSpeed,
            double gravityScale,
            double stepHeight,
            double groundSnapDistance,
            int componentIndex) {
            if (componentIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(componentIndex), "Component index must be non-negative.");
            }
            if (double.IsNaN(moveSpeed) || double.IsInfinity(moveSpeed) || moveSpeed < 0d) {
                throw new ArgumentOutOfRangeException(nameof(moveSpeed), "Move speed must be a finite value greater than or equal to zero.");
            }
            if (double.IsNaN(gravityScale) || double.IsInfinity(gravityScale) || gravityScale < 0d) {
                throw new ArgumentOutOfRangeException(nameof(gravityScale), "Gravity scale must be a finite value greater than or equal to zero.");
            }
            if (double.IsNaN(stepHeight) || double.IsInfinity(stepHeight) || stepHeight < 0d) {
                throw new ArgumentOutOfRangeException(nameof(stepHeight), "Step height must be a finite value greater than or equal to zero.");
            }
            if (double.IsNaN(groundSnapDistance) || double.IsInfinity(groundSnapDistance) || groundSnapDistance < 0d) {
                throw new ArgumentOutOfRangeException(nameof(groundSnapDistance), "Ground snap distance must be a finite value greater than or equal to zero.");
            }

            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(CharacterControllerComponentPayloadVersion);
            writer.WriteFloat3(desiredMoveDirection);
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(moveSpeed));
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(gravityScale));
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(stepHeight));
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(groundSnapDistance));

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.CharacterController3DComponent",
                ComponentIndex = componentIndex,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Writes one optional scene asset reference into a component payload.
        /// </summary>
        /// <param name="writer">Destination writer receiving the payload.</param>
        /// <param name="reference">Reference to serialize.</param>
        static void WriteOptionalReference(EngineBinaryWriter writer, SceneAssetReference reference) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            writer.WriteByte(1);
            writer.WriteInt32((int)reference.SourceKind);
            writer.WriteString(reference.RelativePath);
            writer.WriteString(reference.ProviderId);
            writer.WriteString(reference.AssetId);
        }

        /// <summary>
        /// Writes one `float4` payload into a binary component stream.
        /// </summary>
        /// <param name="writer">Destination writer receiving the payload.</param>
        /// <param name="value">Vector value to write.</param>
        static void WriteFloat4(EngineBinaryWriter writer, float4 value) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.WriteSingle(value.X);
            writer.WriteSingle(value.Y);
            writer.WriteSingle(value.Z);
            writer.WriteSingle(value.W);
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
        /// Resolves the absolute output path for one relative physics validation scene id.
        /// </summary>
        /// <param name="projectRootPath">Absolute project root path.</param>
        /// <param name="sceneId">Relative scene id stored in the asset.</param>
        /// <returns>Absolute output file path.</returns>
        static string GetSceneFullPath(string projectRootPath, string sceneId) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }

            string relativePath = sceneId.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(projectRootPath, "assets", relativePath);
        }
    }
}
