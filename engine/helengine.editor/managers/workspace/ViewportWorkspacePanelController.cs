using System.Text.Json;

namespace helengine.editor {
    /// <summary>
    /// Creates and owns one independent viewport runtime stack for the workspace panel system.
    /// </summary>
    public sealed class ViewportWorkspacePanelController : IEditorWorkspacePanelController {
        /// <summary>
        /// Draw order used by scene cameras created for workspace viewports.
        /// </summary>
        const byte SceneCameraDrawOrder = 0;
        /// <summary>
        /// Built-in shader file used by the normal transform-gizmo material.
        /// </summary>
        const string TransformGizmoShaderFileName = "EditorTransformGizmo.hlsl";
        /// <summary>
        /// Built-in shader file used by the highlighted transform-gizmo material.
        /// </summary>
        const string TransformGizmoHighlightShaderFileName = "EditorTransformGizmoHighlight.hlsl";
        /// <summary>
        /// Shared runtime shader variant used by editor transform gizmo materials.
        /// </summary>
        const string DefaultRuntimeShaderVariant = "default";
        /// <summary>
        /// Draw order used by gizmo overlay cameras created for workspace viewports.
        /// </summary>
        const byte GizmoCameraDrawOrder = 1;
        /// <summary>
        /// Default far clip plane used by workspace scene cameras so large editor-authored scene aids remain visible.
        /// </summary>
        const float DefaultSceneCameraFarPlaneDistance = 5000f;
        /// <summary>
        /// Default picker render target width used before the viewport lays out.
        /// </summary>
        const int DefaultPickerRenderTargetWidth = 640;
        /// <summary>
        /// Default picker render target height used before the viewport lays out.
        /// </summary>
        const int DefaultPickerRenderTargetHeight = 360;
        /// <summary>
        /// Minimum camera-to-pivot separation accepted when restoring a workspace pose.
        /// </summary>
        const double MinimumWorkspaceOrbitDistance = 0.001;
        /// <summary>
        /// Shared JSON options used to deserialize persisted viewport state payloads written with camelCase names.
        /// </summary>
        static JsonSerializerOptions ViewportStateJsonSerializerOptions { get; } = new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Runtime stack owned by this viewport panel instance.
        /// </summary>
        readonly EditorViewportWorkspaceState State;
        /// <summary>
        /// Frames the current editor selection inside the viewport scene camera on demand.
        /// </summary>
        readonly EditorViewportSelectionFramingService SelectionFramingService;
        readonly EditorBuiltInShaderAssetLibrary BuiltInShaderLibrary;
        readonly EngineGeneratedMaterialCache GeneratedMaterialCache;
        readonly EditorSessionRendererResources RendererResources;
        /// <summary>
        /// Host-owned factory used to create one picking backend per viewport.
        /// </summary>
        readonly IEditorPickingBackendFactory PickingBackendFactory;
        Core OwnerCore => RendererResources.ObjectManager.OwnerCore ?? throw new InvalidOperationException("Viewport renderer resources must be bound to an owning core.");

        /// <summary>
        /// Initializes one workspace controller and its independent viewport runtime stack.
        /// </summary>
        /// <param name="font">Font used by the viewport title bar and toolbar.</param>
        /// <param name="snapModifierFont">Font used by viewport snap modifier labels.</param>
        /// <param name="toolbarIcons">Runtime textures used by the viewport toolbar.</param>
        /// <param name="sceneCanvasProfileState">Scene-owned canvas profile shared across viewports.</param>
        /// <param name="metrics">Scaled editor UI metrics used by the dockable viewport.</param>
        /// <param name="pickingBackendFactory">Optional host factory that supplies one native picking backend per viewport.</param>
        public ViewportWorkspacePanelController(
            FontAsset font,
            FontAsset snapModifierFont,
            EditorViewportToolbarIconSet toolbarIcons,
            EditorSceneCanvasProfileState sceneCanvasProfileState,
            EditorUiMetrics metrics,
            EditorBuiltInShaderAssetLibrary builtInShaderLibrary,
            EngineGeneratedMaterialCache generatedMaterialCache,
            EditorSessionRendererResources rendererResources,
            IEditorPickingBackendFactory pickingBackendFactory = null) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }
            if (snapModifierFont == null) {
                throw new ArgumentNullException(nameof(snapModifierFont));
            }
            if (toolbarIcons == null) {
                throw new ArgumentNullException(nameof(toolbarIcons));
            }
            if (sceneCanvasProfileState == null) {
                throw new ArgumentNullException(nameof(sceneCanvasProfileState));
            }
            if (metrics == null) {
                throw new ArgumentNullException(nameof(metrics));
            }
            BuiltInShaderLibrary = builtInShaderLibrary ?? throw new ArgumentNullException(nameof(builtInShaderLibrary));
            GeneratedMaterialCache = generatedMaterialCache ?? throw new ArgumentNullException(nameof(generatedMaterialCache));
            RendererResources = rendererResources ?? throw new ArgumentNullException(nameof(rendererResources));

            PickingBackendFactory = pickingBackendFactory;

