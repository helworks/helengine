namespace helengine.editor {
    /// <summary>
    /// Draws live camera-angle diagnostics over an editor viewport so gizmo snapping behavior can be discussed precisely.
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
        /// Smallest squared horizontal length treated as a valid camera-to-selection direction.
        /// </summary>
        const double MinimumHorizontalLengthSquared = 0.000000000001;
        /// <summary>
        /// Default forward axis used to derive yaw and pitch from camera orientation.
        /// </summary>
        static readonly float3 ForwardAxis = new float3(0f, 0f, -1f);

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
            OverlayBackgroundRenderOrder = Core.Instance.ObjectManager.GetRenderOrderForLayer2D(2);
            OverlayTextRenderOrder = Core.Instance.ObjectManager.GetRenderOrderForLayer2D(3);
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

            Initialized = true;
        }

        /// <summary>
        /// Refreshes debug text and overlay layout each frame.
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
