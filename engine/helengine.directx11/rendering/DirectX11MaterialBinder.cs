using SharpDX.Direct3D11;
using Buffer = SharpDX.Direct3D11.Buffer;
using D3DDevice = SharpDX.Direct3D11.Device;

namespace helengine.directx11 {
    /// <summary>
    /// Binds runtime materials to the DirectX11 pipeline: it resolves the concrete DirectX11 material, shader resources, texture views and constant-buffer payloads behind a runtime material chain, and remembers which material and which pixel-shader texture slots are currently bound so redundant work and stale bindings are avoided.
    /// </summary>
    public class DirectX11MaterialBinder : IDisposable {
        /// <summary>
        /// Device used to create the shared sampler and immutable material constant buffers.
        /// </summary>
        readonly D3DDevice Device;

        /// <summary>
        /// Pipeline state cache that applies the fixed-function state a material declares.
        /// </summary>
        readonly DirectX11PipelineStateCache PipelineStateCache;

        /// <summary>
        /// Sampler state shared by textured 3D materials.
        /// </summary>
        readonly SamplerState MaterialTextureSampler;

        /// <summary>
        /// Prepared material bindings and shared immutable constant payload resources owned by this renderer.
        /// </summary>
        readonly DirectX11MaterialBindingCache<Buffer> MaterialBindings;

        /// <summary>Shader identity last applied, checked separately from material identity during hot reload.</summary>
        DirectX11ShaderResource ActiveShaderResource;

        /// <summary>
        /// Tracks the pixel-shader texture slots most recently assigned by material bindings so stale shader-resource views can be cleared deterministically.
        /// </summary>
        readonly List<int> ActiveMaterialTextureSlots;

        /// <summary>
        /// Tracks the material currently bound to the pipeline for the active pass.
        /// </summary>
        DirectX11MaterialResource ActiveMaterial;

        /// <summary>
        /// Initializes the binder against the device and pipeline state cache it binds through.
        /// </summary>
        /// <param name="device">Device used to create the sampler and constant buffers.</param>
        /// <param name="pipelineStateCache">Cache that applies material-declared fixed-function state.</param>
        public DirectX11MaterialBinder(D3DDevice device, DirectX11PipelineStateCache pipelineStateCache) {
            if (device == null) {
                throw new ArgumentNullException(nameof(device));
            }

            if (pipelineStateCache == null) {
                throw new ArgumentNullException(nameof(pipelineStateCache));
            }

            Device = device;
            PipelineStateCache = pipelineStateCache;
            MaterialBindings = new DirectX11MaterialBindingCache<Buffer>(CreateMaterialConstantBuffer);
            ActiveMaterialTextureSlots = new List<int>();
            MaterialTextureSampler = CreateMaterialTextureSampler();
        }

        /// <summary>
        /// Returns whether one DirectX11 material is the material currently bound to the pipeline.
        /// </summary>
        /// <param name="material">Material to compare against the bound material.</param>
        /// <returns>True when the supplied material is the active one.</returns>
        public bool IsActiveMaterial(DirectX11MaterialResource material) {
            return ReferenceEquals(ActiveMaterial, material);
        }

        /// <summary>
        /// Clears the bound texture slots and forgets the active material so the next draw rebinds everything.
        /// </summary>
        public void ResetActiveMaterial() {
            ClearActiveMaterialTextureBindings();
            ActiveMaterial = null;
            ActiveShaderResource = null;
        }

        /// <summary>
        /// Clears every pixel-shader texture slot that was populated by the previously applied material bindings.
        /// </summary>
        public void ClearActiveMaterialTextureBindings() {
            DeviceContext context = Device.ImmediateContext;
            for (int bindingIndex = 0; bindingIndex < ActiveMaterialTextureSlots.Count; bindingIndex++) {
                int slot = ActiveMaterialTextureSlots[bindingIndex];
                context.PixelShader.SetShaderResource(slot, null);
                context.PixelShader.SetSampler(slot, null);
            }

            ActiveMaterialTextureSlots.Clear();
        }

