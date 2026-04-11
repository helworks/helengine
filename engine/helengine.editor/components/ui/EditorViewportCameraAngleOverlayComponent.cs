namespace helengine.editor {
    /// <summary>
    /// Draws live camera-angle diagnostics over an editor viewport and labels the currently visible transform-gizmo axes.
    /// </summary>
    public class EditorViewportCameraAngleOverlayComponent : UpdateComponent {
        /// <summary>
        /// Conversion factor from radians to degrees.
        /// </summary>
        const double RadiansToDegrees = 180.0 / Math.PI;
        /// <summary>
        /// Quarter-turn angle in radians used for 90-degree snap readouts.
        /// </summary>
        const double QuarterTurnRadians = Math.PI * 0.5;
        /// <summary>
        /// Perspective vertical field of view used by scene camera rendering.
        /// </summary>
        const double PerspectiveVerticalFieldOfViewRadians = Math.PI / 4.0;
        /// <summary>
        /// Horizontal pixel padding applied to overlay text.
        /// </summary>
        const int OverlayPaddingX = 8;
        /// <summary>
        /// Vertical pixel padding applied to overlay text.
        /// </summary>
        const int OverlayPaddingY = 6;
        /// <summary>
        /// Horizontal offset from the viewport content edge.
        /// </summary>
        const int OverlayMarginX = 8;
        /// <summary>
        /// Vertical offset from the viewport content edge.
        /// </summary>
        const int OverlayMarginY = 8;
        /// <summary>
        /// World-space depth bias applied toward the camera so billboard labels stay in front of gizmo tips.
        /// </summary>
        const int AxisLabelDepthBiasPixels = 12;
        /// <summary>
        /// Extra offset applied beyond the cone tip in gizmo-local units so each label sits a fixed distance after its axis tip.
        /// </summary>
        const double AxisLabelAlongAxisOffsetAfterCone = 0.0;
        /// <summary>
        /// Vertical world-space lift applied in camera-up space so the label clears the translation-gizmo tip.
        /// </summary>
        const int AxisLabelVerticalLiftPixels = 12;
        /// <summary>
        /// Additional scale multiplier applied after converting font pixels into world units.
        /// </summary>
        const double AxisLabelPixelScaleMultiplier = 1.2;
        /// <summary>
        /// Render order used by world-space axis-label billboards.
        /// </summary>
        const byte AxisLabelRenderOrder3D = 1;
        /// <summary>
        /// Fraction of viewport height the translation gizmo targets on screen.
        /// </summary>
        const double GizmoTargetViewportHeightFraction = 0.20;
        /// <summary>
        /// Number of translation-axis labels rendered at once.
        /// </summary>
        const int AxisLabelCount = 3;
        /// <summary>
        /// Smallest squared horizontal length treated as a valid camera-to-selection direction.
        /// </summary>
        const double MinimumHorizontalLengthSquared = 0.000000000001;
        /// <summary>
        /// Smallest squared vector length treated as non-zero.
        /// </summary>
        const double MinimumDirectionLengthSquared = 0.000000000001;
        /// <summary>
        /// Smallest camera distance used when converting billboard size from pixels into world units.
        /// </summary>
        const double MinimumCameraDistance = 0.001;
        /// <summary>
        /// Default forward axis used to derive yaw and pitch from camera orientation.
        /// </summary>
        static readonly float3 ForwardAxis = new float3(0f, 0f, -1f);
        /// <summary>
        /// Default up axis used to derive camera basis vectors.
        /// </summary>
        static readonly float3 UpAxis = new float3(0f, 1f, 0f);
        /// <summary>
        /// Local-space axis direction used by translation-gizmo handles before axis-specific orientation is applied.
        /// </summary>
        static readonly float3 AxisHandleLocalPrimaryDirection = new float3(0f, 1f, 0f);
        /// <summary>
        /// Base local orientation used by the X translation handle before yaw-facing rotation is applied.
        /// </summary>
        static readonly float4 XAxisBaseOrientation = CreateAxisOrientation(new float3(0f, 0f, 1f), -Math.PI * 0.5);
        /// <summary>
        /// Base local orientation used by the Y translation handle before yaw-facing rotation is applied.
        /// </summary>
        static readonly float4 YAxisBaseOrientation = float4.Identity;
        /// <summary>
        /// Base local orientation used by the Z translation handle before yaw-facing rotation is applied.
        /// </summary>
        static readonly float4 ZAxisBaseOrientation = CreateAxisOrientation(new float3(1f, 0f, 0f), Math.PI * 0.5);

        /// <summary>
        /// Scene camera whose orientation and position are displayed.
        /// </summary>
        readonly CameraComponent SceneCamera;
        /// <summary>
        /// Font used to render overlay text.
        /// </summary>
        readonly FontAsset Font;
        /// <summary>
        /// Vertical viewport offset below the dock title bar where camera content begins.
        /// </summary>
        readonly int ViewportTopOffset;
        /// <summary>
        /// Render order used for the overlay background.
        /// </summary>
        readonly byte OverlayBackgroundRenderOrder;
        /// <summary>
        /// Render order used for the overlay text.
        /// </summary>
        readonly byte OverlayTextRenderOrder;

        /// <summary>
        /// Overlay root entity positioned in viewport-local coordinates.
        /// </summary>
        EditorEntity OverlayRoot;
        /// <summary>
        /// Background rectangle used to keep text readable over scene content.
        /// </summary>
        RoundedRectComponent OverlayBackground;
        /// <summary>
        /// Host entity for text offset inside the background padding.
        /// </summary>
        EditorEntity TextHost;
        /// <summary>
        /// Text component displaying live camera-angle values.
        /// </summary>
        TextComponent OverlayText;
        /// <summary>
        /// World-space entities used to render transform-gizmo axis billboard labels.
        /// </summary>
        EditorEntity[] AxisLabelEntities;
        /// <summary>
        /// Mesh components used to render transform-gizmo axis billboard labels.
        /// </summary>
        MeshComponent[] AxisLabelMeshes;
        /// <summary>
        /// Cached billboard models keyed by axis-label text such as x+ or z-.
        /// </summary>
        Dictionary<string, RuntimeModel> AxisLabelModels;
        /// <summary>
        /// Tracks whether overlay entities were created.
        /// </summary>
        bool Initialized;

        /// <summary>
        /// Initializes a viewport camera-angle overlay component.
        /// </summary>
        /// <param name="sceneCamera">Camera to inspect for debug angle output.</param>
        /// <param name="font">Font used for overlay text.</param>
        /// <param name="viewportTopOffset">Offset in pixels from title bar top to viewport content top.</param>
        public EditorViewportCameraAngleOverlayComponent(CameraComponent sceneCamera, FontAsset font, int viewportTopOffset) {
            SceneCamera = sceneCamera ?? throw new ArgumentNullException(nameof(sceneCamera));
            Font = font ?? throw new ArgumentNullException(nameof(font));
            if (viewportTopOffset < 0) {
                throw new ArgumentOutOfRangeException(nameof(viewportTopOffset), "Viewport top offset must be zero or greater.");
            }

            ViewportTopOffset = viewportTopOffset;
            OverlayBackgroundRenderOrder = RenderOrder2D.OverlayBackground;
            OverlayTextRenderOrder = RenderOrder2D.OverlayForeground;
        }

        /// <summary>
        /// Creates overlay entities when this component is attached.
        /// </summary>
        /// <param name="entity">Owning viewport entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            if (Initialized) {
                return;
            }

            if (entity is not EditorEntity editorEntity) {
                throw new InvalidOperationException("Viewport camera angle overlay must be attached to an EditorEntity.");
            }

            OverlayRoot = new EditorEntity {
                InternalEntity = true,
                LayerMask = editorEntity.LayerMask,
                Position = new float3(OverlayMarginX, DockableEntity.TitleBarHeight + ViewportTopOffset + OverlayMarginY, 0.35f)
            };
            editorEntity.AddChild(OverlayRoot);

            OverlayBackground = new RoundedRectComponent {
                Radius = 5f,
                BorderThickness = 1f,
                FillColor = new byte4(0, 0, 0, 145),
                BorderColor = new byte4(255, 255, 255, 64),
                Size = new int2(1, 1),
                RenderOrder2D = OverlayBackgroundRenderOrder
            };
            OverlayRoot.AddComponent(OverlayBackground);

            TextHost = new EditorEntity {
                InternalEntity = true,
                LayerMask = editorEntity.LayerMask,
                Position = new float3(OverlayPaddingX, OverlayPaddingY, 0.1f)
            };
            OverlayRoot.AddChild(TextHost);

            OverlayText = new TextComponent {
                Font = Font,
                Color = new byte4(235, 235, 235, 255),
                RenderOrder2D = OverlayTextRenderOrder,
                Size = new int2(1, 1),
                Text = string.Empty
            };
            TextHost.AddComponent(OverlayText);

            CreateAxisLabelEntities();
            Initialized = true;
        }

        /// <summary>
        /// Refreshes debug text, overlay layout, and translation-axis labels each frame.
        /// </summary>
        public override void Update() {
            if (!Initialized) {
                return;
            }

            Entity cameraEntity = SceneCamera.Parent;
            if (cameraEntity == null) {
                throw new InvalidOperationException("Scene camera must belong to an entity to compute debug angles.");
            }

            string text = BuildOverlayText(cameraEntity);
            OverlayText.Text = text;
            LayoutOverlay(text);
            UpdateAxisLabels(cameraEntity);
        }

        /// <summary>
        /// Builds the live overlay text for camera and snap-angle diagnostics.
        /// </summary>
        /// <param name="cameraEntity">Entity that owns the scene camera.</param>
        /// <returns>Multiline debug text rendered in the viewport.</returns>
        string BuildOverlayText(Entity cameraEntity) {
            if (cameraEntity == null) {
                throw new ArgumentNullException(nameof(cameraEntity));
            }

            float3 forward = float4.RotateVector(ForwardAxis, cameraEntity.Orientation);
            double cameraYawRadians = NormalizeAngleRadians(Math.Atan2(forward.X, -forward.Z));
            double clampedForwardY = Math.Clamp((double)forward.Y, -1.0, 1.0);
            double cameraPitchRadians = Math.Asin(clampedForwardY);
            double cameraYawDegrees = cameraYawRadians * RadiansToDegrees;
            double cameraPitchDegrees = cameraPitchRadians * RadiansToDegrees;

            Entity selectedEntity = EditorSelectionService.SelectedEntity;
            if (selectedEntity == null || !selectedEntity.Enabled) {
                return string.Concat(
                    "Camera->Pivot Yaw: n/a",
                    "\nCamera Yaw: ", FormatAngleDegrees(cameraYawDegrees), " deg",
                    "\nCamera Pitch: ", FormatAngleDegrees(cameraPitchDegrees), " deg");
            }

            bool hasCameraToPivotYaw;
            bool hasPivotToCameraYaw;
            double cameraToPivotYawRadians = ComputeCameraToPivotYawRadians(cameraEntity.Position, selectedEntity.Position, out hasCameraToPivotYaw);
            double pivotToCameraYawRadians = ComputePivotToCameraYawRadians(selectedEntity.Position, cameraEntity.Position, out hasPivotToCameraYaw);
            int snappedQuarterTurns = TransformGizmoYawSnapper.ComputeSnappedQuarterTurns(selectedEntity.Position, cameraEntity.Position);
            double snappedYawRadians = NormalizeAngleRadians((snappedQuarterTurns * QuarterTurnRadians) - QuarterTurnRadians);

            if (!hasCameraToPivotYaw || !hasPivotToCameraYaw) {
                return string.Concat(
                    "Camera->Pivot Yaw: n/a",
                    "\nPivot->Camera Yaw (snap basis): n/a",
                    "\nSnap Quarter: ", snappedQuarterTurns.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "\nSnap Yaw: ", FormatAngleDegrees(snappedYawRadians * RadiansToDegrees), " deg",
                    "\nCamera Yaw: ", FormatAngleDegrees(cameraYawDegrees), " deg",
                    "\nCamera Pitch: ", FormatAngleDegrees(cameraPitchDegrees), " deg");
            }

            return string.Concat(
                "Camera->Pivot Yaw: ", FormatAngleDegrees(cameraToPivotYawRadians * RadiansToDegrees), " deg",
                "\nPivot->Camera Yaw (snap basis): ", FormatAngleDegrees(pivotToCameraYawRadians * RadiansToDegrees), " deg",
                "\nSnap Quarter: ", snappedQuarterTurns.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "\nSnap Yaw: ", FormatAngleDegrees(snappedYawRadians * RadiansToDegrees), " deg",
                "\nCamera Yaw: ", FormatAngleDegrees(cameraYawDegrees), " deg",
                "\nCamera Pitch: ", FormatAngleDegrees(cameraPitchDegrees), " deg");
        }

        /// <summary>
        /// Computes the horizontal yaw angle from the camera toward the selected pivot.
        /// </summary>
        /// <param name="cameraPosition">Camera world position.</param>
        /// <param name="selectionPosition">Selected entity world position.</param>
        /// <param name="hasAngle">True when a valid horizontal direction exists; otherwise false.</param>
        /// <returns>Horizontal yaw in radians when available; otherwise zero.</returns>
        double ComputeCameraToPivotYawRadians(float3 cameraPosition, float3 selectionPosition, out bool hasAngle) {
            float3 toPivot = selectionPosition - cameraPosition;
            double horizontalLengthSquared = (toPivot.X * toPivot.X) + (toPivot.Z * toPivot.Z);
            if (horizontalLengthSquared <= MinimumHorizontalLengthSquared) {
                hasAngle = false;
                return 0.0;
            }

            hasAngle = true;
            double inverseLength = 1.0 / Math.Sqrt(horizontalLengthSquared);
            double directionX = toPivot.X * inverseLength;
            double directionZ = toPivot.Z * inverseLength;
            return NormalizeAngleRadians(Math.Atan2(directionX, -directionZ));
        }

        /// <summary>
        /// Computes the horizontal yaw angle from selection origin toward the camera.
        /// </summary>
        /// <param name="selectionPosition">Selected entity world position.</param>
        /// <param name="cameraPosition">Camera world position.</param>
        /// <param name="hasAngle">True when a valid horizontal direction exists; otherwise false.</param>
        /// <returns>Horizontal yaw in radians when available; otherwise zero.</returns>
        double ComputePivotToCameraYawRadians(float3 selectionPosition, float3 cameraPosition, out bool hasAngle) {
            float3 toCamera = cameraPosition - selectionPosition;
            double horizontalLengthSquared = (toCamera.X * toCamera.X) + (toCamera.Z * toCamera.Z);
            if (horizontalLengthSquared <= MinimumHorizontalLengthSquared) {
                hasAngle = false;
                return 0.0;
            }

            hasAngle = true;
            double inverseLength = 1.0 / Math.Sqrt(horizontalLengthSquared);
            double directionX = toCamera.X * inverseLength;
            double directionZ = toCamera.Z * inverseLength;
            return NormalizeAngleRadians(Math.Atan2(directionX, directionZ));
        }

        /// <summary>
        /// Updates overlay background and text bounds from the current content.
        /// </summary>
        /// <param name="text">Current overlay text.</param>
        void LayoutOverlay(string text) {
            if (text == null) {
                throw new ArgumentNullException(nameof(text));
            }

            string[] lines = text.Split('\n');
            double maxWidth = 0.0;
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++) {
                FontTightMetrics metrics = Font.MeasureTight(lines[lineIndex]);
                if (metrics.Width > maxWidth) {
                    maxWidth = metrics.Width;
                }
            }

            int width = (int)Math.Ceiling(maxWidth) + OverlayPaddingX * 2;
            int height = (int)Math.Ceiling(lines.Length * Font.LineHeight) + OverlayPaddingY * 2;
            OverlayBackground.Size = new int2(width, height);
            OverlayText.Size = new int2(Math.Max(1, width - (OverlayPaddingX * 2)), Math.Max(1, height - (OverlayPaddingY * 2)));
            TextHost.Position = new float3(OverlayPaddingX, OverlayPaddingY, 0.1f);
        }

        /// <summary>
        /// Creates the world-space billboard entities used for translation-axis labels.
        /// </summary>
        void CreateAxisLabelEntities() {
            RenderManager3D render3D = Core.Instance.RenderManager3D;
            if (render3D == null) {
                throw new InvalidOperationException("A 3D renderer must be initialized before creating gizmo axis labels.");
            }

            RuntimeMaterial axisLabelMaterial = TransformGizmoAxisLabelMaterialFactory.Create(render3D, Font);
            AxisLabelModels = new Dictionary<string, RuntimeModel>(StringComparer.Ordinal);
            AxisLabelEntities = new EditorEntity[AxisLabelCount];
            AxisLabelMeshes = new MeshComponent[AxisLabelCount];
            BuildAxisLabelModels(render3D);

            for (int axisIndex = 0; axisIndex < AxisLabelCount; axisIndex++) {
                CreateAxisLabelEntity(axisIndex, axisLabelMaterial);
            }
        }

        /// <summary>
        /// Creates one world-space billboard entity used to render a translation-axis label.
        /// </summary>
        /// <param name="axisIndex">Zero-based translation-axis slot index.</param>
        /// <param name="axisLabelMaterial">Shared material used by all axis-label billboards.</param>
        void CreateAxisLabelEntity(int axisIndex, RuntimeMaterial axisLabelMaterial) {
            if (axisIndex < 0 || axisIndex >= AxisLabelCount) {
                throw new ArgumentOutOfRangeException(nameof(axisIndex), "Axis label slot index must be within the supported translation-axis range.");
            }

            if (axisLabelMaterial == null) {
                throw new ArgumentNullException(nameof(axisLabelMaterial));
            }

            var axisLabelEntity = new EditorEntity {
                Name = string.Concat("Transform Gizmo Axis Label ", axisIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                InternalEntity = true,
                LayerMask = EditorLayerMasks.SceneGizmo,
                Enabled = false
            };
            var axisLabelMesh = new MeshComponent {
                Material = axisLabelMaterial
            };
            axisLabelEntity.AddComponent(axisLabelMesh);
            axisLabelMesh.RenderOrder3D = AxisLabelRenderOrder3D;
            AxisLabelEntities[axisIndex] = axisLabelEntity;
            AxisLabelMeshes[axisIndex] = axisLabelMesh;
        }

        /// <summary>
        /// Updates translation-axis label positions and text from the current gizmo-facing state.
        /// </summary>
        /// <param name="cameraEntity">Entity that owns the scene camera.</param>
        void UpdateAxisLabels(Entity cameraEntity) {
            if (cameraEntity == null) {
                throw new ArgumentNullException(nameof(cameraEntity));
            }

            if (!IsTranslateToolActive()) {
                SetAxisLabelsVisible(false);
                return;
            }

            Entity selectedEntity = EditorSelectionService.SelectedEntity;
            if (selectedEntity == null || !selectedEntity.Enabled) {
                SetAxisLabelsVisible(false);
                return;
            }

            float3 cameraForward = NormalizeDirection(float4.RotateVector(ForwardAxis, cameraEntity.Orientation));
            float3 cameraUp = NormalizeDirection(float4.RotateVector(UpAxis, cameraEntity.Orientation));
            if (cameraForward == float3.Zero || cameraUp == float3.Zero) {
                SetAxisLabelsVisible(false);
                return;
            }

            float4 yawFacingOrientation = TransformGizmoYawSnapper.ComputeSnappedYawFacingOrientation(selectedEntity.Position, cameraEntity.Position);
            double gizmoScale = ResolveAxisLabelScale(selectedEntity.Position, cameraEntity.Position);

            for (int axisIndex = 0; axisIndex < AxisLabelCount; axisIndex++) {
                float3 axisDirection = ResolveAxisDirection(axisIndex, yawFacingOrientation);
                if (!TryResolveAxisLabelData(
                    cameraEntity,
                    selectedEntity.Position,
                    axisDirection,
                    gizmoScale,
                    cameraForward,
                    cameraUp,
                    out string axisLabel,
                    out float3 labelPosition,
                    out float4 labelOrientation,
                    out float labelScale)) {
                    SetAxisLabelsVisible(false);
                    return;
                }

                ApplyAxisLabel(axisIndex, axisLabel, labelPosition, labelOrientation, labelScale);
            }

            SetAxisLabelsVisible(true);
        }

        /// <summary>
        /// Resolves one translation-axis label text and billboard transform data.
        /// </summary>
        /// <param name="cameraEntity">Entity that owns the scene camera.</param>
        /// <param name="selectedPosition">Selected scene position used as the gizmo origin.</param>
        /// <param name="axisDirection">Normalized world-space direction of the translation axis.</param>
        /// <param name="gizmoScale">Current translation-gizmo world scale.</param>
        /// <param name="cameraForward">Normalized camera forward direction.</param>
        /// <param name="cameraUp">Normalized camera up direction.</param>
        /// <param name="axisLabel">Resolved axis label text.</param>
        /// <param name="labelPosition">World-space billboard position.</param>
        /// <param name="labelOrientation">Billboard orientation facing the active camera.</param>
        /// <param name="labelScale">Billboard scale that preserves font readability on screen.</param>
        /// <returns>True when axis label data is valid; otherwise false.</returns>
        bool TryResolveAxisLabelData(
            Entity cameraEntity,
            float3 selectedPosition,
            float3 axisDirection,
            double gizmoScale,
            float3 cameraForward,
            float3 cameraUp,
            out string axisLabel,
            out float3 labelPosition,
            out float4 labelOrientation,
            out float labelScale) {
            if (cameraEntity == null) {
                throw new ArgumentNullException(nameof(cameraEntity));
            }
            if (axisDirection == float3.Zero) {
                axisLabel = string.Empty;
                labelPosition = float3.Zero;
                labelOrientation = float4.Identity;
                labelScale = 0f;
                return false;
            }

            if (gizmoScale <= 0.0) {
                axisLabel = string.Empty;
                labelPosition = float3.Zero;
                labelOrientation = float4.Identity;
                labelScale = 0f;
                return false;
            }

            double axisTipDistance = TransformTranslationGizmoFactory.AxisLength * gizmoScale;
            double axisLabelAlongAxisDistance = AxisLabelAlongAxisOffsetAfterCone * gizmoScale;
            float3 tipWorldPosition = selectedPosition + (axisDirection * (float)axisTipDistance);

            double worldUnitsPerPixel = ComputeWorldUnitsPerPixel(tipWorldPosition, cameraEntity.Position);
            labelPosition =
                tipWorldPosition +
                (axisDirection * (float)axisLabelAlongAxisDistance) +
                (cameraUp * (float)(worldUnitsPerPixel * AxisLabelVerticalLiftPixels)) -
                (cameraForward * (float)(worldUnitsPerPixel * AxisLabelDepthBiasPixels));
            labelOrientation = cameraEntity.Orientation;
            labelScale = (float)(worldUnitsPerPixel * AxisLabelPixelScaleMultiplier);
            axisLabel = BuildAxisLabel(axisDirection);
            return true;
        }

        /// <summary>
        /// Applies the resolved billboard model and transform for one translation-axis label.
        /// </summary>
        /// <param name="axisIndex">Zero-based translation-axis slot index.</param>
        /// <param name="axisLabel">Axis-label text to display.</param>
        /// <param name="labelPosition">World-space billboard position.</param>
        /// <param name="labelOrientation">Billboard orientation facing the camera.</param>
        /// <param name="labelScale">Billboard scale factor.</param>
        void ApplyAxisLabel(int axisIndex, string axisLabel, float3 labelPosition, float4 labelOrientation, float labelScale) {
            if (axisIndex < 0 || axisIndex >= AxisLabelCount) {
                throw new ArgumentOutOfRangeException(nameof(axisIndex), "Axis label slot index must be within the supported translation-axis range.");
            }

            if (axisLabel == null) {
                throw new ArgumentNullException(nameof(axisLabel));
            }

            if (AxisLabelModels == null || !AxisLabelModels.TryGetValue(axisLabel, out RuntimeModel labelModel)) {
                throw new InvalidOperationException($"Axis-label billboard model '{axisLabel}' has not been created.");
            }

            if (AxisLabelMeshes == null || AxisLabelMeshes[axisIndex] == null) {
                throw new InvalidOperationException("Axis-label mesh component has not been created.");
            }

            AxisLabelMeshes[axisIndex].Model = labelModel;
            AxisLabelEntities[axisIndex].Position = labelPosition;
            AxisLabelEntities[axisIndex].Orientation = labelOrientation;
            AxisLabelEntities[axisIndex].Scale = new float3(labelScale, labelScale, labelScale);
        }

        /// <summary>
        /// Converts one screen pixel at the supplied world position into world units.
        /// </summary>
        /// <param name="origin">World-space origin where the billboard will be placed.</param>
        /// <param name="cameraPosition">World-space camera position.</param>
        /// <returns>World units represented by one screen pixel at the supplied depth.</returns>
        double ComputeWorldUnitsPerPixel(float3 origin, float3 cameraPosition) {
            float4 viewport = SceneCamera.Viewport;
            double viewportHeight = viewport.W;
            if (viewportHeight <= 0.0) {
                throw new InvalidOperationException("Scene camera viewport height must be greater than zero.");
            }

            float3 offset = origin - cameraPosition;
            double distance = Math.Sqrt(
                (offset.X * offset.X) +
                (offset.Y * offset.Y) +
                (offset.Z * offset.Z));
            if (distance < MinimumCameraDistance) {
                distance = MinimumCameraDistance;
            }

            double tanHalfFov = Math.Tan(PerspectiveVerticalFieldOfViewRadians * 0.5);
            if (tanHalfFov <= 0.0) {
                throw new InvalidOperationException("Perspective field of view must produce a positive tangent value.");
            }

            return (2.0 * distance * tanHalfFov) / viewportHeight;
        }

        /// <summary>
        /// Builds the cached billboard models used for transform-gizmo axis labels.
        /// </summary>
        /// <param name="render3D">Renderer used to upload billboard geometry.</param>
        void BuildAxisLabelModels(RenderManager3D render3D) {
            if (render3D == null) {
                throw new ArgumentNullException(nameof(render3D));
            }

            BuildAxisLabelModel(render3D, "x+");
            BuildAxisLabelModel(render3D, "x-");
            BuildAxisLabelModel(render3D, "y+");
            BuildAxisLabelModel(render3D, "y-");
            BuildAxisLabelModel(render3D, "z+");
            BuildAxisLabelModel(render3D, "z-");
        }

        /// <summary>
        /// Builds and caches one billboard model for the supplied axis-label text.
        /// </summary>
        /// <param name="render3D">Renderer used to upload billboard geometry.</param>
        /// <param name="axisLabel">Axis-label text to cache.</param>
        void BuildAxisLabelModel(RenderManager3D render3D, string axisLabel) {
            if (render3D == null) {
                throw new ArgumentNullException(nameof(render3D));
            }

            if (string.IsNullOrWhiteSpace(axisLabel)) {
                throw new ArgumentException("Axis-label text must be provided.", nameof(axisLabel));
            }

            ModelAsset modelAsset = TransformGizmoAxisLabelModelFactory.Create(Font, axisLabel);
            AxisLabelModels[axisLabel] = render3D.BuildModelFromRaw(modelAsset);
        }

        /// <summary>
        /// Builds a signed axis label from a normalized world-space axis direction.
        /// </summary>
        /// <param name="axisDirection">World-space axis direction.</param>
        /// <returns>Signed axis label such as x+, y-, or z+.</returns>
        string BuildAxisLabel(float3 axisDirection) {
            double absX = Math.Abs(axisDirection.X);
            double absY = Math.Abs(axisDirection.Y);
            double absZ = Math.Abs(axisDirection.Z);
            if (absX >= absY && absX >= absZ) {
                return axisDirection.X >= 0f ? "x+" : "x-";
            }

            if (absY >= absX && absY >= absZ) {
                return axisDirection.Y >= 0f ? "y+" : "y-";
            }

            return axisDirection.Z >= 0f ? "z+" : "z-";
        }

        /// <summary>
        /// Resolves the scale used by axis-label billboards so drag-time label size matches the frozen translation-gizmo size.
        /// </summary>
        /// <param name="origin">Selected entity position used as the gizmo origin.</param>
        /// <param name="cameraPosition">World-space camera position.</param>
        /// <returns>Uniform world scale used by the axis-label billboards.</returns>
        double ResolveAxisLabelScale(float3 origin, float3 cameraPosition) {
            double computedScale = ComputeGizmoScale(origin, cameraPosition);
            TransformTranslationGizmoFollowComponent gizmoFollowComponent = TransformTranslationGizmoFollowComponent.GetForCamera(SceneCamera);
            double frozenScale = 0.0;
            if (gizmoFollowComponent != null) {
                frozenScale = gizmoFollowComponent.CurrentScale;
            }

            return TransformGizmoAxisLabelScaleResolver.Resolve(
                EditorGizmoDragService.IsDragging(SceneCamera),
                computedScale,
                frozenScale);
        }

        /// <summary>
        /// Computes the current translation-gizmo world scale so label positions match the gizmo tip positions.
        /// </summary>
        /// <param name="origin">Selected entity position used as the gizmo origin.</param>
        /// <param name="cameraPosition">World-space camera position.</param>
        /// <returns>World scale applied by the translation gizmo.</returns>
        double ComputeGizmoScale(float3 origin, float3 cameraPosition) {
            float4 viewport = SceneCamera.Viewport;
            double viewportHeight = viewport.W;
            if (viewportHeight <= 0.0) {
                throw new InvalidOperationException("Scene camera viewport height must be greater than zero.");
            }

            if (TransformTranslationGizmoFactory.AxisLength <= 0f) {
                throw new InvalidOperationException("Transform gizmo axis length must be greater than zero.");
            }

            double targetAxisPixels = viewportHeight * GizmoTargetViewportHeightFraction;
            float3 offset = origin - cameraPosition;
            double distance = Math.Sqrt(
                (offset.X * offset.X) +
                (offset.Y * offset.Y) +
                (offset.Z * offset.Z));
            if (distance < MinimumCameraDistance) {
                distance = MinimumCameraDistance;
            }

            double tanHalfFov = Math.Tan(PerspectiveVerticalFieldOfViewRadians * 0.5);
            if (tanHalfFov <= 0.0) {
                throw new InvalidOperationException("Perspective field of view must produce a positive tangent value.");
            }

            double targetWorldAxisLength = targetAxisPixels * (2.0 * distance * tanHalfFov) / viewportHeight;
            return targetWorldAxisLength / TransformTranslationGizmoFactory.AxisLength;
        }

        /// <summary>
        /// Resolves the world-space direction of one translation axis after yaw-facing orientation is applied.
        /// </summary>
        /// <param name="axisIndex">Zero-based translation-axis slot index.</param>
        /// <param name="yawFacingOrientation">Current snapped yaw orientation applied to horizontal gizmo handles.</param>
        /// <returns>Normalized world-space direction for the requested axis.</returns>
        float3 ResolveAxisDirection(int axisIndex, float4 yawFacingOrientation) {
            float4 axisOrientation = ResolveAxisHandleOrientation(axisIndex, yawFacingOrientation);
            return NormalizeDirection(float4.RotateVector(AxisHandleLocalPrimaryDirection, axisOrientation));
        }

        /// <summary>
        /// Resolves the world-space handle orientation for one translation axis after yaw-facing rotation is applied.
        /// </summary>
        /// <param name="axisIndex">Zero-based translation-axis slot index.</param>
        /// <param name="yawFacingOrientation">Current snapped yaw orientation applied to horizontal gizmo handles.</param>
        /// <returns>Combined world-space orientation for the requested axis handle.</returns>
        float4 ResolveAxisHandleOrientation(int axisIndex, float4 yawFacingOrientation) {
            return yawFacingOrientation * ResolveAxisBaseOrientation(axisIndex);
        }

        /// <summary>
        /// Resolves the base local orientation used by one translation axis before yaw-facing rotation is applied.
        /// </summary>
        /// <param name="axisIndex">Zero-based translation-axis slot index.</param>
        /// <returns>Base local orientation for the requested translation axis.</returns>
        float4 ResolveAxisBaseOrientation(int axisIndex) {
            switch (axisIndex) {
                case 0:
                    return XAxisBaseOrientation;
                case 1:
                    return YAxisBaseOrientation;
                case 2:
                    return ZAxisBaseOrientation;
                default:
                    throw new ArgumentOutOfRangeException(nameof(axisIndex), "Axis label slot index must be within the supported translation-axis range.");
            }
        }

        /// <summary>
        /// Normalizes a direction and returns zero when the vector magnitude is too small.
        /// </summary>
        /// <param name="value">Direction vector to normalize.</param>
        /// <returns>Normalized direction or zero when input magnitude is near zero.</returns>
        float3 NormalizeDirection(float3 value) {
            double lengthSquared =
                (value.X * value.X) +
                (value.Y * value.Y) +
                (value.Z * value.Z);
            if (lengthSquared <= MinimumDirectionLengthSquared) {
                return float3.Zero;
            }

            double inverseLength = 1.0 / Math.Sqrt(lengthSquared);
            return new float3(
                (float)(value.X * inverseLength),
                (float)(value.Y * inverseLength),
                (float)(value.Z * inverseLength));
        }

        /// <summary>
        /// Sets visibility for all translation-axis label billboards.
        /// </summary>
        /// <param name="visible">True to render the axis labels; false to hide them.</param>
        void SetAxisLabelsVisible(bool visible) {
            if (AxisLabelEntities == null) {
                return;
            }

            for (int axisIndex = 0; axisIndex < AxisLabelEntities.Length; axisIndex++) {
                if (AxisLabelEntities[axisIndex] == null) {
                    continue;
                }

                AxisLabelEntities[axisIndex].Enabled = visible;
            }
        }

        /// <summary>
        /// Creates a quaternion from a world-space axis and angle in radians.
        /// </summary>
        /// <param name="axis">Normalized or non-normalized axis of rotation.</param>
        /// <param name="angleRadians">Rotation angle in radians.</param>
        /// <returns>Quaternion representing the requested axis-angle rotation.</returns>
        static float4 CreateAxisOrientation(float3 axis, double angleRadians) {
            float3 rotationAxis = axis;
            float4 orientation;
            float4.CreateFromAxisAngle(ref rotationAxis, (float)angleRadians, out orientation);
            return orientation;
        }

        /// <summary>
        /// Determines whether translation tool visuals should be active for this viewport.
        /// </summary>
        /// <returns>True when the viewport tool mode is translation.</returns>
        bool IsTranslateToolActive() {
            return EditorViewportToolService.GetToolMode(SceneCamera) == EditorViewportToolMode.Translate;
        }

        /// <summary>
        /// Normalizes an angle in radians to the [-PI, PI] range.
        /// </summary>
        /// <param name="angleRadians">Angle to normalize.</param>
        /// <returns>Normalized angle in radians.</returns>
        double NormalizeAngleRadians(double angleRadians) {
            double twoPi = Math.PI * 2.0;
            double normalized = angleRadians;
            while (normalized > Math.PI) {
                normalized -= twoPi;
            }
            while (normalized < -Math.PI) {
                normalized += twoPi;
            }

            return normalized;
        }

        /// <summary>
        /// Formats an angle in degrees for compact debug display.
        /// </summary>
        /// <param name="angleDegrees">Angle in degrees.</param>
        /// <returns>Angle string with a single decimal precision.</returns>
        string FormatAngleDegrees(double angleDegrees) {
            return angleDegrees.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
