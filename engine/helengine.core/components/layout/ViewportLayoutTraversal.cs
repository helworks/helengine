namespace helengine {
    /// <summary>Defines layout owner boundaries consistently for direct application and typed retention.</summary>
    internal static class ViewportLayoutTraversal {
        /// <summary>Publishes a completed layout to animators once per layout pass, without per-frame ancestor searches.</summary>
        public static void NotifyAnimations(Entity entity) {
            if (entity.Components != null) {
                for (int index = 0; index < entity.Components.Count; index++) {
                    if (entity.Components[index] is AnimationPlayerComponent player) { player.PrepareLayout(); }
                }
            }
            if (entity.Children != null) {
                for (int index = 0; index < entity.Children.Count; index++) {
                    if (ShouldVisit(entity.Children[index])) { NotifyAnimations(entity.Children[index]); }
                }
            }
        }

        /// <summary>Applies loaded values directly; no original-value records are constructed.</summary>
        public static void ApplyOnce(Entity entity, Entity owner, in ViewportLayoutMetrics metrics) {
            ViewportLayoutMetrics local = metrics;
            if (!ReferenceEquals(entity, owner)) { local.Origin = new float2(0f, 0f); }
            if (entity.Components != null) {
                for (int index = 0; index < entity.Components.Count; index++) {
                    ViewportLayoutApplier.ApplyComponent(entity.Components[index], metrics);
                }
            }
            entity.LocalPosition = ViewportLayoutApplier.ResolvePosition(entity.LocalPosition, local);
            if (entity.Children != null) {
                for (int index = 0; index < entity.Children.Count; index++) {
                    if (ShouldVisit(entity.Children[index])) { ApplyOnce(entity.Children[index], owner, metrics); }
                }
            }
        }

        /// <summary>Reanchors after all sizes and anchor distances have reached their prepared values.</summary>
        public static void RefreshAnchoring(Entity entity, bool includeRoot) {
            if (includeRoot && entity.Components != null) {
                for (int index = 0; index < entity.Components.Count; index++) {
                    if (entity.Components[index] is LayoutComponent layout) { layout.RefreshAnchoring(); }
                }
            }
            if (entity.Children != null) {
                for (int index = 0; index < entity.Children.Count; index++) {
                    if (ShouldVisit(entity.Children[index])) { RefreshAnchoring(entity.Children[index], true); }
                }
            }
        }

        /// <summary>Leaves independently scaled children and generated overlay internals to their owners.</summary>
        public static bool ShouldVisit(Entity child) {
            if (child.Components != null) {
                for (int index = 0; index < child.Components.Count; index++) {
                    if (child.Components[index] is ViewportComponent viewport && viewport.ScalesSubtree) { return false; }
                    if (child.Components[index] is ReferenceCanvasFitComponent) { return false; }
                    if (child.Components[index] is ScrollBarComponent) { return false; }
                }
            }
            Entity parent = child.Parent;
            if (parent != null && parent.Components != null) {
                for (int index = 0; index < parent.Components.Count; index++) {
                    if (parent.Components[index] is FPSComponent fps && ReferenceEquals(child, fps.OverlayLayoutRoot)) { return false; }
                }
            }
            return true;
        }
    }
}
