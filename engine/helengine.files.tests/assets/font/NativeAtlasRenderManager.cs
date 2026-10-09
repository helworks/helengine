using helengine;

namespace helengine.files.tests.assets.font {
    /// <summary>Records decoded atlas coverage when a packaged font reader creates its runtime texture.</summary>
    sealed class NativeAtlasRenderManager : RenderManager2D {
        /// <summary>Gets the copied coverage pixels submitted by the font reader.</summary>
        public byte[] DecodedPixels { get; set; }

        /// <summary>Decodes the raw atlas and returns a metadata-only texture for the font serialization fixture.</summary>
        public override RuntimeTexture BuildTextureFromRaw(TextureAsset data) {
            DecodedPixels = TextureAssetPixelCodec.DecodeToRgba32(data);
            return new ManagedRuntimeTexture();
        }

        /// <summary>Accepts no texture-region writes during a font-reader fixture.</summary>
        protected override void UpdateTextureRegionCore(RuntimeTexture texture, int x, int y, int width, int height,
            byte[] rgba8, int sourceRowPitch) { }

        /// <summary>Leaves sprite submission outside the serialization fixture.</summary>
        public override void DrawSprite(ISpriteDrawable2D sprite) { }

        /// <summary>Leaves text submission outside the serialization fixture.</summary>
        public override void DrawText(ITextDrawable2D text) { }

        /// <summary>Leaves rounded-shape submission outside the serialization fixture.</summary>
        public override void DrawRoundedRect(IRoundedRectDrawable2D shape) { }
    }
}
