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
        /// Gets or sets the legacy primary runtime material bound to slot zero.
        /// </summary>
        public RuntimeMaterial Material {
            get {
                return MaterialsBySlot.Length == 0 ? null : MaterialsBySlot[0];
            }
            set {
                if (MaterialsBySlot.Length == 0) {
                    MaterialsBySlot = value == null
                        ? Array.Empty<RuntimeMaterial>()
                        : new[] { value };
                    return;
                }

                RuntimeMaterial[] updatedMaterials = new RuntimeMaterial[MaterialsBySlot.Length];
                Array.Copy(MaterialsBySlot, updatedMaterials, MaterialsBySlot.Length);
                updatedMaterials[0] = value;
                MaterialsBySlot = updatedMaterials;
            }
        }

        /// <summary>
        /// Gets the runtime materials bound to each submesh slot.
        /// </summary>
        public RuntimeMaterial[] Materials => MaterialsBySlot;

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
            MaterialsBySlot = Array.Empty<RuntimeMaterial>();
        }

        /// <summary>
        /// Replaces the runtime materials bound to each submesh slot.
        /// </summary>
        /// <param name="runtimeMaterials">Ordered runtime materials by submesh slot.</param>
        public void SetMaterials(RuntimeMaterial[] runtimeMaterials) {
            if (runtimeMaterials == null) {
                throw new ArgumentNullException(nameof(runtimeMaterials));
            }

            MaterialsBySlot = new RuntimeMaterial[runtimeMaterials.Length];
            Array.Copy(runtimeMaterials, MaterialsBySlot, runtimeMaterials.Length);
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
