namespace helengine {
    /// <summary>
    /// Represents runtime material data for shader-backed renderers that expose texture and constant-buffer bindings.
    /// </summary>
    public class ShaderRuntimeMaterial : RuntimeMaterial {
        /// <summary>
        /// Resolved material layout that describes the bindings exposed by this material.
        /// </summary>
        MaterialLayout LayoutValue;

        /// <summary>
        /// Property values bound to this material.
        /// </summary>
        MaterialPropertyBlock PropertiesValue;

        /// <summary>
        /// Initializes a new shader runtime material with an empty layout and an empty property block.
        /// </summary>
        public ShaderRuntimeMaterial() {
            LayoutValue = MaterialLayout.Empty;
            PropertiesValue = new MaterialPropertyBlock(LayoutValue);
        }

        /// <summary>
        /// Gets the resolved material layout that describes the shader bindings exposed by this material.
        /// </summary>
        public MaterialLayout Layout => LayoutValue;

        /// <summary>
        /// Gets the material-scoped property block that stores texture and constant-buffer values for the layout.
        /// </summary>
        public MaterialPropertyBlock Properties => PropertiesValue;

        /// <summary>
        /// Releases shader-runtime-material-owned native resources and nested containers.
        /// </summary>
        public override void Dispose() {
            if (OwnsLayout(LayoutValue)) {
                NativeOwnership.DisposeAndDelete(LayoutValue);
            }

            LayoutValue = null;
            NativeOwnership.DisposeAndDelete(PropertiesValue);
            PropertiesValue = null;
            base.Dispose();
        }

        /// <summary>
        /// Replaces the resolved material layout and recreates the property block to match its bindings.
        /// </summary>
        /// <param name="layout">Resolved material layout for the shader runtime material.</param>
        public void SetLayout(MaterialLayout layout) {
            if (layout == null) {
                throw new ArgumentNullException(nameof(layout));
            } else if (ParentMaterial != null) {
                throw new InvalidOperationException("Parented shader runtime materials inherit their layout from the parent material.");
            }

            MaterialLayout previousOwnedLayout = ResolveOwnedLayout(Layout);
            ApplyResolvedLayout(layout);
            SynchronizeChildMaterials();
            DisposeOwnedLayout(previousOwnedLayout);
        }

        /// <summary>
        /// Resolves the runtime texture that should be sampled for this material after applying inherited defaults.
        /// </summary>
        /// <returns>Resolved runtime texture for the draw, or null when the active material layout has no assigned texture.</returns>
        public RuntimeTexture ResolveTexture() {
            if (Properties.TryGetFirstTexture(out RuntimeTexture propertyTexture)) {
                return propertyTexture;
            }

            ShaderRuntimeMaterial parentShaderMaterial = GetParentShaderMaterial();
            if (parentShaderMaterial != null) {
                return parentShaderMaterial.ResolveTexture();
            }

            return null;
        }

