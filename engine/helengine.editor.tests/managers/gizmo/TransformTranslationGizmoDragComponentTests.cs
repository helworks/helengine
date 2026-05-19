using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.gizmo {
    /// <summary>
    /// Verifies translation gizmo drags preserve stored viewport-space coordinates for authored 2D content.
    /// </summary>
    public sealed class TransformTranslationGizmoDragComponentTests : IDisposable {
        /// <summary>
        /// Input backend used to drive deterministic drag frames.
        /// </summary>
        TestInputBackend InputBackendValue;
        /// <summary>
        /// Scene camera used by the translation gizmo under test.
        /// </summary>
        CameraComponent SceneCameraValue;

        /// <summary>
        /// Clears shared editor gizmo state and disposes the active core instance after each drag test.
        /// </summary>
        public void Dispose() {
            EditorSelectionService.ClearSelection();
            EditorGizmoHoverService.ClearHoveredHandle();
            EditorInputCaptureService.Reset();

            if (SceneCameraValue != null) {
                EditorViewportToolService.ClearToolMode(SceneCameraValue);
                EditorGizmoDragService.EndDrag(SceneCameraValue);
            }

            Core.Instance?.Dispose();
        }

        /// <summary>
        /// Ensures plane dragging one viewport-owned 2D entity updates its stored world position from the presented drag plane instead of storing the presented coordinates directly.
        /// </summary>
        [Fact]
        public void Update_WhenPlaneDraggingViewportOwnedEntity_StoresOriginalWorldSpacePosition() {
            InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera();
            Entity viewportEntity = CreateViewportOwner();
            Entity selectedEntity = CreateViewportChild(viewportEntity);
            Entity handleEntity = CreateHandleEntity();
            TransformTranslationGizmoDragComponent component = CreateDragComponent(sceneCamera);
            int2 startPointer = new int2(250, 200);
            int2 currentPointer = new int2(250, 250);
            float3 presentedStartPosition = EditorViewportDirect2DPresentationService.ResolvePresentedWorldAnchorPosition(selectedEntity);
            float3 startPlanePoint = ResolvePlanePoint(sceneCamera, startPointer, presentedStartPosition, new float3(0f, 0f, 1f));
            float3 currentPlanePoint = ResolvePlanePoint(sceneCamera, currentPointer, presentedStartPosition, new float3(0f, 0f, 1f));

            EditorViewportToolService.SetToolMode(sceneCamera, EditorViewportToolMode.Translate);
            EditorSelectionService.SetSelectedEntity(selectedEntity);
            InitializeActivePlaneDrag(component, selectedEntity, handleEntity, presentedStartPosition, startPlanePoint);

            CompleteDragFrame(component, CreateMouseState(currentPointer.X, currentPointer.Y, ButtonState.Pressed));

            float3 expectedPresentedWorldPosition = presentedStartPosition + (currentPlanePoint - startPlanePoint);
            float3 expectedStoredWorldPosition = EditorViewportDirect2DPresentationService.ResolveStoredWorldPositionFromPresentedAnchor(selectedEntity, expectedPresentedWorldPosition);
            Assert.True(currentPlanePoint.Y < 0f);
            Assert.True(selectedEntity.Position.Y > 0f);
            AssertFloat3ApproximatelyEqual(expectedStoredWorldPosition, selectedEntity.Position, 0.001f);
        }

        /// <summary>
        /// Ensures axis dragging one viewport-owned 2D entity along the presented downward axis restores a positive stored Y position behind the scenes.
        /// </summary>
        [Fact]
        public void Update_WhenAxisDraggingViewportOwnedEntity_StoresOriginalWorldSpacePosition() {
            InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera();
            Entity viewportEntity = CreateViewportOwner();
            Entity selectedEntity = CreateViewportChild(viewportEntity);
            Entity handleEntity = CreateHandleEntity();
            TransformTranslationGizmoDragComponent component = CreateDragComponent(sceneCamera);
            int2 startPointer = new int2(250, 200);
            int2 currentPointer = new int2(250, 250);
            float3 presentedStartPosition = EditorViewportDirect2DPresentationService.ResolvePresentedWorldAnchorPosition(selectedEntity);
            float3 axisDirection = new float3(0f, -1f, 0f);
            double startAxisParameter = ResolveAxisParameter(sceneCamera, startPointer, presentedStartPosition, axisDirection);
            double currentAxisParameter = ResolveAxisParameter(sceneCamera, currentPointer, presentedStartPosition, axisDirection);
            float3 expectedPresentedWorldPosition = presentedStartPosition + (axisDirection * (float)(currentAxisParameter - startAxisParameter));

            EditorViewportToolService.SetToolMode(sceneCamera, EditorViewportToolMode.Translate);
            EditorSelectionService.SetSelectedEntity(selectedEntity);
            InitializeActiveAxisDrag(component, selectedEntity, handleEntity, presentedStartPosition, axisDirection, startAxisParameter);

            CompleteDragFrame(component, CreateMouseState(currentPointer.X, currentPointer.Y, ButtonState.Pressed));

            float3 expectedStoredWorldPosition = EditorViewportDirect2DPresentationService.ResolveStoredWorldPositionFromPresentedAnchor(selectedEntity, expectedPresentedWorldPosition);
            Assert.True(expectedPresentedWorldPosition.Y < 0f);
            Assert.True(selectedEntity.Position.Y > 0f);
            AssertFloat3ApproximatelyEqual(expectedStoredWorldPosition, selectedEntity.Position, 0.001f);
        }

        /// <summary>
        /// Initializes one active core instance with deterministic input and render services for drag testing.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            InputBackendValue = new TestInputBackend();
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), InputBackendValue, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Creates one deterministic scene camera used to solve drag rays.
        /// </summary>
        /// <returns>Configured scene camera component.</returns>
        CameraComponent CreateSceneCamera() {
            EditorEntity cameraEntity = new EditorEntity {
                InternalEntity = true,
                Position = new float3(0f, 0f, 100f),
                Orientation = float4.Identity
            };

            CameraComponent sceneCamera = new CameraComponent {
                Viewport = new float4(0f, 0f, 500f, 400f)
            };
            cameraEntity.AddComponent(sceneCamera);
            SceneCameraValue = sceneCamera;
            return sceneCamera;
        }

        /// <summary>
        /// Creates one viewport-owner entity so the selected child uses the editor viewport presentation path.
        /// </summary>
        /// <returns>Viewport-owner entity.</returns>
        Entity CreateViewportOwner() {
            Entity viewportEntity = new Entity();
            viewportEntity.InitComponents();
            viewportEntity.InitChildren();
            viewportEntity.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.FixedBindingMode,
                FixedSize = new int2(1280, 720)
            });
            return viewportEntity;
        }

        /// <summary>
        /// Creates one viewport-owned child entity translated through the gizmo.
        /// </summary>
        /// <param name="viewportEntity">Viewport-owner parent for the authored 2D entity.</param>
        /// <returns>Viewport-owned child entity.</returns>
        Entity CreateViewportChild(Entity viewportEntity) {
            Entity selectedEntity = new Entity {
                LocalPosition = new float3(0f, 0f, 25f)
            };
            selectedEntity.InitComponents();
            selectedEntity.InitChildren();
            selectedEntity.AddComponent(new SpriteComponent {
                Size = new int2(64, 64)
            });
            viewportEntity.AddChild(selectedEntity);
            return selectedEntity;
        }

        /// <summary>
        /// Creates one inert handle entity used only to keep the drag active.
        /// </summary>
        /// <returns>Handle entity retained by the active drag state.</returns>
        Entity CreateHandleEntity() {
            Entity handleEntity = new Entity();
            handleEntity.InitComponents();
            handleEntity.InitChildren();
            return handleEntity;
        }

        /// <summary>
        /// Creates one translation gizmo drag component attached to an editor-owned entity.
        /// </summary>
        /// <param name="sceneCamera">Scene camera used by the gizmo drag component.</param>
        /// <returns>Configured translation gizmo drag component.</returns>
        TransformTranslationGizmoDragComponent CreateDragComponent(CameraComponent sceneCamera) {
            EditorEntity owner = new EditorEntity();
            TransformTranslationGizmoDragComponent component = new TransformTranslationGizmoDragComponent(sceneCamera);
            owner.AddComponent(component);
            return component;
        }

        /// <summary>
        /// Seeds one active plane drag state onto the translation gizmo component.
        /// </summary>
        /// <param name="component">Translation gizmo drag component under test.</param>
        /// <param name="selectedEntity">Entity being translated.</param>
        /// <param name="handleEntity">Handle retained by the active drag state.</param>
        /// <param name="presentedStartPosition">Presented world-space drag origin.</param>
        /// <param name="startPlanePoint">Initial pointer intersection point on the drag plane.</param>
        void InitializeActivePlaneDrag(
            TransformTranslationGizmoDragComponent component,
            Entity selectedEntity,
            Entity handleEntity,
            float3 presentedStartPosition,
            float3 startPlanePoint) {
            SetPrivateField(component, "IsDragging", true);
            SetPrivateField(component, "DraggedEntity", selectedEntity);
            SetPrivateField(component, "DragHandleEntity", handleEntity);
            SetPrivateField(component, "DragConstraintType", TransformGizmoHandleConstraintType.Plane);
            SetPrivateField(component, "DragPrimaryDirection", new float3(1f, 0f, 0f));
            SetPrivateField(component, "DragSecondaryDirection", new float3(0f, -1f, 0f));
            SetPrivateField(component, "DragPlaneNormal", new float3(0f, 0f, 1f));
            SetPrivateField(component, "DragStartEntityPosition", presentedStartPosition);
            SetPrivateField(component, "DragStartPlanePoint", startPlanePoint);
            SetPrivateField(component, "DragStartAxisParameter", 0.0);
        }

        /// <summary>
        /// Seeds one active axis drag state onto the translation gizmo component.
        /// </summary>
        /// <param name="component">Translation gizmo drag component under test.</param>
        /// <param name="selectedEntity">Entity being translated.</param>
        /// <param name="handleEntity">Handle retained by the active drag state.</param>
        /// <param name="presentedStartPosition">Presented world-space drag origin.</param>
        /// <param name="axisDirection">Presented world-space axis direction.</param>
        /// <param name="startAxisParameter">Initial pointer parameter along the drag axis.</param>
        void InitializeActiveAxisDrag(
            TransformTranslationGizmoDragComponent component,
            Entity selectedEntity,
            Entity handleEntity,
            float3 presentedStartPosition,
            float3 axisDirection,
            double startAxisParameter) {
            SetPrivateField(component, "IsDragging", true);
            SetPrivateField(component, "DraggedEntity", selectedEntity);
            SetPrivateField(component, "DragHandleEntity", handleEntity);
            SetPrivateField(component, "DragConstraintType", TransformGizmoHandleConstraintType.Axis);
            SetPrivateField(component, "DragPrimaryDirection", axisDirection);
            SetPrivateField(component, "DragSecondaryDirection", float3.Zero);
            SetPrivateField(component, "DragPlaneNormal", new float3(0f, 0f, 1f));
            SetPrivateField(component, "DragStartEntityPosition", presentedStartPosition);
            SetPrivateField(component, "DragStartAxisParameter", startAxisParameter);
            SetPrivateField(component, "DragStartPlanePoint", float3.Zero);
        }

        /// <summary>
        /// Advances one full drag frame for the supplied component and mouse state.
        /// </summary>
        /// <param name="component">Translation gizmo drag component under test.</param>
        /// <param name="mouseState">Mouse state exposed during the frame.</param>
        void CompleteDragFrame(UpdateComponent component, MouseState mouseState) {
            InputBackendValue.SetMouseState(mouseState);
            InputBackendValue.EarlyUpdate();
            component.Update();
            InputBackendValue.Update();
        }

        /// <summary>
        /// Creates one mouse state with the supplied left button state.
        /// </summary>
        /// <param name="x">Pointer X coordinate in client pixels.</param>
        /// <param name="y">Pointer Y coordinate in client pixels.</param>
        /// <param name="leftButton">Left button state for the frame.</param>
        /// <returns>Mouse state supplied to the active input backend.</returns>
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
        /// Resolves one world-space plane intersection point from the supplied pointer.
        /// </summary>
        /// <param name="sceneCamera">Scene camera used to build the pointer ray.</param>
        /// <param name="pointer">Pointer position in window coordinates.</param>
        /// <param name="planeOrigin">World-space origin point on the drag plane.</param>
        /// <param name="planeNormal">Normalized world-space plane normal.</param>
        /// <returns>Intersection point on the drag plane.</returns>
        float3 ResolvePlanePoint(CameraComponent sceneCamera, int2 pointer, float3 planeOrigin, float3 planeNormal) {
            bool rayBuilt = EditorViewportPointerRayBuilder.TryBuildPerspectiveCameraRay(sceneCamera, pointer, out float3 rayOrigin, out float3 rayDirection);
            Assert.True(rayBuilt);

            double denominator = float3.Dot(planeNormal, rayDirection);
            Assert.NotEqual(0.0, denominator);

            float3 planeDelta = planeOrigin - rayOrigin;
            double distanceAlongRay = float3.Dot(planeDelta, planeNormal) / denominator;
            return rayOrigin + (rayDirection * (float)distanceAlongRay);
        }

        /// <summary>
        /// Resolves one closest-point parameter along the supplied axis from the current pointer.
        /// </summary>
        /// <param name="sceneCamera">Scene camera used to build the pointer ray.</param>
        /// <param name="pointer">Pointer position in window coordinates.</param>
        /// <param name="axisOrigin">World-space axis origin.</param>
        /// <param name="axisDirection">Normalized world-space axis direction.</param>
        /// <returns>Closest-point parameter along the supplied axis.</returns>
        double ResolveAxisParameter(CameraComponent sceneCamera, int2 pointer, float3 axisOrigin, float3 axisDirection) {
            bool rayBuilt = EditorViewportPointerRayBuilder.TryBuildPerspectiveCameraRay(sceneCamera, pointer, out float3 rayOrigin, out float3 rayDirection);
            Assert.True(rayBuilt);

            double rayAxisDot = float3.Dot(rayDirection, axisDirection);
            double denominator = 1.0 - (rayAxisDot * rayAxisDot);
            Assert.NotEqual(0.0, denominator);

            float3 cameraToAxis = rayOrigin - axisOrigin;
            double rayCameraDot = float3.Dot(rayDirection, cameraToAxis);
            double axisCameraDot = float3.Dot(axisDirection, cameraToAxis);
            return (axisCameraDot - (rayAxisDot * rayCameraDot)) / denominator;
        }

        /// <summary>
        /// Sets one private field through reflection for focused drag-state setup.
        /// </summary>
        /// <param name="target">Object whose private field should be updated.</param>
        /// <param name="fieldName">Private field name to update.</param>
        /// <param name="value">Value assigned to the private field.</param>
        void SetPrivateField(object target, string fieldName, object value) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(target, value);
        }

        /// <summary>
        /// Verifies two vectors are equal within one small tolerance on every axis.
        /// </summary>
        /// <param name="expected">Expected vector.</param>
        /// <param name="actual">Actual vector.</param>
        /// <param name="tolerance">Inclusive tolerance applied per component.</param>
        void AssertFloat3ApproximatelyEqual(float3 expected, float3 actual, float tolerance) {
            Assert.InRange(actual.X, expected.X - tolerance, expected.X + tolerance);
            Assert.InRange(actual.Y, expected.Y - tolerance, expected.Y + tolerance);
            Assert.InRange(actual.Z, expected.Z - tolerance, expected.Z + tolerance);
        }
    }
}
