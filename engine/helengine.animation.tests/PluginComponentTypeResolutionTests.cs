using helengine.animation.runtime;
using helengine.editor;
using Xunit;

namespace helengine.animation.tests {
    /// <summary>
    /// Verifies that persisted components from engine plugin assemblies resolve while a project script type resolver is
    /// active. Platform builds always pass the project's resolver, which only knows the project's own code modules.
    /// </summary>
    public sealed class PluginComponentTypeResolutionTests {
        /// <summary>
        /// Ensures the animation player in a generated blueprint resolves when the registry carries a project script resolver
        /// that has no entry for the plugin assembly.
        /// </summary>
        [Fact]
        public void PluginComponent_resolvesBesideProjectScriptResolver() {
            ScriptTypeResolver projectScripts = new ScriptTypeResolver();
            projectScripts.Register("Caixinha", typeof(PluginComponentTypeResolutionTests).Assembly);
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry(projectScripts);

            Type resolved = registry.ResolveComponentType(typeof(HierarchyAnimationPlayerComponent).FullName + ", helengine.animation.runtime");

            Assert.Equal(typeof(HierarchyAnimationPlayerComponent), resolved);
        }
    }
}
