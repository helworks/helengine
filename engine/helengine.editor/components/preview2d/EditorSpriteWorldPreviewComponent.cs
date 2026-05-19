namespace helengine {
    /// <summary>
    /// Renders one editor-only world-space mesh proxy for an authored sprite component.
    /// </summary>
    public sealed class EditorSpriteWorldPreviewComponent : EditorWorldSpace2DPreviewComponentBase {
        /// <summary>
        /// Authored sprite component mirrored by this preview proxy.
        /// </summary>
        readonly SpriteComponent SourceComponentValue;

        /// <summary>
        /// Initializes one sprite preview proxy bound to the supplied authored source entity and component.
        /// </summary>
        /// <param name="sourceEntity">Authored source entity mirrored by the preview proxy.</param>
        /// <param name="sourceComponent">Authored sprite component mirrored by the preview proxy.</param>
        public EditorSpriteWorldPreviewComponent(Entity sourceEntity, SpriteComponent sourceComponent)
            : base(sourceEntity) {
            SourceComponentValue = sourceComponent ?? throw new ArgumentNullException(nameof(sourceComponent));
        }

        /// <summary>
        /// Gets the authored sprite component mirrored by this preview proxy.
        /// </summary>
        public SpriteComponent SourceComponent => SourceComponentValue;

        /// <summary>
        /// Resolves the authored preview size from the sprite component.
        /// </summary>
        /// <returns>Sprite preview size in world units.</returns>
        protected override int2 ResolvePreviewSize() {
            if (SourceComponentValue.Size.X <= 0 || SourceComponentValue.Size.Y <= 0) {
                if (SourceComponentValue.Texture != null) {
                    return new int2(
                        Math.Max(1, SourceComponentValue.Texture.Width),
                        Math.Max(1, SourceComponentValue.Texture.Height));
                }
            }

            return SourceComponentValue.Size;
        }

        /// <summary>
        /// Resolves the runtime texture displayed by the preview mesh.
        /// </summary>
        /// <returns>Sprite runtime texture when present; otherwise the shared white pixel texture.</returns>
        protected override RuntimeTexture ResolvePreviewTexture() {
            if (SourceComponentValue.Texture != null) {
                return SourceComponentValue.Texture;
            }

            return TextureUtils.PixelTexture;
        }

        /// <summary>
        /// Creates the dedicated textured preview material used by sprite world-preview proxies.
        /// </summary>
        /// <param name="render3D">Renderer that will own the runtime material.</param>
        /// <returns>Configured runtime material for the sprite preview proxy.</returns>
        protected override RuntimeMaterial CreatePreviewMaterial(RenderManager3D render3D) {
            return helengine.editor.EditorWorldSpaceSpritePreviewMaterialFactory.Create(render3D, ResolvePreviewTexture());
        }
    }
}
