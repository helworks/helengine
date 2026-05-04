namespace helengine {
    /// <summary>
    /// Synchronizes authored scene state to and from one runtime physics world.
    /// </summary>
    public interface IPhysicsSceneBinding {
        /// <summary>
        /// Pulls authored scene state into the runtime physics world before simulation.
        /// </summary>
        void SynchronizeFromScene();

        /// <summary>
        /// Pushes solved runtime state back into authored scene objects after simulation.
        /// </summary>
        void SynchronizeToScene();
    }
}