        /// <summary>
        /// Applies a material to the DirectX11 pipeline if it is not already active.
        /// </summary>
        /// <param name="shaderMaterial">Concrete DirectX11 material that owns the shader resources.</param>
        /// <param name="material">Resolved runtime material instance that provides render-state and texture values.</param>
        public void ApplyMaterial(DirectX11MaterialResource shaderMaterial, ShaderRuntimeMaterial material) {
            if (shaderMaterial == null) {
                throw new ArgumentNullException(nameof(shaderMaterial));
            }

            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            DirectX11ShaderResource shaderResource = shaderMaterial.ShaderResource;
            DeviceContext context = Device.ImmediateContext;
            if (!ReferenceEquals(ActiveMaterial, shaderMaterial) || !ReferenceEquals(ActiveShaderResource, shaderResource)) {
                ClearActiveMaterialTextureBindings();

                context.InputAssembler.InputLayout = shaderResource.InputLayout;
                context.VertexShader.Set(shaderResource.VertexShader);
                context.PixelShader.Set(shaderResource.PixelShader);
                ActiveMaterial = shaderMaterial;
                ActiveShaderResource = shaderResource;
            }

            PipelineStateCache.ApplyMaterialRenderState(context, material.RenderState);
            DirectX11PreparedMaterial<Buffer> prepared = MaterialBindings.Get(material);
            ApplyPreparedConstantBuffers(prepared);
            ClearActiveMaterialTextureBindings();
            foreach (DirectX11PreparedTexture binding in prepared.Textures) {
                int slot = ResolveDirectX11BindingSlot(binding.Binding);
                context.PixelShader.SetShaderResource(slot, ResolveTextureResourceView(binding.Texture));
                context.PixelShader.SetSampler(slot, MaterialTextureSampler);
                TrackActiveMaterialTextureSlot(slot);
            }
        }

        /// <summary>
        /// Binds the visible material's culling and primary alpha texture without replacing the custom pass shaders or ID output state.
        /// </summary>
        /// <param name="material">Material assigned to this submesh, or null for untextured default geometry.</param>
        /// <returns>True when the custom shader should reject transparent pixels from the primary texture.</returns>
        public bool BindCustomPassMaterial(RuntimeMaterial material) {
            DeviceContext context = Device.ImmediateContext;
            ClearActiveMaterialTextureBindings();
            ActiveMaterial = null;
            ActiveShaderResource = null;
            ShaderResourceView resourceView = null;
            if (material == null) {
                context.Rasterizer.State = PipelineStateCache.DefaultRasterizerState;
            } else {
                context.Rasterizer.State = PipelineStateCache.ResolveRasterizerState(material.RenderState);
                if (material.RenderState.BlendMode == MaterialBlendMode.AlphaBlend) {
                    resourceView = ResolveMaterialTextureResourceView(RequireShaderRuntimeMaterial(material));
                }
            }
            context.PixelShader.SetShaderResource(0, resourceView);
            context.PixelShader.SetSampler(0, resourceView == null ? null : MaterialTextureSampler);
            TrackActiveMaterialTextureSlot(0);
            return resourceView != null;
        }

        /// <summary>
        /// Binds retained per-material constant buffers, preparing and uploading only new payloads.
        /// </summary>
        /// <param name="material">Resolved runtime material instance that provides constant-buffer values.</param>
        public void ApplyMaterialConstantBufferBindings(ShaderRuntimeMaterial material) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            ApplyPreparedConstantBuffers(MaterialBindings.Get(material));
        }

        /// <summary>Binds retained immutable constants, explicitly unbinding missing properties while leaving engine-owned slots untouched.</summary>
        void ApplyPreparedConstantBuffers(DirectX11PreparedMaterial<Buffer> prepared) {
            DeviceContext context = Device.ImmediateContext;
            foreach (DirectX11PreparedConstant<Buffer> binding in prepared.Constants) {
                Buffer buffer = binding.Content?.Resource;
                context.VertexShader.SetConstantBuffer(binding.Slot, buffer);
                context.PixelShader.SetConstantBuffer(binding.Slot, buffer);
            }
        }

        /// <summary>Releases a material's prepared state and all descendants that retained inherited values.</summary>
        public void ReleaseMaterial(RuntimeMaterial material) {
            MaterialBindings.Release(material);
            ResetActiveMaterial();
        }

