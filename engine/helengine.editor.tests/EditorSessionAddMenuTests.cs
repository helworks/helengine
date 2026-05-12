using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the editor session handles title-bar add commands.
    /// </summary>
    public class EditorSessionAddMenuTests : IDisposable {
        /// <summary>
        /// Temporary project root used by add-menu session tests.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Initializes the core services required for hierarchy and selection updates.
        /// </summary>
        public EditorSessionAddMenuTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-add-menu-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempProjectRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempProjectRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
            EditorSelectionService.ClearSelection();
            EditorSceneMutationService.Reset();
            EngineGeneratedModelCache.ResetForTests();
            EngineGeneratedMaterialCache.ResetForTests();
            EditorCameraVisualResources.ResetForTests();
        }

        /// <summary>
        /// Deletes temporary project state after each test.
        /// </summary>
        public void Dispose() {
            EditorSelectionService.ClearSelection();
            EditorSceneMutationService.Reset();
            EngineGeneratedModelCache.ResetForTests();
            EngineGeneratedMaterialCache.ResetForTests();
            EditorCameraVisualResources.ResetForTests();
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures Add > Empty creates and selects a root scene entity.
        /// </summary>
        [Fact]
        public void HandleAddEmptyRequested_CreatesAndSelectsRootSceneEntity() {
            EditorSession session = CreateSessionForAddCommands();

            InvokePrivate(session, "HandleAddEmptyRequested");

            EditorEntity selectedEntity = Assert.IsType<EditorEntity>(EditorSelectionService.SelectedEntity);
            Assert.Equal("Empty", selectedEntity.Name);
            Assert.Equal(float3.Zero, selectedEntity.LocalPosition);
            Assert.Equal(1, GetHierarchyNodeCount(session));
        }

        /// <summary>
        /// Ensures Add > Cube creates a mesh-backed scene entity and selects it.
        /// </summary>
        [Fact]
        public void HandleAddCubeRequested_CreatesMeshEntityAndSelectsIt() {
            EditorSession session = CreateSessionForAddCommands();

            InvokePrivate(session, "HandleAddCubeRequested");

            EditorEntity selectedEntity = Assert.IsType<EditorEntity>(EditorSelectionService.SelectedEntity);
            MeshComponent meshComponent = Assert.IsType<MeshComponent>(Assert.Single(selectedEntity.Components, component => component is MeshComponent));

            Assert.Equal("Cube", selectedEntity.Name);
            Assert.NotNull(meshComponent.Model);
            Assert.NotNull(meshComponent.Material);
            Assert.Equal(1, GetHierarchyNodeCount(session));
        }

        /// <summary>
        /// Ensures Add > Camera creates a camera-backed scene entity and selects it.
        /// </summary>
        [Fact]
        public void HandleAddCameraRequested_CreatesCameraEntityAndSelectsIt() {
            EditorSession session = CreateSessionForAddCommands();

            InvokePrivate(session, "HandleAddCameraRequested");

            EditorEntity selectedEntity = Assert.IsType<EditorEntity>(EditorSelectionService.SelectedEntity);
            CameraComponent cameraComponent = Assert.IsType<CameraComponent>(Assert.Single(selectedEntity.Components, component => component is CameraComponent));
            EditorSceneCameraSuppressionComponent suppressionComponent = Assert.IsType<EditorSceneCameraSuppressionComponent>(Assert.Single(selectedEntity.Components, component => component is EditorSceneCameraSuppressionComponent));
            EditorEntity visualEntity = Assert.IsType<EditorEntity>(Assert.Single(selectedEntity.Children));
            EditorCameraVisualComponent visualComponent = Assert.IsType<EditorCameraVisualComponent>(Assert.Single(visualEntity.Components, component => component is EditorCameraVisualComponent));

            Assert.Equal("Camera", selectedEntity.Name);
            Assert.Equal((ushort)0, cameraComponent.LayerMask);
            Assert.False(cameraComponent.ClearSettings.ClearColorEnabled);
            Assert.Equal(EditorLayerMasks.SceneObjects, suppressionComponent.LayerMask);
            Assert.True(visualEntity.InternalEntity);
            Assert.Equal(EditorLayerMasks.SceneCameraVisuals, visualEntity.LayerMask);
            Assert.NotNull(visualComponent.Model);
            Assert.Equal(1, GetHierarchyNodeCount(session));
        }

        /// <summary>
        /// Ensures Add > Light > Spot Light creates a spot-light entity and selects it.
        /// </summary>
        [Fact]
        public void HandleAddSpotLightRequested_CreatesSpotLightEntityAndSelectsIt() {
            EditorSession session = CreateSessionForAddCommands();

            InvokePrivate(session, "HandleAddSpotLightRequested");

            EditorEntity selectedEntity = Assert.IsType<EditorEntity>(EditorSelectionService.SelectedEntity);
            SpotLightComponent lightComponent = Assert.IsType<SpotLightComponent>(Assert.Single(selectedEntity.Components, component => component is SpotLightComponent));

            Assert.Equal("Spot Light", selectedEntity.Name);
            Assert.Equal(10f, lightComponent.Range);
            Assert.Equal(25f, lightComponent.InnerConeAngleDegrees);
            Assert.Equal(45f, lightComponent.OuterConeAngleDegrees);
            Assert.Equal(1, GetHierarchyNodeCount(session));
        }

        /// <summary>
        /// Ensures Add > Light > Point Light creates a point-light entity and selects it.
        /// </summary>
        [Fact]
        public void HandleAddPointLightRequested_CreatesPointLightEntityAndSelectsIt() {
            EditorSession session = CreateSessionForAddCommands();

            InvokePrivate(session, "HandleAddPointLightRequested");

            EditorEntity selectedEntity = Assert.IsType<EditorEntity>(EditorSelectionService.SelectedEntity);
            PointLightComponent lightComponent = Assert.IsType<PointLightComponent>(Assert.Single(selectedEntity.Components, component => component is PointLightComponent));

            Assert.Equal("Point Light", selectedEntity.Name);
            Assert.Equal(10f, lightComponent.Range);
            Assert.Equal(1, GetHierarchyNodeCount(session));
        }

        /// <summary>
        /// Ensures Add > Light > Directional Light creates a directional-light entity and selects it.
        /// </summary>
        [Fact]
        public void HandleAddDirectionalLightRequested_CreatesDirectionalLightEntityAndSelectsIt() {
            EditorSession session = CreateSessionForAddCommands();

            InvokePrivate(session, "HandleAddDirectionalLightRequested");

            EditorEntity selectedEntity = Assert.IsType<EditorEntity>(EditorSelectionService.SelectedEntity);
            DirectionalLightComponent lightComponent = Assert.IsType<DirectionalLightComponent>(Assert.Single(selectedEntity.Components, component => component is DirectionalLightComponent));

            Assert.Equal("Directional Light", selectedEntity.Name);
            Assert.True(lightComponent.ShadowsEnabled);
            Assert.Equal(1, GetHierarchyNodeCount(session));
        }

        /// <summary>
        /// Ensures Add > Light > Ambient Light creates an ambient-light entity and selects it.
        /// </summary>
        [Fact]
        public void HandleAddAmbientLightRequested_CreatesAmbientLightEntityAndSelectsIt() {
            EditorSession session = CreateSessionForAddCommands();

            InvokePrivate(session, "HandleAddAmbientLightRequested");

            EditorEntity selectedEntity = Assert.IsType<EditorEntity>(EditorSelectionService.SelectedEntity);
            AmbientLightComponent lightComponent = Assert.IsType<AmbientLightComponent>(Assert.Single(selectedEntity.Components, component => component is AmbientLightComponent));

            Assert.Equal("Ambient Light", selectedEntity.Name);
            Assert.Equal(1f, lightComponent.Intensity);
            Assert.Equal(1, GetHierarchyNodeCount(session));
        }

        /// <summary>
        /// Ensures Add commands mark the current scene as mutated.
        /// </summary>
        [Fact]
        public void HandleAddEmptyRequested_RaisesSceneMutated() {
            bool raised = false;
            Action handleSceneMutated = () => raised = true;
            EditorSession session = CreateSessionForAddCommands();

            try {
                EditorSceneMutationService.SceneMutated += handleSceneMutated;

                InvokePrivate(session, "HandleAddEmptyRequested");

                Assert.True(raised);
            } finally {
                EditorSceneMutationService.SceneMutated -= handleSceneMutated;
                EditorSceneMutationService.Reset();
            }
        }

        /// <summary>
        /// Creates a partially initialized editor session containing only the collaborators used by add handlers.
        /// </summary>
        /// <returns>Editor session instance configured for add-command tests.</returns>
        EditorSession CreateSessionForAddCommands() {
            EditorSession session = (EditorSession)RuntimeHelpers.GetUninitializedObject(typeof(EditorSession));
            SceneHierarchyPanel sceneHierarchyPanel = new SceneHierarchyPanel(CreateFont());
            EditorSceneCreationService sceneCreationService = new EditorSceneCreationService();

            SetPrivateField(session, "sceneHierarchyPanel", sceneHierarchyPanel);
            SetPrivateField(session, "SceneCreationService", sceneCreationService);

            return session;
        }

        /// <summary>
        /// Invokes one non-public instance method.
        /// </summary>
        /// <param name="target">Target object that owns the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        void InvokePrivate(object target, string methodName) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(target, Array.Empty<object>());
        }

        /// <summary>
        /// Assigns one non-public instance field.
        /// </summary>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to assign.</param>
        /// <param name="value">Value assigned to the field.</param>
        void SetPrivateField(object target, string fieldName, object value) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }

        /// <summary>
        /// Reads the flattened hierarchy node count from the scene hierarchy panel.
        /// </summary>
        /// <param name="session">Editor session that owns the hierarchy panel.</param>
        /// <returns>Current number of hierarchy nodes.</returns>
        int GetHierarchyNodeCount(EditorSession session) {
            FieldInfo panelField = session.GetType().GetField("sceneHierarchyPanel", BindingFlags.Instance | BindingFlags.NonPublic);
            SceneHierarchyPanel panel = Assert.IsType<SceneHierarchyPanel>(panelField.GetValue(session));
            FieldInfo nodesField = panel.GetType().GetField("nodes", BindingFlags.Instance | BindingFlags.NonPublic);
            ICollection nodes = Assert.IsAssignableFrom<ICollection>(nodesField.GetValue(panel));
            return nodes.Count;
        }

        /// <summary>
        /// Creates a small font asset that can satisfy scene-hierarchy layout.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['C'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['E'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['H'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['h'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['y'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
            };

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                16f,
                64,
                64);
        }
    }
}
