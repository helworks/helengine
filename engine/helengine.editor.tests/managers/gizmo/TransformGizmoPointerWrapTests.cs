using System.Reflection;
using helengine;
using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.gizmo {
    /// <summary>
    /// Verifies transform-gizmo drag components request client-edge pointer wrapping only while a drag remains active.
    /// </summary>
    public class TransformGizmoPointerWrapTests : IDisposable {
        /// <summary>
        /// Camera created for the current test so shared tool and drag state can be cleaned up.
        /// </summary>
        CameraComponent CameraUnderTest;

        /// <summary>
        /// Clears shared editor state after each gizmo pointer-wrap test.
        /// </summary>
        public void Dispose() {
            EditorSelectionService.ClearSelection();
            EditorGizmoHoverService.ClearHoveredHandle();
            EditorInputCaptureService.Reset();

            if (CameraUnderTest != null) {
                EditorViewportToolService.ClearToolMode(CameraUnderTest);
                EditorGizmoDragService.EndDrag(CameraUnderTest);
            }
        }

        /// <summary>
        /// Ensures translation drags keep pointer wrapping enabled until the drag is released.
        /// </summary>
        [Fact]
        public void Update_WhenTranslationDragIsActive_EnablesPointerWrapUntilRelease() {
            TestInputBackend input = InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera();
            EditorViewportToolService.SetToolMode(sceneCamera, EditorViewportToolMode.Translate);
            EditorEntity selectedEntity = CreateSelectedEntity();
            EditorEntity handleEntity = CreateAxisHandleEntity();
            EditorEntity owner = new EditorEntity();
            TransformTranslationGizmoDragComponent component = new TransformTranslationGizmoDragComponent(sceneCamera);
            owner.AddComponent(component);
            InitializeActiveTranslationDrag(component, selectedEntity, handleEntity);

            CompleteDragFrame(input, component, CreateMouseState(250, 200, ButtonState.Pressed));
            Assert.True(Core.Instance.InputSystem.IsPointerWrapEnabled);

            CompleteDragFrame(input, component, CreateMouseState(250, 200, ButtonState.Released));
            Assert.False(Core.Instance.InputSystem.IsPointerWrapEnabled);
        }

        /// <summary>
        /// Ensures rotation drags keep pointer wrapping enabled until the drag is released.
        /// </summary>
        [Fact]
        public void Update_WhenRotationDragIsActive_EnablesPointerWrapUntilRelease() {
            TestInputBackend input = InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera();
            EditorViewportToolService.SetToolMode(sceneCamera, EditorViewportToolMode.Rotate);
            EditorEntity selectedEntity = CreateSelectedEntity();
            EditorEntity handleEntity = CreateAxisHandleEntity();
            EditorEntity owner = new EditorEntity();
            TransformRotationGizmoDragComponent component = new TransformRotationGizmoDragComponent(sceneCamera);
            owner.AddComponent(component);
            InitializeActiveRotationDrag(component, selectedEntity, handleEntity);

            CompleteDragFrame(input, component, CreateMouseState(250, 200, ButtonState.Pressed));
            Assert.True(Core.Instance.InputSystem.IsPointerWrapEnabled);

            CompleteDragFrame(input, component, CreateMouseState(250, 200, ButtonState.Released));
            Assert.False(Core.Instance.InputSystem.IsPointerWrapEnabled);
        }

        /// <summary>
        /// Ensures scale drags keep pointer wrapping enabled until the drag is released.
        /// </summary>
        [Fact]
        public void Update_WhenScaleDragIsActive_EnablesPointerWrapUntilRelease() {
            TestInputBackend input = InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera();
            EditorViewportToolService.SetToolMode(sceneCamera, EditorViewportToolMode.Scale);
            EditorEntity selectedEntity = CreateSelectedEntity();
            EditorEntity handleEntity = CreateAxisHandleEntity();
            EditorEntity owner = new EditorEntity();
            TransformScaleGizmoDragComponent component = new TransformScaleGizmoDragComponent(sceneCamera);
            owner.AddComponent(component);
            InitializeActiveScaleDrag(component, selectedEntity, handleEntity);

            CompleteDragFrame(input, component, CreateMouseState(250, 200, ButtonState.Pressed));
            Assert.True(Core.Instance.InputSystem.IsPointerWrapEnabled);

            CompleteDragFrame(input, component, CreateMouseState(250, 200, ButtonState.Released));
            Assert.False(Core.Instance.InputSystem.IsPointerWrapEnabled);
        }

        /// <summary>
        /// Initializes a fresh core with deterministic client bounds for pointer-wrap tests.
        /// </summary>
        /// <returns>Input manager used by the current test.</returns>
        TestInputBackend InitializeCore() {
            Core core = new Core();
            TestInputBackend input = new TestInputBackend();
            core.InputSystem.SetMouseClientBounds(new int2(500, 400));
            core.Initialize(null, null, input);
            return input;
        }

        /// <summary>
        /// Creates a scene camera with a deterministic viewport rectangle.
        /// </summary>
        /// <returns>Configured camera component used by the drag controller.</returns>
        CameraComponent CreateSceneCamera() {
            EditorEntity cameraEntity = new EditorEntity {
                InternalEntity = true
            };

            CameraComponent sceneCamera = new CameraComponent {
                Viewport = new float4(0f, 0f, 500f, 400f)
            };

            cameraEntity.AddComponent(sceneCamera);
            CameraUnderTest = sceneCamera;
            return sceneCamera;
        }

        /// <summary>
        /// Creates the selected entity transformed by the active gizmo drag.
        /// </summary>
        /// <returns>Selected entity registered with the editor selection service.</returns>
        EditorEntity CreateSelectedEntity() {
            EditorEntity selectedEntity = new EditorEntity {
                Position = new float3(0f, 0f, 0f),
                Scale = new float3(1f, 1f, 1f),
                Orientation = float4.Identity
            };

            EditorSelectionService.SetSelectedEntity(selectedEntity);
            return selectedEntity;
        }

        /// <summary>
        /// Creates one axis-constrained gizmo handle entity.
        /// </summary>
        /// <returns>Handle entity used by the active drag state.</returns>
        EditorEntity CreateAxisHandleEntity() {
            EditorEntity handleEntity = new EditorEntity {
                InternalEntity = true,
                Orientation = float4.Identity
            };

            handleEntity.AddComponent(new TransformGizmoHandleComponent(new float3(1f, 0f, 0f)));
            EditorGizmoHoverService.SetHoveredHandle(handleEntity);
            return handleEntity;
        }

        /// <summary>
        /// Seeds the translation drag component with an active axis drag.
        /// </summary>
        /// <param name="component">Translation drag component under test.</param>
        /// <param name="selectedEntity">Entity being translated.</param>
        /// <param name="handleEntity">Handle driving the translation drag.</param>
        void InitializeActiveTranslationDrag(TransformTranslationGizmoDragComponent component, Entity selectedEntity, Entity handleEntity) {
            SetPrivateField(component, "IsDragging", true);
            SetPrivateField(component, "DraggedEntity", selectedEntity);
            SetPrivateField(component, "DragHandleEntity", handleEntity);
            SetPrivateField(component, "DragConstraintType", TransformGizmoHandleConstraintType.Axis);
            SetPrivateField(component, "DragPrimaryDirection", new float3(1f, 0f, 0f));
            SetPrivateField(component, "DragSecondaryDirection", float3.Zero);
            SetPrivateField(component, "DragPlaneNormal", new float3(0f, 1f, 0f));
            SetPrivateField(component, "DragStartEntityPosition", selectedEntity.Position);
            SetPrivateField(component, "DragStartAxisParameter", 0.0);
            SetPrivateField(component, "DragStartPlanePoint", float3.Zero);
        }

        /// <summary>
        /// Seeds the rotation drag component with an active ring drag.
        /// </summary>
        /// <param name="component">Rotation drag component under test.</param>
        /// <param name="selectedEntity">Entity being rotated.</param>
        /// <param name="handleEntity">Handle driving the rotation drag.</param>
        void InitializeActiveRotationDrag(TransformRotationGizmoDragComponent component, Entity selectedEntity, Entity handleEntity) {
            SetPrivateField(component, "IsDragging", true);
            SetPrivateField(component, "DraggedEntity", selectedEntity);
            SetPrivateField(component, "DragHandleEntity", handleEntity);
            SetPrivateField(component, "DragRotationAxis", new float3(0f, 1f, 0f));
            SetPrivateField(component, "DragRotationCenter", selectedEntity.Position);
            SetPrivateField(component, "DragStartEntityOrientation", selectedEntity.Orientation);
            SetPrivateField(component, "DragAccumulatedAngle", 0.0);
            SetPrivateField(component, "DragPreviousVector", new float3(1f, 0f, 0f));
        }

        /// <summary>
        /// Seeds the scale drag component with an active axis drag.
        /// </summary>
        /// <param name="component">Scale drag component under test.</param>
        /// <param name="selectedEntity">Entity being scaled.</param>
        /// <param name="handleEntity">Handle driving the scale drag.</param>
        void InitializeActiveScaleDrag(TransformScaleGizmoDragComponent component, Entity selectedEntity, Entity handleEntity) {
            SetPrivateField(component, "IsDragging", true);
            SetPrivateField(component, "DraggedEntity", selectedEntity);
            SetPrivateField(component, "DragHandleEntity", handleEntity);
            SetPrivateField(component, "DragConstraintType", TransformGizmoHandleConstraintType.Axis);
            SetPrivateField(component, "DragPrimaryDirection", new float3(1f, 0f, 0f));
            SetPrivateField(component, "DragSecondaryDirection", float3.Zero);
            SetPrivateField(component, "DragPlaneNormal", new float3(0f, 1f, 0f));
            SetPrivateField(component, "DragStartEntityScale", selectedEntity.Scale);
            SetPrivateField(component, "DragStartEntityPosition", selectedEntity.Position);
            SetPrivateField(component, "DragStartAxisParameter", 0.0);
            SetPrivateField(component, "DragStartPlanePoint", float3.Zero);
        }

        /// <summary>
        /// Captures input, executes one drag-controller frame, and finalizes the input frame.
        /// </summary>
        /// <param name="input">Input manager supplying the current mouse state.</param>
        /// <param name="component">Drag component being exercised.</param>
        /// <param name="mouseState">Mouse state exposed for the current frame.</param>
        void CompleteDragFrame(TestInputBackend input, UpdateComponent component, MouseState mouseState) {
            input.SetMouseState(mouseState);
            input.EarlyUpdate();
            component.Update();
            input.Update();
        }

        /// <summary>
        /// Creates one mouse state with the supplied left-button state.
        /// </summary>
        /// <param name="x">Pointer X coordinate in client pixels.</param>
        /// <param name="y">Pointer Y coordinate in client pixels.</param>
        /// <param name="leftButton">Left mouse button state for the frame.</param>
        /// <returns>Mouse state used by the drag tests.</returns>
        MouseState CreateMouseState(int x, int y, ButtonState leftButton) {
            return new MouseState(
                x,
                y,
                0,
                leftButton,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released);
        }

        /// <summary>
        /// Sets one private field value through reflection for focused drag-state seeding.
        /// </summary>
        /// <param name="target">Object whose private field should be updated.</param>
        /// <param name="fieldName">Private field name to update.</param>
        /// <param name="value">Value assigned to the private field.</param>
        void SetPrivateField(object target, string fieldName, object value) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(target, value);
        }
    }
}

