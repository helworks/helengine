using helengine.directx11;
using System.Reflection;
using helengine.editor;
using helengine.editor.tests.testing;
using helengine.ui;
using helengine.vulkan;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies loading `.helen` scene files into live editor entities.
    /// </summary>
    public class SceneFileLoadServiceTests : IDisposable {
        /// <summary>
        /// Temporary project root used by scene load tests.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Initializes a temporary project root and the core services required for scene loading.
        /// </summary>
        public SceneFileLoadServiceTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-scene-file-load-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets", "Scenes"));

            EditorCore core = new EditorCore(new Project {
                Name = "Scene File Load",
                Path = TempProjectRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"), new CoreInitializationOptions {
                ContentRootPath = TempProjectRootPath
            });
            ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
            shaderBackendRegistry.Register(new DirectX11ShaderBackend());
            shaderBackendRegistry.Register(new VulkanShaderBackend());
            EditorBuiltInShaderAssetLibrary.ConfigureShaderBackends(shaderBackendRegistry);
        }

        /// <summary>
        /// Deletes temporary project state after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a saved `.helen` file can be materialized back into editor entities.
        /// </summary>
        [Fact]
        public void Load_WhenSceneFileExists_ReturnsRootEntities() {
            SceneAssetReference modelReference = CreateGeneratedModelReference();
            SceneAssetReference materialReference = CreateGeneratedMaterialReference();
            string scenePath = SaveSceneAsset("Loaded.helen", "Loaded Cube", modelReference, materialReference);
            SceneFileLoadService loadService = CreateLoadService(modelReference, materialReference);

            LoadedEditorSceneDocument loaded = loadService.Load(scenePath);

            EditorEntity root = Assert.Single(loaded.RootEntities);
            Assert.Equal("Loaded Cube", root.Name);
            Assert.False(root.Enabled);
            Assert.Equal(SceneCanvasProfile.DefaultWidth, loaded.SceneSettings.CanvasProfile.Width);
            Assert.Equal(SceneCanvasProfile.DefaultHeight, loaded.SceneSettings.CanvasProfile.Height);
        }

        /// <summary>
        /// Ensures a generic reflected camera payload loads into a live camera that still holds the authored values directly.
        /// </summary>
        [Fact]
        public void Load_WhenSceneContainsGenericCameraPayload_RestoresAuthoredValuesOnLiveCamera() {
            string scenePath = WriteSceneAsset("GenericCamera.helen", CreateAutomaticTaggedCameraRecord(
                7,
                0x3456,
                new float4(0.2f, 0.3f, 0.4f, 0.5f),
                0.25f,
                125f,
                new CameraClearSettings(true, new float4(0.1f, 0.2f, 0.3f, 0.4f), true, 0.9f, true, 6),
                new CameraRenderSettings {
                    DepthPrepassMode = DepthPrepassMode.Always,
                    ShadowDistance = 222f,
                    PostProcessTier = PostProcessTier.Low
                }));
            SceneFileLoadService loadService = new SceneFileLoadService(TempProjectRootPath, CreatePersistenceRegistry(), new TestSceneAssetReferenceResolver());

            LoadedEditorSceneDocument loaded = loadService.Load(scenePath);

            EditorEntity root = Assert.Single(loaded.RootEntities);
            CameraComponent loadedCamera = Assert.IsType<CameraComponent>(Assert.Single(root.Components, component => component is CameraComponent));
            Assert.Equal((byte)7, loadedCamera.CameraDrawOrder);
            Assert.Equal((ushort)0x3456, loadedCamera.LayerMask);
            Assert.Equal(new float4(0.2f, 0.3f, 0.4f, 0.5f), loadedCamera.Viewport);
            Assert.Equal(0.25f, loadedCamera.NearPlaneDistance);
            Assert.Equal(125f, loadedCamera.FarPlaneDistance);
            Assert.Equal(new CameraClearSettings(true, new float4(0.1f, 0.2f, 0.3f, 0.4f), true, 0.9f, true, 6), loadedCamera.ClearSettings);
            Assert.Equal(DepthPrepassMode.Always, loadedCamera.RenderSettings.DepthPrepassMode);
            Assert.Equal(222f, loadedCamera.RenderSettings.ShadowDistance);
            Assert.Equal(PostProcessTier.Low, loadedCamera.RenderSettings.PostProcessTier);
        }

        /// <summary>
        /// Ensures invalid scene files fail with a clear exception.
        /// </summary>
        [Fact]
        public void Load_WhenSceneFileIsInvalid_ThrowsInvalidOperationException() {
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "Broken.helen");
            File.WriteAllText(scenePath, "not-a-helen");
            SceneFileLoadService loadService = CreateLoadService(CreateGeneratedModelReference(), CreateGeneratedMaterialReference());

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => loadService.Load(scenePath));

            Assert.Contains("Scene load failed", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures entities created during a failed materialization are removed from the live object manager.
        /// </summary>
        [Fact]
        public void Load_WhenMaterializationFails_CleansNewEntitiesAndPreservesExistingScene() {
            SceneAssetReference modelReference = CreateGeneratedModelReference();
            SceneAssetReference materialReference = CreateGeneratedMaterialReference();
            string scenePath = SaveSceneAsset("BrokenMaterialization.helen", "Transient Root", modelReference, materialReference);
            ComponentPersistenceRegistry registry = CreatePersistenceRegistry();
            SceneFileLoadService loadService = new SceneFileLoadService(TempProjectRootPath, registry, new TestSceneAssetReferenceResolver());
            EditorEntity existing = Assert.IsType<EditorEntity>(Core.Instance.EntityFactory.Create("Existing"));

            Assert.Throws<InvalidOperationException>(() => loadService.Load(scenePath));

            Assert.Contains(Core.Instance.ObjectManager.Entities, entity => ReferenceEquals(entity, existing));
            Assert.DoesNotContain(Core.Instance.ObjectManager.Entities, entity => entity is EditorEntity editorEntity && string.Equals(editorEntity.Name, "Transient Root", StringComparison.Ordinal));
        }

        /// <summary>
        /// Ensures scene loading returns the authored scene canvas profile stored in the scene file.
        /// </summary>
        [Fact]
        public void Load_WhenSceneFileContainsCustomCanvasProfile_ReturnsSceneSettings() {
            SceneAssetReference modelReference = CreateGeneratedModelReference();
            SceneAssetReference materialReference = CreateGeneratedMaterialReference();
            SceneSettingsAsset sceneSettings = new SceneSettingsAsset {
                CanvasProfile = new SceneCanvasProfile {
                    Width = 1600,
                    Height = 900
                }
            };
            string scenePath = SaveSceneAsset("CanvasProfile.helen", "Loaded Cube", modelReference, materialReference, sceneSettings);
            SceneFileLoadService loadService = CreateLoadService(modelReference, materialReference);

            LoadedEditorSceneDocument loaded = loadService.Load(scenePath);

            Assert.Equal(1600, loaded.SceneSettings.CanvasProfile.Width);
            Assert.Equal(900, loaded.SceneSettings.CanvasProfile.Height);
        }

        /// <summary>
        /// Ensures scene loading returns the authored dont-unload scene setting stored in the scene file.
        /// </summary>
        [Fact]
        public void Load_WhenSceneFileContainsDontUnload_ReturnsSceneSettingsFlag() {
            SceneAssetReference modelReference = CreateGeneratedModelReference();
            SceneAssetReference materialReference = CreateGeneratedMaterialReference();
            SceneSettingsAsset sceneSettings = new SceneSettingsAsset {
                CanvasProfile = new SceneCanvasProfile {
                    Width = 1600,
                    Height = 900
                },
                DontUnload = true
            };
            string scenePath = SaveSceneAsset("Persistent.helen", "Loaded Cube", modelReference, materialReference, sceneSettings);
            SceneFileLoadService loadService = CreateLoadService(modelReference, materialReference);

            LoadedEditorSceneDocument loaded = loadService.Load(scenePath);

            Assert.True(loaded.SceneSettings.DontUnload);
        }

        /// <summary>
        /// Creates a scene-load service with runtime references registered for a saved mesh component.
        /// </summary>
        /// <param name="modelReference">Model reference to resolve during load.</param>
        /// <param name="materialReference">Material reference to resolve during load.</param>
        /// <returns>Configured scene file load service.</returns>
        SceneFileLoadService CreateLoadService(SceneAssetReference modelReference, SceneAssetReference materialReference) {
            TestSceneAssetReferenceResolver resolver = new TestSceneAssetReferenceResolver();
            resolver.RegisterModel(modelReference, new TestRuntimeModel());
            resolver.RegisterMaterial(materialReference, new TestRuntimeMaterial());
            return new SceneFileLoadService(TempProjectRootPath, CreatePersistenceRegistry(), resolver);
        }

        /// <summary>
        /// Creates the component persistence registry used by scene save and load tests.
        /// </summary>
        /// <returns>Configured component persistence registry.</returns>
        ComponentPersistenceRegistry CreatePersistenceRegistry() {
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            return registry;
        }

        /// <summary>
        /// Saves one scene file containing a single mesh-backed user entity.
        /// </summary>
        /// <param name="fileName">Scene file name to write.</param>
        /// <param name="entityName">Name assigned to the saved root entity.</param>
        /// <param name="modelReference">Model reference persisted with the mesh component.</param>
        /// <param name="materialReference">Material reference persisted with the mesh component.</param>
        /// <returns>Absolute path to the written `.helen` file.</returns>
        string SaveSceneAsset(string fileName, string entityName, SceneAssetReference modelReference, SceneAssetReference materialReference) {
            return SaveSceneAsset(fileName, entityName, modelReference, materialReference, new SceneSettingsAsset());
        }

        /// <summary>
        /// Saves one scene file containing a single mesh-backed user entity and explicit scene settings.
        /// </summary>
        /// <param name="fileName">Scene file name to write.</param>
        /// <param name="entityName">Name assigned to the saved root entity.</param>
        /// <param name="modelReference">Model reference persisted with the mesh component.</param>
        /// <param name="materialReference">Material reference persisted with the mesh component.</param>
        /// <param name="sceneSettings">Scene-level settings that should be persisted.</param>
        /// <returns>Absolute path to the written `.helen` file.</returns>
        string SaveSceneAsset(string fileName, string entityName, SceneAssetReference modelReference, SceneAssetReference materialReference, SceneSettingsAsset sceneSettings) {
            EditorEntity root = Assert.IsType<EditorEntity>(Core.Instance.EntityFactory.Create(entityName));
            MeshComponent meshComponent = new MeshComponent {
                Model = new TestRuntimeModel(),
                Materials = new RuntimeMaterial[] { new TestRuntimeMaterial() }
            };
            root.AddComponent(meshComponent);
            EntitySaveComponent saveComponent = GetSaveComponent(root);
            saveComponent.SetAssetReference(meshComponent, "Model", modelReference);
            saveComponent.SetAssetReference(meshComponent, "Materials[0]", materialReference);

            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, CreatePersistenceRegistry());
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", fileName);
            saveService.Save(scenePath, sceneSettings);
            root.Enabled = false;
            Core.Instance.ObjectManager.RemoveEntity(root);
            return scenePath;
        }

        /// <summary>
        /// Writes one scene file containing a single root entity with the supplied component records.
        /// </summary>
        /// <param name="fileName">Scene file name to write beneath the temporary project root.</param>
        /// <param name="componentRecords">Serialized component records attached to the root entity.</param>
        /// <returns>Absolute path to the written scene file.</returns>
        string WriteSceneAsset(string fileName, params SceneComponentAssetRecord[] componentRecords) {
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", fileName);
            using FileStream stream = File.Create(scenePath);
            AssetSerializer.Serialize(
                stream,
                new SceneAsset {
                    Id = fileName,
                    SceneSettings = new SceneSettingsAsset(),
                    RootEntities = [
                        new SceneEntityAsset {
                            Id = 1u,
                            Name = "Camera",
                            LocalPosition = float3.Zero,
                            LocalScale = float3.One,
                            LocalOrientation = float4.Identity,
                            Components = componentRecords ?? Array.Empty<SceneComponentAssetRecord>(),
                            Children = Array.Empty<SceneEntityAsset>()
                        }
                    ]
                });
            return scenePath;
        }

        /// <summary>
        /// Creates one generic reflected tagged camera component payload record.
        /// </summary>
        /// <param name="cameraDrawOrder">Authored camera draw order.</param>
        /// <param name="layerMask">Authored camera layer mask.</param>
        /// <param name="viewport">Authored camera viewport.</param>
        /// <param name="nearPlaneDistance">Authored near clip-plane distance.</param>
        /// <param name="farPlaneDistance">Authored far clip-plane distance.</param>
        /// <param name="clearSettings">Authored clear settings.</param>
        /// <param name="renderSettings">Authored render settings.</param>
        /// <returns>Serialized camera component record encoded through the generic tagged payload shape.</returns>
        static SceneComponentAssetRecord CreateAutomaticTaggedCameraRecord(
            byte cameraDrawOrder,
            ushort layerMask,
            float4 viewport,
            float nearPlaneDistance,
            float farPlaneDistance,
            CameraClearSettings clearSettings,
            CameraRenderSettings renderSettings) {
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            WriteAutomaticTaggedField(writer, nameof(CameraComponent.CameraDrawOrder), typeof(byte), cameraDrawOrder);
            WriteAutomaticTaggedField(writer, nameof(CameraComponent.LayerMask), typeof(ushort), layerMask);
            WriteAutomaticTaggedField(writer, nameof(CameraComponent.Viewport), typeof(float4), viewport);
            WriteAutomaticTaggedField(writer, nameof(CameraComponent.NearPlaneDistance), typeof(float), nearPlaneDistance);
            WriteAutomaticTaggedField(writer, nameof(CameraComponent.FarPlaneDistance), typeof(float), farPlaneDistance);
            WriteAutomaticTaggedField(writer, nameof(CameraComponent.ClearSettings), typeof(CameraClearSettings), clearSettings);
            WriteAutomaticTaggedField(writer, nameof(CameraComponent.RenderSettings), typeof(CameraRenderSettings), renderSettings);

            return new SceneComponentAssetRecord {
                ComponentTypeId = AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(CameraComponent)),
                ComponentIndex = 0,
                Payload = writer.BuildPayload()
            };
        }

        /// <summary>
        /// Writes one generic reflected tagged field using the automatic component binary encoding rules.
        /// </summary>
        /// <param name="writer">Tagged field writer receiving the field.</param>
        /// <param name="fieldName">Stable reflected member name.</param>
        /// <param name="valueType">Runtime member type encoded by the field.</param>
        /// <param name="value">Authored member value to encode.</param>
        static void WriteAutomaticTaggedField(EditorTaggedSceneComponentFieldWriter writer, string fieldName, Type valueType, object value) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }
            if (string.IsNullOrWhiteSpace(fieldName)) {
                throw new ArgumentException("Field name must be provided.", nameof(fieldName));
            }
            if (valueType == null) {
                throw new ArgumentNullException(nameof(valueType));
            }

            MethodInfo writeMethod = typeof(AutomaticScriptComponentPersistenceDescriptor).GetMethod(
                "WriteSupportedValue",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.NotNull(writeMethod);

            writer.WriteField(fieldName, fieldWriter => writeMethod.Invoke(null, new object[] { fieldWriter, valueType, value }));
        }

        /// <summary>
        /// Writes one minimal playable scene asset referenced by the test menu definition.
        /// </summary>
        /// <param name="relativeScenePath">Project-relative scene path to create beneath the temporary assets root.</param>
        void WritePlayableSceneAsset(string relativeScenePath) {
            if (string.IsNullOrWhiteSpace(relativeScenePath)) {
                throw new ArgumentException("Relative scene path must be provided.", nameof(relativeScenePath));
            }

            string scenePath = Path.Combine(TempProjectRootPath, relativeScenePath.Replace('/', Path.DirectorySeparatorChar));
            string sceneDirectoryPath = Path.GetDirectoryName(scenePath);
            if (string.IsNullOrWhiteSpace(sceneDirectoryPath)) {
                throw new InvalidOperationException("Playable scene directory could not be resolved.");
            }

            Directory.CreateDirectory(sceneDirectoryPath);
            SceneAsset sceneAsset = new SceneAsset {
                RootEntities = Array.Empty<SceneEntityAsset>()
            };

            using FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, sceneAsset);
        }

        /// <summary>
        /// Retrieves the hidden save component attached to one editor entity.
        /// </summary>
        /// <param name="entity">Entity whose save component should be returned.</param>
        /// <returns>Attached hidden save component.</returns>
        EntitySaveComponent GetSaveComponent(EditorEntity entity) {
            return Assert.IsType<EntitySaveComponent>(Assert.Single(entity.Components, component => component is EntitySaveComponent));
        }

        /// <summary>
        /// Writes one deterministic packaged font asset to the temporary content root.
        /// </summary>
        /// <param name="relativePath">Project-relative font asset path.</param>
        /// <param name="fontAsset">Font asset written to disk.</param>
        void WriteFontAsset(string relativePath, FontAsset fontAsset) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }
            if (fontAsset == null) {
                throw new ArgumentNullException(nameof(fontAsset));
            }

            string fullPath = Path.Combine(TempProjectRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            string directoryPath = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new InvalidOperationException("Font asset directory could not be resolved.");
            }

            Directory.CreateDirectory(directoryPath);
            using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            FontAssetBinarySerializer.Serialize(stream, fontAsset);
        }

        /// <summary>
        /// Creates one deterministic font asset containing the glyphs required by the menu preview labels.
        /// </summary>
        /// <returns>Font asset with stable glyph metrics.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar>();
            for (char character = 'A'; character <= 'Z'; character++) {
                characters[character] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f);
            }

            for (char character = 'a'; character <= 'z'; character++) {
                characters[character] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f);
            }

            characters['i'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f);
            characters['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f);
            characters['r'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f);
            characters['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f);
            characters['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f);
            characters['M'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f);
            characters[' '] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f);
            characters['.'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f);

            FontAsset fontAsset = new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                16f,
                64,
                64);
            fontAsset.SourceTextureAsset = new TextureAsset {
                Width = 64,
                Height = 64,
                Colors = new byte[64 * 64 * 4]
            };
            return fontAsset;
        }

        /// <summary>
        /// Creates one generated model reference used by the saved mesh component.
        /// </summary>
        /// <returns>Stable generated model reference.</returns>
        SceneAssetReference CreateGeneratedModelReference() {
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = "Engine/Models/Cube",
                ProviderId = "engine",
                AssetId = EngineGeneratedModelCache.CubeAssetId
            };
        }

        /// <summary>
        /// Creates one generated material reference used by the saved mesh component.
        /// </summary>
        /// <returns>Stable generated material reference.</returns>
        SceneAssetReference CreateGeneratedMaterialReference() {
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = EngineGeneratedAssetProvider.StandardMaterialRelativePath,
                ProviderId = EngineGeneratedAssetProvider.ProviderIdValue,
                AssetId = EngineGeneratedMaterialCache.StandardAssetId
            };
        }
    }
}
