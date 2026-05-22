using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using Buffer = SharpDX.Direct3D11.Buffer;
using D3DDevice = SharpDX.Direct3D11.Device;
using DxgiFactory1 = SharpDX.DXGI.Factory1;

namespace helengine.directx11 {
    /// <summary>
    /// DirectX11-backed renderer responsible for 3D rendering and swap chain management.
    /// </summary>
    public class DirectX11Renderer3D : RenderManager3D, IShaderRenderManager3D, IRenderVisitor3D, IDirectX11RenderPassExecutor {
        /// <summary>
        /// Number of buffers used by each swap chain.
        /// </summary>
        const int SwapChainBufferCount = 2;
        /// <summary>
        /// Default forward axis for cameras before rotation.
        /// </summary>
        static readonly float3 DefaultForward = new float3(0f, 0f, -1f);
        /// <summary>
        /// Default up axis for cameras before rotation.
        /// </summary>
        static readonly float3 DefaultUp = new float3(0f, 1f, 0f);
        /// <summary>
        /// Temporary trace file used while comparing directional-shadow plaza face lighting between DirectX11 and PSP.
        /// </summary>
        static readonly string PlazaFaceTracePath = Path.Combine(Path.GetTempPath(), "helengine_windows_plaza_trace.log");
        /// <summary>
        /// Shader filename used for missing-material rendering.
        /// </summary>
        const string MissingMaterialShaderFileName = "MissingMaterial.fx";
        /// <summary>
        /// Maximum number of point-shadow cube textures bound by the built-in forward shader.
        /// </summary>
        const int MaximumPointShadowTextureSlots = 4;
        /// <summary>
        /// Constant depth bias applied while rendering shadow maps to reduce self-shadowing on simple authored geometry.
        /// </summary>
        const int ShadowDepthBias = 1000;
        /// <summary>
        /// Slope-scaled depth bias applied while rendering shadow maps so grazing-angle receivers do not collapse into full self-shadowing.
        /// </summary>
        const float ShadowSlopeScaledDepthBias = 1.0f;

        /// <summary>
        /// Tracks elapsed time for frame statistics.
        /// </summary>
        Stopwatch frameStopwatch = Stopwatch.StartNew();
        /// <summary>
        /// Draw call count accumulated during the current frame.
        /// </summary>
        int drawCallsThisFrame;
        /// <summary>
        /// Draw call count from the previous frame.
        /// </summary>
        int lastDrawCalls;
        /// <summary>
        /// Visible light count selected for the previous extracted camera frame.
        /// </summary>
        int lastSelectedLightCount;
        /// <summary>
        /// Shadow-enabled light count selected for the previous extracted camera frame.
        /// </summary>
        int lastSelectedShadowLightCount;
        /// <summary>
        /// Frames per second measured on the previous frame.
        /// </summary>
        double lastFps;
        /// <summary>
        /// Frame time in milliseconds measured on the previous frame.
        /// </summary>
        double lastFrameTimeMs;
        /// <summary>
        /// Swapchain surfaces tracked by this renderer.
        /// </summary>
        List<DirectX11SwapChainSurface> surfaces;
        /// <summary>
        /// Lookup of swapchain surfaces by native window handle.
        /// </summary>
        Dictionary<IntPtr, DirectX11SwapChainSurface> surfacesByHandle;
        /// <summary>
        /// Constant buffer for the built-in standard mesh transform data.
        /// </summary>
        Buffer constantBuffer;
        /// <summary>
        /// Constant buffer used for custom effect shader data.
        /// </summary>
        Buffer customPassConstantBuffer;
        /// <summary>
        /// Constant buffer used for packed forward-light shader data.
        /// </summary>
        Buffer forwardLightConstantBuffer;
        /// <summary>
        /// Constant buffer used for packed atlas-shadow shader data.
        /// </summary>
        Buffer shadowConstantBuffer;
        /// <summary>
        /// Constant buffer used by the built-in point-shadow depth shader.
        /// </summary>
        Buffer pointShadowDepthConstantBuffer;
        /// <summary>
        /// Cache of compiled DirectX11 shader resources.
        /// </summary>
        Dictionary<string, DirectX11ShaderResource> ShaderResourceCache;
        /// <summary>
        /// Tracks materials grouped by shader asset id for hot reload updates.
        /// </summary>
        Dictionary<string, List<DirectX11MaterialResource>> MaterialsByShaderAssetId;
        /// <summary>
        /// Cache of DirectX11 constant buffers keyed by shader slot for per-material payload uploads.
        /// </summary>
        Dictionary<int, Buffer> MaterialConstantBuffersBySlot;
        /// <summary>
        /// Blend state for standard rendering.
        /// </summary>
        BlendState blendState;
        /// <summary>
        /// Sampler state shared by textured 3D materials.
        /// </summary>
        SamplerState materialTextureSampler;
        /// <summary>
        /// 2D renderer used for overlays and UI.
        /// </summary>
        DirectX11Renderer2D renderer2D;
        /// <summary>
        /// Rasterizer state used for 3D rendering.
        /// </summary>
        RasterizerState rasterizerState3D;
        /// <summary>
        /// Rasterizer state used while rendering shadow maps.
        /// </summary>
        RasterizerState shadowRasterizerState3D;
        /// Depth-stencil state used for 3D rendering.
        /// </summary>
        DepthStencilState depthStencilState3D;
        /// <summary>
        /// Cache of non-default rasterizer states keyed by material render state.
        /// </summary>
        Dictionary<int, RasterizerState> RasterizerStateCache;
        /// <summary>
        /// Cache of non-default depth-stencil states keyed by material render state.
        /// </summary>
        Dictionary<int, DepthStencilState> DepthStencilStateCache;
        /// <summary>
        /// Tracks the active material for the current pass.
        /// </summary>
        DirectX11MaterialResource ActiveMaterial;
        /// <summary>
        /// World-space camera position for the active 3D camera pass.
        /// </summary>
        float3 currentCameraPosition;
        /// <summary>
        /// Tracks the rasterizer state currently bound to the pipeline.
        /// </summary>
        RasterizerState ActiveRasterizerState;
        /// <summary>
        /// Tracks the depth-stencil state currently bound to the pipeline.
        /// </summary>
        DepthStencilState ActiveDepthStencilState;
        /// <summary>
        /// Tracks the blend state currently bound to the pipeline.
        /// </summary>
        BlendState ActiveBlendState;
        /// <summary>
        /// Stores the fallback material used when a drawable has no material.
        /// </summary>
        DirectX11MaterialResource MissingMaterial;
        /// <summary>
        /// Tracks whether the current pass is a custom shader render.
        /// </summary>
        bool isCustomPassActive;
        /// <summary>
        /// Provides per-draw colors for the active custom pass.
        /// </summary>
        Func<IDrawable3D, byte4> customColorProvider;
        /// <summary>
        /// Stores custom shader pass requests keyed by camera.
        /// </summary>
        Dictionary<ICamera, DirectX11CustomPassRequest> customPassRequests;
        /// <summary>
        /// Caches compiled shader passes by a composite key.
        /// </summary>
        Dictionary<string, DirectX11ShaderPass> shaderPassCache;
        /// <summary>
        /// Default vertex shader entry point for custom passes.
        /// </summary>
        const string DefaultCustomVertexEntry = "VS";
        /// <summary>
        /// Default pixel shader entry point for custom passes.
        /// </summary>
        const string DefaultCustomPixelEntry = "PS";
        /// <summary>
        /// Cached view-projection matrix for the active camera render pass.
        /// </summary>
        float4x4 currentViewProjection;
        /// <summary>
        /// Throttles plaza face trace writes so diagnostics stay bounded during renderer investigation.
        /// </summary>
        int plazaFaceTraceCounter;
        /// <summary>
        /// Limits unconditional plaza drawable trace writes so the parity log stays bounded.
        /// </summary>
        int plazaDrawableTraceCounter;
        /// <summary>
        /// Shared extraction service used to build backend-neutral render frames.
        /// </summary>
        RenderFrameExtractionService FrameExtractionServiceValue;
        /// <summary>
        /// Shared render-plan builder used to select the ordered DirectX11 pass list.
        /// </summary>
        DirectX11RenderPlanBuilder RenderPlanBuilderValue;
        /// <summary>
        /// Shared plan executor used to dispatch selected pass kinds into runtime pass methods.
        /// </summary>
        DirectX11RenderPlanExecutor RenderPlanExecutorValue;
        /// <summary>
        /// Queue snapshot visitor used to copy ordered drawables before extraction.
        /// </summary>
        DirectX11RenderQueueSnapshotVisitor RenderQueueSnapshotVisitorValue;
        /// <summary>
        /// Builder that packs selected lights into the built-in DirectX11 forward-light constant buffer layout.
        /// </summary>
        DirectX11ForwardLightShaderDataBuilder ForwardLightShaderDataBuilderValue;
        /// <summary>
        /// Service that applies the DirectX11 visible-light budget to extracted lights.
        /// </summary>
        DirectX11LightSelectionService LightSelectionServiceValue;
        /// <summary>
        /// Service that plans DirectX11 shadow resources for the selected light set.
        /// </summary>
        DirectX11ShadowResourcePlanner ShadowResourcePlannerValue;
        /// <summary>
        /// Builder that packs atlas-shadow data into the built-in DirectX11 shadow constant buffer layout.
        /// </summary>
        DirectX11ShadowShaderDataBuilder ShadowShaderDataBuilderValue;
        /// <summary>
        /// Tracks the shadow resources planned for the current extracted camera frame.
        /// </summary>
        DirectX11ShadowResourceSet CurrentShadowResourceSet;
        /// <summary>
        /// Cached DirectX11 shadow atlas resources for the current runtime slice.
        /// </summary>
        DirectX11ShadowAtlasResources ShadowAtlasResourcesValue;
        /// <summary>
        /// Cached depth-only shader pass used while rendering atlas shadows.
        /// </summary>
        DirectX11ShaderPass ShadowDepthShaderPassValue;
        /// <summary>
        /// Cached point-shadow depth shader pass used while rendering cube-shadow faces.
        /// </summary>
        DirectX11ShaderPass PointShadowDepthShaderPassValue;
        /// <summary>
        /// Cached point-shadow cube resources reused across frames.
        /// </summary>
        List<DirectX11PointShadowCubeResources> PointShadowCubeResourcesValue;

