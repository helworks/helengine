namespace helengine {
    /// <summary>
    /// Preserves the legacy 3D physics registration entrypoint while forwarding runtime attachment to helengine.bepu.
    /// </summary>
    public static class Physics3DRuntimeComponentRegistration {
        /// <summary>
        /// Registers the packaged-scene component deserializers on one initialized core instance and attaches the BEPU-backed runtime.
        /// </summary>
        /// <param name="core">Initialized core that owns the runtime scene loader.</param>
        public static void Register(Core core) {
            BepuRuntimeComponentRegistration.Register(core);
        }
    }
}
