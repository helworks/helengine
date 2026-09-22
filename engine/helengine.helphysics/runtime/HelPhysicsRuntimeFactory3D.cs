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
        public static HelPhysicsSceneBinder3D Create([NativeTakesOwnership] HelPhysicsWorldSettings3D settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            return new HelPhysicsSceneBinder3D(new HelPhysicsWorld3D(settings));
        }
        /// <summary>
        /// Creates a normal scene-bindable HelPhysics runtime using the owning core scheduler fixed step.
        /// </summary>
        /// <param name="ownerCore">Core that owns fixed-step scheduling for the runtime.</param>
        /// <returns>A runtime adapter ready for scene binding and core attachment.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="ownerCore"/> is null.</exception>
        public static HelPhysicsRuntime3D CreateForCore(Core ownerCore) {
            return new HelPhysicsRuntime3D(ownerCore);
        }

        /// <summary>
        /// Creates a standalone scene-bindable HelPhysics runtime using the default fixed step.
        /// </summary>
        /// <returns>A runtime adapter using the default HelPhysics fixed step.</returns>
        public static HelPhysicsRuntime3D CreateDefault() {
            HelPhysicsWorldSettings3D settings = new HelPhysicsWorldSettings3D();
            return new HelPhysicsRuntime3D(settings.FixedStepSeconds);
        }

        /// <summary>
        /// Creates a standalone scene-bindable HelPhysics runtime using the owning core scheduler fixed step.
        /// </summary>
        /// <param name="ownerCore">Core that owns fixed-step scheduling for the runtime.</param>
        /// <returns>A runtime adapter ready for scene binding and core attachment.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="ownerCore"/> is null.</exception>
        public static HelPhysicsRuntime3D CreateDefault(Core ownerCore) {
            return new HelPhysicsRuntime3D(ownerCore);
        }    }
}
