using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace helengine.editor.iconbuilder {
    /// <summary>
    /// Generates toolbar PNG icons for transform controls, snap labels, and snap adjustment buttons.
    /// </summary>
    public class ToolbarIconBuilder {
        /// <summary>
        /// X-axis accent color used by generated icons.
        /// </summary>
        static readonly Color XAxisColor = Color.FromArgb(255, 255, 107, 107);
        /// <summary>
        /// Y-axis accent color used by generated icons.
        /// </summary>
        static readonly Color YAxisColor = Color.FromArgb(255, 99, 230, 132);
        /// <summary>
        /// Z-axis accent color used by generated icons.
        /// </summary>
        static readonly Color ZAxisColor = Color.FromArgb(255, 97, 168, 255);
        /// <summary>
        /// Neutral joint color shared by all generated icons.
        /// </summary>
        static readonly Color NeutralColor = Color.FromArgb(255, 245, 247, 251);
        /// <summary>
        /// Neutral foreground color used by generated snap arrow icons.
        /// </summary>
        static readonly Color SnapArrowColor = Color.FromArgb(255, 245, 247, 251);
        /// <summary>
        /// Neutral foreground color used by the generated grid icon.
        /// </summary>
        static readonly Color GridLineColor = Color.FromArgb(255, 235, 239, 246);
        /// <summary>
        /// Body color used by the generated magnet icon.
        /// </summary>
        static readonly Color MagnetBodyColor = Color.FromArgb(255, 241, 91, 91);
        /// <summary>
        /// Pole color used by the generated magnet icon.
        /// </summary>
        static readonly Color MagnetPoleColor = Color.FromArgb(255, 251, 252, 254);
        /// <summary>
        /// Fill color used by the generated keycap icons.
        /// </summary>
        static readonly Color KeyFillColor = Color.FromArgb(255, 247, 248, 252);
        /// <summary>
        /// Stroke color used by the generated keycap icons.
        /// </summary>
        static readonly Color KeyStrokeColor = Color.FromArgb(255, 118, 126, 148);
        /// <summary>
        /// Text color used by the generated keycap icons.
        /// </summary>
        static readonly Color KeyTextColor = Color.FromArgb(255, 46, 55, 74);

        /// <summary>
        /// Builds every toolbar icon into the supplied output directory.
        /// </summary>
        /// <param name="outputDirectory">Directory that receives the generated PNG files.</param>
        /// <param name="iconSize">Square icon size in pixels.</param>
        public void BuildAll(string outputDirectory, int iconSize) {
            if (string.IsNullOrWhiteSpace(outputDirectory)) {
                throw new ArgumentException("Output directory must be provided.", nameof(outputDirectory));
            }

            if (iconSize < 16) {
                throw new ArgumentOutOfRangeException(nameof(iconSize), "Icon size must be at least 16 pixels.");
            }

            Directory.CreateDirectory(outputDirectory);

            BuildTransformIcon(Path.Combine(outputDirectory, "transform.png"), iconSize);
            BuildRotateIcon(Path.Combine(outputDirectory, "rotate.png"), iconSize);
            BuildScaleIcon(Path.Combine(outputDirectory, "scale.png"), iconSize);
            BuildGridIcon(Path.Combine(outputDirectory, "grid.png"), iconSize);
            BuildMagnetIcon(Path.Combine(outputDirectory, "magnet.png"), iconSize);
            BuildCtrlKeyIcon(Path.Combine(outputDirectory, "key-ctrl.png"), iconSize);
            BuildShiftKeyIcon(Path.Combine(outputDirectory, "key-shift.png"), iconSize);
            BuildSnapIncreaseIcon(Path.Combine(outputDirectory, "snap-increase.png"), iconSize);
            BuildSnapDecreaseIcon(Path.Combine(outputDirectory, "snap-decrease.png"), iconSize);
        }

        /// <summary>
        /// Builds the translation-style transform icon.
        /// </summary>
        /// <param name="filePath">Destination file path.</param>
        /// <param name="iconSize">Square icon size in pixels.</param>
        void BuildTransformIcon(string filePath, int iconSize) {
            using Bitmap bitmap = CreateBitmap(iconSize);
            using Graphics graphics = CreateGraphics(bitmap);
            double strokeWidth = iconSize * 0.0875;
            PointF center = new PointF(
                (float)(iconSize * 0.36),
                (float)(iconSize * 0.58));
            PointF xEnd = new PointF(
                (float)(iconSize * 0.84),
                (float)(iconSize * 0.38));
            PointF yEnd = new PointF(
                (float)(iconSize * 0.36),
                (float)(iconSize * 0.12));
            PointF zEnd = new PointF(
                (float)(iconSize * 0.14),
                (float)(iconSize * 0.82));
            double headLength = iconSize * 0.14;
            double headWidth = iconSize * 0.12;
            double jointRadius = iconSize * 0.07;

            DrawAxisLine(graphics, center, xEnd, XAxisColor, strokeWidth);
            DrawAxisLine(graphics, center, yEnd, YAxisColor, strokeWidth);
            DrawAxisLine(graphics, center, zEnd, ZAxisColor, strokeWidth);
            FillArrowHead(graphics, center, xEnd, XAxisColor, headLength, headWidth);
            FillArrowHead(graphics, center, yEnd, YAxisColor, headLength, headWidth);
            FillArrowHead(graphics, center, zEnd, ZAxisColor, headLength, headWidth);
            FillCircle(graphics, center, jointRadius, NeutralColor);

            SaveBitmap(bitmap, filePath);
        }

        /// <summary>
        /// Builds the rotation icon using three circular arrows.
        /// </summary>
        /// <param name="filePath">Destination file path.</param>
        /// <param name="iconSize">Square icon size in pixels.</param>
        void BuildRotateIcon(string filePath, int iconSize) {
            using Bitmap bitmap = CreateBitmap(iconSize);
            using Graphics graphics = CreateGraphics(bitmap);
            double strokeWidth = iconSize * 0.075;
            float center = (float)(iconSize * 0.5);
            float radius = (float)(iconSize * 0.29);
            double headLength = iconSize * 0.125;
            double headWidth = iconSize * 0.11;
            double jointRadius = iconSize * 0.055;

            DrawArcArrow(graphics, center, center, radius, 200.0, 112.0, XAxisColor, strokeWidth, headLength, headWidth);
            DrawArcArrow(graphics, center, center, radius, 330.0, 112.0, YAxisColor, strokeWidth, headLength, headWidth);
            DrawArcArrow(graphics, center, center, radius, 92.0, 102.0, ZAxisColor, strokeWidth, headLength, headWidth);
            FillCircle(graphics, new PointF(center, center), jointRadius, NeutralColor);

            SaveBitmap(bitmap, filePath);
        }

        /// <summary>
        /// Builds the scale icon using axis arms capped with boxes.
        /// </summary>
        /// <param name="filePath">Destination file path.</param>
        /// <param name="iconSize">Square icon size in pixels.</param>
        void BuildScaleIcon(string filePath, int iconSize) {
            using Bitmap bitmap = CreateBitmap(iconSize);
            using Graphics graphics = CreateGraphics(bitmap);
            double strokeWidth = iconSize * 0.075;
            PointF center = new PointF(
                (float)(iconSize * 0.34),
                (float)(iconSize * 0.60));
            PointF xEnd = new PointF(
                (float)(iconSize * 0.80),
                (float)(iconSize * 0.40));
            PointF yEnd = new PointF(
                (float)(iconSize * 0.34),
                (float)(iconSize * 0.16));
            PointF zEnd = new PointF(
                (float)(iconSize * 0.16),
                (float)(iconSize * 0.82));
            double boxSize = iconSize * 0.14;
            double jointRadius = iconSize * 0.055;

            DrawAxisLine(graphics, center, xEnd, XAxisColor, strokeWidth);
            DrawAxisLine(graphics, center, yEnd, YAxisColor, strokeWidth);
            DrawAxisLine(graphics, center, zEnd, ZAxisColor, strokeWidth);
            FillSquare(graphics, xEnd, boxSize, XAxisColor);
            FillSquare(graphics, yEnd, boxSize, YAxisColor);
            FillSquare(graphics, zEnd, boxSize, ZAxisColor);
            FillCircle(graphics, center, jointRadius, NeutralColor);

            SaveBitmap(bitmap, filePath);
        }

        /// <summary>
        /// Builds the viewport grid icon using a receding ground-plane grid.
        /// </summary>
        /// <param name="filePath">Destination file path.</param>
        /// <param name="iconSize">Square icon size in pixels.</param>
        void BuildGridIcon(string filePath, int iconSize) {
            using Bitmap bitmap = CreateBitmap(iconSize);
            using Graphics graphics = CreateGraphics(bitmap);
            float strokeWidth = Math.Max(2f, (float)(iconSize * 0.06));
            float halfStroke = strokeWidth * 0.5f;
            float left = (float)(iconSize * 0.16);
            float right = (float)(iconSize * 0.84);
            float top = (float)(iconSize * 0.24);
            float bottom = (float)(iconSize * 0.84);
            float centerX = iconSize * 0.5f;
            float horizonY = (float)(iconSize * 0.38);

            using Pen gridPen = new Pen(GridLineColor, strokeWidth) {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };

            graphics.DrawLine(gridPen, left, bottom - halfStroke, right, bottom - halfStroke);
            graphics.DrawLine(gridPen, centerX, top, centerX, bottom);
            graphics.DrawLine(gridPen, left, bottom, centerX, horizonY);
            graphics.DrawLine(gridPen, right, bottom, centerX, horizonY);
            graphics.DrawLine(
                gridPen,
                (float)(iconSize * 0.28),
                (float)(iconSize * 0.62),
                (float)(iconSize * 0.72),
                (float)(iconSize * 0.62));
            graphics.DrawLine(
                gridPen,
                (float)(iconSize * 0.36),
                (float)(iconSize * 0.50),
                (float)(iconSize * 0.64),
                (float)(iconSize * 0.50));

            SaveBitmap(bitmap, filePath);
        }

        /// <summary>
        /// Builds the snap label magnet icon.
        /// </summary>
        /// <param name="filePath">Destination file path.</param>
        /// <param name="iconSize">Base icon size in pixels.</param>
        void BuildMagnetIcon(string filePath, int iconSize) {
            using Bitmap bitmap = CreateBitmap(iconSize);
            using Graphics graphics = CreateGraphics(bitmap);
            DrawMagnetSymbol(graphics, iconSize, iconSize);
            SaveBitmap(bitmap, filePath);
        }

        /// <summary>
        /// Builds the control-key keycap icon used by the first snap label.
        /// </summary>
        /// <param name="filePath">Destination file path.</param>
        /// <param name="iconSize">Base icon height in pixels.</param>
        void BuildCtrlKeyIcon(string filePath, int iconSize) {
            int keyWidth = (int)Math.Round(iconSize * 1.55);
            using Bitmap bitmap = CreateBitmap(keyWidth, iconSize);
            using Graphics graphics = CreateGraphics(bitmap);
            DrawKeycapSymbol(graphics, keyWidth, iconSize, "CTRL");
            SaveBitmap(bitmap, filePath);
        }

        /// <summary>
        /// Builds the shift-key keycap icon used by the second snap label.
        /// </summary>
        /// <param name="filePath">Destination file path.</param>
        /// <param name="iconSize">Base icon height in pixels.</param>
        void BuildShiftKeyIcon(string filePath, int iconSize) {
            int keyWidth = (int)Math.Round(iconSize * 1.80);
            using Bitmap bitmap = CreateBitmap(keyWidth, iconSize);
            using Graphics graphics = CreateGraphics(bitmap);
            DrawKeycapSymbol(graphics, keyWidth, iconSize, "SHIFT");
            SaveBitmap(bitmap, filePath);
        }

        /// <summary>
        /// Builds the snap increase icon as an upward-pointing arrow.
        /// </summary>
        /// <param name="filePath">Destination file path.</param>
        /// <param name="iconSize">Square icon size in pixels.</param>
        void BuildSnapIncreaseIcon(string filePath, int iconSize) {
            using Bitmap bitmap = CreateBitmap(iconSize);
            using Graphics graphics = CreateGraphics(bitmap);
            DrawSnapArrowSymbol(graphics, iconSize, true, SnapArrowColor);
            SaveBitmap(bitmap, filePath);
        }

        /// <summary>
        /// Builds the snap decrease icon as a downward-pointing arrow.
        /// </summary>
        /// <param name="filePath">Destination file path.</param>
        /// <param name="iconSize">Square icon size in pixels.</param>
        void BuildSnapDecreaseIcon(string filePath, int iconSize) {
            using Bitmap bitmap = CreateBitmap(iconSize);
            using Graphics graphics = CreateGraphics(bitmap);
            DrawSnapArrowSymbol(graphics, iconSize, false, SnapArrowColor);
            SaveBitmap(bitmap, filePath);
        }

        /// <summary>
        /// Creates a transparent bitmap for icon rendering.
        /// </summary>
        /// <param name="iconSize">Square icon size in pixels.</param>
        /// <returns>Allocated bitmap.</returns>
        Bitmap CreateBitmap(int iconSize) {
            Bitmap bitmap = new Bitmap(iconSize, iconSize, PixelFormat.Format32bppArgb);
            bitmap.MakeTransparent();
            return bitmap;
        }

        /// <summary>
        /// Creates a transparent bitmap for icon rendering using explicit dimensions.
        /// </summary>
        /// <param name="width">Bitmap width in pixels.</param>
        /// <param name="height">Bitmap height in pixels.</param>
        /// <returns>Allocated bitmap.</returns>
        Bitmap CreateBitmap(int width, int height) {
            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            bitmap.MakeTransparent();
            return bitmap;
        }

        /// <summary>
        /// Creates a high-quality graphics context for icon rendering.
        /// </summary>
        /// <param name="bitmap">Target bitmap.</param>
        /// <returns>Configured graphics context.</returns>
        Graphics CreateGraphics(Bitmap bitmap) {
            if (bitmap == null) {
                throw new ArgumentNullException(nameof(bitmap));
            }

            Graphics graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            return graphics;
        }

        /// <summary>
        /// Draws the snap-label magnet symbol.
        /// </summary>
        /// <param name="graphics">Graphics context receiving the magnet.</param>
        /// <param name="width">Icon width in pixels.</param>
        /// <param name="height">Icon height in pixels.</param>
        void DrawMagnetSymbol(Graphics graphics, int width, int height) {
            if (graphics == null) {
                throw new ArgumentNullException(nameof(graphics));
            }

            float lineWidth = Math.Max(4f, (float)(height * 0.18));
            float leftX = (float)(width * 0.30);
            float rightX = (float)(width * 0.70);
            float topY = (float)(height * 0.18);
            float poleBottomY = (float)(height * 0.38);
            float legBottomY = (float)(height * 0.62);
            RectangleF arcBounds = new RectangleF(
                (float)(width * 0.30),
                (float)(height * 0.22),
                (float)(width * 0.40),
                (float)(height * 0.44));

            using Pen bodyPen = new Pen(MagnetBodyColor, lineWidth);
            bodyPen.StartCap = LineCap.Round;
            bodyPen.EndCap = LineCap.Round;
            bodyPen.LineJoin = LineJoin.Round;
            graphics.DrawLine(bodyPen, leftX, poleBottomY, leftX, legBottomY);
            graphics.DrawLine(bodyPen, rightX, poleBottomY, rightX, legBottomY);
            graphics.DrawArc(bodyPen, arcBounds, 0f, 180f);

            using Pen polePen = new Pen(MagnetPoleColor, lineWidth);
            polePen.StartCap = LineCap.Round;
            polePen.EndCap = LineCap.Round;
            graphics.DrawLine(polePen, leftX, topY, leftX, poleBottomY);
            graphics.DrawLine(polePen, rightX, topY, rightX, poleBottomY);
        }

        /// <summary>
        /// Draws the rounded keycap icon used by snap-slot labels.
        /// </summary>
        /// <param name="graphics">Graphics context receiving the keycap.</param>
        /// <param name="width">Icon width in pixels.</param>
        /// <param name="height">Icon height in pixels.</param>
        /// <param name="label">Key label drawn inside the keycap.</param>
        void DrawKeycapSymbol(Graphics graphics, int width, int height, string label) {
            if (graphics == null) {
                throw new ArgumentNullException(nameof(graphics));
            }
            if (string.IsNullOrWhiteSpace(label)) {
                throw new ArgumentException("Key label must be provided.", nameof(label));
            }

            RectangleF outerBounds = new RectangleF(
                (float)(width * 0.05),
                (float)(height * 0.10),
                (float)(width * 0.90),
                (float)(height * 0.80));
            RectangleF innerBounds = new RectangleF(
                outerBounds.X,
                outerBounds.Y,
                outerBounds.Width,
                (float)(outerBounds.Height * 0.82));
            float cornerRadius = (float)(height * 0.18);

            using GraphicsPath outerPath = CreateRoundedRectanglePath(outerBounds, cornerRadius);
            using SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(64, 30, 36, 48));
            graphics.FillPath(shadowBrush, outerPath);

            using GraphicsPath innerPath = CreateRoundedRectanglePath(innerBounds, cornerRadius);
            using SolidBrush fillBrush = new SolidBrush(KeyFillColor);
            using Pen strokePen = new Pen(KeyStrokeColor, Math.Max(1f, (float)(height * 0.05)));
            graphics.FillPath(fillBrush, innerPath);
            graphics.DrawPath(strokePen, innerPath);

            float fontSize = (float)(height * (label.Length <= 4 ? 0.33 : 0.26));
            using Font font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            using SolidBrush textBrush = new SolidBrush(KeyTextColor);
            using StringFormat stringFormat = new StringFormat();
            stringFormat.Alignment = StringAlignment.Center;
            stringFormat.LineAlignment = StringAlignment.Center;
            RectangleF textBounds = new RectangleF(
                innerBounds.X,
                innerBounds.Y - (float)(height * 0.02),
                innerBounds.Width,
                innerBounds.Height);
            graphics.DrawString(label, font, textBrush, textBounds, stringFormat);
        }

        /// <summary>
        /// Creates a rounded rectangle path for keycap rendering.
        /// </summary>
        /// <param name="bounds">Rectangle bounds for the path.</param>
        /// <param name="cornerRadius">Corner radius in pixels.</param>
        /// <returns>Configured rounded rectangle path.</returns>
        GraphicsPath CreateRoundedRectanglePath(RectangleF bounds, float cornerRadius) {
            if (bounds.Width <= 0f || bounds.Height <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(bounds), "Rounded rectangle bounds must be positive.");
            }

            float diameter = Math.Min(cornerRadius * 2f, Math.Min(bounds.Width, bounds.Height));
            RectangleF arc = new RectangleF(bounds.X, bounds.Y, diameter, diameter);
            GraphicsPath path = new GraphicsPath();
            path.AddArc(arc, 180f, 90f);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270f, 90f);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0f, 90f);
            arc.X = bounds.X;
            path.AddArc(arc, 90f, 90f);
            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// Draws the snap adjustment arrow symbol used by the toolbar buttons.
        /// </summary>
        /// <param name="graphics">Graphics context receiving the symbol.</param>
        /// <param name="iconSize">Square icon size in pixels.</param>
        /// <param name="pointsUp">True for an up arrow; false for a down arrow.</param>
        /// <param name="color">Fill color used by the symbol.</param>
        void DrawSnapArrowSymbol(Graphics graphics, int iconSize, bool pointsUp, Color color) {
            if (graphics == null) {
                throw new ArgumentNullException(nameof(graphics));
            }
            if (iconSize < 16) {
                throw new ArgumentOutOfRangeException(nameof(iconSize), "Icon size must be at least 16 pixels.");
            }

            float centerX = (float)(iconSize * 0.5);
            float triangleWidth = (float)(iconSize * 0.42);
            float triangleHeight = (float)(iconSize * 0.26);
            float stemWidth = Math.Max(2f, (float)(iconSize * 0.12));
            float stemHeight = (float)(iconSize * 0.22);
            float stemTop = pointsUp
                ? (float)(iconSize * 0.42)
                : (float)(iconSize * 0.34);
            float stemLeft = centerX - stemWidth * 0.5f;
            float tipY = pointsUp
                ? (float)(iconSize * 0.18)
                : (float)(iconSize * 0.82);
            float triangleBaseY = pointsUp
                ? tipY + triangleHeight
                : tipY - triangleHeight;
            PointF[] triangle = pointsUp
                ? new[] {
                    new PointF(centerX, tipY),
                    new PointF(centerX - triangleWidth * 0.5f, triangleBaseY),
                    new PointF(centerX + triangleWidth * 0.5f, triangleBaseY)
                }
                : new[] {
                    new PointF(centerX, tipY),
                    new PointF(centerX - triangleWidth * 0.5f, triangleBaseY),
                    new PointF(centerX + triangleWidth * 0.5f, triangleBaseY)
                };
            RectangleF stemBounds = new RectangleF(stemLeft, stemTop, stemWidth, stemHeight);

            using SolidBrush brush = new SolidBrush(color);
            graphics.FillPolygon(brush, triangle);
            graphics.FillRectangle(brush, stemBounds);
        }

        /// <summary>
        /// Draws one colored axis segment.
        /// </summary>
        /// <param name="graphics">Graphics context receiving the line.</param>
        /// <param name="start">Line start point.</param>
        /// <param name="end">Line end point.</param>
        /// <param name="color">Line color.</param>
        /// <param name="strokeWidth">Line width in pixels.</param>
        void DrawAxisLine(Graphics graphics, PointF start, PointF end, Color color, double strokeWidth) {
            if (graphics == null) {
                throw new ArgumentNullException(nameof(graphics));
            }

            using Pen pen = new Pen(color, (float)strokeWidth);
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;
            graphics.DrawLine(pen, start, end);
        }

        /// <summary>
        /// Draws one circular arc with an arrowhead at its end.
        /// </summary>
        /// <param name="graphics">Graphics context receiving the arc.</param>
        /// <param name="centerX">Arc center X position.</param>
        /// <param name="centerY">Arc center Y position.</param>
        /// <param name="radius">Arc radius.</param>
        /// <param name="startAngleDegrees">Arc start angle in degrees.</param>
        /// <param name="sweepAngleDegrees">Arc sweep angle in degrees.</param>
        /// <param name="color">Arc color.</param>
        /// <param name="strokeWidth">Arc line width in pixels.</param>
        /// <param name="headLength">Arrowhead length in pixels.</param>
        /// <param name="headWidth">Arrowhead width in pixels.</param>
        void DrawArcArrow(
            Graphics graphics,
            float centerX,
            float centerY,
            float radius,
            double startAngleDegrees,
            double sweepAngleDegrees,
            Color color,
            double strokeWidth,
            double headLength,
            double headWidth) {
            if (graphics == null) {
                throw new ArgumentNullException(nameof(graphics));
            }

            float diameter = radius * 2f;
            RectangleF arcBounds = new RectangleF(centerX - radius, centerY - radius, diameter, diameter);
            using Pen pen = new Pen(color, (float)strokeWidth);
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;
            graphics.DrawArc(pen, arcBounds, (float)startAngleDegrees, (float)sweepAngleDegrees);

            double endAngleDegrees = startAngleDegrees + sweepAngleDegrees;
            PointF tip = ResolvePointOnCircle(centerX, centerY, radius, endAngleDegrees);
            double tangentAngleDegrees = endAngleDegrees + 90.0;
            PointF[] triangle = CreateOrientedTriangle(tip, tangentAngleDegrees, headLength, headWidth);
            using SolidBrush brush = new SolidBrush(color);
            graphics.FillPolygon(brush, triangle);
        }

        /// <summary>
        /// Fills one arrowhead aligned to an axis segment.
        /// </summary>
        /// <param name="graphics">Graphics context receiving the arrowhead.</param>
        /// <param name="start">Axis start point.</param>
        /// <param name="tip">Arrow tip point.</param>
        /// <param name="color">Arrowhead fill color.</param>
        /// <param name="headLength">Arrowhead length in pixels.</param>
        /// <param name="headWidth">Arrowhead width in pixels.</param>
        void FillArrowHead(Graphics graphics, PointF start, PointF tip, Color color, double headLength, double headWidth) {
            if (graphics == null) {
                throw new ArgumentNullException(nameof(graphics));
            }

            double angleDegrees = Math.Atan2(tip.Y - start.Y, tip.X - start.X) * (180.0 / Math.PI);
            PointF[] triangle = CreateOrientedTriangle(tip, angleDegrees, headLength, headWidth);
            using SolidBrush brush = new SolidBrush(color);
            graphics.FillPolygon(brush, triangle);
        }

        /// <summary>
        /// Fills one centered square used by the scale icon.
        /// </summary>
        /// <param name="graphics">Graphics context receiving the square.</param>
        /// <param name="center">Square center point.</param>
        /// <param name="sideLength">Square side length in pixels.</param>
        /// <param name="color">Square fill color.</param>
        void FillSquare(Graphics graphics, PointF center, double sideLength, Color color) {
            if (graphics == null) {
                throw new ArgumentNullException(nameof(graphics));
            }

            float halfSide = (float)(sideLength * 0.5);
            RectangleF bounds = new RectangleF(center.X - halfSide, center.Y - halfSide, (float)sideLength, (float)sideLength);
            using SolidBrush brush = new SolidBrush(color);
            graphics.FillRectangle(brush, bounds);
        }

        /// <summary>
        /// Fills one circle used for the icon center joint.
        /// </summary>
        /// <param name="graphics">Graphics context receiving the circle.</param>
        /// <param name="center">Circle center point.</param>
        /// <param name="radius">Circle radius in pixels.</param>
        /// <param name="color">Circle fill color.</param>
        void FillCircle(Graphics graphics, PointF center, double radius, Color color) {
            if (graphics == null) {
                throw new ArgumentNullException(nameof(graphics));
            }

            float diameter = (float)(radius * 2.0);
            RectangleF bounds = new RectangleF(center.X - (float)radius, center.Y - (float)radius, diameter, diameter);
            using SolidBrush brush = new SolidBrush(color);
            graphics.FillEllipse(brush, bounds);
        }

        /// <summary>
        /// Creates an oriented triangle used by arrowheads.
        /// </summary>
        /// <param name="tip">Triangle tip point.</param>
        /// <param name="angleDegrees">Direction angle in degrees.</param>
        /// <param name="headLength">Triangle length in pixels.</param>
        /// <param name="headWidth">Triangle width in pixels.</param>
        /// <returns>Triangle vertices in drawing order.</returns>
        PointF[] CreateOrientedTriangle(PointF tip, double angleDegrees, double headLength, double headWidth) {
            double angleRadians = angleDegrees * (Math.PI / 180.0);
            double baseX = tip.X - Math.Cos(angleRadians) * headLength;
            double baseY = tip.Y - Math.Sin(angleRadians) * headLength;
            double perpendicularX = -Math.Sin(angleRadians);
            double perpendicularY = Math.Cos(angleRadians);
            double halfWidth = headWidth * 0.5;
            return new[] {
                new PointF(tip.X, tip.Y),
                new PointF(
                    (float)(baseX + perpendicularX * halfWidth),
                    (float)(baseY + perpendicularY * halfWidth)),
                new PointF(
                    (float)(baseX - perpendicularX * halfWidth),
                    (float)(baseY - perpendicularY * halfWidth))
            };
        }

        /// <summary>
        /// Resolves a point on a circle using screen-space coordinates.
        /// </summary>
        /// <param name="centerX">Circle center X position.</param>
        /// <param name="centerY">Circle center Y position.</param>
        /// <param name="radius">Circle radius.</param>
        /// <param name="angleDegrees">Angle in degrees.</param>
        /// <returns>Resolved point on the circle.</returns>
        PointF ResolvePointOnCircle(float centerX, float centerY, float radius, double angleDegrees) {
            double angleRadians = angleDegrees * (Math.PI / 180.0);
            return new PointF(
                (float)(centerX + Math.Cos(angleRadians) * radius),
                (float)(centerY + Math.Sin(angleRadians) * radius));
        }

        /// <summary>
        /// Saves a rendered bitmap to disk as a PNG file.
        /// </summary>
        /// <param name="bitmap">Bitmap to save.</param>
        /// <param name="filePath">Destination file path.</param>
        void SaveBitmap(Bitmap bitmap, string filePath) {
            if (bitmap == null) {
                throw new ArgumentNullException(nameof(bitmap));
            }

            if (string.IsNullOrWhiteSpace(filePath)) {
                throw new ArgumentException("File path must be provided.", nameof(filePath));
            }

            string directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(directory)) {
                throw new InvalidOperationException("File path must include a directory.");
            }

            Directory.CreateDirectory(directory);
            bitmap.Save(filePath, ImageFormat.Png);
        }
    }
}
