using SharpDX.Direct3D11;
using D3DDevice = SharpDX.Direct3D11.Device;

namespace helengine.directx11 {
    /// <summary>
    /// Owns the fixed-function DirectX11 pipeline state used by 3D rendering: the shared default states, the per-material state caches keyed by material render state, and the record of what is currently bound so redundant binds are skipped.
    /// </summary>
    public class DirectX11PipelineStateCache : IDisposable {
        /// <summary>
        /// Constant depth bias applied while rendering shadow maps to reduce self-shadowing on simple authored geometry.
        /// </summary>
        const int ShadowDepthBias = 1000;

        /// <summary>
        /// Slope-scaled depth bias applied while rendering shadow maps so grazing-angle receivers do not collapse into full self-shadowing.
        /// </summary>
        const float ShadowSlopeScaledDepthBias = 1.0f;

        /// <summary>
        /// Device used to create pipeline state objects on demand.
        /// </summary>
        readonly D3DDevice Device;

        /// <summary>
        /// Cache of non-default rasterizer states keyed by material render state.
        /// </summary>
        readonly Dictionary<int, RasterizerState> RasterizerStatesByKey;

        /// <summary>
        /// Cache of non-default depth-stencil states keyed by material render state.
        /// </summary>
        readonly Dictionary<int, DepthStencilState> DepthStencilStatesByKey;

        /// <summary>
        /// Tracks the rasterizer state currently bound to the pipeline.
        /// </summary>
        RasterizerState TrackedRasterizerState;

        /// <summary>
        /// Tracks the depth-stencil state currently bound to the pipeline.
        /// </summary>
        DepthStencilState TrackedDepthStencilState;

        /// <summary>
        /// Tracks the blend state currently bound to the pipeline.
        /// </summary>
        BlendState TrackedBlendState;

        /// <summary>
        /// Creates the shared default pipeline states on the supplied device.
        /// </summary>
        /// <param name="device">Device that owns every state object created by this cache.</param>
        public DirectX11PipelineStateCache(D3DDevice device) {
            if (device == null) {
                throw new ArgumentNullException(nameof(device));
            }

            Device = device;
            RasterizerStatesByKey = new Dictionary<int, RasterizerState>();
            DepthStencilStatesByKey = new Dictionary<int, DepthStencilState>();

            RasterizerStateDescription rasterizerDescription = new RasterizerStateDescription {
                CullMode = CullMode.Back,
                FillMode = FillMode.Solid,
                IsFrontCounterClockwise = true,
                IsDepthClipEnabled = true
            };
            DefaultRasterizerState = new RasterizerState(Device, rasterizerDescription);

            RasterizerStateDescription shadowRasterizerDescription = new RasterizerStateDescription {
                CullMode = CullMode.Back,
                FillMode = FillMode.Solid,
                IsFrontCounterClockwise = true,
                IsDepthClipEnabled = true,
                DepthBias = ShadowDepthBias,
                SlopeScaledDepthBias = ShadowSlopeScaledDepthBias
            };
            ShadowRasterizerState = new RasterizerState(Device, shadowRasterizerDescription);

            DepthStencilStateDescription depthStencilDescription = new DepthStencilStateDescription {
                IsDepthEnabled = true,
                DepthWriteMask = DepthWriteMask.All,
                DepthComparison = Comparison.Less
            };
            DefaultDepthStencilState = new DepthStencilState(Device, depthStencilDescription);

            AlphaBlendState = CreateAlphaBlendState(BlendOption.SourceAlpha, BlendOption.InverseSourceAlpha, BlendOperation.Add);
        }

        /// <summary>
        /// Gets the rasterizer state used by ordinary back-face-culled 3D rendering.
        /// </summary>
        public RasterizerState DefaultRasterizerState { get; }

        /// <summary>
        /// Gets the depth-biased rasterizer state used while rendering shadow maps.
        /// </summary>
        public RasterizerState ShadowRasterizerState { get; }

        /// <summary>
        /// Gets the depth-tested, depth-writing state used by ordinary 3D rendering.
        /// </summary>
        public DepthStencilState DefaultDepthStencilState { get; }

        /// <summary>
        /// Gets the alpha blend state used by transparent materials.
        /// </summary>
        public BlendState AlphaBlendState { get; }

