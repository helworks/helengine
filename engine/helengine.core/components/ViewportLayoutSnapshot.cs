namespace helengine {
    /// <summary>
    /// Stores the authored layout state for one viewport-scaled entity subtree node so viewport-owned scaling can be reapplied without cumulative drift.
    /// </summary>
    public sealed class ViewportLayoutSnapshot {
        /// <summary>
        /// Initializes one snapshot for the supplied entity and any supported attached layout components.
        /// </summary>
        /// <param name="entity">Entity whose authored state should be preserved.</param>
        /// <param name="isRootEntity">True when the entity is the root of the scaled subtree.</param>
        public ViewportLayoutSnapshot(Entity entity, bool isRootEntity) {
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
            IsRootEntity = isRootEntity;
            LocalPosition = entity.LocalPosition;

            TrackedFpsComponent = FindFpsComponent(entity);
            if (TrackedFpsComponent != null) {
                FpsFontScale = TrackedFpsComponent.FontScale;
                FpsPadding = TrackedFpsComponent.Padding;
            }

            TrackedLayoutComponent = FindLayoutComponent(entity);
            if (TrackedLayoutComponent != null) {
                AnchorDistances = TrackedLayoutComponent.AnchorDistances;
            }

            TrackedRoundedRectComponent = FindRoundedRectComponent(entity);
            if (TrackedRoundedRectComponent != null) {
                RoundedRectSize = TrackedRoundedRectComponent.Size;
                RoundedRectRadius = TrackedRoundedRectComponent.Radius;
                RoundedRectBorderThickness = TrackedRoundedRectComponent.BorderThickness;
            }

            TrackedTextComponent = FindTextComponent(entity);
            if (TrackedTextComponent != null) {
                TextSize = TrackedTextComponent.Size;
                TextFontScale = TrackedTextComponent.FontScale;
                TextOutlineScale = TrackedTextComponent.OutlineScale;
                TextShadowOffset = TrackedTextComponent.ShadowOffset;
            }

            TrackedSpriteComponent = FindSpriteComponent(entity);
            if (TrackedSpriteComponent != null) {
                SpriteSize = TrackedSpriteComponent.Size;
            }

            TrackedClipRectComponent = FindClipRectComponent(entity);
            if (TrackedClipRectComponent != null) {
                ClipRectSize = TrackedClipRectComponent.Size;
            }

            TrackedInteractableComponent = FindInteractableComponent(entity);
            if (TrackedInteractableComponent != null) {
                InteractableSize = TrackedInteractableComponent.Size;
            }

            TrackedScrollComponent = FindScrollComponent(entity);
            if (TrackedScrollComponent != null) {
                ScrollSize = TrackedScrollComponent.Size;
                ScrollItemExtent = TrackedScrollComponent.ItemExtent;
            }
        }

        /// <summary>
        /// Gets the entity whose authored state is represented by the snapshot.
        /// </summary>
        public Entity Entity { get; }

        /// <summary>
        /// Gets a value indicating whether the snapshot belongs to the root entity of the scaled subtree.
        /// </summary>
        public bool IsRootEntity { get; }

        /// <summary>
        /// Gets the authored local position captured for the entity.
        /// </summary>
        public float3 LocalPosition { get; }

        /// <summary>Gets the performance overlay that owns its generated text subtree, when attached.</summary>
        public FPSComponent TrackedFpsComponent { get; }

        /// <summary>Gets the authored font scale propagated by the performance overlay to its generated rows.</summary>
        public float FpsFontScale { get; }

        /// <summary>Gets the authored performance overlay margin before density conversion.</summary>
        public int2 FpsPadding { get; }

        /// <summary>
        /// Gets the attached anchor component when one exists.
        /// </summary>
        public LayoutComponent TrackedLayoutComponent { get; }

        /// <summary>
        /// Gets the authored anchor distances captured from the anchor component.
        /// </summary>
        public float4 AnchorDistances { get; }

        /// <summary>
        /// Gets the attached rounded-rectangle component when one exists.
        /// </summary>
        public RoundedRectComponent TrackedRoundedRectComponent { get; }

        /// <summary>
        /// Gets the authored rounded-rectangle size.
        /// </summary>
        public int2 RoundedRectSize { get; }

        /// <summary>
        /// Gets the authored rounded-rectangle corner radius.
        /// </summary>
        public float RoundedRectRadius { get; }

        /// <summary>
        /// Gets the authored rounded-rectangle border thickness.
        /// </summary>
        public float RoundedRectBorderThickness { get; }

        /// <summary>
        /// Gets the attached text component when one exists.
        /// </summary>
        public TextComponent TrackedTextComponent { get; }

        /// <summary>
        /// Gets the authored text layout size.
        /// </summary>
        public int2 TextSize { get; }

        /// <summary>
        /// Gets the authored glyph scale captured from the text component.
        /// </summary>
        public float TextFontScale { get; }

        /// <summary>
        /// Gets the authored outline thickness so viewport resizing preserves its proportion to the glyphs.
        /// </summary>
        public float TextOutlineScale { get; }

        /// <summary>
        /// Gets the authored shadow displacement before viewport scaling is applied.
        /// </summary>
        public float2 TextShadowOffset { get; }

        /// <summary>
        /// Gets the attached sprite component when one exists.
        /// </summary>
        public SpriteComponent TrackedSpriteComponent { get; }

        /// <summary>
        /// Gets the authored sprite size.
        /// </summary>
        public int2 SpriteSize { get; }

        /// <summary>
        /// Gets the attached clip-rectangle component when one exists.
        /// </summary>
        public ClipRectComponent TrackedClipRectComponent { get; }

        /// <summary>
        /// Gets the authored clip-rectangle size.
        /// </summary>
        public int2 ClipRectSize { get; }

        /// <summary>
        /// Gets the attached interactable component when one exists.
        /// </summary>
        public InteractableComponent TrackedInteractableComponent { get; }

        /// <summary>
        /// Gets the authored interactable region size.
        /// </summary>
        public int2 InteractableSize { get; }

        /// <summary>
        /// Gets the attached scroll component when one exists.
        /// </summary>
        public ScrollComponent TrackedScrollComponent { get; }

        /// <summary>
        /// Gets the authored scroll viewport size.
        /// </summary>
        public int2 ScrollSize { get; }

        /// <summary>
        /// Gets the authored scroll item extent.
        /// </summary>
        public int ScrollItemExtent { get; }

        /// <summary>
        /// Applies one absolute viewport-driven scale to the captured layout. Glyphs and their effects follow the canvas height when stretched and the uniform canvas scale otherwise.
        /// </summary>
        /// <param name="anchorSpace">Anchor space resolved for the live viewport.</param>
        /// <param name="canvasOrigin">Origin applied to the scaled subtree root.</param>
        /// <param name="referenceWidth">Authored reference canvas width.</param>
        /// <param name="referenceHeight">Authored reference canvas height.</param>
        /// <param name="stretchCanvas">True when the viewport fills the live bounds with independent axis scales.</param>
        public void Apply(AnchorSpace anchorSpace, float2 canvasOrigin, int referenceWidth, int referenceHeight, bool stretchCanvas) {
            double widthScale = referenceWidth > 0 ? (double)anchorSpace.Size.X / referenceWidth : 1d;
            double heightScale = referenceHeight > 0 ? (double)anchorSpace.Size.Y / referenceHeight : 1d;
            double scalarScale = stretchCanvas ? heightScale : Math.Min(widthScale, heightScale);
            ApplyScale(canvasOrigin, widthScale, heightScale, scalarScale);
        }

        /// <summary>Applies a uniform physical-density scale from authored values, including layout bounds used by pointer hit tests.</summary>
        /// <param name="scale">Physical pixels per authored viewport pixel.</param>
        public void ApplyDensityScale(double scale) {
            ApplyScale(new float2(0f, 0f), scale, scale, scale);
        }

        /// <summary>Restores each tracked layout field from its authored snapshot and applies the requested axis and glyph scales.</summary>
        /// <param name="canvasOrigin">Root displacement within the physical viewport.</param>
        /// <param name="widthScale">Horizontal pixels per authored pixel.</param>
        /// <param name="heightScale">Vertical pixels per authored pixel.</param>
        /// <param name="scalarScale">Uniform glyph and decoration scale.</param>
        void ApplyScale(float2 canvasOrigin, double widthScale, double heightScale, double scalarScale) {
            float positionX = ScaleFloat(LocalPosition.X, widthScale);
            float positionY = ScaleFloat(LocalPosition.Y, heightScale);
            if (IsRootEntity) {
                positionX += canvasOrigin.X;
                positionY += canvasOrigin.Y;
            }

            Entity.LocalPosition = new float3(positionX, positionY, LocalPosition.Z);

            if (TrackedFpsComponent != null) {
                TrackedFpsComponent.FontScale = TrackedFpsComponent.NativeFontPixels != TextNativePixelMode.Disabled
                    ? 1f : ScaleFloat(FpsFontScale, scalarScale);
                TrackedFpsComponent.Padding = ScaleInt2(FpsPadding, widthScale, heightScale);
            }

            if (TrackedLayoutComponent != null) {
                TrackedLayoutComponent.AnchorDistances = new float4(
                    ScaleFloat(AnchorDistances.X, widthScale),
                    ScaleFloat(AnchorDistances.Y, widthScale),
                    ScaleFloat(AnchorDistances.Z, heightScale),
                    ScaleFloat(AnchorDistances.W, heightScale));
            }

            if (TrackedRoundedRectComponent != null) {
                TrackedRoundedRectComponent.Size = ScaleInt2(RoundedRectSize, widthScale, heightScale);
                TrackedRoundedRectComponent.Radius = ScaleFloat(RoundedRectRadius, scalarScale);
                TrackedRoundedRectComponent.BorderThickness = ScaleFloat(RoundedRectBorderThickness, scalarScale);
            }

            if (TrackedTextComponent != null) {
                TrackedTextComponent.Size = ScaleInt2(TextSize, widthScale, heightScale);
                TrackedTextComponent.FontScale = TrackedTextComponent.NativeFontPixels != TextNativePixelMode.Disabled ? 1f : ScaleFloat(TextFontScale, scalarScale);
                float outlineScale = ScaleFloat(TextOutlineScale, scalarScale);
                float2 shadowOffset = new float2(
                    ScaleFloat(TextShadowOffset.X, scalarScale),
                    ScaleFloat(TextShadowOffset.Y, scalarScale));
                TextNativePixelMode nativePixels = TrackedTextComponent.NativeFontPixels;
                if (nativePixels != TextNativePixelMode.Disabled) {
                    outlineScale = TextNativePixelEffectUtils.SnapOutlineScale(outlineScale, nativePixels);
                    shadowOffset = TextNativePixelEffectUtils.SnapShadowOffset(shadowOffset, nativePixels);
                }

                TrackedTextComponent.OutlineScale = outlineScale;
                TrackedTextComponent.ShadowOffset = shadowOffset;
            }

            if (TrackedSpriteComponent != null) {
                TrackedSpriteComponent.Size = ScaleInt2(SpriteSize, widthScale, heightScale);
            }

            if (TrackedClipRectComponent != null) {
                TrackedClipRectComponent.Size = ScaleInt2(ClipRectSize, widthScale, heightScale);
            }

            if (TrackedInteractableComponent != null) {
                TrackedInteractableComponent.Size = ScaleInt2(InteractableSize, widthScale, heightScale);
            }

            if (TrackedScrollComponent != null) {
                TrackedScrollComponent.Size = ScaleInt2(ScrollSize, widthScale, heightScale);
                TrackedScrollComponent.ItemExtent = ScaleInt(ScrollItemExtent, scalarScale);
            }
        }

        /// <summary>
        /// Refreshes anchored layout after the scaled bounds and distances have been applied.
        /// </summary>
        public void RefreshAnchoring() {
            if (TrackedLayoutComponent != null) {
                TrackedLayoutComponent.RefreshAnchoring();
            }
        }

        /// <summary>
        /// Writes the authored component values back onto the tracked components so the subtree holds unscaled layout data.
        /// A live value that differs from the value recorded right after the last scale pass is treated as an edit made in the
        /// scaled preview and is mapped back into authored space instead of being discarded. Entity positions are restored
        /// separately by <see cref="RestoreAuthoredPosition"/> so no component write can move them afterwards.
        /// </summary>
        /// <param name="applied">Live values recorded right after the last scale pass, or null when no edits should be detected.</param>
        /// <param name="widthScale">Horizontal pixels per authored pixel used by the last scale pass.</param>
        /// <param name="heightScale">Vertical pixels per authored pixel used by the last scale pass.</param>
        /// <param name="scalarScale">Uniform glyph and decoration scale used by the last scale pass.</param>
        public void RestoreAuthoredComponents(ViewportLayoutSnapshot applied, double widthScale, double heightScale, double scalarScale) {
            bool hasApplied = applied != null;
            if (TrackedFpsComponent != null) {
                double fpsFontScale = TrackedFpsComponent.NativeFontPixels != TextNativePixelMode.Disabled ? 1d : scalarScale;
                TrackedFpsComponent.FontScale = UnscaleFloat(TrackedFpsComponent.FontScale, hasApplied ? applied.FpsFontScale : 0f, hasApplied, FpsFontScale, fpsFontScale);
                TrackedFpsComponent.Padding = UnscaleInt2(TrackedFpsComponent.Padding, hasApplied ? applied.FpsPadding : new int2(0, 0), hasApplied, FpsPadding, widthScale, heightScale);
            }

            if (TrackedLayoutComponent != null) {
                float4 live = TrackedLayoutComponent.AnchorDistances;
                float4 appliedDistances = hasApplied ? applied.AnchorDistances : new float4(0f, 0f, 0f, 0f);
                TrackedLayoutComponent.AnchorDistances = new float4(
                    UnscaleFloat(live.X, appliedDistances.X, hasApplied, AnchorDistances.X, widthScale),
                    UnscaleFloat(live.Y, appliedDistances.Y, hasApplied, AnchorDistances.Y, widthScale),
                    UnscaleFloat(live.Z, appliedDistances.Z, hasApplied, AnchorDistances.Z, heightScale),
                    UnscaleFloat(live.W, appliedDistances.W, hasApplied, AnchorDistances.W, heightScale));
            }

            if (TrackedRoundedRectComponent != null) {
                TrackedRoundedRectComponent.Size = UnscaleInt2(TrackedRoundedRectComponent.Size, hasApplied ? applied.RoundedRectSize : new int2(0, 0), hasApplied, RoundedRectSize, widthScale, heightScale);
                TrackedRoundedRectComponent.Radius = UnscaleFloat(TrackedRoundedRectComponent.Radius, hasApplied ? applied.RoundedRectRadius : 0f, hasApplied, RoundedRectRadius, scalarScale);
                TrackedRoundedRectComponent.BorderThickness = UnscaleFloat(TrackedRoundedRectComponent.BorderThickness, hasApplied ? applied.RoundedRectBorderThickness : 0f, hasApplied, RoundedRectBorderThickness, scalarScale);
            }

            if (TrackedTextComponent != null) {
                double textFontScale = TrackedTextComponent.NativeFontPixels != TextNativePixelMode.Disabled ? 1d : scalarScale;
                float2 liveShadow = TrackedTextComponent.ShadowOffset;
                float2 appliedShadow = hasApplied ? applied.TextShadowOffset : new float2(0f, 0f);
                TrackedTextComponent.Size = UnscaleInt2(TrackedTextComponent.Size, hasApplied ? applied.TextSize : new int2(0, 0), hasApplied, TextSize, widthScale, heightScale);
                TrackedTextComponent.FontScale = UnscaleFloat(TrackedTextComponent.FontScale, hasApplied ? applied.TextFontScale : 0f, hasApplied, TextFontScale, textFontScale);
                TrackedTextComponent.OutlineScale = UnscaleFloat(TrackedTextComponent.OutlineScale, hasApplied ? applied.TextOutlineScale : 0f, hasApplied, TextOutlineScale, scalarScale);
                TrackedTextComponent.ShadowOffset = new float2(
                    UnscaleFloat(liveShadow.X, appliedShadow.X, hasApplied, TextShadowOffset.X, scalarScale),
                    UnscaleFloat(liveShadow.Y, appliedShadow.Y, hasApplied, TextShadowOffset.Y, scalarScale));
            }

            if (TrackedSpriteComponent != null) {
                TrackedSpriteComponent.Size = UnscaleInt2(TrackedSpriteComponent.Size, hasApplied ? applied.SpriteSize : new int2(0, 0), hasApplied, SpriteSize, widthScale, heightScale);
            }

            if (TrackedClipRectComponent != null) {
                TrackedClipRectComponent.Size = UnscaleInt2(TrackedClipRectComponent.Size, hasApplied ? applied.ClipRectSize : new int2(0, 0), hasApplied, ClipRectSize, widthScale, heightScale);
            }

            if (TrackedInteractableComponent != null) {
                TrackedInteractableComponent.Size = UnscaleInt2(TrackedInteractableComponent.Size, hasApplied ? applied.InteractableSize : new int2(0, 0), hasApplied, InteractableSize, widthScale, heightScale);
            }

            if (TrackedScrollComponent != null) {
                TrackedScrollComponent.Size = UnscaleInt2(TrackedScrollComponent.Size, hasApplied ? applied.ScrollSize : new int2(0, 0), hasApplied, ScrollSize, widthScale, heightScale);
                TrackedScrollComponent.ItemExtent = UnscaleInt(TrackedScrollComponent.ItemExtent, hasApplied ? applied.ScrollItemExtent : 0, hasApplied, ScrollItemExtent, scalarScale);
            }
        }

        /// <summary>
        /// Writes the authored local position back onto the tracked entity, mapping a position edited in the scaled preview back into authored space.
        /// </summary>
        /// <param name="applied">Live values recorded right after the last scale pass, or null when no edits should be detected.</param>
        /// <param name="canvasOrigin">Root displacement added by the last scale pass.</param>
        /// <param name="widthScale">Horizontal pixels per authored pixel used by the last scale pass.</param>
        /// <param name="heightScale">Vertical pixels per authored pixel used by the last scale pass.</param>
        public void RestoreAuthoredPosition(ViewportLayoutSnapshot applied, float2 canvasOrigin, double widthScale, double heightScale) {
            bool hasApplied = applied != null;
            float3 live = Entity.LocalPosition;
            float3 appliedPosition = hasApplied ? applied.LocalPosition : new float3(0f, 0f, 0f);
            float originX = IsRootEntity ? canvasOrigin.X : 0f;
            float originY = IsRootEntity ? canvasOrigin.Y : 0f;
            float positionX = hasApplied && live.X != appliedPosition.X ? (float)((live.X - originX) / widthScale) : LocalPosition.X;
            float positionY = hasApplied && live.Y != appliedPosition.Y ? (float)((live.Y - originY) / heightScale) : LocalPosition.Y;
            float positionZ = hasApplied && live.Z != appliedPosition.Z ? live.Z : LocalPosition.Z;
            Entity.LocalPosition = new float3(positionX, positionY, positionZ);
        }

        /// <summary>
        /// Finds the first attached layout component.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>An entity-owned layout component borrowed by the snapshot when one exists; otherwise null.</returns>
        [NativeBorrowedReturn]
        static LayoutComponent FindLayoutComponent(Entity entity) {
            if (entity.Components == null) {
                return null;
            }

            for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                if (entity.Components[componentIndex] is LayoutComponent component) {
                    return component;
                }
            }

            return null;
        }

        /// <summary>Finds the performance overlay whose generated children are laid out by the overlay itself.</summary>
        /// <param name="entity">Entity whose components are inspected.</param>
        /// <returns>The borrowed attached overlay, or null.</returns>
        [NativeBorrowedReturn]
        static FPSComponent FindFpsComponent(Entity entity) {
            if (entity.Components != null) {
                for (int index = 0; index < entity.Components.Count; index++) {
                    if (entity.Components[index] is FPSComponent component) {
                        return component;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Finds the first attached rounded-rectangle component.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>An entity-owned rounded-rectangle component borrowed by the snapshot when one exists; otherwise null.</returns>
        [NativeBorrowedReturn]
        static RoundedRectComponent FindRoundedRectComponent(Entity entity) {
            if (entity.Components == null) {
                return null;
            }

            for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                if (entity.Components[componentIndex] is RoundedRectComponent component) {
                    return component;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the first attached text component.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>An entity-owned text component borrowed by the snapshot when one exists; otherwise null.</returns>
        [NativeBorrowedReturn]
        static TextComponent FindTextComponent(Entity entity) {
            if (entity.Components == null) {
                return null;
            }

            for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                if (entity.Components[componentIndex] is TextComponent component) {
                    return component;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the first attached sprite component.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>An entity-owned sprite component borrowed by the snapshot when one exists; otherwise null.</returns>
        [NativeBorrowedReturn]
        static SpriteComponent FindSpriteComponent(Entity entity) {
            if (entity.Components == null) {
                return null;
            }

            for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                if (entity.Components[componentIndex] is SpriteComponent component) {
                    return component;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the first attached clip-rectangle component.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>An entity-owned clip-rectangle component borrowed by the snapshot when one exists; otherwise null.</returns>
        [NativeBorrowedReturn]
        static ClipRectComponent FindClipRectComponent(Entity entity) {
            if (entity.Components == null) {
                return null;
            }

            for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                if (entity.Components[componentIndex] is ClipRectComponent component) {
                    return component;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the first attached interactable component.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>An entity-owned interactable component borrowed by the snapshot when one exists; otherwise null.</returns>
        [NativeBorrowedReturn]
        static InteractableComponent FindInteractableComponent(Entity entity) {
            if (entity.Components == null) {
                return null;
            }

            for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                if (entity.Components[componentIndex] is InteractableComponent component) {
                    return component;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the first attached scroll component.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>An entity-owned scroll component borrowed by the snapshot when one exists; otherwise null.</returns>
        [NativeBorrowedReturn]
        static ScrollComponent FindScrollComponent(Entity entity) {
            if (entity.Components == null) {
                return null;
            }

            for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                if (entity.Components[componentIndex] is ScrollComponent component) {
                    return component;
                }
            }

            return null;
        }

        /// <summary>
        /// Scales one signed pixel value while preserving zero and rounding to the nearest integer.
        /// </summary>
        /// <param name="value">Authored pixel value.</param>
        /// <param name="scale">Resolved scalar scale.</param>
        /// <returns>Scaled pixel value.</returns>
        static int ScaleInt(int value, double scale) {
            if (value == 0) {
                return 0;
            }

            return Math.Max(1, (int)Math.Round(value * scale));
        }

        /// <summary>
        /// Scales one integer vector while preserving zero-valued axes.
        /// </summary>
        /// <param name="value">Authored integer vector.</param>
        /// <param name="widthScale">Resolved horizontal scale.</param>
        /// <param name="heightScale">Resolved vertical scale.</param>
        /// <returns>Scaled integer vector.</returns>
        static int2 ScaleInt2(int2 value, double widthScale, double heightScale) {
            return new int2(ScaleInt(value.X, widthScale), ScaleInt(value.Y, heightScale));
        }

        /// <summary>
        /// Scales one authored float value using double precision before casting back to float.
        /// </summary>
        /// <param name="value">Authored float value.</param>
        /// <param name="scale">Resolved scale.</param>
        /// <returns>Scaled float value.</returns>
        static float ScaleFloat(float value, double scale) {
            return (float)(value * scale);
        }

        /// <summary>
        /// Resolves the authored value for one float field: the captured authored value while the live value still matches the
        /// last scale pass, otherwise the live edit divided back into authored space.
        /// </summary>
        /// <param name="live">Current live value.</param>
        /// <param name="applied">Value written by the last scale pass.</param>
        /// <param name="hasApplied">True when a last scale pass was recorded and edits can be detected.</param>
        /// <param name="authored">Captured authored value.</param>
        /// <param name="scale">Scale used by the last scale pass.</param>
        /// <returns>Authored value that should be persisted and scaled next.</returns>
        static float UnscaleFloat(float live, float applied, bool hasApplied, float authored, double scale) {
            if (!hasApplied || live == applied || !(scale > 0d)) {
                return authored;
            }

            return (float)(live / scale);
        }

        /// <summary>
        /// Resolves the authored value for one integer field, mapping a live edit back through the last scale pass.
        /// </summary>
        /// <param name="live">Current live value.</param>
        /// <param name="applied">Value written by the last scale pass.</param>
        /// <param name="hasApplied">True when a last scale pass was recorded and edits can be detected.</param>
        /// <param name="authored">Captured authored value.</param>
        /// <param name="scale">Scale used by the last scale pass.</param>
        /// <returns>Authored value that should be persisted and scaled next.</returns>
        static int UnscaleInt(int live, int applied, bool hasApplied, int authored, double scale) {
            if (!hasApplied || live == applied || !(scale > 0d)) {
                return authored;
            }

            return ScaleInt(live, 1d / scale);
        }

        /// <summary>
        /// Resolves the authored value for one integer vector, mapping each edited axis back through the last scale pass.
        /// </summary>
        /// <param name="live">Current live value.</param>
        /// <param name="applied">Value written by the last scale pass.</param>
        /// <param name="hasApplied">True when a last scale pass was recorded and edits can be detected.</param>
        /// <param name="authored">Captured authored value.</param>
        /// <param name="widthScale">Horizontal scale used by the last scale pass.</param>
        /// <param name="heightScale">Vertical scale used by the last scale pass.</param>
        /// <returns>Authored vector that should be persisted and scaled next.</returns>
        static int2 UnscaleInt2(int2 live, int2 applied, bool hasApplied, int2 authored, double widthScale, double heightScale) {
            return new int2(
                UnscaleInt(live.X, applied.X, hasApplied, authored.X, widthScale),
                UnscaleInt(live.Y, applied.Y, hasApplied, authored.Y, heightScale));
        }
    }
}
