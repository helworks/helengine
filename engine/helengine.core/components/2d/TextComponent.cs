namespace helengine {
    /// <summary>
    /// Renders text using a provided font asset via the 2D render manager.
    /// </summary>
    public class TextComponent : Component, ITextDrawable2D {
        byte renderOrder2D;

        /// <summary>
        /// Gets or sets the render order for this text drawable.
        /// </summary>
        public byte RenderOrder2D {
            get { return renderOrder2D; }
            set {
                if (renderOrder2D != value) {
                    if (Parent != null && Parent.IsHierarchyEnabled) {
                        Core.Instance.ObjectManager.RemoveFromRender2D(this);
                        renderOrder2D = value;
                        Core.Instance.ObjectManager.RegisterForRender2D(this);
                    } else {
                        renderOrder2D = value;
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets an optional pre-rendered texture backing this text.
        /// </summary>
        public RuntimeTexture Texture { get; set; }

        /// <summary>
        /// Gets or sets the rotation applied during rendering.
        /// </summary>
        public float Rotation { get; set; }

        /// <summary>
        /// Gets or sets the source rectangle within the backing texture.
        /// </summary>
        public float4 SourceRect { get; set; }

        /// <summary>
        /// Gets or sets the layout size of the rendered text.
        /// </summary>
        public int2 Size { get; set; }

        /// <summary>
        /// Gets or sets the color tint applied to the glyphs.
        /// </summary>
        public byte4 Color { get; set; }

        /// <summary>
        /// Gets or sets the text content to render.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Gets or sets the font asset used for rendering.
        /// </summary>
        public FontAsset Font { get; set; }

        /// <summary>
        /// Gets or sets the layer mask used to filter cameras.
        /// </summary>
        public byte LayerMask { get; set; }

        /// <summary>
        /// Initializes a new text component with default values.
        /// </summary>
        public TextComponent() {
            Text = "";
            Color = new byte4(255, 255, 255, 255);
            SourceRect = new float4(0, 0, 1, 1);
        }

        /// <summary>
        /// Registers the text drawable when added to an enabled entity.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            if (entity.IsHierarchyEnabled) {
                Core.Instance.ObjectManager.RegisterForRender2D(this);
            }
        }

        /// <summary>
        /// Registers or unregisters the text based on enabled state changes.
        /// </summary>
        /// <param name="newEnabled">New enabled state.</param>
        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (newEnabled) {
                Core.Instance.ObjectManager.RegisterForRender2D(this);
            } else {
                Core.Instance.ObjectManager.RemoveFromRender2D(this);
            }
        }

        /// <summary>
        /// Issues a draw call for this text through the 2D render manager.
        /// </summary>
        public virtual void Draw() {
            if (Font == null) {
                return;
            }

            Core.Instance.RenderManager2D.DrawText(this);
        }
    }
}
