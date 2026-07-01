namespace helengine {
    /// <summary>
    /// Stores one flat, reusable stream of resolved 2D commands for one camera frame.
    /// </summary>
    public sealed class RenderCommandList2D : IDisposable {
        /// <summary>
        /// Logical command kinds in insertion order.
        /// </summary>
        readonly List<RenderCommand2DType> CommandTypes;

        /// <summary>
        /// Payload indexes aligned with <see cref="CommandTypes"/>.
        /// </summary>
        readonly List<int> PayloadIndices;

        /// <summary>
        /// Rectangular payloads used by clip-push commands.
        /// </summary>
        readonly List<float4> ClipRects;

        /// <summary>
        /// Runtime textures used by textured-quad commands.
        /// </summary>
        readonly List<RuntimeTexture> QuadTextures;

        /// <summary>
        /// Destination bounds used by textured-quad commands.
        /// </summary>
        readonly List<float4> QuadBounds;

        /// <summary>
        /// Source rectangles used by textured-quad commands.
        /// </summary>
        readonly List<float4> QuadSourceRects;

        /// <summary>
        /// Tint colors used by textured-quad commands.
        /// </summary>
        readonly List<byte4> QuadColors;

        /// <summary>
        /// Clockwise rotation angles, in radians, used by textured-quad commands.
        /// </summary>
        readonly List<float> QuadRotations;

        /// <summary>
        /// Runtime textures used by glyph-quad commands.
        /// </summary>
        readonly List<RuntimeTexture> GlyphTextures;

        /// <summary>
        /// Destination bounds used by glyph-quad commands.
        /// </summary>
        readonly List<float4> GlyphBounds;

        /// <summary>
        /// Source rectangles used by glyph-quad commands.
        /// </summary>
        readonly List<float4> GlyphSourceRects;

        /// <summary>
        /// Tint colors used by glyph-quad commands.
        /// </summary>
        readonly List<byte4> GlyphColors;

        /// <summary>
        /// Bounds used by rounded-rectangle commands.
        /// </summary>
        readonly List<float4> RoundedRectBounds;

        /// <summary>
        /// Corner radii used by rounded-rectangle commands.
        /// </summary>
        readonly List<float> RoundedRectRadii;

        /// <summary>
        /// Border thicknesses used by rounded-rectangle commands.
        /// </summary>
        readonly List<float> RoundedRectBorderThicknesses;

        /// <summary>
        /// Corner masks used by rounded-rectangle commands.
        /// </summary>
        readonly List<RoundedRectCorners> RoundedRectCornersValues;

        /// <summary>
        /// Fill colors used by rounded-rectangle commands.
        /// </summary>
        readonly List<byte4> RoundedRectFillColors;

        /// <summary>
        /// Border colors used by rounded-rectangle commands.
        /// </summary>
        readonly List<byte4> RoundedRectBorderColors;

        /// <summary>
        /// Tracks whether the native backing lists owned by this reusable command container were already released.
        /// </summary>
        bool IsDisposedValue;

        /// <summary>
        /// Initializes a reusable command list with the supplied initial capacity.
        /// </summary>
        /// <param name="initialCapacity">Initial logical command capacity.</param>
        public RenderCommandList2D(int initialCapacity) {
            if (initialCapacity < 0) {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            }

            CommandTypes = new List<RenderCommand2DType>(initialCapacity);
            PayloadIndices = new List<int>(initialCapacity);
            ClipRects = new List<float4>(initialCapacity);
            QuadTextures = new List<RuntimeTexture>(initialCapacity);
            QuadBounds = new List<float4>(initialCapacity);
            QuadSourceRects = new List<float4>(initialCapacity);
            QuadColors = new List<byte4>(initialCapacity);
            QuadRotations = new List<float>(initialCapacity);
            GlyphTextures = new List<RuntimeTexture>(initialCapacity);
            GlyphBounds = new List<float4>(initialCapacity);
            GlyphSourceRects = new List<float4>(initialCapacity);
            GlyphColors = new List<byte4>(initialCapacity);
            RoundedRectBounds = new List<float4>(initialCapacity);
            RoundedRectRadii = new List<float>(initialCapacity);
            RoundedRectBorderThicknesses = new List<float>(initialCapacity);
            RoundedRectCornersValues = new List<RoundedRectCorners>(initialCapacity);
            RoundedRectFillColors = new List<byte4>(initialCapacity);
            RoundedRectBorderColors = new List<byte4>(initialCapacity);
        }

        /// <summary>
        /// Gets the number of logical commands stored in the list.
        /// </summary>
        public int Count {
            get { return CommandTypes.Count; }
        }

        /// <summary>
        /// Releases the native backing lists owned by this reusable command container.
        /// </summary>
        public void Dispose() {
            if (IsDisposedValue) {
                return;
            }

            Reset();
            NativeOwnership.Delete(CommandTypes);
            NativeOwnership.Delete(PayloadIndices);
            NativeOwnership.Delete(ClipRects);
            NativeOwnership.Delete(QuadTextures);
            NativeOwnership.Delete(QuadBounds);
            NativeOwnership.Delete(QuadSourceRects);
            NativeOwnership.Delete(QuadColors);
            NativeOwnership.Delete(QuadRotations);
            NativeOwnership.Delete(GlyphTextures);
            NativeOwnership.Delete(GlyphBounds);
            NativeOwnership.Delete(GlyphSourceRects);
            NativeOwnership.Delete(GlyphColors);
            NativeOwnership.Delete(RoundedRectBounds);
            NativeOwnership.Delete(RoundedRectRadii);
            NativeOwnership.Delete(RoundedRectBorderThicknesses);
            NativeOwnership.Delete(RoundedRectCornersValues);
            NativeOwnership.Delete(RoundedRectFillColors);
            NativeOwnership.Delete(RoundedRectBorderColors);
            IsDisposedValue = true;
        }

        /// <summary>
        /// Clears all logical commands and payload lists so the container can be reused for another frame.
        /// </summary>
        public void Reset() {
            CommandTypes.Clear();
            PayloadIndices.Clear();
            ClipRects.Clear();
            QuadTextures.Clear();
            QuadBounds.Clear();
            QuadSourceRects.Clear();
            QuadColors.Clear();
            QuadRotations.Clear();
            GlyphTextures.Clear();
            GlyphBounds.Clear();
            GlyphSourceRects.Clear();
            GlyphColors.Clear();
            RoundedRectBounds.Clear();
            RoundedRectRadii.Clear();
            RoundedRectBorderThicknesses.Clear();
            RoundedRectCornersValues.Clear();
            RoundedRectFillColors.Clear();
            RoundedRectBorderColors.Clear();
        }

        /// <summary>
        /// Adds one clip-push command to the list.
        /// </summary>
        /// <param name="clipRect">Resolved clip rectangle in pixels.</param>
        public void AddClipPush(float4 clipRect) {
            int payloadIndex = ClipRects.Count;
            ClipRects.Add(clipRect);
            CommandTypes.Add(RenderCommand2DType.ClipPush);
            PayloadIndices.Add(payloadIndex);
        }

        /// <summary>
        /// Adds one clip-pop command to the list.
        /// </summary>
        public void AddClipPop() {
            CommandTypes.Add(RenderCommand2DType.ClipPop);
            PayloadIndices.Add(-1);
        }

        /// <summary>
        /// Adds one textured-quad command to the list.
        /// </summary>
        /// <param name="texture">Runtime texture sampled by the quad.</param>
        /// <param name="bounds">Resolved destination bounds in pixels.</param>
        /// <param name="sourceRect">Resolved texture source rectangle.</param>
        /// <param name="color">Resolved tint color.</param>
        /// <param name="rotationRadians">Clockwise rotation angle, in radians, around the quad center.</param>
        public void AddTexturedQuad(RuntimeTexture texture, float4 bounds, float4 sourceRect, byte4 color, float rotationRadians) {
            if (texture == null) {
                throw new ArgumentNullException(nameof(texture));
            }

            int payloadIndex = QuadTextures.Count;
            QuadTextures.Add(texture);
            QuadBounds.Add(bounds);
            QuadSourceRects.Add(sourceRect);
            QuadColors.Add(color);
            QuadRotations.Add(rotationRadians);
            CommandTypes.Add(RenderCommand2DType.TexturedQuad);
            PayloadIndices.Add(payloadIndex);
        }

        /// <summary>
        /// Adds one glyph-quad command to the list.
        /// </summary>
        /// <param name="texture">Runtime font atlas texture sampled by the glyph.</param>
        /// <param name="bounds">Resolved glyph bounds in pixels.</param>
        /// <param name="sourceRect">Resolved atlas source rectangle.</param>
        /// <param name="color">Resolved tint color.</param>
        public void AddGlyphQuad(RuntimeTexture texture, float4 bounds, float4 sourceRect, byte4 color) {
            if (texture == null) {
                throw new ArgumentNullException(nameof(texture));
            }

            int payloadIndex = GlyphTextures.Count;
            GlyphTextures.Add(texture);
            GlyphBounds.Add(bounds);
            GlyphSourceRects.Add(sourceRect);
            GlyphColors.Add(color);
            CommandTypes.Add(RenderCommand2DType.GlyphQuad);
            PayloadIndices.Add(payloadIndex);
        }

        /// <summary>
        /// Adds one rounded-rectangle command to the list.
        /// </summary>
        /// <param name="bounds">Resolved destination bounds in pixels.</param>
        /// <param name="radius">Resolved corner radius.</param>
        /// <param name="borderThickness">Resolved border thickness.</param>
        /// <param name="corners">Resolved rounded-corner mask.</param>
        /// <param name="fillColor">Resolved fill color.</param>
        /// <param name="borderColor">Resolved border color.</param>
        public void AddRoundedRect(
            float4 bounds,
            float radius,
            float borderThickness,
            RoundedRectCorners corners,
            byte4 fillColor,
            byte4 borderColor) {
            int payloadIndex = RoundedRectBounds.Count;
            RoundedRectBounds.Add(bounds);
            RoundedRectRadii.Add(radius);
            RoundedRectBorderThicknesses.Add(borderThickness);
            RoundedRectCornersValues.Add(corners);
            RoundedRectFillColors.Add(fillColor);
            RoundedRectBorderColors.Add(borderColor);
            CommandTypes.Add(RenderCommand2DType.RoundedRect);
            PayloadIndices.Add(payloadIndex);
        }

        /// <summary>
        /// Gets the command type at one logical command index.
        /// </summary>
        /// <param name="commandIndex">Zero-based logical command index.</param>
        /// <returns>Stored command kind.</returns>
        public RenderCommand2DType GetCommandType(int commandIndex) {
            return CommandTypes[commandIndex];
        }

        /// <summary>
        /// Gets the clip-push payload index stored at one logical command index.
        /// </summary>
        /// <param name="commandIndex">Zero-based logical command index.</param>
        /// <returns>Zero-based clip-push payload index.</returns>
        public int GetClipPushPayloadIndex(int commandIndex) {
            return PayloadIndices[commandIndex];
        }

        /// <summary>
        /// Gets the textured-quad payload index stored at one logical command index.
        /// </summary>
        /// <param name="commandIndex">Zero-based logical command index.</param>
        /// <returns>Zero-based textured-quad payload index.</returns>
        public int GetTexturedQuadPayloadIndex(int commandIndex) {
            return PayloadIndices[commandIndex];
        }

        /// <summary>
        /// Gets the glyph-quad payload index stored at one logical command index.
        /// </summary>
        /// <param name="commandIndex">Zero-based logical command index.</param>
        /// <returns>Zero-based glyph-quad payload index.</returns>
        public int GetGlyphQuadPayloadIndex(int commandIndex) {
            return PayloadIndices[commandIndex];
        }

        /// <summary>
        /// Gets the rounded-rectangle payload index stored at one logical command index.
        /// </summary>
        /// <param name="commandIndex">Zero-based logical command index.</param>
        /// <returns>Zero-based rounded-rectangle payload index.</returns>
        public int GetRoundedRectPayloadIndex(int commandIndex) {
            return PayloadIndices[commandIndex];
        }

        /// <summary>
        /// Gets the clip rectangle stored for one clip-push payload.
        /// </summary>
        /// <param name="payloadIndex">Zero-based clip-push payload index.</param>
        /// <returns>Stored clip rectangle.</returns>
        public float4 GetClipPushRect(int payloadIndex) {
            return ClipRects[payloadIndex];
        }

        /// <summary>
        /// Gets the runtime texture stored for one textured-quad payload.
        /// </summary>
        /// <param name="payloadIndex">Zero-based textured-quad payload index.</param>
        /// <returns>Stored runtime texture.</returns>
        public RuntimeTexture GetTexturedQuadTexture(int payloadIndex) {
            return QuadTextures[payloadIndex];
        }

        /// <summary>
        /// Gets the destination bounds stored for one textured-quad payload.
        /// </summary>
        /// <param name="payloadIndex">Zero-based textured-quad payload index.</param>
        /// <returns>Stored destination bounds.</returns>
        public float4 GetTexturedQuadBounds(int payloadIndex) {
            return QuadBounds[payloadIndex];
        }

        /// <summary>
        /// Gets the source rectangle stored for one textured-quad payload.
        /// </summary>
        /// <param name="payloadIndex">Zero-based textured-quad payload index.</param>
        /// <returns>Stored source rectangle.</returns>
        public float4 GetTexturedQuadSourceRect(int payloadIndex) {
            return QuadSourceRects[payloadIndex];
        }

        /// <summary>
        /// Gets the tint color stored for one textured-quad payload.
        /// </summary>
        /// <param name="payloadIndex">Zero-based textured-quad payload index.</param>
        /// <returns>Stored tint color.</returns>
        public byte4 GetTexturedQuadColor(int payloadIndex) {
            return QuadColors[payloadIndex];
        }

        /// <summary>
        /// Gets the clockwise rotation angle, in radians, stored for one textured-quad payload.
        /// </summary>
        /// <param name="payloadIndex">Zero-based textured-quad payload index.</param>
        /// <returns>Stored clockwise rotation angle around the quad center.</returns>
        public float GetTexturedQuadRotation(int payloadIndex) {
            return QuadRotations[payloadIndex];
        }

        /// <summary>
        /// Gets the runtime texture stored for one glyph-quad payload.
        /// </summary>
        /// <param name="payloadIndex">Zero-based glyph-quad payload index.</param>
        /// <returns>Stored runtime texture.</returns>
        public RuntimeTexture GetGlyphQuadTexture(int payloadIndex) {
            return GlyphTextures[payloadIndex];
        }

        /// <summary>
        /// Gets the destination bounds stored for one glyph-quad payload.
        /// </summary>
        /// <param name="payloadIndex">Zero-based glyph-quad payload index.</param>
        /// <returns>Stored destination bounds.</returns>
        public float4 GetGlyphQuadBounds(int payloadIndex) {
            return GlyphBounds[payloadIndex];
        }

        /// <summary>
        /// Gets the source rectangle stored for one glyph-quad payload.
        /// </summary>
        /// <param name="payloadIndex">Zero-based glyph-quad payload index.</param>
        /// <returns>Stored source rectangle.</returns>
        public float4 GetGlyphQuadSourceRect(int payloadIndex) {
            return GlyphSourceRects[payloadIndex];
        }

        /// <summary>
        /// Gets the tint color stored for one glyph-quad payload.
        /// </summary>
        /// <param name="payloadIndex">Zero-based glyph-quad payload index.</param>
        /// <returns>Stored tint color.</returns>
        public byte4 GetGlyphQuadColor(int payloadIndex) {
            return GlyphColors[payloadIndex];
        }

        /// <summary>
        /// Gets the bounds stored for one rounded-rectangle payload.
        /// </summary>
        /// <param name="payloadIndex">Zero-based rounded-rectangle payload index.</param>
        /// <returns>Stored destination bounds.</returns>
        public float4 GetRoundedRectBounds(int payloadIndex) {
            return RoundedRectBounds[payloadIndex];
        }

        /// <summary>
        /// Gets the corner radius stored for one rounded-rectangle payload.
        /// </summary>
        /// <param name="payloadIndex">Zero-based rounded-rectangle payload index.</param>
        /// <returns>Stored corner radius.</returns>
        public float GetRoundedRectRadius(int payloadIndex) {
            return RoundedRectRadii[payloadIndex];
        }

        /// <summary>
        /// Gets the border thickness stored for one rounded-rectangle payload.
        /// </summary>
        /// <param name="payloadIndex">Zero-based rounded-rectangle payload index.</param>
        /// <returns>Stored border thickness.</returns>
        public float GetRoundedRectBorderThickness(int payloadIndex) {
            return RoundedRectBorderThicknesses[payloadIndex];
        }

        /// <summary>
        /// Gets the corner mask stored for one rounded-rectangle payload.
        /// </summary>
        /// <param name="payloadIndex">Zero-based rounded-rectangle payload index.</param>
        /// <returns>Stored rounded-corner mask.</returns>
        public RoundedRectCorners GetRoundedRectCorners(int payloadIndex) {
            return RoundedRectCornersValues[payloadIndex];
        }

        /// <summary>
        /// Gets the fill color stored for one rounded-rectangle payload.
        /// </summary>
        /// <param name="payloadIndex">Zero-based rounded-rectangle payload index.</param>
        /// <returns>Stored fill color.</returns>
        public byte4 GetRoundedRectFillColor(int payloadIndex) {
            return RoundedRectFillColors[payloadIndex];
        }

        /// <summary>
        /// Gets the border color stored for one rounded-rectangle payload.
        /// </summary>
        /// <param name="payloadIndex">Zero-based rounded-rectangle payload index.</param>
        /// <returns>Stored border color.</returns>
        public byte4 GetRoundedRectBorderColor(int payloadIndex) {
            return RoundedRectBorderColors[payloadIndex];
        }
    }
}
