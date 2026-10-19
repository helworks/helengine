namespace helengine {
    /// <summary>Stores one bounded, borrowed text identity and its range in the command list's existing glyph payload arrays.</summary>
    internal struct TextCommandReuseEntry {
        /// <summary>Current text component; its scene owner must invalidate the cache before destroying it.</summary>
        public ITextDrawable2D Text;
        /// <summary>Borrowed font supplying this text's current metrics.</summary>
        public FontAsset Font;
        /// <summary>Borrowed metric object, compared with the current font rather than dereferenced after replacement.</summary>
        public FontInfo FontInfo;
        /// <summary>Borrowed runtime atlas used by the retained glyphs.</summary>
        public RuntimeTexture Texture;
        /// <summary>Render-state generation captured from the live text.</summary>
        public int Version;
        /// <summary>First glyph payload retained for this text.</summary>
        public int Start;
        /// <summary>Number of retained primary and effect glyphs.</summary>
        public int Count;
        /// <summary>Last world X origin rounded by the ordinary text emission path.</summary>
        public Real OriginX;
        /// <summary>Last world Y origin rounded by the ordinary text emission path.</summary>
        public Real OriginY;
        /// <summary>Whether this text passed hierarchy and clipping filters when its range was captured.</summary>
        public bool Visible;
        /// <summary>Captured atlas width used by glyph layout.</summary>
        public int AtlasWidth;
        /// <summary>Captured atlas height used by glyph layout.</summary>
        public int AtlasHeight;
        /// <summary>Captured physical texture width.</summary>
        public int TextureWidth;
        /// <summary>Captured physical texture height.</summary>
        public int TextureHeight;
        /// <summary>Captured line height used by wrapping and glyph placement.</summary>
        public Real LineHeight;
        /// <summary>Captured advance of a space.</summary>
        public Real SpaceWidth;
    }
}
