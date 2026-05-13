using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies reusable editor slider interaction behavior.
    /// </summary>
    public class EditorSliderTests {
        /// <summary>
        /// Ensures pointer dragging updates slider value and raises the live change event.
        /// </summary>
        [Fact]
        public void SetNormalizedValue_WhenPointerDragMovesThumb_RaisesValueChangedWithMappedValue() {
            InitializeCore();
            EditorSlider slider = CreateSlider(0.01, 10.0, 0.1, EditorSliderScaleMode.Logarithmic);
            double? observedValue = null;
            slider.ValueChanged += value => observedValue = value;

            slider.SetNormalizedValue(0.5);

            Assert.NotNull(observedValue);
            Assert.InRange(observedValue.Value, 0.3, 0.4);
        }

        /// <summary>
        /// Ensures logarithmic mapping uses geometric interpolation across wide ranges.
        /// </summary>
        [Fact]
        public void SetNormalizedValue_WhenScaleModeIsLogarithmic_UsesLogarithmicMapping() {
            InitializeCore();
            EditorSlider slider = CreateSlider(1.0, 5000.0, 100.0, EditorSliderScaleMode.Logarithmic);

            slider.SetNormalizedValue(0.5);

            Assert.InRange(slider.Value, 60.0, 90.0);
        }

        /// <summary>
        /// Ensures keyboard adjustments move the slider by the configured step while preserving bounds.
        /// </summary>
        [Fact]
        public void AdjustFromKey_WhenArrowKeysArePressed_MovesValueWithinRange() {
            InitializeCore();
            EditorSlider slider = CreateSlider(0.01, 10.0, 0.5, EditorSliderScaleMode.Linear);

            slider.AdjustFromKey(Keys.Right);
            slider.AdjustFromKey(Keys.Left);
            slider.AdjustFromKey(Keys.Left);

            Assert.Equal(0.49, slider.Value, 2);
        }

        /// <summary>
        /// Creates one slider instance with the supplied range and scale mode.
        /// </summary>
        /// <param name="minimumValue">Minimum authored value.</param>
        /// <param name="maximumValue">Maximum authored value.</param>
        /// <param name="initialValue">Initial authored value.</param>
        /// <param name="scaleMode">Mapping mode used by the slider.</param>
        /// <returns>Slider configured for the requested test case.</returns>
        EditorSlider CreateSlider(double minimumValue, double maximumValue, double initialValue, EditorSliderScaleMode scaleMode) {
            return new EditorSlider(minimumValue, maximumValue, initialValue, scaleMode, 120, 10);
        }

        /// <summary>
        /// Initializes a minimal core so editor entities can allocate engine-side state during slider construction.
        /// </summary>
        void InitializeCore() {
            Core core = new Core(new CoreInitializationOptions {
                RenderList3DInitialCapacity = 4,
                RenderList2DInitialCapacity = 4
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }
    }
}
