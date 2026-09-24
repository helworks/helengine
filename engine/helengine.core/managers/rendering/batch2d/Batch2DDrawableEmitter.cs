namespace helengine {
    /// <summary>
    /// Converts sprite, text, and rounded-rectangle drawable snapshots into shared batch-writer quads.
    /// </summary>
    public sealed class Batch2DDrawableEmitter {
        /// <summary>Writer receiving value copies of all expanded drawable vertices.</summary>
        readonly Batch2DWriter Writer;
        /// <summary>Borrowed renderer retained as the owner for a future renderer-local white texel path for plain solid rectangles; RoundedShape itself does not use a texture.</summary>
        readonly RenderManager2D RenderManager;

        /// <summary>
        /// Creates a drawable emitter bound to one writer and its owning render manager.
        /// </summary>
        /// <param name="writer">Writer that copies emitted vertices into bounded chunks.</param>
        /// <param name="renderManager">Owning renderer retained for renderer-local plain-solid texture paths; RoundedShape does not acquire its white pixel.</param>
        public Batch2DDrawableEmitter([NativeRetainsBorrow] Batch2DWriter writer,
            [NativeRetainsBorrow] RenderManager2D renderManager) {
            Writer = writer ?? throw new ArgumentNullException(nameof(writer));
            RenderManager = renderManager ?? throw new ArgumentNullException(nameof(renderManager));
        }

        /// <summary>
        /// Captures a sprite's texture, tint, source rectangle, destination, and world transform as one quad.
        /// </summary>
        /// <param name="drawable">Sprite drawable whose state is read synchronously.</param>
        /// <param name="run">Caller-resolved scissor snapshot for this draw.</param>
        public void EmitSprite(ISpriteDrawable2D drawable, Batch2DRun run) {
            if (drawable == null || drawable.Parent == null || !drawable.Parent.Enabled || drawable.Texture == null) {
                return;
            }

            RuntimeTexture texture = drawable.Texture;
            if (texture.IsDisposed) {
#if HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION
                throw new InvalidOperationException("A disposed sprite texture cannot be emitted.");
#else
                throw new ObjectDisposedException(nameof(drawable.Texture), "A disposed sprite texture cannot be emitted.");
#endif
            }

            int2 size = drawable.Size;
            if (size.X <= 0 || size.Y <= 0) {
                size = new int2(texture.Width, texture.Height);
            }
            if (size.X <= 0 || size.Y <= 0) {
                return;
            }

            Entity parent = drawable.Parent;
            float3 position = parent.Position;
            float3 scale = parent.Scale;
            float3 rotatedRight = float4.RotateVector(float3.UnitX, parent.Orientation);
            double rotation = Math.Atan2(rotatedRight.Y, rotatedRight.X);
            double width = (double)size.X * scale.X;
            double height = (double)size.Y * scale.Y;
            float4 color = Batch2DGeometry.NormalizeColor(drawable.Color);
            Batch2DGeometry.CreateQuad(position.X, position.Y, width, height, 0d, rotation,
                drawable.SourceRect, color, out Batch2DVertex topLeft, out Batch2DVertex topRight,
                out Batch2DVertex bottomRight, out Batch2DVertex bottomLeft);

            Batch2DRun spriteRun = new Batch2DRun(Batch2DVariant.Textured, texture,
                run.ScissorX, run.ScissorY, run.ScissorWidth, run.ScissorHeight);
            Writer.AppendQuad(spriteRun, topLeft, topRight, bottomRight, bottomLeft);
        }

        /// <summary>
        /// Expands font glyphs with shared layout helpers and the current text drawable state.
        /// </summary>
        /// <param name="drawable">Text drawable whose current state is copied into glyph vertices.</param>
        /// <param name="run">Caller-resolved scissor snapshot for this draw.</param>
        public void EmitText(ITextDrawable2D drawable, Batch2DRun run) {
            Batch2DTextEmitter.EmitText(Writer, drawable, run);
        }

        /// <summary>
        /// Captures rounded-shape fill, border, corner mask, dimensions, and rotation into one shape quad.
        /// </summary>
        /// <param name="drawable">Rounded rectangle drawable whose state is read synchronously.</param>
        /// <param name="run">Caller-resolved scissor snapshot for this draw.</param>
        public void EmitRoundedRect(IRoundedRectDrawable2D drawable, Batch2DRun run) {
            if (drawable == null || drawable.Parent == null || !drawable.Parent.Enabled ||
                drawable.Size.X <= 0 || drawable.Size.Y <= 0) {
                return;
            }

            Entity parent = drawable.Parent;
            float3 position = parent.Position;
            float4 corners = new float4(
                IsCornerEnabled(drawable.Corners, RoundedRectCorners.TopLeft) ? 1f : 0f,
                IsCornerEnabled(drawable.Corners, RoundedRectCorners.TopRight) ? 1f : 0f,
                IsCornerEnabled(drawable.Corners, RoundedRectCorners.BottomLeft) ? 1f : 0f,
                IsCornerEnabled(drawable.Corners, RoundedRectCorners.BottomRight) ? 1f : 0f);
            float4 fillColor = Batch2DGeometry.NormalizeColor(drawable.FillColor);
            float4 borderColor = Batch2DGeometry.NormalizeColor(drawable.BorderColor);
            Batch2DGeometry.CreateRoundedQuad(position.X, position.Y, drawable.Size.X, drawable.Size.Y,
                0d, drawable.Rotation, drawable.Radius, drawable.BorderThickness, corners,
                fillColor, borderColor, out Batch2DVertex topLeft, out Batch2DVertex topRight,
                out Batch2DVertex bottomRight, out Batch2DVertex bottomLeft);

            Batch2DRun shapeRun = new Batch2DRun(Batch2DVariant.RoundedShape, null,
                run.ScissorX, run.ScissorY, run.ScissorWidth, run.ScissorHeight);
            Writer.AppendQuad(shapeRun, topLeft, topRight, bottomRight, bottomLeft);
        }

        /// <summary>
        /// Tests one declared corner flag without interpreting unrelated enum bits as enabled corners.
        /// </summary>
        /// <param name="corners">Corner flags stored on the drawable.</param>
        /// <param name="corner">One named corner bit.</param>
        /// <returns>True when that specific corner bit is present.</returns>
        static bool IsCornerEnabled(RoundedRectCorners corners, RoundedRectCorners corner) {
            return (((int)corners) & ((int)corner)) == (int)corner;
        }
    }
}
