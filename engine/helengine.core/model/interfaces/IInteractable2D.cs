using System;

namespace helengine {
    /// <summary>
    /// Describes a 2D element that can receive pointer interaction.
    /// </summary>
    public interface IInteractable2D {
        /// <summary>
        /// Gets the parent entity owning the interactable.
        /// </summary>
        Entity Parent { get; }

        /// <summary>
        /// Gets the cursor the host should display while this interactable is hovered.
        /// </summary>
        PointerCursorKind HoverCursor { get; }

        /// <summary>
        /// Gets or sets the size of the interactable region.
        /// </summary>
        int2 Size { get; set; }

        /// <summary>
        /// Event raised when the cursor interacts with the region.
        /// </summary>
        event Action<int2, int2, PointerInteraction> CursorEvent;

        /// <summary>
        /// Handles a cursor event for the interactable.
        /// </summary>
        /// <param name="relPos">Relative pointer position.</param>
        /// <param name="delta">Pointer movement delta.</param>
        /// <param name="state">Pointer interaction state.</param>
        void OnCursor(int2 relPos, int2 delta, PointerInteraction state);
    }
}
