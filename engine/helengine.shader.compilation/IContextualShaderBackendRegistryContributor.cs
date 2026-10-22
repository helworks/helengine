using helengine.baseplatform.Builders;

namespace helengine {
    /// <summary>Registers SDK-bound shader compilers for one captured build without consulting mutable process configuration again.</summary>
    public interface IContextualShaderBackendRegistryContributor {
        /// <summary>Binds compiler instances to the exact SDK snapshot used by the build cache and native packaging.</summary>
        void RegisterShaderBackends(ShaderBackendRegistry shaderBackendRegistry, PlatformCookContext cookContext);
    }
}
