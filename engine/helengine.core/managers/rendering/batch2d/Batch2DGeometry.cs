namespace helengine {
    /// <summary>
    /// Builds the four value vertices used by textured drawable and rounded-shape batch quads.
    /// </summary>
    public static class Batch2DGeometry {
        /// <summary>Resolves pivot and scalar rotation in local pixels before applying any entity transform.</summary>
        /// <param name="sprite">Sprite supplying size, optional texture-size fallback, pivot, and rotation.</param>
        /// <returns>Local corners retaining their source UV association.</returns>
        public static SpriteQuad2D ResolveSpriteLocalQuad(ISpriteDrawable2D sprite) {
            if (sprite == null) {
                throw new ArgumentNullException(nameof(sprite));
            }
            int2 size = sprite.Size;
            if (size.X <= 0 || size.Y <= 0) {
                RuntimeTexture texture = sprite.Texture;
                if (texture == null) {
                    throw new InvalidOperationException("A sprite without an authored size requires a texture.");
                }
                size = new int2(texture.Width, texture.Height);
            }
            float2 pivot = sprite.Pivot;
            RealEvaluation left = -(RealEvaluation)pivot.X * size.X;
            RealEvaluation top = -(RealEvaluation)pivot.Y * size.Y;
            RealEvaluation angle = sprite.RotationRadians;
            RealEvaluation cosine = angle == 0d ? 1d : RealMath.Cos(angle);
            RealEvaluation sine = angle == 0d ? 0d : RealMath.Sin(angle);
            return new SpriteQuad2D {
                TopLeft = RotateSpritePoint(left, top, cosine, sine),
                TopRight = RotateSpritePoint(left + size.X, top, cosine, sine),
                BottomRight = RotateSpritePoint(left + size.X, top + size.Y, cosine, sine),
                BottomLeft = RotateSpritePoint(left, top + size.Y, cosine, sine)
            };
        }

        /// <summary>Composes sprite-local geometry with the exact entity hierarchy once, without angle decomposition.</summary>
        /// <param name="sprite">Attached sprite whose destination geometry is captured synchronously.</param>
        /// <returns>Pixel corners preserving rotations, mirrored scales, and affine shear.</returns>
        public static SpriteQuad2D ResolveSpriteQuad(ISpriteDrawable2D sprite) {
            SpriteQuad2D local = ResolveSpriteLocalQuad(sprite);
            if (sprite.Parent == null) {
                throw new InvalidOperationException("Sprite geometry requires an owning entity.");
            }
            float4x4 world = sprite.Parent.WorldTransformMatrix;
            return new SpriteQuad2D {
                TopLeft = TransformSpritePoint(local.TopLeft, world),
                TopRight = TransformSpritePoint(local.TopRight, world),
                BottomRight = TransformSpritePoint(local.BottomRight, world),
                BottomLeft = TransformSpritePoint(local.BottomLeft, world)
            };
        }

        /// <summary>Copies resolved sprite corners into batch vertices without repeating trigonometry or transforms.</summary>
        /// <param name="quad">Resolved destination corners.</param>
        /// <param name="sourceRect">Normalized source U, V, width, and height.</param>
        /// <param name="color">Straight-alpha normalized tint.</param>
        /// <param name="topLeft">Receives the upper-left UV vertex.</param>
        /// <param name="topRight">Receives the upper-right UV vertex.</param>
        /// <param name="bottomRight">Receives the lower-right UV vertex.</param>
        /// <param name="bottomLeft">Receives the lower-left UV vertex.</param>
        public static void CreateSpriteQuad(SpriteQuad2D quad, float4 sourceRect, float4 color,
            out Batch2DVertex topLeft, out Batch2DVertex topRight,
            out Batch2DVertex bottomRight, out Batch2DVertex bottomLeft) {
            topLeft = CreateTexturedVertex(quad.TopLeft.X, quad.TopLeft.Y, 0d, 0d, 0d, 1d, 0d,
                sourceRect.X, sourceRect.Y, color);
            topRight = CreateTexturedVertex(quad.TopRight.X, quad.TopRight.Y, 0d, 0d, 0d, 1d, 0d,
                sourceRect.X + sourceRect.Z, sourceRect.Y, color);
            bottomRight = CreateTexturedVertex(quad.BottomRight.X, quad.BottomRight.Y, 0d, 0d, 0d, 1d, 0d,
                sourceRect.X + sourceRect.Z, sourceRect.Y + sourceRect.W, color);
            bottomLeft = CreateTexturedVertex(quad.BottomLeft.X, quad.BottomLeft.Y, 0d, 0d, 0d, 1d, 0d,
                sourceRect.X, sourceRect.Y + sourceRect.W, color);
        }

        /// <summary>Rotates one local point with the same positive-Z sign as entity quaternions.</summary>
        /// <param name="x">Horizontal local pixel offset.</param>
        /// <param name="y">Vertical local pixel offset.</param>
        /// <param name="cosine">Shared cosine for this sprite.</param>
        /// <param name="sine">Shared sine for this sprite.</param>
        /// <returns>Rotated local point.</returns>
        static float2 RotateSpritePoint(RealEvaluation x, RealEvaluation y, RealEvaluation cosine, RealEvaluation sine) {
            return new float2((RealEvaluation)(x * cosine - y * sine), (RealEvaluation)(x * sine + y * cosine));
        }

        /// <summary>Applies an affine row-vector entity matrix to a sprite-local point in its Z-zero plane.</summary>
        /// <param name="point">Sprite-local point.</param>
        /// <param name="world">Composed entity-to-world matrix.</param>
        /// <returns>World pixel position projected to the 2D plane.</returns>
        static float2 TransformSpritePoint(float2 point, float4x4 world) {
            return new float2(point.X * world.M11 + point.Y * world.M21 + world.M41,
                point.X * world.M12 + point.Y * world.M22 + world.M42);
        }

        /// <summary>Creates a sprite quad from its transformed local origin and world-space horizontal and vertical axes.</summary>
        /// <param name="origin">World position of the sprite's local top-left corner.</param>
        /// <param name="right">World direction of the sprite's local positive X axis.</param>
        /// <param name="down">World direction of the sprite's local positive Y axis.</param>
        /// <param name="width">Local width multiplied by inherited horizontal scale.</param>
        /// <param name="height">Local height multiplied by inherited vertical scale.</param>
        /// <param name="depth">Pixel-space depth copied to every vertex.</param>
        /// <param name="sourceRect">Normalized source rectangle ordered U, V, width, height.</param>
        /// <param name="color">Straight-alpha normalized tint.</param>
        /// <param name="topLeft">Receives the transformed local origin.</param>
        /// <param name="topRight">Receives the transformed upper-right corner.</param>
        /// <param name="bottomRight">Receives the transformed lower-right corner.</param>
        /// <param name="bottomLeft">Receives the transformed lower-left corner.</param>
        public static void CreateTransformedQuad(float3 origin, float3 right, float3 down,
            RealEvaluation width, RealEvaluation height, RealEvaluation depth, float4 sourceRect, float4 color,
            out Batch2DVertex topLeft, out Batch2DVertex topRight,
            out Batch2DVertex bottomRight, out Batch2DVertex bottomLeft) {
            RealEvaluation rightU = sourceRect.X + sourceRect.Z;
            RealEvaluation bottomV = sourceRect.Y + sourceRect.W;
            topLeft = CreateTransformedTexturedVertex(origin, right, down, 0d, 0d, depth,
                sourceRect.X, sourceRect.Y, color);
            topRight = CreateTransformedTexturedVertex(origin, right, down, width, 0d, depth,
                rightU, sourceRect.Y, color);
            bottomRight = CreateTransformedTexturedVertex(origin, right, down, width, height, depth,
                rightU, bottomV, color);
            bottomLeft = CreateTransformedTexturedVertex(origin, right, down, 0d, height, depth,
                sourceRect.X, bottomV, color);
        }

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
        public static void CreateQuad(RealEvaluation x, RealEvaluation y, RealEvaluation width, RealEvaluation height, RealEvaluation depth, RealEvaluation rotation,
            float4 sourceRect, float4 color, out Batch2DVertex topLeft, out Batch2DVertex topRight,
            out Batch2DVertex bottomRight, out Batch2DVertex bottomLeft) {
            RealEvaluation halfWidth = width * 0.5d;
            RealEvaluation halfHeight = height * 0.5d;
            RealEvaluation centerX = x + halfWidth;
            RealEvaluation centerY = y + halfHeight;
            RealEvaluation cosine = RealMath.Cos(rotation);
            RealEvaluation sine = RealMath.Sin(rotation);
            RealEvaluation rightU = sourceRect.X + sourceRect.Z;
            RealEvaluation bottomV = sourceRect.Y + sourceRect.W;

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
        public static void CreateRoundedQuad(RealEvaluation x, RealEvaluation y, RealEvaluation width, RealEvaluation height, RealEvaluation depth,
            RealEvaluation rotation, RealEvaluation radius, RealEvaluation borderWidth, float4 corners, float4 fillColor,
            float4 borderColor, out Batch2DVertex topLeft, out Batch2DVertex topRight,
            out Batch2DVertex bottomRight, out Batch2DVertex bottomLeft) {
            if (!IsFinite(radius)) {
                throw new ArgumentException("Rounded-shape radius must be finite.", nameof(radius));
            }
            if (!IsFinite(borderWidth)) {
                throw new ArgumentException("Rounded-shape border width must be finite.", nameof(borderWidth));
            }

            RealEvaluation halfWidth = width * 0.5d;
            RealEvaluation halfHeight = height * 0.5d;
            RealEvaluation centerX = x + halfWidth;
            RealEvaluation centerY = y + halfHeight;
            RealEvaluation cosine = RealMath.Cos(rotation);
            RealEvaluation sine = RealMath.Sin(rotation);
            float4 shape = new float4((RealEvaluation)halfWidth, (RealEvaluation)halfHeight,
                (RealEvaluation)RealMath.Min(RealMath.Max(radius, 0d), RealMath.Min(halfWidth, halfHeight)),
                (RealEvaluation)RealMath.Min(RealMath.Max(borderWidth, 0d), RealMath.Min(halfWidth, halfHeight)));

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
            RealEvaluation channelScale = 1d / 255d;
            return new float4(
                (RealEvaluation)(color.X * channelScale),
                (RealEvaluation)(color.Y * channelScale),
                (RealEvaluation)(color.Z * channelScale),
                (RealEvaluation)(color.W * channelScale));
        }

        /// <summary>
        /// Determines whether a source geometry parameter is neither infinite nor NaN.
        /// </summary>
        /// <param name="value">Radius or border width to validate before clamping.</param>
        /// <returns>True when the supplied parameter is finite.</returns>
        static bool IsFinite(RealEvaluation value) {
            return !RealEvaluation.IsNaN(value) && !RealEvaluation.IsInfinity(value);
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
        static float4 RotatePosition(RealEvaluation centerX, RealEvaluation centerY, RealEvaluation localX, RealEvaluation localYDown,
            RealEvaluation depth, RealEvaluation cosine, RealEvaluation sine) {
            return new float4(
                (RealEvaluation)(centerX + localX * cosine + localYDown * sine),
                (RealEvaluation)(centerY - localX * sine + localYDown * cosine),
                (RealEvaluation)depth,
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
        static Batch2DVertex CreateTexturedVertex(RealEvaluation centerX, RealEvaluation centerY, RealEvaluation localX,
            RealEvaluation localYDown, RealEvaluation depth, RealEvaluation cosine, RealEvaluation sine, RealEvaluation u, RealEvaluation v, float4 color) {
            return new Batch2DVertex {
                Position = RotatePosition(centerX, centerY, localX, localYDown, depth, cosine, sine),
                TexLocal = new float4(u, v, 0f, 0f),
                Color = color,
                Shape = new float4(0f, 0f, 0f, 0f),
                Corners = new float4(0f, 0f, 0f, 0f),
                BorderColor = new float4(0f, 0f, 0f, 0f)
            };
        }

        /// <summary>Transforms one sprite-local offset through inherited axes and initializes all textured vertex attributes.</summary>
        /// <param name="origin">World position of the local top-left corner.</param>
        /// <param name="right">World direction of the local positive X axis.</param>
        /// <param name="down">World direction of the local positive Y axis.</param>
        /// <param name="localX">Scaled horizontal distance from the local origin.</param>
        /// <param name="localY">Scaled vertical distance from the local origin.</param>
        /// <param name="depth">Pixel-space depth copied to the vertex.</param>
        /// <param name="u">Texture U coordinate.</param>
        /// <param name="v">Texture V coordinate.</param>
        /// <param name="color">Straight-alpha normalized tint.</param>
        /// <returns>Fully initialized vertex at the transformed local corner.</returns>
        static Batch2DVertex CreateTransformedTexturedVertex(float3 origin, float3 right, float3 down,
            RealEvaluation localX, RealEvaluation localY, RealEvaluation depth, RealEvaluation u, RealEvaluation v, float4 color) {
            RealEvaluation x = origin.X + localX * right.X + localY * down.X;
            RealEvaluation y = origin.Y + localX * right.Y + localY * down.Y;
            return CreateTexturedVertex(x, y, 0d, 0d, depth, 1d, 0d, u, v, color);
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
        static Batch2DVertex CreateShapeVertex(RealEvaluation centerX, RealEvaluation centerY, RealEvaluation localX,
            RealEvaluation localYDown, RealEvaluation depth, RealEvaluation cosine, RealEvaluation sine, float4 shape, float4 corners,
            float4 fillColor, float4 borderColor) {
            return new Batch2DVertex {
                Position = RotatePosition(centerX, centerY, localX, localYDown, depth, cosine, sine),
                TexLocal = new float4(0f, 0f, (RealEvaluation)localX, (RealEvaluation)localYDown),
                Color = fillColor,
                Shape = shape,
                Corners = corners,
                BorderColor = borderColor
            };
        }
    }
}