        /// <summary>
        /// Resolves the material constant-buffer payloads that should be uploaded for one draw.
        /// </summary>
        /// <param name="material">Resolved runtime material instance that provides constant-buffer values.</param>
        /// <returns>Resolved DirectX11 constant-buffer payloads keyed by their shader slots.</returns>
        internal List<DirectX11MaterialConstantBufferBinding> ResolveMaterialConstantBufferBindings(ShaderRuntimeMaterial material) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            List<DirectX11MaterialConstantBufferBinding> resolvedBindings = new List<DirectX11MaterialConstantBufferBinding>();
            MaterialLayoutBindings layoutBindings = material.Layout.ConstantBufferBindings;
            for (int bindingIndex = 0; bindingIndex < layoutBindings.Length; bindingIndex++) {
                MaterialLayoutBinding binding = layoutBindings[bindingIndex];
                if (binding == null || IsEngineManagedConstantBufferBinding(binding.Name)) {
                    continue;
                }
                if (!material.TryResolveConstantBufferData(binding.Name, out byte[] data)) {
                    continue;
                }

                resolvedBindings.Add(new DirectX11MaterialConstantBufferBinding(binding.Name, binding.Slot, data));
            }

            return resolvedBindings;
        }

        /// <summary>
        /// Resolves the material texture bindings that should be uploaded for one draw.
        /// </summary>
        /// <param name="material">Resolved runtime material instance that provides texture values.</param>
        /// <returns>Resolved DirectX11 texture bindings keyed by their shader slots.</returns>
        internal List<DirectX11MaterialTextureBinding> ResolveMaterialTextureBindings(ShaderRuntimeMaterial material) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            List<DirectX11MaterialTextureBinding> resolvedBindings = new List<DirectX11MaterialTextureBinding>();
            MaterialLayoutBindings layoutBindings = material.Layout.TextureBindings;
            for (int bindingIndex = 0; bindingIndex < layoutBindings.Length; bindingIndex++) {
                MaterialLayoutBinding binding = layoutBindings[bindingIndex];
                if (binding == null || !TryResolveMaterialTexture(material, binding.Name, out RuntimeTexture runtimeTexture)) {
                    continue;
                }

                resolvedBindings.Add(new DirectX11MaterialTextureBinding(ResolveDirectX11BindingSlot(binding), ResolveTextureResourceView(runtimeTexture)));
            }

            return resolvedBindings;
        }

        /// <summary>
        /// Resolves the DirectX11 root material that owns the concrete shader resource for one runtime material chain.
        /// </summary>
        /// <param name="runtimeMaterial">Runtime material whose root should be resolved.</param>
        /// <returns>Resolved DirectX11 root material.</returns>
        public DirectX11MaterialResource ResolveDirectX11Material(ShaderRuntimeMaterial runtimeMaterial) {
            if (runtimeMaterial == null) {
                throw new ArgumentNullException(nameof(runtimeMaterial));
            }

            RuntimeMaterial rootMaterial = runtimeMaterial.ResolveRootMaterial();
            if (rootMaterial is not DirectX11MaterialResource directX11Material) {
                throw new InvalidOperationException("Drawable materials must resolve to DirectX11MaterialResource through their parent chain.");
            }

            return directX11Material;
        }

        /// <summary>
        /// Determines whether one runtime material chain should contribute geometry to DirectX11 shadow-map passes.
        /// </summary>
        /// <param name="runtimeMaterial">Runtime material assigned to the drawable, or <c>null</c> for the missing-material path.</param>
        /// <returns>True when the DirectX11 shadow passes should render the drawable.</returns>
        public bool ShouldMaterialCastShadows(RuntimeMaterial runtimeMaterial) {
            if (runtimeMaterial == null) {
                return true;
            }

            return ResolveDirectX11Material(RequireShaderRuntimeMaterial(runtimeMaterial)).CastsShadows;
        }

        /// <summary>
        /// Resolves the shader resource view sampled by a textured 3D material.
        /// </summary>
        /// <param name="material">Material whose texture binding should be resolved.</param>
        /// <returns>Shader resource view to bind for the material, or null when the material intentionally has no texture.</returns>
        public ShaderResourceView ResolveMaterialTextureResourceView(ShaderRuntimeMaterial material) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            RuntimeTexture runtimeTexture = material.ResolveTexture();
            if (runtimeTexture == null) {
                return null;
            }

            return ResolveTextureResourceView(runtimeTexture);
        }

        /// <summary>
        /// Resolves the shader resource view sampled by one runtime texture instance.
        /// </summary>
        /// <param name="runtimeTexture">Texture whose DirectX11 shader resource view should be resolved.</param>
        /// <returns>Shader resource view to bind for the texture.</returns>
        public ShaderResourceView ResolveTextureResourceView(RuntimeTexture runtimeTexture) {
            if (runtimeTexture == null) {
                throw new ArgumentNullException(nameof(runtimeTexture));
            }

            if (runtimeTexture.IsDisposed) {
                throw new ObjectDisposedException(nameof(runtimeTexture));
            }

            if (runtimeTexture is DirectX11TextureResource textureResource) {
                if (textureResource.Resource == null || textureResource.Resource.IsDisposed) {
                    throw new InvalidOperationException("DirectX11 texture resources must expose a shader resource view.");
                }

                return textureResource.Resource;
            } else if (runtimeTexture is DirectX11RenderTargetResource renderTargetResource) {
                if (renderTargetResource.ShaderResourceView == null || renderTargetResource.ShaderResourceView.IsDisposed) {
                    throw new InvalidOperationException("DirectX11 render targets used as material textures must expose a shader resource view.");
                }

                return renderTargetResource.ShaderResourceView;
            }

            throw new InvalidOperationException("3D material textures must be DirectX11 texture resources.");
        }

        /// <summary>
        /// Requires one resolved runtime material to expose shader-runtime binding state for the DirectX11 backend.
        /// </summary>
        /// <param name="runtimeMaterial">Runtime material instance to validate.</param>
        /// <returns>Shader runtime material view over the supplied material.</returns>
        public ShaderRuntimeMaterial RequireShaderRuntimeMaterial(RuntimeMaterial runtimeMaterial) {
            if (runtimeMaterial == null) {
                throw new ArgumentNullException(nameof(runtimeMaterial));
            }
            if (runtimeMaterial is not ShaderRuntimeMaterial shaderRuntimeMaterial) {
                throw new InvalidOperationException("DirectX11 rendering requires shader-backed runtime materials.");
            }

            return shaderRuntimeMaterial;
        }

        /// <summary>
        /// Releases the shared texture sampler, prepared material entries and all pooled constant buffers.
        /// </summary>
        public void Dispose() {
            MaterialBindings.Dispose();
            MaterialTextureSampler.Dispose();
            ActiveMaterialTextureSlots.Clear();
            ActiveMaterial = null;
            ActiveShaderResource = null;
        }

        /// <summary>
        /// Resolves the native DirectX11 register slot for one unified material-layout binding.
        /// </summary>
        /// <param name="binding">Material-layout binding whose slot should be mapped for the DirectX11 backend.</param>
        /// <returns>Native DirectX11 register slot used when binding the resource.</returns>
        [NativeMigrationRequired(
            "windows.native_directx_renderer",
            "Changes to managed DirectX11 material-slot remapping must also be applied to the Windows native DirectX renderer implementation.")]
        static int ResolveDirectX11BindingSlot(MaterialLayoutBinding binding) {
            if (binding == null) {
                throw new ArgumentNullException(nameof(binding));
            }

            ShaderBindingPolicy policy = ShaderBindingPolicies.Default;
            int shift = GetBindingShift(policy, binding.ResourceType);
            if (shift <= 0 || binding.Slot < shift) {
                return binding.Slot;
            }

            return binding.Slot - shift;
        }

        /// <summary>
        /// Gets the unified-slot shift used by the shared shader binding policy for one resource class.
        /// </summary>
        /// <param name="policy">Binding policy that defines unified resource-class shifts.</param>
        /// <param name="resourceType">Resource class whose shift should be resolved.</param>
        /// <returns>Unified-slot shift for the resource class.</returns>
        static int GetBindingShift(ShaderBindingPolicy policy, ShaderResourceType resourceType) {
            if (policy == null) {
                throw new ArgumentNullException(nameof(policy));
            }

            switch (resourceType) {
                case ShaderResourceType.Texture2D:
                case ShaderResourceType.TextureCube:
                    return policy.TextureShift;
                case ShaderResourceType.Sampler:
                    return policy.SamplerShift;
                case ShaderResourceType.Buffer:
                case ShaderResourceType.StorageBuffer:
                case ShaderResourceType.StorageTexture2D:
                    return policy.StorageShift;
                default:
                    return policy.ConstantBufferShift;
            }
        }

        /// <summary>
        /// Determines whether one shader constant-buffer binding is owned by the renderer rather than by runtime material properties.
        /// </summary>
        /// <param name="bindingName">Shader constant-buffer binding name to classify.</param>
        /// <returns>True when the renderer manages the binding for the active pass; otherwise false.</returns>
        static bool IsEngineManagedConstantBufferBinding(string bindingName) {
            if (string.IsNullOrWhiteSpace(bindingName)) {
                return false;
            }

            return string.Equals(bindingName, "TransformBuffer", StringComparison.Ordinal)
                || string.Equals(bindingName, "ForwardLightBuffer", StringComparison.Ordinal)
                || string.Equals(bindingName, "ShadowBuffer", StringComparison.Ordinal);
        }

        /// <summary>
        /// Records one pixel-shader texture slot that is now owned by the active material bindings.
        /// </summary>
        /// <param name="slot">Pixel-shader texture slot that was populated for the current draw.</param>
        void TrackActiveMaterialTextureSlot(int slot) {
            if (slot < 0) {
                throw new ArgumentOutOfRangeException(nameof(slot), "Material texture slots cannot be negative.");
            }
            if (ActiveMaterialTextureSlots.Contains(slot)) {
                return;
            }

            ActiveMaterialTextureSlots.Add(slot);
        }

        /// <summary>
        /// Resolves one named material texture from the current material or one of its shader-material parents.
        /// </summary>
        /// <param name="material">Material whose binding should be resolved.</param>
        /// <param name="bindingName">Texture binding name to resolve.</param>
        /// <param name="runtimeTexture">Resolved runtime texture when present.</param>
        /// <returns>True when the material chain provides the requested texture binding; otherwise false.</returns>
        bool TryResolveMaterialTexture(ShaderRuntimeMaterial material, string bindingName, out RuntimeTexture runtimeTexture) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }
            if (string.IsNullOrWhiteSpace(bindingName)) {
                runtimeTexture = null;
                return false;
            }

            int bindingIndex = material.Layout.FindTextureBindingIndex(bindingName);
            if (bindingIndex >= 0) {
                runtimeTexture = material.Properties.GetTexture(bindingIndex);
                if (runtimeTexture != null) {
                    return true;
                }
            }

            if (material.ParentMaterial is ShaderRuntimeMaterial parentShaderMaterial) {
                return TryResolveMaterialTexture(parentShaderMaterial, bindingName, out runtimeTexture);
            }

            runtimeTexture = null;
            return false;
        }

        /// <summary>Creates and initializes one shared material constant buffer. Subsequent draws only bind this resource.</summary>
        /// <param name="data">Retained immutable payload, required to satisfy the DirectX constant-buffer alignment.</param>
        /// <returns>Initialized resource owned by the content cache.</returns>
        Buffer CreateMaterialConstantBuffer(byte[] data) {
            if (data.Length == 0 || data.Length % 16 != 0) {
                throw new InvalidOperationException($"DirectX11 constant-buffer size must be positive and 16-byte aligned, but received {data.Length} bytes.");
            }
            using (var stream = new SharpDX.DataStream(data.Length, true, true)) {
                stream.Write(data, 0, data.Length);
                stream.Position = 0;
                return new Buffer(Device, stream, new BufferDescription(data.Length, ResourceUsage.Immutable,
                    BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0));
            }
        }

        /// <summary>
        /// Creates the sampler used by textured 3D materials.
        /// </summary>
        /// <returns>Configured sampler state.</returns>
        SamplerState CreateMaterialTextureSampler() {
            SamplerStateDescription samplerDescription = new SamplerStateDescription {
                Filter = Filter.MinMagMipPoint,
                AddressU = TextureAddressMode.Wrap,
                AddressV = TextureAddressMode.Wrap,
                AddressW = TextureAddressMode.Wrap,
                ComparisonFunction = Comparison.Never,
                MinimumLod = 0,
                MaximumLod = float.MaxValue
            };

            return new SamplerState(Device, samplerDescription);
        }
    }
}
