using System;
using System.Collections.Generic;

namespace helengine.editor {
    /// <summary>
    /// Displays a dockable scene hierarchy list for the current scene graph.
    /// </summary>
    public class SceneHierarchyPanel : DockableEntity {
        /// <summary>
        /// Height of each row in the hierarchy list.
        /// </summary>
        public const int RowHeight = 22;

        const int RowIndent = 14;
        const ushort SceneLayerMask = 0b0100000000000000;

        readonly FontAsset font;
        readonly EditorEntity contentRoot;
        readonly List<RowElements> rows;
        readonly List<NodeInfo> nodes;
        /// <summary>
        /// Render order used for row backgrounds.
        /// </summary>
        readonly byte rowBackgroundOrder;
        /// <summary>
        /// Render order used for row text.
        /// </summary>
        readonly byte rowTextOrder;

        /// <summary>
        /// Initializes a new scene hierarchy panel with the provided font.
        /// </summary>
        /// <param name="font">Font used for row labels.</param>
        public SceneHierarchyPanel(FontAsset font) : base(font) {
            this.font = font;
            Title = "Scene Hierarchy";
            MinSize = new int2(220, 160);

            rowBackgroundOrder = Core.Instance.ObjectManager.GetRenderOrderForLayer2D(1);
            rowTextOrder = Core.Instance.ObjectManager.GetRenderOrderForLayer2D(2);

            contentRoot = new EditorEntity();
            contentRoot.LayerMask = LayerMask;
            contentRoot.Position = new float3(0, TitleBarHeight, 0.05f);
            AddChild(contentRoot);

            rows = new List<RowElements>(32);
            nodes = new List<NodeInfo>(64);

            RefreshHierarchy();
        }

        /// <summary>
        /// Rebuilds the hierarchy view from the current object manager state.
        /// </summary>
        public void RefreshHierarchy() {
            if (nodes == null) {
                return;
            }

            var manager = Core.Instance?.ObjectManager;
            if (manager == null) {
                return;
            }

            nodes.Clear();

            List<Entity> all = manager.Entities;
            for (int i = 0; i < all.Count; i++) {
                Entity entity = all[i];
                if (!IsSceneEntity(entity)) {
                    continue;
                }

                if (entity.Parent == null || !IsSceneEntity(entity.Parent)) {
                    AppendHierarchy(entity, 0);
                }
            }

            LayoutRows();
        }

        /// <summary>
        /// Handles layout updates when the dockable size changes.
        /// </summary>
        protected override void OnSizeChanged() {
            base.OnSizeChanged();
            if (font == null || nodes == null || rows == null) {
                return;
            }

            LayoutRows();
        }

        /// <summary>
        /// Recursively flattens the scene hierarchy into the node list.
        /// </summary>
        /// <param name="entity">Current entity being visited.</param>
        /// <param name="depth">Depth in the hierarchy.</param>
        void AppendHierarchy(Entity entity, int depth) {
            nodes.Add(new NodeInfo(entity, depth));

            if (entity.Children == null) {
                return;
            }

            for (int i = 0; i < entity.Children.Count; i++) {
                Entity child = entity.Children[i];
                if (IsSceneEntity(child)) {
                    AppendHierarchy(child, depth + 1);
                }
            }
        }

        /// <summary>
        /// Lays out all rows based on the current node list and panel size.
        /// </summary>
        void LayoutRows() {
            EnsureRowCount(nodes.Count);

            float lineHeight = MathF.Max(font.LineHeight, 1f);

            for (int i = 0; i < rows.Count; i++) {
                RowElements row = rows[i];
                if (i >= nodes.Count) {
                    row.Entity.Enabled = false;
                    row.IsHovering = false;
                    row.IsPressed = false;
                    UpdateRowBackground(row, ThemeManager.Colors.SurfacePrimary);
                    continue;
                }

                NodeInfo node = nodes[i];
                row.Entity.Enabled = true;
                row.Entity.Position = new float3(0, i * RowHeight, 0.1f);

                bool alternate = i % 2 == 1;
                byte4 baseColor = alternate ? ThemeManager.Colors.SurfaceInput : ThemeManager.Colors.SurfacePrimary;
                row.BaseColor = baseColor;
                UpdateRowBackground(row, baseColor);

                int rowWidth = Math.Max(Size.X, MinSize.X);

                row.Background.Size = new int2(rowWidth, RowHeight);
                row.Interactable.Size = new int2(rowWidth, RowHeight);

                float indent = 8 + node.Depth * RowIndent;
                row.LabelHost.Position = new float3(indent, MathF.Round((RowHeight - lineHeight) * 0.5f), 0.2f);

                string label = node.Entity is EditorEntity editorEntity ? editorEntity.Name : node.Entity.GetType().Name;
                row.Label.Text = label;
                row.Label.Size = new int2(Math.Max(0, rowWidth - (int)indent), (int)MathF.Ceiling(lineHeight));
                row.Label.Color = ThemeManager.Colors.InputForegroundPrimary;
            }
        }

        /// <summary>
        /// Ensures the row pool has enough entries to render the current hierarchy.
        /// </summary>
        /// <param name="count">Required row count.</param>
        void EnsureRowCount(int count) {
            bool created = false;
            for (int i = rows.Count; i < count; i++) {
                rows.Add(CreateRow());
                created = true;
            }
            if (created) {
                RefreshRenderOrderBias();
            }
        }

