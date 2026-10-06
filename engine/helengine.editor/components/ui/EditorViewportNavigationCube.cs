namespace helengine.editor {
    /// <summary>
    /// Displays a GPU-rendered 3D navigation cube and routes viewport-owned pointer gestures.
    /// </summary>
    public sealed class EditorViewportNavigationCube : IDisposable {
        /// <summary>Logical width and height of the cube raster canvas.</summary>
        const ushort CanvasSize = EditorViewportNavigationCubeInteractionController.LogicalCubeSize;
        /// <summary>RGBA8 byte count of the cube raster canvas.</summary>
        const int CanvasByteCount = CanvasSize * CanvasSize * 4;
        /// <summary>Logical gap between the cube and projection control.</summary>
        const int ControlGap = 8;
        /// <summary>Signed face targets in the same order as the overlay face colors and labels.</summary>
        static readonly EditorViewportNavigationTarget[] FaceTargets = {
            new EditorViewportNavigationTarget(-1,0,0), new EditorViewportNavigationTarget(1,0,0),
            new EditorViewportNavigationTarget(0,-1,0), new EditorViewportNavigationTarget(0,1,0),
            new EditorViewportNavigationTarget(0,0,-1), new EditorViewportNavigationTarget(0,0,1)
        };
        /// <summary>Readable labels corresponding to the signed faces.</summary>
        static readonly string[] FaceNames = { "Left", "Right", "Bottom", "Top", "Back", "Front" };
        /// <summary>Signed-face material colors used by the GPU-rendered cube.</summary>
        static readonly byte4[] FaceColors = {
            new byte4(155,62,62,255), new byte4(225,104,104,255),
            new byte4(55,130,75,255), new byte4(105,190,112,255),
            new byte4(58,93,158,255), new byte4(105,153,224,255)
        };
        /// <summary>Viewport entity that owns all overlay children.</summary>
        readonly EditorEntity ViewportEntity;
        /// <summary>Camera orientation represented by the cube.</summary>
        readonly CameraComponent SceneCamera;
        /// <summary>Session-owned 2D renderer for the overlay canvas.</summary>
        readonly RenderManager2D Renderer;
        /// <summary>Session shaders used by the physical cube faces.</summary>
        readonly EditorBuiltInShaderAssetLibrary Shaders;
        /// <summary>Private 3D scene rendered behind the interaction feedback.</summary>
        EditorNavigationCubeRenderScene CubeRenderScene;
        /// <summary>Transparent UI feedback drawn above the GPU cube image.</summary>
        SpriteComponent HoverSprite;
        /// <summary>Session-owned input system used to poll pointer state.</summary>
        readonly InputSystem Input;
        /// <summary>Session-owned font used by cube labels.</summary>
        FontAsset Font;
        /// <summary>Scaled UI layout metrics for this viewport.</summary>
        EditorUiMetrics UiMetrics;
        /// <summary>Frame duration callback used by view transitions.</summary>
        readonly Func<double> FrameDeltaSecondsProvider;
        /// <summary>Shared projected cube geometry used for rendering and hit testing.</summary>
        readonly EditorViewportNavigationCubeGeometry Geometry = new EditorViewportNavigationCubeGeometry();
        /// <summary>Pointer and camera gesture state for this viewport.</summary>
        readonly EditorViewportNavigationCubeInteractionController Interaction;
        /// <summary>Root entity for the private 2D overlay hierarchy.</summary>
        EditorEntity OverlayRoot;
        /// <summary>Sprite presenting the color target produced by the private 3D camera.</summary>
        SpriteComponent CubeSprite;
        /// <summary>Renderer-owned texture used only by the cube overlay.</summary>
        RuntimeTexture CubeTexture;
        /// <summary>Transparent RGBA8 canvas containing only pointer feedback and edge outlines.</summary>
        byte[] CubePixels;
        /// <summary>Face label entities positioned over visible faces.</summary>
        EditorEntity[] FaceLabelEntities;
        /// <summary>Face label text drawables.</summary>
        TextComponent[] FaceLabelTexts;
        /// <summary>Root entity for the projection button.</summary>
        EditorEntity ProjectionRoot;
        /// <summary>Projection button background drawable.</summary>
        RoundedRectComponent ProjectionBackground;
        /// <summary>Projection button text drawable.</summary>
        TextComponent ProjectionText;
        /// <summary>Root entity for the non-interactive destination preview shown during cube hover.</summary>
        EditorEntity HoverPreviewRoot;
        /// <summary>Destination preview background that keeps hover text legible over scene content.</summary>
        RoundedRectComponent HoverPreviewBackground;
        /// <summary>Text showing the face, edge, or corner the current hit will select.</summary>
        TextComponent HoverPreviewText;
        /// <summary>Child entity that positions destination text inside its background pill.</summary>
        EditorEntity HoverPreviewTextRoot;
        /// <summary>Whether view entities and renderer texture were created.</summary>
        bool IsInitialized;
        /// <summary>Whether input and renderer resources were released.</summary>
        bool IsDisposed;

        /// <summary>
        /// Creates a navigation cube from resources owned by one editor session.
        /// </summary>
        /// <param name="viewportEntity">Viewport entity owning the overlay hierarchy.</param>
        /// <param name="sceneCamera">Editor scene camera shown by the viewport.</param>
        /// <param name="navigationController">Viewport-local camera navigation state.</param>
        /// <param name="renderer">Session-owned 2D renderer.</param>
        /// <param name="input">Session-owned input system.</param>
        /// <param name="font">Session-owned UI font.</param>
        /// <param name="uiMetrics">Current scaled UI metrics.</param>
        /// <param name="interactionServices">Session-owned pointer blocker services.</param>
        /// <param name="frameDeltaSecondsProvider">Returns frame duration in seconds.</param>
        /// <param name="shaders">Session shader library used by the GPU cube materials.</param>
        public EditorViewportNavigationCube(EditorEntity viewportEntity, CameraComponent sceneCamera,
            EditorViewportNavigationController navigationController, RenderManager2D renderer, InputSystem input,
            FontAsset font, EditorUiMetrics uiMetrics,
            EditorSessionInteractionServices interactionServices, Func<double> frameDeltaSecondsProvider,
            EditorBuiltInShaderAssetLibrary shaders) {
            ViewportEntity = viewportEntity ?? throw new ArgumentNullException(nameof(viewportEntity));
            SceneCamera = sceneCamera ?? throw new ArgumentNullException(nameof(sceneCamera));
            Renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
            Shaders = shaders ?? throw new ArgumentNullException(nameof(shaders));
            Input = input ?? throw new ArgumentNullException(nameof(input));
            Font = font ?? throw new ArgumentNullException(nameof(font));
            UiMetrics = uiMetrics ?? throw new ArgumentNullException(nameof(uiMetrics));
            FrameDeltaSecondsProvider = frameDeltaSecondsProvider ?? throw new ArgumentNullException(nameof(frameDeltaSecondsProvider));
            if (interactionServices == null) {
                throw new ArgumentNullException(nameof(interactionServices));
            }
            Interaction = new EditorViewportNavigationCubeInteractionController(
                navigationController ?? throw new ArgumentNullException(nameof(navigationController)),
                interactionServices.InputCapture);
        }

        /// <summary>Gets the viewport that must own the updater component.</summary>
        public EditorEntity ParentViewportEntity => ViewportEntity;
        /// <summary>Gets pointer state for viewport cancellation and behavioral tests.</summary>
        public EditorViewportNavigationCubeInteractionController InteractionController => Interaction;

        /// <summary>Creates the private 3D render scene and its overlay controls exactly once.</summary>
        public void Initialize() {
            EnsureNotDisposed();
            if (IsInitialized) {
                return;
            }
            OverlayRoot = CreateOverlayEntity("Navigation Cube Root");
            OverlayRoot.Enabled = false;
            ViewportEntity.AddChild(OverlayRoot);
            CubePixels = new byte[CanvasByteCount];
            CubeTexture = BuildCubeTexture();
            CubeRenderScene = new EditorNavigationCubeRenderScene(ViewportEntity, Shaders, FaceColors, CanvasSize * 2);
            CubeSprite = new SpriteComponent {
                Texture = CubeRenderScene.Target,
                Size = new int2(1, 1)
            };
            OverlayRoot.AddComponent(CubeSprite);
            HoverSprite = new SpriteComponent { Texture = CubeTexture, Size = new int2(1, 1) };
            EditorEntity hoverEntity = CreateOverlayEntity("Navigation Cube Hover Feedback");
            hoverEntity.LocalPosition = new float3(0f, 0f, 0.05f);
            OverlayRoot.AddChild(hoverEntity);
            hoverEntity.AddComponent(HoverSprite);
            CreateFaceLabels();
            CreateProjectionControl();
            CreateDestinationPreview();
            IsInitialized = true;
            Resize(new int2(Math.Max(0, (int)Math.Round(SceneCamera.Viewport.Z)),
                Math.Max(0, (int)Math.Round(SceneCamera.Viewport.W))), (float)UiMetrics.Scale);
        }

        /// <summary>Updates device bounds and overlay positions after content or UI scale changes.</summary>
        /// <param name="contentSize">Scene content width and height in device pixels.</param>
        /// <param name="uiScale">Effective editor UI scale.</param>
        public void Resize(int2 contentSize, float uiScale) {
            EnsureNotDisposed();
            if (!float.IsFinite(uiScale) || uiScale <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(uiScale), "UI scale must be finite and positive.");
            }
            int2 origin = new int2((int)Math.Round(SceneCamera.Viewport.X), (int)Math.Round(SceneCamera.Viewport.Y));
            Interaction.Resize(origin, contentSize, uiScale);
            if (!IsInitialized) {
                return;
            }
            OverlayRoot.Enabled = Interaction.IsVisible;
            SynchronizeProjection();
            CubeRenderScene.Synchronize(SceneCamera.Parent.Orientation, Interaction.IsVisible && ViewportEntity.IsHierarchyEnabled, Geometry.ProjectionMode);
            if (!Interaction.IsVisible) {
                ClearHoverPreview();
                return;
            }
            int margin = ScalePixels(EditorViewportNavigationCubeInteractionController.LogicalContentMargin, uiScale);
            int cubeSize = ScalePixels(CanvasSize, uiScale);
            float contentTopOffset = SceneCamera.Viewport.Y - ViewportEntity.Position.Y;
            OverlayRoot.Position = new float3(Math.Max(0, contentSize.X - margin - cubeSize), contentTopOffset + margin, 0.35f);
            CubeSprite.Size = new int2(cubeSize, cubeSize);
            HoverSprite.Size = CubeSprite.Size;
            int buttonWidth = Interaction.ProjectionControlScreenSize.X;
            int buttonHeight = Interaction.ProjectionControlScreenSize.Y;
            ProjectionRoot.Position = new float3((cubeSize - buttonWidth) * 0.5f, cubeSize + ScalePixels(ControlGap, uiScale), 0.05f);
            ProjectionBackground.Size = new int2(buttonWidth, buttonHeight);
            ProjectionRoot.Enabled = Interaction.IsProjectionControlVisible;
            LayoutProjectionText();
        }

        /// <summary>Cancels a pending click or orbit and releases viewport-wide input capture.</summary>
        public void CancelInteraction() {
            Interaction.CancelInteraction();
        }

        /// <summary>
        /// Applies refreshed font and scale resources after the editor UI scale changes.
        /// </summary>
        /// <param name="font">Updated editor UI font.</param>
        /// <param name="uiMetrics">Updated scaled editor UI metrics.</param>
        public void ApplyUiMetrics(FontAsset font, EditorUiMetrics uiMetrics) {
            EnsureNotDisposed();
            Font = font ?? throw new ArgumentNullException(nameof(font));
            UiMetrics = uiMetrics ?? throw new ArgumentNullException(nameof(uiMetrics));
            if (!IsInitialized) {
                return;
            }

            for (int labelIndex = 0; labelIndex < FaceLabelTexts.Length; labelIndex++) {
                FaceLabelTexts[labelIndex].Font = Font;
            }
            ProjectionText.Font = Font;
            Resize(new int2(Math.Max(0, (int)Math.Round(SceneCamera.Viewport.Z)),
                Math.Max(0, (int)Math.Round(SceneCamera.Viewport.W))), (float)UiMetrics.Scale);
            UpdateFaceLabels(SceneCamera.Parent.Orientation);
            LayoutProjectionText();
        }

        /// <summary>Keeps GPU, hover feedback, and hit testing on the viewport projection.</summary>
        void SynchronizeProjection() {
            Geometry.ProjectionMode = SceneCamera is ICameraProjectionSettings projection ? projection.ProjectionMode : CameraProjectionMode.Perspective;
            Interaction.ProjectionMode = Geometry.ProjectionMode;
        }

        /// <summary>Polls input, advances navigation, and refreshes the cube image and labels.</summary>
        public void Update() {
            EnsureNotDisposed();
            SynchronizeProjection();
            CubeRenderScene?.Synchronize(SceneCamera.Parent.Orientation, IsInitialized && ViewportEntity.IsHierarchyEnabled && Interaction.IsVisible, Geometry.ProjectionMode);
            if (!IsInitialized || !ViewportEntity.IsHierarchyEnabled || !Interaction.IsVisible) {
                return;
            }
            Entity cameraEntity = SceneCamera.Parent;
            if (cameraEntity == null) {
                throw new InvalidOperationException("Navigation cube camera must belong to an entity.");
            }
            IInputForegroundState focus = Input.Backend as IInputForegroundState;
            int2 pointerPosition = new int2(Input.GetPointerX(), Input.GetPointerY());
            Interaction.UpdatePointer(pointerPosition,
                Input.WasPointerPrimaryPressed(),
                Input.CurrentFrame.Pointer.IsButtonDown(InputPointerButton.Primary),
                focus == null || focus.IsForegroundActive,
                cameraEntity.Orientation,
                FrameDeltaSecondsProvider());
            UpdateCubeImage(cameraEntity.Orientation);
            UpdateFaceLabels(cameraEntity.Orientation);
            UpdateProjectionText();
            UpdateDestinationPreview(pointerPosition, new int2(
                Math.Max(0, (int)Math.Round(SceneCamera.Viewport.Z)),
                Math.Max(0, (int)Math.Round(SceneCamera.Viewport.W))));
        }

        /// <summary>Releases blockers, overlay entities, and the renderer-owned cube texture.</summary>
        public void Dispose() {
            if (IsDisposed) {
                return;
            }
            Interaction.Dispose();
            if (OverlayRoot != null && OverlayRoot.Parent == ViewportEntity) {
                ViewportEntity.RemoveChild(OverlayRoot);
                OverlayRoot.Dispose();
                OverlayRoot = null;
            }
            if (HoverPreviewRoot != null && HoverPreviewRoot.Parent == ViewportEntity) {
                ViewportEntity.RemoveChild(HoverPreviewRoot);
                HoverPreviewRoot.Dispose();
                HoverPreviewRoot = null;
            }
            if (CubeTexture != null) {
                Renderer.ReleaseTexture(CubeTexture);
                CubeTexture = null;
            }
            CubeRenderScene?.Dispose();
            CubeRenderScene = null;
            CubePixels = null;
            IsDisposed = true;
        }

        /// <summary>Builds the runtime RGBA texture from the initial transparent cube canvas.</summary>
        /// <returns>Renderer-owned overlay texture.</returns>
        RuntimeTexture BuildCubeTexture() {
            TextureAsset source = new TextureAsset {
                Colors = CubePixels, Width = CanvasSize, Height = CanvasSize,
                ColorFormat = TextureAssetColorFormat.Rgba32, AlphaPrecision = TextureAssetAlphaPrecision.A8,
                IsEngineOwned = true
            };
            try {
                return Renderer.BuildTextureFromRaw(source);
            } finally {
                source.Dispose();
            }
        }

        /// <summary>Creates a static non-selectable text label for each signed face.</summary>
        void CreateFaceLabels() {
            FaceLabelEntities = new EditorEntity[FaceTargets.Length];
            FaceLabelTexts = new TextComponent[FaceTargets.Length];
            for (int i = 0; i < FaceTargets.Length; i++) {
                EditorEntity labelEntity = CreateOverlayEntity(string.Concat("Navigation Cube ", FaceNames[i], " Label"));
                TextComponent label = new TextComponent {
                    Font = Font, Text = FaceNames[i], Color = new byte4(255,255,255,255),
                    SelectionEnabled = false, Size = new int2(1,1)
                };
                OverlayRoot.AddChild(labelEntity);
                labelEntity.AddComponent(label);
                FaceLabelEntities[i] = labelEntity;
                FaceLabelTexts[i] = label;
            }
        }

        /// <summary>Creates the projection mode button background and label entities.</summary>
        void CreateProjectionControl() {
            ProjectionRoot = CreateOverlayEntity("Navigation Cube Projection Button");
            ProjectionBackground = new RoundedRectComponent {
                Radius = 4f, BorderThickness = 1f, FillColor = new byte4(29,34,44,230),
                BorderColor = new byte4(170,184,207,255), Size = new int2(1,1)
            };
            ProjectionRoot.AddComponent(ProjectionBackground);
            EditorEntity textRoot = CreateOverlayEntity("Navigation Cube Projection Label");
            ProjectionText = new TextComponent {
                Font = Font, Text = "Persp", Color = new byte4(245,247,250,255),
                SelectionEnabled = false, Size = new int2(1,1)
            };
            OverlayRoot.AddChild(ProjectionRoot);
            ProjectionRoot.AddChild(textRoot);
            textRoot.AddComponent(ProjectionText);
        }

        /// <summary>Creates the hidden destination pill used to explain the current hover target.</summary>
        void CreateDestinationPreview() {
            HoverPreviewRoot = CreateOverlayEntity("Navigation Cube Destination Preview");
            HoverPreviewRoot.Enabled = false;
            HoverPreviewBackground = new RoundedRectComponent {
                Radius = 5f,
                BorderThickness = 1f,
                FillColor = new byte4(18,25,36,242),
                BorderColor = new byte4(255,226,133,255),
                Size = new int2(1,1)
            };
            HoverPreviewRoot.AddComponent(HoverPreviewBackground);
            HoverPreviewTextRoot = CreateOverlayEntity("Navigation Cube Destination Label");
            HoverPreviewText = new TextComponent {
                Font = Font,
                Text = string.Empty,
                Color = new byte4(255,255,255,255),
                SelectionEnabled = false,
                WrapText = true,
                Size = new int2(1,1)
            };
            HoverPreviewRoot.AddChild(HoverPreviewTextRoot);
            HoverPreviewTextRoot.AddComponent(HoverPreviewText);
            ViewportEntity.AddChild(HoverPreviewRoot);
        }

        /// <summary>Creates an internal, non-authored entity owned by this viewport overlay.</summary>
        /// <param name="name">Human-readable overlay role.</param>
        /// <returns>Unparented overlay entity.</returns>
        EditorEntity CreateOverlayEntity(string name) {
            return new EditorEntity(ViewportEntity.OwnerCore, ViewportEntity.InteractionServices) {
                Name = name, InternalEntity = true, LayerMask = ViewportEntity.LayerMask
            };
        }

        /// <summary>Throws when renderer-backed resources have already been released.</summary>
        void EnsureNotDisposed() {
            if (IsDisposed) {
                throw new ObjectDisposedException(nameof(EditorViewportNavigationCube));
            }
        }

        /// <summary>Updates the GPU camera and draws only interaction feedback above its render target.</summary>
        /// <param name="orientation">Current camera orientation.</param>
        void UpdateCubeImage(float4 orientation) {
            SynchronizeProjection();
            CubeRenderScene.Synchronize(orientation, true, Geometry.ProjectionMode);
            Array.Clear(CubePixels);
            IReadOnlyList<float2> vertices = Geometry.GetProjectedVertices(orientation, CanvasSize);
            IReadOnlyList<int2> edges = Geometry.GetVisibleEdgeVertexIndices(orientation, CanvasSize);
            for (int i = 0; i < edges.Count; i++) {
                int2 edge = edges[i];
                DrawLine(vertices[edge.X], vertices[edge.Y], new byte4(220,228,240,255));
            }
            DrawHover(vertices, edges, orientation);
            Renderer.UpdateTextureRegion(CubeTexture, 0, 0, CanvasSize, CanvasSize, CubePixels, CanvasSize * 4);
        }

        /// <summary>Positions labels on the visible face polygons returned by shared cube geometry.</summary>
        /// <param name="orientation">Camera orientation used for face visibility and projection.</param>
        void UpdateFaceLabels(float4 orientation) {
            IReadOnlyList<EditorViewportNavigationTarget> visible = Geometry.GetVisibleFaceTargets(orientation);
            for (int i = 0; i < FaceTargets.Length; i++) {
                bool faceVisible = ContainsTarget(visible, FaceTargets[i]);
                FaceLabelEntities[i].Enabled = Interaction.IsVisible && faceVisible;
                if (!faceVisible) {
                    continue;
                }
                IReadOnlyList<float2> polygon = Geometry.GetFaceVertices(FaceTargets[i], orientation, CanvasSize);
                float centerX = 0f;
                float centerY = 0f;
                for (int p = 0; p < polygon.Count; p++) {
                    centerX += polygon[p].X;
                    centerY += polygon[p].Y;
                }
                centerX /= polygon.Count;
                centerY /= polygon.Count;
                FontTightMetrics metrics = Font.MeasureTight(FaceNames[i]);
                float scale = (float)UiMetrics.Scale;
                FaceLabelEntities[i].Position = new float3(
                    (float)((centerX * scale) - (metrics.Width * 0.5)),
                    (float)((centerY * scale) - (Font.LineHeight * 0.5)), 0.1f);
                FaceLabelTexts[i].Size = new int2(Math.Max(1, (int)Math.Ceiling(metrics.Width)), Math.Max(1, (int)Math.Ceiling(Font.LineHeight)));
            }
        }

        /// <summary>Updates projection label contents, centering, and hover colors.</summary>
        void UpdateProjectionText() {
            ICameraProjectionSettings projection = SceneCamera as ICameraProjectionSettings;
            ProjectionText.Text = projection != null && projection.ProjectionMode == CameraProjectionMode.Orthographic ? "Ortho" : "Persp";
            if (Interaction.IsProjectionControlHovered) {
                ProjectionBackground.FillColor = new byte4(55,86,128,245);
                ProjectionBackground.BorderColor = new byte4(205,220,245,255);
            } else {
                ProjectionBackground.FillColor = new byte4(29,34,44,230);
                ProjectionBackground.BorderColor = new byte4(170,184,207,255);
            }
            LayoutProjectionText();
        }

        /// <summary>Shows and positions a bounded pill naming the projection or direction selected by the current hover.</summary>
        /// <param name="pointerPosition">Window-space pointer used to avoid covering the hovered region in tight layouts.</param>
        /// <param name="contentSize">Device-pixel viewport content dimensions.</param>
        void UpdateDestinationPreview(int2 pointerPosition, int2 contentSize) {
            EditorViewportNavigationTarget target = Interaction.HoveredTarget;
            if (!Interaction.IsVisible) {
                ClearHoverPreview();
                return;
            }

            if (Interaction.IsCenterToggleHovered) {
                HoverPreviewText.Text = BuildProjectionToggleDestination();
            } else if (target != null) {
                HoverPreviewText.Text = BuildDestinationLabel(target);
            } else {
                ClearHoverPreview();
                return;
            }
            FontTightMetrics textMetrics = Font.MeasureTight(HoverPreviewText.Text);
            int margin = ScalePixels(EditorViewportNavigationCubeInteractionController.LogicalContentMargin, (float)UiMetrics.Scale);
            int paddingX = ScalePixels(5, (float)UiMetrics.Scale);
            int paddingY = ScalePixels(3, (float)UiMetrics.Scale);
            int gap = ScalePixels(ControlGap, (float)UiMetrics.Scale);
            int availableContentWidth = Math.Max(1, contentSize.X - (margin * 2));
            int desiredPanelWidth = Math.Max(1, (int)Math.Ceiling(textMetrics.Width) + (paddingX * 2));
            int panelWidth = Math.Min(desiredPanelWidth, availableContentWidth);
            int contentOriginX = (int)Math.Round(SceneCamera.Viewport.X - ViewportEntity.Position.X);
            int contentOriginY = (int)Math.Round(SceneCamera.Viewport.Y - ViewportEntity.Position.Y);
            int cubeX = (int)Math.Round(OverlayRoot.LocalPosition.X - contentOriginX);
            int cubeY = (int)Math.Round(OverlayRoot.LocalPosition.Y - contentOriginY);
            int leftSpace = cubeX - margin - gap;
            bool placeLeftOfCube = leftSpace >= ScalePixels(48, (float)UiMetrics.Scale);
            if (placeLeftOfCube) {
                panelWidth = Math.Min(panelWidth, leftSpace);
            }

            int textWidth = Math.Max(1, panelWidth - (paddingX * 2));
            int lineCount = CountWrappedLines(HoverPreviewText.Text, textWidth);
            int lineHeight = Math.Max(1, (int)Math.Ceiling(Font.LineHeight));
            int panelHeight = (lineHeight * lineCount) + (paddingY * 2);
            int previewX;
            int previewY;
            if (placeLeftOfCube) {
                previewX = contentOriginX + cubeX - panelWidth - gap;
                previewY = contentOriginY + cubeY;
            } else {
                float2 logicalPointer = new float2(
                    (float)((pointerPosition.X - Interaction.CubeScreenPosition.X) / UiMetrics.Scale),
                    (float)((pointerPosition.Y - Interaction.CubeScreenPosition.Y) / UiMetrics.Scale));
                previewX = contentOriginX + cubeX + (logicalPointer.X < (CanvasSize * 0.5f)
                    ? ScalePixels(CanvasSize, (float)UiMetrics.Scale) - margin - panelWidth
                    : margin);
                previewY = contentOriginY + cubeY + (logicalPointer.Y < (CanvasSize * 0.5f)
                    ? ScalePixels(CanvasSize, (float)UiMetrics.Scale) - margin - panelHeight
                    : margin);
            }

            int contentLeft = contentOriginX + margin;
            int contentTop = contentOriginY + margin;
            int contentRight = contentOriginX + contentSize.X - margin;
            int contentBottom = contentOriginY + contentSize.Y - margin;
            previewX = Math.Clamp(previewX, contentLeft, Math.Max(contentLeft, contentRight - panelWidth));
            previewY = Math.Clamp(previewY, contentTop, Math.Max(contentTop, contentBottom - panelHeight));
            HoverPreviewRoot.Position = new float3(previewX, previewY, 0.4f);
            HoverPreviewBackground.Size = new int2(panelWidth, panelHeight);
            HoverPreviewTextRoot.Position = new float3(paddingX, paddingY, 0.01f);
            HoverPreviewText.Size = new int2(textWidth, Math.Max(lineHeight, lineHeight * lineCount));
            HoverPreviewRoot.Enabled = true;
            Interaction.SetHoverPreviewBounds(
                new int2((int)Math.Round(HoverPreviewRoot.Position.X), (int)Math.Round(HoverPreviewRoot.Position.Y)),
                new int2(panelWidth, panelHeight));
        }

        /// <summary>Hides the destination pill and releases its input blocker when the pointer leaves every target.</summary>
        void ClearHoverPreview() {
            if (HoverPreviewRoot != null) {
                HoverPreviewRoot.Enabled = false;
                HoverPreviewText.Text = string.Empty;
            }
            if (!IsDisposed && Interaction != null) {
                Interaction.SetHoverPreviewBounds(default, default);
            }
        }

        /// <summary>Formats non-zero target axes as signed face names in front, vertical, horizontal order.</summary>
        /// <param name="target">Visible hit target to describe.</param>
        /// <returns>Readable destination label for a face, edge, or corner.</returns>
        static string BuildDestinationLabel(EditorViewportNavigationTarget target) {
            List<string> faceNames = new List<string>(3);
            if (target.Z != 0) {
                faceNames.Add(target.Z > 0 ? "Front" : "Back");
            }
            if (target.Y != 0) {
                faceNames.Add(target.Y > 0 ? "Top" : "Bottom");
            }
            if (target.X != 0) {
                faceNames.Add(target.X > 0 ? "Right" : "Left");
            }

            return string.Join(" / ", faceNames);
        }

        /// <summary>Names the projection mode that a center click will select.</summary>
        /// <returns>Opposite projection mode shown by the editor viewport camera.</returns>
        string BuildProjectionToggleDestination() {
            ICameraProjectionSettings projection = SceneCamera as ICameraProjectionSettings;
            return projection != null && projection.ProjectionMode == CameraProjectionMode.Orthographic
                ? "Perspective"
                : "Orthographic";
        }

        /// <summary>Estimates the number of whole-word lines needed for the destination label's current width.</summary>
        /// <param name="text">Destination text split into face-name words.</param>
        /// <param name="maximumWidth">Available text width in device pixels.</param>
        /// <returns>At least one visual line.</returns>
        int CountWrappedLines(string text, int maximumWidth) {
            string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            double spaceWidth = Font.MeasureTight(" ").Width;
            double lineWidth = 0.0;
            int lineCount = 1;
            for (int wordIndex = 0; wordIndex < words.Length; wordIndex++) {
                double wordWidth = Font.MeasureTight(words[wordIndex]).Width;
                double candidateWidth = lineWidth == 0.0 ? wordWidth : lineWidth + spaceWidth + wordWidth;
                if (lineWidth > 0.0 && candidateWidth > maximumWidth) {
                    lineCount++;
                    lineWidth = wordWidth;
                } else {
                    lineWidth = candidateWidth;
                }
            }

            return lineCount;
        }

        /// <summary>Centers the projection label in the current button bounds.</summary>
        void LayoutProjectionText() {
            FontTightMetrics metrics = Font.MeasureTight(ProjectionText.Text);
            ProjectionText.Size = new int2(Math.Max(1, (int)Math.Ceiling(metrics.Width)), Math.Max(1, (int)Math.Ceiling(Font.LineHeight)));
            EditorEntity labelRoot = ProjectionText.Parent as EditorEntity;
            labelRoot.Position = new float3(
                Math.Max(0f, (ProjectionBackground.Size.X - metrics.Width) * 0.5f),
                Math.Max(0f, (ProjectionBackground.Size.Y - Font.LineHeight) * 0.5f), 0.1f);
        }

        /// <summary>Draws hover feedback for the center projection toggle or a face, edge, or corner target.</summary>
        /// <param name="vertices">Shared projected cube vertices.</param>
        /// <param name="edges">Visible physical edges.</param>
        /// <param name="orientation">Current camera orientation.</param>
        void DrawHover(IReadOnlyList<float2> vertices, IReadOnlyList<int2> edges, float4 orientation) {
            if (Interaction.IsCenterToggleHovered) {
                DrawCenterToggleHighlight();
                return;
            }

            EditorViewportNavigationTarget target = Interaction.HoveredTarget;
            if (target == null) {
                return;
            }
            if (target.IsFace) {
                DrawPolygon(Geometry.GetFaceVertices(target, orientation, CanvasSize), new byte4(255,226,125,104));
            } else if (target.IsEdge) {
                for (int i = 0; i < edges.Count; i++) {
                    int2 edge = edges[i];
                    if (ResolveEdgeTarget(edge).Equals(target)) {
                        DrawHighlightedEdge(vertices[edge.X], vertices[edge.Y], new byte4(255,226,105,255));
                    }
                }
            } else {
                IReadOnlyList<int> corners = Geometry.GetVisibleVertexIndices(orientation, CanvasSize);
                for (int i = 0; i < corners.Count; i++) {
                    int index = corners[i];
                    if (ResolveCornerTarget(index).Equals(target)) {
                        DrawCornerHighlight(vertices[index], new byte4(255,226,105,255));
                    }
                }
            }
        }

        /// <summary>Rasterizes a polygon with the projected vertices shared by the hit resolver.</summary>
        /// <param name="vertices">Ordered projected polygon corners.</param>
        /// <param name="color">RGBA face fill or highlight.</param>
        void DrawPolygon(IReadOnlyList<float2> vertices, byte4 color) {
            float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
            for (int i = 0; i < vertices.Count; i++) {
                minX = Math.Min(minX, vertices[i].X);
                minY = Math.Min(minY, vertices[i].Y);
                maxX = Math.Max(maxX, vertices[i].X);
                maxY = Math.Max(maxY, vertices[i].Y);
            }
            int left = Math.Clamp((int)Math.Floor(minX), 0, CanvasSize - 1);
            int top = Math.Clamp((int)Math.Floor(minY), 0, CanvasSize - 1);
            int right = Math.Clamp((int)Math.Ceiling(maxX), 0, CanvasSize - 1);
            int bottom = Math.Clamp((int)Math.Ceiling(maxY), 0, CanvasSize - 1);
            for (int y = top; y <= bottom; y++) {
                for (int x = left; x <= right; x++) {
                    if (ContainsPolygonPoint(vertices, new float2(x + 0.5f, y + 0.5f))) {
                        BlendPixel(x, y, color);
                    }
                }
            }
        }

        /// <summary>Draws a projected physical cube edge using an integer line raster.</summary>
        /// <param name="start">Projected line start.</param>
        /// <param name="end">Projected line end.</param>
        /// <param name="color">RGBA line color.</param>
        void DrawLine(float2 start, float2 end, byte4 color) {
            int x = (int)Math.Round(start.X), y = (int)Math.Round(start.Y);
            int endX = (int)Math.Round(end.X), endY = (int)Math.Round(end.Y);
            int dx = Math.Abs(endX - x), sx = x < endX ? 1 : -1;
            int dy = -Math.Abs(endY - y), sy = y < endY ? 1 : -1;
            int error = dx + dy;
            while (true) {
                BlendPixel(x, y, color);
                if (x == endX && y == endY) {
                    return;
                }
                int doubled = error * 2;
                if (doubled >= dy) { error += dy; x += sx; }
                if (doubled <= dx) { error += dx; y += sy; }
            }
        }

        /// <summary>Fills the same-width screen-space band accepted by the geometry edge hit radius.</summary>
        /// <param name="start">Projected edge start.</param>
        /// <param name="end">Projected edge end.</param>
        /// <param name="color">Opaque target highlight color.</param>
        void DrawHighlightedEdge(float2 start, float2 end, byte4 color) {
            double radius = CanvasSize * EditorViewportNavigationCubeGeometry.EdgeHitRadiusFraction;
            int padding = (int)Math.Ceiling(radius);
            int left = Math.Max(0, (int)Math.Floor(Math.Min(start.X, end.X)) - padding);
            int right = Math.Min(CanvasSize - 1, (int)Math.Ceiling(Math.Max(start.X, end.X)) + padding);
            int top = Math.Max(0, (int)Math.Floor(Math.Min(start.Y, end.Y)) - padding);
            int bottom = Math.Min(CanvasSize - 1, (int)Math.Ceiling(Math.Max(start.Y, end.Y)) + padding);
            double radiusSquared = radius * radius;
            for (int y = top; y <= bottom; y++) {
                for (int x = left; x <= right; x++) {
                    if (GetDistanceToSegmentSquared(new float2(x + 0.5f, y + 0.5f), start, end) <= radiusSquared) {
                        BlendPixel(x, y, color);
                    }
                }
            }
        }

        /// <summary>Measures squared distance between a pixel center and its closest point on a projected segment.</summary>
        /// <param name="point">Pixel center being rasterized.</param>
        /// <param name="start">Projected segment start.</param>
        /// <param name="end">Projected segment end.</param>
        /// <returns>Squared point-to-segment distance.</returns>
        static double GetDistanceToSegmentSquared(float2 point, float2 start, float2 end) {
            double deltaX = end.X - start.X;
            double deltaY = end.Y - start.Y;
            double lengthSquared = (deltaX * deltaX) + (deltaY * deltaY);
            double amount = lengthSquared <= 0.000000000001
                ? 0.0
                : Math.Clamp((((point.X - start.X) * deltaX) + ((point.Y - start.Y) * deltaY)) / lengthSquared, 0.0, 1.0);
            double nearestX = start.X + (deltaX * amount);
            double nearestY = start.Y + (deltaY * amount);
            double offsetX = point.X - nearestX;
            double offsetY = point.Y - nearestY;
            return (offsetX * offsetX) + (offsetY * offsetY);
        }

        /// <summary>Draws a filled corner patch using the shared geometry corner-hit radius.</summary>
        /// <param name="center">Projected corner position.</param>
        /// <param name="color">RGBA highlight color.</param>
        void DrawCornerHighlight(float2 center, byte4 color) {
            int cx = (int)Math.Round(center.X), cy = (int)Math.Round(center.Y);
            double radius = CanvasSize * EditorViewportNavigationCubeGeometry.CornerHitRadiusFraction;
            int padding = (int)Math.Ceiling(radius);
            for (int y = cy - padding; y <= cy + padding; y++) {
                for (int x = cx - padding; x <= cx + padding; x++) {
                    double dx = (x + 0.5) - center.X;
                    double dy = (y + 0.5) - center.Y;
                    if ((dx * dx) + (dy * dy) <= radius * radius) {
                        BlendPixel(x, y, color);
                    }
                }
            }
        }

        /// <summary>Draws a filled center badge and bright ring matching the projection-toggle hit radius.</summary>
        void DrawCenterToggleHighlight() {
            int center = CanvasSize / 2;
            int radius = EditorViewportNavigationCubeInteractionController.LogicalCenterToggleRadius;
            int innerRadius = radius - 2;
            double radiusSquared = radius * radius;
            double innerRadiusSquared = innerRadius * innerRadius;
            byte4 fillColor = new byte4(255,214,96,140);
            byte4 ringColor = new byte4(255,235,148,255);
            for (int y = center - radius; y <= center + radius; y++) {
                for (int x = center - radius; x <= center + radius; x++) {
                    double deltaX = (x + 0.5) - center;
                    double deltaY = (y + 0.5) - center;
                    double distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
                    if (distanceSquared <= radiusSquared) {
                        BlendPixel(x, y, distanceSquared >= innerRadiusSquared ? ringColor : fillColor);
                    }
                }
            }
        }

        /// <summary>Alpha-composites one color into the RGBA canvas with bounds clipping.</summary>
        /// <param name="x">Canvas pixel X.</param>
        /// <param name="y">Canvas pixel Y.</param>
        /// <param name="color">RGBA source color.</param>
        void BlendPixel(int x, int y, byte4 color) {
            if (x < 0 || y < 0 || x >= CanvasSize || y >= CanvasSize) {
                return;
            }
            int index = ((y * CanvasSize) + x) * 4;
            int alpha = color.W, inverse = 255 - color.W;
            byte previousAlpha = CubePixels[index + 3];
            int outputAlpha = alpha + ((previousAlpha * inverse) / 255);
            if (outputAlpha == 0) { return; }
            CubePixels[index] = (byte)(((color.X * alpha) + (CubePixels[index] * previousAlpha * inverse / 255)) / outputAlpha);
            CubePixels[index + 1] = (byte)(((color.Y * alpha) + (CubePixels[index + 1] * previousAlpha * inverse / 255)) / outputAlpha);
            CubePixels[index + 2] = (byte)(((color.Z * alpha) + (CubePixels[index + 2] * previousAlpha * inverse / 255)) / outputAlpha);
            CubePixels[index + 3] = (byte)outputAlpha;
        }

        /// <summary>Checks whether a pixel center is inside an ordered polygon.</summary>
        /// <param name="vertices">Ordered polygon corners.</param>
        /// <param name="point">Pixel-center coordinate.</param>
        /// <returns>True when inside the polygon.</returns>
        static bool ContainsPolygonPoint(IReadOnlyList<float2> vertices, float2 point) {
            bool inside = false;
            for (int current = 0, previous = vertices.Count - 1; current < vertices.Count; previous = current++) {
                float2 a = vertices[current], b = vertices[previous];
                if ((a.Y > point.Y) != (b.Y > point.Y) &&
                    point.X < (((b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y)) + a.X)) {
                    inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>Checks whether a visible-face list includes a target direction.</summary>
        /// <param name="faces">Visible face targets.</param>
        /// <param name="target">Target being searched.</param>
        /// <returns>True when the target appears in the collection.</returns>
        static bool ContainsTarget(IReadOnlyList<EditorViewportNavigationTarget> faces, EditorViewportNavigationTarget target) {
            for (int i = 0; i < faces.Count; i++) {
                if (faces[i].Equals(target)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Maps a physical edge endpoint pair to its two fixed signed axes.</summary>
        /// <param name="edge">Indices of the two endpoint corners.</param>
        /// <returns>Signed edge direction.</returns>
        static EditorViewportNavigationTarget ResolveEdgeTarget(int2 edge) {
            EditorViewportNavigationTarget first = ResolveCornerTarget(edge.X);
            EditorViewportNavigationTarget second = ResolveCornerTarget(edge.Y);
            return new EditorViewportNavigationTarget(
                first.X == second.X ? first.X : 0,
                first.Y == second.Y ? first.Y : 0,
                first.Z == second.Z ? first.Z : 0);
        }

        /// <summary>Maps one cube vertex index to its signed corner direction.</summary>
        /// <param name="index">Stable cube corner index.</param>
        /// <returns>Signed corner target.</returns>
        static EditorViewportNavigationTarget ResolveCornerTarget(int index) {
            return new EditorViewportNavigationTarget(
                (index & 1) == 0 ? -1 : 1,
                (index & 2) == 0 ? -1 : 1,
                (index & 4) == 0 ? -1 : 1);
        }

        /// <summary>Scales logical UI pixels upward to integral device pixels.</summary>
        /// <param name="logicalPixels">Positive logical pixel length.</param>
        /// <param name="scale">Effective editor UI scale.</param>
        /// <returns>Scaled pixel length.</returns>
        static int ScalePixels(int logicalPixels, double scale) {
            return Math.Max(1, (int)Math.Ceiling(logicalPixels * scale));
        }
    }
}
