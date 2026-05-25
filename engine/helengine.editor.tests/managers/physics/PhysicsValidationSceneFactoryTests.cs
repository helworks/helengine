using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.physics {
    /// <summary>
    /// Verifies exportable physics validation scenes can be authored as normal `.helen` assets.
    /// </summary>
    public sealed class PhysicsValidationSceneFactoryTests : IDisposable {
        /// <summary>
        /// Temporary project root used for generated physics scene outputs.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Initializes a temporary project root for physics scene generation tests.
        /// </summary>
        public PhysicsValidationSceneFactoryTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-physics-validation-scene-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets"));
        }

        /// <summary>
        /// Removes temporary generated project data after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Resolves the packaged scene file path for one authored scene inside the supplied build output root.
        /// </summary>
        /// <param name="buildRootPath">Build output root that contains packaged scene assets.</param>
        /// <param name="sceneId">Authored scene id whose packaged output should be resolved.</param>
        /// <returns>Absolute packaged scene file path for the authored scene.</returns>
        static string GetPackagedScenePath(string buildRootPath, string sceneId) {
            if (string.IsNullOrWhiteSpace(buildRootPath)) {
                throw new ArgumentException("Build root path must be provided.", nameof(buildRootPath));
            }
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }

            return Path.Combine(
                buildRootPath,
                PackagedScenePathResolver.BuildRelativePath(sceneId, 0).Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// Ensures the scene catalog exposes every planned physics validation scenario in a stable order.
        /// </summary>
        [Fact]
        public void GetSceneIds_ReturnsStablePhysicsScenarioList() {
            string[] sceneIds = PhysicsValidationSceneCatalog.GetSceneIds();

            Assert.Equal(new[] {
                "scenes/physics/test_scene_character_slope.helen",
                "scenes/physics/test_scene_character_steps.helen",
                "scenes/physics/test_scene_character_moving_platform.helen",
                "scenes/physics/test_scene_dynamic_stack_boxes.helen",
                "scenes/physics/test_scene_dynamic_sphere_stack.helen",
                "scenes/physics/test_scene_dynamic_mixed_stack.helen",
                "scenes/physics/test_scene_kinematic_push.helen",
                "scenes/physics/test_scene_mesh_ground_stability.helen",
                "scenes/physics/test_scene_trigger_volume.helen"
            }, sceneIds);
        }

        /// <summary>
        /// Ensures the factory writes every physics validation scene as a normal project asset under the requested physics folder.
        /// </summary>
        [Fact]
        public void WriteScenes_WritesAllPhysicsScenarioScenesUnderPhysicsFolder() {
            PhysicsValidationSceneFactory factory = new PhysicsValidationSceneFactory();

            factory.WriteScenes(TempProjectRootPath);

            string[] sceneIds = PhysicsValidationSceneCatalog.GetSceneIds();
            for (int index = 0; index < sceneIds.Length; index++) {
                string sceneId = sceneIds[index];
                string fullPath = GetSceneFullPath(TempProjectRootPath, sceneId);

                Assert.True(File.Exists(fullPath));

                using FileStream stream = File.OpenRead(fullPath);
                SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));

                Assert.Equal(sceneId, sceneAsset.Id);
                Assert.Contains(sceneAsset.RootEntities, entity => string.Equals(entity.Name, "Camera", StringComparison.Ordinal));
                Assert.Contains(sceneAsset.RootEntities, entity => string.Equals(entity.Name, "Scenario", StringComparison.Ordinal));
            }
        }

        /// <summary>
        /// Ensures one generated scenario contains the expected authored layout markers for the stacked-box validation case.
        /// </summary>
        [Fact]
        public void CreateSceneAsset_ForDynamicStackBoxes_CreatesCameraAndNamedScenarioEntities() {
            PhysicsValidationSceneFactory factory = new PhysicsValidationSceneFactory();

            SceneAsset sceneAsset = factory.CreateSceneAsset(PhysicsValidationSceneCatalog.DynamicStackBoxesSceneId);

            Assert.Equal(PhysicsValidationSceneCatalog.DynamicStackBoxesSceneId, sceneAsset.Id);
            Assert.NotEmpty(sceneAsset.AssetReferences);

            SceneEntityAsset cameraEntity = FindRootEntity(sceneAsset, "Camera");
            SceneEntityAsset scenarioEntity = FindRootEntity(sceneAsset, "Scenario");

            Assert.Contains(cameraEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.CameraComponent", StringComparison.Ordinal));
            Assert.Contains(scenarioEntity.Children, entity => string.Equals(entity.Name, "StackBox01", StringComparison.Ordinal));
            Assert.Contains(scenarioEntity.Children, entity => string.Equals(entity.Name, "StackBox04", StringComparison.Ordinal));
            Assert.Contains(scenarioEntity.Children, entity => string.Equals(entity.Name, "DynamicSpawn", StringComparison.Ordinal));
        }

        /// <summary>
        /// Ensures the stack-box validation scene includes serialized rigid-body and box-collider records for the bodies the current runtime can simulate.
        /// </summary>
        [Fact]
        public void CreateSceneAsset_ForDynamicStackBoxes_WritesPhysicsRecordsForGroundAndStackBodies() {
            PhysicsValidationSceneFactory factory = new PhysicsValidationSceneFactory();

            SceneAsset sceneAsset = factory.CreateSceneAsset(PhysicsValidationSceneCatalog.DynamicStackBoxesSceneId);
            SceneEntityAsset scenarioEntity = FindRootEntity(sceneAsset, "Scenario");
            SceneEntityAsset groundEntity = FindChildEntity(scenarioEntity, "Ground");
            SceneEntityAsset firstStackBoxEntity = FindChildEntity(scenarioEntity, "StackBox01");

            Assert.Contains(groundEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.RigidBody3DComponent", StringComparison.Ordinal));
            Assert.Contains(groundEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.BoxCollider3DComponent", StringComparison.Ordinal));
            Assert.Contains(firstStackBoxEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.RigidBody3DComponent", StringComparison.Ordinal));
            Assert.Contains(firstStackBoxEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.BoxCollider3DComponent", StringComparison.Ordinal));
        }

        /// <summary>
        /// Ensures the sphere-stack validation scene includes serialized physics records for its static floor and dynamic stacked spheres.
        /// </summary>
        [Fact]
        public void CreateSceneAsset_ForDynamicSphereStack_WritesPhysicsRecordsForGroundAndStackedSpheres() {
            PhysicsValidationSceneFactory factory = new PhysicsValidationSceneFactory();

            SceneAsset sceneAsset = factory.CreateSceneAsset(PhysicsValidationSceneCatalog.DynamicSphereStackSceneId);
            SceneEntityAsset cameraEntity = FindRootEntity(sceneAsset, "Camera");
            SceneEntityAsset scenarioEntity = FindRootEntity(sceneAsset, "Scenario");
            SceneEntityAsset groundEntity = FindChildEntity(scenarioEntity, "Ground");
            SceneEntityAsset firstSphereEntity = FindChildEntity(scenarioEntity, "StackSphere01");
            SceneEntityAsset eighthSphereEntity = FindChildEntity(scenarioEntity, "StackSphere08");
            SceneAssetReference firstSphereMaterialReference = ReadMaterialReference(firstSphereEntity);
            SceneAssetReference eighthSphereMaterialReference = ReadMaterialReference(eighthSphereEntity);
            float3 cameraForward = float4.RotateVector(new float3(0f, 0f, -1f), cameraEntity.LocalOrientation);
            float3 cameraToStack = float3.Normalize(new float3(0f, 4f, 0f) - cameraEntity.LocalPosition);

            Assert.True(float3.Dot(cameraForward, cameraToStack) > 0.95f, "The sphere-stack camera should look toward the stacked sphere volume.");
            Assert.Contains(groundEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.RigidBody3DComponent", StringComparison.Ordinal));
            Assert.Contains(groundEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.BoxCollider3DComponent", StringComparison.Ordinal));
            Assert.DoesNotContain(scenarioEntity.Children, entity => string.Equals(entity.Name, "Ramp", StringComparison.Ordinal));
            Assert.Contains(firstSphereEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.MeshComponent", StringComparison.Ordinal));
            Assert.Contains(firstSphereEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.RigidBody3DComponent", StringComparison.Ordinal));
            Assert.Contains(firstSphereEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.SphereCollider3DComponent", StringComparison.Ordinal));
            Assert.Contains(eighthSphereEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.SphereCollider3DComponent", StringComparison.Ordinal));
            Assert.Equal(SceneAssetReferenceSourceKind.FileSystem, firstSphereMaterialReference.SourceKind);
            Assert.Equal(SceneAssetReferenceSourceKind.FileSystem, eighthSphereMaterialReference.SourceKind);
            Assert.NotEqual(firstSphereMaterialReference.RelativePath, eighthSphereMaterialReference.RelativePath);
        }

        /// <summary>
        /// Ensures the mixed dynamic validation scene includes dynamic boxes and spheres so primitive cross-shape contacts can be inspected together.
        /// </summary>
        [Fact]
        public void CreateSceneAsset_ForDynamicMixedStack_WritesBoxAndSpherePhysicsRecords() {
            PhysicsValidationSceneFactory factory = new PhysicsValidationSceneFactory();

            SceneAsset sceneAsset = factory.CreateSceneAsset(PhysicsValidationSceneCatalog.DynamicMixedStackSceneId);
            SceneEntityAsset cameraEntity = FindRootEntity(sceneAsset, "Camera");
            SceneEntityAsset scenarioEntity = FindRootEntity(sceneAsset, "Scenario");
            SceneEntityAsset groundEntity = FindChildEntity(scenarioEntity, "Ground");
            SceneEntityAsset firstBoxEntity = FindChildEntity(scenarioEntity, "StackBox01");
            SceneEntityAsset firstSphereEntity = FindChildEntity(scenarioEntity, "StackSphere01");
            SceneAssetReference firstBoxMaterialReference = ReadMaterialReference(firstBoxEntity);
            SceneAssetReference firstSphereMaterialReference = ReadMaterialReference(firstSphereEntity);
            float3 cameraForward = float4.RotateVector(new float3(0f, 0f, -1f), cameraEntity.LocalOrientation);
            float3 cameraToStack = float3.Normalize(new float3(0f, 2.5f, 0f) - cameraEntity.LocalPosition);

            Assert.True(float3.Dot(cameraForward, cameraToStack) > 0.9f, "The mixed-stack camera should look toward the cube and sphere contact volume.");
            Assert.Contains(groundEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.RigidBody3DComponent", StringComparison.Ordinal));
            Assert.Contains(groundEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.BoxCollider3DComponent", StringComparison.Ordinal));
            Assert.Contains(firstBoxEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.RigidBody3DComponent", StringComparison.Ordinal));
            Assert.Contains(firstBoxEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.BoxCollider3DComponent", StringComparison.Ordinal));
            Assert.Contains(firstSphereEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.RigidBody3DComponent", StringComparison.Ordinal));
            Assert.Contains(firstSphereEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.SphereCollider3DComponent", StringComparison.Ordinal));
            Assert.Equal(SceneAssetReferenceSourceKind.FileSystem, firstBoxMaterialReference.SourceKind);
            Assert.Equal(SceneAssetReferenceSourceKind.FileSystem, firstSphereMaterialReference.SourceKind);
            Assert.NotEqual(firstBoxMaterialReference.RelativePath, firstSphereMaterialReference.RelativePath);
        }

        /// <summary>
        /// Ensures the stacked-box validation scene includes one shadowed directional light and distinct file-backed materials for the visible stack meshes.
        /// </summary>
        [Fact]
        public void CreateSceneAsset_ForDynamicStackBoxes_AddsShadowedDirectionalLightAndDistinctMeshMaterials() {
            PhysicsValidationSceneFactory factory = new PhysicsValidationSceneFactory();

            SceneAsset sceneAsset = factory.CreateSceneAsset(PhysicsValidationSceneCatalog.DynamicStackBoxesSceneId);
            SceneEntityAsset scenarioEntity = FindRootEntity(sceneAsset, "Scenario");
            SceneEntityAsset lightEntity = FindChildEntity(scenarioEntity, "KeyLight");
            SceneEntityAsset groundEntity = FindChildEntity(scenarioEntity, "Ground");
            SceneEntityAsset firstStackBoxEntity = FindChildEntity(scenarioEntity, "StackBox01");
            SceneEntityAsset secondStackBoxEntity = FindChildEntity(scenarioEntity, "StackBox02");
            SceneEntityAsset thirdStackBoxEntity = FindChildEntity(scenarioEntity, "StackBox03");
            SceneEntityAsset fourthStackBoxEntity = FindChildEntity(scenarioEntity, "StackBox04");

            SceneComponentAssetRecord lightRecord = Assert.Single(lightEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.DirectionalLightComponent", StringComparison.Ordinal));
            DirectionalLightComponent lightComponent = ReadDirectionalLight(lightRecord);
            Assert.True(lightComponent.ShadowsEnabled);
            Assert.Equal(ShadowMapMode.Forced, lightComponent.ShadowMapMode);
            Assert.Equal(1f, lightComponent.Intensity);

            SceneAssetReference groundMaterialReference = ReadMaterialReference(groundEntity);
            SceneAssetReference firstBoxMaterialReference = ReadMaterialReference(firstStackBoxEntity);
            SceneAssetReference secondBoxMaterialReference = ReadMaterialReference(secondStackBoxEntity);
            SceneAssetReference thirdBoxMaterialReference = ReadMaterialReference(thirdStackBoxEntity);
            SceneAssetReference fourthBoxMaterialReference = ReadMaterialReference(fourthStackBoxEntity);

            Assert.Equal(SceneAssetReferenceSourceKind.FileSystem, groundMaterialReference.SourceKind);
            Assert.Equal(SceneAssetReferenceSourceKind.FileSystem, firstBoxMaterialReference.SourceKind);
            Assert.NotEqual(groundMaterialReference.RelativePath, firstBoxMaterialReference.RelativePath);
            Assert.NotEqual(firstBoxMaterialReference.RelativePath, secondBoxMaterialReference.RelativePath);
            Assert.NotEqual(secondBoxMaterialReference.RelativePath, thirdBoxMaterialReference.RelativePath);
            Assert.NotEqual(thirdBoxMaterialReference.RelativePath, fourthBoxMaterialReference.RelativePath);
        }

        /// <summary>
        /// Ensures writing the physics validation scenes also emits schema-backed standard materials with authored base colors.
        /// </summary>
        [Fact]
        public void WriteScenes_WritesSharedPhysicsDemoMaterialAssetsWithBaseColors() {
            PhysicsValidationSceneFactory factory = new PhysicsValidationSceneFactory();

            factory.WriteScenes(TempProjectRootPath);

            string shaderPath = Path.Combine(TempProjectRootPath, "assets", "Shaders", "physics", "PhysicsDemoMesh.hlsl");
            string neutralMaterialPath = Path.Combine(TempProjectRootPath, "assets", "Materials", "physics", "PhysicsDemoNeutral.hasset");
            string blueMaterialPath = Path.Combine(TempProjectRootPath, "assets", "Materials", "physics", "PhysicsDemoBlue.hasset");

            Assert.False(File.Exists(shaderPath));
            Assert.True(File.Exists(neutralMaterialPath));
            Assert.True(File.Exists(blueMaterialPath));

            MaterialAssetSettingsService settingsService = new MaterialAssetSettingsService();
            ShaderMaterialAsset blueMaterialAsset = settingsService.LoadMaterialAsset(blueMaterialPath, "windows");
            MaterialConstantBufferAsset baseColorBuffer = Assert.Single(blueMaterialAsset.ConstantBuffers, constantBuffer => string.Equals(constantBuffer.Name, "BaseColorBuffer", StringComparison.Ordinal));
            float4 baseColor = ReadFloat4(baseColorBuffer.Data);

            Assert.Equal("ForwardStandardShader", blueMaterialAsset.ShaderAssetId);
            Assert.Equal("ForwardStandardShader.vs", blueMaterialAsset.VertexProgram);
            Assert.Equal("ForwardStandardShader.ps", blueMaterialAsset.PixelProgram);
            Assert.Equal(new float4(84f / 255f, 143f / 255f, 230f / 255f, 1.0f), baseColor);
        }

        /// <summary>
        /// Ensures the kinematic-push validation scene includes serialized rigid-body, box-collider, and motion records for the runtime-driven pusher.
        /// </summary>
        [Fact]
        public void CreateSceneAsset_ForKinematicPush_WritesPhysicsRecordsForPusherAndDynamicTarget() {
            PhysicsValidationSceneFactory factory = new PhysicsValidationSceneFactory();

            SceneAsset sceneAsset = factory.CreateSceneAsset(PhysicsValidationSceneCatalog.KinematicPushSceneId);
            SceneEntityAsset scenarioEntity = FindRootEntity(sceneAsset, "Scenario");
            SceneEntityAsset pusherEntity = FindChildEntity(scenarioEntity, "KinematicPusher");
            SceneEntityAsset dynamicTargetEntity = FindChildEntity(scenarioEntity, "DynamicTarget");

            Assert.Contains(pusherEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.RigidBody3DComponent", StringComparison.Ordinal));
            Assert.Contains(pusherEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.BoxCollider3DComponent", StringComparison.Ordinal));
            Assert.Contains(pusherEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.KinematicMotion3DComponent", StringComparison.Ordinal));
            Assert.Contains(dynamicTargetEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.RigidBody3DComponent", StringComparison.Ordinal));
            Assert.Contains(dynamicTargetEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.BoxCollider3DComponent", StringComparison.Ordinal));
        }

        /// <summary>
        /// Ensures the slope validation scene includes serialized controller and collider records for the runtime-driven character.
        /// </summary>
        [Fact]
        public void CreateSceneAsset_ForCharacterSlope_WritesPhysicsRecordsForSlopeAndController() {
            PhysicsValidationSceneFactory factory = new PhysicsValidationSceneFactory();

            SceneAsset sceneAsset = factory.CreateSceneAsset(PhysicsValidationSceneCatalog.CharacterSlopeSceneId);
            SceneEntityAsset scenarioEntity = FindRootEntity(sceneAsset, "Scenario");
            SceneEntityAsset groundEntity = FindChildEntity(scenarioEntity, "Ground");
            SceneEntityAsset rampEntity = FindChildEntity(scenarioEntity, "SlopeRamp");
            SceneEntityAsset controllerEntity = FindChildEntity(scenarioEntity, "CharacterController");

            Assert.Contains(groundEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.RigidBody3DComponent", StringComparison.Ordinal));
            Assert.Contains(groundEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.BoxCollider3DComponent", StringComparison.Ordinal));
            Assert.Contains(rampEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.RigidBody3DComponent", StringComparison.Ordinal));
            Assert.Contains(rampEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.BoxCollider3DComponent", StringComparison.Ordinal));
            Assert.Contains(controllerEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.BoxCollider3DComponent", StringComparison.Ordinal));
            Assert.Contains(controllerEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.CharacterController3DComponent", StringComparison.Ordinal));
        }

        /// <summary>
        /// Ensures the moving-platform validation scene includes serialized rigid-body, box-collider, and motion records for the runtime-driven platform.
        /// </summary>
        [Fact]
        public void CreateSceneAsset_ForCharacterMovingPlatform_WritesPhysicsRecordsForMovingPlatform() {
            PhysicsValidationSceneFactory factory = new PhysicsValidationSceneFactory();

            SceneAsset sceneAsset = factory.CreateSceneAsset(PhysicsValidationSceneCatalog.CharacterMovingPlatformSceneId);
            SceneEntityAsset scenarioEntity = FindRootEntity(sceneAsset, "Scenario");
            SceneEntityAsset groundEntity = FindChildEntity(scenarioEntity, "Ground");
            SceneEntityAsset platformEntity = FindChildEntity(scenarioEntity, "MovingPlatform");

            Assert.Contains(groundEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.RigidBody3DComponent", StringComparison.Ordinal));
            Assert.Contains(groundEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.BoxCollider3DComponent", StringComparison.Ordinal));
            Assert.Contains(platformEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.RigidBody3DComponent", StringComparison.Ordinal));
            Assert.Contains(platformEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.BoxCollider3DComponent", StringComparison.Ordinal));
            Assert.Contains(platformEntity.Components, component => string.Equals(component.ComponentTypeId, "helengine.KinematicMotion3DComponent", StringComparison.Ordinal));
        }

        /// <summary>
        /// Ensures the generated kinematic-push validation scene can be packaged, loaded through the runtime scene loader, and simulated by the current 3D physics runtime.
        /// </summary>
        [Fact]
        public void CreateSceneAsset_ForKinematicPush_PackagesLoadsAndSimulatesPusherDisplacingTarget() {
            PhysicsValidationSceneFactory factory = new PhysicsValidationSceneFactory();
            string buildRootPath = Path.Combine(TempProjectRootPath, "Build");
            Directory.CreateDirectory(buildRootPath);
            factory.WriteScenes(TempProjectRootPath);

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(TempProjectRootPath);
            packager.Package(new[] { PhysicsValidationSceneCatalog.KinematicPushSceneId }, buildRootPath);

            string packagedScenePath = GetPackagedScenePath(buildRootPath, PhysicsValidationSceneCatalog.KinematicPushSceneId);
            SceneAsset sceneAsset;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = buildRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
            Physics3DRuntimeComponentRegistration.Register(core);

            RuntimeSceneLoadService sceneLoadService = new RuntimeSceneLoadService(core.SceneAssetReferenceResolver, core.SceneRuntimeComponentRegistry);
            IReadOnlyList<Entity> rootEntities = sceneLoadService.Load(sceneAsset);
            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(rootEntities);

            for (int index = 0; index < 60; index++) {
                world.Step(1.0 / 60.0);
            }

            Entity scenarioEntity = rootEntities[1];
            Entity dynamicTargetEntity = scenarioEntity.Children[1];
            Entity pusherEntity = scenarioEntity.Children[2];

            Assert.True(pusherEntity.LocalPosition.X > -2f, $"Expected the generated kinematic pusher to advance along X, but its X position was {pusherEntity.LocalPosition.X}.");
            Assert.True(dynamicTargetEntity.LocalPosition.X > 1.5f, $"Expected the generated dynamic target to be displaced along X, but its X position was {dynamicTargetEntity.LocalPosition.X}.");
            Assert.InRange(dynamicTargetEntity.LocalPosition.Y, 0.49f, 0.51f);
        }

        /// <summary>
        /// Ensures the generated moving-platform validation scene can be packaged, loaded through the runtime scene loader, and simulated so the platform advances along its path.
        /// </summary>
        [Fact]
        public void CreateSceneAsset_ForCharacterMovingPlatform_PackagesLoadsAndMovesPlatform() {
            PhysicsValidationSceneFactory factory = new PhysicsValidationSceneFactory();
            string buildRootPath = Path.Combine(TempProjectRootPath, "Build");
            Directory.CreateDirectory(buildRootPath);
            factory.WriteScenes(TempProjectRootPath);

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(TempProjectRootPath);
            packager.Package(new[] { PhysicsValidationSceneCatalog.CharacterMovingPlatformSceneId }, buildRootPath);

            string packagedScenePath = GetPackagedScenePath(buildRootPath, PhysicsValidationSceneCatalog.CharacterMovingPlatformSceneId);
            SceneAsset sceneAsset;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = buildRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
            Physics3DRuntimeComponentRegistration.Register(core);

            RuntimeSceneLoadService sceneLoadService = new RuntimeSceneLoadService(core.SceneAssetReferenceResolver, core.SceneRuntimeComponentRegistry);
            IReadOnlyList<Entity> rootEntities = sceneLoadService.Load(sceneAsset);
            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(rootEntities);

            for (int index = 0; index < 60; index++) {
                world.Step(1.0 / 60.0);
            }

            Entity scenarioEntity = rootEntities[1];
            Entity platformEntity = scenarioEntity.Children[3];

            Assert.True(platformEntity.LocalPosition.X > -0.5f, $"Expected the generated moving platform to advance along X, but its X position was {platformEntity.LocalPosition.X}.");
            Assert.InRange(platformEntity.LocalPosition.Y, 0.75f, 0.75f);
        }

        /// <summary>
        /// Ensures the generated slope validation scene can be packaged, loaded through the runtime scene loader, and simulated so the character climbs the ramp.
        /// </summary>
        [Fact]
        public void CreateSceneAsset_ForCharacterSlope_PackagesLoadsAndClimbsSlope() {
            PhysicsValidationSceneFactory factory = new PhysicsValidationSceneFactory();
            string buildRootPath = Path.Combine(TempProjectRootPath, "Build");
            Directory.CreateDirectory(buildRootPath);
            factory.WriteScenes(TempProjectRootPath);

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(TempProjectRootPath);
            packager.Package(new[] { PhysicsValidationSceneCatalog.CharacterSlopeSceneId }, buildRootPath);

            string packagedScenePath = GetPackagedScenePath(buildRootPath, PhysicsValidationSceneCatalog.CharacterSlopeSceneId);
            SceneAsset sceneAsset;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = buildRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
            Physics3DRuntimeComponentRegistration.Register(core);

            RuntimeSceneLoadService sceneLoadService = new RuntimeSceneLoadService(core.SceneAssetReferenceResolver, core.SceneRuntimeComponentRegistry);
            IReadOnlyList<Entity> rootEntities = sceneLoadService.Load(sceneAsset);
            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(rootEntities);

            for (int index = 0; index < 180; index++) {
                world.Step(1.0 / 60.0);
            }

            Entity scenarioEntity = rootEntities[1];
            Entity controllerEntity = scenarioEntity.Children[2];

            Assert.True(controllerEntity.LocalPosition.X > 1.5f, $"Expected the generated character controller to advance along X, but its X position was {controllerEntity.LocalPosition.X}.");
            Assert.True(controllerEntity.LocalPosition.Y > 0.9f, $"Expected the generated character controller to climb the slope, but its Y position was {controllerEntity.LocalPosition.Y}.");
        }

        /// <summary>
        /// Ensures automatic reflected rigid-body and box-collider payloads load into the runtime with a dynamic upper box that falls under gravity.
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
                ContentRootPath = TempProjectRootPath
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
        /// Resolves the absolute scene file path for one generated relative scene id.
        /// </summary>
        /// <param name="projectRootPath">Absolute project root path.</param>
        /// <param name="sceneId">Relative scene asset id stored inside the file.</param>
        /// <returns>Absolute file path where the scene should be written.</returns>
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

        /// <summary>
        /// Finds one root entity with the requested authored name.
        /// </summary>
        /// <param name="sceneAsset">Scene asset that should contain the entity.</param>
        /// <param name="entityName">Authored root-entity name.</param>
        /// <returns>Matching root entity.</returns>
        static SceneEntityAsset FindRootEntity(SceneAsset sceneAsset, string entityName) {
            if (sceneAsset == null) {
                throw new ArgumentNullException(nameof(sceneAsset));
            }
            if (string.IsNullOrWhiteSpace(entityName)) {
                throw new ArgumentException("Entity name must be provided.", nameof(entityName));
            }

            SceneEntityAsset[] rootEntities = sceneAsset.RootEntities ?? Array.Empty<SceneEntityAsset>();
            for (int index = 0; index < rootEntities.Length; index++) {
                SceneEntityAsset entity = rootEntities[index];
                if (string.Equals(entity.Name, entityName, StringComparison.Ordinal)) {
                    return entity;
                }
            }

            throw new InvalidOperationException($"Could not find root entity '{entityName}'.");
        }

        /// <summary>
        /// Finds one authored child entity under the supplied parent entity.
        /// </summary>
        /// <param name="parentEntity">Parent entity that owns the requested child.</param>
        /// <param name="entityName">Authored child-entity name.</param>
        /// <returns>Matching child entity.</returns>
        static SceneEntityAsset FindChildEntity(SceneEntityAsset parentEntity, string entityName) {
            if (parentEntity == null) {
                throw new ArgumentNullException(nameof(parentEntity));
            }
            if (string.IsNullOrWhiteSpace(entityName)) {
                throw new ArgumentException("Entity name must be provided.", nameof(entityName));
            }

            SceneEntityAsset[] childEntities = parentEntity.Children ?? Array.Empty<SceneEntityAsset>();
            for (int index = 0; index < childEntities.Length; index++) {
                SceneEntityAsset entity = childEntities[index];
                if (string.Equals(entity.Name, entityName, StringComparison.Ordinal)) {
                    return entity;
                }
            }

            throw new InvalidOperationException($"Could not find child entity '{entityName}'.");
        }

        /// <summary>
        /// Reads one serialized directional light component record into a live light instance for assertion.
        /// </summary>
        /// <param name="componentRecord">Serialized scene component record to decode.</param>
        /// <returns>Directional light reconstructed from the payload.</returns>
        static DirectionalLightComponent ReadDirectionalLight(SceneComponentAssetRecord componentRecord) {
            if (componentRecord == null) {
                throw new ArgumentNullException(nameof(componentRecord));
            }

            using MemoryStream stream = new MemoryStream(componentRecord.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            Assert.Equal(LightComponentScenePayloadSerializer.CurrentVersion, version);
            return LightComponentScenePayloadSerializer.ReadDirectionalLight(reader);
        }

        /// <summary>
        /// Reads one packed little-endian float4 payload written into a material constant buffer.
        /// </summary>
        /// <param name="data">Sixteen-byte float payload to decode.</param>
        /// <returns>Decoded vector value.</returns>
        static float4 ReadFloat4(byte[] data) {
            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            } else if (data.Length != 16) {
                throw new InvalidOperationException("Float4 material constant buffers must contain exactly sixteen bytes.");
            }

            using MemoryStream stream = new MemoryStream(data, false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            return new float4(
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle());
        }

        /// <summary>
        /// Reads the material reference from one serialized mesh component payload.
        /// </summary>
        /// <param name="entity">Entity whose mesh record should be decoded.</param>
        /// <returns>Scene asset reference used by the mesh material.</returns>
        static SceneAssetReference ReadMaterialReference(SceneEntityAsset entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            SceneComponentAssetRecord meshRecord = Assert.Single(entity.Components, component => string.Equals(component.ComponentTypeId, "helengine.MeshComponent", StringComparison.Ordinal));
            using MemoryStream stream = new MemoryStream(meshRecord.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            Assert.Equal(1, version);
            SceneAssetReference modelReference = ReadOptionalReference(reader);
            SceneAssetReference materialReference = ReadOptionalReference(reader);
            Assert.NotNull(modelReference);
            Assert.NotNull(materialReference);
            return materialReference;
        }

        /// <summary>
        /// Reads one optional scene asset reference from a binary component payload.
        /// </summary>
        /// <param name="reader">Payload reader positioned at the optional reference flag.</param>
        /// <returns>Decoded scene asset reference.</returns>
        static SceneAssetReference ReadOptionalReference(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            bool hasValue = reader.ReadByte() != 0;
            Assert.True(hasValue);
            return new SceneAssetReference {
                SourceKind = (SceneAssetReferenceSourceKind)reader.ReadInt32(),
                RelativePath = reader.ReadString(),
                ProviderId = reader.ReadString(),
                AssetId = reader.ReadString()
            };
        }

        /// <summary>
        /// Creates one reflected automatic rigid-body scene record using the shared ordinal member order.
        /// </summary>
        /// <param name="bodyKind">Rigid-body participation mode to encode.</param>
        /// <param name="useGravity">True when gravity should be enabled.</param>
        /// <returns>Serialized rigid-body scene record.</returns>
        static SceneComponentAssetRecord CreateAutomaticRigidBodyRecord(BodyKind3D bodyKind, bool useGravity) {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteInt32(6);
            writer.WriteFloat3(float3.Zero);
            writer.WriteInt32((int)bodyKind);
            writer.WriteDouble(1d);
            writer.WriteFloat3(float3.Zero);
            writer.WriteDouble(1d);
            writer.WriteByte(useGravity ? (byte)1 : (byte)0);

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.RigidBody3DComponent",
                ComponentIndex = 0,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Creates one reflected automatic box-collider scene record using the shared ordinal member order.
        /// </summary>
        /// <param name="size">Full collider size to encode.</param>
        /// <param name="isTrigger">True when the collider should be encoded as a trigger.</param>
        /// <returns>Serialized box-collider scene record.</returns>
        static SceneComponentAssetRecord CreateAutomaticBoxColliderRecord(float3 size, bool isTrigger) {
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
    }
}
