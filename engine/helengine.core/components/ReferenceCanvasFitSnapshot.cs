namespace helengine {
    /// <summary>
    /// Stores the authored layout state for one entity subtree node so reference-canvas scaling can be reapplied without cumulative drift.
    /// </summary>
    public sealed class ReferenceCanvasFitSnapshot {
        /// <summary>
        /// Initializes one snapshot for the supplied entity and any supported attached layout components.
        /// </summary>
        /// <param name="entity">Entity whose authored state should be preserved.</param>
        public ReferenceCanvasFitSnapshot(Entity entity) {
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
            LocalPosition = entity.LocalPosition;

            TrackedAnchorComponent = FindAnchorComponent(entity);
            if (TrackedAnchorComponent != null) {
                AnchorDistances = TrackedAnchorComponent.AnchorDistances;
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
        /// Gets the authored local position captured for the entity.
        /// </summary>
        public float3 LocalPosition { get; }

        /// <summary>
        /// Gets the attached anchor component when one exists.
        /// </summary>
        public AnchorComponent TrackedAnchorComponent { get; }

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
        /// Applies one absolute fit scale to the captured entity and any supported attached layout components.
        /// </summary>
        /// <param name="scale">Uniform fit scale resolved for the live window.</param>
        public void Apply(double scale) {
            Entity.LocalPosition = new float3(
                ScaleFloat(LocalPosition.X, scale),
                ScaleFloat(LocalPosition.Y, scale),
                LocalPosition.Z);

            if (TrackedAnchorComponent != null) {
                TrackedAnchorComponent.AnchorDistances = new float4(
                    ScaleFloat(AnchorDistances.X, scale),
                    ScaleFloat(AnchorDistances.Y, scale),
                    ScaleFloat(AnchorDistances.Z, scale),
                    ScaleFloat(AnchorDistances.W, scale));
            }

            if (TrackedRoundedRectComponent != null) {
                TrackedRoundedRectComponent.Size = ScaleInt2(RoundedRectSize, scale);
                TrackedRoundedRectComponent.Radius = ScaleFloat(RoundedRectRadius, scale);
                TrackedRoundedRectComponent.BorderThickness = ScaleFloat(RoundedRectBorderThickness, scale);
            }

            if (TrackedTextComponent != null) {
                TrackedTextComponent.Size = ScaleInt2(TextSize, scale);
                TrackedTextComponent.FontScale = ScaleFloat(TextFontScale, scale);
            }

            if (TrackedClipRectComponent != null) {
                TrackedClipRectComponent.Size = ScaleInt2(ClipRectSize, scale);
            }

            if (TrackedInteractableComponent != null) {
                TrackedInteractableComponent.Size = ScaleInt2(InteractableSize, scale);
            }

            if (TrackedScrollComponent != null) {
                TrackedScrollComponent.Size = ScaleInt2(ScrollSize, scale);
                TrackedScrollComponent.ItemExtent = ScaleInt(ScrollItemExtent, scale);
            }
        }

        /// <summary>
        /// Refreshes anchored layout after the scaled bounds and distances have been applied.
        /// </summary>
        public void RefreshAnchoring() {
            if (TrackedAnchorComponent != null) {
                TrackedAnchorComponent.RefreshAnchoring();
            }
        }

        /// <summary>
        /// Finds the first attached anchor component.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>Attached anchor component when one exists; otherwise null.</returns>
        static AnchorComponent FindAnchorComponent(Entity entity) {
            if (entity.Components == null) {
                return null;
            }

            for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                if (entity.Components[componentIndex] is AnchorComponent component) {
                    return component;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the first attached rounded-rectangle component.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>Attached rounded-rectangle component when one exists; otherwise null.</returns>
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
        /// <returns>Attached text component when one exists; otherwise null.</returns>
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
        /// Finds the first attached clip-rectangle component.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>Attached clip-rectangle component when one exists; otherwise null.</returns>
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
        /// <returns>Attached interactable component when one exists; otherwise null.</returns>
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
        /// <returns>Attached scroll component when one exists; otherwise null.</returns>
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
        /// <param name="scale">Uniform fit scale.</param>
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
        /// <param name="scale">Uniform fit scale.</param>
        /// <returns>Scaled integer vector.</returns>
        static int2 ScaleInt2(int2 value, double scale) {
            return new int2(ScaleInt(value.X, scale), ScaleInt(value.Y, scale));
        }

        /// <summary>
        /// Scales one authored float value using double precision before casting back to float.
        /// </summary>
        /// <param name="value">Authored float value.</param>
        /// <param name="scale">Uniform fit scale.</param>
        /// <returns>Scaled float value.</returns>
        static float ScaleFloat(float value, double scale) {
            return (float)(value * scale);
        }
    }
}