        /// <summary>
        /// Initializes the DirectX11 device and default pipelines.
        /// </summary>
        public DirectX11Renderer3D() {
            surfaces = new List<DirectX11SwapChainSurface>();
            surfacesByHandle = new Dictionary<IntPtr, DirectX11SwapChainSurface>();
            customPassRequests = new Dictionary<ICamera, DirectX11CustomPassRequest>();
            shaderPassCache = new Dictionary<string, DirectX11ShaderPass>(StringComparer.Ordinal);
            ShaderResourceCache = new Dictionary<string, DirectX11ShaderResource>(StringComparer.Ordinal);
            MaterialsByShaderAssetId = new Dictionary<string, List<DirectX11MaterialResource>>(StringComparer.OrdinalIgnoreCase);
            RasterizerStateCache = new Dictionary<int, RasterizerState>();
            DepthStencilStateCache = new Dictionary<int, DepthStencilState>();
            FrameExtractionServiceValue = new RenderFrameExtractionService();
            RenderPlanBuilderValue = new DirectX11RenderPlanBuilder();
            RenderPlanExecutorValue = new DirectX11RenderPlanExecutor(true, false);
            RenderQueueSnapshotVisitorValue = new DirectX11RenderQueueSnapshotVisitor();
            LightSelectionServiceValue = new DirectX11LightSelectionService();
            ShadowResourcePlannerValue = new DirectX11ShadowResourcePlanner();
            PointShadowCubeResourcesValue = new List<DirectX11PointShadowCubeResources>();
            MaterialConstantBuffersBySlot = new Dictionary<int, Buffer>();
            if (File.Exists(PlazaFaceTracePath)) {
                File.Delete(PlazaFaceTracePath);
            }

            WindowResized += OnWindowResized;

            using (var factory = new DxgiFactory1()) {
                Adapter = factory.GetAdapter1(0);
            }

            Device = new D3DDevice(Adapter, DeviceCreationFlags.None, new[] {
                FeatureLevel.Level_12_1,
                FeatureLevel.Level_12_0,
                FeatureLevel.Level_11_1,
                FeatureLevel.Level_11_0,
                FeatureLevel.Level_10_0,
                FeatureLevel.Level_9_3,
                FeatureLevel.Level_9_2,
                FeatureLevel.Level_9_1,
            });

            constantBuffer = new Buffer(Device, Utilities.SizeOf<StandardMeshShaderData>(), ResourceUsage.Default,
                BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            customPassConstantBuffer = new Buffer(Device, Utilities.SizeOf<CustomEffectShaderData>(), ResourceUsage.Default,
                BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            forwardLightConstantBuffer = new Buffer(Device, Utilities.SizeOf<DirectX11ForwardLightShaderData>(), ResourceUsage.Default,
                BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            shadowConstantBuffer = new Buffer(Device, Utilities.SizeOf<DirectX11ShadowShaderData>(), ResourceUsage.Default,
                BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            pointShadowDepthConstantBuffer = new Buffer(Device, Utilities.SizeOf<DirectX11PointShadowDepthShaderData>(), ResourceUsage.Default,
                BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);

            blendState = CreateBlendState(BlendOption.SourceAlpha, BlendOption.InverseSourceAlpha, BlendOperation.Add);
            materialTextureSampler = CreateMaterialTextureSampler();

            renderer2D = new DirectX11Renderer2D(this);
            DebugInfoRegistry.Register(new DirectX11Renderer3DDebugInfoProvider(this));

            var rasterizerDesc3D = new RasterizerStateDescription {
                CullMode = CullMode.Back,
                FillMode = FillMode.Solid,
                IsFrontCounterClockwise = true,
                IsDepthClipEnabled = true
            };
            rasterizerState3D = new RasterizerState(Device, rasterizerDesc3D);
            var shadowRasterizerDesc3D = new RasterizerStateDescription {
                CullMode = CullMode.Back,
                FillMode = FillMode.Solid,
                IsFrontCounterClockwise = true,
                IsDepthClipEnabled = true,
                DepthBias = ShadowDepthBias,
                SlopeScaledDepthBias = ShadowSlopeScaledDepthBias
            };
            shadowRasterizerState3D = new RasterizerState(Device, shadowRasterizerDesc3D);

            var depthStencilDesc3D = new DepthStencilStateDescription {
                IsDepthEnabled = true,
                DepthWriteMask = DepthWriteMask.All,
                DepthComparison = Comparison.Less
            };
            depthStencilState3D = new DepthStencilState(Device, depthStencilDesc3D);
        }

        /// <summary>
        /// Gets the Direct3D device.
        /// </summary>
        public D3DDevice Device { get; }

        /// <summary>
        /// Gets the DXGI adapter used by the device.
        /// </summary>
        public Adapter1 Adapter { get; }

        /// <summary>
        /// Gets the 2D renderer used for overlay/UI rendering.
        /// </summary>
        public RenderManager2D Render2D => renderer2D;

        /// <summary>
        /// Gets the capability profile published by the DirectX11 backend.
        /// </summary>
        /// <returns>DirectX11 capability profile used by shared planning services.</returns>
        public override RendererBackendCapabilityProfile GetCapabilityProfile() {
            return DirectX11RenderCapabilityProfile.CreateDefault();
        }

        /// <summary>
        /// Gets the last recorded frames-per-second value.
        /// </summary>
        internal double LastFps => lastFps;

        /// <summary>
        /// Gets the draw call count from the previous frame.
        /// </summary>
        internal int LastDrawCalls => lastDrawCalls;

        /// <summary>
        /// Gets the draw-call count recorded by the most recent completed draw.
        /// </summary>
        public override int LastDrawCallCount => lastDrawCalls;

        /// <summary>
        /// Gets the last frame time in milliseconds.
        /// </summary>
        internal double LastFrameTimeMs => lastFrameTimeMs;

        /// <summary>
        /// Gets the visible light count selected for the previous extracted camera frame.
        /// </summary>
        internal int LastSelectedLightCount => lastSelectedLightCount;

        /// <summary>
        /// Gets the shadow-enabled light count selected for the previous extracted camera frame.
        /// </summary>
        internal int LastSelectedShadowLightCount => lastSelectedShadowLightCount;

        /// <summary>
        /// Releases GPU resources and detaches from window events.
        /// </summary>
        public override void Dispose() {
            WindowResized -= OnWindowResized;

            renderer2D.Dispose();

            for (int i = 0; i < surfaces.Count; i++) {
                surfaces[i].Dispose();
            }
            surfaces.Clear();
            surfacesByHandle.Clear();

            depthStencilState3D?.Dispose();
            shadowRasterizerState3D?.Dispose();
            rasterizerState3D?.Dispose();
            materialTextureSampler?.Dispose();
            blendState?.Dispose();
            pointShadowDepthConstantBuffer?.Dispose();
            shadowConstantBuffer?.Dispose();
            forwardLightConstantBuffer?.Dispose();
            customPassConstantBuffer?.Dispose();
            constantBuffer?.Dispose();
            DisposeMaterialConstantBuffers();
            ShadowAtlasResourcesValue?.Dispose();
            ShadowDepthShaderPassValue?.Dispose();
            PointShadowDepthShaderPassValue?.Dispose();
            DisposePointShadowCubeResources();
            DisposeRasterizerStateCache();
            DisposeDepthStencilStateCache();
            DisposeMissingMaterial();
            DisposeShaderResourceCache();
            DisposeShaderPassCache();
            MaterialsByShaderAssetId.Clear();
            Device?.Dispose();
            Adapter?.Dispose();

            base.Dispose();
        }

        /// <summary>
        /// Adds a window and builds the swap chain and render targets.
        /// </summary>
        /// <param name="handle">Native window handle.</param>
        /// <param name="width">Window width.</param>
        /// <param name="height">Window height.</param>
        public override void AddWindow(IntPtr handle, int width, int height) {
            base.AddWindow(handle, width, height);

            using (var factory = Adapter.GetParent<Factory>()) {
                var surface = new DirectX11SwapChainSurface();
                surfaces.Add(surface);
                surfacesByHandle.Add(handle, surface);

                surface.Width = width;
                surface.Height = height;

                var desc = new SwapChainDescription {
                    BufferCount = SwapChainBufferCount,
                    ModeDescription = new ModeDescription(width, height, new Rational(60, 1), Format.B8G8R8A8_UNorm),
                    IsWindowed = true,
                    OutputHandle = handle,
                    SampleDescription = new SampleDescription(1, 0),
                    SwapEffect = SwapEffect.FlipDiscard,
                    Usage = Usage.RenderTargetOutput,
                    Flags = SwapChainFlags.AllowModeSwitch
                };

                surface.SwapChain = new SwapChain(factory, Device, desc);

                using (var backBuffer = surface.SwapChain.GetBackBuffer<Texture2D>(0)) {
                    surface.RenderTargetView = new RenderTargetView(Device, backBuffer);
                }

                surface.DepthBuffer = new Texture2D(Device, new Texture2DDescription {
                    Format = Format.D32_Float_S8X24_UInt,
                    ArraySize = 1,
                    MipLevels = 1,
                    Width = width,
                    Height = height,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Default,
                    BindFlags = BindFlags.DepthStencil,
                    CpuAccessFlags = CpuAccessFlags.None,
                    OptionFlags = ResourceOptionFlags.None
                });

                surface.DepthStencilView = new DepthStencilView(Device, surface.DepthBuffer);

                factory.MakeWindowAssociation(handle, WindowAssociationFlags.IgnoreAll);
            }
        }

        /// <summary>
        /// Handles window resize by recreating swap chain buffers and depth resources.
        /// </summary>
        /// <param name="handle">Native window handle.</param>
        /// <param name="newWidth">New width.</param>
        /// <param name="newHeight">New height.</param>
        void OnWindowResized(IntPtr handle, int newWidth, int newHeight) {
            if (!surfacesByHandle.TryGetValue(handle, out var surface)) {
                return;
            }

            surface.RenderTargetView?.Dispose();
            surface.DepthStencilView?.Dispose();
            surface.DepthBuffer?.Dispose();

            surface.Width = newWidth;
            surface.Height = newHeight;

            surface.SwapChain.ResizeBuffers(SwapChainBufferCount, newWidth, newHeight, Format.B8G8R8A8_UNorm, SwapChainFlags.AllowModeSwitch);

            using (var backBuffer = surface.SwapChain.GetBackBuffer<Texture2D>(0)) {
                surface.RenderTargetView = new RenderTargetView(Device, backBuffer);
            }

            surface.DepthBuffer = new Texture2D(Device, new Texture2DDescription {
                Format = Format.D32_Float_S8X24_UInt,
                ArraySize = 1,
                MipLevels = 1,
                Width = newWidth,
                Height = newHeight,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.DepthStencil,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            });

            surface.DepthStencilView = new DepthStencilView(Device, surface.DepthBuffer);
        }

        /// <summary>
        /// Builds a runtime model from raw mesh data.
        /// </summary>
        /// <param name="data">Raw model asset data.</param>
        /// <returns>GPU-ready model resource.</returns>
        public override RuntimeModel BuildModelFromRaw(ModelAsset data) {
            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }
            if (data.Positions == null || data.Positions.Length == 0) {
                throw new ArgumentException("Model data must include positions.", nameof(data));
            }

            var model = new DirectX11ModelResource();
            var vertices = new VertexPositionNormalUV[data.Positions.Length];
            ModelAssetIndexData indexData = ModelAssetIndexData.Resolve(data);
            float3 boundsMin = data.Positions[0];
            float3 boundsMax = data.Positions[0];

            for (int i = 0; i < data.Positions.Length; i++) {
                float3 pos = data.Positions[i];
                float3 normal = data.Normals[i];
                float2 tex = data.TexCoords[i];
                vertices[i] = new VertexPositionNormalUV(pos, normal, tex);
                if (i > 0) {
                    boundsMin = new float3(
                        Math.Min(boundsMin.X, pos.X),
                        Math.Min(boundsMin.Y, pos.Y),
                        Math.Min(boundsMin.Z, pos.Z));
                    boundsMax = new float3(
                        Math.Max(boundsMax.X, pos.X),
                        Math.Max(boundsMax.Y, pos.Y),
                        Math.Max(boundsMax.Z, pos.Z));
                }
            }

            model.VertexBuffer = Buffer.Create(Device, BindFlags.VertexBuffer, vertices);
            model.VertexCount = vertices.Length;

            if (indexData.IndexCount > 0) {
                model.IndexCount = indexData.IndexCount;
                model.Uses32BitIndices = indexData.Uses32BitIndices;
                if (indexData.Uses32BitIndices) {
                    model.IndexBuffer = Buffer.Create(Device, BindFlags.IndexBuffer, indexData.Indices32);
                } else {
                    model.IndexBuffer = Buffer.Create(Device, BindFlags.IndexBuffer, indexData.Indices16);
                }
            }

            model.SetBounds(boundsMin, boundsMax);
            model.SetSubmeshes(ModelSubmeshResolver.BuildRuntimeSubmeshes(data));

            return model;
        }

        /// <summary>
        /// Gets the shader compile target used by the DirectX11 renderer.
        /// </summary>
        public ShaderCompileTarget ShaderCompileTarget => ShaderCompileTarget.DirectX11;

        /// <summary>
        /// Builds a runtime material from one packaged material asset using the DirectX11 shader runtime loader.
        /// </summary>
        /// <param name="assetContentManager">Content manager that can deserialize companion shader packages.</param>
        /// <param name="contentRootPath">Absolute packaged content root.</param>
        /// <param name="materialAssetPath">Absolute path to the serialized material asset.</param>
        /// <param name="materialAsset">Raw material asset definition.</param>
        /// <returns>Runtime material instance.</returns>
        public override RuntimeMaterial BuildMaterialFromRawAsset(
            ContentManager assetContentManager,
            string contentRootPath,
            string materialAssetPath,
            MaterialAsset materialAsset) {
            return ShaderRuntimeMaterialLoader.BuildMaterialFromRawAsset(this, assetContentManager, contentRootPath, materialAssetPath, materialAsset);
        }

        /// <summary>
        /// Builds a runtime material from raw asset data and a shader asset.
        /// </summary>
        /// <param name="materialAsset">Raw material asset definition.</param>
        /// <param name="shaderAsset">Shader asset used by the material.</param>
        /// <returns>Runtime material instance.</returns>
        public virtual RuntimeMaterial BuildMaterialFromRaw(MaterialAsset materialAsset, ShaderAsset shaderAsset) {
            if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            }

            if (shaderAsset == null) {
                throw new ArgumentNullException(nameof(shaderAsset));
            }

            if (string.IsNullOrWhiteSpace(materialAsset.ShaderAssetId)) {
                throw new InvalidOperationException("Material assets must reference a shader asset id.");
            }

            if (!string.Equals(materialAsset.ShaderAssetId, shaderAsset.Id, StringComparison.Ordinal)) {
                throw new InvalidOperationException("Material asset shader id does not match the provided shader asset.");
            }

            DirectX11ShaderResource shaderResource = GetShaderResource(materialAsset, shaderAsset);
            MaterialLayout layout = MaterialLayoutBuilder.Build(materialAsset, shaderAsset);
            var material = new DirectX11MaterialResource(
                shaderResource,
                materialAsset.ShaderAssetId,
                materialAsset.VertexProgram,
                materialAsset.PixelProgram,
                materialAsset.Variant);
            material.SetId(materialAsset.Id);
            material.SetLayout(layout);
            material.SetRenderState(materialAsset.RenderState);
            material.LightingModel = RuntimeMaterialLightingModel.MetalRoughPbr;
            material.SupportsNormalMapping = !string.IsNullOrWhiteSpace(materialAsset.NormalTextureAssetId);
            material.SupportsEmissive = !string.IsNullOrWhiteSpace(materialAsset.EmissiveTextureAssetId);
            material.CastsShadows = materialAsset.CastsShadows;
            material.ReceivesShadows = materialAsset.ReceivesShadows;
            material.ApplyConstantBufferDefaults(materialAsset.ConstantBuffers ?? Array.Empty<MaterialConstantBufferAsset>());
            StandardMaterialTextureBindingDefaults.Apply(material);
            RegisterMaterial(material);
            return material;
        }

        /// <summary>
        /// Assigns one authored diffuse texture to a DirectX11 shader runtime material.
        /// </summary>
        /// <param name="material">Runtime material that should receive the diffuse texture.</param>
        /// <param name="texture">Runtime texture that should become the material diffuse texture.</param>
        public override void AssignRawMaterialDiffuseTexture(RuntimeMaterial material, RuntimeTexture texture) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }
            if (texture == null) {
                throw new ArgumentNullException(nameof(texture));
            }
            if (material is not ShaderRuntimeMaterial shaderMaterial) {
                throw new InvalidOperationException("DirectX11 raw runtime materials must be shader-backed.");
            }

            shaderMaterial.Properties.SetTexture(StandardMaterialTextureBindingDefaults.DiffuseTextureBindingName, texture);
        }

        /// <summary>
        /// Invalidates cached shader resources and updates materials for the specified shader asset.
        /// </summary>
        /// <param name="shaderAssetId">Shader asset identifier to invalidate.</param>
        /// <param name="shaderAsset">Updated shader asset data.</param>
        public virtual void InvalidateShaderResources(string shaderAssetId, ShaderAsset shaderAsset) {
            if (string.IsNullOrWhiteSpace(shaderAssetId)) {
                throw new ArgumentException("Shader asset id must be provided.", nameof(shaderAssetId));
            }

            if (shaderAsset == null) {
                throw new ArgumentNullException(nameof(shaderAsset));
            }

            InvalidateShaderCache(shaderAssetId);
            if (!MaterialsByShaderAssetId.TryGetValue(shaderAssetId, out List<DirectX11MaterialResource> materials)) {
                return;
            }

            for (int i = 0; i < materials.Count; i++) {
                DirectX11MaterialResource material = materials[i];
                if (material == null) {
                    continue;
                }

                DirectX11ShaderResource shaderResource = GetShaderResource(
                    shaderAssetId,
                    material.VertexProgram,
                    material.PixelProgram,
                    material.Variant,
                    shaderAsset);
                MaterialLayout layout = BuildMaterialLayout(material, shaderAsset);
                material.UpdateShaderResource(shaderResource);
                material.SetLayout(layout);
            }

            ActiveMaterial = null;
        }

        /// <summary>
        /// Creates a DirectX11 render target suitable for camera output.
        /// </summary>
        /// <param name="width">Width of the render target in pixels.</param>
        /// <param name="height">Height of the render target in pixels.</param>
        /// <returns>Render target instance.</returns>
        public override RenderTarget CreateRenderTarget(int width, int height) {
            return new DirectX11RenderTargetResource(Device, width, height, Format.R8G8B8A8_UNorm, Format.D32_Float);
        }