        /// <summary>
        /// Creates a single row entity with background, label, and hover handling.
        /// </summary>
        /// <returns>Newly created row elements.</returns>
        RowElements CreateRow() {
            var rowEntity = new EditorEntity();
            rowEntity.LayerMask = LayerMask;
            rowEntity.Position = float3.Zero;

            var background = new SpriteComponent();
            background.Texture = TextureUtils.PixelTexture;
            background.Color = ThemeManager.Colors.SurfacePrimary;
            background.RenderOrder2D = rowBackgroundOrder;
            rowEntity.AddComponent(background);

            var interactable = new InteractableComponent();
            interactable.Size = new int2(Size.X, RowHeight);
            rowEntity.AddComponent(interactable);

            var labelHost = new EditorEntity();
            labelHost.LayerMask = LayerMask;
            labelHost.Position = new float3(8, 2, 0.2f);
            rowEntity.AddChild(labelHost);

            var text = new TextComponent();
            text.Font = font;
            text.Text = string.Empty;
            text.Color = ThemeManager.Colors.InputForegroundPrimary;
            text.Size = new int2(100, RowHeight);
            text.RenderOrder2D = rowTextOrder;
            labelHost.AddComponent(text);

            var row = new RowElements(rowEntity, background, labelHost, text, interactable);
            interactable.CursorEvent += (pos, delta, state) => HandleRowCursor(row, state);
            contentRoot.AddChild(rowEntity);
            return row;
        }

        /// <summary>
        /// Determines whether an entity should appear in the hierarchy.
        /// </summary>
        /// <param name="entity">Entity to evaluate.</param>
        /// <returns>True when the entity belongs to the scene layer.</returns>
        bool IsSceneEntity(Entity entity) {
            return (entity.LayerMask & SceneLayerMask) != 0;
        }

        /// <summary>
        /// Handles pointer interactions for a row to update its visual state.
        /// </summary>
        /// <param name="row">Row receiving the event.</param>
        /// <param name="state">Interaction state.</param>
        void HandleRowCursor(RowElements row, PointerInteraction state) {
            switch (state) {
                case PointerInteraction.Hover:
                    row.IsHovering = true;
                    break;
                case PointerInteraction.Press:
                    row.IsPressed = true;
                    break;
                case PointerInteraction.Release:
                    row.IsPressed = false;
                    break;
                case PointerInteraction.Leave:
                    row.IsHovering = false;
                    row.IsPressed = false;
                    break;
                default:
                    break;
            }

            UpdateRowBackground(row, row.BaseColor);
        }

        /// <summary>
        /// Updates a row background color based on hover and press state.
        /// </summary>
        /// <param name="row">Row to update.</param>
        /// <param name="baseColor">Base color for the row.</param>
        void UpdateRowBackground(RowElements row, byte4 baseColor) {
            if (row.IsPressed) {
                row.Background.Color = ThemeManager.Colors.AccentPrimary;
                return;
            }

            if (row.IsHovering) {
                row.Background.Color = ThemeManager.Colors.AccentSecondary;
                return;
            }

            row.Background.Color = baseColor;
        }

        /// <summary>
        /// Captures a flattened hierarchy node and its depth.
        /// </summary>
        readonly struct NodeInfo {
            /// <summary>
            /// Initializes a new node info instance.
            /// </summary>
            /// <param name="entity">Referenced entity.</param>
            /// <param name="depth">Depth within the hierarchy.</param>
            public NodeInfo(Entity entity, int depth) {
                Entity = entity;
                Depth = depth;
            }

            /// <summary>
            /// Gets the referenced entity.
            /// </summary>
            public Entity Entity { get; }

            /// <summary>
            /// Gets the depth within the hierarchy.
            /// </summary>
            public int Depth { get; }
        }

        /// <summary>
        /// Bundles row entities and interaction state for layout and input handling.
        /// </summary>
        sealed class RowElements {
            /// <summary>
            /// Initializes a new row bundle.
            /// </summary>
            /// <param name="entity">Root entity for the row.</param>
            /// <param name="background">Background sprite.</param>
            /// <param name="labelHost">Entity hosting the label text.</param>
            /// <param name="label">Text component for the label.</param>
            /// <param name="interactable">Interactable hit region.</param>
            public RowElements(
                EditorEntity entity,
                SpriteComponent background,
                EditorEntity labelHost,
                TextComponent label,
                InteractableComponent interactable) {

                Entity = entity;
                Background = background;
                LabelHost = labelHost;
                Label = label;
                Interactable = interactable;
                BaseColor = ThemeManager.Colors.SurfacePrimary;
            }

            /// <summary>
            /// Gets the row root entity.
            /// </summary>
            public EditorEntity Entity { get; }

            /// <summary>
            /// Gets the background sprite component.
            /// </summary>
            public SpriteComponent Background { get; }

            /// <summary>
            /// Gets the entity hosting the label component.
            /// </summary>
            public EditorEntity LabelHost { get; }

            /// <summary>
            /// Gets the text component for the row label.
            /// </summary>
            public TextComponent Label { get; }

            /// <summary>
            /// Gets the interactable region for input handling.
            /// </summary>
            public InteractableComponent Interactable { get; }

            /// <summary>
            /// Gets or sets the base color when not hovered or pressed.
            /// </summary>
            public byte4 BaseColor { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether the row is hovered.
            /// </summary>
            public bool IsHovering { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether the row is pressed.
            /// </summary>
            public bool IsPressed { get; set; }
        }
    }
}
