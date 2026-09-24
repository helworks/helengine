namespace helengine {
    /// <summary>
    /// Builds the four value vertices used by textured drawable and rounded-shape batch quads.
    /// </summary>
    public static class Batch2DGeometry {
        /// <summary>
        /// Creates a textured quad in top-left, top-right, bottom-right, bottom-left order without allocating per vertex.
        /// </summary>
        /// <param name="x">Unrotated destination left edge.</param>
        /// <param name="y">Unrotated destination top edge in Y-down screen coordinates.</param>
        /// <param name="width">Destination width.</param>
        /// <param name="height">Destination height.</param>
        /// <param name="depth">Pixel-space depth copied to every vertex.</param>
        /// <param name="rotation">DirectX-reference angle applied using the existing Y-up shader sign in Y-down screen coordinates.</param>
        /// <param name="sourceRect">Normalized source rectangle ordered U, V, width, height.</param>
        /// <param name="color">Straight-alpha tint in normalized channel values.</param>
        /// <param name="topLeft">Receives the first quad vertex.</param>
        /// <param name="topRight">Receives the second quad vertex.</param>
        /// <param name="bottomRight">Receives the third quad vertex.</param>
        /// <param name="bottomLeft">Receives the fourth quad vertex.</param>
        public static void CreateQuad(double x, double y, double width, double height, double depth, double rotation,
            float4 sourceRect, float4 color, out Batch2DVertex topLeft, out Batch2DVertex topRight,
            out Batch2DVertex bottomRight, out Batch2DVertex bottomLeft) {
            double halfWidth = width * 0.5d;
            double halfHeight = height * 0.5d;
            double centerX = x + halfWidth;
            double centerY = y + halfHeight;
            double cosine = Math.Cos(rotation);
            double sine = Math.Sin(rotation);
            float rightU = sourceRect.X + sourceRect.Z;
            float bottomV = sourceRect.Y + sourceRect.W;

            topLeft = CreateTexturedVertex(centerX, centerY, -halfWidth, -halfHeight, depth, cosine, sine,
                sourceRect.X, sourceRect.Y, color);
            topRight = CreateTexturedVertex(centerX, centerY, halfWidth, -halfHeight, depth, cosine, sine,
                rightU, sourceRect.Y, color);
            bottomRight = CreateTexturedVertex(centerX, centerY, halfWidth, halfHeight, depth, cosine, sine,
                rightU, bottomV, color);
            bottomLeft = CreateTexturedVertex(centerX, centerY, -halfWidth, halfHeight, depth, cosine, sine,
                sourceRect.X, bottomV, color);
        }

        /// <summary>
        /// Creates a rounded-shape quad and stores its unrotated local pixel coordinates on each vertex.
        /// </summary>
        /// <param name="x">Unrotated destination left edge.</param>
        /// <param name="y">Unrotated destination top edge.</param>
        /// <param name="width">Positive destination width.</param>
        /// <param name="height">Positive destination height.</param>
        /// <param name="depth">Pixel-space depth copied to every vertex.</param>
        /// <param name="rotation">DirectX-reference angle applied using the existing Y-up shader sign in Y-down screen coordinates.</param>
        /// <param name="radius">Corner radius in pixels.</param>
        /// <param name="borderWidth">Border thickness in pixels.</param>
        /// <param name="corners">Corner-enable values ordered top-left, top-right, bottom-left, bottom-right.</param>
        /// <param name="fillColor">Straight-alpha normalized fill color.</param>
        /// <param name="borderColor">Straight-alpha normalized border color.</param>
        /// <param name="topLeft">Receives the first quad vertex.</param>
        /// <param name="topRight">Receives the second quad vertex.</param>
        /// <param name="bottomRight">Receives the third quad vertex.</param>
        /// <param name="bottomLeft">Receives the fourth quad vertex.</param>
        public static void CreateRoundedQuad(double x, double y, double width, double height, double depth,
            double rotation, double radius, double borderWidth, float4 corners, float4 fillColor,
            float4 borderColor, out Batch2DVertex topLeft, out Batch2DVertex topRight,
            out Batch2DVertex bottomRight, out Batch2DVertex bottomLeft) {
            if (!IsFinite(radius)) {
                throw new ArgumentException("Rounded-shape radius must be finite.", nameof(radius));
            }
            if (!IsFinite(borderWidth)) {
                throw new ArgumentException("Rounded-shape border width must be finite.", nameof(borderWidth));
            }

            double halfWidth = width * 0.5d;
            double halfHeight = height * 0.5d;
            double centerX = x + halfWidth;
            double centerY = y + halfHeight;
            double cosine = Math.Cos(rotation);
            double sine = Math.Sin(rotation);
            float4 shape = new float4((float)halfWidth, (float)halfHeight,
                (float)Math.Min(Math.Max(radius, 0d), Math.Min(halfWidth, halfHeight)),
                (float)Math.Min(Math.Max(borderWidth, 0d), Math.Min(halfWidth, halfHeight)));

            topLeft = CreateShapeVertex(centerX, centerY, -halfWidth, -halfHeight, depth, cosine, sine,
                shape, corners, fillColor, borderColor);
            topRight = CreateShapeVertex(centerX, centerY, halfWidth, -halfHeight, depth, cosine, sine,
                shape, corners, fillColor, borderColor);
            bottomRight = CreateShapeVertex(centerX, centerY, halfWidth, halfHeight, depth, cosine, sine,
                shape, corners, fillColor, borderColor);
            bottomLeft = CreateShapeVertex(centerX, centerY, -halfWidth, halfHeight, depth, cosine, sine,
                shape, corners, fillColor, borderColor);
        }

        /// <summary>
        /// Converts byte color channels to straight-alpha normalized shader values.
        /// </summary>
        /// <param name="color">Color to normalize.</param>
        /// <returns>Normalized red, green, blue, and alpha channels.</returns>
        public static float4 NormalizeColor(byte4 color) {
            const double channelScale = 1d / 255d;
            return new float4(
                (float)(color.X * channelScale),
                (float)(color.Y * channelScale),
                (float)(color.Z * channelScale),
                (float)(color.W * channelScale));
        }

        /// <summary>
        /// Determines whether a source geometry parameter is neither infinite nor NaN.
        /// </summary>
        /// <param name="value">Radius or border width to validate before clamping.</param>
        /// <returns>True when the supplied parameter is finite.</returns>
        static bool IsFinite(double value) {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        /// <summary>
        /// Rotates a local Y-down pixel offset with the same sign as the existing DirectX sprite shader.
        /// </summary>
        /// <param name="centerX">Unrotated quad center X.</param>
        /// <param name="centerY">Unrotated quad center Y.</param>
        /// <param name="localX">Local horizontal offset.</param>
        /// <param name="localYDown">Local vertical offset positive toward screen bottom.</param>
        /// <param name="depth">Vertex depth.</param>
        /// <param name="cosine">Cosine of the rotation angle.</param>
        /// <param name="sine">Sine of the rotation angle.</param>
        /// <returns>Homogeneous screen-space position.</returns>
        static float4 RotatePosition(double centerX, double centerY, double localX, double localYDown,
            double depth, double cosine, double sine) {
            return new float4(
                (float)(centerX + localX * cosine + localYDown * sine),
                (float)(centerY - localX * sine + localYDown * cosine),
                (float)depth,
                1f);
        }

        /// <summary>
        /// Creates one textured vertex with explicitly zeroed shape-only attributes.
        /// </summary>
        /// <param name="centerX">Quad center X.</param>
        /// <param name="centerY">Quad center Y.</param>
        /// <param name="localX">Local horizontal corner offset.</param>
        /// <param name="localYDown">Local vertical corner offset, positive downward.</param>
        /// <param name="depth">Vertex depth.</param>
        /// <param name="cosine">Rotation cosine.</param>
        /// <param name="sine">Rotation sine.</param>
        /// <param name="u">Texture U coordinate.</param>
        /// <param name="v">Texture V coordinate.</param>
        /// <param name="color">Straight-alpha normalized tint.</param>
        /// <returns>Fully initialized textured vertex.</returns>
        static Batch2DVertex CreateTexturedVertex(double centerX, double centerY, double localX,
            double localYDown, double depth, double cosine, double sine, float u, float v, float4 color) {
            return new Batch2DVertex {
                Position = RotatePosition(centerX, centerY, localX, localYDown, depth, cosine, sine),
                TexLocal = new float4(u, v, 0f, 0f),
                Color = color,
                Shape = new float4(0f, 0f, 0f, 0f),
                Corners = new float4(0f, 0f, 0f, 0f),
                BorderColor = new float4(0f, 0f, 0f, 0f)
            };
        }

        /// <summary>
        /// Creates one rounded-shape vertex with screen position rotated and local coordinates unchanged.
        /// </summary>
        /// <param name="centerX">Quad center X.</param>
        /// <param name="centerY">Quad center Y.</param>
        /// <param name="localX">Local horizontal corner offset.</param>
        /// <param name="localYDown">Local vertical corner offset, positive downward.</param>
        /// <param name="depth">Vertex depth.</param>
        /// <param name="cosine">Rotation cosine.</param>
        /// <param name="sine">Rotation sine.</param>
        /// <param name="shape">Half-extents, clamped radius, and clamped border width.</param>
        /// <param name="corners">Four corner enable values.</param>
        /// <param name="fillColor">Straight-alpha fill color.</param>
        /// <param name="borderColor">Straight-alpha border color.</param>
        /// <returns>Fully initialized rounded-shape vertex.</returns>
        static Batch2DVertex CreateShapeVertex(double centerX, double centerY, double localX,
            double localYDown, double depth, double cosine, double sine, float4 shape, float4 corners,
            float4 fillColor, float4 borderColor) {
            return new Batch2DVertex {
                Position = RotatePosition(centerX, centerY, localX, localYDown, depth, cosine, sine),
                TexLocal = new float4(0f, 0f, (float)localX, (float)localYDown),
                Color = fillColor,
                Shape = shape,
                Corners = corners,
                BorderColor = borderColor
            };
        }
    }
}