        /// <summary>
        /// Queues a one-frame custom shader pass using the default entry points.
        /// </summary>
        /// <param name="camera">Camera providing transform, viewport, and target information.</param>
        /// <param name="renderQueue">Render queue supplying drawables for the pass.</param>
        /// <param name="shaderPath">Path to the shader file.</param>
        /// <param name="colorProvider">Function that supplies per-draw colors for the shader.</param>
        public void RequestShaderPass(
            ICamera camera,
            IRenderQueue3D renderQueue,
            string shaderPath,
            Func<IDrawable3D, byte4> colorProvider) {
            RequestShaderPass(camera, renderQueue, shaderPath, DefaultCustomVertexEntry, DefaultCustomPixelEntry, colorProvider);
        }

        /// <summary>
        /// Queues a one-frame custom shader pass with explicit entry points.
        /// </summary>
        /// <param name="camera">Camera providing transform, viewport, and target information.</param>
        /// <param name="renderQueue">Render queue supplying drawables for the pass.</param>
        /// <param name="shaderPath">Path to the shader file.</param>
        /// <param name="vertexEntry">Vertex shader entry point.</param>
        /// <param name="pixelEntry">Pixel shader entry point.</param>
        /// <param name="colorProvider">Function that supplies per-draw colors for the shader.</param>
        public void RequestShaderPass(
            ICamera camera,
            IRenderQueue3D renderQueue,
            string shaderPath,
            string vertexEntry,
            string pixelEntry,
            Func<IDrawable3D, byte4> colorProvider) {
            var request = new DirectX11CustomPassRequest(camera, renderQueue, shaderPath, vertexEntry, pixelEntry, colorProvider);
            customPassRequests[camera] = request;
        }

        /// <summary>
        /// Renders all queued custom shader passes before the main surface rendering.
        /// </summary>
        void RenderCustomPasses() {
            if (customPassRequests.Count == 0) {
                return;
            }

            foreach (var entry in customPassRequests) {
                RenderCustomPass(entry.Value);
            }

            customPassRequests.Clear();
        }

        /// <summary>
        /// Renders a single custom pass into the camera's render target.
        /// </summary>
        /// <param name="request">Custom pass request to execute.</param>
        void RenderCustomPass(DirectX11CustomPassRequest request) {
            if (request == null) {
                throw new ArgumentNullException(nameof(request));
            }

            ICamera camera = request.Camera;
            RenderTarget renderTarget = camera.RenderTarget;
            if (renderTarget == null) {
                throw new InvalidOperationException("Custom shader passes require a render target.");
            }
            if (renderTarget is not DirectX11RenderTargetResource directX11Target) {
                throw new InvalidOperationException("Custom shader passes require DirectX11 render targets.");
            }

            DirectX11ShaderPass shaderPass = GetShaderPass(request.ShaderPath, request.VertexEntry, request.PixelEntry);

            var context = Device.ImmediateContext;
            context.InputAssembler.InputLayout = shaderPass.InputLayout;
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            context.Rasterizer.State = rasterizerState3D;
            context.OutputMerger.SetDepthStencilState(depthStencilState3D, 0);
            context.OutputMerger.SetBlendState(null);

            RenderTargetView renderTargetView = directX11Target.RenderTargetView;
            DepthStencilView depthStencilView = directX11Target.DepthStencilView;

            CameraClearSettings clearSettings = camera.ClearSettings;
            bool clearColor = clearSettings.ClearColorEnabled;
            float4 clearColorValue = clearSettings.ClearColor;
            bool clearDepth = clearSettings.ClearDepthEnabled;
            float clearDepthValue = clearSettings.ClearDepth;
            bool clearStencil = clearSettings.ClearStencilEnabled;
            byte clearStencilValue = clearSettings.ClearStencil;

            context.OutputMerger.SetTargets(depthStencilView, renderTargetView);
            if (clearColor) {
                context.ClearRenderTargetView(renderTargetView, new RawColor4(clearColorValue.X, clearColorValue.Y, clearColorValue.Z, clearColorValue.W));
            }
            DepthStencilClearFlags clearFlags = 0;
            if (clearDepth) {
                clearFlags |= DepthStencilClearFlags.Depth;
            }
            if (clearStencil) {
                clearFlags |= DepthStencilClearFlags.Stencil;
            }
            if (clearFlags != 0) {
                context.ClearDepthStencilView(depthStencilView, clearFlags, clearDepthValue, clearStencilValue);
            }

            float4x4 view;
            float3 cameraPos = camera.Parent.Position;
            currentCameraPosition = cameraPos;
            float4 cameraOrientation = camera.Parent.Orientation;
            float3 cameraForward = float4.RotateVector(DefaultForward, cameraOrientation);
            float3 cameraUp = float4.RotateVector(DefaultUp, cameraOrientation);
            float3 cameraTarget = cameraPos + cameraForward;
            float4x4.CreateLookAt(ref cameraPos, ref cameraTarget, ref cameraUp, out view);

            float4 viewport = CameraViewportResolver.ResolveViewport(camera.Viewport, directX11Target.Width, directX11Target.Height);
            context.Rasterizer.SetViewport(viewport.X, viewport.Y, viewport.Z, viewport.W);

            float4x4 projection = CameraProjectionUtils.CreatePerspectiveProjection(camera, (float)Math.PI / 4.0f, viewport.Z / viewport.W);

            float4x4.Multiply(ref view, ref projection, out currentViewProjection);

            isCustomPassActive = true;
            customColorProvider = request.ColorProvider;
            try {
                context.VertexShader.Set(shaderPass.VertexShader);
                context.PixelShader.Set(shaderPass.PixelShader);
                context.VertexShader.SetConstantBuffer(0, customPassConstantBuffer);
                context.PixelShader.SetConstantBuffer(0, customPassConstantBuffer);

                request.RenderQueue.VisitOrdered(this);
            } finally {
                isCustomPassActive = false;
                customColorProvider = null;
            }
        }

        /// <summary>
        /// Extracts, plans, and executes one camera render using the shared frame contracts.
        /// </summary>
        /// <param name="surface">Render surface for the window.</param>
        /// <param name="camera">Camera to render.</param>
        protected virtual void RenderCamera(DirectX11SwapChainSurface surface, CameraComponent camera) {
            if (surface == null) {
                throw new ArgumentNullException(nameof(surface));
            } else if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }

            IDrawable3D[] drawables = SnapshotRenderQueue(camera.RenderQueue3D);
            LightComponent[] lights = SnapshotVisibleLights(camera);
            RendererBackendCapabilityProfile capabilityProfile = GetCapabilityProfile();
            RenderFrameExtractionResult extractionResult = GetFrameExtractionService().Extract(
                [camera],
                drawables,
                lights,
                capabilityProfile);
            RenderFrame frame = extractionResult.Frames[0];
            RenderFrameLightSubmission[] selectedLights = GetLightSelectionService().SelectVisibleLights(frame.LightSubmissions, capabilityProfile.MaximumVisibleLights);
            lastSelectedLightCount = selectedLights.Length;
            DirectX11ShadowResourceSet shadowResourceSet = GetShadowResourcePlanner().PlanResources(selectedLights, capabilityProfile.MaximumShadowedLights);
            lastSelectedShadowLightCount = shadowResourceSet.SelectedShadowLights.Count;
            CurrentShadowResourceSet = shadowResourceSet;
            RenderPlan plan = GetRenderPlanBuilder().Build(frame, extractionResult.BackendCapabilities);
            DirectX11RenderPassExecutionContext context = new DirectX11RenderPassExecutionContext(
                frame,
                surface,
                selectedLights,
                shadowResourceSet.SelectedShadowLights,
                shadowResourceSet.AtlasAllocations,
                shadowResourceSet.PointShadowResources);
            ExecuteCameraPlan(context, plan);
        }