        /// <summary>
        /// Binds the default opaque pipeline state for a custom shader pass without recording it, because the custom pass leaves the tracked state to the next camera frame.
        /// </summary>
        /// <param name="context">Device context receiving the state.</param>
        public void BindCustomPassState(DeviceContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            context.Rasterizer.State = DefaultRasterizerState;
            context.OutputMerger.SetDepthStencilState(DefaultDepthStencilState, 0);
            context.OutputMerger.SetBlendState(null);
        }

        /// <summary>
        /// Binds and records the rasterizer and depth-stencil state a camera frame starts from.
        /// </summary>
        /// <param name="context">Device context receiving the state.</param>
        public void BindCameraFrameState(DeviceContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            context.Rasterizer.State = DefaultRasterizerState;
            context.OutputMerger.SetDepthStencilState(DefaultDepthStencilState, 0);
            TrackedRasterizerState = DefaultRasterizerState;
            TrackedDepthStencilState = DefaultDepthStencilState;
        }

        /// <summary>
        /// Binds and records the opaque pipeline state restored after a shadow pass has taken over the device context.
        /// </summary>
        /// <param name="context">Device context receiving the state.</param>
        public void BindRestoredCameraFrameState(DeviceContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            context.Rasterizer.State = DefaultRasterizerState;
            context.OutputMerger.SetDepthStencilState(DefaultDepthStencilState, 0);
            context.OutputMerger.SetBlendState(null);
            TrackedRasterizerState = DefaultRasterizerState;
            TrackedDepthStencilState = DefaultDepthStencilState;
            TrackedBlendState = null;
        }

        /// <summary>
        /// Binds the depth-biased opaque state used while filling shadow maps without recording it, because the shadow pass restores the camera frame state afterwards.
        /// </summary>
        /// <param name="context">Device context receiving the state.</param>
        public void BindShadowDepthState(DeviceContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            context.Rasterizer.State = ShadowRasterizerState;
            context.OutputMerger.SetDepthStencilState(DefaultDepthStencilState, 0);
            context.OutputMerger.SetBlendState(null);
        }

        /// <summary>
        /// Binds and records one blend state.
        /// </summary>
        /// <param name="context">Device context receiving the state.</param>
        /// <param name="blendState">Blend state to bind, or <c>null</c> for opaque output.</param>
        public void BindBlendState(DeviceContext context, BlendState blendState) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            context.OutputMerger.SetBlendState(blendState);
            TrackedBlendState = blendState;
        }

        /// <summary>
        /// Applies material-defined fixed-function render state, skipping any part that is already bound.
        /// </summary>
        /// <param name="context">Device context receiving the state.</param>
        /// <param name="renderState">Material render state to bind.</param>
        public void ApplyMaterialRenderState(DeviceContext context, MaterialRenderState renderState) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            if (renderState == null) {
                throw new ArgumentNullException(nameof(renderState));
            }

            RasterizerState rasterizerState = ResolveRasterizerState(renderState);
            if (!ReferenceEquals(TrackedRasterizerState, rasterizerState)) {
                context.Rasterizer.State = rasterizerState;
                TrackedRasterizerState = rasterizerState;
            }

            DepthStencilState depthStencilState = ResolveDepthStencilState(renderState);
            if (!ReferenceEquals(TrackedDepthStencilState, depthStencilState)) {
                context.OutputMerger.SetDepthStencilState(depthStencilState, 0);
                TrackedDepthStencilState = depthStencilState;
            }

