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
    [NativeMigrationRequired(
        "windows.native_directx_renderer",
        "Changes to this managed DirectX11 renderer must also be applied to the Windows native DirectX renderer implementation.")]
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
        /// Shader filename used for missing-material rendering.
        /// </summary>
        const string MissingMaterialShaderFileName = "MissingMaterial.fx";
        /// <summary>
        /// Maximum number of point-shadow cube textures bound by the built-in forward shader.
        /// </summary>
        const int MaximumPointShadowTextureSlots = 4;

        /// <summary>
        /// Tracks elapsed time for frame statistics.
        /// </summary>
        Stopwatch FrameStopwatch = Stopwatch.StartNew();
        /// <summary>
        /// Draw call count accumulated during the current frame.
        /// </summary>
        int DrawCallsThisFrame;
        /// <summary>
        /// Draw call count from the previous frame.
        /// </summary>
        int LastDrawCallsValue;
        /// <summary>
        /// Visible light count selected for the previous extracted camera frame.
        /// </summary>
        int LastSelectedLightCountValue;
        /// <summary>
        /// Shadow-enabled light count selected for the previous extracted camera frame.
        /// </summary>
        int LastSelectedShadowLightCountValue;
        /// <summary>
        /// Frames per second measured on the previous frame.
        /// </summary>
        double LastFpsValue;
        /// <summary>
        /// Frame time in milliseconds measured on the previous frame.
        /// </summary>
        double LastFrameTimeMsValue;
        /// <summary>
        /// Swapchain surfaces tracked by this renderer.
        /// </summary>
        List<DirectX11SwapChainSurface> Surfaces;
        /// <summary>
        /// Lookup of swapchain surfaces by native window handle.
        /// </summary>
        Dictionary<IntPtr, DirectX11SwapChainSurface> SurfacesByHandle;
        /// <summary>
        /// Constant buffer for the built-in standard mesh transform data.
        /// </summary>
        Buffer ConstantBuffer;
        /// <summary>
        /// Constant buffer used for custom effect shader data.
        /// </summary>
        Buffer CustomPassConstantBuffer;
        /// <summary>
        /// Constant buffer used for packed forward-light shader data.
        /// </summary>
        Buffer ForwardLightConstantBuffer;
        /// <summary>
        /// Constant buffer used for packed atlas-shadow shader data.
        /// </summary>
        Buffer ShadowConstantBuffer;
        /// <summary>
        /// Constant buffer used by the built-in point-shadow depth shader.
        /// </summary>
        Buffer PointShadowDepthConstantBuffer;
        /// <summary>
        /// Cache of compiled DirectX11 shader resources.
        /// </summary>
        Dictionary<string, DirectX11ShaderResource> ShaderResourceCache;
        /// <summary>
        /// Tracks materials grouped by shader asset id for hot reload updates.
        /// </summary>
        Dictionary<string, List<DirectX11MaterialResource>> MaterialsByShaderAssetId;
        /// <summary>
        /// 2D renderer used for overlays and UI.
        /// </summary>
        DirectX11Renderer2D Renderer2D;
        /// <summary>
        /// Owns the shared default pipeline states, the per-material state caches and the record of what is currently bound.
        /// </summary>
        readonly DirectX11PipelineStateCache PipelineStateCache;
        /// <summary>
        /// Binds runtime materials, their texture views and their constant-buffer payloads to the pipeline.
        /// </summary>
        readonly DirectX11MaterialBinder MaterialBinder;
        /// <summary>
        /// World-space camera position for the active 3D camera pass.
        /// </summary>
        float3 CurrentCameraPosition;
        /// <summary>
        /// Stores the fallback material used when a drawable has no material.
        /// </summary>
        DirectX11MaterialResource MissingMaterial;
        /// <summary>
        /// Tracks whether the current pass is a custom shader render.
        /// </summary>
        bool IsCustomPassActive;
        /// <summary>
        /// Provides per-draw colors for the active custom pass.
        /// </summary>
        Func<IDrawable3D, byte4> CustomColorProvider;
        /// <summary>
        /// Stores custom shader pass requests keyed by camera.
        /// </summary>
        Dictionary<ICamera, DirectX11CustomPassRequest> CustomPassRequests;
        /// <summary>
        /// Caches compiled shader passes by a composite key.
        /// </summary>
        Dictionary<string, DirectX11ShaderPass> ShaderPassCache;
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
        float4x4 CurrentViewProjection;
        /// <summary>
        /// Tracks whether the renderer is traversing and presenting a frame.
        /// </summary>
        bool FrameActive;
        /// <summary>
        /// Shared extraction service used to build backend-neutral render frames.
        /// </summary>
        readonly RenderFrameExtractionService FrameExtractionService;
        /// <summary>
        /// Shared render-plan builder used to select the ordered DirectX11 pass list.
        /// </summary>
        readonly DirectX11RenderPlanBuilder RenderPlanBuilder;
        /// <summary>
        /// Shared plan executor used to dispatch selected pass kinds into runtime pass methods.
        /// </summary>
        readonly DirectX11RenderPlanExecutor RenderPlanExecutor;
        /// <summary>
        /// Queue snapshot visitor used to copy ordered drawables before extraction.
        /// </summary>
        readonly DirectX11RenderQueueSnapshotVisitor RenderQueueSnapshotVisitor;
        /// <summary>
        /// Builder that packs selected lights into the built-in DirectX11 forward-light constant buffer layout.
        /// </summary>
        readonly DirectX11ForwardLightShaderDataBuilder ForwardLightShaderDataBuilder;
        /// <summary>
        /// Service that applies the DirectX11 visible-light budget to extracted lights.
        /// </summary>
        readonly DirectX11LightSelectionService LightSelectionService;
        /// <summary>
        /// Service that plans DirectX11 shadow resources for the selected light set.
        /// </summary>
        readonly DirectX11ShadowResourcePlanner ShadowResourcePlanner;
        /// <summary>
        /// Builder that packs atlas-shadow data into the built-in DirectX11 shadow constant buffer layout.
        /// </summary>
        readonly DirectX11ShadowShaderDataBuilder ShadowShaderDataBuilder;
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
            Surfaces = new List<DirectX11SwapChainSurface>();
            SurfacesByHandle = new Dictionary<IntPtr, DirectX11SwapChainSurface>();
            CustomPassRequests = new Dictionary<ICamera, DirectX11CustomPassRequest>();
            ShaderPassCache = new Dictionary<string, DirectX11ShaderPass>(StringComparer.Ordinal);
            ShaderResourceCache = new Dictionary<string, DirectX11ShaderResource>(StringComparer.Ordinal);
            MaterialsByShaderAssetId = new Dictionary<string, List<DirectX11MaterialResource>>(StringComparer.OrdinalIgnoreCase);
            FrameExtractionService = new RenderFrameExtractionService();
            RenderPlanBuilder = new DirectX11RenderPlanBuilder();
            RenderPlanExecutor = new DirectX11RenderPlanExecutor(true, false);
            RenderQueueSnapshotVisitor = new DirectX11RenderQueueSnapshotVisitor();
            ForwardLightShaderDataBuilder = new DirectX11ForwardLightShaderDataBuilder();
            LightSelectionService = new DirectX11LightSelectionService();
            ShadowResourcePlanner = new DirectX11ShadowResourcePlanner();
            ShadowShaderDataBuilder = new DirectX11ShadowShaderDataBuilder();
            PointShadowCubeResourcesValue = new List<DirectX11PointShadowCubeResources>();
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
            EnableImmediateContextMultithreadProtection();
            PipelineStateCache = new DirectX11PipelineStateCache(Device);
            MaterialBinder = new DirectX11MaterialBinder(Device, PipelineStateCache);

            ConstantBuffer = new Buffer(Device, Utilities.SizeOf<StandardMeshShaderData>(), ResourceUsage.Default,
                BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            CustomPassConstantBuffer = new Buffer(Device, Utilities.SizeOf<CustomEffectShaderData>(), ResourceUsage.Default,
                BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            ForwardLightConstantBuffer = new Buffer(Device, Utilities.SizeOf<DirectX11ForwardLightShaderData>(), ResourceUsage.Default,
                BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            ShadowConstantBuffer = new Buffer(Device, Utilities.SizeOf<DirectX11ShadowShaderData>(), ResourceUsage.Default,
                BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            PointShadowDepthConstantBuffer = new Buffer(Device, Utilities.SizeOf<DirectX11PointShadowDepthShaderData>(), ResourceUsage.Default,
                BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);

            Renderer2D = new DirectX11Renderer2D(this);
            DebugInfoRegistry.Register(new DirectX11Renderer3DDebugInfoProvider(this));
        }

        /// <summary>
        /// Gets the Direct3D device.
        /// </summary>
        public D3DDevice Device { get; }

        /// <summary>
        /// Gets whether the DirectX11 renderer is traversing an active frame.
        /// </summary>
        internal bool IsFrameActive { get { return FrameActive; } }

        /// <summary>
        /// Gets the DXGI adapter used by the device.
        /// </summary>
        public Adapter1 Adapter { get; }

        /// <summary>
        /// Gets the 2D renderer used for overlay/UI rendering.
        /// </summary>
        public RenderManager2D Render2D => Renderer2D;

        /// <summary>
        /// Enables Direct3D11 immediate-context multithread protection so cross-thread renderer access is serialized by the runtime.
        /// </summary>
        void EnableImmediateContextMultithreadProtection() {
            using Multithread multithread = Device.ImmediateContext.QueryInterface<Multithread>();
            if (!multithread.GetMultithreadProtected()) {
                multithread.SetMultithreadProtected(true);
            }
        }

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
        internal double LastFps => LastFpsValue;

        /// <summary>
        /// Gets the draw call count from the previous frame.
        /// </summary>
        internal int LastDrawCalls => LastDrawCallsValue;

        /// <summary>
        /// Gets the draw-call count recorded by the most recent completed draw.
        /// </summary>
        public override int LastDrawCallCount => LastDrawCallsValue;

        /// <summary>
        /// Gets the last frame time in milliseconds.
        /// </summary>
        internal double LastFrameTimeMs => LastFrameTimeMsValue;

        /// <summary>
        /// Gets the visible light count selected for the previous extracted camera frame.
        /// </summary>
        internal int LastSelectedLightCount => LastSelectedLightCountValue;

        /// <summary>
        /// Gets the shadow-enabled light count selected for the previous extracted camera frame.
        /// </summary>
        internal int LastSelectedShadowLightCount => LastSelectedShadowLightCountValue;

        /// <summary>
        /// Releases GPU resources and detaches from window events.
        /// </summary>
        public override void Dispose() {
            WindowResized -= OnWindowResized;

            Renderer2D.Dispose();

            for (int i = 0; i < Surfaces.Count; i++) {
                Surfaces[i].Dispose();
            }
            Surfaces.Clear();
            SurfacesByHandle.Clear();

            MaterialBinder.Dispose();
            PipelineStateCache.Dispose();
            PointShadowDepthConstantBuffer?.Dispose();
            ShadowConstantBuffer?.Dispose();
            ForwardLightConstantBuffer?.Dispose();
            CustomPassConstantBuffer?.Dispose();
            ConstantBuffer?.Dispose();
            ShadowAtlasResourcesValue?.Dispose();
            ShadowDepthShaderPassValue?.Dispose();
            PointShadowDepthShaderPassValue?.Dispose();
            DisposePointShadowCubeResources();
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
                Surfaces.Add(surface);
                SurfacesByHandle.Add(handle, surface);

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
            if (newWidth <= 0 || newHeight <= 0) {
                return;
            }

            if (!SurfacesByHandle.TryGetValue(handle, out var surface)) {
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
        /// <param name="materialAssetPath">Runtime asset path to the serialized material asset.</param>
        /// <param name="materialAsset">Raw material asset definition.</param>
        /// <returns>Runtime material instance.</returns>
        [NativeOwnedReturn]
        public override RuntimeMaterial BuildMaterialFromRawAsset(
            ContentManager assetContentManager,
            string materialAssetPath) {
            return ShaderRuntimeMaterialLoader.BuildMaterialFromRawAsset(this, assetContentManager, materialAssetPath);
        }

        /// <summary>
        /// Builds a runtime material from raw asset data and a shader asset.
        /// </summary>
        /// <param name="materialAsset">Raw material asset definition.</param>
        /// <param name="shaderAsset">Shader asset used by the material.</param>
        /// <returns>Runtime material instance.</returns>
        [NativeOwnedReturn]
        public virtual RuntimeMaterial BuildMaterialFromRaw(
            [NativeNoEscape] ShaderMaterialAsset materialAsset,
            [NativeNoEscape] ShaderAsset shaderAsset) {
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
            StandardMaterialTextureBindingDefaults.Apply(material, Renderer2D);
            RegisterMaterial(material);
            return material;
        }

        /// <summary>
        /// Releases one runtime material and removes it from shader hot-reload tracking before the shared renderer contract disposes it.
        /// </summary>
        /// <param name="material">Runtime material to release.</param>
        public override void ReleaseMaterial(RuntimeMaterial material) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            if (material is DirectX11MaterialResource directX11Material) {
                UnregisterMaterial(directX11Material);
                if (MaterialBinder.IsActiveMaterial(directX11Material)) {
                    MaterialBinder.ResetActiveMaterial();
                }
            }

            base.ReleaseMaterial(material);
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

            MaterialBinder.ResetActiveMaterial();
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
            CustomPassRequests[camera] = request;
        }

        /// <summary>
        /// Renders all queued custom shader passes before the main surface rendering.
        /// </summary>
        void RenderCustomPasses() {
            if (CustomPassRequests.Count == 0) {
                return;
            }

            foreach (var entry in CustomPassRequests) {
                RenderCustomPass(entry.Value);
            }

            CustomPassRequests.Clear();
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
            PipelineStateCache.BindCustomPassState(context);

            RenderTargetView renderTargetView = directX11Target.RenderTargetView;
            DepthStencilView depthStencilView = directX11Target.DepthStencilView;

            CameraClearSettings clearSettings = camera.ClearSettings;
            bool clearColor = clearSettings.ClearColorEnabled;
            float4 clearColorValue = clearSettings.ClearColor;
            bool clearDepth = clearSettings.ClearDepthEnabled;
            float clearDepthValue = clearSettings.ClearDepth;
            bool clearStencil = clearSettings.ClearStencilEnabled;
            byte clearStencilValue = clearSettings.ClearStencil;

            ClearShaderResourceBindingsForRenderTargetChange();
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
            CurrentCameraPosition = cameraPos;
            float4 cameraOrientation = camera.Parent.Orientation;
            float3 cameraForward = float4.RotateVector(DefaultForward, cameraOrientation);
            float3 cameraUp = float4.RotateVector(DefaultUp, cameraOrientation);
            float3 cameraTarget = cameraPos + cameraForward;
            float4x4.CreateLookAt(ref cameraPos, ref cameraTarget, ref cameraUp, out view);

            float4 viewport = CameraViewportResolver.ResolveViewport(camera.Viewport, directX11Target.Width, directX11Target.Height);
            context.Rasterizer.SetViewport(viewport.X, viewport.Y, viewport.Z, viewport.W);

            float4x4 projection = CameraProjectionUtils.CreatePerspectiveProjection(camera, (float)Math.PI / 4.0f, viewport.Z / viewport.W);

            float4x4.Multiply(ref view, ref projection, out CurrentViewProjection);

            IsCustomPassActive = true;
            CustomColorProvider = request.ColorProvider;
            try {
                context.VertexShader.Set(shaderPass.VertexShader);
                context.PixelShader.Set(shaderPass.PixelShader);
                context.VertexShader.SetConstantBuffer(0, CustomPassConstantBuffer);
                context.PixelShader.SetConstantBuffer(0, CustomPassConstantBuffer);

                request.RenderQueue.VisitOrdered(this);
            } finally {
                IsCustomPassActive = false;
                CustomColorProvider = null;
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
            RenderFrameExtractionResult extractionResult = FrameExtractionService.Extract(
                [camera],
                drawables,
                lights,
                capabilityProfile);
            RenderFrame frame = extractionResult.Frames[0];
            RenderFrameLightSubmission[] selectedLights = LightSelectionService.SelectVisibleLights(frame.LightSubmissions, capabilityProfile.MaximumVisibleLights);
            LastSelectedLightCountValue = selectedLights.Length;
            DirectX11ShadowResourceSet shadowResourceSet = ShadowResourcePlanner.PlanResources(selectedLights, capabilityProfile.MaximumShadowedLights);
            LastSelectedShadowLightCountValue = shadowResourceSet.SelectedShadowLights.Count;
            CurrentShadowResourceSet = shadowResourceSet;
            RenderPlan plan = RenderPlanBuilder.Build(frame, extractionResult.BackendCapabilities);
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
            RenderPlanExecutor.ExecutePlan(context, plan, this);
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

            ClearShaderResourceBindingsForRenderTargetChange();
            deviceContext.OutputMerger.SetTargets(depthStencilView, renderTargetView);
            ApplyCameraClear(deviceContext, camera, context.Surface, renderTarget, renderTargetView, depthStencilView);

            PipelineStateCache.BindCameraFrameState(deviceContext);

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

            float4x4.Multiply(ref view, ref projection, out CurrentViewProjection);

            deviceContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            IsCustomPassActive = false;
            CustomColorProvider = null;
            MaterialBinder.ResetActiveMaterial();
            PipelineStateCache.BindBlendState(deviceContext, null);
            deviceContext.VertexShader.SetConstantBuffer(0, ConstantBuffer);
            deviceContext.PixelShader.SetConstantBuffer(0, ConstantBuffer);
            UpdateShadowShaderData(new DirectX11ShadowShaderData());
            UpdateShadowAtlasBindings(false);
            UpdatePointShadowBindings(0);
            PrepareForwardLightState(context);
        }

        /// <summary>
        /// Clears shader-resource bindings that can alias a render target before that target is rebound for output.
        /// </summary>
        [NativeMigrationRequired(
            "windows.native_directx_renderer",
            "Changes to DirectX11 render-target transition cleanup must also be applied to the Windows native DirectX renderer implementation.")]
        void ClearShaderResourceBindingsForRenderTargetChange() {
            Renderer2D.ClearActiveTextureBindings();
            MaterialBinder.ClearActiveMaterialTextureBindings();
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
            PipelineStateCache.BindRestoredCameraFrameState(deviceContext);
            float4 viewport = ResolveCameraViewport(camera, context.Surface);
            deviceContext.Rasterizer.SetViewport(viewport.X, viewport.Y, viewport.Z, viewport.W);
            deviceContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            deviceContext.VertexShader.SetConstantBuffer(0, ConstantBuffer);
            deviceContext.PixelShader.SetConstantBuffer(0, ConstantBuffer);
            deviceContext.PixelShader.SetConstantBuffer(1, ForwardLightConstantBuffer);
            deviceContext.PixelShader.SetConstantBuffer(2, ShadowConstantBuffer);
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

            Renderer2D.RenderCamera(context.Frame.Camera);
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
            return ForwardLightShaderDataBuilder.Build(selectedLights);
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
            return ShadowShaderDataBuilder.Build(context.Frame.Camera, context.SelectedLights, shadowResourceSet);
        }

        /// <summary>
        /// Uploads the packed atlas-shadow shader data to the DirectX11 pixel-shader constant-buffer slot.
        /// </summary>
        /// <param name="data">Packed atlas-shadow shader data prepared for the current frame.</param>
        protected virtual void UpdateShadowShaderData(DirectX11ShadowShaderData data) {
            var context = Device.ImmediateContext;
            context.UpdateSubresource(ref data, ShadowConstantBuffer);
            context.PixelShader.SetConstantBuffer(2, ShadowConstantBuffer);
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
            PipelineStateCache.BindShadowDepthState(deviceContext);
            deviceContext.InputAssembler.InputLayout = pointShadowPass.InputLayout;
            deviceContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            deviceContext.VertexShader.Set(pointShadowPass.VertexShader);
            deviceContext.PixelShader.Set(pointShadowPass.PixelShader);
            deviceContext.VertexShader.SetConstantBuffer(0, PointShadowDepthConstantBuffer);
            deviceContext.PixelShader.SetConstantBuffer(0, PointShadowDepthConstantBuffer);

            for (int resourceIndex = 0; resourceIndex < shadowResourceSet.PointShadowResources.Count; resourceIndex++) {
                DirectX11PointShadowResource pointShadowResource = shadowResourceSet.PointShadowResources[resourceIndex];
                PointLightComponent pointLight = (PointLightComponent)pointShadowResource.Light.Light;
                DirectX11PointShadowCubeResources cubeResources = pointShadowCubeResources[resourceIndex];
                for (int faceIndex = 0; faceIndex < 6; faceIndex++) {
                    deviceContext.OutputMerger.SetTargets(cubeResources.DepthStencilViews[faceIndex], cubeResources.RenderTargetViews[faceIndex]);
                    deviceContext.ClearDepthStencilView(cubeResources.DepthStencilViews[faceIndex], DepthStencilClearFlags.Depth, 1f, 0);
                    deviceContext.ClearRenderTargetView(cubeResources.RenderTargetViews[faceIndex], new RawColor4(1f, 0f, 0f, 1f));
                    deviceContext.Rasterizer.SetViewport(0, 0, cubeResources.Resolution, cubeResources.Resolution);
                    float4x4 lightViewProjection = ShadowShaderDataBuilder.BuildPointShadowViewProjectionMatrix(pointLight, faceIndex);
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
            PipelineStateCache.BindShadowDepthState(deviceContext);
            deviceContext.InputAssembler.InputLayout = shadowPass.InputLayout;
            deviceContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            deviceContext.VertexShader.Set(shadowPass.VertexShader);
            deviceContext.PixelShader.Set(shadowPass.PixelShader);
            deviceContext.VertexShader.SetConstantBuffer(0, CustomPassConstantBuffer);
            deviceContext.PixelShader.SetConstantBuffer(0, CustomPassConstantBuffer);

            for (int allocationIndex = 0; allocationIndex < shadowResourceSet.AtlasAllocations.Count; allocationIndex++) {
                DirectX11ShadowAtlasAllocation allocation = shadowResourceSet.AtlasAllocations[allocationIndex];
                deviceContext.Rasterizer.SetViewport(allocation.X, allocation.Y, allocation.Width, allocation.Height);
                float4x4 lightViewProjection = ShadowShaderDataBuilder.BuildShadowViewProjectionMatrix(context.Frame.Camera, allocation);
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
            } else if (!MaterialBinder.ShouldMaterialCastShadows(submission.Material)) {
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
            deviceContext.UpdateSubresource(ref shadowData, CustomPassConstantBuffer);
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
            } else if (!MaterialBinder.ShouldMaterialCastShadows(submission.Material)) {
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
            deviceContext.UpdateSubresource(ref shadowData, PointShadowDepthConstantBuffer);
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

            return drawable.Parent.WorldTransformMatrix;
        }

        /// <summary>
        /// Uploads the packed forward-light shader data to the DirectX11 pixel-shader constant-buffer slot.
        /// </summary>
        /// <param name="data">Packed forward-light shader data prepared for the current frame.</param>
        protected virtual void UpdateForwardLightShaderData(DirectX11ForwardLightShaderData data) {
            var context = Device.ImmediateContext;
            context.UpdateSubresource(ref data, ForwardLightConstantBuffer);
            context.PixelShader.SetConstantBuffer(1, ForwardLightConstantBuffer);
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

            // Custom passes must draw every submesh: submitting only index zero left the remaining submesh
            // ranges out of picker renders, which made multi-material meshes unclickable outside their first
            // material region.
            int submeshCount = 1;
            if (drawable.Model is DirectX11ModelResource modelResource
                && modelResource.Submeshes != null
                && modelResource.Submeshes.Length > 0) {
                submeshCount = modelResource.Submeshes.Length;
            }

            for (int submeshIndex = 0; submeshIndex < submeshCount; submeshIndex++) {
                RuntimeMaterial material = drawable.Materials.Length == 0
                    ? null
                    : drawable.Materials[Math.Min(submeshIndex, drawable.Materials.Length - 1)];
                Visit(new RenderFrameDrawableSubmission(
                    drawable,
                    submeshIndex,
                    material,
                    false,
                    new RenderFrameBatchingMetadata(false, false, false)));
            }
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
            ShaderRuntimeMaterial effectiveRuntimeMaterial = null;
            if (!IsCustomPassActive) {
                if (runtimeMaterial == null) {
                    DirectX11MaterialResource missingMaterial = GetMissingMaterial();
                    effectiveRuntimeMaterial = missingMaterial;
                    MaterialBinder.ApplyMaterial(missingMaterial, missingMaterial);
                } else {
                    effectiveRuntimeMaterial = MaterialBinder.RequireShaderRuntimeMaterial(runtimeMaterial);
                    DirectX11MaterialResource directX11Material = MaterialBinder.ResolveDirectX11Material(effectiveRuntimeMaterial);
                    MaterialBinder.ApplyMaterial(directX11Material, effectiveRuntimeMaterial);
                }
            }

            Entity parent = drawable.Parent;
            var data = (DirectX11ModelResource)drawable.Model;

            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(data.VertexBuffer, Utilities.SizeOf<VertexPositionNormalUV>(), 0));
            if (data.IndexBuffer != null && data.IndexCount > 0) {
                Format indexFormat = data.Uses32BitIndices ? Format.R32_UInt : Format.R16_UInt;
                context.InputAssembler.SetIndexBuffer(data.IndexBuffer, indexFormat, 0);
            }

            float4x4 world = parent.WorldTransformMatrix;

            float4x4 worldViewProj;
            float4x4.Multiply(ref world, ref CurrentViewProjection, out worldViewProj);

            float4x4 worldTransposed;
            float4x4.Transpose(ref world, out worldTransposed);
            float4x4 worldViewProjTransposed;
            float4x4.Transpose(ref worldViewProj, out worldViewProjTransposed);

            if (IsCustomPassActive) {
                if (CustomColorProvider == null) {
                    throw new InvalidOperationException("Custom pass color provider must be set before rendering.");
                }

                byte4 customColor = CustomColorProvider(drawable);
                var customData = new CustomEffectShaderData {
                    worldViewProj = worldViewProjTransposed,
                    color = new float4(customColor.X / 255f, customColor.Y / 255f, customColor.Z / 255f, customColor.W / 255f)
                };
                context.UpdateSubresource(ref customData, CustomPassConstantBuffer);
            } else {
                if (effectiveRuntimeMaterial == null) {
                    effectiveRuntimeMaterial = MaterialBinder.RequireShaderRuntimeMaterial(runtimeMaterial);
                }

                ShaderRuntimeMaterial rootMaterial = MaterialBinder.RequireShaderRuntimeMaterial(effectiveRuntimeMaterial.ResolveRootMaterial());
                if (BuiltInMaterialIds.UsesStandardMeshTransform(
                    rootMaterial.Id,
                    rootMaterial.Layout.ShaderAssetId,
                    rootMaterial.Layout.VertexProgram,
                    rootMaterial.Layout.PixelProgram)) {
                    StandardMeshShaderData standardData = BuildStandardMeshShaderData(world, CurrentCameraPosition, effectiveRuntimeMaterial.ReceivesShadows, effectiveRuntimeMaterial.SupportsEmissive);
                    standardData.World = worldTransposed;
                    standardData.WorldViewProj = worldViewProjTransposed;
                    context.UpdateSubresource(ref standardData, ConstantBuffer);
                } else {
                    context.UpdateSubresource(ref worldViewProjTransposed, ConstantBuffer);
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
        /// <param name="hasEmissiveTexture">Whether the current material should sample an authored emissive texture.</param>
        /// <returns>Standard-mesh shader data configured for one draw.</returns>
        static StandardMeshShaderData BuildStandardMeshShaderData(float4x4 world, float3 cameraPosition, bool receivesShadows, bool hasEmissiveTexture) {
            // Zero-scale entities produce singular world matrices; a degenerate draw has no visible surface,
            // so an identity normal matrix is a safe fallback instead of crashing the frame.
            float4x4.TryInverseTranspose(ref world, out float4x4 inverseTransposeNormalMatrix);
            float4x4.Transpose(ref inverseTransposeNormalMatrix, out float4x4 normalMatrixTransposed);

            return new StandardMeshShaderData {
                World = default,
                WorldViewProj = default,
                NormalMatrix = normalMatrixTransposed,
                CameraPosition = new float4(cameraPosition.X, cameraPosition.Y, cameraPosition.Z, 0f),
                MaterialFlags = new float4(receivesShadows ? 1f : 0f, hasEmissiveTexture ? 1f : 0f, 0f, 0f)
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
            context.InputAssembler.PrimitiveTopology = ResolvePrimitiveTopology(submesh.PrimitiveTopology);
            if (model.IndexBuffer != null && model.IndexCount > 0) {
                context.DrawIndexed(submesh.IndexCount, submesh.IndexStart, 0);
            } else {
                context.Draw(submesh.IndexCount, submesh.IndexStart);
            }
        }

        static PrimitiveTopology ResolvePrimitiveTopology(ModelPrimitiveTopology primitiveTopology) {
            return primitiveTopology switch {
                ModelPrimitiveTopology.LineList => PrimitiveTopology.LineList,
                _ => PrimitiveTopology.TriangleList
            };
        }

        /// <summary>
        /// Gets whether DirectX11 repro trace logging is enabled for the current process.
        /// <summary>
        /// Sets the rounded-rectangle rendering backend for UI shapes.
        /// </summary>
        /// <param name="backend">Backend to use for rounded rectangles.</param>
        public void SetRoundedRectBackend(RoundedRectBackend backend) {
            Renderer2D.SetRoundedRectBackend(backend);
        }

        /// <summary>
        /// Increments the per-frame draw call counter.
        /// </summary>
        /// <param name="count">Number of draw calls to add.</param>
        internal void IncrementDrawCalls(int count) {
            DrawCallsThisFrame += count;
        }

        /// <summary>
        /// Executes the full render pass for all windows and cameras.
        /// </summary>
        public override void Draw() {
            base.Draw();

            if (Surfaces.Count == 0) {
                return;
            }

            FrameActive = true;
            try {
                UpdateFrameStats();

                RenderCustomPasses();

                Core ownerCore = OwnerCore ?? throw new InvalidOperationException("DirectX11 renderer is not attached to an owning Core.");
                var cameras = ownerCore.ObjectManager.Cameras;

                for (int i = 0; i < Surfaces.Count; i++) {
                    var surface = Surfaces[i];

                    for (int j = 0; j < cameras.Count; j++) {
                        ICamera camera = cameras[j];
                        if (camera is not CameraComponent cameraComponent) {
                            throw new InvalidOperationException("DirectX11 rendering requires camera entries to be CameraComponent instances.");
                        }

                        RenderCamera(surface, cameraComponent);
                    }

                    surface.SwapChain.Present(0, PresentFlags.None);
                }
            } finally {
                FrameActive = false;
            }
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

            RenderQueueSnapshotVisitor.Reset(renderQueue.Count);
            renderQueue.VisitOrdered(RenderQueueSnapshotVisitor);
            return RenderQueueSnapshotVisitor.CreateSnapshot();
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
            Core ownerCore = OwnerCore ?? throw new InvalidOperationException("DirectX11 renderer is not attached to an owning Core.");
            List<Entity> entities = ownerCore.ObjectManager.Entities;
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
            LastDrawCallsValue = DrawCallsThisFrame;
            DrawCallsThisFrame = 0;

            double ms = FrameStopwatch.Elapsed.TotalMilliseconds;
            LastFrameTimeMsValue = ms;
            LastFpsValue = ms > 0 ? 1000.0 / ms : 0;
            FrameStopwatch.Restart();
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

            var materialAsset = new ShaderMaterialAsset {
                ShaderAssetId = material.ShaderAssetId,
                VertexProgram = material.VertexProgram,
                PixelProgram = material.PixelProgram,
                Variant = material.Variant,
                RenderState = material.RenderState
            };

            return MaterialLayoutBuilder.Build(materialAsset, shaderAsset);
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
        /// <param name="materialAsset">Shader-owned material asset that defines program selections.</param>
        /// <param name="shaderAsset">Shader asset containing compiled binaries.</param>
        /// <returns>Compiled DirectX11 shader resource.</returns>
        DirectX11ShaderResource GetShaderResource(ShaderMaterialAsset materialAsset, ShaderAsset shaderAsset) {
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
        /// Removes one runtime material from shader hot-reload tracking.
        /// </summary>
        /// <param name="material">Material to unregister.</param>
        void UnregisterMaterial(DirectX11MaterialResource material) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            string shaderAssetId = material.ShaderAssetId;
            if (string.IsNullOrWhiteSpace(shaderAssetId)) {
                return;
            }
            if (!MaterialsByShaderAssetId.TryGetValue(shaderAssetId, out List<DirectX11MaterialResource> materials)) {
                return;
            }

            materials.Remove(material);
            if (materials.Count == 0) {
                MaterialsByShaderAssetId.Remove(shaderAssetId);
            }
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
            if (ShaderPassCache.TryGetValue(cacheKey, out DirectX11ShaderPass cachedPass)) {
                return cachedPass;
            }

            var shaderPass = new DirectX11ShaderPass(Device, shaderPath, vertexEntry, pixelEntry);
            ShaderPassCache[cacheKey] = shaderPass;
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
            foreach (var shaderPass in ShaderPassCache.Values) {
                shaderPass.Dispose();
            }

            ShaderPassCache.Clear();
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
