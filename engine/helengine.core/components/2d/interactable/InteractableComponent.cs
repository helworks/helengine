namespace helengine {
    /// <summary>
    /// Provides a hit-testable region that raises pointer events.
    /// </summary>
    public class InteractableComponent : Component, IInteractable2D {
        /// <summary>
        /// Gets or sets the cursor the host should display while this interactable is hovered.
        /// </summary>
        public PointerCursorKind HoverCursor { get; set; }

        /// <summary>
        /// Gets or sets the size of the interactable region.
        /// </summary>
        public int2 Size { get; set; }

        /// <summary>
        /// Raised when the cursor interacts with the region.
        /// </summary>
        public event Action<int2, int2, PointerInteraction> CursorEvent;

        /// <summary>
        /// Registers the interactable with the manager when added to an enabled entity.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            if (entity.IsHierarchyEnabled) {
                Core.Instance.ObjectManager.RegisterInteractable(this);
            }
        }

        /// <summary>
        /// Registers or unregisters the interactable based on enabled state changes.
        /// </summary>
        /// <param name="newEnabled">New enabled state.</param>
        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (newEnabled) {
                Core.Instance.ObjectManager.RegisterInteractable(this);
            } else {
                Core.Instance.ObjectManager.RemoveInteractable(this);
                if (Core.Instance != null && Core.Instance.PointerInteractionSystem != null) {
                    Core.Instance.PointerInteractionSystem.ClearInteractionFor(this);
                }
            }
        }

        /// <summary>
        /// Removes the interactable from the global hit-test list when the owning entity detaches it.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);
            Core.Instance.ObjectManager.RemoveInteractable(this);
            if (Core.Instance != null && Core.Instance.PointerInteractionSystem != null) {
                Core.Instance.PointerInteractionSystem.ClearInteractionFor(this);
            }
        }

        /// <summary>
        /// Raises the cursor event with relative position and movement.
        /// </summary>
        /// <param name="relPos">Relative pointer position.</param>
        /// <param name="delta">Pointer delta.</param>
        /// <param name="state">Pointer interaction state.</param>
        public virtual void OnCursor(int2 relPos, int2 delta, PointerInteraction state) {
            CursorEvent?.Invoke(relPos, delta, state);
        }
    }
}
