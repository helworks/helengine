namespace helengine {
    /// <summary>
    /// Exposes trigger overlap events emitted by one 3D physics runtime during the most recent fixed step.
    /// </summary>
    public interface IPhysicsTriggerEventRuntime3D {
        /// <summary>
        /// Gets the trigger overlap events emitted during the most recent fixed step.
        /// </summary>
        IReadOnlyList<TriggerEvent3D> TriggerEvents { get; }
    }
}
