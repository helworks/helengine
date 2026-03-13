using helengine;
using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.gizmo {
    /// <summary>
    /// Verifies translation-gizmo follow behavior for the reusable snap-preview grid.
    /// </summary>
    public class TransformTranslationGizmoFollowComponentTests : IDisposable {
        /// <summary>
        /// Tolerance used for floating-point comparisons.
        /// </summary>
        const float FloatTolerance = 0.001f;
        /// <summary>
        /// Camera created for the current test so shared tool state can be cleaned up.
        /// </summary>
        CameraComponent CameraUnderTest;

        /// <summary>
        /// Clears shared editor state after each test.
        /// </summary>
        public void Dispose() {
            EditorSelectionService.ClearSelection();
            EditorGizmoHoverService.ClearHoveredHandle();

            if (CameraUnderTest != null) {
                EditorViewportToolService.ClearToolMode(CameraUnderTest);
                EditorGizmoDragService.EndDrag(CameraUnderTest);
            }
        }

        /// <summary>
        /// Ensures holding control shows the reusable snap-preview grid and sizes it from the active translation snap value.
        /// </summary>
        [Fact]
        public void Update_WhenControlSnapIsActive_ShowsSnapPreviewWithSnapSizedWorldScale() {
            TestInputManager input = InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera(new float3(0f, 2f, -8f));
            EditorViewportToolService.SetToolMode(sceneCamera, EditorViewportToolMode.Translate);

            RuntimeMaterial normalMaterial = new TestRuntimeMaterial();
            RuntimeMaterial highlightMaterial = new TestRuntimeMaterial();
            EditorEntity previewEntity = CreatePreviewEntity(new TestRuntimeMaterial());
            EditorEntity gizmoRoot = CreateGizmoRoot(normalMaterial, previewEntity);
            gizmoRoot.AddComponent(new TransformTranslationGizmoFollowComponent(sceneCamera, gizmoRoot, normalMaterial, highlightMaterial, previewEntity));

            EditorEntity selectedEntity = new EditorEntity();
            EditorSelectionService.SetSelectedEntity(selectedEntity);
            EditorGizmoHoverService.SetHoveredHandle(gizmoRoot.Children[0]);

            input.SetKeyboardState(new KeyboardState(Keys.LeftControl));
            input.EarlyUpdate();

            UpdateFollowComponent(gizmoRoot);

            float expectedSnapScale = (float)TransformGizmoSnapSettingsService.GetSnapValue(EditorViewportToolMode.Translate, TransformGizmoSnapSlot.Snap1);
            Assert.True(previewEntity.Enabled);
            Assert.InRange(Math.Abs(previewEntity.Scale.X - expectedSnapScale), 0f, FloatTolerance);
            Assert.InRange(Math.Abs(previewEntity.Scale.Y - expectedSnapScale), 0f, FloatTolerance);
            Assert.InRange(Math.Abs(previewEntity.Scale.Z - expectedSnapScale), 0f, FloatTolerance);
        }

        /// <summary>
        /// Ensures the snap-preview grid stays hidden when no snap modifier is active.
        /// </summary>
        [Fact]
        public void Update_WhenNoSnapModifierIsActive_HidesSnapPreview() {
            TestInputManager input = InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera(new float3(0f, 2f, -8f));
            EditorViewportToolService.SetToolMode(sceneCamera, EditorViewportToolMode.Translate);

            RuntimeMaterial normalMaterial = new TestRuntimeMaterial();
            RuntimeMaterial highlightMaterial = new TestRuntimeMaterial();
            EditorEntity previewEntity = CreatePreviewEntity(new TestRuntimeMaterial());
            EditorEntity gizmoRoot = CreateGizmoRoot(normalMaterial, previewEntity);
            gizmoRoot.AddComponent(new TransformTranslationGizmoFollowComponent(sceneCamera, gizmoRoot, normalMaterial, highlightMaterial, previewEntity));

            EditorEntity selectedEntity = new EditorEntity();
            EditorSelectionService.SetSelectedEntity(selectedEntity);
            EditorGizmoHoverService.SetHoveredHandle(gizmoRoot.Children[0]);

            input.SetKeyboardState(new KeyboardState());
            input.EarlyUpdate();

            UpdateFollowComponent(gizmoRoot);

            Assert.False(previewEntity.Enabled);
        }

        /// <summary>
        /// Initializes a fresh core with a configurable input manager for entity-based tests.
        /// </summary>
        /// <returns>Input manager used by the current test.</returns>
        TestInputManager InitializeCore() {
            Core core = new Core();
            var input = new TestInputManager();
            core.Initialize(null, null, input);
            return input;
        }

        /// <summary>
        /// Creates a scene camera entity with the supplied world position.
        /// </summary>
        /// <param name="cameraPosition">World-space camera position.</param>
        /// <returns>Configured scene camera component.</returns>
        CameraComponent CreateSceneCamera(float3 cameraPosition) {
            var cameraEntity = new EditorEntity {
                InternalEntity = true,
                Position = cameraPosition
            };

            var sceneCamera = new CameraComponent {
                Viewport = new float4(0f, 0f, 1280f, 720f)
            };
            cameraEntity.AddComponent(sceneCamera);
            Core.Instance.ObjectManager.Cameras.Clear();
            CameraUnderTest = sceneCamera;
            return sceneCamera;
        }

        /// <summary>
        /// Creates a translation gizmo root with one axis handle and one reusable preview child.
        /// </summary>
        /// <param name="material">Material assigned to the handle meshes.</param>
        /// <param name="previewEntity">Reusable preview entity owned by the gizmo root.</param>
        /// <returns>Configured translation gizmo root.</returns>
        EditorEntity CreateGizmoRoot(RuntimeMaterial material, EditorEntity previewEntity) {
            var gizmoRoot = new EditorEntity {
                InternalEntity = true,
                LayerMask = EditorLayerMasks.SceneGizmo,
                Name = "Transform Translation Gizmo"
            };

            gizmoRoot.AddChild(CreateAxisEntity("Transform Gizmo X", CreateXAxisOrientation(), material));
            gizmoRoot.AddChild(previewEntity);
            return gizmoRoot;
        }

        /// <summary>
        /// Creates one axis entity with a shaft mesh and a tip mesh.
        /// </summary>
        /// <param name="name">Axis entity name.</param>
        /// <param name="orientation">Axis orientation relative to the gizmo root.</param>
        /// <param name="material">Material assigned to the axis meshes.</param>
        /// <returns>Configured axis entity.</returns>
        EditorEntity CreateAxisEntity(string name, float4 orientation, RuntimeMaterial material) {
            var axisEntity = new EditorEntity {
                Name = name,
                InternalEntity = true,
                LayerMask = EditorLayerMasks.SceneGizmo,
                Enabled = false,
                Scale = float3.Zero,
                Orientation = orientation
            };
            axisEntity.AddComponent(new TransformGizmoHandleComponent(new float3(0f, 1f, 0f)));

            var shaftEntity = new EditorEntity {
                Name = string.Concat(name, " Shaft"),
                InternalEntity = true,
                LayerMask = EditorLayerMasks.SceneGizmo,
                Enabled = false,
                Scale = float3.Zero
            };
            MeshComponent shaftMesh = new MeshComponent();
            shaftMesh.Model = new TestRuntimeModel();
            shaftMesh.Material = material;
            shaftEntity.AddComponent(shaftMesh);
            axisEntity.AddChild(shaftEntity);

            var tipEntity = new EditorEntity {
                Name = string.Concat(name, " Tip"),
                InternalEntity = true,
                LayerMask = EditorLayerMasks.SceneGizmo,
                Enabled = false,
                Scale = float3.Zero,
                Position = new float3(0f, TransformTranslationGizmoFactory.ShaftLength, 0f)
            };
            MeshComponent tipMesh = new MeshComponent();
            tipMesh.Model = new TestRuntimeModel();
            tipMesh.Material = material;
            tipEntity.AddComponent(tipMesh);
            axisEntity.AddChild(tipEntity);

            return axisEntity;
        }

        /// <summary>
        /// Creates the reusable preview entity attached to the translation gizmo root.
        /// </summary>
        /// <param name="material">Material assigned to the preview mesh.</param>
        /// <returns>Configured preview entity.</returns>
        EditorEntity CreatePreviewEntity(RuntimeMaterial material) {
            var previewEntity = new EditorEntity {
                Name = "Transform Gizmo Snap Preview",
                InternalEntity = true,
                LayerMask = EditorLayerMasks.SceneGizmo,
                Enabled = false
            };
            MeshComponent previewMesh = new MeshComponent();
            previewMesh.Model = new TestRuntimeModel();
            previewMesh.Material = material;
            previewEntity.AddComponent(previewMesh);
            return previewEntity;
        }

        /// <summary>
        /// Updates the translation follow component attached to the supplied gizmo root.
        /// </summary>
        /// <param name="gizmoRoot">Translation gizmo root to update.</param>
        void UpdateFollowComponent(EditorEntity gizmoRoot) {
            if (gizmoRoot == null) {
                throw new ArgumentNullException(nameof(gizmoRoot));
            }

            for (int componentIndex = 0; componentIndex < gizmoRoot.Components.Count; componentIndex++) {
                if (gizmoRoot.Components[componentIndex] is TransformTranslationGizmoFollowComponent followComponent) {
                    followComponent.Update();
                    return;
                }
            }

            throw new InvalidOperationException("Expected a translation-gizmo follow component on the gizmo root.");
        }

        /// <summary>
        /// Creates the quaternion that maps +Y geometry into +X direction.
        /// </summary>
        /// <returns>Quaternion rotating +Y to +X.</returns>
        float4 CreateXAxisOrientation() {
            float3 zAxis = new float3(0f, 0f, 1f);
            float4 orientation;
            float4.CreateFromAxisAngle(ref zAxis, (float)(-Math.PI * 0.5), out orientation);
            return orientation;
        }
    }
}
