using helengine.directx11;
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
        /// Default picker render target width used before the viewport lays out.
        /// </summary>
        const int DefaultPickerRenderTargetWidth = 640;
        /// <summary>
        /// Default picker render target height used before the viewport lays out.
        /// </summary>
        const int DefaultPickerRenderTargetHeight = 360;
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
        /// Initializes one workspace controller and its independent viewport runtime stack.
        /// </summary>
        /// <param name="font">Font used by the viewport title bar and toolbar.</param>
        /// <param name="snapModifierFont">Font used by viewport snap modifier labels.</param>
        /// <param name="toolbarIcons">Runtime textures used by the viewport toolbar.</param>
        /// <param name="sceneCanvasProfileState">Scene-owned canvas profile shared across viewports.</param>
        /// <param name="metrics">Scaled editor UI metrics used by the dockable viewport.</param>
        public ViewportWorkspacePanelController(
            FontAsset font,
            FontAsset snapModifierFont,
            EditorViewportToolbarIconSet toolbarIcons,
            EditorSceneCanvasProfileState sceneCanvasProfileState,
            EditorUiMetrics metrics) {
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

            State = CreateViewportState(font, snapModifierFont, toolbarIcons, sceneCanvasProfileState, metrics);
        }

        /// <summary>
        /// Initializes one workspace controller around an existing viewport runtime stack.
        /// </summary>
        /// <param name="state">Existing viewport runtime stack owned by the controller.</param>
        public ViewportWorkspacePanelController(EditorViewportWorkspaceState state) {
            State = state ?? throw new ArgumentNullException(nameof(state));
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
                ToolMode = State.Viewport.ToolMode,
                NearPlaneDistance = State.SceneCamera.NearPlaneDistance,
                FarPlaneDistance = State.SceneCamera.FarPlaneDistance,
                CanvasWidth = State.Viewport.CanvasPreviewSettings.CanvasWidth,
                CanvasHeight = State.Viewport.CanvasPreviewSettings.CanvasHeight,
                PixelsPerWorldUnit = State.Viewport.CanvasPreviewSettings.PixelsPerWorldUnit,
                IsGridVisible = IsGridVisible(),
                IsSettingsOverlayOpen = State.Viewport.IsSettingsOverlayVisible,
                TranslateSnap1 = TransformGizmoSnapSettingsService.GetSnapValue(State.SceneCamera, EditorViewportToolMode.Translate, TransformGizmoSnapSlot.Snap1),
                TranslateSnap2 = TransformGizmoSnapSettingsService.GetSnapValue(State.SceneCamera, EditorViewportToolMode.Translate, TransformGizmoSnapSlot.Snap2),
                RotateSnap1 = TransformGizmoSnapSettingsService.GetSnapValue(State.SceneCamera, EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap1),
                RotateSnap2 = TransformGizmoSnapSettingsService.GetSnapValue(State.SceneCamera, EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap2),
                ScaleSnap1 = TransformGizmoSnapSettingsService.GetSnapValue(State.SceneCamera, EditorViewportToolMode.Scale, TransformGizmoSnapSlot.Snap1),
                ScaleSnap2 = TransformGizmoSnapSettingsService.GetSnapValue(State.SceneCamera, EditorViewportToolMode.Scale, TransformGizmoSnapSlot.Snap2)
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
            State.SceneCameraEntity.Position = new float3(
                document.CameraPositionX,
                document.CameraPositionY,
                document.CameraPositionZ);
            State.SceneCameraEntity.Orientation = new float4(
                document.CameraOrientationX,
                document.CameraOrientationY,
                document.CameraOrientationZ,
                document.CameraOrientationW);
            State.Viewport.ToolMode = document.ToolMode;
            State.SceneCamera.NearPlaneDistance = document.NearPlaneDistance;
            State.SceneCamera.FarPlaneDistance = document.FarPlaneDistance;
            State.Viewport.CanvasPreviewSettings.CanvasWidth = document.CanvasWidth;
            State.Viewport.CanvasPreviewSettings.CanvasHeight = document.CanvasHeight;
            State.Viewport.CanvasPreviewSettings.PixelsPerWorldUnit = document.PixelsPerWorldUnit;
            SetGridVisible(document.IsGridVisible);
            State.Viewport.SetSettingsOverlayOpen(document.IsSettingsOverlayOpen);
            TransformGizmoSnapSettingsService.ResetDefaults(State.SceneCamera);
            RestoreSnapValue(EditorViewportToolMode.Translate, TransformGizmoSnapSlot.Snap1, document.TranslateSnap1);
            RestoreSnapValue(EditorViewportToolMode.Translate, TransformGizmoSnapSlot.Snap2, document.TranslateSnap2);
            RestoreSnapValue(EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap1, document.RotateSnap1);
            RestoreSnapValue(EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap2, document.RotateSnap2);
            RestoreSnapValue(EditorViewportToolMode.Scale, TransformGizmoSnapSlot.Snap1, document.ScaleSnap1);
            RestoreSnapValue(EditorViewportToolMode.Scale, TransformGizmoSnapSlot.Snap2, document.ScaleSnap2);
        }

        /// <summary>
        /// Disposes the viewport panel and its independent runtime camera stack.
        /// </summary>
        public void Dispose() {
            State.Viewport.ClearInputBlockers();
            EditorViewportToolService.ClearToolMode(State.SceneCamera);
            TransformGizmoSnapSettingsService.ClearState(State.SceneCamera);
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
        /// <returns>Workspace state bundle for the new viewport instance.</returns>
        EditorViewportWorkspaceState CreateViewportState(
            FontAsset font,
            FontAsset snapModifierFont,
            EditorViewportToolbarIconSet toolbarIcons,
            EditorSceneCanvasProfileState sceneCanvasProfileState,
            EditorUiMetrics metrics) {
            RenderManager3D render3D = Core.Instance.RenderManager3D;
            EditorEntity sceneCameraEntity = CreateSceneCameraEntity();
            CameraComponent sceneCamera = CreateSceneCamera(sceneCameraEntity);
            ViewportComponent sceneViewportComponent = new ViewportComponent {
                BindingMode = ViewportComponent.ExplicitCameraBindingMode,
                BoundCameraComponent = sceneCamera
            };
            sceneCameraEntity.AddComponent(sceneViewportComponent);
            EditorViewportDirect2DScenePresenterComponent direct2DScenePresenterComponent = new EditorViewportDirect2DScenePresenterComponent(sceneCamera, sceneViewportComponent);
            sceneCameraEntity.AddComponent(direct2DScenePresenterComponent);
            CameraComponent gizmoCamera = CreateGizmoCamera(sceneCameraEntity, sceneCamera);
            EditorViewport viewport = new EditorViewport(sceneCamera, font, snapModifierFont, toolbarIcons, sceneCanvasProfileState, metrics);
            EditorViewportCameraController cameraController = new EditorViewportCameraController(sceneCamera);
            sceneCameraEntity.AddComponent(cameraController);
            RuntimeMaterial transformGizmoMaterial = BuildTransformGizmoNormalMaterial(render3D);
            RuntimeMaterial transformGizmoHighlightMaterial = BuildTransformGizmoHighlightMaterial(render3D);
            RuntimeMaterial transformGizmoPlaneMaterial = TransformGizmoPlaneMaterialFactory.CreateNormal(render3D);
            RuntimeMaterial transformGizmoPlaneHighlightMaterial = TransformGizmoPlaneMaterialFactory.CreateHighlight(render3D);
            EditorEntity translationGizmoRoot = TransformTranslationGizmoFactory.Create(
                render3D,
                sceneCamera,
                transformGizmoMaterial,
                transformGizmoHighlightMaterial,
                transformGizmoPlaneMaterial,
                transformGizmoPlaneHighlightMaterial);
            EditorEntity rotationGizmoRoot = TransformRotationGizmoFactory.Create(render3D, sceneCamera, transformGizmoMaterial, transformGizmoHighlightMaterial);
            EditorEntity scaleGizmoRoot = TransformScaleGizmoFactory.Create(render3D, sceneCamera, transformGizmoMaterial, transformGizmoHighlightMaterial);
            EditorViewportGizmoDrawableCollector gizmoDrawableCollector = new EditorViewportGizmoDrawableCollector(
                viewport.GetOwnedSceneGizmoEntities,
                translationGizmoRoot,
                rotationGizmoRoot,
                scaleGizmoRoot);
            sceneCameraEntity.AddComponent(new EditorViewportGizmoRenderQueueComponent(gizmoCamera, gizmoDrawableCollector));
            EditorEntity pickerCameraEntity = CreatePickerCameraEntity(sceneCameraEntity);
            CameraComponent pickerCamera = CreatePickerCamera();
            pickerCameraEntity.AddComponent(pickerCamera);
            RenderTarget pickerRenderTarget = null;
            if (render3D is DirectX11Renderer3D pickerRenderer) {
                pickerRenderTarget = render3D.CreateRenderTarget(DefaultPickerRenderTargetWidth, DefaultPickerRenderTargetHeight);
                pickerCamera.RenderTarget = pickerRenderTarget;
                sceneCameraEntity.AddComponent(new EditorViewportPicker(sceneCamera, gizmoCamera, gizmoDrawableCollector, pickerCameraEntity, pickerCamera, pickerRenderer));
            } else {
                pickerCamera.RenderTarget = null;
            }

            return new EditorViewportWorkspaceState(
                viewport,
                sceneCameraEntity,
                sceneCamera,
                sceneViewportComponent,
                direct2DScenePresenterComponent,
                gizmoCamera,
                pickerCameraEntity,
                pickerCamera,
                pickerRenderTarget,
                cameraController,
                translationGizmoRoot,
                rotationGizmoRoot,
                scaleGizmoRoot);
        }

        /// <summary>
        /// Creates the root camera entity for one viewport stack.
        /// </summary>
        /// <returns>Initialized scene camera entity.</returns>
        EditorEntity CreateSceneCameraEntity() {
            EditorEntity sceneCameraEntity = new EditorEntity();
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
            CameraComponent sceneCamera = new CameraComponent();
            sceneCamera.LayerMask = EditorLayerMasks.SceneObjects | EditorLayerMasks.SceneGrid | EditorLayerMasks.SceneCameraVisuals | EditorLayerMasks.SceneCanvasPlane;
            sceneCamera.CameraDrawOrder = SceneCameraDrawOrder;
            sceneCamera.ClearSettings = new CameraClearSettings(true, new float4(0.39215687f, 0.58431375f, 0.92941177f, 1f), true, 1.0f, false, 0);
            sceneCameraEntity.AddComponent(sceneCamera);
            sceneCameraEntity.AddComponent(new TransformTranslationGizmoDragComponent(sceneCamera));
            sceneCameraEntity.AddComponent(new TransformRotationGizmoDragComponent(sceneCamera));
            sceneCameraEntity.AddComponent(new TransformScaleGizmoDragComponent(sceneCamera));
            return sceneCamera;
        }

        /// <summary>
        /// Creates the gizmo overlay camera for one viewport stack.
        /// </summary>
        /// <param name="sceneCameraEntity">Entity that owns the camera.</param>
        /// <param name="sceneCamera">Primary scene camera whose viewport rectangle is mirrored.</param>
        /// <returns>Created gizmo overlay camera.</returns>
        CameraComponent CreateGizmoCamera(EditorEntity sceneCameraEntity, CameraComponent sceneCamera) {
            CameraComponent gizmoCamera = new CameraComponent();
            gizmoCamera.LayerMask = EditorLayerMasks.SceneGizmo;
            gizmoCamera.CameraDrawOrder = GizmoCameraDrawOrder;
            gizmoCamera.ClearSettings = new CameraClearSettings(false, new float4(0f, 0f, 0f, 0f), true, 1.0f, false, 0);
            gizmoCamera.Viewport = sceneCamera.Viewport;
            sceneCameraEntity.AddComponent(gizmoCamera);
            return gizmoCamera;
        }

        /// <summary>
        /// Creates the hidden picker-camera entity for one viewport stack.
        /// </summary>
        /// <param name="sceneCameraEntity">Scene camera entity whose transform seeds the picker camera.</param>
        /// <returns>Created picker-camera entity.</returns>
        EditorEntity CreatePickerCameraEntity(EditorEntity sceneCameraEntity) {
            EditorEntity pickerCameraEntity = new EditorEntity();
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
            CameraComponent pickerCamera = new CameraComponent();
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

            TransformGizmoSnapSettingsService.SetSnapValue(State.SceneCamera, toolMode, snapSlot, value);
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

            ShaderAsset shaderAsset = EditorBuiltInShaderAssetLibrary.LoadShaderAsset(render3D, shaderFileName);
            string shaderName = Path.GetFileNameWithoutExtension(shaderFileName);
            if (string.IsNullOrWhiteSpace(shaderName)) {
                throw new InvalidOperationException("Built-in shader name could not be resolved.");
            }

            if (string.IsNullOrWhiteSpace(shaderAsset.Id)) {
                throw new InvalidOperationException("Shader asset id must be provided.");
            }

            MaterialAsset materialAsset = new MaterialAsset {
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