            SelectionFramingService = new EditorViewportSelectionFramingService();
            State = CreateViewportState(font, snapModifierFont, toolbarIcons, sceneCanvasProfileState, metrics);
        }

        /// <summary>
        /// Initializes one workspace controller around an existing viewport runtime stack.
        /// </summary>
        /// <param name="state">Existing viewport runtime stack owned by the controller.</param>
        /// <param name="pickingBackendFactory">Optional host factory that supplies one native picking backend per viewport.</param>
        public ViewportWorkspacePanelController(EditorViewportWorkspaceState state, EditorBuiltInShaderAssetLibrary builtInShaderLibrary, EngineGeneratedMaterialCache generatedMaterialCache, EditorSessionRendererResources rendererResources,
            IEditorPickingBackendFactory pickingBackendFactory = null) {
            BuiltInShaderLibrary = builtInShaderLibrary ?? throw new ArgumentNullException(nameof(builtInShaderLibrary));
            GeneratedMaterialCache = generatedMaterialCache ?? throw new ArgumentNullException(nameof(generatedMaterialCache));
            RendererResources = rendererResources ?? throw new ArgumentNullException(nameof(rendererResources));
            PickingBackendFactory = pickingBackendFactory;

            SelectionFramingService = new EditorViewportSelectionFramingService();
            State = state ?? throw new ArgumentNullException(nameof(state));
            WireViewportCallbacks(State);
        }

        /// <summary>
        /// Gets the dockable viewport panel owned by this controller.
        /// </summary>
        public DockableEntity Dockable => State.Viewport;
        /// <summary>
        /// Gets the runtime viewport stack owned by this controller.
        /// </summary>
        public EditorViewportWorkspaceState ViewportState => State;

        /// <summary>
        /// Captures one serializable state payload for the viewport instance.
        /// </summary>
        /// <returns>Serializable viewport state payload.</returns>
        public object CaptureState() {
            return new ViewportWorkspacePanelStateDocument {
                CameraPositionX = State.SceneCameraEntity.Position.X,
                CameraPositionY = State.SceneCameraEntity.Position.Y,
                CameraPositionZ = State.SceneCameraEntity.Position.Z,
                CameraOrientationX = State.SceneCameraEntity.Orientation.X,
                CameraOrientationY = State.SceneCameraEntity.Orientation.Y,
                CameraOrientationZ = State.SceneCameraEntity.Orientation.Z,
                CameraOrientationW = State.SceneCameraEntity.Orientation.W,
                ProjectionMode = GetProjectionSettings(State.SceneCamera).ProjectionMode,
                OrthographicVerticalSpan = GetCapturedOrthographicSpan(State.SceneCamera, State.SceneCameraEntity.Position,
                    State.CameraController.GetOrbitTarget()),
                HasOrbitPivot = true,
                OrbitPivotX = State.CameraController.GetOrbitTarget().X,
                OrbitPivotY = State.CameraController.GetOrbitTarget().Y,
                OrbitPivotZ = State.CameraController.GetOrbitTarget().Z,
                ToolMode = State.Viewport.ToolMode,
                NearPlaneDistance = State.SceneCamera.NearPlaneDistance,
                FarPlaneDistance = State.SceneCamera.FarPlaneDistance,
                CameraSpeedMode = State.Viewport.CameraSpeedMode,
                ManualCameraSpeedOverride = State.Viewport.ManualCameraSpeedOverride,
                CanvasWidth = State.Viewport.CanvasPreviewSettings.CanvasWidth,
                CanvasHeight = State.Viewport.CanvasPreviewSettings.CanvasHeight,
                PixelsPerWorldUnit = State.Viewport.CanvasPreviewSettings.PixelsPerWorldUnit,
                IsGridVisible = IsGridVisible(),
                IsSettingsOverlayOpen = State.Viewport.IsSettingsOverlayVisible,
                TranslateSnap1 = EditorSessionInteractionServices.From(State.Viewport).TransformSnap.GetSnapValue(State.SceneCamera, EditorViewportToolMode.Translate, TransformGizmoSnapSlot.Snap1),
                TranslateSnap2 = EditorSessionInteractionServices.From(State.Viewport).TransformSnap.GetSnapValue(State.SceneCamera, EditorViewportToolMode.Translate, TransformGizmoSnapSlot.Snap2),
                RotateSnap1 = EditorSessionInteractionServices.From(State.Viewport).TransformSnap.GetSnapValue(State.SceneCamera, EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap1),
                RotateSnap2 = EditorSessionInteractionServices.From(State.Viewport).TransformSnap.GetSnapValue(State.SceneCamera, EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap2),
                ScaleSnap1 = EditorSessionInteractionServices.From(State.Viewport).TransformSnap.GetSnapValue(State.SceneCamera, EditorViewportToolMode.Scale, TransformGizmoSnapSlot.Snap1),
                ScaleSnap2 = EditorSessionInteractionServices.From(State.Viewport).TransformSnap.GetSnapValue(State.SceneCamera, EditorViewportToolMode.Scale, TransformGizmoSnapSlot.Snap2)
            };
        }

        /// <summary>
        /// Restores one previously captured viewport state payload.
        /// </summary>
        /// <param name="state">Viewport state payload to reapply.</param>
        public void RestoreState(object state) {
            if (state == null) {
                return;
            }

            ViewportWorkspacePanelStateDocument document = ResolveStateDocument(state);
            ValidateNavigationState(document);
            float3 cameraPosition = new float3(
                document.CameraPositionX,
                document.CameraPositionY,
                document.CameraPositionZ);
            float4 cameraOrientation = new float4(
                document.CameraOrientationX,
                document.CameraOrientationY,
                document.CameraOrientationZ,
                document.CameraOrientationW);
            ValidateCameraPose(cameraPosition, cameraOrientation);
            cameraOrientation.Normalize();
            State.NavigationController.CancelTransition();
            State.SceneCameraEntity.Position = cameraPosition;
            State.SceneCameraEntity.Orientation = cameraOrientation;
            float3 orbitPivot;
            double orbitDistance;
            if (document.HasOrbitPivot) {
                orbitPivot = new float3(document.OrbitPivotX, document.OrbitPivotY, document.OrbitPivotZ);
                orbitDistance = GetDistance(cameraPosition, orbitPivot);
                State.CameraController.SetOrbitState(orbitPivot, orbitDistance);
            } else {
                State.CameraController.ResetOrbitTargetFromCamera();
                orbitPivot = State.CameraController.GetOrbitTarget();
                orbitDistance = GetDistance(cameraPosition, orbitPivot);
            }
            if (!double.IsFinite(orbitDistance) || orbitDistance < MinimumWorkspaceOrbitDistance) {
                throw new ArgumentOutOfRangeException(nameof(state), "The saved camera pose must have a finite, non-zero orbit distance.");
            }
            float resolvedSpan = document.OrthographicVerticalSpan == 0f
                ? DeriveOrthographicSpan(State.SceneCamera, orbitDistance)
                : document.OrthographicVerticalSpan;
            ICameraProjectionSettings projectionSettings = GetProjectionSettings(State.SceneCamera);
            projectionSettings.OrthographicVerticalSpan = resolvedSpan;
            projectionSettings.ProjectionMode = document.ProjectionMode;
            State.Viewport.ToolMode = document.ToolMode;
            State.SceneCamera.NearPlaneDistance = document.NearPlaneDistance;
            State.SceneCamera.FarPlaneDistance = document.FarPlaneDistance;
            State.Viewport.CameraSpeedMode = document.CameraSpeedMode;
            State.Viewport.ManualCameraSpeedOverride = document.ManualCameraSpeedOverride <= 0.0
                ? EditorViewportCameraController.DefaultMoveSpeed
                : document.ManualCameraSpeedOverride;
            State.Viewport.CanvasPreviewSettings.CanvasWidth = document.CanvasWidth;
            State.Viewport.CanvasPreviewSettings.CanvasHeight = document.CanvasHeight;
            State.Viewport.CanvasPreviewSettings.PixelsPerWorldUnit = document.PixelsPerWorldUnit;
            SetGridVisible(document.IsGridVisible);
            State.Viewport.SetSettingsOverlayOpen(document.IsSettingsOverlayOpen);
            EditorSessionInteractionServices.From(State.Viewport).TransformSnap.ResetDefaults(State.SceneCamera);
            RestoreSnapValue(EditorViewportToolMode.Translate, TransformGizmoSnapSlot.Snap1, document.TranslateSnap1);
            RestoreSnapValue(EditorViewportToolMode.Translate, TransformGizmoSnapSlot.Snap2, document.TranslateSnap2);
            RestoreSnapValue(EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap1, document.RotateSnap1);
            RestoreSnapValue(EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap2, document.RotateSnap2);
            RestoreSnapValue(EditorViewportToolMode.Scale, TransformGizmoSnapSlot.Snap1, document.ScaleSnap1);
            RestoreSnapValue(EditorViewportToolMode.Scale, TransformGizmoSnapSlot.Snap2, document.ScaleSnap2);
            SynchronizeCameraStack(State);
        }

        /// <summary>
        /// Disposes the viewport panel and its independent runtime camera stack.
        /// </summary>
        public void Dispose() {
            State.Viewport.DetachNavigationCube();
            State.Viewport.ClearInputBlockers();
            EditorSessionInteractionServices.From(State.Viewport).GizmoHover.ClearHoveredHandle(State.SceneCamera);
            EditorSessionInteractionServices.From(State.Viewport).GizmoDrag.EndDrag(State.SceneCamera);
            EditorSessionInteractionServices.From(State.Viewport).ViewportTool.ClearToolMode(State.SceneCamera);
            EditorSessionInteractionServices.From(State.Viewport).TransformSnap.ClearState(State.SceneCamera);
            State.TranslationGizmoRoot.Dispose();
            State.RotationGizmoRoot.Dispose();
            State.ScaleGizmoRoot.Dispose();
            State.SceneCameraEntity.Dispose();
            State.PickerCameraEntity.Dispose();
        }

        /// <summary>
        /// Creates one independent viewport runtime stack and returns its state bundle.
        /// </summary>
        /// <param name="font">Font used by the viewport title bar and toolbar.</param>
        /// <param name="snapModifierFont">Font used by viewport snap modifier labels.</param>
        /// <param name="toolbarIcons">Runtime textures used by the viewport toolbar.</param>
        /// <param name="sceneCanvasProfileState">Scene-owned canvas profile shared across viewports.</param>
        /// <param name="metrics">Scaled editor UI metrics used by the dockable viewport.</param>
        /// <param name="pickingBackendFactory">Optional host factory that supplies one native picking backend per viewport.</param>
        /// <returns>Workspace state bundle for the new viewport instance.</returns>
        EditorViewportWorkspaceState CreateViewportState(
            FontAsset font,
            FontAsset snapModifierFont,
            EditorViewportToolbarIconSet toolbarIcons,
            EditorSceneCanvasProfileState sceneCanvasProfileState,
            EditorUiMetrics metrics) {
            RenderManager3D render3D = RendererResources.RenderManager3D;
            EditorEntity sceneCameraEntity = CreateSceneCameraEntity();
            CameraComponent sceneCamera = CreateSceneCamera(sceneCameraEntity);
            ViewportComponent sceneViewportComponent = new ViewportComponent {
                BindingMode = ViewportComponent.ExplicitCameraBindingMode,
                BoundCameraComponent = sceneCamera
            };
            sceneCameraEntity.AddComponent(sceneViewportComponent);
            EditorViewportDirect2DScenePresenterComponent direct2DScenePresenterComponent = new EditorViewportDirect2DScenePresenterComponent(sceneCamera, sceneViewportComponent, RendererResources);
            sceneCameraEntity.AddComponent(direct2DScenePresenterComponent);
            EditorWorldSpace2DPreviewSyncComponent worldSpace2DPreviewSyncComponent = new EditorWorldSpace2DPreviewSyncComponent(BuiltInShaderLibrary, RendererResources);
            sceneCameraEntity.AddComponent(worldSpace2DPreviewSyncComponent);
            EditorViewportBorderGizmoSyncComponent viewportBorderGizmoSyncComponent = new EditorViewportBorderGizmoSyncComponent(BuiltInShaderLibrary, RendererResources);
            sceneCameraEntity.AddComponent(viewportBorderGizmoSyncComponent);
            sceneCameraEntity.AddComponent(new ComponentSceneSelectionEditorSyncComponent(render3D, GeneratedMaterialCache));
            CameraComponent gizmoCamera = CreateGizmoCamera(sceneCameraEntity, sceneCamera);
            EditorViewport viewport = new EditorViewport(OwnerCore, sceneCamera, font, snapModifierFont, toolbarIcons, sceneCanvasProfileState, metrics, BuiltInShaderLibrary, RendererResources);
            viewport.SetInput(RendererResources.Input);
            EditorViewportCameraController cameraController = new EditorViewportCameraController(sceneCamera, RendererResources.Input);
            viewport.CameraController = cameraController;
            sceneCameraEntity.AddComponent(cameraController);
            EditorViewportNavigationController navigationController = new EditorViewportNavigationController(cameraController);
            viewport.AttachNavigationCube(navigationController);
            viewport.FocusSelectionRequested = HandleFocusSelectionRequested;
            RuntimeMaterial transformGizmoMaterial = BuildTransformGizmoNormalMaterial(render3D);
            RuntimeMaterial transformGizmoHighlightMaterial = BuildTransformGizmoHighlightMaterial(render3D);
            RuntimeMaterial transformGizmoPlaneMaterial = TransformGizmoPlaneMaterialFactory.CreateNormal(render3D, BuiltInShaderLibrary);
            RuntimeMaterial transformGizmoPlaneHighlightMaterial = TransformGizmoPlaneMaterialFactory.CreateHighlight(render3D, BuiltInShaderLibrary);
            EditorEntity translationGizmoRoot = TransformTranslationGizmoFactory.Create(
                render3D,
                sceneCamera,
                transformGizmoMaterial,
                transformGizmoHighlightMaterial,
                transformGizmoPlaneMaterial,
                transformGizmoPlaneHighlightMaterial,
                BuiltInShaderLibrary);
            EditorEntity rotationGizmoRoot = TransformRotationGizmoFactory.Create(render3D, sceneCamera, transformGizmoMaterial, transformGizmoHighlightMaterial, BuiltInShaderLibrary);
            EditorEntity scaleGizmoRoot = TransformScaleGizmoFactory.Create(render3D, sceneCamera, transformGizmoMaterial, transformGizmoHighlightMaterial);
            foreach (Component gizmoComponent in translationGizmoRoot.Components) {
                if (gizmoComponent is TransformTranslationGizmoFollowComponent translationFollow) {
                    translationFollow.SetInput(RendererResources.Input);
                }
            }
            foreach (Component gizmoComponent in rotationGizmoRoot.Components) {
                if (gizmoComponent is TransformRotationGizmoFollowComponent rotationFollow) {
                    rotationFollow.SetInput(RendererResources.Input);
                }
            }
            EditorViewportGizmoDrawableCollector gizmoDrawableCollector = new EditorViewportGizmoDrawableCollector(
                viewport.GetOwnedSceneGizmoEntities,
                translationGizmoRoot,
                rotationGizmoRoot,
                scaleGizmoRoot);
            sceneCameraEntity.AddComponent(new EditorViewportGizmoRenderQueueComponent(sceneCamera, gizmoCamera, gizmoDrawableCollector, RendererResources.ObjectManager));
            EditorEntity pickerCameraEntity = CreatePickerCameraEntity(sceneCameraEntity);
            CameraComponent pickerCamera = CreatePickerCamera();
            pickerCameraEntity.AddComponent(pickerCamera);
            RenderTarget pickerRenderTarget = null;
            if (PickingBackendFactory != null && PickingBackendFactory.IsSupported) {
                IEditorPickingBackend pickingBackend = PickingBackendFactory.Create(pickerCamera);
                sceneCameraEntity.AddComponent(new EditorViewportPicker(sceneCamera, gizmoCamera, gizmoDrawableCollector, pickerCameraEntity, pickerCamera, pickingBackend, RendererResources));
            } else {
                pickerCamera.RenderTarget = null;
            }

            EditorViewportWorkspaceState state = new EditorViewportWorkspaceState(
                viewport,
                sceneCameraEntity,
                sceneCamera,
                sceneViewportComponent,
                direct2DScenePresenterComponent,
                worldSpace2DPreviewSyncComponent,
                viewportBorderGizmoSyncComponent,
                gizmoCamera,
                pickerCameraEntity,
                pickerCamera,
                pickerRenderTarget,
                cameraController,
                navigationController,
                translationGizmoRoot,
                rotationGizmoRoot,
                scaleGizmoRoot);
            WireViewportCallbacks(state);
            viewport.InitializeHierarchy();
            translationGizmoRoot.InitializeHierarchy();
            rotationGizmoRoot.InitializeHierarchy();
            scaleGizmoRoot.InitializeHierarchy();
            sceneCameraEntity.InitializeHierarchy();
            return state;
        }

        /// <summary>
        /// Wires viewport callbacks that depend on the fully constructed runtime viewport state.
        /// </summary>
        /// <param name="state">Viewport runtime state that should receive callbacks.</param>
        void WireViewportCallbacks(EditorViewportWorkspaceState state) {
            if (state == null) {
                throw new ArgumentNullException(nameof(state));
            }

            state.Viewport.FocusSelectionRequested = HandleFocusSelectionRequested;
            state.NavigationController.CameraStateChanged = () => SynchronizeCameraStack(state);
        }

        /// <summary>
        /// Frames the current editor selection inside this viewport's scene camera.
        /// </summary>
        void HandleFocusSelectionRequested() {
            Entity selectedEntity = EditorSessionInteractionServices.From(State.Viewport).Selection.SelectedEntity;
            if (selectedEntity == null) {
                return;
            }

            State.NavigationController.CancelTransition();
            SelectionFramingService.FocusSelection(State.SceneCamera, State.CameraController, selectedEntity);
            SynchronizeCameraStack(State);
        }

        /// <summary>
        /// Copies the active scene camera projection and pose into the gizmo and picker cameras for one viewport stack.
        /// </summary>
        /// <param name="state">Viewport camera stack receiving synchronized state.</param>
        void SynchronizeCameraStack(EditorViewportWorkspaceState state) {
            if (state == null) {
                throw new ArgumentNullException(nameof(state));
            }

            state.GizmoCamera.Viewport = state.SceneCamera.Viewport;
            EditorViewportCameraProjectionSynchronizer.Synchronize(state.SceneCamera, state.GizmoCamera);
            state.PickerCameraEntity.Position = state.SceneCameraEntity.Position;
            state.PickerCameraEntity.Orientation = state.SceneCameraEntity.Orientation;
            EditorViewportCameraProjectionSynchronizer.Synchronize(state.SceneCamera, state.PickerCamera);
        }

        /// <summary>
        /// Reads the optional editor projection contract from one workspace camera.
        /// </summary>
        /// <param name="camera">Editor viewport camera.</param>
        /// <returns>Optional projection settings exposed by the editor camera.</returns>
        static ICameraProjectionSettings GetProjectionSettings(CameraComponent camera) {
            if (camera is not ICameraProjectionSettings projectionSettings) {
                throw new InvalidOperationException("Workspace viewport cameras must support editor projection settings.");
            }

            return projectionSettings;
        }

        /// <summary>
        /// Captures a perspective-equivalent orthographic span or preserves the active orthographic span.
        /// </summary>
        /// <param name="camera">Viewport camera whose field of view and projection state are captured.</param>
        /// <param name="cameraPosition">Current camera world position.</param>
        /// <param name="orbitPivot">Current viewport orbit pivot.</param>
        /// <returns>Vertical world-space span to use in orthographic mode.</returns>
        static float GetCapturedOrthographicSpan(CameraComponent camera, float3 cameraPosition, float3 orbitPivot) {
            ICameraProjectionSettings settings = GetProjectionSettings(camera);
            if (settings.ProjectionMode == CameraProjectionMode.Orthographic) {
                return settings.OrthographicVerticalSpan;
            }

            return DeriveOrthographicSpan(camera, GetDistance(cameraPosition, orbitPivot));
        }

        /// <summary>
        /// Derives the orthographic vertical span that has the same scale as a perspective camera at one distance.
        /// </summary>
        /// <param name="camera">Camera whose field of view defines the scale.</param>
        /// <param name="distance">Distance between camera and orbit pivot.</param>
        /// <returns>Finite positive vertical world-space span.</returns>
        static float DeriveOrthographicSpan(CameraComponent camera, double distance) {
            float fieldOfView = camera.FieldOfView;
            if (!float.IsFinite(fieldOfView) || fieldOfView <= 0f || !double.IsFinite(distance) || distance <= 0.0) {
                throw new ArgumentOutOfRangeException(nameof(distance), "Camera field of view and pivot distance must be finite and positive.");
            }

            double span = 2.0 * distance * Math.Tan(CameraProjectionUtils.ClampFieldOfView(fieldOfView) * 0.5);
            if (!double.IsFinite(span) || span < CameraProjectionUtils.MinimumOrthographicVerticalSpan || span > float.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(distance), "The camera pose cannot be represented by an orthographic span.");
            }

            return (float)span;
        }

        /// <summary>
        /// Validates projection and pivot fields, allowing zero span only as the marker for a missing legacy value.
        /// </summary>
        /// <param name="document">Workspace state document to validate.</param>
        static void ValidateNavigationState(ViewportWorkspacePanelStateDocument document) {
            if (document.ProjectionMode != CameraProjectionMode.Perspective && document.ProjectionMode != CameraProjectionMode.Orthographic) {
                throw new ArgumentOutOfRangeException(nameof(document.ProjectionMode), document.ProjectionMode, "Workspace camera projection mode is not supported.");
            }
            if (!float.IsFinite(document.OrthographicVerticalSpan) || document.OrthographicVerticalSpan < 0f ||
                (document.OrthographicVerticalSpan > 0f && document.OrthographicVerticalSpan < CameraProjectionUtils.MinimumOrthographicVerticalSpan)) {
                throw new ArgumentOutOfRangeException(nameof(document.OrthographicVerticalSpan), "Workspace orthographic span must be finite and positive when present.");
            }
            if (!float.IsFinite(document.OrbitPivotX) || !float.IsFinite(document.OrbitPivotY) || !float.IsFinite(document.OrbitPivotZ)) {
                throw new ArgumentOutOfRangeException(nameof(document.OrbitPivotX), "Workspace orbit pivot must contain only finite values.");
            }
        }

        /// <summary>
        /// Rejects camera positions and quaternions that cannot define a stable workspace camera pose.
        /// </summary>
        /// <param name="position">Restored camera world position.</param>
        /// <param name="orientation">Restored camera orientation.</param>
        static void ValidateCameraPose(float3 position, float4 orientation) {
            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z)) {
                throw new ArgumentOutOfRangeException(nameof(position), "Workspace camera position must contain only finite values.");
            }
            if (!float.IsFinite(orientation.X) || !float.IsFinite(orientation.Y) || !float.IsFinite(orientation.Z) || !float.IsFinite(orientation.W)) {
                throw new ArgumentOutOfRangeException(nameof(orientation), "Workspace camera orientation must contain only finite values.");
            }

            double lengthSquared =
                (orientation.X * orientation.X) + (orientation.Y * orientation.Y) +
                (orientation.Z * orientation.Z) + (orientation.W * orientation.W);
            if (!double.IsFinite(lengthSquared) || lengthSquared <= 0.000001) {
                throw new ArgumentOutOfRangeException(nameof(orientation), "Workspace camera orientation must have non-zero magnitude.");
            }
        }

        /// <summary>
        /// Computes the Euclidean camera-to-pivot distance without reducing precision to a single-precision vector length.
        /// </summary>
        /// <param name="cameraPosition">Camera world position.</param>
        /// <param name="orbitPivot">World-space orbit pivot.</param>
        /// <returns>Distance in world units.</returns>
        static double GetDistance(float3 cameraPosition, float3 orbitPivot) {
            double deltaX = cameraPosition.X - orbitPivot.X;
            double deltaY = cameraPosition.Y - orbitPivot.Y;
            double deltaZ = cameraPosition.Z - orbitPivot.Z;
            return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ));
        }

        /// <summary>
        /// Creates the root camera entity for one viewport stack.
        /// </summary>
        /// <returns>Initialized scene camera entity.</returns>
        EditorEntity CreateSceneCameraEntity() {
            EditorEntity sceneCameraEntity = new EditorEntity(OwnerCore, EditorEntity.RequireInteractionServices(OwnerCore));
            sceneCameraEntity.InternalEntity = true;
            sceneCameraEntity.Position = new float3(0f, 3f, -8f);
            ApplyDefaultSceneCameraOrientation(sceneCameraEntity);
            return sceneCameraEntity;
        }

        /// <summary>
        /// Creates the scene camera for one viewport stack.
        /// </summary>
        /// <param name="sceneCameraEntity">Entity that owns the camera.</param>
        /// <returns>Created scene camera.</returns>
        CameraComponent CreateSceneCamera(EditorEntity sceneCameraEntity) {
            CameraComponent sceneCamera = new EditorViewportCameraComponent();
            sceneCamera.LayerMask = EditorLayerMasks.SceneObjects | EditorLayerMasks.SceneGrid | EditorLayerMasks.SceneCameraVisuals | EditorLayerMasks.SceneCanvasPlane;
            sceneCamera.CameraDrawOrder = SceneCameraDrawOrder;
            sceneCamera.FarPlaneDistance = DefaultSceneCameraFarPlaneDistance;
            sceneCamera.ClearSettings = new CameraClearSettings(true, new float4(0.39215687f, 0.58431375f, 0.92941177f, 1f), true, 1.0f, false, 0);
            sceneCameraEntity.AddComponent(sceneCamera);
            TransformTranslationGizmoDragComponent translationDrag = new TransformTranslationGizmoDragComponent(sceneCamera);
            translationDrag.SetInput(RendererResources.Input);
            sceneCameraEntity.AddComponent(translationDrag);
            TransformRotationGizmoDragComponent rotationDrag = new TransformRotationGizmoDragComponent(sceneCamera);
            rotationDrag.SetInput(RendererResources.Input);
            sceneCameraEntity.AddComponent(rotationDrag);
            TransformScaleGizmoDragComponent scaleDrag = new TransformScaleGizmoDragComponent(sceneCamera);
            scaleDrag.SetInput(RendererResources.Input);
            sceneCameraEntity.AddComponent(scaleDrag);
            return sceneCamera;
        }

        /// <summary>
        /// Creates the gizmo overlay camera for one viewport stack.
        /// </summary>
        /// <param name="sceneCameraEntity">Entity that owns the camera.</param>
        /// <param name="sceneCamera">Primary scene camera whose viewport rectangle is mirrored.</param>
        /// <returns>Created gizmo overlay camera.</returns>
        CameraComponent CreateGizmoCamera(EditorEntity sceneCameraEntity, CameraComponent sceneCamera) {
            CameraComponent gizmoCamera = new EditorViewportCameraComponent();
            gizmoCamera.LayerMask = EditorLayerMasks.SceneGizmo;
            gizmoCamera.CameraDrawOrder = GizmoCameraDrawOrder;
            gizmoCamera.ClearSettings = new CameraClearSettings(false, new float4(0f, 0f, 0f, 0f), true, 1.0f, false, 0);
            gizmoCamera.Viewport = sceneCamera.Viewport;
            SynchronizeGizmoCameraProjection(sceneCamera, gizmoCamera);
            sceneCameraEntity.AddComponent(gizmoCamera);
            return gizmoCamera;
        }

        /// <summary>
        /// Mirrors the scene camera clip-plane settings onto the gizmo overlay camera so editor gizmos remain visible anywhere the scene camera can frame.
        /// </summary>
        /// <param name="sceneCamera">Primary scene camera that defines the authored framing range.</param>
        /// <param name="gizmoCamera">Overlay camera that renders transform gizmos on top of the scene.</param>
        void SynchronizeGizmoCameraProjection(CameraComponent sceneCamera, CameraComponent gizmoCamera) {
            if (sceneCamera == null) {
                throw new ArgumentNullException(nameof(sceneCamera));
            }
            if (gizmoCamera == null) {
                throw new ArgumentNullException(nameof(gizmoCamera));
            }

            EditorViewportCameraProjectionSynchronizer.Synchronize(sceneCamera, gizmoCamera);
        }

        /// <summary>
        /// Creates the hidden picker-camera entity for one viewport stack.
        /// </summary>
        /// <param name="sceneCameraEntity">Scene camera entity whose transform seeds the picker camera.</param>
        /// <returns>Created picker-camera entity.</returns>
        EditorEntity CreatePickerCameraEntity(EditorEntity sceneCameraEntity) {
            EditorEntity pickerCameraEntity = new EditorEntity(OwnerCore, EditorEntity.RequireInteractionServices(OwnerCore));
            pickerCameraEntity.InternalEntity = true;
            pickerCameraEntity.Enabled = false;
            pickerCameraEntity.Position = sceneCameraEntity.Position;
            pickerCameraEntity.Orientation = sceneCameraEntity.Orientation;
            pickerCameraEntity.LayerMask = EditorLayerMasks.SceneObjects;
            return pickerCameraEntity;
        }

        /// <summary>
        /// Creates the hidden picker camera for one viewport stack.
        /// </summary>
        /// <returns>Created picker camera.</returns>
        CameraComponent CreatePickerCamera() {
            CameraComponent pickerCamera = new EditorViewportCameraComponent();
            pickerCamera.LayerMask = EditorLayerMasks.SceneObjects | EditorLayerMasks.SceneCameraVisuals;
            pickerCamera.Viewport = new float4(0f, 0f, DefaultPickerRenderTargetWidth, DefaultPickerRenderTargetHeight);
            pickerCamera.ClearSettings = new CameraClearSettings(true, new float4(0f, 0f, 0f, 0f), true, 1.0f, false, 0);
            return pickerCamera;
        }

        /// <summary>
        /// Applies the default editor perspective orientation to one scene camera entity.
        /// </summary>
        /// <param name="sceneCameraEntity">Scene camera entity that should face the origin.</param>
        void ApplyDefaultSceneCameraOrientation(EditorEntity sceneCameraEntity) {
            float3 toOrigin = float3.Normalize(new float3(-sceneCameraEntity.Position.X, -sceneCameraEntity.Position.Y, -sceneCameraEntity.Position.Z));
            double yaw = Math.Atan2(toOrigin.X, -toOrigin.Z);
            double pitch = Math.Asin(toOrigin.Y);
            float4 orientation;
            float4.CreateFromYawPitchRoll((float)yaw, (float)pitch, 0f, out orientation);
            sceneCameraEntity.Orientation = orientation;
        }

        /// <summary>
        /// Resolves one serialized state payload into a typed viewport state document.
        /// </summary>
        /// <param name="state">Serialized state payload.</param>
        /// <returns>Typed viewport state document.</returns>
        ViewportWorkspacePanelStateDocument ResolveStateDocument(object state) {
            if (state is ViewportWorkspacePanelStateDocument document) {
                return document;
            }
            if (state is JsonElement jsonElement) {
                ViewportWorkspacePanelStateDocument deserialized = jsonElement.Deserialize<ViewportWorkspacePanelStateDocument>(ViewportStateJsonSerializerOptions);
                if (deserialized == null) {
                    throw new InvalidOperationException("Viewport workspace state could not be deserialized.");
                }

                return deserialized;
            }

            throw new InvalidOperationException("Viewport workspace state payload has an unsupported type.");
        }

        /// <summary>
        /// Returns whether the viewport grid layer is currently enabled on the scene camera.
        /// </summary>
        /// <returns>True when the viewport grid is visible.</returns>
        bool IsGridVisible() {
            return (State.SceneCamera.LayerMask & EditorLayerMasks.SceneGrid) != 0;
        }

        /// <summary>
        /// Applies one viewport grid visibility state to the scene camera.
        /// </summary>
        /// <param name="isVisible">True to render the grid layer; false to hide it.</param>
        void SetGridVisible(bool isVisible) {
            ushort layerMask = State.SceneCamera.LayerMask;
            if (isVisible) {
                State.SceneCamera.LayerMask = (ushort)(layerMask | EditorLayerMasks.SceneGrid);
                return;
            }

            State.SceneCamera.LayerMask = (ushort)(layerMask & ~EditorLayerMasks.SceneGrid);
        }

        /// <summary>
        /// Restores one persisted snap value when the payload supplied a positive value.
        /// </summary>
        /// <param name="toolMode">Tool mode whose snap value should be restored.</param>
        /// <param name="snapSlot">Snap slot to restore.</param>
        /// <param name="value">Persisted snap value.</param>
        void RestoreSnapValue(EditorViewportToolMode toolMode, TransformGizmoSnapSlot snapSlot, double value) {
            if (value <= 0.0) {
                return;
            }

            EditorSessionInteractionServices.From(State.Viewport).TransformSnap.SetSnapValue(State.SceneCamera, toolMode, snapSlot, value);
        }

        /// <summary>
        /// Builds the default material used by transform gizmo meshes.
        /// </summary>
        /// <param name="render3D">Renderer that will own the runtime material.</param>
        /// <returns>Runtime material instance.</returns>
        RuntimeMaterial BuildTransformGizmoNormalMaterial(RenderManager3D render3D) {
            return BuildBuiltInRuntimeMaterial(render3D, TransformGizmoShaderFileName);
        }

        /// <summary>
        /// Builds the highlighted material used by transform gizmo meshes.
        /// </summary>
        /// <param name="render3D">Renderer that will own the runtime material.</param>
        /// <returns>Runtime material instance.</returns>
        RuntimeMaterial BuildTransformGizmoHighlightMaterial(RenderManager3D render3D) {
            return BuildBuiltInRuntimeMaterial(render3D, TransformGizmoHighlightShaderFileName);
        }

        /// <summary>
        /// Builds a runtime material from one built-in editor shader source file.
        /// </summary>
        /// <param name="render3D">Renderer that will own the runtime material.</param>
        /// <param name="shaderFileName">Built-in editor shader source file name.</param>
        /// <returns>Runtime material instance.</returns>
        RuntimeMaterial BuildBuiltInRuntimeMaterial(RenderManager3D render3D, string shaderFileName) {
            if (render3D == null) {
                throw new ArgumentNullException(nameof(render3D));
            }

            if (string.IsNullOrWhiteSpace(shaderFileName)) {
                throw new ArgumentException("Shader file name must be provided.", nameof(shaderFileName));
            }

            if (BuiltInShaderLibrary == null) {
                throw new InvalidOperationException("A built-in shader library is required for new viewport materials.");
            }
            ShaderAsset shaderAsset = BuiltInShaderLibrary.LoadShaderAsset(render3D, shaderFileName);
            string shaderName = Path.GetFileNameWithoutExtension(shaderFileName);
            if (string.IsNullOrWhiteSpace(shaderName)) {
                throw new InvalidOperationException("Built-in shader name could not be resolved.");
            }

            if (string.IsNullOrWhiteSpace(shaderAsset.Id)) {
                throw new InvalidOperationException("Shader asset id must be provided.");
            }

            ShaderMaterialAsset materialAsset = new ShaderMaterialAsset {
                Id = string.Concat(shaderName, ".material"),
                ShaderAssetId = shaderAsset.Id,
                VertexProgram = string.Concat(shaderName, ".vs"),
                PixelProgram = string.Concat(shaderName, ".ps"),
                Variant = DefaultRuntimeShaderVariant
            };

            return render3D.BuildMaterialFromRaw(materialAsset, shaderAsset);
        }
    }
}
