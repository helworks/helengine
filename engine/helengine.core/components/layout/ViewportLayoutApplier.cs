namespace helengine {
    /// <summary>Applies resolved factors directly, without allocating or retaining original UI values.</summary>
    public static class ViewportLayoutApplier {
        /// <summary>Transforms one local position; callers use a zero origin for non-root entities.</summary>
        /// <param name="authoredPosition">Position before layout processing.</param>
        /// <param name="metrics">Resolved factors and optional root displacement.</param>
        /// <returns>Local position in prepared layout space.</returns>
        public static float3 ResolvePosition(float3 authoredPosition, in ViewportLayoutMetrics metrics) {
            return LayoutNumericKernel<RealEvaluation, RuntimeNumericOperations>.Position(authoredPosition, metrics, new RuntimeNumericOperations());
        }

        /// <summary>Transforms a detached authoring position under the selected runtime contract.</summary>
        public static float3 ResolvePosition(float3 authoredPosition, in ViewportLayoutMetrics metrics, NumericPrecisionPolicy policy) {
            return LayoutNumericKernel<double, CookNumericOperations>.Position(authoredPosition, metrics, new CookNumericOperations(policy));
        }

        /// <summary>Scales a detached dimension with the target arithmetic, including integer-to-evaluation conversion.</summary>
        public static int ScaleInteger(int value, double scale, NumericPrecisionPolicy policy) {
            CookNumericOperations math = new CookNumericOperations(policy);
            return LayoutNumericKernel<double, CookNumericOperations>.ScaleInteger(value, math.Number(scale), math);
        }

        /// <summary>Scales the fields owned by one real participant, including detached cook participants.</summary>
        /// <param name="component">Live or detached UI component.</param>
        /// <param name="metrics">Shared layout factors.</param>
        public static void ApplyComponent(Component component, in ViewportLayoutMetrics metrics) {
            ApplyComponentWithMath<RealEvaluation, RuntimeNumericOperations>(component, metrics, new RuntimeNumericOperations());
        }

        /// <summary>Applies the same participant formula under an explicit cook numeric policy.</summary>
        public static void ApplyComponent(Component component, in ViewportLayoutMetrics metrics, NumericPrecisionPolicy policy) {
            ApplyComponentWithMath<double, CookNumericOperations>(component, metrics, new CookNumericOperations(policy));
        }

        /// <summary>Specializes participant preparation for runtime or cook arithmetic.</summary>
        static void ApplyComponentWithMath<T, TMath>(Component component, in ViewportLayoutMetrics metrics, TMath math) where TMath : struct, INumericOperations<T> {
            if (component is LayoutComponent anchors) {
                float4 distances = anchors.AnchorDistances;
                anchors.AnchorDistances = new float4(LayoutNumericKernel<T, TMath>.Scale(distances.X, metrics.ScaleX, math), LayoutNumericKernel<T, TMath>.Scale(distances.Y, metrics.ScaleX, math),
                    LayoutNumericKernel<T, TMath>.Scale(distances.Z, metrics.ScaleY, math), LayoutNumericKernel<T, TMath>.Scale(distances.W, metrics.ScaleY, math));
            } else if (component is RoundedRectComponent rectangle) {
                rectangle.Size = LayoutNumericKernel<T, TMath>.ScaleSize(rectangle.Size, math.Read(metrics.ScaleX), math.Read(metrics.ScaleY), math);
                rectangle.Radius = LayoutNumericKernel<T, TMath>.Scale(rectangle.Radius, metrics.GlyphScale, math);
                rectangle.BorderThickness = LayoutNumericKernel<T, TMath>.Scale(rectangle.BorderThickness, metrics.GlyphScale, math);
            } else if (component is TextComponent text) {
                text.Size = LayoutNumericKernel<T, TMath>.ScaleSize(text.Size, math.Read(metrics.ScaleX), math.Read(metrics.ScaleY), math);
                text.FontScale = text.NativeFontPixels == TextNativePixelMode.Disabled ? LayoutNumericKernel<T, TMath>.Scale(text.FontScale, metrics.GlyphScale, math) : 1f;
                T outline = math.Multiply(math.Read(text.OutlineScale), math.Read(metrics.GlyphScale));
                float2 shadow = new float2(LayoutNumericKernel<T, TMath>.Scale(text.ShadowOffset.X, metrics.GlyphScale, math), LayoutNumericKernel<T, TMath>.Scale(text.ShadowOffset.Y, metrics.GlyphScale, math));
                if (text.NativeFontPixels != TextNativePixelMode.Disabled) {
                    outline = math.SnapMagnitude(outline, text.NativeFontPixels);
                    shadow = new float2(LayoutNumericKernel<T, TMath>.SnapShadowAxis(shadow.X, text.NativeFontPixels, math), LayoutNumericKernel<T, TMath>.SnapShadowAxis(shadow.Y, text.NativeFontPixels, math));
                }
                text.OutlineScale = math.Write(outline);
                text.ShadowOffset = shadow;
            } else if (component is SpriteComponent sprite) {
                sprite.Size = LayoutNumericKernel<T, TMath>.ScaleSize(sprite.Size, math.Read(metrics.ScaleX), math.Read(metrics.ScaleY), math);
            } else if (component is ClipRectComponent clip) {
                clip.Size = LayoutNumericKernel<T, TMath>.ScaleSize(clip.Size, math.Read(metrics.ScaleX), math.Read(metrics.ScaleY), math);
            } else if (component is InteractableComponent interactable) {
                interactable.Size = LayoutNumericKernel<T, TMath>.ScaleSize(interactable.Size, math.Read(metrics.ScaleX), math.Read(metrics.ScaleY), math);
            } else if (component is ScrollComponent scroll) {
                scroll.Size = LayoutNumericKernel<T, TMath>.ScaleSize(scroll.Size, math.Read(metrics.ScaleX), math.Read(metrics.ScaleY), math);
                scroll.ItemExtent = LayoutNumericKernel<T, TMath>.ScaleInteger(scroll.ItemExtent, math.Read(metrics.GlyphScale), math);
            } else if (component is FPSComponent fps) {
                fps.FontScale = fps.NativeFontPixels == TextNativePixelMode.Disabled ? LayoutNumericKernel<T, TMath>.Scale(fps.FontScale, metrics.GlyphScale, math) : 1f;
                fps.Padding = LayoutNumericKernel<T, TMath>.ScaleSize(fps.Padding, math.Read(metrics.ScaleX), math.Read(metrics.ScaleY), math);
            }
        }

        /// <summary>Writes anchor-resolved bounds to the same supported hosts as live LayoutComponent.</summary>
        /// <param name="component">Detached or live sized participant.</param>
        /// <param name="resolvedSize">Final anchor-resolved dimensions.</param>
        public static void ApplyResolvedSize(Component component, int2 resolvedSize) {
            if (component is RoundedRectComponent roundedRectComponent) {
                roundedRectComponent.Size = resolvedSize;
            } else if (component is SpriteComponent spriteComponent) {
                spriteComponent.Size = resolvedSize;
            } else if (component is TextComponent textComponent) {
                textComponent.Size = resolvedSize;
            } else if (component is ClipRectComponent clipRectComponent) {
                clipRectComponent.Size = resolvedSize;
            } else if (component is InteractableComponent interactableComponent) {
                interactableComponent.Size = resolvedSize;
            } else if (component is ScrollComponent scrollComponent) {
                scrollComponent.Size = resolvedSize;
            } else if (component is ComboBoxComponent comboBoxComponent) {
                comboBoxComponent.Size = resolvedSize;
            } else if (component is TextBoxComponent textBoxComponent) {
                textBoxComponent.Size = resolvedSize;
            } else if (component is CheckBoxComponent checkBoxComponent) {
                checkBoxComponent.Size = resolvedSize;
            }
        }

        /// <summary>Preserves zero and the established minimum-one rule for nonzero authored dimensions.</summary>
        /// <param name="value">Authored dimension.</param>
        /// <param name="scale">Resolved multiplier.</param>
        /// <returns>Prepared integer dimension.</returns>
        public static int ScaleInteger(int value, RealEvaluation scale) {
            return LayoutNumericKernel<RealEvaluation, RuntimeNumericOperations>.ScaleInteger(value, scale, new RuntimeNumericOperations());
        }

        /// <summary>Scales detached dimensions using the selected target contract.</summary>
        public static int2 ScaleSize(int2 size, Real scaleX, Real scaleY, NumericPrecisionPolicy policy) {
            CookNumericOperations math = new CookNumericOperations(policy);
            return LayoutNumericKernel<double, CookNumericOperations>.ScaleSize(size, math.Read(scaleX), math.Read(scaleY), math);
        }

        /// <summary>Applies independent axis factors using existing integer rounding.</summary>
        /// <param name="size">Authored component size.</param>
        /// <param name="scaleX">Horizontal multiplier.</param>
        /// <param name="scaleY">Vertical multiplier.</param>
        /// <returns>Prepared size.</returns>
        public static int2 ScaleSize(int2 size, RealEvaluation scaleX, RealEvaluation scaleY) {
            return new int2(ScaleInteger(size.X, scaleX), ScaleInteger(size.Y, scaleY));
        }
    }
}
