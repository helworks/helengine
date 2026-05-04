namespace helengine {
    /// <summary>
    /// Defines how one 3D rigid body participates in simulation.
    /// </summary>
    public enum BodyKind3D {
        /// <summary>
        /// Static bodies never move and only serve as collision anchors.
        /// </summary>
        Static = 0,

        /// <summary>
        /// Kinematic bodies are moved by authored code and push other simulated bodies.
        /// </summary>
        Kinematic = 1,

        /// <summary>
        /// Dynamic bodies are advanced by the physics solver.
        /// </summary>
        Dynamic = 2
    }
}
