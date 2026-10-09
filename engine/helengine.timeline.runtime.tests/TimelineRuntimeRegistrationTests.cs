using System.Reflection;

namespace helengine.timeline.runtime.tests {
    /// <summary>
    /// Verifies the opt-in module bootstrap: the generated-module manifest and the content processor registration.
    /// </summary>
    public sealed class TimelineRuntimeRegistrationTests {
        /// <summary>
        /// The assembly declares its generated runtime module, activated by the player component.
        /// </summary>
        [Fact]
        public void Assembly_declaresGeneratedRuntimeModule() {
            GeneratedRuntimeModuleManifestAttribute manifest = Assert.Single(
                typeof(TimelinePlayerComponent).Assembly.GetCustomAttributes<GeneratedRuntimeModuleManifestAttribute>());

            Assert.Equal("timeline-runtime-module", manifest.ModuleId);
            Assert.Equal(typeof(TimelineRuntimeRegistration), manifest.RegistrationType);
            Assert.Equal(nameof(TimelineRuntimeRegistration.Register), manifest.RegistrationMethodName);
            Assert.Equal(new Type[] { typeof(TimelinePlayerComponent) }, manifest.ActivationTypes);
        }

        /// <summary>
        /// Registering adds the cooked timeline processor once; repeating it is harmless.
        /// </summary>
        [Fact]
        public void Register_addsCookedTimelineProcessorIdempotently() {
            using Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            Assert.False(core.GetContentManager().IsProcessorRegistered(TimelineRuntimeRegistration.CookedTimelineProcessorId));

            TimelineRuntimeRegistration.Register(core);
            TimelineRuntimeRegistration.Register(core);

            Assert.True(core.GetContentManager().IsProcessorRegistered(TimelineRuntimeRegistration.CookedTimelineProcessorId));
        }
    }
}