            BlendState resolvedBlendState = ResolveBlendState(renderState);
            if (!ReferenceEquals(TrackedBlendState, resolvedBlendState)) {
                context.OutputMerger.SetBlendState(resolvedBlendState);
                TrackedBlendState = resolvedBlendState;
            }
        }

        /// <summary>
        /// Resolves the DirectX11 rasterizer state required by one material render state.
        /// </summary>
        /// <param name="renderState">Material render state to translate.</param>
        /// <returns>Rasterizer state to bind.</returns>
        public RasterizerState ResolveRasterizerState(MaterialRenderState renderState) {
            if (renderState == null) {
                throw new ArgumentNullException(nameof(renderState));
            } else if (renderState.CullMode == MaterialCullMode.Back) {
                return DefaultRasterizerState;
            }

            int key = MaterialRenderStateKeyBuilder.Build(renderState);
            if (RasterizerStatesByKey.TryGetValue(key, out RasterizerState cachedState)) {
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

            RasterizerStateDescription rasterizerDescription = new RasterizerStateDescription {
                CullMode = cullMode,
                FillMode = FillMode.Solid,
                IsFrontCounterClockwise = true,
                IsDepthClipEnabled = true
            };
            RasterizerState state = new RasterizerState(Device, rasterizerDescription);
            RasterizerStatesByKey[key] = state;
            return state;
        }

        /// <summary>
        /// Resolves the DirectX11 depth-stencil state required by one material render state.
        /// </summary>
        /// <param name="renderState">Material render state to translate.</param>
        /// <returns>Depth-stencil state to bind.</returns>
        public DepthStencilState ResolveDepthStencilState(MaterialRenderState renderState) {
            if (renderState == null) {
                throw new ArgumentNullException(nameof(renderState));
            } else if (renderState.DepthTestEnabled && renderState.DepthWriteEnabled) {
                return DefaultDepthStencilState;
            }

            int key = MaterialRenderStateKeyBuilder.Build(renderState);
            if (DepthStencilStatesByKey.TryGetValue(key, out DepthStencilState cachedState)) {
                return cachedState;
            }

            DepthStencilStateDescription depthStencilDescription = new DepthStencilStateDescription {
                IsDepthEnabled = renderState.DepthTestEnabled,
                DepthWriteMask = renderState.DepthWriteEnabled ? DepthWriteMask.All : DepthWriteMask.Zero,
                DepthComparison = Comparison.Less
            };
            DepthStencilState state = new DepthStencilState(Device, depthStencilDescription);
            DepthStencilStatesByKey[key] = state;
            return state;
        }

        /// <summary>
        /// Resolves the DirectX11 blend state required by one material render state.
        /// </summary>
        /// <param name="renderState">Material render state to translate.</param>
        /// <returns>Blend state to bind, or <c>null</c> for opaque output.</returns>
        public BlendState ResolveBlendState(MaterialRenderState renderState) {
            if (renderState == null) {
                throw new ArgumentNullException(nameof(renderState));
            } else if (renderState.BlendMode == MaterialBlendMode.Opaque) {
                return null;
            } else if (renderState.BlendMode == MaterialBlendMode.AlphaBlend) {
                return AlphaBlendState;
            }

            throw new InvalidOperationException($"Unsupported material blend mode '{renderState.BlendMode}'.");
        }

        /// <summary>
        /// Releases every default and cached pipeline state owned by this cache.
        /// </summary>
        public void Dispose() {
            DisposeRasterizerStateCache();
            DisposeDepthStencilStateCache();
            DefaultDepthStencilState.Dispose();
            ShadowRasterizerState.Dispose();
            DefaultRasterizerState.Dispose();
            AlphaBlendState.Dispose();
            TrackedRasterizerState = null;
            TrackedDepthStencilState = null;
            TrackedBlendState = null;
        }

        /// <summary>
        /// Creates the blend state used for alpha-blended materials.
        /// </summary>
        /// <param name="sourceBlend">Source blend factor.</param>
        /// <param name="destinationBlend">Destination blend factor.</param>
        /// <param name="blendOperation">Blend operation.</param>
        /// <returns>Blend state instance.</returns>
        BlendState CreateAlphaBlendState(BlendOption sourceBlend, BlendOption destinationBlend, BlendOperation blendOperation) {
            BlendStateDescription blendStateDescription = new BlendStateDescription {
                AlphaToCoverageEnable = false,
                IndependentBlendEnable = false
            };

            blendStateDescription.RenderTarget[0] = new RenderTargetBlendDescription {
                IsBlendEnabled = true,
                SourceBlend = sourceBlend,
                DestinationBlend = destinationBlend,
                BlendOperation = blendOperation,
                SourceAlphaBlend = BlendOption.One,
                DestinationAlphaBlend = BlendOption.Zero,
                AlphaBlendOperation = BlendOperation.Add,
                RenderTargetWriteMask = ColorWriteMaskFlags.All
            };

            return new BlendState(Device, blendStateDescription);
        }

        /// <summary>
        /// Releases every cached non-default rasterizer state.
        /// </summary>
        void DisposeRasterizerStateCache() {
            foreach (RasterizerState state in RasterizerStatesByKey.Values) {
                state.Dispose();
            }

            RasterizerStatesByKey.Clear();
        }

        /// <summary>
        /// Releases every cached non-default depth-stencil state.
        /// </summary>
        void DisposeDepthStencilStateCache() {
            foreach (DepthStencilState state in DepthStencilStatesByKey.Values) {
                state.Dispose();
            }

            DepthStencilStatesByKey.Clear();
        }
    }
}
