using helengine;
using Xunit;

namespace helengine.core.tests {
    /// <summary>
    /// Verifies the core only accepts physics runtimes that can take ownership of the loaded scene hierarchy.
    /// </summary>
    public sealed class CoreAttachPhysicsRuntimeTests {
        /// <summary>
        /// Ensures a runtime that implements only the fixed-step contract is rejected at attach time, instead of
        /// being accepted and then silently skipped by every caller that needs to bind a scene to it.
        /// </summary>
        [Fact]
        public void AttachPhysicsRuntime_WhenRuntimeCannotBindScenes_ThrowsInvalidOperationException() {
            Core core = CreateCore();
            StepOnlyPhysicsRuntime runtime = new StepOnlyPhysicsRuntime();

            Assert.Throws<InvalidOperationException>(() => core.AttachPhysicsRuntime(runtime));
            Assert.Null(core.PhysicsRuntime);
        }

        /// <summary>
        /// Ensures scene-bindable runtimes still attach normally.
        /// </summary>
        [Fact]
        public void AttachPhysicsRuntime_WhenRuntimeBindsScenes_AttachesRuntime() {
            Core core = CreateCore();
            SceneBindablePhysicsRuntime runtime = new SceneBindablePhysicsRuntime();

            core.AttachPhysicsRuntime(runtime);

            Assert.Same(runtime, core.PhysicsRuntime);
        }

        /// <summary>
        /// Creates one initialized core configured with a fixed physics schedule.
        /// </summary>
        /// <returns>Core instance used by attach tests.</returns>
        static Core CreateCore() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory),
                PhysicsFixedStepSeconds = 1d / 60d,
                PhysicsMaxStepsPerUpdate = 8
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            return core;
        }

        /// <summary>
        /// Physics runtime stub that deliberately implements only the fixed-step contract.
        /// </summary>
        sealed class StepOnlyPhysicsRuntime : IPhysicsRuntime {
            /// <summary>
            /// Ignores fixed-step calls; this stub exists only to be rejected at attach time.
            /// </summary>
            /// <param name="stepSeconds">Fixed simulation step length in seconds.</param>
            public void Step(double stepSeconds) {
            }
        }

        /// <summary>
        /// Physics runtime stub that satisfies the scene-binding contract the core requires.
        /// </summary>
        sealed class SceneBindablePhysicsRuntime : ISceneBindablePhysicsRuntime {
            /// <summary>
            /// Gets the number of runtime bodies bound by the most recent scene binding.
            /// </summary>
            public int RegisteredBodyCount { get; private set; }

            /// <summary>
            /// Records the bound scene hierarchy size.
            /// </summary>
            /// <param name="rootEntities">Root entities that define the active scene.</param>
            public void BindScene(IReadOnlyList<Entity> rootEntities) {
                if (rootEntities == null) {
                    throw new ArgumentNullException(nameof(rootEntities));
                }

                RegisteredBodyCount = rootEntities.Count;
            }

            /// <summary>
            /// Ignores fixed-step calls; this stub only needs to satisfy the attach contract.
            /// </summary>
            /// <param name="stepSeconds">Fixed simulation step length in seconds.</param>
            public void Step(double stepSeconds) {
            }
        }
    }
}
