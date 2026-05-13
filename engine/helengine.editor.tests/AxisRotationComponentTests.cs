using System.Reflection;
using helengine.editor.tests.testing;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the city-owned axis rotation gameplay component uses frame-rate-independent local-space rotation.
    /// </summary>
    public sealed class AxisRotationComponentTests {
        /// <summary>
        /// Absolute path to the generated city gameplay assembly used by player-facing component tests.
        /// </summary>
        const string CityGameplayAssemblyPath = @"C:\dev\helprojs\city\user_settings\generated_code\bin\gameplay\Debug\net9.0\gameplay.dll";

        /// <summary>
        /// Full type name used by the reusable city axis rotation component.
        /// </summary>
        const string AxisRotationComponentTypeName = "gameplay.rendering.AxisRotationComponent";

        /// <summary>
        /// Ensures equal simulated elapsed time reaches equivalent local orientation independent of frame count.
        /// </summary>
        [Fact]
        public void AxisRotationComponent_WithEquivalentElapsedTime_ReachesEquivalentOrientation() {
            float4 singleStepOrientation = RunAxisRotation(new[] { 0.5d });
            float4 manyStepOrientation = RunAxisRotation(CreateRepeatedStepSequence(30, 1.0d / 60.0d));

            AssertApproximatelyEqual(singleStepOrientation, manyStepOrientation, 0.001f);
        }

        /// <summary>
        /// Ensures one zero-length authored axis fails clearly instead of inventing a default rotation basis.
        /// </summary>
        [Fact]
        public void AxisRotationComponent_WithZeroAxis_ThrowsInvalidOperationException() {
            Core core = CreateCore();
            Entity entity = new Entity();
            entity.InitComponents();
            Component component = CreateAxisRotationComponent(float3.Zero, 1f);
            entity.AddComponent(component);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => core.Update(0.1d));
            Assert.Contains("non-zero axis", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Runs one axis rotation simulation using the supplied frame times and returns the final local orientation.
        /// </summary>
        /// <param name="elapsedSeconds">Elapsed frame seconds applied to the simulated runtime.</param>
        /// <returns>Final entity local orientation after every simulated update.</returns>
        float4 RunAxisRotation(IEnumerable<double> elapsedSeconds) {
            if (elapsedSeconds == null) {
                throw new ArgumentNullException(nameof(elapsedSeconds));
            }

            Core core = CreateCore();
            Entity entity = new Entity();
            entity.InitComponents();
            entity.AddComponent(CreateAxisRotationComponent(new float3(0f, 1f, 0f), (float)(Math.PI / 2.0)));

            foreach (double stepSeconds in elapsedSeconds) {
                core.Update(stepSeconds);
            }

            return entity.LocalOrientation;
        }

        /// <summary>
        /// Creates one initialized runtime core suitable for component update tests.
        /// </summary>
        /// <returns>Initialized core instance.</returns>
        Core CreateCore() {
            Core core = new Core(new CoreInitializationOptions());
            core.Initialize(null, new TestRenderManager2D(), new TestInputBackend(), TestPlatformInfo.Shared);
            return core;
        }

        /// <summary>
        /// Creates one axis rotation component instance from the generated city gameplay assembly.
        /// </summary>
        /// <param name="axis">Authored local rotation axis.</param>
        /// <param name="angularSpeedRadiansPerSecond">Authored angular speed in radians per second.</param>
        /// <returns>Created gameplay component cast to the engine component base type.</returns>
        Component CreateAxisRotationComponent(float3 axis, float angularSpeedRadiansPerSecond) {
            Assembly gameplayAssembly = LoadGameplayAssembly();
            Type componentType = gameplayAssembly.GetType(AxisRotationComponentTypeName, throwOnError: false);
            Assert.NotNull(componentType);

            object componentObject = Activator.CreateInstance(componentType);
            Assert.NotNull(componentObject);

            PropertyInfo axisProperty = componentType.GetProperty("Axis");
            PropertyInfo speedProperty = componentType.GetProperty("AngularSpeedRadiansPerSecond");
            Assert.NotNull(axisProperty);
            Assert.NotNull(speedProperty);

            axisProperty.SetValue(componentObject, axis);
            speedProperty.SetValue(componentObject, angularSpeedRadiansPerSecond);
            return Assert.IsAssignableFrom<Component>(componentObject);
        }

        /// <summary>
        /// Loads the generated city gameplay assembly used by player-facing behavior tests.
        /// </summary>
        /// <returns>Loaded gameplay assembly.</returns>
        Assembly LoadGameplayAssembly() {
            Assert.True(File.Exists(CityGameplayAssemblyPath));
            return Assembly.LoadFrom(CityGameplayAssemblyPath);
        }

        /// <summary>
        /// Creates one repeated elapsed-time sequence for frame-count-independence verification.
        /// </summary>
        /// <param name="count">Number of repeated steps to emit.</param>
        /// <param name="stepSeconds">Elapsed seconds stored in each emitted step.</param>
        /// <returns>Repeated elapsed-time sequence.</returns>
        double[] CreateRepeatedStepSequence(int count, double stepSeconds) {
            if (count < 1) {
                throw new ArgumentOutOfRangeException(nameof(count));
            } else if (stepSeconds <= 0d) {
                throw new ArgumentOutOfRangeException(nameof(stepSeconds));
            }

            double[] steps = new double[count];
            for (int index = 0; index < steps.Length; index++) {
                steps[index] = stepSeconds;
            }

            return steps;
        }

        /// <summary>
        /// Asserts two quaternions are approximately equal within the supplied component tolerance.
        /// </summary>
        /// <param name="expected">Expected quaternion.</param>
        /// <param name="actual">Actual quaternion.</param>
        /// <param name="tolerance">Maximum allowed absolute per-component difference.</param>
        void AssertApproximatelyEqual(float4 expected, float4 actual, float tolerance) {
            if (tolerance <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(tolerance));
            }

            Assert.InRange(Math.Abs(expected.X - actual.X), 0f, tolerance);
            Assert.InRange(Math.Abs(expected.Y - actual.Y), 0f, tolerance);
            Assert.InRange(Math.Abs(expected.Z - actual.Z), 0f, tolerance);
            Assert.InRange(Math.Abs(expected.W - actual.W), 0f, tolerance);
        }
    }
}
