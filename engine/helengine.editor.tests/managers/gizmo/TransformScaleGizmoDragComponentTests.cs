using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.gizmo {
    /// <summary>
    /// Verifies scale gizmo drags operate on stored local scale values instead of composed world scale.
    /// </summary>
    public sealed class TransformScaleGizmoDragComponentTests : IDisposable {
        readonly Core CoreValue;
        readonly TestGeneratedAssetGraph GeneratedAssetGraph;
        readonly helengine.editor.EditorSessionInteractionServices InteractionServices;
        readonly TestInputBackend InputBackendValue;
        /// <summary>
        /// Camera created for the current test so shared gizmo state can be cleaned up.
        /// </summary>
        CameraComponent SceneCameraValue;

        public TransformScaleGizmoDragComponentTests() {
            CoreValue = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            InputBackendValue = new TestInputBackend();
            CoreValue.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), InputBackendValue, new PlatformInfo("test", "test-version"));
            GeneratedAssetGraph = new TestGeneratedAssetGraph(CoreValue);
            InteractionServices = GeneratedAssetGraph.InteractionServices;
        }

        /// <summary>
        /// Clears shared editor state and disposes the active core after each drag test.
        /// </summary>
        public void Dispose() {
            InteractionServices.Selection.ClearSelection();
            InteractionServices.GizmoHover.ClearHoveredHandle();
            if (SceneCameraValue != null) {
                InteractionServices.ViewportTool.ClearToolMode(SceneCameraValue);
                InteractionServices.GizmoDrag.EndDrag(SceneCameraValue);
            }

            GeneratedAssetGraph.Dispose();
            CoreValue.Dispose();
        }

        /// <summary>
        /// Ensures starting one scale drag on a child of a scaled parent captures the child's stored local scale.
        /// </summary>
        [Fact]
        public void Update_WhenScaleDragBeginsForParentedSelection_CapturesLocalScale() {
            TestInputBackend input = InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera();
            InteractionServices.ViewportTool.SetToolMode(sceneCamera, EditorViewportToolMode.Scale);

            EditorEntity parentEntity = new EditorEntity(CoreValue, InteractionServices) {
                Scale = new float3(2f, 2f, 2f)
            };
            EditorEntity selectedEntity = new EditorEntity(CoreValue, InteractionServices) {
                Position = float3.Zero,
                LocalScale = new float3(1f, 1f, 1f),
                Orientation = float4.Identity
            };
            parentEntity.AddChild(selectedEntity);
            InteractionServices.Selection.SetSelectedEntity(selectedEntity);

            EditorEntity handleEntity = CreateAxisHandleEntity();
            EditorEntity owner = new EditorEntity(CoreValue, InteractionServices);
            TransformScaleGizmoDragComponent component = new TransformScaleGizmoDragComponent(sceneCamera);
            owner.AddComponent(component);

            CompleteDragFrame(input, component, CreateMouseState(250, 200, ButtonState.Pressed));

            Assert.True(GetPrivateField<bool>(component, "IsDragging"));
            Assert.Equal(selectedEntity.LocalScale, GetPrivateField<float3>(component, "DragStartEntityScale"));
            Assert.NotEqual(selectedEntity.Scale, GetPrivateField<float3>(component, "DragStartEntityScale"));
            Assert.Same(handleEntity, GetPrivateField<Entity>(component, "DragHandleEntity"));
        }

        /// <summary>
        /// Initializes a fresh core with deterministic client bounds for drag tests.
        /// </summary>
        /// <returns>Input backend used by the current test.</returns>
        TestInputBackend InitializeCore() {
            CoreValue.InputSystem.SetMouseClientBounds(new int2(500, 400));
            return InputBackendValue;
        }

        /// <summary>
        /// Creates a scene camera with a deterministic viewport rectangle.
        /// </summary>
        /// <returns>Configured scene camera component used by the drag controller.</returns>
        CameraComponent CreateSceneCamera() {
            EditorEntity cameraEntity = new EditorEntity(CoreValue, InteractionServices) {
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
        /// Creates one axis-constrained gizmo handle entity and registers it as hovered.
        /// </summary>
        /// <returns>Hovered handle entity.</returns>
        EditorEntity CreateAxisHandleEntity() {
            EditorEntity handleEntity = new EditorEntity(CoreValue, InteractionServices) {
                InternalEntity = true,
                Orientation = float4.Identity
            };

            handleEntity.AddComponent(new TransformGizmoHandleComponent(new float3(1f, 0f, 0f)));
            InteractionServices.GizmoHover.SetHoveredHandle(handleEntity);
            return handleEntity;
        }

        /// <summary>
        /// Captures input, executes one drag-controller frame, and finalizes the input frame.
        /// </summary>
        /// <param name="input">Input backend supplying the current mouse state.</param>
        /// <param name="component">Drag component being exercised.</param>
        /// <param name="mouseState">Mouse state exposed for the current frame.</param>
        void CompleteDragFrame(TestInputBackend input, UpdateComponent component, MouseState mouseState) {
            if (input == null) {
                throw new ArgumentNullException(nameof(input));
            }
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }

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
        /// <returns>Mouse state used by the drag test.</returns>
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
        /// Reads one private field value through reflection for focused drag-state assertions.
        /// </summary>
        /// <typeparam name="T">Expected field value type.</typeparam>
        /// <param name="target">Object whose private field should be read.</param>
        /// <param name="fieldName">Private field name to retrieve.</param>
        /// <returns>Current private field value.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            }
            if (string.IsNullOrWhiteSpace(fieldName)) {
                throw new ArgumentException("Field name must be provided.", nameof(fieldName));
            }

            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) {
                throw new InvalidOperationException(string.Concat("Could not find private field '", fieldName, "'."));
            }

            return (T)field.GetValue(target);
        }
    }
}
