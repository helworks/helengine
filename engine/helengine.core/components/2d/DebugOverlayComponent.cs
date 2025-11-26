using System.Text;

namespace helengine {
    public class DebugOverlayComponent : UpdateComponent {
        Entity bgEntity;
        Entity textEntity;
        RoundedRectComponent bg;
        TextComponent text;
        FontAsset font;
        bool initialized;
        KeyboardState lastKeyboard;

        public bool Visible { get; private set; } = false;
        public byte RenderOrder2D { get; set; } = 250;
        public int2 Padding { get; set; } = new int2(8, 6);
        public Keys ToggleKey { get; set; } = Keys.F8;

        public DebugOverlayComponent(FontAsset font) {
            this.font = font;
        }

        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            if (initialized) {
                return;
            }

            initialized = true;

            entity.InitChildren();
            bgEntity = new Entity();
            bgEntity.LayerMask = entity.LayerMask;
            bgEntity.InitComponents();
            entity.AddChild(bgEntity);

            bg = new RoundedRectComponent();
            bg.Size = new int2(200, 80);
            bg.Radius = 6f;
            bg.BorderThickness = 1f;
            bg.FillColor = new byte4(0, 0, 0, 160);
            bg.BorderColor = new byte4(255, 255, 255, 64);
            bg.RenderOrder2D = RenderOrder2D;
            bgEntity.AddComponent(bg);

            textEntity = new Entity();
            textEntity.LayerMask = entity.LayerMask;
            textEntity.InitComponents();
            entity.AddChild(textEntity);

            text = new TextComponent();
            text.Font = font;
            text.Color = new byte4(230, 230, 230, 255);
            text.RenderOrder2D = (byte)(RenderOrder2D + 1);
            textEntity.AddComponent(text);

            bgEntity.Enabled = false;
            textEntity.Enabled = false;
        }

        public override void Update() {
            if (!initialized) {
                return;
            }

            // Edge-triggered toggle on key press (not hold)
            var kb = Core.Instance.InputManager.Keyboard.GetState();
            bool pressed = kb.IsKeyDown(ToggleKey) && !lastKeyboard.IsKeyDown(ToggleKey);
            if (pressed) {
                Visible = !Visible;
            }
            lastKeyboard = kb;

            bgEntity.Enabled = Visible;
            textEntity.Enabled = Visible;

            if (!Visible) {
                return;
            }

            // Build info text
            var rows = DebugInfoRegistry.Snapshot();
            var sb = new StringBuilder(256);

            string current = null;
            for (int i = 0; i < rows.Count; i++) {
                var (cat, key, value) = rows[i];
                if (cat != current) {
                    if (current != null) sb.Append('\n');
                    sb.Append('[').Append(cat).Append(']').Append('\n');
                    current = cat;
                }
                sb.Append(key).Append(':').Append(' ').Append(value).Append('\n');
            }
            string textStr = sb.ToString().TrimEnd();
            text.Text = textStr;

            // Layout
            var lines = textStr.Split('\n');
            float maxW = 0f;
            for (int i = 0; i < lines.Length; i++) {
                var m = font.MeasureTight(lines[i]);
                if (m.Width > maxW) maxW = m.Width;
            }
            int w = (int)Math.Ceiling(maxW) + Padding.X * 2;
            int h = (int)Math.Ceiling(lines.Length * font.LineHeight) + Padding.Y * 2;

            bg.Size = new int2(w, h);
            textEntity.Position = new float3(Padding.X, Padding.Y, 0.1f);
        }
    }
}
