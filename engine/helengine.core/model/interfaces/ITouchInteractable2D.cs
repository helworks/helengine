namespace helengine {
    /// <summary>A hit-testable control that accepts independent contact lifetimes in viewport-relative coordinates.</summary>
    public interface ITouchInteractable2D : IInteractable2D {
        /// <summary>Handles one captured finger. Coordinates are relative to the control; cancellation must not activate it.</summary>
        void OnTouch(InputTouchState touch);
    }
}
