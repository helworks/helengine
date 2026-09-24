using helengine.baseplatform.Results;

namespace helengine.baseplatform.Builders {
    /// <summary>Declares shader program pairs required by a platform renderer independently of authored materials.</summary>
    public interface IPlatformRendererShaderDependencyProvider {
        /// <summary>Gets the renderer-owned shader dependencies in deterministic first-request order.</summary>
        IReadOnlyList<PlatformShaderDependency> RendererShaderDependencies { get; }
    }
}
