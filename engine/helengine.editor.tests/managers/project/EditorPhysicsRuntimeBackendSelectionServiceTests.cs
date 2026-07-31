using Xunit;

namespace helengine.editor.tests.managers.project {
    /// <summary>
    /// Verifies generated-core physics backend selection remains explicit, exclusive, and backward-compatible with existing BEPU builds.
    /// </summary>
    public sealed class EditorPhysicsRuntimeBackendSelectionServiceTests {
        /// <summary>
        /// Ensures builds created before the backend setting existed continue to select BEPU.
        /// </summary>
        [Fact]
        public void Resolve_WhenSettingIsAbsent_ReturnsBepu() {
            EditorPhysicsRuntimeBackendSelectionService service = new();

            string backend = service.Resolve(new Dictionary<string, string>());

            Assert.Equal(EditorPhysicsRuntimeBackendSelectionService.BepuValue, backend);
        }

        /// <summary>
        /// Ensures an explicit BEPU selection is retained as the canonical backend value.
        /// </summary>
        [Fact]
        public void Resolve_WhenBepuIsSelected_ReturnsBepu() {
            EditorPhysicsRuntimeBackendSelectionService service = new();
            Dictionary<string, string> values = new() {
                [EditorPhysicsRuntimeBackendSelectionService.SettingId] = "bepu"
            };

            string backend = service.Resolve(values);

            Assert.Equal(EditorPhysicsRuntimeBackendSelectionService.BepuValue, backend);
        }

        /// <summary>
        /// Ensures an explicit HelPhysics selection is retained as the canonical backend value.
        /// </summary>
        [Fact]
        public void Resolve_WhenHelPhysicsIsSelected_ReturnsHelPhysics() {
            EditorPhysicsRuntimeBackendSelectionService service = new();
            Dictionary<string, string> values = new() {
                [EditorPhysicsRuntimeBackendSelectionService.SettingId] = "helphysics"
            };

            string backend = service.Resolve(values);

            Assert.Equal(EditorPhysicsRuntimeBackendSelectionService.HelPhysicsValue, backend);
        }

        /// <summary>
        /// Ensures a present but blank selection fails instead of silently reverting to another runtime.
        /// </summary>
        [Fact]
        public void Resolve_WhenSelectionIsBlank_ThrowsSettingSpecificError() {
            EditorPhysicsRuntimeBackendSelectionService service = new();
            Dictionary<string, string> values = new() {
                [EditorPhysicsRuntimeBackendSelectionService.SettingId] = " "
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => service.Resolve(values));

            Assert.Contains(EditorPhysicsRuntimeBackendSelectionService.SettingId, exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures an unknown backend fails before generated-core regeneration can produce an ambiguous runtime.
        /// </summary>
        [Fact]
        public void Resolve_WhenSelectionIsUnknown_ThrowsSettingSpecificError() {
            EditorPhysicsRuntimeBackendSelectionService service = new();
            Dictionary<string, string> values = new() {
                [EditorPhysicsRuntimeBackendSelectionService.SettingId] = "unknown"
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => service.Resolve(values));

            Assert.Contains(EditorPhysicsRuntimeBackendSelectionService.SettingId, exception.Message, StringComparison.Ordinal);
            Assert.Contains("unknown", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures BEPU selection contributes only the BEPU generated-core symbol.
        /// </summary>
        [Fact]
        public void BuildPreprocessorSymbols_WhenBepuIsSelected_ReturnsOnlyBepuSymbol() {
            EditorPhysicsRuntimeBackendSelectionService service = new();

            IReadOnlyList<string> symbols = service.BuildPreprocessorSymbols(EditorPhysicsRuntimeBackendSelectionService.BepuValue);

            Assert.Equal([EditorPhysicsRuntimeBackendSelectionService.BepuSymbol], symbols);
        }

        /// <summary>
        /// Ensures HelPhysics selection contributes only the HelPhysics generated-core symbol.
        /// </summary>
        [Fact]
        public void BuildPreprocessorSymbols_WhenHelPhysicsIsSelected_ReturnsOnlyHelPhysicsSymbol() {
            EditorPhysicsRuntimeBackendSelectionService service = new();

            IReadOnlyList<string> symbols = service.BuildPreprocessorSymbols(EditorPhysicsRuntimeBackendSelectionService.HelPhysicsValue);

            Assert.Equal([EditorPhysicsRuntimeBackendSelectionService.HelPhysicsSymbol], symbols);
        }
    }
}
