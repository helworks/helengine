namespace helengine {
    /// <summary>Reads the version-six page directory without loading referenced pixel assets.</summary>
    public static class FontPageBinary {
        /// <summary>Reads bounded descriptors and rejects glyphs that refer to a nonexistent atlas.</summary>
        public static FontPage[] Read(EngineBinaryReader reader, Dictionary<char, FontChar> characters) {
            int count = reader.ReadInt32();
            if (count < 1 || count > 1024) { throw new InvalidOperationException("Paged fonts require between one and 1024 additional atlases."); }
            FontPage[] pages = new FontPage[count];
            for (int index = 0; index < count; index++) { pages[index] = new FontPage(reader.ReadString(), reader.ReadInt32(), reader.ReadInt32()); }
            foreach (FontChar glyph in characters.Values) {
                if (glyph.PageIndex < 0 || glyph.PageIndex > count) { throw new InvalidOperationException("Font glyph references an invalid atlas page."); }
            }
            return pages;
        }
    }
}
