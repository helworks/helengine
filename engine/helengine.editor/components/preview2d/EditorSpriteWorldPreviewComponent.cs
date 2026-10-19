namespace helengine {
    /// <summary>
    /// Renders one editor-only world-space mesh proxy for an authored sprite component.
    /// </summary>
    [RunInEditor]
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
        public EditorSpriteWorldPreviewComponent(Entity sourceEntity, SpriteComponent sourceComponent, helengine.editor.EditorBuiltInShaderAssetLibrary builtInShaderLibrary, helengine.editor.EditorSessionRendererResources rendererResources)
            : base(sourceEntity, builtInShaderLibrary, rendererResources) {
            SourceComponentValue = sourceComponent ?? throw new ArgumentNullException(nameof(sourceComponent));
        }

        /// <summary>
        /// Gets the authored sprite component mirrored by this preview proxy.
        /// </summary>
        public SpriteComponent SourceComponent => SourceComponentValue;

        RuntimeModel OwnedPreviewModel;
        float3[] LastPreviewPositions;
        float4 LastSourceRect;

        /// <summary>Releases the sprite-specific affine mesh when its proxy leaves the scene.</summary>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);
            if (OwnedPreviewModel != null) {
                RendererResources.RenderManager3D.ReleaseModel(OwnedPreviewModel);
                OwnedPreviewModel = null;
            }
            LastPreviewPositions = null;
        }

        /// <summary>Uses shared local sprite corners and the complete entity matrix, preserving tilted planes and shear.</summary>
        protected override void SynchronizePreviewGeometry() {
            if (SourceComponentValue.Pivot.Equals(new float2(0f, 0f)) && SourceComponentValue.RotationRadians == 0f
                && HasUnitEntityBasis() && SourceComponentValue.SourceRect.Equals(new float4(0f, 0f, 1f, 1f))) {
                if (OwnedPreviewModel != null) {
                    Model = ResolvePreviewModel();
                    RendererResources.RenderManager3D.ReleaseModel(OwnedPreviewModel);
                    OwnedPreviewModel = null;
                    LastPreviewPositions = null;
                }
                return;
            }
            SpriteQuad2D quad = Batch2DGeometry.ResolveSpriteLocalQuad(SourceComponentValue);
            float3 topLeft = ResolvePreviewVertex(quad.TopLeft);
            float3 topRight = ResolvePreviewVertex(quad.TopRight);
            float3 bottomRight = ResolvePreviewVertex(quad.BottomRight);
            float3 bottomLeft = ResolvePreviewVertex(quad.BottomLeft);
            float3[] positions = [topLeft, topRight, bottomRight, bottomLeft, topLeft, bottomLeft, bottomRight, topRight];
            float4 sourceRect = SourceComponentValue.SourceRect;
            if (LastPreviewPositions != null && LastSourceRect.Equals(sourceRect)
                && positions.SequenceEqual(LastPreviewPositions)) {
                return;
            }
            bool viewport = helengine.editor.EditorViewportDirect2DPresentationService.TryResolveViewportOwner(SourceEntity, out _, out _);
            float topV = viewport ? (float)(sourceRect.Y) : (float)(sourceRect.Y + sourceRect.W);
            float bottomV = viewport ? (float)(sourceRect.Y + sourceRect.W) : (float)(sourceRect.Y);
            float leftU = (float)(sourceRect.X), rightU = (float)(sourceRect.X + sourceRect.Z);
            float3 normal = float3.Cross(topRight - topLeft, bottomLeft - topLeft);
            if (normal.LengthSquared() > 0f) { normal = float3.Normalize(normal); }
            ModelAsset asset = new ModelAsset {
                Id = "editor:world-space-sprite-preview-affine-quad",
                Positions = positions,
                Normals = [normal, normal, normal, normal, -normal, -normal, -normal, -normal],
                TexCoords = [new float2(leftU, topV), new float2(rightU, topV), new float2(rightU, bottomV), new float2(leftU, bottomV),
                    new float2(leftU, topV), new float2(leftU, bottomV), new float2(rightU, bottomV), new float2(rightU, topV)],
                Indices16 = [0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7]
            };
            RuntimeModel next = RendererResources.RenderManager3D.BuildModelFromRaw(asset);
            RuntimeModel previous = OwnedPreviewModel;
            OwnedPreviewModel = next;
            Model = next;
            LastPreviewPositions = positions;
            LastSourceRect = sourceRect;
            if (previous != null) { RendererResources.RenderManager3D.ReleaseModel(previous); }
        }

        bool HasUnitEntityBasis() {
            float4x4 matrix = SourceEntity.WorldTransformMatrix;
            float3 expectedRight = float4.RotateVector(float3.UnitX, SourceEntity.Orientation);
            float3 expectedDown = float4.RotateVector(float3.UnitY, SourceEntity.Orientation);
            float3 right = new float3(matrix.M11, matrix.M12, matrix.M13);
            float3 down = new float3(matrix.M21, matrix.M22, matrix.M23);
            return (right - expectedRight).LengthSquared() < 0.0000000001f
                && (down - expectedDown).LengthSquared() < 0.0000000001f;
        }

        float3 ResolvePreviewVertex(float2 local) {
            float4x4 matrix = SourceEntity.WorldTransformMatrix;
            float3 world = new float3(local.X * matrix.M11 + local.Y * matrix.M21 + matrix.M41,
                local.X * matrix.M12 + local.Y * matrix.M22 + matrix.M42,
                local.X * matrix.M13 + local.Y * matrix.M23 + matrix.M43);
            float3 presented = helengine.editor.EditorViewportDirect2DPresentationService.TransformPresentedWorldPoint(SourceEntity, world);
            float3 previewLocal = float4.RotateVector(presented - Parent.Position, float4.Inverse(Parent.Orientation));
            float3 scale = Parent.Scale;
            return new float3(previewLocal.X / scale.X, previewLocal.Y / scale.Y, previewLocal.Z / scale.Z);
        }

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

            return OwnerCore.RenderManager2D.PixelTexture;
        }

        /// <summary>
        /// Creates the dedicated textured preview material used by sprite world-preview proxies.
        /// </summary>
        /// <param name="render3D">Renderer that will own the runtime material.</param>
        /// <returns>Configured runtime material for the sprite preview proxy.</returns>
        protected override RuntimeMaterial CreatePreviewMaterial(RenderManager3D render3D, helengine.editor.EditorBuiltInShaderAssetLibrary builtInShaderLibrary) {
            return helengine.editor.EditorWorldSpaceSpritePreviewMaterialFactory.Create(render3D, ResolvePreviewTexture(), builtInShaderLibrary);
        }
    }
}
