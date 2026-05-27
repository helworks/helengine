namespace helengine.editor {
    /// <summary>
    /// Owns the hidden camera, hidden clone entity, render target, and runtime material used to capture one exact 2D preview texture.
    /// </summary>
    public sealed class EditorExact2DPreviewCaptureService : IDisposable {
        /// <summary>
        /// Renderer used to allocate render targets and preview materials.
        /// </summary>
        readonly RenderManager3D Render3D;

        /// <summary>
        /// Hidden editor entity that owns the offscreen capture camera.
        /// </summary>
        readonly EditorEntity PreviewCameraEntity;

        /// <summary>
        /// Offscreen camera component used to render the hidden 2D clone into the preview texture.
        /// </summary>
        readonly CameraComponent PreviewCameraComponentValue;

        /// <summary>
        /// Hidden editor entity that owns the cloned 2D preview component.
        /// </summary>
        readonly EditorEntity PreviewContentEntity;

        /// <summary>
        /// Runtime material used by the world-space preview quad.
        /// </summary>
        readonly RuntimeMaterial PreviewMaterialValue;

        /// <summary>
        /// Current render target used by the hidden preview camera.
        /// </summary>
        RenderTarget PreviewRenderTargetValue;

        /// <summary>
        /// Hidden cloned text component when the service is currently previewing text.
        /// </summary>
        TextComponent PreviewTextComponentValue;

        /// <summary>
        /// Hidden cloned rounded-rectangle component when the service is currently previewing a rounded rectangle.
        /// </summary>
        RoundedRectComponent PreviewRoundedRectComponentValue;

        /// <summary>
        /// Tracks the current preview texture size.
        /// </summary>
        int2 PreviewSizeValue;

        /// <summary>
        /// Tracks how many times the preview content has been synchronized for capture.
        /// </summary>
        int CaptureCountValue;

        /// <summary>
        /// Tracks whether the service has already been disposed.
        /// </summary>
        bool IsDisposedValue;

        /// <summary>
        /// Initializes one exact 2D preview capture service.
        /// </summary>
        /// <param name="render3D">Renderer used to allocate preview resources.</param>
        public EditorExact2DPreviewCaptureService(RenderManager3D render3D) {
            Render3D = render3D ?? throw new ArgumentNullException(nameof(render3D));
            PreviewSizeValue = new int2(1, 1);

            PreviewCameraEntity = EditorExact2DPreviewSceneFactory.CreatePreviewCameraEntity();
            PreviewCameraComponentValue = EditorExact2DPreviewSceneFactory.CreatePreviewCameraComponent(PreviewSizeValue);
            PreviewCameraEntity.AddComponent(PreviewCameraComponentValue);

            PreviewContentEntity = EditorExact2DPreviewSceneFactory.CreatePreviewContentEntity();
            PreviewMaterialValue = EditorExact2DPreviewMaterialFactory.Create(Render3D, TextureUtils.PixelTexture);
        }

        /// <summary>
        /// Gets the hidden preview camera used by the capture service.
        /// </summary>
        public CameraComponent PreviewCamera => PreviewCameraComponentValue;

        /// <summary>
        /// Gets the current preview render target.
        /// </summary>
        public RenderTarget PreviewRenderTarget => PreviewRenderTargetValue;

        /// <summary>
        /// Gets the runtime material bound to the current preview texture.
        /// </summary>
        public RuntimeMaterial PreviewMaterial => PreviewMaterialValue;

        /// <summary>
        /// Gets the hidden cloned text component when the service is in text-preview mode.
        /// </summary>
        public TextComponent PreviewTextComponent => PreviewTextComponentValue;

        /// <summary>
        /// Gets the hidden cloned rounded-rectangle component when the service is in rounded-rectangle-preview mode.
        /// </summary>
        public RoundedRectComponent PreviewRoundedRectComponent => PreviewRoundedRectComponentValue;

        /// <summary>
        /// Gets the number of preview synchronizations performed by the service.
        /// </summary>
        public int CaptureCount => CaptureCountValue;

        /// <summary>
        /// Synchronizes hidden capture resources for one authored text component and returns the runtime material that samples the preview target.
        /// </summary>
        /// <param name="sourceEntity">Authored source entity that owns the text component.</param>
        /// <param name="sourceComponent">Authored text component to clone into the preview scene.</param>
        /// <param name="previewSize">Exact preview render-target size.</param>
        /// <returns>Runtime material bound to the synchronized preview render target.</returns>
        public RuntimeMaterial CaptureTextPreview(Entity sourceEntity, TextComponent sourceComponent, int2 previewSize) {
            ThrowIfDisposed();
            if (sourceEntity == null) {
                throw new ArgumentNullException(nameof(sourceEntity));
            } else if (sourceComponent == null) {
                throw new ArgumentNullException(nameof(sourceComponent));
            }

            EnsureTextPreviewComponent();
            EnsureRenderTarget(previewSize);
            SynchronizeTextComponent(sourceEntity, sourceComponent);
            CaptureCountValue++;
            return PreviewMaterialValue;
        }

        /// <summary>
        /// Synchronizes hidden capture resources for one authored rounded-rectangle component and returns the runtime material that samples the preview target.
        /// </summary>
        /// <param name="sourceEntity">Authored source entity that owns the rounded-rectangle component.</param>
        /// <param name="sourceComponent">Authored rounded-rectangle component to clone into the preview scene.</param>
        /// <param name="previewSize">Exact preview render-target size.</param>
        /// <returns>Runtime material bound to the synchronized preview render target.</returns>
        public RuntimeMaterial CaptureRoundedRectPreview(Entity sourceEntity, RoundedRectComponent sourceComponent, int2 previewSize) {
            ThrowIfDisposed();
            if (sourceEntity == null) {
                throw new ArgumentNullException(nameof(sourceEntity));
            } else if (sourceComponent == null) {
                throw new ArgumentNullException(nameof(sourceComponent));
            }

            EnsureRoundedRectPreviewComponent();
            EnsureRenderTarget(previewSize);
            SynchronizeRoundedRectComponent(sourceEntity, sourceComponent);
            CaptureCountValue++;
            return PreviewMaterialValue;
        }

        /// <summary>
        /// Releases all owned preview resources.
        /// </summary>
        public void Dispose() {
            if (IsDisposedValue) {
                return;
            }

            IsDisposedValue = true;
            DisposePreviewRenderTarget();
            RemovePreviewComponent();

            Core.Instance.ObjectManager.RemoveCamera(PreviewCameraComponentValue);
            Core.Instance.ObjectManager.RemoveEntity(PreviewCameraEntity);
            PreviewCameraEntity.Dispose();

            Core.Instance.ObjectManager.RemoveEntity(PreviewContentEntity);
            PreviewContentEntity.Dispose();

            Render3D.ReleaseMaterial(PreviewMaterialValue);
        }

        /// <summary>
        /// Throws when the service has already been disposed.
        /// </summary>
        void ThrowIfDisposed() {
            if (IsDisposedValue) {
                throw new ObjectDisposedException(nameof(EditorExact2DPreviewCaptureService));
            }
        }

        /// <summary>
        /// Ensures the service currently owns a hidden cloned text component and no rounded-rectangle clone.
        /// </summary>
        void EnsureTextPreviewComponent() {
            if (PreviewTextComponentValue != null) {
                return;
            }

            RemovePreviewComponent();
            PreviewTextComponentValue = EditorExact2DPreviewSceneFactory.CreatePreviewTextComponent();
            PreviewContentEntity.AddComponent(PreviewTextComponentValue);
        }

        /// <summary>
        /// Ensures the service currently owns a hidden cloned rounded-rectangle component and no text clone.
        /// </summary>
        void EnsureRoundedRectPreviewComponent() {
            if (PreviewRoundedRectComponentValue != null) {
                return;
            }

            RemovePreviewComponent();
            PreviewRoundedRectComponentValue = EditorExact2DPreviewSceneFactory.CreatePreviewRoundedRectComponent();
            PreviewContentEntity.AddComponent(PreviewRoundedRectComponentValue);
        }

        /// <summary>
        /// Removes whichever hidden cloned preview component is currently attached to the content entity.
        /// </summary>
        void RemovePreviewComponent() {
            if (PreviewTextComponentValue != null) {
                PreviewContentEntity.RemoveComponent(PreviewTextComponentValue);
                PreviewTextComponentValue = null;
            }

            if (PreviewRoundedRectComponentValue != null) {
                PreviewContentEntity.RemoveComponent(PreviewRoundedRectComponentValue);
                PreviewRoundedRectComponentValue = null;
            }
        }

        /// <summary>
        /// Resizes the preview render target when the requested size changes.
        /// </summary>
        /// <param name="previewSize">Requested preview render-target size.</param>
        void EnsureRenderTarget(int2 previewSize) {
            int targetWidth = Math.Max(1, previewSize.X);
            int targetHeight = Math.Max(1, previewSize.Y);
            if (PreviewRenderTargetValue != null &&
                PreviewRenderTargetValue.Width == targetWidth &&
                PreviewRenderTargetValue.Height == targetHeight) {
                PreviewCameraComponentValue.Viewport = new float4(0f, 0f, targetWidth, targetHeight);
                PreviewSizeValue = new int2(targetWidth, targetHeight);
                return;
            }

            DisposePreviewRenderTarget();
            PreviewRenderTargetValue = Render3D.CreateRenderTarget(targetWidth, targetHeight);
            PreviewCameraComponentValue.RenderTarget = PreviewRenderTargetValue;
            PreviewCameraComponentValue.Viewport = new float4(0f, 0f, targetWidth, targetHeight);
            ShaderRuntimeMaterialAccess.Require(PreviewMaterialValue).Properties.SetTexture("PreviewTexture", PreviewRenderTargetValue);
            PreviewSizeValue = new int2(targetWidth, targetHeight);
        }

        /// <summary>
        /// Copies texture-affecting authored text state into the hidden cloned text component.
        /// </summary>
        /// <param name="sourceEntity">Authored source entity whose transform establishes local preview placement.</param>
        /// <param name="sourceComponent">Authored text component to clone.</param>
        void SynchronizeTextComponent(Entity sourceEntity, TextComponent sourceComponent) {
            PreviewContentEntity.LocalPosition = float3.Zero;
            PreviewContentEntity.LocalOrientation = float4.Identity;
            PreviewContentEntity.LocalScale = float3.One;
            PreviewContentEntity.Enabled = sourceEntity.Enabled;

            PreviewTextComponentValue.Texture = sourceComponent.Texture;
            PreviewTextComponentValue.Rotation = sourceComponent.Rotation;
            PreviewTextComponentValue.SourceRect = sourceComponent.SourceRect;
            PreviewTextComponentValue.Size = sourceComponent.Size;
            PreviewTextComponentValue.Color = sourceComponent.Color;
            PreviewTextComponentValue.Text = sourceComponent.Text;
            PreviewTextComponentValue.WrapText = sourceComponent.WrapText;
            PreviewTextComponentValue.Font = sourceComponent.Font;
            PreviewTextComponentValue.FontScale = sourceComponent.FontScale;
            PreviewTextComponentValue.Alignment = sourceComponent.Alignment;
            PreviewTextComponentValue.RenderOrder2D = sourceComponent.RenderOrder2D;
        }

        /// <summary>
        /// Copies texture-affecting authored rounded-rectangle state into the hidden cloned rounded-rectangle component.
        /// </summary>
        /// <param name="sourceEntity">Authored source entity whose transform establishes local preview placement.</param>
        /// <param name="sourceComponent">Authored rounded-rectangle component to clone.</param>
        void SynchronizeRoundedRectComponent(Entity sourceEntity, RoundedRectComponent sourceComponent) {
            PreviewContentEntity.LocalPosition = float3.Zero;
            PreviewContentEntity.LocalOrientation = float4.Identity;
            PreviewContentEntity.LocalScale = float3.One;
            PreviewContentEntity.Enabled = sourceEntity.Enabled;

            PreviewRoundedRectComponentValue.RenderOrder2D = sourceComponent.RenderOrder2D;
            PreviewRoundedRectComponentValue.Corners = sourceComponent.Corners;
            PreviewRoundedRectComponentValue.Rotation = sourceComponent.Rotation;
            PreviewRoundedRectComponentValue.Color = sourceComponent.Color;
            PreviewRoundedRectComponentValue.SourceRect = sourceComponent.SourceRect;
            PreviewRoundedRectComponentValue.Size = sourceComponent.Size;
            PreviewRoundedRectComponentValue.Radius = sourceComponent.Radius;
            PreviewRoundedRectComponentValue.BorderThickness = sourceComponent.BorderThickness;
            PreviewRoundedRectComponentValue.FillColor = sourceComponent.FillColor;
            PreviewRoundedRectComponentValue.BorderColor = sourceComponent.BorderColor;
        }

        /// <summary>
        /// Disposes the current preview render target when one is owned.
        /// </summary>
        void DisposePreviewRenderTarget() {
            if (PreviewRenderTargetValue is IDisposable disposableRenderTarget) {
                disposableRenderTarget.Dispose();
            }

            PreviewCameraComponentValue.RenderTarget = null;
            PreviewRenderTargetValue = null;
        }
    }
}
