namespace helengine {
    /// <summary>
    /// Represents runtime material data that may either own concrete GPU resources directly or inherit from a parent material.
    /// </summary>
    public class RuntimeMaterial : RuntimeData {
        /// <summary>
        /// Child materials that inherit layout and render-state values from this material.
        /// </summary>
        readonly List<RuntimeMaterial> ChildMaterialsValue;
        /// <summary>
        /// Parent material that this material inherits from, when this material acts as an override instance.
        /// </summary>
        RuntimeMaterial ParentMaterialValue;

        /// <summary>
        /// Initializes a new runtime material with an empty layout, default render state, and an empty property block.
        /// </summary>
        public RuntimeMaterial() {
            Layout = MaterialLayout.Empty;
            RenderState = new MaterialRenderState();
            Properties = new MaterialPropertyBlock(Layout);
            ChildMaterialsValue = new List<RuntimeMaterial>();
            LightingModel = RuntimeMaterialLightingModel.Unlit;
            SupportsNormalMapping = false;
            SupportsEmissive = false;
        }

        /// <summary>
        /// Gets the resolved material layout that describes the shader bindings exposed by this material.
        /// </summary>
        public MaterialLayout Layout { get; private set; }

        /// <summary>
        /// Gets the fixed-function render state used while drawing the material.
        /// </summary>
        public MaterialRenderState RenderState { get; private set; }

        /// <summary>
        /// Gets the material-scoped property block that stores texture and constant-buffer values for the layout.
        /// </summary>
        public MaterialPropertyBlock Properties { get; private set; }

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
        /// Gets the parent material whose layout, render state, and default values are inherited by this material.
        /// </summary>
        public RuntimeMaterial ParentMaterial => ParentMaterialValue;

        /// <summary>
        /// Replaces the resolved material layout and recreates the property block to match its bindings.
        /// </summary>
        /// <param name="layout">Resolved material layout for the runtime material.</param>
        public void SetLayout(MaterialLayout layout) {
            if (layout == null) {
                throw new ArgumentNullException(nameof(layout));
            } else if (ParentMaterialValue != null) {
                throw new InvalidOperationException("Parented runtime materials inherit their layout from the parent material.");
            }

            ApplyResolvedLayout(layout);
            SynchronizeChildMaterials();
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
        /// Resolves the runtime texture that should be sampled for this material after applying inherited defaults.
        /// </summary>
        /// <returns>Resolved runtime texture for the draw.</returns>
        public RuntimeTexture ResolveTexture() {
            if (Properties.TryGetFirstTexture(out RuntimeTexture propertyTexture)) {
                return propertyTexture;
            } else if (ParentMaterialValue != null) {
                return ParentMaterialValue.ResolveTexture();
            }

            throw new InvalidOperationException("Runtime material does not define a texture for the active material layout.");
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

            RenderState = renderState.Clone();
            SynchronizeChildMaterials();
        }

        /// <summary>
        /// Applies authored default constant-buffer payloads to the material property block.
        /// </summary>
        /// <param name="constantBuffers">Authored constant-buffer payloads keyed by shader binding name.</param>
        public void ApplyConstantBufferDefaults(MaterialConstantBufferAsset[] constantBuffers) {
            if (constantBuffers == null) {
                throw new ArgumentNullException(nameof(constantBuffers));
            }

            for (int constantBufferIndex = 0; constantBufferIndex < constantBuffers.Length; constantBufferIndex++) {
                MaterialConstantBufferAsset constantBuffer = constantBuffers[constantBufferIndex];
                if (constantBuffer == null) {
                    throw new InvalidOperationException("Material constant buffers contain a null entry.");
                }

                Properties.SetConstantBufferData(constantBuffer.Name, constantBuffer.Data);
            }
        }

        /// <summary>
        /// Applies one resolved layout to this material while preserving matching local values.
        /// </summary>
        /// <param name="layout">Resolved material layout to apply.</param>
        void ApplyResolvedLayout(MaterialLayout layout) {
            MaterialLayout previousLayout = Layout;
            MaterialPropertyBlock previousProperties = Properties;
            Layout = layout;
            Properties = new MaterialPropertyBlock(layout);
            RestoreTextureBindings(previousLayout, previousProperties);
            RestoreConstantBufferBindings(previousLayout, previousProperties);
        }

        /// <summary>
        /// Restores matching texture bindings when a material layout changes.
        /// </summary>
        /// <param name="previousLayout">Layout previously assigned to the runtime material.</param>
        /// <param name="previousProperties">Property values associated with the previous layout.</param>
        void RestoreTextureBindings(
            MaterialLayout previousLayout,
            MaterialPropertyBlock previousProperties) {
            if (Layout.TextureBindings.Length == 0) {
                return;
            } else if (previousLayout == null || previousProperties == null) {
                return;
            } else if (previousLayout.TextureBindings.Length == 0) {
                return;
            }

            for (int textureIndex = 0; textureIndex < Layout.TextureBindings.Length; textureIndex++) {
                MaterialLayoutBinding binding = Layout.TextureBindings[textureIndex];
                int previousBindingIndex = previousLayout.FindTextureBindingIndex(binding.Name);
                if (previousBindingIndex < 0) {
                    continue;
                }

                RuntimeTexture previousTexture = previousProperties.GetTexture(previousBindingIndex);
                if (previousTexture == null) {
                    continue;
                }

                Properties.SetTexture(textureIndex, previousTexture);
            }
        }

        /// <summary>
        /// Restores matching constant-buffer payloads when a material layout changes.
        /// </summary>
        /// <param name="previousLayout">Layout previously assigned to the runtime material.</param>
        /// <param name="previousProperties">Property values associated with the previous layout.</param>
        void RestoreConstantBufferBindings(MaterialLayout previousLayout, MaterialPropertyBlock previousProperties) {
            if (Layout.ConstantBufferBindings.Length == 0 || previousLayout == null || previousProperties == null) {
                return;
            } else if (previousLayout.ConstantBufferBindings.Length == 0) {
                return;
            }

            Properties.CopyMatchingValuesFrom(previousProperties);
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
        void SynchronizeWithParentMaterial() {
            if (ParentMaterialValue == null) {
                return;
            }

            if (!ReferenceEquals(Layout, ParentMaterialValue.Layout)) {
                ApplyResolvedLayout(ParentMaterialValue.Layout);
            }

            RenderState = ParentMaterialValue.RenderState.Clone();
            SynchronizeChildMaterials();
        }

        /// <summary>
        /// Synchronizes all registered child materials after this material changes.
        /// </summary>
        void SynchronizeChildMaterials() {
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
