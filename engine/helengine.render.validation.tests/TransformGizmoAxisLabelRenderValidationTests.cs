using System.Threading;
using Xunit;

namespace helengine.render.validation.tests {
    /// <summary>
    /// Executes image-based validation for the transform-gizmo axis labels so invisible billboard regressions fail automated tests.
    /// </summary>
    public class TransformGizmoAxisLabelRenderValidationTests {
        /// <summary>
        /// Ensures the DirectX11 validation run reports visible pixels for all translation-axis labels at the tested camera angles.
        /// </summary>
        [Fact]
        public void Run_DirectX11AxisLabelValidation_Passes() {
            string outputDirectory = Path.Combine(
                Path.GetTempPath(),
                "helengine-render-validation-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outputDirectory);

            try {
                var options = new RenderValidationOptions(
                    RenderBackendSelection.DirectX11,
                    outputDirectory,
                    384,
                    384,
                    6,
                    false,
                    1);
                IReadOnlyList<RenderValidationResult> results = null;
                Exception failure = null;

                Thread thread = new Thread(() => {
                    try {
                        var runner = new RenderValidationRunner(options);
                        results = runner.Run();
                    } catch (Exception ex) {
                        failure = ex;
                    }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();

                Assert.Null(failure);
                Assert.NotNull(results);
                Assert.Single(results);
                Assert.True(results[0].Passed, results[0].Message);
                Assert.Contains("AxisLabel=PASS", results[0].Message);
            } finally {
                if (Directory.Exists(outputDirectory)) {
                    Directory.Delete(outputDirectory, true);
                }
            }
        }
    }
}
