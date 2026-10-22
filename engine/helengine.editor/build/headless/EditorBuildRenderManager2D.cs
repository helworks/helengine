namespace helengine.editor {
    /// <summary>Materializes and updates authoring texture data on the CPU; presentation requires a real runtime renderer.</summary>
    public sealed class EditorBuildRenderManager2D : RenderManager2D {
        /// <summary>Live textures whose pixel storage must be retired when the authoring session ends.</summary>
        readonly HashSet<EditorBuildRuntimeTexture> Textures = new();
        /// <summary>Prevents resource creation after this renderer's lifetime has ended.</summary>
        bool IsDisposed;

        /// <summary>Decodes a raw asset into an independently owned RGBA8 runtime texture.</summary>
        /// <param name="data">Borrowed raw texture data.</param>
        /// <returns>CPU runtime texture with its actual decoded pixel data.</returns>
        public override RuntimeTexture BuildTextureFromRaw(TextureAsset data) {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            EditorBuildRuntimeTexture texture = new(this, data);
            Textures.Add(texture);
            return texture;
        }

        /// <summary>Validates renderer ownership before copying a region into the texture's CPU pixels.</summary>
        /// <param name="texture">Owned destination texture.</param>
        /// <param name="x">Destination left edge.</param>
        /// <param name="y">Destination top edge.</param>
        /// <param name="width">Copied row width.</param>
        /// <param name="height">Copied row count.</param>
        /// <param name="rgba8">Borrowed source pixels.</param>
        /// <param name="sourceRowPitch">Source row stride.</param>
        protected override void UpdateTextureRegionCore(RuntimeTexture texture, int x, int y, int width, int height, byte[] rgba8, int sourceRowPitch) {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            RequireOwnedTexture(texture).UpdateRegion(x, y, width, height, rgba8, sourceRowPitch);
        }

        /// <summary>Releases a CPU texture immediately; no GPU work can defer this ownership handoff.</summary>
        /// <param name="texture">Texture created by this authoring renderer.</param>
        public override void ReleaseTexture(RuntimeTexture texture) {
            EditorBuildRuntimeTexture ownedTexture = RequireOwnedTexture(texture);
            Textures.Remove(ownedTexture);
            ownedTexture.Dispose();
        }

        /// <summary>Releases fallback textures and every remaining texture owned by the authoring session.</summary>
        public override void Dispose() {
            if (IsDisposed) {
                return;
            }
            base.Dispose();
            foreach (EditorBuildRuntimeTexture texture in Textures) {
                texture.Dispose();
            }
            Textures.Clear();
            IsDisposed = true;
        }

        /// <summary>Rejects GPU frame presentation in the asset-only authoring host.</summary>
        public override void Draw() => throw new NotSupportedException("Headless asset authoring cannot draw or present a 2D frame.");
        /// <summary>Rejects sprite rasterization in the asset-only authoring host.</summary>
        /// <param name="sprite">Requested sprite draw.</param>
        public override void DrawSprite(ISpriteDrawable2D sprite) => throw new NotSupportedException("Headless asset authoring cannot rasterize sprites.");
        /// <summary>Rejects text rasterization in the asset-only authoring host.</summary>
        /// <param name="text">Requested text draw.</param>
        public override void DrawText(ITextDrawable2D text) => throw new NotSupportedException("Headless asset authoring cannot rasterize text.");
        /// <summary>Rejects shape rasterization in the asset-only authoring host.</summary>
        /// <param name="shape">Requested rounded rectangle.</param>
        public override void DrawRoundedRect(IRoundedRectDrawable2D shape) => throw new NotSupportedException("Headless asset authoring cannot rasterize rounded rectangles.");

        /// <summary>Rejects accidental cross-session texture use, including resources from another CPU renderer.</summary>
        /// <param name="texture">Runtime resource being used or released.</param>
        /// <returns>The texture whose ownership belongs to this renderer.</returns>
        EditorBuildRuntimeTexture RequireOwnedTexture(RuntimeTexture texture) {
            ArgumentNullException.ThrowIfNull(texture);
            if (texture is not EditorBuildRuntimeTexture ownedTexture || !ReferenceEquals(ownedTexture.Owner, this)) {
                throw new ArgumentException("Texture was not created by this headless authoring renderer.", nameof(texture));
            }
            return ownedTexture;
        }
    }
}