        /// <summary>
        /// Executes one extracted camera plan after the frame and pass order have been resolved.
        /// </summary>
        /// <param name="context">Execution context containing the frame and target surface.</param>
        /// <param name="plan">Ordered pass list to execute.</param>
        protected virtual void ExecuteCameraPlan(DirectX11RenderPassExecutionContext context, RenderPlan plan) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            } else if (plan == null) {
                throw new ArgumentNullException(nameof(plan));
            }

            PrepareCameraFrame(context);
            GetRenderPlanExecutor().ExecutePlan(context, plan, this);
        }

        /// <summary>
        /// Prepares the DirectX11 pipeline and render targets for one extracted camera frame.
        /// </summary>
        /// <param name="context">Execution context containing the frame and target surface.</param>
        protected virtual void PrepareCameraFrame(DirectX11RenderPassExecutionContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            CameraComponent camera = context.Frame.Camera;
            var deviceContext = Device.ImmediateContext;
            RenderTargetView renderTargetView = context.Surface.RenderTargetView;
            DepthStencilView depthStencilView = context.Surface.DepthStencilView;
            CameraClearSettings clearSettings = camera.ClearSettings;
            bool clearColor = clearSettings.ClearColorEnabled;
            float4 clearColorValue = clearSettings.ClearColor;
            bool clearDepth = clearSettings.ClearDepthEnabled;
            float clearDepthValue = clearSettings.ClearDepth;
            bool clearStencil = clearSettings.ClearStencilEnabled;
            byte clearStencilValue = clearSettings.ClearStencil;

            RenderTarget renderTarget = camera.RenderTarget;
            if (renderTarget != null) {
                if (renderTarget is not DirectX11RenderTargetResource directX11Target) {
                    throw new InvalidOperationException("Camera render targets must use DirectX11RenderTargetResource when rendering with DirectX11.");
                }

                renderTargetView = directX11Target.RenderTargetView;
                depthStencilView = directX11Target.DepthStencilView;
            }

            deviceContext.OutputMerger.SetTargets(depthStencilView, renderTargetView);
            ApplyCameraClear(deviceContext, camera, context.Surface, renderTarget, renderTargetView, depthStencilView);

            deviceContext.Rasterizer.State = rasterizerState3D;
            deviceContext.OutputMerger.SetDepthStencilState(depthStencilState3D, 0);
            ActiveRasterizerState = rasterizerState3D;
            ActiveDepthStencilState = depthStencilState3D;

            float4x4 view;
            float3 cameraPos = camera.Parent.Position;
            float4 cameraOrientation = camera.Parent.Orientation;
            float3 cameraForward = float4.RotateVector(DefaultForward, cameraOrientation);
            float3 cameraUp = float4.RotateVector(DefaultUp, cameraOrientation);
            float3 cameraTarget = cameraPos + cameraForward;
            float4x4.CreateLookAt(ref cameraPos, ref cameraTarget, ref cameraUp, out view);

            float4 viewport = ResolveCameraViewport(camera, context.Surface);
            deviceContext.Rasterizer.SetViewport(viewport.X, viewport.Y, viewport.Z, viewport.W);

            float4x4 projection = CameraProjectionUtils.CreatePerspectiveProjection(camera, (float)Math.PI / 4.0f, viewport.Z / viewport.W);

            float4x4.Multiply(ref view, ref projection, out currentViewProjection);

            deviceContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            isCustomPassActive = false;
            customColorProvider = null;
            ActiveMaterial = null;
            deviceContext.OutputMerger.SetBlendState(null);
            ActiveBlendState = null;
            deviceContext.VertexShader.SetConstantBuffer(0, constantBuffer);
            deviceContext.PixelShader.SetConstantBuffer(0, constantBuffer);
            UpdateShadowShaderData(new DirectX11ShadowShaderData());
            UpdateShadowAtlasBindings(false);
            UpdatePointShadowBindings(0);
            PrepareForwardLightState(context);
        }

        /// <summary>
        /// Applies the active camera clear settings to the current output target.
        /// </summary>
        /// <param name="deviceContext">Immediate device context executing the clear.</param>
        /// <param name="camera">Camera whose clear settings should be applied.</param>
        /// <param name="surface">Swap-chain surface receiving backbuffer rendering.</param>
        /// <param name="renderTarget">Explicit camera render target, or null when rendering to the backbuffer.</param>
        /// <param name="renderTargetView">Resolved render-target view for the active pass.</param>
        /// <param name="depthStencilView">Resolved depth-stencil view for the active pass.</param>
        void ApplyCameraClear(
            DeviceContext deviceContext,
            CameraComponent camera,
            DirectX11SwapChainSurface surface,
            RenderTarget renderTarget,
            RenderTargetView renderTargetView,
            DepthStencilView depthStencilView) {
            if (deviceContext == null) {
                throw new ArgumentNullException(nameof(deviceContext));
            }
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }
            if (surface == null) {
                throw new ArgumentNullException(nameof(surface));
            }
            if (renderTargetView == null) {
                throw new ArgumentNullException(nameof(renderTargetView));
            }
            if (depthStencilView == null) {
                throw new ArgumentNullException(nameof(depthStencilView));
            }

            CameraClearSettings clearSettings = camera.ClearSettings;
            bool clearColor = clearSettings.ClearColorEnabled;
            float4 clearColorValue = clearSettings.ClearColor;
            bool clearDepth = clearSettings.ClearDepthEnabled;
            float clearDepthValue = clearSettings.ClearDepth;
            bool clearStencil = clearSettings.ClearStencilEnabled;
            byte clearStencilValue = clearSettings.ClearStencil;
            if (clearColor) {
                float4 viewport = ResolveCameraViewport(camera, surface);
                if (DirectX11CameraClearRegionResolver.RequiresViewportScopedBackBufferColorClear(renderTarget, surface, viewport)) {
                    ClearViewportColorRegion(renderTargetView, clearColorValue, viewport, surface.Width, surface.Height);
                } else {
                    deviceContext.ClearRenderTargetView(renderTargetView, new RawColor4(clearColorValue.X, clearColorValue.Y, clearColorValue.Z, clearColorValue.W));
                }
            }

            DepthStencilClearFlags clearFlags = 0;
            if (clearDepth) {
                clearFlags |= DepthStencilClearFlags.Depth;
            }
            if (clearStencil) {
                clearFlags |= DepthStencilClearFlags.Stencil;
            }
            if (clearFlags != 0) {
                deviceContext.ClearDepthStencilView(depthStencilView, clearFlags, clearDepthValue, clearStencilValue);
            }
        }

        /// <summary>
        /// Clears one color render target only within the active camera viewport rectangle.
        /// </summary>
        /// <param name="renderTargetView">Render target view to clear.</param>
        /// <param name="clearColorValue">Color to write into the viewport region.</param>
        /// <param name="viewport">Viewport rectangle that should receive the clear.</param>
        /// <param name="targetWidth">Target width in pixels.</param>
        /// <param name="targetHeight">Target height in pixels.</param>
        void ClearViewportColorRegion(
            RenderTargetView renderTargetView,
            float4 clearColorValue,
            float4 viewport,
            int targetWidth,
            int targetHeight) {
            if (renderTargetView == null) {
                throw new ArgumentNullException(nameof(renderTargetView));
            }

            using DeviceContext1 deviceContext1 = Device.ImmediateContext.QueryInterface<DeviceContext1>();
            RawRectangle rectangle = DirectX11CameraClearRegionResolver.ResolveViewportRectangle(viewport, targetWidth, targetHeight);
            deviceContext1.ClearView(renderTargetView, new RawColor4(clearColorValue.X, clearColorValue.Y, clearColorValue.Z, clearColorValue.W), new[] { rectangle });
        }

        /// <summary>
        /// Executes the depth-only prepass for the extracted frame.
        /// </summary>
        /// <param name="context">Execution context containing the frame and target surface.</param>
        public virtual void ExecuteDepthPrepass(DirectX11RenderPassExecutionContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }
        }

        /// <summary>
        /// Executes the shadow pass for the extracted frame.
        /// </summary>
        /// <param name="context">Execution context containing the frame and target surface.</param>
        public virtual void ExecuteShadowPass(DirectX11RenderPassExecutionContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            DirectX11ShadowResourceSet shadowResourceSet = CurrentShadowResourceSet;
            if (shadowResourceSet == null) {
                return;
            }

            bool renderedShadowResources = false;
            if (shadowResourceSet.AtlasAllocations.Count > 0) {
                DirectX11ShadowAtlasResources atlasResources = GetShadowAtlasResources(shadowResourceSet);
                RenderShadowAtlas(context, shadowResourceSet, atlasResources);
                renderedShadowResources = true;
            }

            if (shadowResourceSet.PointShadowResources.Count > 0) {
                RenderPointShadowResources(context, shadowResourceSet);
                renderedShadowResources = true;
            }

            if (renderedShadowResources) {
                RestoreCameraFrameTargetsAfterShadowPass(context);
            }

            PrepareShadowShaderState(context, shadowResourceSet);
        }

        /// <summary>
        /// Restores the active camera render target and viewport after shadow rendering changed the output-merger state.
        /// </summary>
        /// <param name="context">Execution context containing the active camera and output surface.</param>
        protected virtual void RestoreCameraFrameTargetsAfterShadowPass(DirectX11RenderPassExecutionContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            CameraComponent camera = context.Frame.Camera;
            RenderTargetView renderTargetView = context.Surface.RenderTargetView;
            DepthStencilView depthStencilView = context.Surface.DepthStencilView;
            RenderTarget renderTarget = camera.RenderTarget;
            if (renderTarget != null) {
                if (renderTarget is not DirectX11RenderTargetResource directX11Target) {
                    throw new InvalidOperationException("Camera render targets must use DirectX11RenderTargetResource when rendering with DirectX11.");
                }

                renderTargetView = directX11Target.RenderTargetView;
                depthStencilView = directX11Target.DepthStencilView;
            }

            var deviceContext = Device.ImmediateContext;
            deviceContext.OutputMerger.SetTargets(depthStencilView, renderTargetView);
            deviceContext.Rasterizer.State = rasterizerState3D;
            deviceContext.OutputMerger.SetDepthStencilState(depthStencilState3D, 0);
            deviceContext.OutputMerger.SetBlendState(null);
            float4 viewport = ResolveCameraViewport(camera, context.Surface);
            deviceContext.Rasterizer.SetViewport(viewport.X, viewport.Y, viewport.Z, viewport.W);
            ActiveRasterizerState = rasterizerState3D;
            ActiveDepthStencilState = depthStencilState3D;
            ActiveBlendState = null;
            deviceContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            deviceContext.VertexShader.SetConstantBuffer(0, constantBuffer);
            deviceContext.PixelShader.SetConstantBuffer(0, constantBuffer);
            deviceContext.PixelShader.SetConstantBuffer(1, forwardLightConstantBuffer);
            deviceContext.PixelShader.SetConstantBuffer(2, shadowConstantBuffer);
        }

        /// <summary>
        /// Resolves one authored camera viewport against the active backbuffer or render-target dimensions.
        /// </summary>
        /// <param name="camera">Camera whose viewport should be resolved.</param>
        /// <param name="surface">Swap-chain surface receiving backbuffer rendering when no explicit render target is bound.</param>
        /// <returns>Viewport rectangle expressed in pixel-space coordinates.</returns>
        static float4 ResolveCameraViewport(CameraComponent camera, DirectX11SwapChainSurface surface) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }
            if (surface == null) {
                throw new ArgumentNullException(nameof(surface));
            }

            RenderTarget renderTarget = camera.RenderTarget;
            if (renderTarget != null) {
                return CameraViewportResolver.ResolveViewport(camera.Viewport, renderTarget.Width, renderTarget.Height);
            }

            return CameraViewportResolver.ResolveViewport(camera.Viewport, surface.Width, surface.Height);
        }

        /// <summary>
        /// Executes the opaque forward pass for the extracted frame.
        /// </summary>
        /// <param name="context">Execution context containing the frame and target surface.</param>
        public virtual void ExecuteOpaqueForwardPass(DirectX11RenderPassExecutionContext context) {
            ExecuteGeometryPass(context, false);
        }

        /// <summary>
        /// Executes the transparent forward pass for the extracted frame.
        /// </summary>
        /// <param name="context">Execution context containing the frame and target surface.</param>
        public virtual void ExecuteTransparentForwardPass(DirectX11RenderPassExecutionContext context) {
            ExecuteGeometryPass(context, true);
        }

        /// <summary>
        /// Executes the post-process chain for the extracted frame.
        /// </summary>
        /// <param name="context">Execution context containing the frame and target surface.</param>
        public virtual void ExecutePostProcessPass(DirectX11RenderPassExecutionContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }
        }

        /// <summary>
        /// Executes the present stage for the extracted frame.
        /// </summary>
        /// <param name="context">Execution context containing the frame and target surface.</param>
        public virtual void ExecutePresentPass(DirectX11RenderPassExecutionContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            renderer2D.RenderCamera(context.Frame.Camera);
        }

        /// <summary>
        /// Prepares the packed forward-light shader state for the current extracted camera frame.
        /// </summary>
        /// <param name="context">Execution context containing the selected light set for the current frame.</param>
        protected virtual void PrepareForwardLightState(DirectX11RenderPassExecutionContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            DirectX11ForwardLightShaderData data = BuildForwardLightShaderData(context.SelectedLights);
            UpdateForwardLightShaderData(data);
        }

        /// <summary>
        /// Builds the packed forward-light shader data for the selected lights of the current frame.
        /// </summary>
        /// <param name="selectedLights">Selected lights that survived backend budgeting.</param>
        /// <returns>Packed forward-light shader data.</returns>
        protected virtual DirectX11ForwardLightShaderData BuildForwardLightShaderData(IReadOnlyList<RenderFrameLightSubmission> selectedLights) {
            return GetForwardLightShaderDataBuilder().Build(selectedLights);
        }

        /// <summary>
        /// Gets the DirectX11 shadow-resource planner used to derive shadow execution resources for the current frame.
        /// </summary>
        /// <returns>DirectX11 shadow-resource planner.</returns>
        protected virtual DirectX11ShadowResourcePlanner GetShadowResourcePlanner() {
            if (ShadowResourcePlannerValue == null) {
                ShadowResourcePlannerValue = new DirectX11ShadowResourcePlanner();
            }

            return ShadowResourcePlannerValue;
        }

        /// <summary>
        /// Retrieves the shared atlas-shadow shader-data builder, creating it lazily when necessary.
        /// </summary>
        /// <returns>Shared DirectX11 atlas-shadow shader-data builder.</returns>
        protected virtual DirectX11ShadowShaderDataBuilder GetShadowShaderDataBuilder() {
            if (ShadowShaderDataBuilderValue == null) {
                ShadowShaderDataBuilderValue = new DirectX11ShadowShaderDataBuilder();
            }

            return ShadowShaderDataBuilderValue;
        }

        /// <summary>
        /// Prepares the atlas-shadow shader state for the current extracted camera frame.
        /// </summary>
        /// <param name="context">Execution context containing selected forward and shadow lights.</param>
        /// <param name="shadowResourceSet">Planned shadow resources for the current frame.</param>
        protected virtual void PrepareShadowShaderState(DirectX11RenderPassExecutionContext context, DirectX11ShadowResourceSet shadowResourceSet) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            } else if (shadowResourceSet == null) {
                throw new ArgumentNullException(nameof(shadowResourceSet));
            }

            DirectX11ShadowShaderData data = BuildShadowShaderData(context, shadowResourceSet);
            UpdateShadowShaderData(data);
            UpdateShadowAtlasBindings(shadowResourceSet.AtlasAllocations.Count > 0);
            UpdatePointShadowBindings(shadowResourceSet.PointShadowResources.Count);
        }

        /// <summary>
        /// Builds the atlas-shadow shader data for the current extracted camera frame.
        /// </summary>
        /// <param name="context">Execution context containing selected forward and shadow lights.</param>
        /// <param name="shadowResourceSet">Planned shadow resources for the current frame.</param>
        /// <returns>Packed atlas-shadow shader data.</returns>
        protected virtual DirectX11ShadowShaderData BuildShadowShaderData(
            DirectX11RenderPassExecutionContext context,
            DirectX11ShadowResourceSet shadowResourceSet) {
            return GetShadowShaderDataBuilder().Build(context.Frame.Camera, context.SelectedLights, shadowResourceSet);
        }

        /// <summary>
        /// Uploads the packed atlas-shadow shader data to the DirectX11 pixel-shader constant-buffer slot.
        /// </summary>
        /// <param name="data">Packed atlas-shadow shader data prepared for the current frame.</param>
        protected virtual void UpdateShadowShaderData(DirectX11ShadowShaderData data) {
            var context = Device.ImmediateContext;
            context.UpdateSubresource(ref data, shadowConstantBuffer);
            context.PixelShader.SetConstantBuffer(2, shadowConstantBuffer);
        }

        /// <summary>
        /// Updates the pixel-shader atlas-shadow resource bindings for the current frame.
        /// </summary>
        /// <param name="atlasWasAvailable">Whether the current frame prepared an atlas shadow resource.</param>
        protected virtual void UpdateShadowAtlasBindings(bool atlasWasAvailable) {
            var context = Device.ImmediateContext;
            if (atlasWasAvailable && ShadowAtlasResourcesValue != null) {
                context.PixelShader.SetShaderResource(1, ShadowAtlasResourcesValue.ShaderResourceView);
                context.PixelShader.SetSampler(1, ShadowAtlasResourcesValue.SamplerState);
            } else {
                context.PixelShader.SetShaderResource(1, null);
                context.PixelShader.SetSampler(1, null);
            }
        }

        /// <summary>
        /// Updates the pixel-shader point-shadow resource bindings for the current frame.
        /// </summary>
        /// <param name="pointShadowResourceCount">Number of point-shadow resources prepared for the current frame.</param>
        protected virtual void UpdatePointShadowBindings(int pointShadowResourceCount) {
            var context = Device.ImmediateContext;
            for (int slotIndex = 0; slotIndex < MaximumPointShadowTextureSlots; slotIndex++) {
                ShaderResourceView shaderResourceView = slotIndex < pointShadowResourceCount && slotIndex < PointShadowCubeResourcesValue.Count
                    ? PointShadowCubeResourcesValue[slotIndex].ShaderResourceView
                    : null;
                context.PixelShader.SetShaderResource(2 + slotIndex, shaderResourceView);
            }

            SamplerState samplerState = pointShadowResourceCount > 0 && PointShadowCubeResourcesValue.Count > 0
                ? PointShadowCubeResourcesValue[0].SamplerState
                : null;
            context.PixelShader.SetSampler(2, null);
            context.PixelShader.SetSampler(2, samplerState);
        }

        /// <summary>
        /// Renders point-light cube shadow resources for the current extracted camera frame.
        /// </summary>
        /// <param name="context">Execution context containing the current camera frame.</param>
        /// <param name="shadowResourceSet">Planned shadow resources for the current frame.</param>
        protected virtual void RenderPointShadowResources(DirectX11RenderPassExecutionContext context, DirectX11ShadowResourceSet shadowResourceSet) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            } else if (shadowResourceSet == null) {
                throw new ArgumentNullException(nameof(shadowResourceSet));
            }

            IReadOnlyList<DirectX11PointShadowCubeResources> pointShadowCubeResources = GetPointShadowCubeResources(shadowResourceSet);
            DirectX11ShaderPass pointShadowPass = GetPointShadowDepthShaderPass();
            var deviceContext = Device.ImmediateContext;
            deviceContext.Rasterizer.State = shadowRasterizerState3D;
            deviceContext.OutputMerger.SetDepthStencilState(depthStencilState3D, 0);
            deviceContext.OutputMerger.SetBlendState(null);
            deviceContext.InputAssembler.InputLayout = pointShadowPass.InputLayout;
            deviceContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            deviceContext.VertexShader.Set(pointShadowPass.VertexShader);
            deviceContext.PixelShader.Set(pointShadowPass.PixelShader);
            deviceContext.VertexShader.SetConstantBuffer(0, pointShadowDepthConstantBuffer);
            deviceContext.PixelShader.SetConstantBuffer(0, pointShadowDepthConstantBuffer);

            for (int resourceIndex = 0; resourceIndex < shadowResourceSet.PointShadowResources.Count; resourceIndex++) {
                DirectX11PointShadowResource pointShadowResource = shadowResourceSet.PointShadowResources[resourceIndex];
                PointLightComponent pointLight = (PointLightComponent)pointShadowResource.Light.Light;
                DirectX11PointShadowCubeResources cubeResources = pointShadowCubeResources[resourceIndex];
                for (int faceIndex = 0; faceIndex < 6; faceIndex++) {
                    deviceContext.OutputMerger.SetTargets(cubeResources.DepthStencilViews[faceIndex], cubeResources.RenderTargetViews[faceIndex]);
                    deviceContext.ClearDepthStencilView(cubeResources.DepthStencilViews[faceIndex], DepthStencilClearFlags.Depth, 1f, 0);
                    deviceContext.ClearRenderTargetView(cubeResources.RenderTargetViews[faceIndex], new RawColor4(1f, 0f, 0f, 1f));
                    deviceContext.Rasterizer.SetViewport(0, 0, cubeResources.Resolution, cubeResources.Resolution);
                    float4x4 lightViewProjection = GetShadowShaderDataBuilder().BuildPointShadowViewProjectionMatrix(pointLight, faceIndex);
                    for (int casterIndex = 0; casterIndex < context.Frame.ShadowCasterSubmissions.Count; casterIndex++) {
                        RenderFrameShadowCasterSubmission shadowCaster = context.Frame.ShadowCasterSubmissions[casterIndex];
                        if (shadowCaster == null) {
                            continue;
                        }

                        DrawPointShadowCaster(shadowCaster, lightViewProjection, pointLight.Parent.Position, pointLight.Range);
                    }
                }
            }
        }

        /// <summary>
        /// Retrieves the point-shadow cube resources for the current runtime slice, recreating them when the planned count or resolution changes.
        /// </summary>
        /// <param name="shadowResourceSet">Planned shadow resources for the current frame.</param>
        /// <returns>Point-shadow cube resources matching the planned point-shadow set.</returns>
        protected virtual IReadOnlyList<DirectX11PointShadowCubeResources> GetPointShadowCubeResources(DirectX11ShadowResourceSet shadowResourceSet) {
            if (shadowResourceSet == null) {
                throw new ArgumentNullException(nameof(shadowResourceSet));
            }

            while (PointShadowCubeResourcesValue.Count > shadowResourceSet.PointShadowResources.Count) {
                int lastIndex = PointShadowCubeResourcesValue.Count - 1;
                PointShadowCubeResourcesValue[lastIndex].Dispose();
                PointShadowCubeResourcesValue.RemoveAt(lastIndex);
            }

            for (int resourceIndex = 0; resourceIndex < shadowResourceSet.PointShadowResources.Count; resourceIndex++) {
                DirectX11PointShadowResource pointShadowResource = shadowResourceSet.PointShadowResources[resourceIndex];
                if (PointShadowCubeResourcesValue.Count <= resourceIndex) {
                    PointShadowCubeResourcesValue.Add(new DirectX11PointShadowCubeResources(Device, pointShadowResource.Resolution));
                    continue;
                }

                DirectX11PointShadowCubeResources cachedResources = PointShadowCubeResourcesValue[resourceIndex];
                if (cachedResources.Resolution == pointShadowResource.Resolution) {
                    continue;
                }

                cachedResources.Dispose();
                PointShadowCubeResourcesValue[resourceIndex] = new DirectX11PointShadowCubeResources(Device, pointShadowResource.Resolution);
            }

            return PointShadowCubeResourcesValue;
        }

        /// <summary>
        /// Retrieves the shadow atlas resources for the current runtime slice, recreating them when the planned dimensions change.
        /// </summary>
        /// <param name="shadowResourceSet">Planned shadow resources for the current frame.</param>
        /// <returns>Shadow atlas resources matching the planned atlas dimensions.</returns>
        protected virtual DirectX11ShadowAtlasResources GetShadowAtlasResources(DirectX11ShadowResourceSet shadowResourceSet) {
            if (shadowResourceSet == null) {
                throw new ArgumentNullException(nameof(shadowResourceSet));
            }

            if (shadowResourceSet.AtlasWidth <= 0 || shadowResourceSet.AtlasHeight <= 0) {
                return null;
            }

            if (ShadowAtlasResourcesValue != null
                && ShadowAtlasResourcesValue.Width == shadowResourceSet.AtlasWidth
                && ShadowAtlasResourcesValue.Height == shadowResourceSet.AtlasHeight) {
                return ShadowAtlasResourcesValue;
            }

            ShadowAtlasResourcesValue?.Dispose();
            ShadowAtlasResourcesValue = new DirectX11ShadowAtlasResources(Device, shadowResourceSet.AtlasWidth, shadowResourceSet.AtlasHeight);
            return ShadowAtlasResourcesValue;
        }

        /// <summary>
        /// Retrieves the shared depth-only shader pass used while rendering atlas shadows.
        /// </summary>
        /// <returns>Depth-only DirectX11 shader pass.</returns>
        protected virtual DirectX11ShaderPass GetShadowDepthShaderPass() {
            if (ShadowDepthShaderPassValue == null) {
                string shaderPath = DirectX11BuiltInShaderPathResolver.ResolveShaderPath("EditorShadowDepth.hlsl");
                ShadowDepthShaderPassValue = new DirectX11ShaderPass(Device, shaderPath, "VS", "PS");
            }

            return ShadowDepthShaderPassValue;
        }

        /// <summary>
        /// Retrieves the shared shader pass used while rendering point-shadow cube faces.
        /// </summary>
        /// <returns>Point-shadow depth DirectX11 shader pass.</returns>
        protected virtual DirectX11ShaderPass GetPointShadowDepthShaderPass() {
            if (PointShadowDepthShaderPassValue == null) {
                string shaderPath = DirectX11BuiltInShaderPathResolver.ResolveShaderPath("EditorPointShadowDepth.hlsl");
                PointShadowDepthShaderPassValue = new DirectX11ShaderPass(Device, shaderPath, "VS", "PS");
            }

            return PointShadowDepthShaderPassValue;
        }

        /// <summary>
        /// Renders atlas-backed shadow-caster depth for the current extracted camera frame.
        /// </summary>
        /// <param name="context">Execution context containing the current camera frame.</param>
        /// <param name="shadowResourceSet">Planned shadow resources for the current frame.</param>
        /// <param name="atlasResources">Atlas resources receiving shadow depth.</param>
        protected virtual void RenderShadowAtlas(
            DirectX11RenderPassExecutionContext context,
            DirectX11ShadowResourceSet shadowResourceSet,
            DirectX11ShadowAtlasResources atlasResources) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            } else if (shadowResourceSet == null) {
                throw new ArgumentNullException(nameof(shadowResourceSet));
            } else if (atlasResources == null) {
                throw new ArgumentNullException(nameof(atlasResources));
            }

            DirectX11ShaderPass shadowPass = GetShadowDepthShaderPass();
            var deviceContext = Device.ImmediateContext;
            deviceContext.OutputMerger.SetTargets(atlasResources.DepthStencilView, (RenderTargetView)null);
            deviceContext.ClearDepthStencilView(atlasResources.DepthStencilView, DepthStencilClearFlags.Depth, 1f, 0);
            deviceContext.Rasterizer.State = shadowRasterizerState3D;
            deviceContext.OutputMerger.SetDepthStencilState(depthStencilState3D, 0);
            deviceContext.OutputMerger.SetBlendState(null);
            deviceContext.InputAssembler.InputLayout = shadowPass.InputLayout;
            deviceContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            deviceContext.VertexShader.Set(shadowPass.VertexShader);
            deviceContext.PixelShader.Set(shadowPass.PixelShader);
            deviceContext.VertexShader.SetConstantBuffer(0, customPassConstantBuffer);
            deviceContext.PixelShader.SetConstantBuffer(0, customPassConstantBuffer);

            for (int allocationIndex = 0; allocationIndex < shadowResourceSet.AtlasAllocations.Count; allocationIndex++) {
                DirectX11ShadowAtlasAllocation allocation = shadowResourceSet.AtlasAllocations[allocationIndex];
                deviceContext.Rasterizer.SetViewport(allocation.X, allocation.Y, allocation.Width, allocation.Height);
                float4x4 lightViewProjection = GetShadowShaderDataBuilder().BuildShadowViewProjectionMatrix(context.Frame.Camera, allocation);
                for (int casterIndex = 0; casterIndex < context.Frame.ShadowCasterSubmissions.Count; casterIndex++) {
                    RenderFrameShadowCasterSubmission shadowCaster = context.Frame.ShadowCasterSubmissions[casterIndex];
                    if (shadowCaster == null) {
                        continue;
                    }

                    DrawShadowCaster(shadowCaster, lightViewProjection);
                }
            }
        }

        /// <summary>
        /// Draws one shadow-caster submission using the current depth-only shadow shader pass.
        /// </summary>
        /// <param name="submission">Shadow-caster submission to render into the shadow atlas.</param>
        /// <param name="lightViewProjection">Untransposed light view-projection matrix for the active atlas tile.</param>
        protected virtual void DrawShadowCaster(RenderFrameShadowCasterSubmission submission, float4x4 lightViewProjection) {
            if (submission?.Drawable?.Parent == null || !submission.Drawable.Parent.Enabled) {
                return;
            } else if (!ShouldMaterialCastShadows(submission.Material)) {
                return;
            }

            var deviceContext = Device.ImmediateContext;
            var data = (DirectX11ModelResource)submission.Drawable.Model;
            deviceContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(data.VertexBuffer, Utilities.SizeOf<VertexPositionNormalUV>(), 0));
            if (data.IndexBuffer != null && data.IndexCount > 0) {
                Format indexFormat = data.Uses32BitIndices ? Format.R32_UInt : Format.R16_UInt;
                deviceContext.InputAssembler.SetIndexBuffer(data.IndexBuffer, indexFormat, 0);
            }

            float4x4 world = BuildDrawableWorldMatrix(submission.Drawable);
            float4x4 worldLightViewProjection;
            float4x4.Multiply(ref world, ref lightViewProjection, out worldLightViewProjection);
            float4x4 worldLightViewProjectionTransposed;
            float4x4.Transpose(ref worldLightViewProjection, out worldLightViewProjectionTransposed);

            CustomEffectShaderData shadowData = new CustomEffectShaderData {
                worldViewProj = worldLightViewProjectionTransposed,
                color = new float4(0f, 0f, 0f, 0f)
            };
            deviceContext.UpdateSubresource(ref shadowData, customPassConstantBuffer);
            DrawSubmesh(data, ResolveSubmesh(data, submission.SubmeshIndex));
        }

        /// <summary>
        /// Draws one shadow-caster submission into the active point-shadow cube face.
        /// </summary>
        /// <param name="submission">Shadow-caster submission to render into the active point-shadow cube face.</param>
        /// <param name="lightViewProjection">Untransposed point-light view-projection matrix for the active cube face.</param>
        /// <param name="lightPosition">Point-light position in world space.</param>
        /// <param name="lightRange">Point-light effective range.</param>
        protected virtual void DrawPointShadowCaster(RenderFrameShadowCasterSubmission submission, float4x4 lightViewProjection, float3 lightPosition, float lightRange) {
            if (submission?.Drawable?.Parent == null || !submission.Drawable.Parent.Enabled) {
                return;
            } else if (!ShouldMaterialCastShadows(submission.Material)) {
                return;
            }

            var deviceContext = Device.ImmediateContext;
            var data = (DirectX11ModelResource)submission.Drawable.Model;
            deviceContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(data.VertexBuffer, Utilities.SizeOf<VertexPositionNormalUV>(), 0));
            if (data.IndexBuffer != null && data.IndexCount > 0) {
                Format indexFormat = data.Uses32BitIndices ? Format.R32_UInt : Format.R16_UInt;
                deviceContext.InputAssembler.SetIndexBuffer(data.IndexBuffer, indexFormat, 0);
            }

            float4x4 world = BuildDrawableWorldMatrix(submission.Drawable);
            float4x4 worldLightViewProjection;
            float4x4.Multiply(ref world, ref lightViewProjection, out worldLightViewProjection);
            float4x4 worldTransposed;
            float4x4.Transpose(ref world, out worldTransposed);
            float4x4 worldLightViewProjectionTransposed;
            float4x4.Transpose(ref worldLightViewProjection, out worldLightViewProjectionTransposed);

            DirectX11PointShadowDepthShaderData shadowData = new DirectX11PointShadowDepthShaderData {
                World = worldTransposed,
                WorldViewProj = worldLightViewProjectionTransposed,
                LightPositionAndRange = new float4(lightPosition.X, lightPosition.Y, lightPosition.Z, lightRange)
            };
            deviceContext.UpdateSubresource(ref shadowData, pointShadowDepthConstantBuffer);
            DrawSubmesh(data, ResolveSubmesh(data, submission.SubmeshIndex));
        }

        /// <summary>
        /// Builds the world matrix for one drawable from its parent transform.
        /// </summary>
        /// <param name="drawable">Drawable whose parent transform should be encoded.</param>
        /// <returns>World matrix for the drawable parent.</returns>
        protected virtual float4x4 BuildDrawableWorldMatrix(IDrawable3D drawable) {
            if (drawable?.Parent == null) {
                throw new ArgumentNullException(nameof(drawable));
            }

            float4 orientation = drawable.Parent.Orientation;
            float4x4 rotation;
            float4x4.CreateFromQuaternion(ref orientation, out rotation);
            float3 scale = drawable.Parent.Scale;
            float4x4 size;
            float4x4.CreateScale(scale.X, scale.Y, scale.Z, out size);
            float4x4 rotationScale;
            float4x4.Multiply(ref rotation, ref size, out rotationScale);
            float3 position = drawable.Parent.Position;
            float4x4 translation;
            float4x4.CreateTranslation(ref position, out translation);
            float4x4 world;
            float4x4.Multiply(ref rotationScale, ref translation, out world);
            return world;
        }

        /// <summary>
        /// Returns whether one value is within a small tolerance of an expected authored showcase value.
        /// </summary>
        /// <param name="value">Actual value to compare.</param>
        /// <param name="expectedValue">Expected authored showcase value.</param>
        /// <param name="tolerance">Maximum absolute difference allowed.</param>
        /// <returns>True when the value matches within the requested tolerance; otherwise false.</returns>
        bool IsApproximately(float value, float expectedValue, float tolerance) {
            return Math.Abs(value - expectedValue) <= tolerance;
        }

        /// <summary>
        /// Returns whether one entity matches the authored central directional-shadow plaza tower used for parity tracing.
        /// </summary>
        /// <param name="entity">Entity under inspection.</param>
        /// <returns>True when the entity transform is approximately equal to the central plaza tower; otherwise false.</returns>
        bool IsDirectionalShadowCentralTower(Entity entity) {
            if (entity == null) {
                return false;
            }

            float3 position = entity.Position;
            float3 scale = entity.Scale;
            return IsApproximately(position.X, 0f, 0.5f)
                && IsApproximately(position.Y, 9f, 0.5f)
                && IsApproximately(position.Z, -12f, 0.5f)
                && IsApproximately(scale.X, 7f, 0.5f)
                && IsApproximately(scale.Y, 18f, 0.5f)
                && IsApproximately(scale.Z, 7f, 0.5f);
        }

        /// <summary>
        /// Resolves the first active directional light currently registered in the object manager.
        /// </summary>
        /// <returns>First enabled directional light with an enabled parent entity, or null when none is active.</returns>
        DirectionalLightComponent ResolveActiveDirectionalLight() {
            if (Core.Instance?.ObjectManager?.DirectionalLights == null) {
                return null;
            }

            for (int index = 0; index < Core.Instance.ObjectManager.DirectionalLights.Count; index++) {
                DirectionalLightComponent directionalLight = Core.Instance.ObjectManager.DirectionalLights[index];
                if (directionalLight?.Parent == null || !directionalLight.Parent.IsHierarchyEnabled) {
                    continue;
                }

                return directionalLight;
            }

            return null;
        }

        /// <summary>
        /// Appends one parity trace line for the authored central plaza tower so DirectX11 face lighting can be compared against PSP.
        /// </summary>
        /// <param name="entity">Tower entity currently being rendered.</param>
        void WritePlazaTowerFaceDebugTrace(Entity entity) {
            float3 entityPosition = entity.Position;
            float3 entityScale = entity.Scale;
            if (plazaDrawableTraceCounter < 40) {
                plazaDrawableTraceCounter++;
                string drawableLine =
                    $"PlazaDrawableTrace position={entityPosition.X},{entityPosition.Y},{entityPosition.Z}" +
                    $" scale={entityScale.X},{entityScale.Y},{entityScale.Z}";
                File.AppendAllText(PlazaFaceTracePath, drawableLine + Environment.NewLine);
            }

            if (!IsDirectionalShadowCentralTower(entity)) {
                return;
            }

            DirectionalLightComponent directionalLight = ResolveActiveDirectionalLight();
            if (directionalLight?.Parent == null) {
                return;
            }

            plazaFaceTraceCounter++;
            if ((plazaFaceTraceCounter % 1) != 0) {
                return;
            }

            float3 cameraToTower = float3.Normalize(currentCameraPosition - entityPosition);
            float3 lightDirection = float3.Normalize(LightDirectionUtility.GetEntityForwardDirection(directionalLight.Parent)) * -1f;
            float4 entityOrientation = entity.Orientation;

            float3 positiveX = float4.RotateVector(new float3(1f, 0f, 0f), entityOrientation);
            float3 negativeX = float4.RotateVector(new float3(-1f, 0f, 0f), entityOrientation);
            float3 positiveZ = float4.RotateVector(new float3(0f, 0f, 1f), entityOrientation);
            float3 negativeZ = float4.RotateVector(new float3(0f, 0f, -1f), entityOrientation);

            string line =
                $"PlazaTowerFaceDebug position={entityPosition.X},{entityPosition.Y},{entityPosition.Z}" +
                $" scale={entityScale.X},{entityScale.Y},{entityScale.Z}" +
                $" cameraToTower={cameraToTower.X},{cameraToTower.Y},{cameraToTower.Z}" +
                $" light={lightDirection.X},{lightDirection.Y},{lightDirection.Z}" +
                $" pxView={float3.Dot(positiveX, cameraToTower)} pxLight={float3.Dot(positiveX, lightDirection)}" +
                $" nxView={float3.Dot(negativeX, cameraToTower)} nxLight={float3.Dot(negativeX, lightDirection)}" +
                $" pzView={float3.Dot(positiveZ, cameraToTower)} pzLight={float3.Dot(positiveZ, lightDirection)}" +
                $" nzView={float3.Dot(negativeZ, cameraToTower)} nzLight={float3.Dot(negativeZ, lightDirection)}";
            File.AppendAllText(PlazaFaceTracePath, line + Environment.NewLine);
        }

        /// <summary>
        /// Uploads the packed forward-light shader data to the DirectX11 pixel-shader constant-buffer slot.
        /// </summary>
        /// <param name="data">Packed forward-light shader data prepared for the current frame.</param>
        protected virtual void UpdateForwardLightShaderData(DirectX11ForwardLightShaderData data) {
            var context = Device.ImmediateContext;
            context.UpdateSubresource(ref data, forwardLightConstantBuffer);
            context.PixelShader.SetConstantBuffer(1, forwardLightConstantBuffer);
        }

        /// <summary>
        /// Executes one filtered geometry pass for the extracted frame.
        /// </summary>
        /// <param name="context">Execution context containing the frame and target surface.</param>
        /// <param name="transparentPass">Whether to draw transparent or opaque submissions.</param>
        void ExecuteGeometryPass(DirectX11RenderPassExecutionContext context, bool transparentPass) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            RenderFrame frame = context.Frame;
            for (int drawableIndex = 0; drawableIndex < frame.DrawableSubmissions.Count; drawableIndex++) {
                RenderFrameDrawableSubmission submission = frame.DrawableSubmissions[drawableIndex];
                if (submission == null || submission.IsTransparent != transparentPass) {
                    continue;
                }

                Visit(submission);
            }
        }

        /// <summary>
        /// Draws a single 3D drawable encountered during queue traversal.
        /// </summary>
        /// <param name="drawable">Drawable to render.</param>
        public void Visit(IDrawable3D drawable) {
            if (drawable == null) {
                throw new ArgumentNullException(nameof(drawable));
            }

            Visit(new RenderFrameDrawableSubmission(
                drawable,
                0,
                drawable.Material,
                false,
                new RenderFrameBatchingMetadata(false, false, false)));
        }

        /// <summary>
        /// Draws one extracted 3D submission encountered during queue traversal.
        /// </summary>
        /// <param name="submission">Drawable submission to render.</param>
        protected virtual void Visit(RenderFrameDrawableSubmission submission) {
            if (submission?.Drawable?.Parent == null || !submission.Drawable.Parent.Enabled) {
                return;
            }

            var context = Device.ImmediateContext;
            IDrawable3D drawable = submission.Drawable;
            RuntimeMaterial runtimeMaterial = submission.Material;
            if (!isCustomPassActive) {
                if (runtimeMaterial == null) {
                    DirectX11MaterialResource missingMaterial = GetMissingMaterial();
                    ApplyMaterial(missingMaterial, missingMaterial);
                } else {
                    ShaderRuntimeMaterial shaderRuntimeMaterial = RequireShaderRuntimeMaterial(runtimeMaterial);
                    DirectX11MaterialResource directX11Material = ResolveDirectX11Material(shaderRuntimeMaterial);
                    ApplyMaterial(directX11Material, shaderRuntimeMaterial);
                }
            }

            Entity parent = drawable.Parent;
            WritePlazaTowerFaceDebugTrace(parent);
            var data = (DirectX11ModelResource)drawable.Model;

            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(data.VertexBuffer, Utilities.SizeOf<VertexPositionNormalUV>(), 0));
            if (data.IndexBuffer != null && data.IndexCount > 0) {
                Format indexFormat = data.Uses32BitIndices ? Format.R32_UInt : Format.R16_UInt;
                context.InputAssembler.SetIndexBuffer(data.IndexBuffer, indexFormat, 0);
            }

            float4 orientation = parent.Orientation;
            float4x4 rotation;
            float4x4.CreateFromQuaternion(ref orientation, out rotation);

            float3 scale = parent.Scale;
            float4x4 size;
            float4x4.CreateScale(scale.X, scale.Y, scale.Z, out size);

            float4x4 rotationScale;
            float4x4.Multiply(ref rotation, ref size, out rotationScale);

            float3 position = parent.Position;
            float4x4 translation;
            float4x4.CreateTranslation(ref position, out translation);

            float4x4 world;
            float4x4.Multiply(ref rotationScale, ref translation, out world);

            float4x4 worldViewProj;
            float4x4.Multiply(ref world, ref currentViewProjection, out worldViewProj);

            float4x4 worldTransposed;
            float4x4.Transpose(ref world, out worldTransposed);
            float4x4 worldViewProjTransposed;
            float4x4.Transpose(ref worldViewProj, out worldViewProjTransposed);

            if (isCustomPassActive) {
                if (customColorProvider == null) {
                    throw new InvalidOperationException("Custom pass color provider must be set before rendering.");
                }

                byte4 customColor = customColorProvider(drawable);
                var customData = new CustomEffectShaderData {
                    worldViewProj = worldViewProjTransposed,
                    color = new float4(customColor.X / 255f, customColor.Y / 255f, customColor.Z / 255f, customColor.W / 255f)
                };
                context.UpdateSubresource(ref customData, customPassConstantBuffer);
            } else {
                ShaderRuntimeMaterial rootMaterial = RequireShaderRuntimeMaterial(runtimeMaterial.ResolveRootMaterial());
                if (BuiltInMaterialIds.UsesStandardMeshTransform(
                    rootMaterial.Id,
                    rootMaterial.Layout.ShaderAssetId,
                    rootMaterial.Layout.VertexProgram,
                    rootMaterial.Layout.PixelProgram)) {
                    StandardMeshShaderData standardData = BuildStandardMeshShaderData(world, currentCameraPosition, runtimeMaterial.ReceivesShadows);
                    standardData.World = worldTransposed;
                    standardData.WorldViewProj = worldViewProjTransposed;
                    context.UpdateSubresource(ref standardData, constantBuffer);
                } else {
                    context.UpdateSubresource(ref worldViewProjTransposed, constantBuffer);
                }
            }

            DrawSubmesh(data, ResolveSubmesh(data, submission.SubmeshIndex));
        }

        /// <summary>
        /// Builds the packed transform payload consumed by the built-in standard mesh shader.
        /// </summary>
        /// <param name="world">World transform for the current draw.</param>
        /// <param name="cameraPosition">World-space camera position for the current draw.</param>
        /// <param name="receivesShadows">Whether the current material should sample forward shadows.</param>
        /// <returns>Standard-mesh shader data configured for one draw.</returns>
        static StandardMeshShaderData BuildStandardMeshShaderData(float4x4 world, float3 cameraPosition, bool receivesShadows) {
            float4x4.InverseTranspose(ref world, out float4x4 inverseTransposeNormalMatrix);

            return new StandardMeshShaderData {
                World = default,
                WorldViewProj = default,
                NormalMatrix = inverseTransposeNormalMatrix,
                CameraPosition = new float4(cameraPosition.X, cameraPosition.Y, cameraPosition.Z, 0f),
                MaterialFlags = new float4(receivesShadows ? 1f : 0f, 0f, 0f, 0f)
            };
        }

        /// <summary>
        /// Resolves one runtime submesh from the supplied model resource.
        /// </summary>
        /// <param name="model">Model resource that owns the submesh ranges.</param>
        /// <param name="submeshIndex">Zero-based submesh index to resolve.</param>
        /// <returns>Resolved runtime submesh.</returns>
        protected virtual RuntimeSubmesh ResolveSubmesh(DirectX11ModelResource model, int submeshIndex) {
            if (model == null) {
                throw new ArgumentNullException(nameof(model));
            } else if (submeshIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(submeshIndex), "Submesh index must be non-negative.");
            }

            if (model.Submeshes != null && submeshIndex < model.Submeshes.Length) {
                return model.Submeshes[submeshIndex];
            }

            return new RuntimeSubmesh {
                MaterialSlotName = string.Empty,
                IndexStart = 0,
                IndexCount = model.IndexBuffer != null && model.IndexCount > 0
                    ? model.IndexCount
                    : model.VertexCount
            };
        }

        /// <summary>
        /// Draws one resolved submesh from the currently bound model resource.
        /// </summary>
        /// <param name="model">Model resource currently bound to the input assembler.</param>
        /// <param name="submesh">Resolved submesh range to draw.</param>
        protected virtual void DrawSubmesh(DirectX11ModelResource model, RuntimeSubmesh submesh) {
            if (model == null) {
                throw new ArgumentNullException(nameof(model));
            } else if (submesh == null) {
                throw new ArgumentNullException(nameof(submesh));
            }

            var context = Device.ImmediateContext;
            if (model.IndexBuffer != null && model.IndexCount > 0) {
                context.DrawIndexed(submesh.IndexCount, submesh.IndexStart, 0);
            } else {
                context.Draw(submesh.IndexCount, submesh.IndexStart);
            }
        }


        /// <summary>
        /// Sets the rounded-rectangle rendering backend for UI shapes.
        /// </summary>
        /// <param name="backend">Backend to use for rounded rectangles.</param>
        public void SetRoundedRectBackend(RoundedRectBackend backend) {
            renderer2D.SetRoundedRectBackend(backend);
        }

        /// <summary>
        /// Increments the per-frame draw call counter.
        /// </summary>
        /// <param name="count">Number of draw calls to add.</param>
        internal void IncrementDrawCalls(int count) {
            drawCallsThisFrame += count;
        }

        /// <summary>
        /// Executes the full render pass for all windows and cameras.
        /// </summary>
        public override void Draw() {
            base.Draw();

            if (surfaces.Count == 0) {
                return;
            }

            UpdateFrameStats();

            RenderCustomPasses();

            var cameras = Core.Instance.ObjectManager.Cameras;

            for (int i = 0; i < surfaces.Count; i++) {
                var surface = surfaces[i];

                for (int j = 0; j < cameras.Count; j++) {
                    ICamera camera = cameras[j];
                    if (camera is not CameraComponent cameraComponent) {
                        throw new InvalidOperationException("DirectX11 rendering requires camera entries to be CameraComponent instances.");
                    }

                    RenderCamera(surface, cameraComponent);
                }

                surface.SwapChain.Present(0, PresentFlags.None);
            }
        }

        /// <summary>
        /// Retrieves the shared frame-extraction service, creating it lazily when necessary.
        /// </summary>
        /// <returns>Shared render-frame extraction service.</returns>
        RenderFrameExtractionService GetFrameExtractionService() {
            if (FrameExtractionServiceValue == null) {
                FrameExtractionServiceValue = new RenderFrameExtractionService();
            }

            return FrameExtractionServiceValue;
        }

        /// <summary>
        /// Retrieves the shared DirectX11 render-plan builder, creating it lazily when necessary.
        /// </summary>
        /// <returns>Shared DirectX11 render-plan builder.</returns>
        DirectX11RenderPlanBuilder GetRenderPlanBuilder() {
            if (RenderPlanBuilderValue == null) {
                RenderPlanBuilderValue = new DirectX11RenderPlanBuilder();
            }

            return RenderPlanBuilderValue;
        }

        /// <summary>
        /// Retrieves the shared DirectX11 render-plan executor, creating it lazily when necessary.
        /// </summary>
        /// <returns>Shared DirectX11 render-plan executor.</returns>
        DirectX11RenderPlanExecutor GetRenderPlanExecutor() {
            if (RenderPlanExecutorValue == null) {
                RenderPlanExecutorValue = new DirectX11RenderPlanExecutor(true, false);
            }

            return RenderPlanExecutorValue;
        }

        /// <summary>
        /// Retrieves the shared forward-light shader-data builder, creating it lazily when necessary.
        /// </summary>
        /// <returns>Shared forward-light shader-data builder.</returns>
        DirectX11ForwardLightShaderDataBuilder GetForwardLightShaderDataBuilder() {
            if (ForwardLightShaderDataBuilderValue == null) {
                ForwardLightShaderDataBuilderValue = new DirectX11ForwardLightShaderDataBuilder();
            }

            return ForwardLightShaderDataBuilderValue;
        }

        /// <summary>
        /// Retrieves the shared DirectX11 light-selection service, creating it lazily when necessary.
        /// </summary>
        /// <returns>Shared DirectX11 light-selection service.</returns>
        DirectX11LightSelectionService GetLightSelectionService() {
            if (LightSelectionServiceValue == null) {
                LightSelectionServiceValue = new DirectX11LightSelectionService();
            }

            return LightSelectionServiceValue;
        }

        /// <summary>
        /// Copies one ordered camera render queue into an extraction-ready snapshot.
        /// </summary>
        /// <param name="renderQueue">Ordered render queue to snapshot.</param>
        /// <returns>Snapshot of the ordered queue contents.</returns>
        IDrawable3D[] SnapshotRenderQueue(IRenderQueue3D renderQueue) {
            if (renderQueue == null) {
                throw new ArgumentNullException(nameof(renderQueue));
            }

            DirectX11RenderQueueSnapshotVisitor snapshotVisitor = RenderQueueSnapshotVisitorValue;
            if (snapshotVisitor == null) {
                snapshotVisitor = new DirectX11RenderQueueSnapshotVisitor();
                RenderQueueSnapshotVisitorValue = snapshotVisitor;
            }

            snapshotVisitor.Reset(renderQueue.Count);
            renderQueue.VisitOrdered(snapshotVisitor);
            return snapshotVisitor.CreateSnapshot();
        }

        /// <summary>
        /// Copies the visible authored lights relevant to one camera into an extraction-ready snapshot.
        /// </summary>
        /// <param name="camera">Camera whose matching visible lights should be gathered.</param>
        /// <returns>Snapshot of visible light components relevant to the camera.</returns>
        LightComponent[] SnapshotVisibleLights(CameraComponent camera) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }

            List<LightComponent> lights = new List<LightComponent>();
            List<Entity> entities = Core.Instance.ObjectManager.Entities;
            for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++) {
                Entity entity = entities[entityIndex];
                if (entity == null || !entity.IsHierarchyEnabled) {
                    continue;
                } else if ((entity.LayerMask & camera.LayerMask) == 0) {
                    continue;
                } else if (entity.Components == null) {
                    continue;
                }

                for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                    if (entity.Components[componentIndex] is LightComponent light) {
                        lights.Add(light);
                    }
                }
            }

            return lights.ToArray();
        }

        /// <summary>
        /// Updates FPS and draw call statistics for the current frame.
        /// </summary>
        void UpdateFrameStats() {
            lastDrawCalls = drawCallsThisFrame;
            drawCallsThisFrame = 0;

            double ms = frameStopwatch.Elapsed.TotalMilliseconds;
            lastFrameTimeMs = ms;
            lastFps = ms > 0 ? 1000.0 / ms : 0;
            frameStopwatch.Restart();
        }

        /// <summary>
        /// Creates a blend state configured for alpha blending.
        /// </summary>
        /// <param name="srcBlend">Source blend factor.</param>
        /// <param name="destBlend">Destination blend factor.</param>
        /// <param name="blendOp">Blend operation.</param>
        /// <returns>Blend state instance.</returns>
        BlendState CreateBlendState(BlendOption srcBlend, BlendOption destBlend, BlendOperation blendOp) {
            var blendStateDesc = new BlendStateDescription {
                AlphaToCoverageEnable = false,
                IndependentBlendEnable = false
            };

            blendStateDesc.RenderTarget[0] = new RenderTargetBlendDescription {
                IsBlendEnabled = true,
                SourceBlend = srcBlend,
                DestinationBlend = destBlend,
                BlendOperation = blendOp,
                SourceAlphaBlend = BlendOption.One,
                DestinationAlphaBlend = BlendOption.Zero,
                AlphaBlendOperation = BlendOperation.Add,
                RenderTargetWriteMask = ColorWriteMaskFlags.All
            };

            return new BlendState(Device, blendStateDesc);
        }

        /// <summary>
        /// Creates the sampler used by textured 3D materials.
        /// </summary>
        /// <returns>Configured sampler state.</returns>
        SamplerState CreateMaterialTextureSampler() {
            var samplerDesc = new SamplerStateDescription {
                Filter = Filter.MinMagMipPoint,
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp,
                ComparisonFunction = Comparison.Never,
                MinimumLod = 0,
                MaximumLod = float.MaxValue
            };

            return new SamplerState(Device, samplerDesc);
        }

        /// <summary>
        /// Applies a material to the DirectX11 pipeline if it is not already active.
        /// </summary>
        /// <param name="shaderMaterial">Concrete DirectX11 material that owns the shader resources.</param>
        /// <param name="material">Resolved runtime material instance that provides render-state and texture values.</param>
        void ApplyMaterial(DirectX11MaterialResource shaderMaterial, ShaderRuntimeMaterial material) {
            if (shaderMaterial == null) {
                throw new ArgumentNullException(nameof(shaderMaterial));
            }

            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            DirectX11ShaderResource shaderResource = shaderMaterial.ShaderResource;
            var context = Device.ImmediateContext;
            if (!ReferenceEquals(ActiveMaterial, shaderMaterial)) {
                context.InputAssembler.InputLayout = shaderResource.InputLayout;
                context.VertexShader.Set(shaderResource.VertexShader);
                context.PixelShader.Set(shaderResource.PixelShader);
                ActiveMaterial = shaderMaterial;
            }

            ApplyMaterialRenderState(material.RenderState);
            ApplyMaterialConstantBufferBindings(material);
            if (material.Layout.TextureBindings.Length > 0) {
                ShaderResourceView resourceView = ResolveMaterialTextureResourceView(material);
                context.PixelShader.SetShaderResource(0, resourceView);
                context.PixelShader.SetSampler(0, materialTextureSampler);
            } else {
                context.PixelShader.SetShaderResource(0, null);
                context.PixelShader.SetSampler(0, null);
            }
        }

        /// <summary>
        /// Applies per-material constant-buffer payloads for the current draw.
        /// </summary>
        /// <param name="material">Resolved runtime material instance that provides constant-buffer values.</param>
        void ApplyMaterialConstantBufferBindings(ShaderRuntimeMaterial material) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            var context = Device.ImmediateContext;
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
        List<DirectX11MaterialConstantBufferBinding> ResolveMaterialConstantBufferBindings(ShaderRuntimeMaterial material) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            var resolvedBindings = new List<DirectX11MaterialConstantBufferBinding>();
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
        /// Resolves the DirectX11 root material that owns the concrete shader resource for one runtime material chain.
        /// </summary>
        /// <param name="runtimeMaterial">Runtime material whose root should be resolved.</param>
        /// <returns>Resolved DirectX11 root material.</returns>
        DirectX11MaterialResource ResolveDirectX11Material(ShaderRuntimeMaterial runtimeMaterial) {
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
        bool ShouldMaterialCastShadows(RuntimeMaterial runtimeMaterial) {
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
        ShaderResourceView ResolveMaterialTextureResourceView(ShaderRuntimeMaterial material) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            RuntimeTexture runtimeTexture = material.ResolveTexture();
            if (runtimeTexture == null) {
                return null;
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
        ShaderRuntimeMaterial RequireShaderRuntimeMaterial(RuntimeMaterial runtimeMaterial) {
            if (runtimeMaterial == null) {
                throw new ArgumentNullException(nameof(runtimeMaterial));
            }
            if (runtimeMaterial is not ShaderRuntimeMaterial shaderRuntimeMaterial) {
                throw new InvalidOperationException("DirectX11 rendering requires shader-backed runtime materials.");
            }

            return shaderRuntimeMaterial;
        }

        /// <summary>
        /// Disposes cached DirectX11 material constant buffers.
        /// </summary>
        void DisposeMaterialConstantBuffers() {
            if (MaterialConstantBuffersBySlot == null) {
                return;
            }

            foreach (KeyValuePair<int, Buffer> pair in MaterialConstantBuffersBySlot) {
                pair.Value?.Dispose();
            }

            MaterialConstantBuffersBySlot.Clear();
        }

        /// <summary>
        /// Gets the fallback material used for drawables without materials.
        /// </summary>
        /// <returns>Missing-material runtime material.</returns>
        DirectX11MaterialResource GetMissingMaterial() {
            if (MissingMaterial != null) {
                return MissingMaterial;
            }

            DirectX11ShaderResource shaderResource = BuildMissingMaterialShaderResource();
            MissingMaterial = new DirectX11MaterialResource(shaderResource);
            return MissingMaterial;
        }

        /// <summary>
        /// Rebuilds a material layout from a hot-reloaded shader asset while preserving the material's current render state.
        /// </summary>
        /// <param name="material">Runtime material whose layout is being rebuilt.</param>
        /// <param name="shaderAsset">Updated shader metadata.</param>
        /// <returns>Rebuilt material layout.</returns>
        MaterialLayout BuildMaterialLayout(DirectX11MaterialResource material, ShaderAsset shaderAsset) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            } else if (shaderAsset == null) {
                throw new ArgumentNullException(nameof(shaderAsset));
            }

            var materialAsset = new MaterialAsset {
                ShaderAssetId = material.ShaderAssetId,
                VertexProgram = material.VertexProgram,
                PixelProgram = material.PixelProgram,
                Variant = material.Variant,
                RenderState = material.RenderState
            };

            return MaterialLayoutBuilder.Build(materialAsset, shaderAsset);
        }

        /// <summary>
        /// Applies material-defined fixed-function render state to the DirectX11 pipeline.
        /// </summary>
        /// <param name="renderState">Material render state to bind.</param>
        void ApplyMaterialRenderState(MaterialRenderState renderState) {
            if (renderState == null) {
                throw new ArgumentNullException(nameof(renderState));
            }

            var context = Device.ImmediateContext;
            RasterizerState rasterizerState = ResolveRasterizerState(renderState);
            if (!ReferenceEquals(ActiveRasterizerState, rasterizerState)) {
                context.Rasterizer.State = rasterizerState;
                ActiveRasterizerState = rasterizerState;
            }

            DepthStencilState depthStencilState = ResolveDepthStencilState(renderState);
            if (!ReferenceEquals(ActiveDepthStencilState, depthStencilState)) {
                context.OutputMerger.SetDepthStencilState(depthStencilState, 0);
                ActiveDepthStencilState = depthStencilState;
            }

            BlendState resolvedBlendState = ResolveBlendState(renderState);
            if (!ReferenceEquals(ActiveBlendState, resolvedBlendState)) {
                context.OutputMerger.SetBlendState(resolvedBlendState);
                ActiveBlendState = resolvedBlendState;
            }
        }

        /// <summary>
        /// Resolves the DirectX11 rasterizer state required by one material render state.
        /// </summary>
        /// <param name="renderState">Material render state to translate.</param>
        /// <returns>Rasterizer state to bind.</returns>
        RasterizerState ResolveRasterizerState(MaterialRenderState renderState) {
            if (renderState == null) {
                throw new ArgumentNullException(nameof(renderState));
            } else if (renderState.CullMode == MaterialCullMode.Back) {
                return rasterizerState3D;
            }

            int key = MaterialRenderStateKeyBuilder.Build(renderState);
            if (RasterizerStateCache.TryGetValue(key, out RasterizerState cachedState)) {
                return cachedState;
            }

            CullMode cullMode;
            if (renderState.CullMode == MaterialCullMode.None) {
                cullMode = CullMode.None;
            } else if (renderState.CullMode == MaterialCullMode.Front) {
                cullMode = CullMode.Front;
            } else if (renderState.CullMode == MaterialCullMode.Back) {
                cullMode = CullMode.Back;
            } else {
                throw new InvalidOperationException($"Unsupported material cull mode '{renderState.CullMode}'.");
            }

            var rasterizerDesc = new RasterizerStateDescription {
                CullMode = cullMode,
                FillMode = FillMode.Solid,
                IsFrontCounterClockwise = true,
                IsDepthClipEnabled = true
            };
            RasterizerState state = new RasterizerState(Device, rasterizerDesc);
            RasterizerStateCache[key] = state;
            return state;
        }

        /// <summary>
        /// Resolves the DirectX11 depth-stencil state required by one material render state.
        /// </summary>
        /// <param name="renderState">Material render state to translate.</param>
        /// <returns>Depth-stencil state to bind.</returns>
        DepthStencilState ResolveDepthStencilState(MaterialRenderState renderState) {
            if (renderState == null) {
                throw new ArgumentNullException(nameof(renderState));
            } else if (renderState.DepthTestEnabled && renderState.DepthWriteEnabled) {
                return depthStencilState3D;
            }

            int key = MaterialRenderStateKeyBuilder.Build(renderState);
            if (DepthStencilStateCache.TryGetValue(key, out DepthStencilState cachedState)) {
                return cachedState;
            }

            var depthStencilDesc = new DepthStencilStateDescription {
                IsDepthEnabled = renderState.DepthTestEnabled,
                DepthWriteMask = renderState.DepthWriteEnabled ? DepthWriteMask.All : DepthWriteMask.Zero,
                DepthComparison = Comparison.Less
            };
            DepthStencilState state = new DepthStencilState(Device, depthStencilDesc);
            DepthStencilStateCache[key] = state;
            return state;
        }

        /// <summary>
        /// Resolves the DirectX11 blend state required by one material render state.
        /// </summary>
        /// <param name="renderState">Material render state to translate.</param>
        /// <returns>Blend state to bind, or <c>null</c> for opaque output.</returns>
        BlendState ResolveBlendState(MaterialRenderState renderState) {
            if (renderState == null) {
                throw new ArgumentNullException(nameof(renderState));
            } else if (renderState.BlendMode == MaterialBlendMode.Opaque) {
                return null;
            } else if (renderState.BlendMode == MaterialBlendMode.AlphaBlend) {
                return blendState;
            }

            throw new InvalidOperationException($"Unsupported material blend mode '{renderState.BlendMode}'.");
        }

        /// <summary>
        /// Builds the shader resource used for missing-material rendering.
        /// </summary>
        /// <returns>Compiled shader resource.</returns>
        DirectX11ShaderResource BuildMissingMaterialShaderResource() {
            string shaderPath = ResolveBuiltInShaderPath(MissingMaterialShaderFileName);
            if (!File.Exists(shaderPath)) {
                throw new FileNotFoundException("Missing-material shader was not found.", shaderPath);
            }

            byte[] vertexBytecode = CompileShaderBytecode(shaderPath, DefaultCustomVertexEntry, "vs_4_0");
            byte[] pixelBytecode = CompileShaderBytecode(shaderPath, DefaultCustomPixelEntry, "ps_4_0");

            string shaderName = Path.GetFileNameWithoutExtension(shaderPath);
            if (string.IsNullOrWhiteSpace(shaderName)) {
                throw new InvalidOperationException("Missing-material shader name could not be resolved.");
            }

            return new DirectX11ShaderResource(
                Device,
                vertexBytecode,
                pixelBytecode,
                VertexPositionNormalUV.Elements,
                string.Concat(shaderName, ".vs"),
                string.Concat(shaderName, ".ps"),
                "default");
        }

        /// <summary>
        /// Compiles HLSL source into shader bytecode.
        /// </summary>
        /// <param name="shaderPath">Path to the HLSL shader file.</param>
        /// <param name="entryPoint">Entry point to compile.</param>
        /// <param name="profile">Shader profile to target.</param>
        /// <returns>Compiled shader bytecode.</returns>
        byte[] CompileShaderBytecode(string shaderPath, string entryPoint, string profile) {
            if (string.IsNullOrWhiteSpace(shaderPath)) {
                throw new ArgumentException("Shader path must be provided.", nameof(shaderPath));
            }

            if (string.IsNullOrWhiteSpace(entryPoint)) {
                throw new ArgumentException("Shader entry point must be provided.", nameof(entryPoint));
            }

            if (string.IsNullOrWhiteSpace(profile)) {
                throw new ArgumentException("Shader profile must be provided.", nameof(profile));
            }

            using (CompilationResult result = DirectX11ShaderSourceCompiler.CompileFromContent(shaderPath, entryPoint, profile)) {
                if (result == null) {
                    throw new InvalidOperationException("Shader compilation produced no result.");
                }

                if (result.Bytecode == null) {
                    throw new InvalidOperationException("Shader compilation produced no bytecode.");
                }

                return result.Bytecode.Data;
            }
        }

        /// <summary>
        /// Resolves the absolute path to a built-in shader file.
        /// </summary>
        /// <param name="shaderFileName">Shader file name to resolve.</param>
        /// <returns>Absolute shader path.</returns>
        string ResolveBuiltInShaderPath(string shaderFileName) {
            if (string.IsNullOrWhiteSpace(shaderFileName)) {
                throw new ArgumentException("Shader file name must be provided.", nameof(shaderFileName));
            }

            string baseDirectory = AppContext.BaseDirectory;
            if (string.IsNullOrWhiteSpace(baseDirectory)) {
                throw new InvalidOperationException("Base directory could not be resolved.");
            }

            string shaderPath = Path.Combine(baseDirectory, "shaders", shaderFileName);
            return Path.GetFullPath(shaderPath);
        }

        /// <summary>
        /// Releases resources owned by the missing-material fallback.
        /// </summary>
        void DisposeMissingMaterial() {
            if (MissingMaterial == null) {
                return;
            }

            MissingMaterial.ShaderResource.Dispose();
            MissingMaterial = null;
        }

        /// <summary>
        /// Retrieves a shader resource for the requested material and shader assets.
        /// </summary>
        /// <param name="materialAsset">Material asset that defines program selections.</param>
        /// <param name="shaderAsset">Shader asset containing compiled binaries.</param>
        /// <returns>Compiled DirectX11 shader resource.</returns>
        DirectX11ShaderResource GetShaderResource(MaterialAsset materialAsset, ShaderAsset shaderAsset) {
            if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            }

            if (shaderAsset == null) {
                throw new ArgumentNullException(nameof(shaderAsset));
            }

            if (string.IsNullOrWhiteSpace(materialAsset.VertexProgram)) {
                throw new InvalidOperationException("Material assets must define a vertex program name.");
            }

            if (string.IsNullOrWhiteSpace(materialAsset.PixelProgram)) {
                throw new InvalidOperationException("Material assets must define a pixel program name.");
            }

            if (string.IsNullOrWhiteSpace(materialAsset.Variant)) {
                throw new InvalidOperationException("Material assets must define a shader variant.");
            }

            if (shaderAsset.Binaries == null || shaderAsset.Binaries.Length == 0) {
                throw new InvalidOperationException("Shader assets must include compiled binaries.");
            }

            string targetName = ShaderTargetNames.GetTargetName(ShaderCompileTarget.DirectX11);
            if (!string.Equals(shaderAsset.TargetName, targetName, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("Shader asset target does not match the DirectX11 renderer.");
            }

            return GetShaderResource(
                materialAsset.ShaderAssetId,
                materialAsset.VertexProgram,
                materialAsset.PixelProgram,
                materialAsset.Variant,
                shaderAsset);
        }

        /// <summary>
        /// Locates a shader binary entry for the requested program, stage, and variant.
        /// </summary>
        /// <param name="shaderAsset">Shader asset containing binary data.</param>
        /// <param name="programName">Program name to locate.</param>
        /// <param name="stage">Shader stage to locate.</param>
        /// <param name="variant">Variant name to locate.</param>
        /// <returns>Matching shader binary asset.</returns>
        ShaderBinaryAsset GetShaderBinary(ShaderAsset shaderAsset, string programName, ShaderStage stage, string variant) {
            ShaderBinaryAsset[] binaries = shaderAsset.Binaries;
            for (int i = 0; i < binaries.Length; i++) {
                ShaderBinaryAsset binary = binaries[i];
                if (binary == null) {
                    continue;
                }

                if (!string.Equals(binary.TargetName, shaderAsset.TargetName, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                if (!string.Equals(binary.ProgramName, programName, StringComparison.Ordinal)) {
                    continue;
                }

                if (binary.Stage != stage) {
                    continue;
                }

                if (!string.Equals(binary.Variant, variant, StringComparison.Ordinal)) {
                    continue;
                }

                if (binary.Bytecode == null || binary.Bytecode.Length == 0) {
                    throw new InvalidOperationException("Shader binary does not include bytecode.");
                }

                return binary;
            }

            throw new InvalidOperationException("Shader binary was not found for the requested program.");
        }

        /// <summary>
        /// Builds a cache key for a compiled shader resource.
        /// </summary>
        /// <param name="shaderAssetId">Shader asset identifier.</param>
        /// <param name="vertexProgram">Vertex program name.</param>
        /// <param name="pixelProgram">Pixel program name.</param>
        /// <param name="variant">Variant name.</param>
        /// <returns>Composite cache key.</returns>
        string GetShaderResourceCacheKey(string shaderAssetId, string vertexProgram, string pixelProgram, string variant) {
            if (string.IsNullOrWhiteSpace(shaderAssetId)) {
                throw new InvalidOperationException("Shader asset id must be provided.");
            }

            return string.Concat(shaderAssetId, "|", vertexProgram, "|", pixelProgram, "|", variant);
        }

        /// <summary>
        /// Retrieves a shader resource using explicit program names and variant.
        /// </summary>
        /// <param name="shaderAssetId">Shader asset identifier.</param>
        /// <param name="vertexProgram">Vertex program name.</param>
        /// <param name="pixelProgram">Pixel program name.</param>
        /// <param name="variant">Variant name.</param>
        /// <param name="shaderAsset">Shader asset containing compiled binaries.</param>
        /// <returns>Compiled DirectX11 shader resource.</returns>
        DirectX11ShaderResource GetShaderResource(
            string shaderAssetId,
            string vertexProgram,
            string pixelProgram,
            string variant,
            ShaderAsset shaderAsset) {
            if (string.IsNullOrWhiteSpace(vertexProgram)) {
                throw new InvalidOperationException("Material assets must define a vertex program name.");
            }

            if (string.IsNullOrWhiteSpace(pixelProgram)) {
                throw new InvalidOperationException("Material assets must define a pixel program name.");
            }

            if (string.IsNullOrWhiteSpace(variant)) {
                throw new InvalidOperationException("Material assets must define a shader variant.");
            }

            if (shaderAsset == null) {
                throw new ArgumentNullException(nameof(shaderAsset));
            }

            if (shaderAsset.Binaries == null || shaderAsset.Binaries.Length == 0) {
                throw new InvalidOperationException("Shader assets must include compiled binaries.");
            }

            string targetName = ShaderTargetNames.GetTargetName(ShaderCompileTarget.DirectX11);
            if (!string.Equals(shaderAsset.TargetName, targetName, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("Shader asset target does not match the DirectX11 renderer.");
            }

            string cacheKey = GetShaderResourceCacheKey(shaderAssetId, vertexProgram, pixelProgram, variant);
            if (ShaderResourceCache.TryGetValue(cacheKey, out DirectX11ShaderResource cachedResource)) {
                return cachedResource;
            }

            ShaderBinaryAsset vertexBinary = GetShaderBinary(shaderAsset, vertexProgram, ShaderStage.Vertex, variant);
            ShaderBinaryAsset pixelBinary = GetShaderBinary(shaderAsset, pixelProgram, ShaderStage.Pixel, variant);

            var shaderResource = new DirectX11ShaderResource(
                Device,
                vertexBinary.Bytecode,
                pixelBinary.Bytecode,
                VertexPositionNormalUV.Elements,
                vertexProgram,
                pixelProgram,
                variant);

            ShaderResourceCache[cacheKey] = shaderResource;
            return shaderResource;
        }

        /// <summary>
        /// Registers a runtime material for shader hot reload updates.
        /// </summary>
        /// <param name="material">Material to register.</param>
        void RegisterMaterial(DirectX11MaterialResource material) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            string shaderAssetId = material.ShaderAssetId;
            if (string.IsNullOrWhiteSpace(shaderAssetId)) {
                return;
            }

            if (!MaterialsByShaderAssetId.TryGetValue(shaderAssetId, out List<DirectX11MaterialResource> materials)) {
                materials = new List<DirectX11MaterialResource>();
                MaterialsByShaderAssetId[shaderAssetId] = materials;
            }

            materials.Add(material);
        }

        /// <summary>
        /// Removes cached shader resources for a shader asset id.
        /// </summary>
        /// <param name="shaderAssetId">Shader asset identifier.</param>
        void InvalidateShaderCache(string shaderAssetId) {
            if (string.IsNullOrWhiteSpace(shaderAssetId)) {
                return;
            }

            string prefix = string.Concat(shaderAssetId, "|");
            List<string> keysToRemove = new List<string>();
            foreach (var pair in ShaderResourceCache) {
                if (pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
                    keysToRemove.Add(pair.Key);
                }
            }

            for (int i = 0; i < keysToRemove.Count; i++) {
                string key = keysToRemove[i];
                if (ShaderResourceCache.TryGetValue(key, out DirectX11ShaderResource resource)) {
                    resource.Dispose();
                }

                ShaderResourceCache.Remove(key);
            }
        }

        /// <summary>
        /// Retrieves a compiled shader pass from the cache or builds a new one.
        /// </summary>
        /// <param name="shaderPath">Path to the shader source file.</param>
        /// <param name="vertexEntry">Vertex shader entry point.</param>
        /// <param name="pixelEntry">Pixel shader entry point.</param>
        /// <returns>Compiled shader pass instance.</returns>
        DirectX11ShaderPass GetShaderPass(string shaderPath, string vertexEntry, string pixelEntry) {
            if (string.IsNullOrWhiteSpace(shaderPath)) {
                throw new ArgumentException("Shader path must be provided.", nameof(shaderPath));
            }
            if (string.IsNullOrWhiteSpace(vertexEntry)) {
                throw new ArgumentException("Vertex entry point must be provided.", nameof(vertexEntry));
            }
            if (string.IsNullOrWhiteSpace(pixelEntry)) {
                throw new ArgumentException("Pixel entry point must be provided.", nameof(pixelEntry));
            }

            string cacheKey = GetShaderCacheKey(shaderPath, vertexEntry, pixelEntry);
            if (shaderPassCache.TryGetValue(cacheKey, out DirectX11ShaderPass cachedPass)) {
                return cachedPass;
            }

            var shaderPass = new DirectX11ShaderPass(Device, shaderPath, vertexEntry, pixelEntry);
            shaderPassCache[cacheKey] = shaderPass;
            return shaderPass;
        }

        /// <summary>
        /// Builds the cache key used to store shader passes.
        /// </summary>
        /// <param name="shaderPath">Path to the shader source file.</param>
        /// <param name="vertexEntry">Vertex shader entry point.</param>
        /// <param name="pixelEntry">Pixel shader entry point.</param>
        /// <returns>Composite cache key for the shader pass.</returns>
        string GetShaderCacheKey(string shaderPath, string vertexEntry, string pixelEntry) {
            return string.Concat(shaderPath, "|", vertexEntry, "|", pixelEntry);
        }

        /// <summary>
        /// Disposes and clears cached shader passes.
        /// </summary>
        void DisposeShaderPassCache() {
            foreach (var shaderPass in shaderPassCache.Values) {
                shaderPass.Dispose();
            }

            shaderPassCache.Clear();
        }

        /// <summary>
        /// Disposes and clears cached point-shadow cube resources.
        /// </summary>
        void DisposePointShadowCubeResources() {
            for (int resourceIndex = 0; resourceIndex < PointShadowCubeResourcesValue.Count; resourceIndex++) {
                PointShadowCubeResourcesValue[resourceIndex].Dispose();
            }

            PointShadowCubeResourcesValue.Clear();
        }

        /// <summary>
        /// Disposes rasterizer states created for non-default material render states.
        /// </summary>
        void DisposeRasterizerStateCache() {
            foreach (RasterizerState state in RasterizerStateCache.Values) {
                state.Dispose();
            }

            RasterizerStateCache.Clear();
        }

        /// <summary>
        /// Disposes depth-stencil states created for non-default material render states.
        /// </summary>
        void DisposeDepthStencilStateCache() {
            foreach (DepthStencilState state in DepthStencilStateCache.Values) {
                state.Dispose();
            }

            DepthStencilStateCache.Clear();
        }

        /// <summary>
        /// Disposes and clears cached shader resources.
        /// </summary>
        void DisposeShaderResourceCache() {
            foreach (var shaderResource in ShaderResourceCache.Values) {
                shaderResource.Dispose();
            }

            ShaderResourceCache.Clear();
        }
    }
}
