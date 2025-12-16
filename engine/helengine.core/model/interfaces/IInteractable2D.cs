namespace helengine {
    /// <summary>
    /// Describes a 2D element that can receive pointer interaction.
    /// </summary>
    public interface IInteractable2D {
        /// <summary>
        /// Gets or sets the size of the interactable region.
        /// </summary>
        int2 Size { get; set; }

        /// <summary>
        /// Gets or sets the relative position of the interactable region.
        /// </summary>
        float3 Position { get; set; }

        /// <summary>
        /// Handles a cursor interaction event.
        /// </summary>
        /// <param name="pos">Pointer position.</param>
        /// <param name="delta">Pointer delta.</param>
        /// <param name="state">Pointer state.</param>
        void CursorEvent(int2 pos, int2 delta, PointerInteraction state);
    }
}
