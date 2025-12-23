namespace helengine {
    /// <summary>
    /// Renders a 3D mesh using the 3D render manager.
    /// </summary>
    public class MeshComponent : Component, IDrawable3D {
        byte renderOrder3D;

        /// <summary>
        /// Gets or sets the runtime model to render.
        /// </summary>
        public RuntimeModel Model { get; set; }

        /// <summary>
        /// Gets or sets the render order bucket for this mesh.
        /// </summary>
        public byte RenderOrder3D {
            get { return renderOrder3D; }
            set {
                if (renderOrder3D != value) {
                    if (Parent.Enabled) {
                        Core.Instance.ObjectManager.RemoveFromRender3D(this);
                        renderOrder3D = value;
                        Core.Instance.ObjectManager.RegisterForRender3D(this);
                    } else {
                        renderOrder3D = value;
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the variant index used to choose a render pipeline.
        /// </summary>
        public byte Variant { get; set; }

        /// <summary>
        /// Initializes a new mesh component.
        /// </summary>
        public MeshComponent() {
        }

        /// <summary>
        /// Registers the mesh with the render manager when added to an enabled entity.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            if (entity.Enabled) {
                Core.Instance.ObjectManager.RegisterForRender3D(this);
            }
        }

        /// <summary>
        /// Registers or unregisters the mesh based on enabled state changes.
        /// </summary>
        /// <param name="newEnabled">New enabled state.</param>
        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (newEnabled) {
                Core.Instance.ObjectManager.RegisterForRender3D(this);
            } else {
                Core.Instance.ObjectManager.RemoveFromRender3D(this);
            }
        }
    }
}
