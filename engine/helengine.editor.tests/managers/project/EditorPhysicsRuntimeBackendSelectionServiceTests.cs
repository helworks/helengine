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
            string backend = EditorPhysicsRuntimeBackendSelectionService.Resolve(new Dictionary<string, string>());

            Assert.Equal(EditorPhysicsRuntimeBackendSelectionService.BepuValue, backend);
        }

        /// <summary>
        /// Ensures an explicit BEPU selection is retained as the canonical backend value.
        /// </summary>
        [Fact]
        public void Resolve_WhenBepuIsSelected_ReturnsBepu() {
            Dictionary<string, string> values = new() {
                [EditorPhysicsRuntimeBackendSelectionService.SettingId] = "bepu"
            };

            string backend = EditorPhysicsRuntimeBackendSelectionService.Resolve(values);

            Assert.Equal(EditorPhysicsRuntimeBackendSelectionService.BepuValue, backend);
        }

        /// <summary>
        /// Ensures an explicit HelPhysics selection is retained as the canonical backend value.
        /// </summary>
        [Fact]
        public void Resolve_WhenHelPhysicsIsSelected_ReturnsHelPhysics() {
            Dictionary<string, string> values = new() {
                [EditorPhysicsRuntimeBackendSelectionService.SettingId] = "helphysics"
            };

            string backend = EditorPhysicsRuntimeBackendSelectionService.Resolve(values);

            Assert.Equal(EditorPhysicsRuntimeBackendSelectionService.HelPhysicsValue, backend);
        }

        /// <summary>
        /// Ensures a present but blank selection fails instead of silently reverting to another runtime.
        /// </summary>
        [Fact]
        public void Resolve_WhenSelectionIsBlank_ThrowsSettingSpecificError() {
            Dictionary<string, string> values = new() {
                [EditorPhysicsRuntimeBackendSelectionService.SettingId] = " "
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => EditorPhysicsRuntimeBackendSelectionService.Resolve(values));

            Assert.Contains(EditorPhysicsRuntimeBackendSelectionService.SettingId, exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures an unknown backend fails before generated-core regeneration can produce an ambiguous runtime.
        /// </summary>
        [Fact]
        public void Resolve_WhenSelectionIsUnknown_ThrowsSettingSpecificError() {
            Dictionary<string, string> values = new() {
                [EditorPhysicsRuntimeBackendSelectionService.SettingId] = "unknown"
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => EditorPhysicsRuntimeBackendSelectionService.Resolve(values));

            Assert.Contains(EditorPhysicsRuntimeBackendSelectionService.SettingId, exception.Message, StringComparison.Ordinal);
            Assert.Contains("unknown", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures an omitted persisted option remains valid before profile defaults are applied.
        /// </summary>
        [Fact]
        public void ValidateExplicitSelection_WhenSettingIsAbsent_DoesNotThrow() {
            EditorPhysicsRuntimeBackendSelectionService.ValidateExplicitSelection(new Dictionary<string, string>());
        }

        /// <summary>
        /// Ensures a persisted blank value fails before generic profile-default resolution can replace it.
        /// </summary>
        [Fact]
        public void ValidateExplicitSelection_WhenSettingIsBlank_ThrowsSettingSpecificError() {
            Dictionary<string, string> values = new() {
                [EditorPhysicsRuntimeBackendSelectionService.SettingId] = " "
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => EditorPhysicsRuntimeBackendSelectionService.ValidateExplicitSelection(values));

            Assert.Contains(EditorPhysicsRuntimeBackendSelectionService.SettingId, exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures BEPU selection contributes only the BEPU generated-core symbol.
        /// </summary>
        [Fact]
        public void BuildPreprocessorSymbols_WhenBepuIsSelected_ReturnsOnlyBepuSymbol() {
            IReadOnlyList<string> symbols = EditorPhysicsRuntimeBackendSelectionService.BuildPreprocessorSymbols(
                EditorPhysicsRuntimeBackendSelectionService.BepuValue);

            Assert.Equal([EditorPhysicsRuntimeBackendSelectionService.BepuSymbol], symbols);
        }

        /// <summary>
        /// Ensures HelPhysics selection contributes only the HelPhysics generated-core symbol.
        /// </summary>
        [Fact]
        public void BuildPreprocessorSymbols_WhenHelPhysicsIsSelected_ReturnsOnlyHelPhysicsSymbol() {
            IReadOnlyList<string> symbols = EditorPhysicsRuntimeBackendSelectionService.BuildPreprocessorSymbols(
                EditorPhysicsRuntimeBackendSelectionService.HelPhysicsValue);

            Assert.Equal([EditorPhysicsRuntimeBackendSelectionService.HelPhysicsSymbol], symbols);
        }

        /// <summary>
        /// Ensures a final symbol graph containing the selected backend and no competitor is accepted.
        /// </summary>
        [Fact]
        public void ValidatePreprocessorSymbols_WhenOnlySelectedBackendIsPresent_DoesNotThrow() {
            IReadOnlyList<string> symbols = [
                "DESKTOP_PLATFORM",
                EditorPhysicsRuntimeBackendSelectionService.HelPhysicsSymbol
            ];

            EditorPhysicsRuntimeBackendSelectionService.ValidatePreprocessorSymbols(
                EditorPhysicsRuntimeBackendSelectionService.HelPhysicsValue,
                symbols);
        }

        /// <summary>
        /// Ensures a final symbol graph that identifies both runtimes is rejected before code generation.
        /// </summary>
        [Fact]
        public void ValidatePreprocessorSymbols_WhenConflictingBackendIsPresent_Throws() {
            IReadOnlyList<string> symbols = [
                EditorPhysicsRuntimeBackendSelectionService.BepuSymbol,
                EditorPhysicsRuntimeBackendSelectionService.HelPhysicsSymbol
            ];

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => EditorPhysicsRuntimeBackendSelectionService.ValidatePreprocessorSymbols(
                    EditorPhysicsRuntimeBackendSelectionService.HelPhysicsValue,
                    symbols));

            Assert.Contains("conflicting", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
