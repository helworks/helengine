namespace helengine {
    /// <summary>Single formula source specialized for compiled runtime arithmetic or explicit per-request cook arithmetic.</summary>
    public static class LayoutNumericKernel<T, TMath> where TMath : struct, INumericOperations<T> {
        /// <summary>Resolves display-space fitting at the selected arithmetic precision.</summary>
        public static int2 Fit(T liveWidth, T liveHeight, int referenceWidth, int referenceHeight, T pixelAspect, TMath math) {
            if (referenceWidth < 1 || referenceHeight < 1) { throw new ArgumentOutOfRangeException(nameof(referenceWidth), "Reference canvas dimensions must be positive."); }
            if (!math.IsFinite(pixelAspect) || math.LessOrEqual(pixelAspect, math.Number(0d))) { throw new ArgumentOutOfRangeException(nameof(pixelAspect), "The pixel aspect must be finite and positive."); }
            T rw = math.Number(referenceWidth);
            T rh = math.Number(referenceHeight);
            T expectedWidth = math.Divide(math.Multiply(liveHeight, rw), math.Multiply(rh, pixelAspect));
            T expectedHeight = math.Divide(math.Multiply(math.Multiply(liveWidth, pixelAspect), rh), rw);
            if (math.LessOrEqual(math.Abs(math.Subtract(liveWidth, expectedWidth)), math.Number(0.5d))
                || math.LessOrEqual(math.Abs(math.Subtract(liveHeight, expectedHeight)), math.Number(0.5d))) {
                return new int2(math.RoundToInt(liveWidth), math.RoundToInt(liveHeight));
            }
            T scale = math.Min(math.Divide(math.Multiply(liveWidth, pixelAspect), rw), math.Divide(liveHeight, rh));
            if (math.LessOrEqual(scale, math.Number(0d))) { return new int2(referenceWidth, referenceHeight); }
            return new int2(Math.Max(1, math.RoundToInt(math.Divide(math.Multiply(rw, scale), pixelAspect))), Math.Max(1, math.RoundToInt(math.Multiply(rh, scale))));
        }

        /// <summary>Resolves owner factors, rounding each stored metrics field before subsequent reads.</summary>
        public static ViewportLayoutMetrics Resolve(int scalingMode, int2 referenceSize, int2 targetSize, T pixelAspect, T uiScale, T dpi, T referenceDpi, TMath math) {
            if (targetSize.X <= 0 || targetSize.Y <= 0 || referenceSize.X <= 0 || referenceSize.Y <= 0) { throw new InvalidOperationException("Viewport layout requires positive reference and target dimensions."); }
            if (!math.IsFinite(pixelAspect) || math.LessOrEqual(pixelAspect, math.Number(0d))) { throw new InvalidOperationException("Viewport layout requires a finite positive pixel aspect."); }
            ViewportLayoutMetrics result = new ViewportLayoutMetrics { ScaleX = math.Write(math.Number(1d)), ScaleY = math.Write(math.Number(1d)), GlyphScale = math.Write(math.Number(1d)), Origin = new float2(0d, 0d), AnchorSize = targetSize, PixelAspect = math.Write(pixelAspect) };
            if (scalingMode == ViewportComponent.NoScalingMode) { return result; }
            if (scalingMode == ViewportComponent.DensityScalingMode) {
                if (!math.IsFinite(dpi) || math.LessOrEqual(dpi, math.Number(0d)) || !math.IsFinite(referenceDpi) || math.LessOrEqual(referenceDpi, math.Number(0d)) || !math.IsFinite(uiScale) || math.LessOrEqual(uiScale, math.Number(0d))) { throw new InvalidOperationException("Density layout requires finite positive output DPI, reference DPI and UI scale."); }
                result.ScaleX = math.Write(math.Multiply(math.Divide(dpi, referenceDpi), uiScale));
                result.ScaleY = math.Write(math.Read(result.ScaleX));
                result.GlyphScale = math.Write(math.Read(result.ScaleX));
                return result;
            }
            if (scalingMode == ViewportComponent.ReferenceCanvasScalingMode) {
                result.AnchorSize = Fit(math.Number(targetSize.X), math.Number(targetSize.Y), referenceSize.X, referenceSize.Y, pixelAspect, math);
                result.Origin = new float2(math.Write(math.Multiply(math.Number(targetSize.X - result.AnchorSize.X), math.Number(0.5d))), math.Write(math.Multiply(math.Number(targetSize.Y - result.AnchorSize.Y), math.Number(0.5d))));
            } else if (scalingMode != ViewportComponent.ReferenceCanvasStretchScalingMode) { throw new InvalidOperationException("Unknown viewport scaling mode."); }
            result.ScaleX = math.Write(math.Divide(math.Number(result.AnchorSize.X), math.Number(referenceSize.X)));
            result.ScaleY = math.Write(math.Divide(math.Number(result.AnchorSize.Y), math.Number(referenceSize.Y)));
            result.GlyphScale = math.Write(scalingMode == ViewportComponent.ReferenceCanvasStretchScalingMode ? math.Read(result.ScaleY) : math.Min(math.Multiply(math.Read(result.ScaleX), pixelAspect), math.Read(result.ScaleY)));
            return result;
        }

        /// <summary>Transforms one position while rounding only at the stored result.</summary>
        public static float3 Position(float3 position, in ViewportLayoutMetrics metrics, TMath math) {
            return new float3(math.Write(math.Add(math.Multiply(math.Read(position.X), math.Read(metrics.ScaleX)), math.Read(metrics.Origin.X))), math.Write(math.Add(math.Multiply(math.Read(position.Y), math.Read(metrics.ScaleY)), math.Read(metrics.Origin.Y))), math.Write(math.Read(position.Z)));
        }

        /// <summary>Preserves zero and the minimum-one rule for nonzero dimensions.</summary>
        public static int ScaleInteger(int value, T scale, TMath math) { return value == 0 ? 0 : Math.Max(1, math.RoundToInt(math.Multiply(math.Number(value), scale))); }

        /// <summary>Scales a pair of integer dimensions.</summary>
        public static int2 ScaleSize(int2 size, T scaleX, T scaleY, TMath math) { return new int2(ScaleInteger(size.X, scaleX, math), ScaleInteger(size.Y, scaleY, math)); }

        /// <summary>Scales a stored scalar and commits it once to storage.</summary>
        public static Real Scale(Real value, Real scale, TMath math) { return math.Write(math.Multiply(math.Read(value), math.Read(scale))); }

        /// <summary>Snaps a stored shadow axis with native-pixel sign and nonzero minimum distance preserved.</summary>
        public static Real SnapShadowAxis(Real storedValue, TextNativePixelMode mode, TMath math) {
            T value = math.Read(storedValue);
            T zero = math.Number(0d);
            if (math.LessOrEqual(value, zero) && math.LessOrEqual(zero, value)) { return math.Write(zero); }
            T magnitude = math.SnapMagnitude(math.Abs(value), mode);
            return math.Write(math.LessOrEqual(value, zero) ? math.Subtract(zero, magnitude) : magnitude);
        }

        /// <summary>Resolves a normalized or pixel camera rectangle in the same arithmetic as subsequent layout.</summary>
        public static float4 CameraViewport(float4 viewport, T targetWidth, T targetHeight, TMath math) {
            T zero = math.Number(0d);
            if (math.LessOrEqual(targetWidth, zero) || math.LessOrEqual(targetHeight, zero)) { throw new ArgumentOutOfRangeException(nameof(targetWidth), "Target dimensions must be positive."); }
            T x = math.Read(viewport.X);
            T y = math.Read(viewport.Y);
            T width = math.Read(viewport.Z);
            T height = math.Read(viewport.W);
            if (math.LessOrEqual(width, math.Number(1d)) && math.LessOrEqual(height, math.Number(1d))) {
                x = math.Multiply(x, targetWidth);
                width = math.Multiply(width, targetWidth);
                bool stacked = math.LessOrEqual(math.Abs(math.Subtract(targetHeight, math.Multiply(targetWidth, math.Number(1.5d)))), math.Number(0.5d))
                    && math.LessOrEqual(zero, math.Read(viewport.Y)) && math.LessOrEqual(math.Add(math.Read(viewport.Y), math.Read(viewport.W)), math.Number(2d));
                T vertical = stacked ? math.Multiply(targetHeight, math.Number(0.5d)) : targetHeight;
                y = math.Multiply(y, vertical);
                height = math.Multiply(height, vertical);
            }
            return new float4(math.Write(x), math.Write(y), math.Write(width), math.Write(height));
        }

        /// <summary>Resolves anchors against the same stored distances and arithmetic widths as the player.</summary>
        public static int2 Anchors(byte flags, float4 distances, int2 spaceSize, float2 origin, int2 currentSize, ref float3 position, TMath math) {
            int width = currentSize.X;
            int height = currentSize.Y;
            bool left = (flags & LayoutComponent.LeftAnchorFlag) != 0;
            bool right = (flags & LayoutComponent.RightAnchorFlag) != 0;
            bool top = (flags & LayoutComponent.TopAnchorFlag) != 0;
            bool bottom = (flags & LayoutComponent.BottomAnchorFlag) != 0;
            if (left && right) { width = Math.Max(0, math.RoundToInt(math.Subtract(math.Subtract(math.Number(spaceSize.X), math.Read(distances.X)), math.Read(distances.Y)))); }
            if (left) { position.X = math.Write(math.Add(math.Read(origin.X), math.Read(distances.X))); }
            else if (right) { position.X = math.Write(math.Subtract(math.Subtract(math.Add(math.Read(origin.X), math.Number(spaceSize.X)), math.Read(distances.Y)), math.Number(currentSize.X))); }
            if (top && bottom) { height = Math.Max(0, math.RoundToInt(math.Subtract(math.Subtract(math.Number(spaceSize.Y), math.Read(distances.Z)), math.Read(distances.W)))); }
            if (top) { position.Y = math.Write(math.Add(math.Read(origin.Y), math.Read(distances.Z))); }
            else if (bottom) { position.Y = math.Write(math.Subtract(math.Subtract(math.Add(math.Read(origin.Y), math.Number(spaceSize.Y)), math.Read(distances.W)), math.Number(currentSize.Y))); }
            return new int2(width, height);
        }
    }
}
