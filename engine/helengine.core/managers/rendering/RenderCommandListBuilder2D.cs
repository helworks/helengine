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
        /// Tracks whether the native reusable builder state was already released.
        /// </summary>
        bool IsDisposedValue;

        /// <summary>
        /// Initializes one reusable 2D command builder.
        /// </summary>
        public RenderCommandListBuilder2D() {
            ClipRegionStackBuilder = new ClipRegionStackBuilder2D();
            ActiveClipChain = new List<IClipRegion2D>();
            NextClipChain = new List<IClipRegion2D>();
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

            if (CommandListValue == null) {
                CommandListValue = new RenderCommandList2D(Math.Max(renderQueue.Count, 4));
            } else {
                CommandListValue.Reset();
            }

            ActiveClipChain.Clear();
            NextClipChain.Clear();
            renderQueue.VisitOrdered(this);
            EmitTrailingClipPops();
            return CommandListValue;
        }

        /// <summary>
        /// Releases the reusable command-list state and native clip-chain lists owned by this builder.
        /// </summary>
        public void Dispose() {
            if (IsDisposedValue) {
                return;
            }

            ActiveClipChain.Clear();
            NextClipChain.Clear();
            NativeOwnership.DisposeAndDelete(CommandListValue);
            NativeOwnership.Delete(ClipRegionStackBuilder);
            NativeOwnership.Delete(ActiveClipChain);
            NativeOwnership.Delete(NextClipChain);
            CommandListValue = null;
            IsDisposedValue = true;
        }

        /// <summary>
        /// Visits one 2D drawable and emits the matching resolved command payload when the drawable is renderable.
        /// </summary>
        /// <param name="drawable">Drawable visited from the ordered render queue.</param>
        public void Visit(IDrawable2D drawable) {
            if (drawable == null || drawable.Parent == null || !drawable.Parent.Enabled) {
                return;
            }

            ClipRegionStackBuilder.BuildClipChain(drawable, NextClipChain);
            if (ShouldSkipDrawableBecauseItIsFullyClipped(drawable)) {
                return;
            }

            SyncClipTransitions();

            if (drawable is ISpriteDrawable2D sprite) {
                EmitSprite(sprite);
                return;
            }

            if (drawable is ITextDrawable2D text) {
                EmitText(text);
                return;
            }

            if (drawable is IRoundedRectDrawable2D roundedRect) {
                EmitRoundedRect(roundedRect);
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
            int maxSharedLength = Math.Min(ActiveClipChain.Count, NextClipChain.Count);
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
                float width = size.X > 0 ? size.X : sprite.Texture.Width;
                float height = size.Y > 0 ? size.Y : sprite.Texture.Height;
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

            float firstRight = first.X + first.Z;
            float firstBottom = first.Y + first.W;
            float secondRight = second.X + second.Z;
            float secondBottom = second.Y + second.W;
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

            int2 size = sprite.Size;
            float3 scale = sprite.Parent.Scale;
            float width = (size.X > 0 ? size.X : sprite.Texture.Width) * scale.X;
            float height = (size.Y > 0 ? size.Y : sprite.Texture.Height) * scale.Y;
            float3 position = sprite.Parent.Position;
            float3 rotatedRight = float4.RotateVector(float3.UnitX, sprite.Parent.Orientation);
            float rotationRadians = (float)Math.Atan2(rotatedRight.Y, rotatedRight.X);
            CommandListValue.AddTexturedQuad(
                sprite.Texture,
                new float4(position.X, position.Y, width, height),
                sprite.SourceRect,
                sprite.Color,
                rotationRadians);
        }

        /// <summary>
        /// Emits one glyph-quad command per rendered glyph for the supplied text drawable.
        /// </summary>
        /// <param name="text">Text drawable to flatten.</param>
        void EmitText(ITextDrawable2D text) {
            FontAsset font = text.Font;
            string content = text.Text ?? string.Empty;
            double fontScale = Math.Max((double)text.FontScale, 0.0001d);
            if (text.WrapText) {
                content = TextLayoutUtils.WrapText(content, font, Math.Max(1, (int)Math.Round(text.Size.X / fontScale)));
            }

            double offsetX = 0d;
            double offsetY = 0d;
            double lineHeight = Math.Max((double)font.LineHeight * fontScale, 1d);
            double baseX = Math.Round(text.Parent.Position.X);
            double baseY = Math.Round(text.Parent.Position.Y);

            for (int index = 0; index < content.Length; index++) {
                char character = content[index];
                if (character == '\n') {
                    offsetY += lineHeight;
                    offsetX = 0d;
                    continue;
                }

                if (character == ' ') {
                    offsetX += font.FontInfo.SpaceWidth * fontScale;
                    continue;
                }

                if (!font.Characters.TryGetValue(character, out FontChar glyph)) {
                    continue;
                }

                double glyphWidth = glyph.SourceRect.Z * font.AtlasWidth * fontScale;
                double glyphHeight = glyph.SourceRect.W * font.AtlasHeight * fontScale;
                double snappedLineOffsetY = Math.Round(offsetY);
                CommandListValue.AddGlyphQuad(
                    font.Texture,
                    new float4(
                        (float)(baseX + offsetX),
                        (float)(baseY + snappedLineOffsetY + (glyph.OffsetY * fontScale)),
                        (float)glyphWidth,
                        (float)glyphHeight),
                    glyph.SourceRect,
                    text.Color);

                double advanceWidth = glyph.AdvanceWidth > 0f
                    ? glyph.AdvanceWidth * fontScale
                    : glyphWidth;
                offsetX += advanceWidth;
            }
        }

        /// <summary>
        /// Emits one rounded-rectangle command for the supplied shape drawable.
        /// </summary>
        /// <param name="roundedRect">Rounded rectangle drawable to flatten.</param>
        void EmitRoundedRect(IRoundedRectDrawable2D roundedRect) {
            float3 position = roundedRect.Parent.Position;
            CommandListValue.AddRoundedRect(
                new float4(position.X, position.Y, roundedRect.Size.X, roundedRect.Size.Y),
                roundedRect.Radius,
                roundedRect.BorderThickness,
                roundedRect.Corners,
                roundedRect.FillColor,
                roundedRect.BorderColor);
        }
    }
}
