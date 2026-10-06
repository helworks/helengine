namespace helengine.editor {
    /// <summary>
    /// Renders a fixed browser-style autoscroll marker at the middle-click activation point.
    /// </summary>
    internal sealed class EditorAutoScrollIndicatorOverlay : IDisposable {
        const int IndicatorSize = EditorAutoScrollIndicatorIconBuilder.Size;
        const float HalfIndicatorSize = IndicatorSize / 2f;
        /// <summary>Aligns the odd-sized marker's center pixel with the middle-click pixel.</summary>
        const int IndicatorAnchorOffset = IndicatorSize / 2;
        const float ShapeDepth = 0.1f;

        static readonly byte4 IndicatorFillColor = new byte4(246, 248, 251, 255);
        static readonly byte4 IndicatorBorderColor = new byte4(20, 27, 37, 255);
        /// <summary>Muted color retained by arrows whose direction is currently idle or blocked.</summary>
        static readonly byte4 IdleArrowColor = new byte4(139, 148, 160, 255);
        /// <summary>Accent applied only to the direction in which the owning scroll component can move.</summary>
        static readonly byte4 ActiveArrowColor = new byte4(54, 128, 197, 255);

        readonly Core CoreValue;
        readonly EditorEntity CameraEntity;
        readonly CameraComponent CameraComponentValue;
        readonly EditorEntity IndicatorRoot;
        /// <summary>Independently tintable upward chevron.</summary>
        readonly SpriteComponent UpArrow;
        /// <summary>Independently tintable downward chevron.</summary>
        readonly SpriteComponent DownArrow;
        /// <summary>Independently tintable leftward chevron.</summary>
        readonly SpriteComponent LeftArrow;
        /// <summary>Independently tintable rightward chevron.</summary>
        readonly SpriteComponent RightArrow;
        /// <summary>Runtime atlas owned by this overlay and uploaded only once.</summary>
        RuntimeTexture OwnedIconAtlas;
        /// <summary>Prevents disposed entities or texture resources from being reused.</summary>
        bool IsDisposed;
        ScrollOrientation ActiveOrientationValue;

        internal bool IsVisible => IndicatorRoot.Enabled;

        internal int2 IndicatorTopLeft => new int2(
            (int)MathF.Round(IndicatorRoot.Position.X),
            (int)MathF.Round(IndicatorRoot.Position.Y));

        internal ScrollOrientation? VisibleOrientation {
            get {
                if (!IsVisible) {
                    return null;
                }

                return ActiveOrientationValue;
            }
        }

        internal EditorAutoScrollIndicatorOverlay(Core core, EditorSessionInteractionServices interactionServices) {
            CoreValue = core ?? throw new ArgumentNullException(nameof(core));
            if (interactionServices == null) {
                throw new ArgumentNullException(nameof(interactionServices));
            }

            CameraEntity = new EditorEntity(CoreValue, interactionServices) {
                Name = "Autoscroll Indicator Camera",
                InternalEntity = true,
                LayerMask = EditorLayerMasks.AutoScrollIndicator,
                Position = new float3(0f, 3f, -8f)
            };
            CameraComponentValue = new CameraComponent {
                LayerMask = EditorLayerMasks.AutoScrollIndicator,
                CameraDrawOrder = EditorUiCameraDrawOrders.AutoScrollIndicator,
                ClearSettings = new CameraClearSettings(false, new float4(0f, 0f, 0f, 0f), false, 1f, false, 0)
            };
            CameraEntity.AddComponent(CameraComponentValue);
            CameraEntity.InitializeHierarchy();
            CameraEntity.Enabled = false;

            IndicatorRoot = new EditorEntity(CoreValue, interactionServices) {
                Name = "Autoscroll Indicator",
                InternalEntity = true,
                LayerMask = EditorLayerMasks.AutoScrollIndicator
            };
            IndicatorRoot.AddComponent(new RoundedRectComponent {
                Size = new int2(IndicatorSize, IndicatorSize),
                Radius = HalfIndicatorSize,
                BorderThickness = 1.25f,
                BorderColor = IndicatorBorderColor,
                FillColor = IndicatorFillColor
            });

            TextureAsset atlas = EditorAutoScrollIndicatorIconBuilder.CreateAtlas();
            try {
                OwnedIconAtlas = CoreValue.RenderManager2D.BuildTextureFromRaw(atlas);
            } finally {
                NativeOwnership.DisposeAndDelete(atlas);
            }
            UpArrow = CreateGlyph(interactionServices, "Autoscroll Up Arrow", EditorAutoScrollIndicatorIconBuilder.Up, IdleArrowColor);
            DownArrow = CreateGlyph(interactionServices, "Autoscroll Down Arrow", EditorAutoScrollIndicatorIconBuilder.Down, IdleArrowColor);
            LeftArrow = CreateGlyph(interactionServices, "Autoscroll Left Arrow", EditorAutoScrollIndicatorIconBuilder.Left, IdleArrowColor);
            RightArrow = CreateGlyph(interactionServices, "Autoscroll Right Arrow", EditorAutoScrollIndicatorIconBuilder.Right, IdleArrowColor);
            CreateGlyph(interactionServices, "Autoscroll Center Dot", EditorAutoScrollIndicatorIconBuilder.Dot, IndicatorBorderColor);

            IndicatorRoot.InitializeHierarchy();
            LeftArrow.Parent.Enabled = false;
            RightArrow.Parent.Enabled = false;
            IndicatorRoot.Enabled = false;
        }

        /// <summary>
        /// Updates overlay visibility and position from the active scroll component's fixed click anchor.
        /// </summary>
        internal void Update() {
            if (IsDisposed) {
                return;
            }
            PointerCursorKind cursorKind = CoreValue.PointerInteractionSystem.HoverCursor;
            bool showVertical = cursorKind == PointerCursorKind.AutoScrollVertical;
            bool showHorizontal = cursorKind == PointerCursorKind.AutoScrollHorizontal;
            bool visible = showVertical || showHorizontal;

            CameraEntity.Enabled = visible;
            IndicatorRoot.Enabled = visible;
            if (!visible) {
                return;
            }

            int2 anchorPosition = CoreValue.PointerInteractionSystem.CursorOverridePosition;
            IndicatorRoot.Position = new float3(
                anchorPosition.X - IndicatorAnchorOffset,
                anchorPosition.Y - IndicatorAnchorOffset,
                ShapeDepth);
            ActiveOrientationValue = showHorizontal ? ScrollOrientation.Horizontal : ScrollOrientation.Vertical;
            UpArrow.Parent.Enabled = showVertical;
            DownArrow.Parent.Enabled = showVertical;
            LeftArrow.Parent.Enabled = showHorizontal;
            RightArrow.Parent.Enabled = showHorizontal;
            UpArrow.Color = IdleArrowColor;
            DownArrow.Color = IdleArrowColor;
            LeftArrow.Color = IdleArrowColor;
            RightArrow.Color = IdleArrowColor;
            int direction = CoreValue.PointerInteractionSystem.CursorOverrideScrollDirection;
            if (direction < 0) {
                (showHorizontal ? LeftArrow : UpArrow).Color = ActiveArrowColor;
            } else if (direction > 0) {
                (showHorizontal ? RightArrow : DownArrow).Color = ActiveArrowColor;
            }
        }

        /// <summary>
        /// Keeps the overlay camera aligned with the host window's client area.
        /// </summary>
        internal void SetViewport(int width, int height) {
            CameraComponentValue.Viewport = new float4(0f, 0f, Math.Max(1, width), Math.Max(1, height));
        }

        /// <summary>Creates a sprite for one atlas mask at the fixed marker origin.</summary>
        /// <param name="interactionServices">Interaction graph owned by the editor session.</param>
        /// <param name="name">Semantic name of the direction or center element.</param>
        /// <param name="tile">Atlas tile that contains the glyph mask.</param>
        /// <param name="color">Initial tint of the glyph.</param>
        /// <returns>The sprite whose tint and visibility are updated by the overlay.</returns>
        SpriteComponent CreateGlyph(EditorSessionInteractionServices interactionServices, string name, int tile, byte4 color) {
            EditorEntity glyphRoot = new EditorEntity(CoreValue, interactionServices) {
                Name = name,
                InternalEntity = true,
                LayerMask = EditorLayerMasks.AutoScrollIndicator,
                LocalPosition = new float3(0f, 0f, ShapeDepth)
            };
            SpriteComponent sprite = new SpriteComponent {
                Texture = OwnedIconAtlas,
                SourceRect = EditorAutoScrollIndicatorIconBuilder.GetSourceRect(tile),
                Size = new int2(IndicatorSize, IndicatorSize),
                Color = color
            };
            glyphRoot.AddComponent(sprite);
            IndicatorRoot.AddChild(glyphRoot);
            return sprite;
        }

        /// <summary>Detaches the overlay entities before releasing its renderer-owned atlas, once.</summary>
        public void Dispose() {
            if (IsDisposed) {
                return;
            }
            IndicatorRoot.Dispose();
            CameraEntity.Dispose();
            if (OwnedIconAtlas != null) {
                CoreValue.RenderManager2D.ReleaseTexture(OwnedIconAtlas);
                OwnedIconAtlas = null;
            }
            IsDisposed = true;
        }
    }
}
