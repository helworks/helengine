using System.Reflection;
using helengine;
using Xunit;

namespace helengine.core.tests {
    /// <summary>
    /// Verifies the core-owned simulation gate stops fixed-step execution without retaining paused wall-clock time.
    /// </summary>
    public sealed class CorePhysicsSimulationPauseTests {
        [Fact]
        public void Paused_physics_simulation_does_not_step_or_accumulate_timing_debt() {
            Core core = CreateCoreWithPhysicsRuntime(out CountingPhysicsRuntime runtime);

            core.PhysicsSimulationIsPaused = true;
            UpdatePhysics(core, 1d);

            Assert.Equal(0, runtime.StepCount);
            Assert.Equal(0d, core.PhysicsScheduler.AccumulatedSeconds, 10);

            core.PhysicsSimulationIsPaused = false;
            UpdatePhysics(core, 1d / 60d);

            Assert.Equal(1, runtime.StepCount);
        }

        static Core CreateCoreWithPhysicsRuntime(out CountingPhysicsRuntime runtime) {
            Core core = new Core(new CoreInitializationOptions {
                PhysicsFixedStepSeconds = 1d / 60d,
                PhysicsMaxStepsPerUpdate = 8
            });
            SetPrivateField(core, "PhysicsSchedulerValue", new PhysicsFixedStepScheduler(1d / 60d));
            runtime = new CountingPhysicsRuntime();
            core.AttachPhysicsRuntime(runtime);
            return core;
        }

        static void UpdatePhysics(Core core, double elapsedSeconds) {
            MethodInfo method = typeof(Core).GetMethod("UpdatePhysics", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method.Invoke(core, [elapsedSeconds]);
        }

        static void SetPrivateField<T>(Core core, string fieldName, T value) {
            FieldInfo field = typeof(Core).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(core, value);
        }

        sealed class CountingPhysicsRuntime : IPhysicsRuntime {
            public int StepCount { get; private set; }

            public void Step(double stepSeconds) {
                StepCount++;
            }
        }
    }
}
