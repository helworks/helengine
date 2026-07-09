namespace helengine {
    /// <summary>
    /// Exposes the parity-complete 3D physics world through a parameterless runtime wrapper that can be created by migration hosts.
    /// </summary>
    public sealed class PhysicsWorld3DCompatibilityRuntime : ISceneBindablePhysicsRuntime, IPhysicsTriggerEventRuntime3D {
        /// <summary>
        /// Backing medium-profile world that performs the actual simulation work.
        /// </summary>
        readonly PhysicsWorld3D InnerWorld;

        /// <summary>
        /// Initializes one compatibility runtime backed by the medium default physics world profile.
        /// </summary>
        public PhysicsWorld3DCompatibilityRuntime() {
            InnerWorld = PhysicsWorld3D.CreateMediumDefault();
        }

        /// <summary>
        /// Gets the trigger overlap events emitted during the most recent fixed step.
        /// </summary>
        public IReadOnlyList<TriggerEvent3D> TriggerEvents => InnerWorld.TriggerEvents;

        /// <summary>
        /// Gets the number of registered runtime bodies currently bound to the world.
        /// </summary>
        public int RegisteredBodyCount => InnerWorld.BodyStates.Count;

        /// <summary>
        /// Binds the supplied scene hierarchy to the backing world.
        /// </summary>
        /// <param name="rootEntities">Root entities that define the active scene.</param>
        public void BindScene(IReadOnlyList<Entity> rootEntities) {
            InnerWorld.BindScene(rootEntities);
        }

        /// <summary>
        /// Advances the backing world by one fixed simulation step.
        /// </summary>
        /// <param name="stepSeconds">Simulation step length in seconds.</param>
        public void Step(double stepSeconds) {
            InnerWorld.Step(stepSeconds);
        }
    }
}