        /// <summary>
        /// Attempts to resolve one constant-buffer payload by binding name after applying parent-material inheritance.
        /// </summary>
        /// <param name="bindingName">Constant-buffer binding name to resolve.</param>
        /// <param name="data">Resolved copied payload when one is assigned.</param>
        /// <returns>True when a payload is assigned on this material chain; otherwise false.</returns>
        public bool TryResolveConstantBufferData(string bindingName, out byte[] data) {
            if (string.IsNullOrWhiteSpace(bindingName)) {
                data = null;
                return false;
            }

            int bindingIndex = Layout.FindConstantBufferBindingIndex(bindingName);
            if (bindingIndex >= 0) {
                byte[] localData = Properties.GetConstantBufferData(bindingIndex);
                if (localData != null) {
                    data = localData;
                    return true;
                }
            }

            ShaderRuntimeMaterial parentShaderMaterial = GetParentShaderMaterial();
            if (parentShaderMaterial != null) {
                return parentShaderMaterial.TryResolveConstantBufferData(bindingName, out data);
            }

            data = null;
            return false;
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
        /// Synchronizes this shader runtime material against its current parent material.
        /// </summary>
        protected override void SynchronizeWithParentMaterial() {
            ShaderRuntimeMaterial parentShaderMaterial = GetRequiredParentShaderMaterial();
            MaterialLayout previousOwnedLayout = null;
            if (parentShaderMaterial != null && !ReferenceEquals(Layout, parentShaderMaterial.Layout)) {
                previousOwnedLayout = ResolveOwnedLayout(Layout);
                ApplyResolvedLayout(parentShaderMaterial.Layout);
            }

            base.SynchronizeWithParentMaterial();
            DisposeOwnedLayout(previousOwnedLayout);
        }

        /// <summary>
        /// Applies one resolved layout to this material while preserving matching local values.
        /// </summary>
        /// <param name="layout">Resolved material layout to apply.</param>
        void ApplyResolvedLayout(MaterialLayout layout) {
            MaterialLayout previousLayout = Layout;
            MaterialPropertyBlock previousProperties = Properties;
            LayoutValue = layout;
            PropertiesValue = new MaterialPropertyBlock(layout);
            RestoreTextureBindings(previousLayout, previousProperties);
            RestoreConstantBufferBindings(previousLayout, previousProperties);
            NativeOwnership.DisposeAndDelete(previousProperties);
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
        /// Gets the parent shader runtime material when this material currently inherits from another shader runtime material.
        /// </summary>
        /// <returns>Parent shader runtime material when present; otherwise null.</returns>
        ShaderRuntimeMaterial GetParentShaderMaterial() {
            RuntimeMaterial parentMaterial = ParentMaterial;
            if (parentMaterial == null) {
                return null;
            }

            if (parentMaterial is ShaderRuntimeMaterial parentShaderMaterial) {
                return parentShaderMaterial;
            }

            return null;
        }

        /// <summary>
        /// Gets the parent shader runtime material and rejects non-shader parents for shader-backed material instances.
        /// </summary>
        /// <returns>Parent shader runtime material when present; otherwise null.</returns>
        ShaderRuntimeMaterial GetRequiredParentShaderMaterial() {
            RuntimeMaterial parentMaterial = ParentMaterial;
            if (parentMaterial == null) {
                return null;
            }

            if (parentMaterial is ShaderRuntimeMaterial parentShaderMaterial) {
                return parentShaderMaterial;
            }

            throw new InvalidOperationException("Shader runtime materials must inherit from other shader runtime materials.");
        }

        /// <summary>
        /// Determines whether this shader runtime material owns one resolved layout instance and may dispose it safely.
        /// </summary>
        /// <param name="layout">Layout instance to inspect.</param>
        /// <returns>True when the layout is owned by this material; otherwise false.</returns>
        bool OwnsLayout(MaterialLayout layout) {
            if (layout == null) {
                return false;
            } else if (ReferenceEquals(layout, MaterialLayout.Empty)) {
                return false;
            }

            ShaderRuntimeMaterial parentShaderMaterial = GetParentShaderMaterial();
            if (parentShaderMaterial != null && ReferenceEquals(layout, parentShaderMaterial.Layout)) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Resolves one previously assigned layout that this material owns and may dispose after child synchronization completes.
        /// </summary>
        /// <param name="layout">Previously assigned layout instance.</param>
        /// <returns>Owned layout instance when later disposal is required; otherwise null.</returns>
        MaterialLayout ResolveOwnedLayout(MaterialLayout layout) {
            if (!OwnsLayout(layout)) {
                return null;
            }

            return layout;
        }

        /// <summary>
        /// Releases one owned layout after all dependent child materials have synchronized away from it.
        /// </summary>
        /// <param name="layout">Owned layout instance to dispose.</param>
        void DisposeOwnedLayout(MaterialLayout layout) {
            if (layout == null) {
                return;
            }

            NativeOwnership.DisposeAndDelete(layout);
        }
    }
}
