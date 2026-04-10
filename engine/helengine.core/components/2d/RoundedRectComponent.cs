namespace helengine {
    /// <summary>
    /// Renders a rounded rectangle shape using the 2D render manager.
    /// </summary>
    public class RoundedRectComponent : Component, IRoundedRectDrawable2D {
        byte renderOrder2D;

        /// <summary>
        /// Gets or sets the render order for this shape.
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
        /// Gets or sets the layer mask used to filter cameras.
        /// </summary>
        public byte LayerMask { get; set; }

        /// <summary>
        /// Gets or sets the rotation applied to the shape.
        /// </summary>
        public float Rotation { get; set; }

        /// <summary>
        /// Gets or sets the fill color.
        /// </summary>
        public byte4 Color { get; set; }

        /// <summary>
        /// Gets or sets the texture source rectangle.
        /// </summary>
        public float4 SourceRect { get; set; }

        /// <summary>
        /// Gets or sets the destination size.
        /// </summary>
        public int2 Size { get; set; }

        /// <summary>
        /// Gets or sets the corner radius.
        /// </summary>
        public float Radius { get; set; }

        /// <summary>
        /// Gets or sets the border thickness.
        /// </summary>
        public float BorderThickness { get; set; }

        /// <summary>
        /// Gets or sets the fill color applied to the shape interior.
        /// </summary>
        public byte4 FillColor { get; set; }

        /// <summary>
        /// Gets or sets the border color applied to the outline.
        /// </summary>
        public byte4 BorderColor { get; set; }

        /// <summary>
        /// Initializes a new rounded rectangle with default styling.
        /// </summary>
        public RoundedRectComponent() {
            Size = new int2(64, 32);
            SourceRect = new float4(0, 0, 1, 1);
            Color = new byte4(255, 255, 255, 255);
            Radius = 8f;
            BorderThickness = 0f;
            FillColor = new byte4(255, 255, 255, 255);
            BorderColor = new byte4(0, 0, 0, 255);
        }

        /// <summary>
        /// Registers the shape with the render manager when added to an enabled entity.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            if (entity.IsHierarchyEnabled) {
                Core.Instance.ObjectManager.RegisterForRender2D(this);
            }
        }

        /// <summary>
        /// Registers or unregisters the shape based on enabled state changes.
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
        /// Issues a draw call for this shape through the 2D render manager.
        /// </summary>
        public virtual void Draw() {
            Core.Instance.RenderManager2D.DrawRoundedRect(this);
        }
    }
}
