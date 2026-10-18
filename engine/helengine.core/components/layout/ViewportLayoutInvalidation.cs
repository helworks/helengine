namespace helengine {
    /// <summary>Invalidates optional layout buffers on cold hierarchy mutations, before borrowed objects disappear.</summary>
    internal static class ViewportLayoutInvalidation {
        /// <summary>Restores transferable originals and removes subtree references from affected owners.</summary>
        public static void BeforeDetach(Entity subtree) { Notify(subtree.Parent, null, subtree); }
        /// <summary>Removes component references before native destruction.</summary>
        public static void BeforeDetach(Component component) { Notify(component.ParentUnsafe, component, null); }
        /// <summary>Marks ancestor owners dirty after attachment without capturing layout early.</summary>
        public static void AfterAttach(Entity subtree) {
            Notify(subtree.Parent, null, null);
            RefreshBindings(subtree);
        }
        /// <summary>Marks new participant composition dirty after attachment.</summary>
        public static void AfterAttach(Component component) { Notify(component.ParentUnsafe, null, null); }
        /// <summary>Prepares a newly initialized instance once at its attachment boundary.</summary>
        public static void PrepareIncomingSubtree(Entity subtree) {
            if (!ViewportLayoutTraversal.ShouldVisit(subtree)) { return; }
            Entity current = subtree.Parent;
            while (current != null) {
                if (current.Components != null) {
                    for (int index = 0; index < current.Components.Count; index++) {
                        if (current.Components[index] is ReferenceCanvasFitComponent fit) {
                            fit.PrepareAttachedSubtree(subtree);
                            return;
                        }
                    }
                    for (int index = 0; index < current.Components.Count; index++) {
                        if (current.Components[index] is ViewportComponent viewport && viewport.ScalesSubtree) {
                            viewport.PrepareAttachedSubtree(subtree);
                            return;
                        }
                    }
                }
                current = current.Parent;
            }
        }
        /// <summary>Refreshes binding-sensitive owners only on a structural change.</summary>
        public static void RefreshBindings(Entity subtree) {
            if (subtree.Components != null) {
                for (int index = 0; index < subtree.Components.Count; index++) {
                    if (subtree.Components[index] is ViewportComponent viewport) { viewport.HierarchyBindingChanged(); }
                    else if (subtree.Components[index] is ReferenceCanvasFitComponent fit) { fit.HierarchyBindingChanged(); }
                }
            }
            if (subtree.Children != null) {
                for (int index = 0; index < subtree.Children.Count; index++) { RefreshBindings(subtree.Children[index]); }
            }
        }
        /// <summary>Routes notifications only during mutation, never on a frame update.</summary>
        static void Notify(Entity current, Component component, Entity subtree) {
            while (current != null) {
                if (current.Components != null) {
                    for (int index = 0; index < current.Components.Count; index++) {
                        if (current.Components[index] is ViewportComponent viewport) { viewport.InvalidateLayout(component, subtree); }
                        else if (current.Components[index] is ReferenceCanvasFitComponent fit) { fit.InvalidateLayout(component, subtree); }
                    }
                }
                current = current.Parent;
            }
        }
    }
}
