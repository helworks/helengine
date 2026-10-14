namespace helengine {
    /// <summary>Retains the borrowed control and camera owning one contact until lift or cancellation.</summary>
    internal struct TouchInteractionCapture {
        /// <summary>Identifies the captured contact independently of its native event index.</summary>
        public int Id;
        /// <summary>References the scene-owned control; the router never disposes this reference.</summary>
        public ITouchInteractable2D Target;
        /// <summary>References the object-manager-owned camera used for coordinate conversion.</summary>
        public ICamera Camera;
        /// <summary>Retains the latest physical position for cancellation after focus or scene changes.</summary>
        public InputTouchState LastSample;
        /// <summary>Records whether the backend included this held contact in the current frame.</summary>
        public bool SeenThisFrame;
    }
}
