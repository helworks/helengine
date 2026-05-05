namespace helengine {
    /// <summary>
    /// Defines the minimal fixed-step contract implemented by one pluggable physics runtime.
    /// </summary>
    public interface IPhysicsRuntime {
        /// <summary>
        /// Advances the physics runtime by one fixed simulation step.
        /// </summary>
        /// <param name="stepSeconds">Simulation step length in seconds.</param>
        void Step(double stepSeconds);
    }
}
