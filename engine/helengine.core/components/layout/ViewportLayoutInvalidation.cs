namespace helengine {
    /// <summary>Invalidates optional layout buffers on cold hierarchy mutations, before borrowed objects disappear.</summary>
    internal static class ViewportLayoutInvalidation {
        /// <summary>Transfers an existing subtree out of enclosing caches before a new scaling owner can initialize.</summary>
        public static void BeforeAttach(Entity owner, Component component) {
            if (component is ReferenceCanvasFitComponent || (component is ViewportComponent viewport && viewport.ScalesSubtree)) {
                BeforeScalingBoundaryChange(owner);
            }
        }
        /// <summary>Restores and drops enclosing-owner originals before an ownership boundary changes.</summary>
        public static void BeforeScalingBoundaryChange(Entity owner) {
            if (owner != null) { Notify(owner.Parent, null, owner); }
        }
        /// <summary>Restores transferable originals and removes subtree references from affected owners.</summary>
        public static void BeforeDetach(Entity subtree) { Notify(subtree.Parent, null, subtree); }
        /// <summary>Removes component references before native destruction.</summary>
        public static void BeforeDetach(Component component) {
            if (component is IAnchorBoundsProvider || component is CameraComponent) {
                ClearProviderReferences(component.ParentUnsafe, component);
                if (component is CameraComponent) {
                    List<Entity> entities = component.ParentUnsafe.OwnerCore.ObjectManager.Entities;
                    for (int index = 0; index < entities.Count; index++) { ClearEntityProviderReferences(entities[index], component); }
                }
            }
            Notify(component.ParentUnsafe, component, null);
        }
        /// <summary>Marks ancestor owners dirty after attachment without capturing layout early.</summary>
        public static void AfterAttach(Entity subtree) {
            Notify(subtree.Parent, null, null);
            RefreshBindings(subtree);
        }
        /// <summary>Marks new participant composition dirty after attachment.</summary>
        public static void AfterAttach(Component component) {
            Notify(component.ParentUnsafe, null, null);
            if (component is IAnchorBoundsProvider || component is CameraComponent) { RefreshBindings(component.ParentUnsafe); }
        }
        /// <summary>Resolves replacement providers after the removed component is no longer visible to hierarchy searches.</summary>
        public static void AfterDetach(Entity owner, Component component) {
            if (component is IAnchorBoundsProvider || component is CameraComponent) { RefreshBindings(owner); }
        }
        /// <summary>Initializes nested attachments as one transaction, without retaining a processed flag on every entity.</summary>
        public static void InitializeIncomingSubtree([NativeRetainsBorrow] Entity subtree) {
            Core core = subtree.OwnerCore;
            Entity previous = core.InitializingLayoutSubtree;
            bool nested = false;
            Entity current = subtree.Parent;
            while (current != null && !nested) {
                nested = ReferenceEquals(current, previous);
                current = current.Parent;
            }
            if (!nested) { core.InitializingLayoutSubtree = subtree; }
            try {
                subtree.InitializeHierarchy();
                if (!nested) { PrepareIncomingSubtree(subtree); }
            } finally {
                core.InitializingLayoutSubtree = previous;
            }
        }
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
                    else if (subtree.Components[index] is AnimationPlayerComponent player) { player.RefreshLayoutOwner(); }
                    else if (subtree.Components[index] is LayoutComponent layout) { layout.HierarchyBindingChanged(); }
                }
            }
            if (subtree.Children != null) {
                for (int index = 0; index < subtree.Children.Count; index++) { RefreshBindings(subtree.Children[index]); }
            }
        }

        /// <summary>Disconnects descendant provider aliases while the removed component is still alive.</summary>
        static void ClearProviderReferences(Entity subtree, Component owner) {
            if (subtree == null) { return; }
            ClearEntityProviderReferences(subtree, owner);
            if (subtree.Children != null) {
                for (int index = 0; index < subtree.Children.Count; index++) { ClearProviderReferences(subtree.Children[index], owner); }
            }
        }
        /// <summary>Clears matching aliases on one entity; flat camera scans also cover explicit references in other roots.</summary>
        static void ClearEntityProviderReferences(Entity entity, Component owner) {
            if (entity.Components == null) { return; }
            for (int index = 0; index < entity.Components.Count; index++) {
                Component consumer = entity.Components[index];
                if (consumer is AnimationPlayerComponent player) { player.LayoutOwnerRemoved(owner); }
                else if (consumer is ViewportComponent viewport) { viewport.LayoutProviderRemoved(owner); }
                else if (consumer is ReferenceCanvasFitComponent fit) { fit.LayoutProviderRemoved(owner); }
                else if (consumer is LayoutComponent layout) { layout.LayoutProviderRemoved(owner); }
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
