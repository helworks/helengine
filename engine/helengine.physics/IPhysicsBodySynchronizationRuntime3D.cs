namespace helengine {
    /// <summary>
    /// Exposes one shared runtime seam that pushes authored entity pose changes back into a live 3D physics simulation after initial scene binding.
    /// </summary>
    public interface IPhysicsBodySynchronizationRuntime3D {
        /// <summary>
        /// Synchronizes one authored kinematic body entity into the live physics runtime after gameplay code changes its transform or authored velocity state.
        /// </summary>
        /// <param name="entity">Authored entity whose bound kinematic body should receive the latest pose and velocity values.</param>
        void SynchronizeKinematicBody(Entity entity);

        /// <summary>
        /// Synchronizes one authored dynamic body entity into the live physics runtime after gameplay code teleports or otherwise rewrites its transform or authored velocity state.
        /// </summary>
        /// <param name="entity">Authored entity whose bound dynamic body should receive the latest pose and velocity values.</param>
        void SynchronizeDynamicBody(Entity entity);

        /// <summary>
        /// Synchronizes one authored dynamic body's velocity values into the live physics runtime while preserving the current runtime pose.
        /// </summary>
        /// <param name="entity">Authored entity whose bound dynamic body should receive the latest authored velocity values without a pose rewrite.</param>
        void SynchronizeDynamicBodyVelocity(Entity entity);
    }
}
