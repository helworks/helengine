using System.Text;

namespace helengine {
    /// <summary>
    /// Renders a toggleable debug overlay showing registered debug info categories.
    /// </summary>
    public class DebugOverlayComponent : UpdateComponent {
        Entity BgEntity;
        Entity TextEntity;
        RoundedRectComponent Bg;
        TextComponent Text;
        FontAsset Font;
        bool Initialized;

        /// <summary>
        /// Gets a value indicating whether the overlay is currently visible.
        /// </summary>
        public bool Visible { get; private set; } = false;

        /// <summary>
        /// Gets or sets the render order used by both background and text.
        /// </summary>
        public byte RenderOrder2D { get; set; } = 250;

        /// <summary>
        /// Gets or sets padding around the text in pixels.
        /// </summary>
        public int2 Padding { get; set; } = new int2(8, 6);

        /// <summary>
        /// Gets or sets the key used to toggle overlay visibility.
        /// </summary>
#if DESKTOP_PLATFORM
        public Keys ToggleKey { get; set; } = Keys.F8;
#endif

        /// <summary>
        /// Creates a debug overlay that renders using the provided font.
        /// </summary>
        /// <param name="font">Font used for overlay text.</param>
        public DebugOverlayComponent(FontAsset font) {
            this.Font = font;
        }

        /// <summary>
        /// Initializes child entities and components when added to an entity.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            if (Initialized) {
                return;
            }

            Initialized = true;

            entity.InitChildren();
            BgEntity = new Entity(OwnerCore ?? throw new InvalidOperationException("Debug overlay requires an owning core."));
            BgEntity.LayerMask = entity.LayerMask;
            BgEntity.InitComponents();
            entity.AddChild(BgEntity);

            Bg = new RoundedRectComponent();
            Bg.Size = new int2(200, 80);
            Bg.Radius = 6f;
            Bg.BorderThickness = 1f;
            Bg.FillColor = new byte4(0, 0, 0, 160);
            Bg.BorderColor = new byte4(255, 255, 255, 64);
            Bg.RenderOrder2D = RenderOrder2D;
            BgEntity.AddComponent(Bg);

            TextEntity = new Entity(OwnerCore ?? throw new InvalidOperationException("Debug overlay requires an owning core."));
            TextEntity.LayerMask = entity.LayerMask;
            TextEntity.InitComponents();
            entity.AddChild(TextEntity);

            Text = new TextComponent();
            Text.Font = Font;
            Text.Color = new byte4(230, 230, 230, 255);
            Text.RenderOrder2D = (byte)(RenderOrder2D + 1);
            TextEntity.AddComponent(Text);

            BgEntity.Enabled = false;
            TextEntity.Enabled = false;
        }

        /// <summary>
        /// Updates overlay visibility, handles input toggle, and rebuilds text.
        /// </summary>
        public override void Update() {
            if (!Initialized) {
                return;
            }

#if DESKTOP_PLATFORM
            // Edge-triggered toggle on key press (not hold)
            InputSystem inputManager = OwnerCore.Input;
            bool pressed = inputManager.WasKeyPressed(ToggleKey);
            if (pressed) {
                Visible = !Visible;
            }
#endif

            BgEntity.Enabled = Visible;
            TextEntity.Enabled = Visible;

            if (!Visible) {
                return;
            }

            List<DebugInfoEntry> rows = DebugInfoRegistry.Snapshot();
            StringBuilder sb = new StringBuilder(256);
            string current = string.Empty;
            float maxW = 0f;
            int lineCount = 0;

            for (int i = 0; i < rows.Count; i++) {
                DebugInfoEntry row = rows[i];
                string cat = row.Category;
                string key = row.Key;
                string value = row.Value;
                if (cat != current) {
                    if (!string.IsNullOrEmpty(current)) {
                        sb.Append('\n');
                    }

                    string headerLine = "[" + cat + "]";
                    string headerLineWithBreak = headerLine + "\n";
                    sb.Append(headerLineWithBreak);
                    FontTightMetrics headerMetrics = Font.MeasureTight(headerLine);
                    if (headerMetrics.Width > maxW) {
                        maxW = headerMetrics.Width;
                    }
                    lineCount++;
                    current = cat;
                }

                string valueLine = key + ": " + value;
                sb.Append(valueLine);
                if (i + 1 < rows.Count) {
                    sb.Append('\n');
                }

                FontTightMetrics valueMetrics = Font.MeasureTight(valueLine);
                if (valueMetrics.Width > maxW) {
                    maxW = valueMetrics.Width;
                }
                lineCount++;
            }

            string textStr = sb.ToString();
            Text.Text = textStr;

            if (lineCount == 0) {
                lineCount = 1;
            }

            int w = (int)Math.Ceiling(maxW) + Padding.X * 2;
            int h = (int)Math.Ceiling(lineCount * Font.LineHeight) + Padding.Y * 2;

            Bg.Size = new int2(w, h);
            TextEntity.Position = new float3(Padding.X, Padding.Y, 0.1f);
        }
    }
}


