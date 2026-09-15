namespace helengine {
    /// <summary>
    /// Computes CPU-side geometry for rounded-rectangle 2D shapes: outer/inner boundary ring
    /// points for the procedural triangle-mesh rendering path, and nine-slice tile destination
    /// rectangles for the atlas-based rendering path. All boundary math is performed with double
    /// precision and only converted to float for the returned point and rectangle values, since
    /// this geometry is regenerated every draw call for every ring segment.
    /// </summary>
    public class RoundedRectGeometryBuilder {
        /// <summary>
        /// Computes a single point on a rounded rectangle's boundary ring at the given angle, for
        /// a rectangle of the supplied size and corner radius centered at the given point. The
        /// boundary follows the rectangle edges and rounds off near each corner: a directional ray
        /// from the center is clamped to the rectangle's straight edges, then blended into a
        /// circular arc of the given radius once the ray crosses into a corner region.
        /// </summary>
        /// <param name="angle">Angle, in radians, measured around the ring.</param>
        /// <param name="width">Full width of the rectangle.</param>
        /// <param name="height">Full height of the rectangle.</param>
        /// <param name="radius">Corner radius, already clamped to the rectangle's half-extents.</param>
        /// <param name="centerX">X coordinate of the rectangle's center.</param>
        /// <param name="centerY">Y coordinate of the rectangle's center.</param>
        /// <returns>The boundary point, in the same coordinate space as the supplied center.</returns>
        public float2 RingPointAt(double angle, double width, double height, double radius, double centerX, double centerY) {
            double directionX = Math.Cos(angle);
            double directionY = Math.Sin(angle);
            double halfWidth = width * 0.5;
            double halfHeight = height * 0.5;
            double signX = Math.Sign(directionX);
            double signY = Math.Sign(directionY);

            double pointX = signX * Math.Max(Math.Abs(halfWidth - radius), Math.Abs(halfWidth * directionX)) + centerX;
            double pointY = signY * Math.Max(Math.Abs(halfHeight - radius), Math.Abs(halfHeight * directionY)) + centerY;

            if (Math.Abs(pointX - centerX) > (halfWidth - radius) && Math.Abs(pointY - centerY) > (halfHeight - radius)) {
                double cornerCenterX = centerX + signX * (halfWidth - radius);
                double cornerCenterY = centerY + signY * (halfHeight - radius);
                pointX = cornerCenterX + radius * signX * Math.Abs(directionX);
                pointY = cornerCenterY + radius * signY * Math.Abs(directionY);
            }

            return new float2((float)pointX, (float)pointY);
        }

        /// <summary>
        /// Computes the angle, in radians, for the given ring segment index out of the given total
        /// segment count. The fraction of a full turn is first rounded to single precision before
        /// being widened back to double, deliberately reproducing the exact rounding that the
        /// single-precision angle stepping this geometry replaced would have produced. The ring
        /// boundary formula in <see cref="RingPointAt"/> is discontinuous at each of the four
        /// cardinal angles (where a straight edge, not a single point, is the true boundary at that
        /// angle), so which side of that discontinuity gets resolved depends on whether the angle
        /// landed a hair above or below the true cardinal value; preserving the original rounding
        /// keeps that resolution identical to the previously shipped geometry instead of flipping
        /// arbitrarily because double precision approximates pi from the other direction.
        /// </summary>
        /// <param name="index">Segment index, from 0 up to (but not including) <paramref name="steps"/>.</param>
        /// <param name="steps">Total number of ring segments around the full boundary.</param>
        /// <returns>The segment's angle, in radians.</returns>
        double SegmentAngle(int index, int steps) {
            float singlePrecisionAngle = (index / (float)steps) * (float)Math.PI * 2.0f;
            return singlePrecisionAngle;
        }

        /// <summary>
        /// Builds the fill triangle-fan vertex positions for a rounded rectangle's outer boundary.
        /// The ring is split into the given number of equal-angle segments; each segment contributes
        /// three vertices, in order: the shape center, then the two boundary points bracketing that
        /// segment.
        /// </summary>
        /// <param name="steps">Number of ring segments to generate.</param>
        /// <param name="width">Full width of the rectangle.</param>
        /// <param name="height">Full height of the rectangle.</param>
        /// <param name="radius">Corner radius, already clamped to the rectangle's half-extents.</param>
        /// <param name="centerX">X coordinate of the rectangle's center.</param>
        /// <param name="centerY">Y coordinate of the rectangle's center.</param>
        /// <returns>Array of <c>steps * 3</c> vertex positions, ready to be written in order to a triangle-list vertex buffer.</returns>
        public float2[] BuildFillRingVertices(int steps, double width, double height, double radius, double centerX, double centerY) {
            float2[] vertices = new float2[steps * 3];
            float2 center = new float2((float)centerX, (float)centerY);

            for (int i = 0; i < steps; i++) {
                double angle0 = SegmentAngle(i, steps);
                double angle1 = SegmentAngle((i + 1) % steps, steps);
                float2 outer0 = RingPointAt(angle0, width, height, radius, centerX, centerY);
                float2 outer1 = RingPointAt(angle1, width, height, radius, centerX, centerY);

                int baseIndex = i * 3;
                vertices[baseIndex] = center;
                vertices[baseIndex + 1] = outer0;
                vertices[baseIndex + 2] = outer1;
            }

            return vertices;
        }

        /// <summary>
        /// Builds the border quad vertex positions for a rounded rectangle's ring. The ring is
        /// split into the given number of equal-angle segments; each segment contributes two
        /// triangles (six vertices) spanning from the outer boundary to the inner boundary, both
        /// rings sharing the same center.
        /// </summary>
        /// <param name="steps">Number of ring segments to generate.</param>
        /// <param name="outerWidth">Full width of the outer rectangle.</param>
        /// <param name="outerHeight">Full height of the outer rectangle.</param>
        /// <param name="outerRadius">Outer corner radius, already clamped to the rectangle's half-extents.</param>
        /// <param name="innerWidth">Full width of the inner rectangle (outer size minus twice the border thickness).</param>
        /// <param name="innerHeight">Full height of the inner rectangle (outer size minus twice the border thickness).</param>
        /// <param name="innerRadius">Inner corner radius (outer radius minus the border thickness, floored at zero).</param>
        /// <param name="centerX">X coordinate of the rectangle's center, shared by the outer and inner rings.</param>
        /// <param name="centerY">Y coordinate of the rectangle's center, shared by the outer and inner rings.</param>
        /// <returns>Array of <c>steps * 6</c> vertex positions, ready to be written in order to a triangle-list vertex buffer.</returns>
        public float2[] BuildBorderRingVertices(int steps, double outerWidth, double outerHeight, double outerRadius, double innerWidth, double innerHeight, double innerRadius, double centerX, double centerY) {
            float2[] vertices = new float2[steps * 6];

            for (int i = 0; i < steps; i++) {
                double angle0 = SegmentAngle(i, steps);
                double angle1 = SegmentAngle((i + 1) % steps, steps);

                float2 outer0 = RingPointAt(angle0, outerWidth, outerHeight, outerRadius, centerX, centerY);
                float2 outer1 = RingPointAt(angle1, outerWidth, outerHeight, outerRadius, centerX, centerY);
                float2 inner0 = RingPointAt(angle0, innerWidth, innerHeight, innerRadius, centerX, centerY);
                float2 inner1 = RingPointAt(angle1, innerWidth, innerHeight, innerRadius, centerX, centerY);

                int baseIndex = i * 6;
                vertices[baseIndex] = outer0;
                vertices[baseIndex + 1] = outer1;
                vertices[baseIndex + 2] = inner1;
                vertices[baseIndex + 3] = outer0;
                vertices[baseIndex + 4] = inner1;
                vertices[baseIndex + 5] = inner0;
            }

            return vertices;
        }

        /// <summary>
        /// Computes the nine nine-slice destination rectangles for tiling a rounded rectangle's
        /// fill or border atlas across a shape of the given position and size, using the given
        /// corner tile size for both corner tiles and the thickness of the edge tiles. Rectangles
        /// are returned row-major (top row, middle row, bottom row), each row left tile, middle
        /// tile, right tile, matching the atlas layout produced by <see cref="NineSliceAtlas"/>.
        /// </summary>
        /// <param name="x">X position of the shape's top-left corner.</param>
        /// <param name="y">Y position of the shape's top-left corner.</param>
        /// <param name="width">Full width of the shape.</param>
        /// <param name="height">Full height of the shape.</param>
        /// <param name="cornerSize">Size, in pixels, of each corner tile, also used as the edge tile thickness.</param>
        /// <returns>Array of 9 destination rectangles, each as (x, y, width, height).</returns>
        public float4[] BuildNineSliceTileRects(float x, float y, float width, float height, int cornerSize) {
            int leftWidth = cornerSize;
            int rightWidth = cornerSize;
            int middleWidth = Math.Max(1, (int)width - leftWidth - rightWidth);
            int topHeight = cornerSize;
            int bottomHeight = cornerSize;
            int middleHeight = Math.Max(1, (int)height - topHeight - bottomHeight);

            return new float4[] {
                new float4(x, y, leftWidth, topHeight),
                new float4(x + leftWidth, y, middleWidth, topHeight),
                new float4(x + leftWidth + middleWidth, y, rightWidth, topHeight),
                new float4(x, y + topHeight, leftWidth, middleHeight),
                new float4(x + leftWidth, y + topHeight, middleWidth, middleHeight),
                new float4(x + leftWidth + middleWidth, y + topHeight, rightWidth, middleHeight),
                new float4(x, y + topHeight + middleHeight, leftWidth, bottomHeight),
                new float4(x + leftWidth, y + topHeight + middleHeight, middleWidth, bottomHeight),
                new float4(x + leftWidth + middleWidth, y + topHeight + middleHeight, rightWidth, bottomHeight)
            };
        }
    }
}
