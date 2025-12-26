namespace helengine {
    /// <summary>
    /// Renders a textured quad using the 2D render manager.
    /// </summary>
    public class SpriteComponent : Component, ISpriteDrawable2D {
        byte renderOrder2D;

        /// <summary>
        /// Gets or sets the render order for this sprite.
        /// </summary>
        public byte RenderOrder2D {
            get { return renderOrder2D; }
            set {
                if (renderOrder2D != value) {
                    if (Parent != null && Parent.Enabled) {
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
        /// Gets or sets the runtime texture to draw.
        /// </summary>
        public RuntimeTexture Texture { get; set; }

        /// <summary>
        /// Gets or sets the rotation applied to the sprite.
        /// </summary>
        public float Rotation { get; set; }

        /// <summary>
        /// Gets or sets the layer mask used to filter cameras.
        /// </summary>
        public byte LayerMask { get; set; }

        /// <summary>
        /// Gets or sets the source rectangle sampled from the texture.
        /// </summary>
        public float4 SourceRect { get; set; }

        /// <summary>
        /// Gets or sets the destination size of the sprite.
        /// </summary>
        public int2 Size { get; set; }

        /// <summary>
        /// Gets or sets the color tint applied to the sprite.
        /// </summary>
        public byte4 Color { get; set; }

        /// <summary>
        /// Initializes a new sprite component with default source rect and color.
        /// </summary>
        public SpriteComponent() {
            SourceRect = new float4(0, 0, 1, 1);
            Color = new byte4(255, 255, 255, 255);
        }

        /// <summary>
        /// Registers the sprite with the render manager when added to an enabled entity.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            if (entity.Enabled) {
                Core.Instance.ObjectManager.RegisterForRender2D(this);
            }
        }

        /// <summary>
        /// Registers or unregisters the sprite based on enabled state changes.
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
        /// Issues a draw call for this sprite through the 2D render manager.
        /// </summary>
        public virtual void Draw() {
            Core.Instance.RenderManager2D.DrawSprite(this);
        }
    }
}
