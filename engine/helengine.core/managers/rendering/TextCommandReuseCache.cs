namespace helengine {
    /// <summary>Owns fixed-capacity metadata for one queue while borrowing its texts and fonts; glyph payloads remain exclusively in the command list.</summary>
    internal sealed class TextCommandReuseCache : IDisposable {
        /// <summary>Conservative charge covering this helper, two array headers and allocator alignment on supported native pointer widths.</summary>
        internal const int OverheadBytes = 256;
        /// <summary>Conservative per-text storage charge, including its borrowed identities and captured scalar state.</summary>
        internal const int TextEntryBytes = 128;
        /// <summary>Conservative per-character storage charge, including all FontChar fields and native alignment.</summary>
        internal const int MetricEntryBytes = 48;
        /// <summary>Maximum text metadata count regardless of a larger configured budget.</summary>
        internal const int MaximumTexts = 16;
        /// <summary>Maximum unique font-character snapshots regardless of a larger configured budget.</summary>
        internal const int MaximumMetrics = 128;
        /// <summary>Maximum eligible glyph payload count; this cache never allocates glyph storage of its own.</summary>
        internal const int MaximumGlyphs = 1024;
        /// <summary>Fixed text metadata storage owned by this helper.</summary>
        [NativeOwnedMember] TextCommandReuseEntry[] Texts;
        /// <summary>Fixed exact metric storage owned by this helper.</summary>
        [NativeOwnedMember] TextCommandReuseMetric[] Metrics;
        /// <summary>Borrowed concrete queue that supplied the current retained ranges.</summary>
        RenderList2D Queue;
        /// <summary>Queue generation captured after its current depth ordering was prepared.</summary>
        int QueueVersion;
        /// <summary>Number of occupied text entries.</summary>
        int TextCount;
        /// <summary>Number of occupied font-character entries.</summary>
        int MetricCount;
        /// <summary>Whether captured metadata corresponds to complete existing glyph payloads.</summary>
        bool Valid;

        /// <summary>Allocates only fixed metadata capacities that fit the caller's conservative byte budget.</summary>
        /// <param name="budget">Maximum charged bytes including helper and array overhead.</param>
        public TextCommandReuseCache(int budget) {
            int available = budget - OverheadBytes;
            int texts = RealMath.Min(MaximumTexts, available / (TextEntryBytes + MetricEntryBytes));
            int metrics = RealMath.Min(MaximumMetrics, (available - texts * TextEntryBytes) / MetricEntryBytes);
            if (texts < 1 || metrics < 1) {
                throw new ArgumentOutOfRangeException(nameof(budget));
            }
            Texts = new TextCommandReuseEntry[texts];
            Metrics = new TextCommandReuseMetric[metrics];
        }

        /// <summary>Gets the conservative bytes reserved for fixed metadata; glyph buffers are not duplicated or included.</summary>
        public int CapacityBytes => OverheadBytes + Texts.Length * TextEntryBytes + Metrics.Length * MetricEntryBytes;
        /// <summary>Gets the fixed text slot capacity.</summary>
        public int TextCapacity => Texts.Length;
        /// <summary>Gets the fixed unique font-character capacity.</summary>
        public int MetricCapacity => Metrics.Length;
        /// <summary>Gets the number of captured text identities available for visibility preflight.</summary>
        public int Count => TextCount;
        /// <summary>Gets whether complete retained glyph ranges are available.</summary>
        public bool IsValid => Valid;

        /// <summary>Compares only queue identity without dereferencing the cached borrowed queue.</summary>
        /// <param name="queue">Current live queue.</param>
        /// <returns>True when this is the queue whose glyphs were captured.</returns>
        public bool IsSameQueue([NativeNoEscape] RenderList2D queue) => ReferenceEquals(Queue, queue);

        /// <summary>Checks queue identity and every current text/font/character value without hashes or dereferencing replaced metric objects.</summary>
        /// <param name="queue">Current live ordered queue.</param>
        /// <returns>True only when the retained layout and glyph cardinality remain valid.</returns>
        public bool Matches([NativeNoEscape] RenderList2D queue) {
            if (!Valid || !ReferenceEquals(Queue, queue) || queue.Version != QueueVersion) {
                return false;
            }
            int slot = 0;
            for (int index = 0; index < queue.Count; index++) {
                if (queue.GetPreparedDrawable(index) is ITextDrawable2D text) {
                    if (slot >= TextCount || !MatchesText(ref Texts[slot], text)) {
                        return false;
                    }
                    slot++;
                }
            }
            if (slot != TextCount) {
                return false;
            }
            for (int index = 0; index < MetricCount; index++) {
                TextCommandReuseMetric metric = Metrics[index];
                bool exists = metric.Font.Characters.TryGetValue(metric.Character, out FontChar glyph);
                if (exists != metric.Exists || (exists && !GlyphsEqual(glyph, metric.Glyph))) {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Checks current live font state against one snapshot; disposal and atlas changes invalidate reuse.</summary>
        /// <param name="entry">Stored borrowed identities and scalar values.</param>
        /// <param name="text">Current live text obtained from the queue.</param>
        /// <returns>True when every layout-relevant font and text value remains unchanged.</returns>
        static bool MatchesText([NativeNoEscape] ref TextCommandReuseEntry entry, ITextDrawable2D text) {
            FontAsset font = text.Font;
            if (!ReferenceEquals(entry.Text, text)) {
                return false;
            }
            if (entry.Version != text.TextRenderStateVersion) {
                return false;
            }
            if (!ReferenceEquals(entry.Font, font)) {
                return false;
            }
            if (font == null) {
                return false;
            }
            if (font.IsDisposed) {
                return false;
            }
            if (font.Characters == null) {
                return false;
            }
            if (!ReferenceEquals(entry.FontInfo, font.FontInfo)) {
                return false;
            }
            if (!ReferenceEquals(entry.Texture, font.Texture)) {
                return false;
            }
            if (font.Texture == null) {
                return false;
            }
            if (font.Texture.IsDisposed) {
                return false;
            }
            if (entry.AtlasWidth != font.AtlasWidth) {
                return false;
            }
            if (entry.AtlasHeight != font.AtlasHeight) {
                return false;
            }
            if (entry.TextureWidth != font.Texture.Width) {
                return false;
            }
            if (entry.TextureHeight != font.Texture.Height) {
                return false;
            }
            if (entry.LineHeight != font.LineHeight) {
                return false;
            }
            if (entry.SpaceWidth != (font.FontInfo == null ? 0f : font.FontInfo.SpaceWidth)) {
                return false;
            }
            return true;
        }

        /// <summary>Compares all glyph values exactly; missing glyphs are compared separately and no hash collision can admit stale metrics.</summary>
        /// <param name="left">Current glyph value.</param>
        /// <param name="right">Captured glyph value.</param>
        /// <returns>True when UV, advances, offsets and bearings match.</returns>
        static bool GlyphsEqual(FontChar left, FontChar right) {
            if (left.SourceRect.X != right.SourceRect.X) {
                return false;
            }
            if (left.SourceRect.Y != right.SourceRect.Y) {
                return false;
            }
            if (left.SourceRect.Z != right.SourceRect.Z) {
                return false;
            }
            if (left.SourceRect.W != right.SourceRect.W) {
                return false;
            }
            if (left.AdvanceWidth != right.AdvanceWidth) {
                return false;
            }
            if (left.OffsetY != right.OffsetY) {
                return false;
            }
            if (left.BearingX != right.BearingX) {
                return false;
            }
            if (left.BearingY != right.BearingY) {
                return false;
            }
            return true;
        }

        /// <summary>Captures bounded current font state before a full build; exceeding either capacity leaves ordinary rendering active.</summary>
        /// <param name="queue">Live concrete queue borrowed until explicit invalidation.</param>
        /// <returns>True when this queue fits the fixed metadata capacities.</returns>
        public bool Capture([NativeRetainsBorrow] RenderList2D queue) {
            Invalidate();
            QueueVersion = queue.Version;
            for (int index = 0; index < queue.Count; index++) {
                if (queue.GetPreparedDrawable(index) is ITextDrawable2D text) {
                    FontAsset font = text.Font;
                    if (TextCount >= Texts.Length || font == null || font.IsDisposed || font.Characters == null
                        || font.Texture == null || font.Texture.IsDisposed || text.Parent == null) {
                        Invalidate();
                        return false;
                    }
                    TextCommandReuseEntry entry = new TextCommandReuseEntry();
                    entry.Text = text;
                    entry.Font = font;
                    entry.FontInfo = font.FontInfo;
                    entry.Texture = font.Texture;
                    entry.Version = text.TextRenderStateVersion;
                    entry.AtlasWidth = font.AtlasWidth;
                    entry.AtlasHeight = font.AtlasHeight;
                    entry.TextureWidth = font.Texture.Width;
                    entry.TextureHeight = font.Texture.Height;
                    entry.LineHeight = font.LineHeight;
                    entry.SpaceWidth = font.FontInfo == null ? 0f : font.FontInfo.SpaceWidth;
                    entry.OriginX = RealMath.Round(text.Parent.Position.X);
                    entry.OriginY = RealMath.Round(text.Parent.Position.Y);
                    Texts[TextCount++] = entry;
                    string content = text.Text ?? string.Empty;
                    for (int characterIndex = 0; characterIndex < content.Length; characterIndex++) {
                        if (!CaptureMetric(font, content[characterIndex])) {
                            Invalidate();
                            return false;
                        }
                    }
                }
            }
            if (TextCount == 0) {
                Invalidate();
                return false;
            }
            Queue = queue;
            return true;
        }

        /// <summary>Records one unique input character without growing the fixed snapshot array.</summary>
        /// <param name="font">Live font retained only as a borrowed identity.</param>
        /// <param name="character">Raw input character, including currently missing glyphs.</param>
        /// <returns>False when no snapshot slot remains.</returns>
        bool CaptureMetric([NativeRetainsBorrow] FontAsset font, char character) {
            for (int index = 0; index < MetricCount; index++) {
                if (ReferenceEquals(Metrics[index].Font, font) && Metrics[index].Character == character) {
                    return true;
                }
            }
            if (MetricCount >= Metrics.Length) {
                return false;
            }
            TextCommandReuseMetric metric = new TextCommandReuseMetric();
            metric.Font = font;
            metric.Character = character;
            metric.Exists = font.Characters.TryGetValue(character, out FontChar glyph);
            metric.Glyph = glyph;
            Metrics[MetricCount++] = metric;
            return true;
        }

        /// <summary>Finds a text range by current reference identity without dereferencing stale metadata identities.</summary>
        /// <param name="text">Current live text being visited.</param>
        /// <returns>The metadata slot, or minus one when this text was not captured.</returns>
        public int Find([NativeNoEscape] ITextDrawable2D text) {
            for (int index = 0; index < TextCount; index++) {
                if (ReferenceEquals(Texts[index].Text, text)) {
                    return index;
                }
            }
            return -1;
        }

        /// <summary>Reads one value snapshot whose references remain borrowed from the current live queue.</summary>
        /// <param name="index">Metadata slot.</param>
        /// <returns>The copied value snapshot.</returns>
        public TextCommandReuseEntry Get(int index) => Texts[index];

        /// <summary>Updates the retained range and last emitted origin after the ordinary text emitter completed.</summary>
        /// <param name="index">Metadata slot.</param>
        /// <param name="start">First payload in the existing glyph buffer.</param>
        /// <param name="count">Number of emitted glyph/effect payloads.</param>
        public void SetRange(int index, int start, int count) {
            TextCommandReuseEntry entry = Texts[index];
            entry.Start = start;
            entry.Count = count;
            entry.Visible = true;
            entry.OriginX = RealMath.Round(entry.Text.Parent.Position.X);
            entry.OriginY = RealMath.Round(entry.Text.Parent.Position.Y);
            Texts[index] = entry;
        }

        /// <summary>Accepts captured metadata only after a complete build within the fixed retained glyph limit.</summary>
        /// <param name="glyphCount">Actual number of glyph payloads produced by the full build.</param>
        public void Commit(int glyphCount) {
            if (glyphCount > MaximumGlyphs) {
                Invalidate();
            } else {
                Valid = true;
            }
        }

        /// <summary>Clears every borrowed reference before scene/resource teardown without disposing any scene-owned object.</summary>
        public void Invalidate() {
            for (int index = 0; index < TextCount; index++) {
                Texts[index] = new TextCommandReuseEntry();
            }
            for (int index = 0; index < MetricCount; index++) {
                Metrics[index] = new TextCommandReuseMetric();
            }
            TextCount = 0;
            MetricCount = 0;
            Queue = null;
            Valid = false;
        }

        /// <summary>Releases only metadata arrays; borrowed fonts, textures, texts and queues remain owned by their original owners.</summary>
        public void Dispose() {
            Invalidate();
            NativeOwnership.Release(ref Texts);
            NativeOwnership.Release(ref Metrics);
        }
    }
}
