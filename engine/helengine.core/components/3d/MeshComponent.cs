namespace helengine {
    /// <summary>
    /// Renders a 3D mesh using the 3D render manager.
    /// </summary>
    public class MeshComponent : Component, IDrawable3D {
        byte renderOrder3D;
        RuntimeMaterial[] MaterialsBySlot;

        /// <summary>
        /// Gets or sets the runtime model to render.
        /// </summary>
        public RuntimeModel Model { get; set; }

        /// <summary>
        /// Gets or sets the runtime materials bound to each submesh slot.
        /// </summary>
        public RuntimeMaterial[] Materials {
            get { return MaterialsBySlot; }
            set {
                if (value == null) {
                    throw new ArgumentNullException(nameof(value));
                }

                RuntimeMaterial[] previousMaterials = MaterialsBySlot;
                MaterialsBySlot = new RuntimeMaterial[value.Length];
                Array.Copy(value, MaterialsBySlot, value.Length);
                NativeOwnership.Release(ref previousMaterials);
            }
        }

        /// <summary>
        /// Gets or sets the render order for this mesh.
        /// </summary>
        public byte RenderOrder3D {
            get { return renderOrder3D; }
            set {
                if (renderOrder3D != value) {
                    if (Parent != null && Parent.IsHierarchyEnabled) {
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
        /// Initializes a new mesh component.
        /// </summary>
        public MeshComponent() {
            MaterialsBySlot = new RuntimeMaterial[0];
        }

        /// <summary>
        /// Replaces the runtime materials bound to each submesh slot.
        /// </summary>
        /// <param name="runtimeMaterials">Ordered runtime materials by submesh slot.</param>
        public void SetMaterials(RuntimeMaterial[] runtimeMaterials) {
            Materials = runtimeMaterials;
        }

        /// <summary>
        /// Registers the mesh with the render manager when added to an enabled entity.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            if (entity.IsHierarchyEnabled) {
                Core.Instance.ObjectManager.RegisterForRender3D(this);
            }
        }

        /// <summary>
        /// Unregisters the mesh from render queues when the component is removed from its entity.
        /// </summary>
        /// <param name="entity">Entity losing this mesh component.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);
            Core.Instance.ObjectManager.RemoveFromRender3D(this);
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

        /// <summary>
        /// Releases the native material-slot array owned by this component without deleting shared material assets.
        /// </summary>
        public override void Dispose() {
            NativeOwnership.Release(ref MaterialsBySlot);
            Model = null;
            base.Dispose();
        }
    }
}
