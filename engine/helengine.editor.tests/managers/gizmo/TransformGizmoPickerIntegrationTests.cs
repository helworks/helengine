using helengine.editor.tests.testing;

namespace helengine.editor.tests.managers.gizmo {
    /// <summary>Checks the real update sequence from picker readback to press, drag, and release for every transform tool.</summary>
    public sealed class TransformGizmoPickerIntegrationTests {
        /// <summary>Ensures picker readback is available before the drag components process the same mouse press.</summary>
        [Theory]
        [InlineData(EditorViewportToolMode.Translate)]
        [InlineData(EditorViewportToolMode.Rotate)]
        [InlineData(EditorViewportToolMode.Scale)]
        public void Update_PickerReadbackStartsAndCompletesDrag(EditorViewportToolMode tool) {
            TestInputBackend input = new TestInputBackend();
            TestRenderManager3D renderer = new TestRenderManager3D();
            using Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            core.Initialize(renderer, new TestRenderManager2D(), input, new PlatformInfo("test", "test"));
            using TestGeneratedAssetGraph graph = new TestGeneratedAssetGraph(core);
            EditorSessionInteractionServices interactions = graph.InteractionServices;
            EditorEntity cameraOwner = new EditorEntity(core, interactions);
            cameraOwner.LocalPosition = new float3(0, 0, 10);
            CameraComponent camera = new CameraComponent { Viewport = new float4(0, 0, 500, 400) };
            cameraOwner.AddComponent(camera);
            CameraComponent gizmoCamera = new CameraComponent { Viewport = camera.Viewport };
            cameraOwner.AddComponent(gizmoCamera);
            TransformTranslationGizmoDragComponent translate = new TransformTranslationGizmoDragComponent(camera);
            TransformRotationGizmoDragComponent rotate = new TransformRotationGizmoDragComponent(camera);
            TransformScaleGizmoDragComponent scale = new TransformScaleGizmoDragComponent(camera);
            cameraOwner.AddComponent(translate);
            cameraOwner.AddComponent(rotate);
            cameraOwner.AddComponent(scale);
            EditorEntity selected = new EditorEntity(core, interactions);
            interactions.Selection.SetSelectedEntity(selected);
            interactions.ViewportTool.SetToolMode(camera, tool);
            EditorEntity translationRoot = new EditorEntity(core, interactions);
            EditorEntity rotationRoot = new EditorEntity(core, interactions);
            EditorEntity scaleRoot = new EditorEntity(core, interactions);
            EditorEntity handle = new EditorEntity(core, interactions) { LayerMask = EditorLayerMasks.SceneGizmo, InternalEntity = true };
            handle.AddComponent(new TransformGizmoHandleComponent(tool == EditorViewportToolMode.Rotate ? new float3(0, 0, 1) : new float3(1, 0, 0)));
            MeshComponent mesh = new MeshComponent();
            handle.AddComponent(mesh);
            EditorEntity activeRoot = tool == EditorViewportToolMode.Translate ? translationRoot : tool == EditorViewportToolMode.Rotate ? rotationRoot : scaleRoot;
            activeRoot.AddChild(handle);
            RuntimeMaterial normal = new TestRuntimeMaterial();
            RuntimeMaterial highlight = new TestRuntimeMaterial();
            EditorEntity preview = new EditorEntity(core, interactions);
            preview.AddComponent(new MeshComponent());
            translationRoot.AddComponent(new TransformTranslationGizmoFollowComponent(camera, translationRoot, normal, highlight, preview));
            rotationRoot.AddComponent(new TransformRotationGizmoFollowComponent(camera, renderer, rotationRoot, normal, highlight, preview));
            scaleRoot.AddComponent(new TransformScaleGizmoFollowComponent(camera, scaleRoot, normal, highlight));
            EditorViewportGizmoDrawableCollector collector = new EditorViewportGizmoDrawableCollector(ResolveNoAdditionalEntities, translationRoot, rotationRoot, scaleRoot);
            EditorEntity pickerOwner = new EditorEntity(core, interactions) { Enabled = false };
            CameraComponent pickerCamera = new CameraComponent();
            pickerOwner.AddComponent(pickerCamera);
            TestEditorPickingBackend backend = new TestEditorPickingBackend();
            EditorViewportPicker picker = new EditorViewportPicker(camera, gizmoCamera, collector, pickerOwner, pickerCamera, backend, graph.RendererResources);
            cameraOwner.AddComponent(picker);
            cameraOwner.InitializeHierarchy();
            translationRoot.InitializeHierarchy();
            rotationRoot.InitializeHierarchy();
            scaleRoot.InitializeHierarchy();
            SetFrame(core, input, 290, 200, ButtonState.Released);
            core.ObjectManager.Update();
            byte4 id = Assert.Single(backend.LastColors).Value;
            backend.IsReadbackReady = true;
            backend.ReadbackColor = id;
            SetFrame(core, input, 290, 200, ButtonState.Pressed);
            core.ObjectManager.Update();
            Assert.Same(handle, interactions.GizmoHover.GetHoveredHandle(camera));
            Assert.True(interactions.GizmoDrag.IsDragging(camera));
            Assert.Same(highlight, Assert.Single(mesh.Materials));
            float3 originalPosition = selected.Position;
            float3 originalScale = selected.Scale;
            float4 originalOrientation = selected.Orientation;
            SetFrame(core, input, 320, 220, ButtonState.Pressed);
            core.ObjectManager.Update();
            Assert.True(tool == EditorViewportToolMode.Translate ? selected.Position != originalPosition
                : tool == EditorViewportToolMode.Scale ? selected.Scale != originalScale : !selected.Orientation.Equals(originalOrientation));
            Assert.Equal(selected.Position, activeRoot.Position);
            SetFrame(core, input, 320, 220, ButtonState.Released);
            core.ObjectManager.Update();
            Assert.False(interactions.GizmoDrag.IsDragging(camera));
            Assert.Same(selected, interactions.Selection.SelectedEntity);
        }

        /// <summary>Returns an empty extension list so the picker only sees this test's transform handle.</summary>
        /// <returns>No additional gizmo entities.</returns>
        static IReadOnlyList<EditorEntity> ResolveNoAdditionalEntities() {
            return Array.Empty<EditorEntity>();
        }

        /// <summary>Completes the previous input frame and samples the next raw mouse state before ordered component updates.</summary>
        /// <param name="core">Core whose input owns the mouse transitions.</param>
        /// <param name="input">Configurable mouse backend.</param>
        /// <param name="x">Pointer X coordinate.</param>
        /// <param name="y">Pointer Y coordinate.</param>
        /// <param name="state">Raw left-button state.</param>
        static void SetFrame(Core core, TestInputBackend input, int x, int y, ButtonState state) {
            core.Input.Update();
            input.SetMouseState(new MouseState(x, y, 0, state, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            core.Input.EarlyUpdate();
        }
    }
}
