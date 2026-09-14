using SharpDX.Direct3D11;
using Buffer = SharpDX.Direct3D11.Buffer;
using D3DDevice = SharpDX.Direct3D11.Device;

namespace helengine.directx11 {
    /// <summary>
    /// Binds runtime materials to the DirectX11 pipeline: it resolves the concrete DirectX11 material, shader resources, texture views and constant-buffer payloads behind a runtime material chain, and remembers which material and which pixel-shader texture slots are currently bound so redundant work and stale bindings are avoided.
    /// </summary>
    public class DirectX11MaterialBinder : IDisposable {
        /// <summary>
        /// Device used to create the shared sampler and per-slot constant buffers.
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
        /// Cache of DirectX11 constant buffers keyed by shader slot for per-material payload uploads.
        /// </summary>
        readonly Dictionary<int, Buffer> MaterialConstantBuffersBySlot;

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
            MaterialConstantBuffersBySlot = new Dictionary<int, Buffer>();
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
            if (!ReferenceEquals(ActiveMaterial, shaderMaterial)) {
                ClearActiveMaterialTextureBindings();

                context.InputAssembler.InputLayout = shaderResource.InputLayout;
                context.VertexShader.Set(shaderResource.VertexShader);
                context.PixelShader.Set(shaderResource.PixelShader);
                ActiveMaterial = shaderMaterial;
            }

            PipelineStateCache.ApplyMaterialRenderState(context, material.RenderState);
            ApplyMaterialConstantBufferBindings(material);
            ClearActiveMaterialTextureBindings();
            if (material.Layout.TextureBindings.Length > 0) {
                List<DirectX11MaterialTextureBinding> resolvedBindings = ResolveMaterialTextureBindings(material);
                for (int bindingIndex = 0; bindingIndex < resolvedBindings.Count; bindingIndex++) {
                    DirectX11MaterialTextureBinding binding = resolvedBindings[bindingIndex];
                    context.PixelShader.SetShaderResource(binding.Slot, binding.ResourceView);
                    context.PixelShader.SetSampler(binding.Slot, MaterialTextureSampler);
                    TrackActiveMaterialTextureSlot(binding.Slot);
                }
            }
        }

        /// <summary>
        /// Applies per-material constant-buffer payloads for the current draw.
        /// </summary>
        /// <param name="material">Resolved runtime material instance that provides constant-buffer values.</param>
        public void ApplyMaterialConstantBufferBindings(ShaderRuntimeMaterial material) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            DeviceContext context = Device.ImmediateContext;
            MaterialLayoutBinding[] layoutBindings = material.Layout.ConstantBufferBindings;
            for (int bindingIndex = 0; bindingIndex < layoutBindings.Length; bindingIndex++) {
                MaterialLayoutBinding binding = layoutBindings[bindingIndex];
                if (binding == null) {
                    continue;
                }

                if (IsEngineManagedConstantBufferBinding(binding.Name)) {
                    continue;
                }

                if (!material.TryResolveConstantBufferData(binding.Name, out _)) {
                    context.VertexShader.SetConstantBuffer(binding.Slot, null);
                    context.PixelShader.SetConstantBuffer(binding.Slot, null);
                }
            }

            List<DirectX11MaterialConstantBufferBinding> resolvedBindings = ResolveMaterialConstantBufferBindings(material);
            for (int bindingIndex = 0; bindingIndex < resolvedBindings.Count; bindingIndex++) {
                DirectX11MaterialConstantBufferBinding binding = resolvedBindings[bindingIndex];
                Buffer constantBuffer = GetOrCreateMaterialConstantBuffer(binding.Slot, binding.Data.Length);
                context.UpdateSubresource(binding.Data, constantBuffer);
                context.VertexShader.SetConstantBuffer(binding.Slot, constantBuffer);
                context.PixelShader.SetConstantBuffer(binding.Slot, constantBuffer);
            }
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
            MaterialLayoutBinding[] layoutBindings = material.Layout.ConstantBufferBindings;
            for (int bindingIndex = 0; bindingIndex < layoutBindings.Length; bindingIndex++) {
                MaterialLayoutBinding binding = layoutBindings[bindingIndex];
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
            MaterialLayoutBinding[] layoutBindings = material.Layout.TextureBindings;
            for (int bindingIndex = 0; bindingIndex < layoutBindings.Length; bindingIndex++) {
                MaterialLayoutBinding binding = layoutBindings[bindingIndex];
                if (!TryResolveMaterialTexture(material, binding.Name, out RuntimeTexture runtimeTexture)) {
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

            if (runtimeTexture is DirectX11TextureResource textureResource) {
                if (textureResource.Resource == null) {
                    throw new InvalidOperationException("DirectX11 texture resources must expose a shader resource view.");
                }

                return textureResource.Resource;
            } else if (runtimeTexture is DirectX11RenderTargetResource renderTargetResource) {
                if (renderTargetResource.ShaderResourceView == null) {
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
        /// Releases the shared texture sampler and every cached per-slot material constant buffer.
        /// </summary>
        public void Dispose() {
            foreach (KeyValuePair<int, Buffer> pair in MaterialConstantBuffersBySlot) {
                pair.Value.Dispose();
            }

            MaterialConstantBuffersBySlot.Clear();
            MaterialTextureSampler.Dispose();
            ActiveMaterialTextureSlots.Clear();
            ActiveMaterial = null;
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

        /// <summary>
        /// Resolves one cached DirectX11 constant buffer for a material shader slot.
        /// </summary>
        /// <param name="slot">DirectX11 constant-buffer slot to bind.</param>
        /// <param name="sizeInBytes">Required constant-buffer size in bytes.</param>
        /// <returns>Cached constant buffer that matches the requested slot and size.</returns>
        Buffer GetOrCreateMaterialConstantBuffer(int slot, int sizeInBytes) {
            if (slot < 0) {
                throw new ArgumentOutOfRangeException(nameof(slot), "Constant-buffer slot cannot be negative.");
            }

            if (sizeInBytes <= 0) {
                throw new ArgumentOutOfRangeException(nameof(sizeInBytes), "Constant-buffer size must be positive.");
            }

            if (sizeInBytes % 16 != 0) {
                throw new InvalidOperationException($"DirectX11 constant-buffer size must be 16-byte aligned, but slot {slot} requested {sizeInBytes} bytes.");
            }

            if (MaterialConstantBuffersBySlot.TryGetValue(slot, out Buffer cachedBuffer)) {
                if (cachedBuffer.Description.SizeInBytes == sizeInBytes) {
                    return cachedBuffer;
                }

                cachedBuffer.Dispose();
                MaterialConstantBuffersBySlot.Remove(slot);
            }

            Buffer constantBuffer = new Buffer(Device, sizeInBytes, ResourceUsage.Default,
                BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            MaterialConstantBuffersBySlot.Add(slot, constantBuffer);
            return constantBuffer;
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
