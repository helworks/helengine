namespace helengine {
    /// <summary>
    /// Walks the current 2D render queue and emits one reusable flat command list for backend consumption.
    /// </summary>
    public sealed class RenderCommandListBuilder2D : IRenderVisitor2D {
        /// <summary>
        /// Reusable command list populated during the current build pass.
        /// </summary>
        RenderCommandList2D CommandListValue;

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

            renderQueue.VisitOrdered(this);
            return CommandListValue;
        }

        /// <summary>
        /// Visits one 2D drawable and emits the matching resolved command payload when the drawable is renderable.
        /// </summary>
        /// <param name="drawable">Drawable visited from the ordered render queue.</param>
        public void Visit(IDrawable2D drawable) {
            if (drawable == null || drawable.Parent == null || !drawable.Parent.Enabled) {
                return;
            }

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
        /// Emits one textured-quad command for the supplied sprite.
        /// </summary>
        /// <param name="sprite">Sprite drawable to flatten.</param>
        void EmitSprite(ISpriteDrawable2D sprite) {
            if (sprite.Texture == null) {
                return;
            }

            int2 size = sprite.Size;
            float width = size.X > 0 ? size.X : sprite.Texture.Width;
            float height = size.Y > 0 ? size.Y : sprite.Texture.Height;
            float3 position = sprite.Parent.Position;
            CommandListValue.AddTexturedQuad(
                sprite.Texture,
                new float4(position.X, position.Y, width, height),
                sprite.SourceRect,
                sprite.Color);
        }

        /// <summary>
        /// Emits one glyph-quad command per rendered glyph for the supplied text drawable.
        /// </summary>
        /// <param name="text">Text drawable to flatten.</param>
        void EmitText(ITextDrawable2D text) {
            FontAsset font = text.Font;
            string content = text.Text ?? string.Empty;
            if (text.WrapText) {
                content = TextLayoutUtils.WrapText(content, font, text.Size.X);
            }

            double offsetX = 0d;
            double offsetY = 0d;
            double lineHeight = Math.Max((double)font.LineHeight, 1d);
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
                    offsetX += font.FontInfo.SpaceWidth;
                    continue;
                }

                if (!font.Characters.TryGetValue(character, out FontChar glyph)) {
                    continue;
                }

                double glyphWidth = glyph.SourceRect.Z * font.AtlasWidth;
                double glyphHeight = glyph.SourceRect.W * font.AtlasHeight;
                double snappedLineOffsetY = Math.Round(offsetY);
                CommandListValue.AddGlyphQuad(
                    font.Texture,
                    new float4(
                        (float)(baseX + offsetX),
                        (float)(baseY + snappedLineOffsetY + glyph.OffsetY),
                        (float)glyphWidth,
                        (float)glyphHeight),
                    glyph.SourceRect,
                    text.Color);

                double advanceWidth = glyph.AdvanceWidth > 0f ? glyph.AdvanceWidth : glyphWidth;
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
