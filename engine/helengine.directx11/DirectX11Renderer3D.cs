using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public class DirectX11Renderer3D : RenderManager3D, IRenderVisitor3D {
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
        /// Cache of compiled DirectX11 shader resources.
        /// </summary>
        Dictionary<string, DirectX11ShaderResource> ShaderResourceCache;
        /// <summary>
        /// Tracks materials grouped by shader asset id for hot reload updates.
        /// </summary>
        Dictionary<string, List<DirectX11MaterialResource>> MaterialsByShaderAssetId;
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

            blendState = CreateBlendState(BlendOption.SourceAlpha, BlendOption.InverseSourceAlpha, BlendOperation.Add);
            materialTextureSampler = CreateMaterialTextureSampler();

            renderer2D = new DirectX11Renderer2D(this);
            DebugInfoRegistry.Register(new DirectX11Renderer3DDebugInfoProvider(this));

            var rasterizerDesc3D = new RasterizerStateDescription {
                CullMode = CullMode.Back,
                FillMode = FillMode.Solid,
                IsDepthClipEnabled = true
            };
            rasterizerState3D = new RasterizerState(Device, rasterizerDesc3D);

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
        /// Gets the last recorded frames-per-second value.
        /// </summary>
        internal double LastFps => lastFps;

        /// <summary>
        /// Gets the draw call count from the previous frame.
        /// </summary>
        internal int LastDrawCalls => lastDrawCalls;

        /// <summary>
        /// Gets the last frame time in milliseconds.
        /// </summary>
        internal double LastFrameTimeMs => lastFrameTimeMs;

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
            rasterizerState3D?.Dispose();
            materialTextureSampler?.Dispose();
            blendState?.Dispose();
            customPassConstantBuffer?.Dispose();
            constantBuffer?.Dispose();
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
            var model = new DirectX11ModelResource();
            var vertices = new VertexPositionNormalUV[data.Positions.Length];
            ModelAssetIndexData indexData = ModelAssetIndexData.Resolve(data);

            for (int i = 0; i < data.Positions.Length; i++) {
                float3 pos = data.Positions[i];
                float3 normal = data.Normals[i];
                float2 tex = data.TexCoords[i];
                vertices[i] = new VertexPositionNormalUV(pos, normal, tex);
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

            return model;
        }

        /// <summary>
        /// Builds a runtime material from raw asset data and a shader asset.
        /// </summary>
        /// <param name="materialAsset">Raw material asset definition.</param>
        /// <param name="shaderAsset">Shader asset used by the material.</param>
        /// <returns>Runtime material instance.</returns>
        public override RuntimeMaterial BuildMaterialFromRaw(MaterialAsset materialAsset, ShaderAsset shaderAsset) {
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
            material.ApplyConstantBufferDefaults(materialAsset.ConstantBuffers ?? Array.Empty<MaterialConstantBufferAsset>());
            RegisterMaterial(material);
            return material;
        }

        /// <summary>
        /// Invalidates cached shader resources and updates materials for the specified shader asset.
        /// </summary>
        /// <param name="shaderAssetId">Shader asset identifier to invalidate.</param>
        /// <param name="shaderAsset">Updated shader asset data.</param>
        public override void InvalidateShaderResources(string shaderAssetId, ShaderAsset shaderAsset) {
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

            float4 viewport = camera.Viewport;
            context.Rasterizer.SetViewport(viewport.X, viewport.Y, viewport.Z, viewport.W);

            float4x4 projection;
            float4x4.CreatePerspectiveFieldOfView((float)Math.PI / 4.0f, (viewport.Z / viewport.W), 0.1f, 100f, out projection);

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
        /// Renders a camera's 3D pass and then its 2D overlay.
        /// </summary>
        /// <param name="surface">Render surface for the window.</param>
        /// <param name="camera">Camera to render.</param>
        void RenderCamera(DirectX11SwapChainSurface surface, ICamera camera) {
            var context = Device.ImmediateContext;
            RenderTargetView renderTargetView = surface.RenderTargetView;
            DepthStencilView depthStencilView = surface.DepthStencilView;
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
            context.Rasterizer.State = rasterizerState3D;
            context.OutputMerger.SetDepthStencilState(depthStencilState3D, 0);
            ActiveRasterizerState = rasterizerState3D;
            ActiveDepthStencilState = depthStencilState3D;

            float4x4 view;
            float3 cameraPos = camera.Parent.Position;
            float4 cameraOrientation = camera.Parent.Orientation;
            float3 cameraForward = float4.RotateVector(DefaultForward, cameraOrientation);
            float3 cameraUp = float4.RotateVector(DefaultUp, cameraOrientation);
            float3 cameraTarget = cameraPos + cameraForward;
            float4x4.CreateLookAt(ref cameraPos, ref cameraTarget, ref cameraUp, out view);

            float4 viewport = camera.Viewport;
            context.Rasterizer.SetViewport(viewport.X, viewport.Y, viewport.Z, viewport.W);

            float4x4 projection;
            float4x4.CreatePerspectiveFieldOfView((float)Math.PI / 4.0f, (viewport.Z / viewport.W), 0.1f, 100f, out projection);

            float4x4.Multiply(ref view, ref projection, out currentViewProjection);

            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            isCustomPassActive = false;
            customColorProvider = null;
            ActiveMaterial = null;
            context.OutputMerger.SetBlendState(null);
            ActiveBlendState = null;
            context.VertexShader.SetConstantBuffer(0, constantBuffer);
            context.PixelShader.SetConstantBuffer(0, constantBuffer);

            IRenderQueue3D renderQueue = camera.RenderQueue3D;
            renderQueue.VisitOrdered(this);

            renderer2D.RenderCamera(camera);
        }

        /// <summary>
        /// Draws a single 3D drawable encountered during queue traversal.
        /// </summary>
        /// <param name="drawable">Drawable to render.</param>
        public void Visit(IDrawable3D drawable) {
            if (drawable?.Parent == null || !drawable.Parent.Enabled) {
                return;
            }

            var context = Device.ImmediateContext;
            RuntimeMaterial runtimeMaterial = drawable.Material;
            if (!isCustomPassActive) {
                if (runtimeMaterial == null) {
                    DirectX11MaterialResource missingMaterial = GetMissingMaterial();
                    ApplyMaterial(missingMaterial, missingMaterial);
                } else {
                    RuntimeMaterial rootMaterial = runtimeMaterial.ResolveRootMaterial();
                    if (rootMaterial is not DirectX11MaterialResource directX11Material) {
                        throw new InvalidOperationException("Drawable materials must resolve to DirectX11MaterialResource through their parent chain.");
                    }

                    ApplyMaterial(directX11Material, runtimeMaterial);
                }
            }

            Entity parent = drawable.Parent;
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
                    RuntimeMaterial rootMaterial = runtimeMaterial.ResolveRootMaterial();
                    if (BuiltInMaterialIds.UsesStandardMeshTransform(rootMaterial.Id)) {
                    float4x4 normalMatrix;
                    float4x4.InverseTranspose(ref world, out normalMatrix);

                    var standardData = new StandardMeshShaderData {
                        World = worldTransposed,
                        WorldViewProj = worldViewProjTransposed,
                        NormalMatrix = normalMatrix,
                        CameraPosition = new float4(currentCameraPosition.X, currentCameraPosition.Y, currentCameraPosition.Z, 0f)
                    };
                    context.UpdateSubresource(ref standardData, constantBuffer);
                } else {
                    context.UpdateSubresource(ref worldViewProjTransposed, constantBuffer);
                }
            }

            if (data.IndexBuffer != null && data.IndexCount > 0) {
                context.DrawIndexed(data.IndexCount, 0, 0);
            } else {
                context.Draw(data.VertexCount, 0);
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

            var context = Device.ImmediateContext;

            RenderCustomPasses();

            var cameras = Core.Instance.ObjectManager.Cameras;

            for (int i = 0; i < surfaces.Count; i++) {
                var surface = surfaces[i];

                for (int j = 0; j < cameras.Count; j++) {
                    var camera = cameras[j];
                    RenderCamera(surface, camera);
                }

                surface.SwapChain.Present(0, PresentFlags.None);
            }
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
        void ApplyMaterial(DirectX11MaterialResource shaderMaterial, RuntimeMaterial material) {
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
        /// Resolves the shader resource view sampled by a textured 3D material.
        /// </summary>
        /// <param name="material">Material whose texture binding should be resolved.</param>
        /// <returns>Shader resource view to bind for the material.</returns>
        ShaderResourceView ResolveMaterialTextureResourceView(RuntimeMaterial material) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            RuntimeTexture runtimeTexture = material.ResolveTexture();
            if (runtimeTexture is not DirectX11TextureResource textureResource) {
                throw new InvalidOperationException("3D material textures must be DirectX11 texture resources.");
            }

            if (textureResource.Resource == null) {
                throw new InvalidOperationException("DirectX11 texture resources must expose a shader resource view.");
            }

            return textureResource.Resource;
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
