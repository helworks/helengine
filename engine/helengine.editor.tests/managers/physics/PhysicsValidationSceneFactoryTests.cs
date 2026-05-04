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
                "scenes/physics/test_scene_dynamic_sphere_ramp.helen",
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
            Assert.Equal(2, sceneAsset.AssetReferences.Length);

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

            string packagedScenePath = Path.Combine(buildRootPath, EditorPlatformBuildScenePackager.MainSceneRelativePath.Replace('/', Path.DirectorySeparatorChar));
            SceneAsset sceneAsset;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = buildRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
            Physics3DRuntimeComponentRegistration.Register(core);

            IReadOnlyList<Entity> rootEntities = core.SceneLoadService.Load(sceneAsset);
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

            string packagedScenePath = Path.Combine(buildRootPath, EditorPlatformBuildScenePackager.MainSceneRelativePath.Replace('/', Path.DirectorySeparatorChar));
            SceneAsset sceneAsset;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = buildRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
            Physics3DRuntimeComponentRegistration.Register(core);

            IReadOnlyList<Entity> rootEntities = core.SceneLoadService.Load(sceneAsset);
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

            string packagedScenePath = Path.Combine(buildRootPath, EditorPlatformBuildScenePackager.MainSceneRelativePath.Replace('/', Path.DirectorySeparatorChar));
            SceneAsset sceneAsset;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = buildRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
            Physics3DRuntimeComponentRegistration.Register(core);

            IReadOnlyList<Entity> rootEntities = core.SceneLoadService.Load(sceneAsset);
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
    }
}
