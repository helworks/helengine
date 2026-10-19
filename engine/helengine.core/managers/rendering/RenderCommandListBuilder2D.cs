namespace helengine {
    /// <summary>
    /// Walks the current 2D render queue and emits one reusable flat command list for backend consumption.
    /// </summary>
    public sealed class RenderCommandListBuilder2D : IRenderVisitor2D, IDisposable {
        /// <summary>
        /// Reusable command list populated during the current build pass.
        /// </summary>
        RenderCommandList2D CommandListValue;

        /// <summary>
        /// Reusable clip-chain resolver used to compare active ancestor clip ownership between drawables.
        /// </summary>
        readonly ClipRegionStackBuilder2D ClipRegionStackBuilder;

        /// <summary>
        /// Clip chain currently active in the emitted command stream.
        /// </summary>
        readonly List<IClipRegion2D> ActiveClipChain;

        /// <summary>
        /// Clip chain resolved for the drawable currently being visited.
        /// </summary>
        readonly List<IClipRegion2D> NextClipChain;

        /// <summary>
        /// Reusable horizontal text offsets for the lines currently being emitted.
        /// </summary>
        readonly List<RealEvaluation> TextLineOffsets;

        /// <summary>
        /// Queue that produced the currently reusable static-text command list.
        /// </summary>
        IRenderQueue2D CachedStaticTextRenderQueue;

        /// <summary>
        /// Signature of the text state and resolved parent positions stored in the reusable static-text command list.
        /// </summary>
        int CachedStaticTextRenderQueueSignature;

        /// <summary>
        /// Tracks whether the current command list was built from a queue eligible for static-text reuse.
        /// </summary>
        bool HasCachedStaticTextRenderQueue;

        /// <summary>
        /// Tracks whether the native reusable builder state was already released.
        /// </summary>
        bool IsDisposedValue;

        /// <summary>Optional borrowed timing sink whose owner must outlive every build using it; the builder never disposes or deletes the sink.</summary>
        IRuntimeCpuProfileSink CpuProfileSinkValue;

        /// <summary>Optional fixed metadata storage; glyph payloads remain owned solely by CommandListValue.</summary>
        [NativeOwnedMember] TextCommandReuseCache TextReuseCache;

        /// <summary>Whether the current visit reuses previously validated glyph ranges.</summary>
        bool ReusingTextPayloads;

        /// <summary>Whether the current full build records ranges for a newly captured queue.</summary>
        bool CapturingTextPayloads;

        /// <summary>Requested metadata allowance; a positive but insufficient allowance uses ordinary builds instead of the legacy signature fast path.</summary>
        int TextReuseMemoryBudgetBytes;

        /// <summary>Gets the conservative metadata allocation charge, excluding the already existing command payload buffers.</summary>
        public int TextCommandReuseMetadataCapacityBytes => TextReuseCache == null ? 0 : TextReuseCache.CapacityBytes;

        /// <summary>Gets the fixed text slot capacity, capped at sixteen regardless of the configured byte budget.</summary>
        public int TextCommandReuseTextCapacity => TextReuseCache == null ? 0 : TextReuseCache.TextCapacity;

        /// <summary>Gets the fixed unique font-character snapshot capacity, capped at one hundred and twenty-eight.</summary>
        public int TextCommandReuseMetricCapacity => TextReuseCache == null ? 0 : TextReuseCache.MetricCapacity;

        /// <summary>Gets the glyph payload count retained for the one validated queue, or zero when reuse is invalid.</summary>
        public int CachedGlyphCount => TextReuseCache != null && TextReuseCache.IsValid ? CommandListValue.GlyphCount : 0;

        /// <summary>
        /// Initializes one reusable 2D command builder.
        /// </summary>
        public RenderCommandListBuilder2D() {
            ClipRegionStackBuilder = new ClipRegionStackBuilder2D();
            ActiveClipChain = new List<IClipRegion2D>();
            NextClipChain = new List<IClipRegion2D>();
            TextLineOffsets = new List<RealEvaluation>();
        }

        /// <summary>Enables coarse nonoverlapping build timing, or disables all clock reads when the optional borrowed sink is null.</summary>
        /// <param name="sink">Host-owned timing sink retained only as a borrowed reference.</param>
        public void SetCpuProfileSink([NativeRetainsBorrow] IRuntimeCpuProfileSink sink) {
            CpuProfileSinkValue = sink;
        }

        /// <summary>Configures fixed mixed-queue metadata; zero preserves the existing text-only path, while positive insufficient budgets allocate nothing and rebuild normally.</summary>
        /// <param name="budget">Nonnegative maximum conservative metadata bytes, including helper and array overhead.</param>
        public void SetTextCommandReuseMemoryBudgetBytes(int budget) {
            if (budget < 0) {
                throw new ArgumentOutOfRangeException(nameof(budget));
            }
            InvalidateTextCommandReuse();
            NativeOwnership.Release(ref TextReuseCache);
            TextReuseMemoryBudgetBytes = budget;
            if (budget >= TextCommandReuseCache.OverheadBytes + TextCommandReuseCache.TextEntryBytes + TextCommandReuseCache.MetricEntryBytes) {
                TextReuseCache = new TextCommandReuseCache(budget);
            }
        }

        /// <summary>Clears borrowed identities and glyph payload references before scene, font or texture owners destroy their resources.</summary>
        public void InvalidateTextCommandReuse() {
            if (TextReuseCache != null) {
                TextReuseCache.Invalidate();
            }
            if (CommandListValue != null) {
                CommandListValue.Reset();
            }
            HasCachedStaticTextRenderQueue = false;
            CachedStaticTextRenderQueue = null;
            CachedStaticTextRenderQueueSignature = 0;
            ReusingTextPayloads = false;
            CapturingTextPayloads = false;
        }

        /// <summary>Reads the host clock only when coarse profiling is enabled.</summary>
        /// <returns>The starting monotonic microseconds, or zero without accessing a clock when profiling is disabled.</returns>
        ulong BeginCpuProfileStage() {
            return CpuProfileSinkValue == null ? 0UL : CpuProfileSinkValue.ReadMicroseconds();
        }

        /// <summary>Records one completed segment without nesting other profile stages and reports a backwards host clock explicitly.</summary>
        /// <param name="stage">Stable command-builder phase identifier.</param>
        /// <param name="start">Timestamp captured immediately before this segment's work.</param>
        void EndCpuProfileStage(RuntimeCpuProfileStage stage, ulong start) {
            if (CpuProfileSinkValue == null) { return; }
            ulong end = CpuProfileSinkValue.ReadMicroseconds();
            if (end < start) {
                CpuProfileSinkValue.ClockFault();
            } else {
                CpuProfileSinkValue.RecordStage(stage, end - start);
            }
        }

        /// <summary>
        /// Builds one resolved command list from the supplied render queue.
        /// </summary>
        /// <param name="renderQueue">Ordered render queue to flatten.</param>
        /// <returns>Reusable command list containing resolved commands for the queue.</returns>
        public RenderCommandList2D Build(IRenderQueue2D renderQueue) {
            if (renderQueue == null) {
                throw new ArgumentNullException(nameof(renderQueue));
            }

            ulong setupStart = BeginCpuProfileStage();
            int staticTextRenderQueueSignature = 0;
            bool canReuseStaticTextCommandList = TextReuseMemoryBudgetBytes == 0
                && TryResolveStaticTextRenderQueueSignature(renderQueue, out staticTextRenderQueueSignature);
            if (canReuseStaticTextCommandList
                && HasCachedStaticTextRenderQueue
                && ReferenceEquals(renderQueue, CachedStaticTextRenderQueue)
                && staticTextRenderQueueSignature == CachedStaticTextRenderQueueSignature) {
                EndCpuProfileStage(RuntimeCpuProfileStage.CommandListSetup, setupStart);
                return CommandListValue;
            }

            ReusingTextPayloads = false;
            CapturingTextPayloads = false;
            bool preserveGlyphPayloads = false;
            if (TextReuseCache != null) {
                if (renderQueue is RenderList2D concreteQueue) {
                    ReusingTextPayloads = TextReuseCache.Matches(concreteQueue) && MatchesTextVisibility();
                    preserveGlyphPayloads = ReusingTextPayloads
                        || (TextReuseCache.IsValid && !TextReuseCache.IsSameQueue(concreteQueue) && !QueueContainsText(concreteQueue));
                    if (!preserveGlyphPayloads) {
                        CapturingTextPayloads = TextReuseCache.Capture(concreteQueue);
                    }
                } else {
                    TextReuseCache.Invalidate();
                }
            }
            if (CommandListValue == null) {
                CommandListValue = new RenderCommandList2D(RealMath.Max(renderQueue.Count, 4));
            } else if (preserveGlyphPayloads) {
                CommandListValue.ResetPreservingGlyphPayloads();
            } else {
                CommandListValue.Reset();
            }

            ActiveClipChain.Clear();
            NextClipChain.Clear();
            EndCpuProfileStage(RuntimeCpuProfileStage.CommandListSetup, setupStart);
            renderQueue.VisitOrdered(this);
            ulong trailingStart = BeginCpuProfileStage();
            EmitTrailingClipPops();
            EndCpuProfileStage(RuntimeCpuProfileStage.CommandListTrailingClips, trailingStart);
            ulong cacheStart = BeginCpuProfileStage();
            if (CapturingTextPayloads) {
                TextReuseCache.Commit(CommandListValue.GlyphCount);
            }
            bool canCacheStaticTextCommandList = canReuseStaticTextCommandList && !ContainsClipCommands();
            if (canCacheStaticTextCommandList) {
                CachedStaticTextRenderQueue = renderQueue;
                CachedStaticTextRenderQueueSignature = staticTextRenderQueueSignature;
                HasCachedStaticTextRenderQueue = true;
            } else {
                CachedStaticTextRenderQueue = null;
                CachedStaticTextRenderQueueSignature = 0;
                HasCachedStaticTextRenderQueue = false;
            }
            EndCpuProfileStage(RuntimeCpuProfileStage.CommandListSetup, cacheStart);

            return CommandListValue;
        }

        /// <summary>Checks visibility before resetting commands, so newly visible text never reuses an absent or incompatible range.</summary>
        /// <returns>True when hierarchy and full-clip decisions still match every captured text.</returns>
        bool MatchesTextVisibility() {
            for (int index = 0; index < TextReuseCache.Count; index++) {
                TextCommandReuseEntry entry = TextReuseCache.Get(index);
                ITextDrawable2D text = entry.Text;
                bool visible = text.Parent != null && text.Parent.IsHierarchyEnabled;
                if (visible) {
                    ClipRegionStackBuilder.BuildClipChain(text, NextClipChain);
                    visible = !ShouldSkipDrawableBecauseItIsFullyClipped(text);
                }
                if (visible != entry.Visible) {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Examines the current concrete queue without dereferencing any cached queue, allowing a foreign nonglyph loading pass to preserve menu payloads.</summary>
        /// <param name="queue">Current live camera queue.</param>
        /// <returns>True when even one current drawable is a text drawable.</returns>
        static bool QueueContainsText([NativeNoEscape] RenderList2D queue) {
            int version = queue.Version;
            for (int index = 0; index < queue.Count; index++) {
                if (queue.GetPreparedDrawable(index) is ITextDrawable2D) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Determines whether the current command list contains clipping state that can change through an ancestor independent of text render state.
        /// </summary>
        /// <returns><c>true</c> when the command list contains clip commands and must not use static-text reuse.</returns>
        bool ContainsClipCommands() {
            for (int commandIndex = 0; commandIndex < CommandListValue.Count; commandIndex++) {
                RenderCommand2DType commandType = CommandListValue.GetCommandType(commandIndex);
                if (commandType == RenderCommand2DType.ClipPush || commandType == RenderCommand2DType.ClipPop) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Releases the reusable command-list state and native clip-chain lists owned by this builder.
        /// </summary>
        public void Dispose() {
            NativeOwnership.Release(ref TextReuseCache);
            if (IsDisposedValue) {
                return;
            }

            ActiveClipChain.Clear();
            NextClipChain.Clear();
            NativeOwnership.DisposeAndDelete(CommandListValue);
            NativeOwnership.Delete(ClipRegionStackBuilder);
            NativeOwnership.Delete(ActiveClipChain);
            NativeOwnership.Delete(NextClipChain);
            NativeOwnership.Delete(TextLineOffsets);
            CommandListValue = null;
            CachedStaticTextRenderQueue = null;
            CachedStaticTextRenderQueueSignature = 0;
            HasCachedStaticTextRenderQueue = false;
            CpuProfileSinkValue = null;
            IsDisposedValue = true;
        }

        /// <summary>
        /// Resolves a signature for a concrete queue containing only static text drawables, or rejects queues that contain other drawable types.
        /// </summary>
        /// <param name="renderQueue">Queue considered for command-list reuse.</param>
        /// <param name="signature">Stable signature for the current text content and resolved positions when reuse is safe.</param>
        /// <returns><c>true</c> when the queue can reuse its previously resolved text command list.</returns>
        bool TryResolveStaticTextRenderQueueSignature(IRenderQueue2D renderQueue, out int signature) {
            signature = renderQueue.Version;
            RenderList2D renderList = renderQueue as RenderList2D;
            if (renderList == null || renderList.Count == 0) {
                return false;
            }

            unchecked {
                for (int index = 0; index < renderList.Count; index++) {
                    IDrawable2D drawable = renderList.GetPreparedDrawable(index);
                    if (!(drawable is ITextDrawable2D)) {
                        return false;
                    }

                    ITextDrawable2D text = (ITextDrawable2D)drawable;
                    if (text.Parent == null) {
                        return false;
                    }

                    float3 position = text.Parent.Position;
                    signature = (signature * 31) + text.TextRenderStateVersion;
                    signature = (signature * 31) + position.GetHashCode();
                    signature = (signature * 31) + text.Parent.IsHierarchyEnabled.GetHashCode();
                }
            }

            return true;
        }

        /// <summary>
        /// Visits one 2D drawable and emits the matching resolved command payload when the drawable is renderable.
        /// </summary>
        /// <param name="drawable">Drawable visited from the ordered render queue.</param>
        public void Visit(IDrawable2D drawable) {
            ulong clippingStart = BeginCpuProfileStage();
            if (drawable == null || drawable.Parent == null || !drawable.Parent.IsHierarchyEnabled) {
                EndCpuProfileStage(RuntimeCpuProfileStage.CommandListClipping, clippingStart);
                return;
            }

            ClipRegionStackBuilder.BuildClipChain(drawable, NextClipChain);
            if (ShouldSkipDrawableBecauseItIsFullyClipped(drawable)) {
                EndCpuProfileStage(RuntimeCpuProfileStage.CommandListClipping, clippingStart);
                return;
            }

            SyncClipTransitions();
            EndCpuProfileStage(RuntimeCpuProfileStage.CommandListClipping, clippingStart);

            if (drawable is ISpriteDrawable2D sprite) {
                ulong spriteStart = BeginCpuProfileStage();
                EmitSprite(sprite);
                EndCpuProfileStage(RuntimeCpuProfileStage.CommandListSprites, spriteStart);
                return;
            }

            if (drawable is ITextDrawable2D text) {
                EmitReusableText(text);
                return;
            }

            if (drawable is IRoundedRectDrawable2D roundedRect) {
                ulong shapeStart = BeginCpuProfileStage();
                EmitRoundedRect(roundedRect);
                EndCpuProfileStage(RuntimeCpuProfileStage.CommandListRoundedRects, shapeStart);
                return;
            }

            throw new InvalidOperationException("Unsupported 2D drawable type.");
        }

        /// <summary>
        /// Pops any remaining active clip regions after the last drawable in the queue has been emitted.
        /// </summary>
        void EmitTrailingClipPops() {
            while (ActiveClipChain.Count > 0) {
                CommandListValue.AddClipPop();
                ActiveClipChain.RemoveAt(ActiveClipChain.Count - 1);
            }
        }

        /// <summary>
        /// Synchronizes the emitted clip stack with the clip chain required by the current drawable.
        /// </summary>
        void SyncClipTransitions() {
            int sharedPrefixLength = GetSharedPrefixLength();

            while (ActiveClipChain.Count > sharedPrefixLength) {
                CommandListValue.AddClipPop();
                ActiveClipChain.RemoveAt(ActiveClipChain.Count - 1);
            }

            while (ActiveClipChain.Count < NextClipChain.Count) {
                IClipRegion2D clipRegion = NextClipChain[ActiveClipChain.Count];
                float4 resolvedRect = ResolveClipRectForPush(clipRegion);
                CommandListValue.AddClipPush(resolvedRect);
                ActiveClipChain.Add(clipRegion);
            }
        }

        /// <summary>
        /// Gets the number of leading clip owners shared between the active and next clip chains.
        /// </summary>
        /// <returns>Shared clip-chain prefix length.</returns>
        int GetSharedPrefixLength() {
            int sharedPrefixLength = 0;
            int maxSharedLength = RealMath.Min(ActiveClipChain.Count, NextClipChain.Count);
            while (sharedPrefixLength < maxSharedLength &&
                   ReferenceEquals(ActiveClipChain[sharedPrefixLength], NextClipChain[sharedPrefixLength])) {
                sharedPrefixLength++;
            }

            return sharedPrefixLength;
        }

        /// <summary>
        /// Resolves the effective clip rectangle for one pushed clip owner by intersecting it with the current active clip chain.
        /// </summary>
        /// <param name="clipRegion">Clip owner being pushed.</param>
        /// <returns>Resolved effective clip rectangle.</returns>
        float4 ResolveClipRectForPush(IClipRegion2D clipRegion) {
            float4 resolvedRect = clipRegion.GetClipRect();
            if (ActiveClipChain.Count <= 0) {
                return resolvedRect;
            }

            float4 currentRect = ActiveClipChain[ActiveClipChain.Count - 1].GetClipRect();
            return ClipRegionStackBuilder.Intersect(currentRect, resolvedRect);
        }

        /// <summary>
        /// Determines whether one drawable lies completely outside the effective clip chain that would constrain it.
        /// </summary>
        /// <param name="drawable">Drawable candidate being visited.</param>
        /// <returns>True when the drawable can be skipped because it contributes no visible pixels inside its effective clip rectangle.</returns>
        bool ShouldSkipDrawableBecauseItIsFullyClipped(IDrawable2D drawable) {
            if (drawable == null) {
                return true;
            }
            if (NextClipChain.Count <= 0) {
                return false;
            }
            if (!TryResolveDrawableBounds(drawable, out float4 drawableBounds)) {
                return false;
            }

            float4 effectiveClipRect = ResolveEffectiveClipRectForNextDrawable();
            return !RectsOverlap(drawableBounds, effectiveClipRect);
        }

        /// <summary>
        /// Resolves the effective clip rectangle that will constrain the drawable currently stored in <see cref="NextClipChain"/>.
        /// </summary>
        /// <returns>Effective clip rectangle for the next drawable.</returns>
        float4 ResolveEffectiveClipRectForNextDrawable() {
            float4 effectiveRect = NextClipChain[0].GetClipRect();
            for (int index = 1; index < NextClipChain.Count; index++) {
                effectiveRect = ClipRegionStackBuilder.Intersect(effectiveRect, NextClipChain[index].GetClipRect());
            }

            return effectiveRect;
        }

        /// <summary>
        /// Attempts to resolve conservative screen-space bounds for one 2D drawable.
        /// </summary>
        /// <param name="drawable">Drawable whose bounds should be resolved.</param>
        /// <param name="bounds">Resolved bounds when available.</param>
        /// <returns>True when conservative bounds were resolved successfully.</returns>
        bool TryResolveDrawableBounds(IDrawable2D drawable, out float4 bounds) {
            bounds = new float4();
            if (drawable == null || drawable.Parent == null) {
                return false;
            }

            if (drawable is ISpriteDrawable2D sprite) {
                if (sprite.Texture == null) {
                    return false;
                }

                int2 size = sprite.Size;
                RealEvaluation width = size.X > 0 ? size.X : sprite.Texture.Width;
                RealEvaluation height = size.Y > 0 ? size.Y : sprite.Texture.Height;
                float3 position = sprite.Parent.Position;
                bounds = new float4(position.X, position.Y, width, height);
                return true;
            }

            if (drawable is ITextDrawable2D text) {
                if (text.Size.X <= 0 || text.Size.Y <= 0) {
                    return false;
                }

                float3 position = text.Parent.Position;
                bounds = new float4(position.X, position.Y, text.Size.X, text.Size.Y);
                return true;
            }

            if (drawable is IRoundedRectDrawable2D roundedRect) {
                if (roundedRect.Size.X <= 0 || roundedRect.Size.Y <= 0) {
                    return false;
                }

                float3 position = roundedRect.Parent.Position;
                bounds = new float4(position.X, position.Y, roundedRect.Size.X, roundedRect.Size.Y);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether two axis-aligned rectangles overlap with positive area.
        /// </summary>
        /// <param name="first">First rectangle expressed as X, Y, Width, Height.</param>
        /// <param name="second">Second rectangle expressed as X, Y, Width, Height.</param>
        /// <returns>True when the rectangles overlap with positive width and height.</returns>
        bool RectsOverlap(float4 first, float4 second) {
            if (first.Z <= 0f || first.W <= 0f || second.Z <= 0f || second.W <= 0f) {
                return false;
            }

            RealEvaluation firstRight = first.X + first.Z;
            RealEvaluation firstBottom = first.Y + first.W;
            RealEvaluation secondRight = second.X + second.Z;
            RealEvaluation secondBottom = second.Y + second.W;
            return first.X < secondRight &&
                   firstRight > second.X &&
                   first.Y < secondBottom &&
                   firstBottom > second.Y;
        }

        /// <summary>
        /// Emits one textured-quad command for the supplied sprite.
        /// </summary>
        /// <param name="sprite">Sprite drawable to flatten.</param>
        void EmitSprite(ISpriteDrawable2D sprite) {
            if (sprite.Texture == null) {
                return;
            }

            CommandListValue.AddTexturedQuadGeometry(sprite.Texture,
                Batch2DGeometry.ResolveSpriteQuad(sprite), sprite.SourceRect, sprite.Color);
        }

        /// <summary>
        /// Emits ordered effect and primary glyph-quad commands for the supplied text drawable.
        /// </summary>
        /// <param name="text">Text drawable to flatten.</param>
        void EmitText(ITextDrawable2D text) {
            ulong layoutStart = BeginCpuProfileStage();
            FontAsset font = text.Font;
            RealEvaluation fontScale = RealMath.Max((RealEvaluation)text.FontScale, 0.0001d);
            string content = text.Text ?? string.Empty;
            if (text.WrapText) {
                content = TextLayoutUtils.WrapText(content, font, RealMath.Max(1, (int)RealMath.Round(text.Size.X / fontScale)));
            }

            if (string.IsNullOrEmpty(content)) {
                EndCpuProfileStage(RuntimeCpuProfileStage.CommandListTextLayout, layoutStart);
                return;
            }

            RealEvaluation offsetX = 0d;
            RealEvaluation offsetY = 0d;
            RealEvaluation lineHeight = RealMath.Max((RealEvaluation)font.LineHeight * fontScale, 1d);
            float3 ownerWorldPosition = text.Parent.Position;
            RealEvaluation baseX = RealMath.Round(ownerWorldPosition.X);
            RealEvaluation baseY = RealMath.Round(ownerWorldPosition.Y);
            IReadOnlyList<RealEvaluation> lineOffsets = BuildTextLineOffsets(text, font, content, fontScale);
            EndCpuProfileStage(RuntimeCpuProfileStage.CommandListTextLayout, layoutStart);
            ulong effectsStart = BeginCpuProfileStage();
            bool hasTextEffects = text.ShadowOffset.X != 0f
                || text.ShadowOffset.Y != 0f
                || text.OutlineScale > 0f;
            List<TextRenderEffectPass> effectPasses = hasTextEffects
                ? TextRenderEffectPassBuilder.Build(text)
                : null;
            EndCpuProfileStage(RuntimeCpuProfileStage.CommandListTextEffectsSetup, effectsStart);
            ulong glyphStart = BeginCpuProfileStage();
            int lineIndex = 0;
            RealEvaluation lineOriginX = baseX + TextLineOffsets2D.Resolve(lineOffsets, lineIndex);

            for (int index = 0; index < content.Length; index++) {
                char character = content[index];
                if (character == '\n') {
                    offsetY += lineHeight;
                    offsetX = 0d;
                    lineIndex++;
                    lineOriginX = baseX + TextLineOffsets2D.Resolve(lineOffsets, lineIndex);
                    continue;
                }

                if (character == ' ') {
                    offsetX += font.FontInfo.SpaceWidth * fontScale;
                    continue;
                }

                if (!font.Characters.TryGetValue(character, out FontChar glyph)) {
                    continue;
                }

                RealEvaluation glyphWidth = glyph.SourceRect.Z * font.AtlasWidth * fontScale;
                RealEvaluation glyphHeight = glyph.SourceRect.W * font.AtlasHeight * fontScale;
                RealEvaluation snappedLineOffsetY = RealMath.Round(offsetY);
                if (hasTextEffects) {
                    for (int passIndex = 0; passIndex < effectPasses.Count; passIndex++) {
                        TextRenderEffectPass pass = effectPasses[passIndex];
                        CommandListValue.AddGlyphQuad(
                            font.Texture,
                            new float4(
                                (RealEvaluation)(lineOriginX + offsetX + glyph.BearingX * fontScale + pass.Offset.X),
                                (RealEvaluation)(baseY + snappedLineOffsetY + (glyph.OffsetY * fontScale) + pass.Offset.Y),
                                (RealEvaluation)glyphWidth,
                                (RealEvaluation)glyphHeight),
                            glyph.SourceRect,
                            pass.Color);
                    }
                } else {
                    CommandListValue.AddGlyphQuad(
                        font.Texture,
                        new float4(
                            (RealEvaluation)(lineOriginX + offsetX + glyph.BearingX * fontScale),
                            (RealEvaluation)(baseY + snappedLineOffsetY + (glyph.OffsetY * fontScale)),
                            (RealEvaluation)glyphWidth,
                            (RealEvaluation)glyphHeight),
                        glyph.SourceRect,
                        text.Color);
                }

                RealEvaluation advanceWidth = glyph.AdvanceWidth > 0f
                    ? glyph.AdvanceWidth * fontScale
                    : glyphWidth;
                offsetX += advanceWidth;
            }

            EndCpuProfileStage(RuntimeCpuProfileStage.CommandListGlyphEmission, glyphStart);
            if (effectPasses != null) {
                NativeOwnership.Delete(effectPasses);
            }
        }

        /// <summary>Reuses unchanged glyphs or reruns the original emitter into the same checked range when only the snapped world origin moved.</summary>
        /// <param name="text">Current live text whose clipping has already been resolved.</param>
        void EmitReusableText([NativeNoEscape] ITextDrawable2D text) {
            int slot = TextReuseCache == null ? -1 : TextReuseCache.Find(text);
            if (ReusingTextPayloads && slot >= 0) {
                TextCommandReuseEntry entry = TextReuseCache.Get(slot);
                if (entry.OriginX == RealMath.Round(text.Parent.Position.X) && entry.OriginY == RealMath.Round(text.Parent.Position.Y)) {
                    ulong glyphStart = BeginCpuProfileStage();
                    CommandListValue.AddRetainedGlyphRange(entry.Start, entry.Count);
                    EndCpuProfileStage(RuntimeCpuProfileStage.CommandListGlyphEmission, glyphStart);
                } else {
                    CommandListValue.BeginGlyphOverwrite(entry.Start, entry.Count);
                    EmitText(text);
                    CommandListValue.EndGlyphOverwrite();
                    TextReuseCache.SetRange(slot, entry.Start, entry.Count);
                }
                return;
            }
            int start = CommandListValue.GlyphCount;
            EmitText(text);
            if (CapturingTextPayloads && slot >= 0) {
                TextReuseCache.SetRange(slot, start, CommandListValue.GlyphCount - start);
            }
        }

        /// <summary>
        /// Builds one horizontal offset per rendered text line so authored alignment is honored when flattening glyph quads.
        /// </summary>
        /// <param name="text">Text drawable that owns the authored layout box.</param>
        /// <param name="font">Font used to render the text.</param>
        /// <param name="content">Final rendered text content after wrapping has been applied.</param>
        /// <param name="fontScale">Resolved glyph scale.</param>
        /// <returns>One horizontal offset per rendered line.</returns>
        IReadOnlyList<RealEvaluation> BuildTextLineOffsets(ITextDrawable2D text, FontAsset font, string content, RealEvaluation fontScale) {
            if (text == null) {
                throw new ArgumentNullException(nameof(text));
            } else if (font == null) {
                throw new ArgumentNullException(nameof(font));
            } else if (content == null) {
                throw new ArgumentNullException(nameof(content));
            } else if (fontScale <= 0d) {
                throw new ArgumentOutOfRangeException(nameof(fontScale), "Font scale must be greater than zero.");
            }

            int lineCount = 1;
            for (int index = 0; index < content.Length; index++) {
                if (content[index] == '\n') {
                    lineCount++;
                }
            }

            TextLineOffsets.Clear();
            for (int index = 0; index < lineCount; index++) {
                TextLineOffsets.Add(0d);
            }

            RealEvaluation visibleWidth = 0d;
            RealEvaluation offsetX = 0d;
            RealEvaluation spaceWidth = font.FontInfo != null
                ? RealMath.Max(font.FontInfo.SpaceWidth * fontScale, 1d)
                : RealMath.Max(font.LineHeight * fontScale * 0.25d, 1d);
            int lineIndex = 0;

            for (int index = 0; index < content.Length; index++) {
                char character = content[index];
                if (character == '\n') {
                    TextLineOffsets[lineIndex] = TextLayoutAlignmentUtils.ResolveHorizontalOffset(text.Alignment, text.Size.X, visibleWidth);
                    lineIndex++;
                    visibleWidth = 0d;
                    offsetX = 0d;
                    continue;
                }

                if (character == ' ') {
                    offsetX += spaceWidth;
                    visibleWidth = RealMath.Max(visibleWidth, offsetX);
                    continue;
                }

                if (!font.Characters.TryGetValue(character, out FontChar glyph)) {
                    continue;
                }

                RealEvaluation glyphWidth = RealMath.Max(1d, glyph.SourceRect.Z * font.AtlasWidth * fontScale);
                visibleWidth = RealMath.Max(visibleWidth, offsetX + glyph.BearingX * fontScale + glyphWidth);
                offsetX += glyph.AdvanceWidth > 0f
                    ? glyph.AdvanceWidth * fontScale
                    : glyphWidth;
            }

            TextLineOffsets[lineIndex] = TextLayoutAlignmentUtils.ResolveHorizontalOffset(text.Alignment, text.Size.X, visibleWidth);
            return TextLineOffsets;
        }

        /// <summary>
        /// Emits one rounded-rectangle command for the supplied shape drawable.
        /// </summary>
        /// <param name="roundedRect">Rounded rectangle drawable to flatten.</param>
        void EmitRoundedRect(IRoundedRectDrawable2D roundedRect) {
            float3 position = roundedRect.Parent.Position;
            // Size, like the sprite quad's size, is authored in local units and must be scaled by the
            // parent entity's world scale before it reaches the command list, because platform renderers
            // consume the flattened command stream without access to the source entity hierarchy and can
            // never recover this factor afterward. Position is left unscaled because it already arrives in
            // resolved screen space (see EmitSprite).
            float3 scale = roundedRect.Parent.Scale;
            RealEvaluation width = roundedRect.Size.X * scale.X;
            RealEvaluation height = roundedRect.Size.Y * scale.Y;
            CommandListValue.AddRoundedRect(
                new float4(position.X, position.Y, width, height),
                roundedRect.Radius,
                roundedRect.BorderThickness,
                roundedRect.Corners,
                roundedRect.FillColor,
                roundedRect.BorderColor);
        }
    }
}
