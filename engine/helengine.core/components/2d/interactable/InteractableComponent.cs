namespace helengine {
    /// <summary>
    /// Provides a hit-testable region that raises pointer events.
    /// </summary>
#if !HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION
    [RunInEditor]
#endif
    public class InteractableComponent : Component, ITouchInteractable2D {
        /// <summary>Tracks whether one contact currently drives this control's legacy cursor callbacks.</summary>
        bool HasCursorTouch;
        /// <summary>Identifies that contact so another finger cannot release or move its cursor capture.</summary>
        int CursorTouchId;
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

        /// <summary>Raised independently for each captured finger, with position relative to this control.</summary>
        public event Action<InputTouchState> TouchEvent;

        /// <summary>
        /// Registers the interactable with the manager when added to an enabled entity.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            if (entity.IsHierarchyEnabled) {
                OwnerCore.ObjectManager.RegisterInteractable(this);
            }
        }

        /// <summary>
        /// Registers or unregisters the interactable based on enabled state changes.
        /// </summary>
        /// <param name="newEnabled">New enabled state.</param>
        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (newEnabled) {
                OwnerCore.ObjectManager.RegisterInteractable(this);
            } else {
                OwnerCore.ObjectManager.RemoveInteractable(this);
            }
        }

        /// <summary>
        /// Removes the interactable from the global hit-test list when the owning entity detaches it.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);
            OwnerCore.ObjectManager.RemoveInteractable(this);
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

        /// <summary>Delivers independent touch events and lets the first finger operate existing cursor-based controls.</summary>
        public virtual void OnTouch(InputTouchState touch) {
            if (touch.Phase == InputTouchPhase.Began && !HasCursorTouch) {
                HasCursorTouch = true;
                CursorTouchId = touch.Id;
            }
            bool drivesCursor = HasCursorTouch && CursorTouchId == touch.Id;
            if (drivesCursor && (touch.Phase == InputTouchPhase.Ended || touch.Phase == InputTouchPhase.Canceled)) {
                HasCursorTouch = false;
            }
            TouchEvent?.Invoke(touch);
            if (!drivesCursor) { return; }
            int2 position = new int2(touch.X, touch.Y);
            int2 delta = new int2(touch.DeltaX, touch.DeltaY);
            if (touch.Phase == InputTouchPhase.Began) {
                if (!HasCursorTouch || Parent == null || !Parent.IsHierarchyEnabled) { return; }
                OnCursor(position, delta, PointerInteraction.Hover);
                if (HasCursorTouch && CursorTouchId == touch.Id) { OnCursor(position, delta, PointerInteraction.Press); }
            } else if (touch.Phase == InputTouchPhase.Moved) {
                OnCursor(position, delta, PointerInteraction.Hover);
            } else if (touch.Phase == InputTouchPhase.Stationary) {
                OnCursor(position, delta, PointerInteraction.None);
            } else if (touch.Phase == InputTouchPhase.Ended) {
                OnCursor(position, delta, PointerInteraction.Release);
                OnCursor(position, new int2(0, 0), PointerInteraction.Leave);
            } else if (touch.Phase == InputTouchPhase.Canceled) {
                OnCursor(position, new int2(0, 0), PointerInteraction.Leave);
            }
        }
    }
}
