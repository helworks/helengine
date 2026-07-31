namespace helengine {
    /// <summary>
    /// Constructs standalone HelPhysics scene runtimes from explicit validated world settings.
    /// </summary>
    public static class HelPhysicsRuntimeFactory3D {
        /// <summary>
        /// Creates one world and its public scene binder without changing global runtime registration.
        /// </summary>
        /// <param name="settings">Explicit fixed-step, capacity, gravity, and solver settings.</param>
        /// <returns>A binder that owns the newly constructed HelPhysics world.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is null.</exception>
        public static HelPhysicsSceneBinder3D Create(HelPhysicsWorldSettings3D settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            return new HelPhysicsSceneBinder3D(new HelPhysicsWorld3D(settings));
        }
    }
}
