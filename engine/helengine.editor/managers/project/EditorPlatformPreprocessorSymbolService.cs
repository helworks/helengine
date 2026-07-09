using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Resolves shared preprocessor symbols used by gameplay and generated-core codegen.
    /// </summary>
    internal static class EditorPlatformPreprocessorSymbolService {
        /// <summary>
        /// Prefix applied to generated-core preprocessor symbols that indicate one runtime feature was force-disabled for the active codegen build.
        /// </summary>
        public const string DisabledFeatureSymbolPrefix = "HELENGINE_CODEGEN_FEATURE_DISABLED_";

        /// <summary>
        /// Preprocessor symbol used when generated runtimes resolve cooked platform-owned material assets.
        /// </summary>
        public const string RuntimeMaterialResolutionCookedPlatformOwnedSymbol = "HELENGINE_RUNTIME_MATERIAL_RESOLUTION_COOKED_PLATFORM_OWNED";

        /// <summary>
        /// Preprocessor symbol used when generated runtimes resolve cooked platform-owned texture assets.
        /// </summary>
        public const string RuntimeTextureResolutionCookedPlatformOwnedSymbol = "HELENGINE_RUNTIME_TEXTURE_RESOLUTION_COOKED_PLATFORM_OWNED";

        /// <summary>
        /// Preprocessor symbol used when generated runtimes resolve cooked platform-owned model assets.
        /// </summary>
        public const string RuntimeModelResolutionCookedPlatformOwnedSymbol = "HELENGINE_RUNTIME_MODEL_RESOLUTION_COOKED_PLATFORM_OWNED";

        /// <summary>
        /// Preprocessor symbol used when generated runtimes allow rooted packaged asset paths.
        /// </summary>
        public const string RuntimeAllowRootedPackagedPathsSymbol = "HELENGINE_RUNTIME_ALLOW_ROOTED_PACKAGED_PATHS";

        /// <summary>
        /// Preprocessor symbol used when generated runtimes support RenderManager2D texture-release flushing.
        /// </summary>
        public const string RuntimeSupportsRenderManager2DTextureReleaseFlushSymbol = "HELENGINE_RUNTIME_SUPPORTS_RENDER_MANAGER_2D_TEXTURE_RELEASE_FLUSH";

        /// <summary>
        /// Resolves the shared gameplay symbols that should be defined while compiling authored gameplay code.
        /// </summary>
        /// <param name="platformId">Stable platform identifier for the active build.</param>
        /// <returns>Ordered shared gameplay symbols.</returns>
        public static IReadOnlyList<string> ResolveGameplaySymbols(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            if (string.Equals(platformId, "windows", StringComparison.OrdinalIgnoreCase)) {
                return ["DESKTOP_PLATFORM"];
            }

            return [];
        }

        /// <summary>
        /// Resolves the generated-core input codegen symbols required by the supplied platform definition.
        /// </summary>
        /// <param name="platformDefinition">Typed platform metadata exposed by the active builder.</param>
        /// <returns>Ordered platform and generated-core symbols.</returns>
        public static IReadOnlyList<string> ResolvePortableInputSymbols(PlatformDefinition platformDefinition) {
            if (platformDefinition == null) {
                throw new ArgumentNullException(nameof(platformDefinition));
            }

            List<string> symbols = [];
            RuntimeGenerationContract runtimeGenerationContract = platformDefinition.RuntimeGenerationContract
                ?? throw new InvalidOperationException($"Platform '{platformDefinition.PlatformId}' must expose a runtime-generation contract.");
            if (runtimeGenerationContract.MaterialResolutionMode == RuntimeMaterialResolutionMode.CookedPlatformOwned) {
                symbols.Add(RuntimeMaterialResolutionCookedPlatformOwnedSymbol);
                symbols.Add(RuntimeTextureResolutionCookedPlatformOwnedSymbol);
                symbols.Add(RuntimeModelResolutionCookedPlatformOwnedSymbol);
            }
            if (runtimeGenerationContract.PackagedPathPolicy == PackagedPathPolicy.RootedOrContentRelative) {
                symbols.Add(RuntimeAllowRootedPackagedPathsSymbol);
            }
            if (runtimeGenerationContract.SupportsRenderManager2DTextureReleaseFlush) {
                symbols.Add(RuntimeSupportsRenderManager2DTextureReleaseFlushSymbol);
            }
            AddUniqueSymbols(symbols, runtimeGenerationContract.PortableInputPreprocessorSymbols);
            symbols.Add("HELENGINE_CODEGEN_DISABLE_MENU_REFLECTION");
            symbols.Add("HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION");
            return symbols;
        }

        /// <summary>
        /// Resolves generated-core preprocessor symbols that represent runtime features explicitly force-disabled for the active build.
        /// </summary>
        /// <param name="selectedCodegenOptionValues">Effective selected codegen option values that may carry one forced-disabled feature list.</param>
        /// <returns>Ordered feature-disabled symbols derived from the configured feature ids.</returns>
        public static IReadOnlyList<string> ResolveDisabledFeatureSymbols(IReadOnlyDictionary<string, string> selectedCodegenOptionValues) {
            if (selectedCodegenOptionValues == null) {
                throw new ArgumentNullException(nameof(selectedCodegenOptionValues));
            }
            if (!selectedCodegenOptionValues.TryGetValue(PlatformCodegenSettingIds.ForcedDisabledFeatures, out string disabledFeatureValue)
                || string.IsNullOrWhiteSpace(disabledFeatureValue)) {
                return [];
            }

            string[] featureIds = disabledFeatureValue.Split([';', ',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            List<string> symbols = [];
            for (int index = 0; index < featureIds.Length; index++) {
                string symbol = BuildDisabledFeatureSymbol(featureIds[index]);
                if (!string.IsNullOrWhiteSpace(symbol) && !symbols.Contains(symbol, StringComparer.Ordinal)) {
                    symbols.Add(symbol);
                }
            }

            return symbols;
        }

        /// <summary>
        /// Builds the stable generated-core preprocessor symbol used to indicate one runtime feature id was force-disabled.
        /// </summary>
        /// <param name="featureId">Stable runtime feature identifier.</param>
        /// <returns>Uppercase generated-core preprocessor symbol.</returns>
        public static string BuildDisabledFeatureSymbol(string featureId) {
            if (string.IsNullOrWhiteSpace(featureId)) {
                throw new ArgumentException("Feature id must be provided.", nameof(featureId));
            }

            char[] characters = featureId.Trim().ToUpperInvariant().ToCharArray();
            for (int index = 0; index < characters.Length; index++) {
                char character = characters[index];
                if ((character >= 'A' && character <= 'Z') || (character >= '0' && character <= '9')) {
                    continue;
                }

                characters[index] = '_';
            }

            return DisabledFeatureSymbolPrefix + new string(characters);
        }

        /// <summary>
        /// Adds each source symbol once while preserving the first-seen order.
        /// </summary>
        /// <param name="destination">Destination symbol list.</param>
        /// <param name="source">Source symbols to append.</param>
        static void AddUniqueSymbols(List<string> destination, IReadOnlyList<string> source) {
            if (destination == null) {
                throw new ArgumentNullException(nameof(destination));
            } else if (source == null) {
                throw new ArgumentNullException(nameof(source));
            }

            for (int index = 0; index < source.Count; index++) {
                string symbol = source[index];
                if (!destination.Contains(symbol, StringComparer.Ordinal)) {
                    destination.Add(symbol);
                }
            }
        }
    }
}
