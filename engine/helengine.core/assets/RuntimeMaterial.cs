namespace helengine {
    /// <summary>
    /// Represents runtime material data that may either own concrete platform resources directly or inherit generic render-state values from a parent material.
    /// </summary>
    public class RuntimeMaterial : RuntimeData, IDisposable {
        /// <summary>
        /// Child materials that inherit generic render-state values from this material.
        /// </summary>
        readonly List<RuntimeMaterial> ChildMaterialsValue;
        /// <summary>
        /// Parent material that this material inherits from, when this material acts as an override instance.
        /// </summary>
        RuntimeMaterial ParentMaterialValue;
        /// <summary>
        /// Fixed-function render state applied while drawing this material.
        /// </summary>
        MaterialRenderState RenderStateValue;

        /// <summary>
        /// Initializes a new runtime material with default render state and conservative lighting feature flags.
        /// </summary>
        public RuntimeMaterial() {
            RenderStateValue = new MaterialRenderState();
            ChildMaterialsValue = new List<RuntimeMaterial>();
            LightingModel = RuntimeMaterialLightingModel.Unlit;
            SupportsNormalMapping = false;
            SupportsEmissive = false;
            CastsShadows = true;
            ReceivesShadows = true;
        }

        /// <summary>
        /// Gets the fixed-function render state used while drawing the material.
        /// </summary>
        public MaterialRenderState RenderState {
            get => RenderStateValue;
            private set => RenderStateValue = value;
        }

        /// <summary>
        /// Gets or sets the lighting model expected by this runtime material.
        /// </summary>
        public RuntimeMaterialLightingModel LightingModel { get; set; }

        /// <summary>
        /// Gets or sets whether this runtime material binds a normal-map input.
        /// </summary>
        public bool SupportsNormalMapping { get; set; }

        /// <summary>
        /// Gets or sets whether this runtime material binds emissive inputs.
        /// </summary>
        public bool SupportsEmissive { get; set; }

        /// <summary>
        /// Gets or sets whether this runtime material contributes geometry to shadow-map passes.
        /// </summary>
        public bool CastsShadows { get; set; }

        /// <summary>
        /// Gets or sets whether this runtime material receives shadow attenuation during lighting.
        /// </summary>
        public bool ReceivesShadows { get; set; }

        /// <summary>
        /// Gets the parent material whose generic render-state defaults are inherited by this material.
        /// </summary>
        public RuntimeMaterial ParentMaterial => ParentMaterialValue;

        /// <summary>
        /// Releases runtime-material-owned native resources and nested containers.
        /// </summary>
        public virtual void Dispose() {
            RuntimeMaterial parentMaterial = ParentMaterialValue;
            if (parentMaterial != null) {
                parentMaterial.UnregisterChildMaterial(this);
            }

            ParentMaterialValue = null;
            NativeOwnership.Delete(RenderStateValue);
            RenderStateValue = null;
            ChildMaterialsValue.Clear();
            NativeOwnership.Delete(ChildMaterialsValue);
        }

        /// <summary>
        /// Binds this material to one parent material so it behaves like a reusable material instance.
        /// </summary>
        /// <param name="parentMaterial">Parent material whose layout and render state should be inherited.</param>
        public void SetParentMaterial(RuntimeMaterial parentMaterial) {
            if (parentMaterial == null) {
                throw new ArgumentNullException(nameof(parentMaterial));
            }

            if (ReferenceEquals(ParentMaterialValue, parentMaterial)) {
                return;
            }

            ValidateParentMaterial(parentMaterial);

            RuntimeMaterial previousParentMaterial = ParentMaterialValue;
            if (previousParentMaterial != null) {
                previousParentMaterial.UnregisterChildMaterial(this);
            }

            ParentMaterialValue = parentMaterial;
            parentMaterial.RegisterChildMaterial(this);
            SynchronizeWithParentMaterial();
        }

        /// <summary>
        /// Resolves the top-most material in the parent chain.
        /// </summary>
        /// <returns>Root material that owns the concrete shader resource for rendering.</returns>
        public RuntimeMaterial ResolveRootMaterial() {
            RuntimeMaterial material = this;
            while (material.ParentMaterialValue != null) {
                material = material.ParentMaterialValue;
            }

            return material;
        }

        /// <summary>
        /// Replaces the material render state with a copied instance so the runtime material owns its own values.
        /// </summary>
        /// <param name="renderState">Render state to apply to the runtime material.</param>
        public void SetRenderState(MaterialRenderState renderState) {
            if (renderState == null) {
                throw new ArgumentNullException(nameof(renderState));
            } else if (ParentMaterialValue != null) {
                throw new InvalidOperationException("Parented runtime materials inherit their render state from the parent material.");
            }

            MaterialRenderState previousRenderState = RenderState;
            RenderState = renderState.Clone();
            NativeOwnership.Delete(previousRenderState);
            SynchronizeChildMaterials();
        }

        /// <summary>
        /// Registers one child material so it stays synchronized with this material's layout and render-state changes.
        /// </summary>
        /// <param name="childMaterial">Child material that depends on this material.</param>
        void RegisterChildMaterial(RuntimeMaterial childMaterial) {
            if (childMaterial == null) {
                throw new ArgumentNullException(nameof(childMaterial));
            } else if (ChildMaterialsValue.Contains(childMaterial)) {
                throw new InvalidOperationException("Child materials cannot be registered to the same parent more than once.");
            }

            ChildMaterialsValue.Add(childMaterial);
        }

        /// <summary>
        /// Unregisters one child material from this material.
        /// </summary>
        /// <param name="childMaterial">Child material that no longer depends on this material.</param>
        void UnregisterChildMaterial(RuntimeMaterial childMaterial) {
            if (childMaterial == null) {
                throw new ArgumentNullException(nameof(childMaterial));
            }

            ChildMaterialsValue.Remove(childMaterial);
        }

        /// <summary>
        /// Validates that a parent-material assignment does not create a cycle.
        /// </summary>
        /// <param name="parentMaterial">Candidate parent material to validate.</param>
        void ValidateParentMaterial(RuntimeMaterial parentMaterial) {
            RuntimeMaterial currentMaterial = parentMaterial;
            while (currentMaterial != null) {
                if (ReferenceEquals(currentMaterial, this)) {
                    throw new InvalidOperationException("Runtime materials cannot inherit from themselves or from one of their children.");
                }

                currentMaterial = currentMaterial.ParentMaterialValue;
            }
        }

        /// <summary>
        /// Synchronizes this material against its current parent material.
        /// </summary>
        protected virtual void SynchronizeWithParentMaterial() {
            if (ParentMaterialValue == null) {
                return;
            }

            MaterialRenderState previousRenderState = RenderState;
            RenderState = ParentMaterialValue.RenderState.Clone();
            NativeOwnership.Delete(previousRenderState);
            SynchronizeChildMaterials();
        }

        /// <summary>
        /// Synchronizes all registered child materials after this material changes.
        /// </summary>
        protected void SynchronizeChildMaterials() {
            for (int childIndex = 0; childIndex < ChildMaterialsValue.Count; childIndex++) {
                RuntimeMaterial childMaterial = ChildMaterialsValue[childIndex];
                if (childMaterial == null) {
                    continue;
                }

                childMaterial.SynchronizeWithParentMaterial();
            }
        }
    }
}
