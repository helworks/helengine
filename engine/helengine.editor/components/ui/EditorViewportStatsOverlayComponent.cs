namespace helengine.editor {
    /// <summary>
    /// Draws a toggleable Unity-style stats box over an editor viewport showing frame rate and scene metrics.
    /// </summary>
    public class EditorViewportStatsOverlayComponent : UpdateComponent {
        /// <summary>
        /// Horizontal pixel padding applied to overlay text.
        /// </summary>
        const int OverlayPaddingX = 8;
        /// <summary>
        /// Vertical pixel padding applied to overlay text.
        /// </summary>
        const int OverlayPaddingY = 6;
        /// <summary>
        /// Offset from the viewport content edges.
        /// </summary>
        const int OverlayMargin = 8;
        /// <summary>
        /// Rolling frame-sample window used for FPS smoothing.
        /// </summary>
        const int FrameSampleWindow = 60;
        /// <summary>
        /// Number of frames between stats text rebuilds so the readout stays legible.
        /// </summary>
        const int TextRefreshFrameInterval = 15;

        /// <summary>
        /// Scene camera whose visible drawable queue is reported.
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
        /// Rolling tracker producing smoothed FPS values.
        /// </summary>
        readonly EditorViewportFrameRateTracker FrameRateTracker;

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
        /// Text component displaying live stats values.
        /// </summary>
        TextComponent OverlayText;
        /// <summary>
        /// Width in pixels of the viewport content used for right alignment.
        /// </summary>
        float AnchorWidth;
        /// <summary>
        /// Frame counter driving the text refresh throttle.
        /// </summary>
        int FrameCounter;
        /// <summary>
        /// Tracks whether overlay entities were created.
        /// </summary>
        bool Initialized;

        /// <summary>
        /// Initializes one viewport stats overlay component.
        /// </summary>
        /// <param name="sceneCamera">Scene camera whose visible drawables are reported.</param>
        /// <param name="font">Font used for overlay text.</param>
        /// <param name="viewportTopOffset">Offset in pixels from title bar top to viewport content top.</param>
        public EditorViewportStatsOverlayComponent(CameraComponent sceneCamera, FontAsset font, int viewportTopOffset) {
            SceneCamera = sceneCamera ?? throw new ArgumentNullException(nameof(sceneCamera));
            Font = font ?? throw new ArgumentNullException(nameof(font));
            if (viewportTopOffset < 0) {
                throw new ArgumentOutOfRangeException(nameof(viewportTopOffset), "Viewport top offset must be zero or greater.");
            }

            ViewportTopOffset = viewportTopOffset;
            FrameRateTracker = new EditorViewportFrameRateTracker(FrameSampleWindow);
        }

        /// <summary>
        /// Gets whether the stats box is currently shown.
        /// </summary>
        public bool IsVisible { get; private set; }

        /// <summary>
        /// Shows or hides the stats box.
        /// </summary>
        /// <param name="visible">True to render the stats box.</param>
        public void SetVisible(bool visible) {
            IsVisible = visible;
            if (OverlayRoot != null) {
                OverlayRoot.Enabled = visible;
                SetHierarchyEnabled(OverlayRoot, visible);
            }
        }

        /// <summary>
        /// Stores the viewport content width used to right-align the stats box.
        /// </summary>
        /// <param name="viewportWidth">Viewport content width in pixels.</param>
        public void SetAnchorWidth(float viewportWidth) {
            AnchorWidth = viewportWidth;
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
                throw new InvalidOperationException("Viewport stats overlay must be attached to an EditorEntity.");
            }

            OverlayRoot = new EditorEntity {
                InternalEntity = true,
                LayerMask = editorEntity.LayerMask,
                Position = new float3(OverlayMargin, DockableEntity.TitleBarHeight + ViewportTopOffset + OverlayMargin, 0.35f)
            };
            editorEntity.AddChild(OverlayRoot);

            OverlayBackground = new RoundedRectComponent {
                Radius = 5f,
                BorderThickness = 1f,
                FillColor = new byte4(0, 0, 0, 145),
                BorderColor = new byte4(255, 255, 255, 64),
                Size = new int2(1, 1),
                RenderOrder2D = RenderOrder2D.OverlayBackground
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
                RenderOrder2D = RenderOrder2D.OverlayForeground,
                Size = new int2(1, 1),
                Text = string.Empty
            };
            TextHost.AddComponent(OverlayText);

            Initialized = true;
            SetVisible(IsVisible);
        }

        /// <summary>
        /// Records frame timing and refreshes the stats readout while visible.
        /// </summary>
        public override void Update() {
            if (!Initialized) {
                return;
            }

            FrameRateTracker.Record(Core.Instance.FrameDeltaSeconds);
            if (!IsVisible) {
                return;
            }

            FrameCounter++;
            if (FrameCounter % TextRefreshFrameInterval != 0 && OverlayText.Text.Length != 0) {
                return;
            }

            string text = EditorViewportStatsTextBuilder.Build(BuildSnapshot());
            OverlayText.Text = text;
            LayoutOverlay(text);
        }

        /// <summary>
        /// Gathers the current frame metrics split into authored-scene and editor groups.
        /// </summary>
        /// <returns>Populated stats snapshot.</returns>
        EditorViewportStatsSnapshot BuildSnapshot() {
            ObjectManager objectManager = Core.Instance.ObjectManager;
            EditorViewportStatsGroup scene = new EditorViewportStatsGroup();
            EditorViewportStatsGroup editor = new EditorViewportStatsGroup();

            for (int index = 0; index < objectManager.Entities.Count; index++) {
                if (EditorViewportStatsSceneClassifier.IsSceneEntity(objectManager.Entities[index])) {
                    scene.EntityCount++;
                } else {
                    editor.EntityCount++;
                }
            }

            for (int index = 0; index < objectManager.Drawables3D.Count; index++) {
                IDrawable3D drawable = objectManager.Drawables3D[index];
                Entity owner = drawable.Parent;
                bool isVisible = owner != null && (owner.LayerMask & SceneCamera.LayerMask) != 0;
                if (EditorViewportStatsSceneClassifier.IsSceneEntity(owner)) {
                    scene.TotalDrawables3D++;
                    if (isVisible) {
                        scene.VisibleDrawables3D++;
                    }
                } else {
                    editor.TotalDrawables3D++;
                    if (isVisible) {
                        editor.VisibleDrawables3D++;
                    }
                }
            }

            for (int index = 0; index < objectManager.Drawables2D.Count; index++) {
                if (EditorViewportStatsSceneClassifier.IsSceneEntity(objectManager.Drawables2D[index].Parent)) {
                    scene.TotalDrawables2D++;
                } else {
                    editor.TotalDrawables2D++;
                }
            }

            for (int index = 0; index < objectManager.DirectionalLights.Count; index++) {
                if (EditorViewportStatsSceneClassifier.IsSceneEntity(objectManager.DirectionalLights[index].Parent)) {
                    scene.DirectionalLightCount++;
                } else {
                    editor.DirectionalLightCount++;
                }
            }

            for (int index = 0; index < objectManager.PointLights.Count; index++) {
                if (EditorViewportStatsSceneClassifier.IsSceneEntity(objectManager.PointLights[index].Parent)) {
                    scene.PointLightCount++;
                } else {
                    editor.PointLightCount++;
                }
            }

            for (int index = 0; index < objectManager.SpotLights.Count; index++) {
                if (EditorViewportStatsSceneClassifier.IsSceneEntity(objectManager.SpotLights[index].Parent)) {
                    scene.SpotLightCount++;
                } else {
                    editor.SpotLightCount++;
                }
            }

            for (int index = 0; index < objectManager.AmbientLights.Count; index++) {
                if (EditorViewportStatsSceneClassifier.IsSceneEntity(objectManager.AmbientLights[index].Parent)) {
                    scene.AmbientLightCount++;
                } else {
                    editor.AmbientLightCount++;
                }
            }

            return new EditorViewportStatsSnapshot {
                Fps = FrameRateTracker.AverageFps,
                FrameMilliseconds = FrameRateTracker.AverageFrameMilliseconds,
                Scene = scene,
                Editor = editor,
                UpdateableCount = objectManager.Updateables.Count
            };
        }

        /// <summary>
        /// Sizes the background to the current text and right-aligns the box inside the viewport content.
        /// </summary>
        /// <param name="text">Current stats text.</param>
        void LayoutOverlay(string text) {
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

            float overlayX = Math.Max(OverlayMargin, AnchorWidth - width - OverlayMargin);
            OverlayRoot.Position = new float3(overlayX, DockableEntity.TitleBarHeight + ViewportTopOffset + OverlayMargin, 0.35f);
        }

        /// <summary>
        /// Applies one enabled state to an entity subtree.
        /// </summary>
        /// <param name="entity">Subtree root.</param>
        /// <param name="enabled">Enabled state to apply.</param>
        static void SetHierarchyEnabled(Entity entity, bool enabled) {
            entity.Enabled = enabled;
            if (entity.Children == null) {
                return;
            }

            for (int index = 0; index < entity.Children.Count; index++) {
                if (entity.Children[index] is Entity childEntity) {
                    SetHierarchyEnabled(childEntity, enabled);
                }
            }
        }
    }
}
