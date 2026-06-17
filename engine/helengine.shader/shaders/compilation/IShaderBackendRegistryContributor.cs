namespace helengine {
    /// <summary>
    /// Allows dynamically loaded extensions to contribute shader backends into the shared compile registry without creating compile-time editor dependencies on backend assemblies.
    /// </summary>
    public interface IShaderBackendRegistryContributor {
        /// <summary>
        /// Registers one or more shader backends into the supplied registry.
        /// </summary>
        /// <param name="shaderBackendRegistry">Registry that should receive the contributed shader backends.</param>
        void RegisterShaderBackends(ShaderBackendRegistry shaderBackendRegistry);
    }
}
