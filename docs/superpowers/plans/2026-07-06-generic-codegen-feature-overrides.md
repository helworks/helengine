# Generic Codegen Feature Overrides Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move runtime feature-pruning policy onto a generic codegen option seam, keep preset ids as compatibility aliases backed by that same seam, and migrate the DS builder off the DS-specific preset.

**Architecture:** HelEngine exposes a generic codegen setting id for forced-disabled features and forwards it through the existing `--set key=value` path. `csharpcodegen` parses that generic option into `CPPBuildFeatureProfile`, then preset aliases contribute the same generic option defaults instead of mutating feature profiles directly. DS switches from `codegen-preset-id=ds-lite` to `codegen-forced-disabled-features=debug_overlay`, while preset compatibility and caller-owned preprocessor symbols remain intact.

**Tech Stack:** C#, xUnit, HelEngine editor build graph, `csharpcodegen`, PowerShell build harness, Nintendo DS platform builder

---

### Task 1: Add The Generic HelEngine Setting Id And Forwarding Test

**Files:**
- Modify: `C:\dev\helworks\helengine\engine\helengine.baseplatform\Definitions\PlatformCodegenSettingIds.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorGeneratedCoreRegenerationServiceTests.cs`

- [ ] **Step 1: Write the failing forwarding test**

Add this test to `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorGeneratedCoreRegenerationServiceTests.cs` near the existing `Build_arguments_includes_selected_preset_id` test:

```csharp
    /// <summary>
    /// Verifies the generated-core regeneration service forwards generic forced-disabled feature settings through the shared `--set` argument path.
    /// </summary>
    [Fact]
    public void Build_arguments_includes_forced_disabled_features_as_generic_set_option() {
        PlatformDefinition platformDefinition = CreatePlatformDefinition("ds", runtimeGenerationContract: null);
        PlatformCodegenProfileDefinition codegenProfile = CreateDefaultCodegenProfile();
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase) {
            [PlatformCodegenSettingIds.ForcedDisabledFeatures] = "debug_overlay;shaders"
        };

        IReadOnlyList<string> arguments = EditorGeneratedCoreRegenerationService.BuildArguments(
            @"C:\tmp\fixture.csproj",
            @"C:\tmp\generated",
            platformDefinition,
            codegenProfile,
            values,
            [],
            false);

        Assert.Contains("--set", arguments);
        Assert.Contains("codegen-forced-disabled-features=debug_overlay;shaders", arguments);
        Assert.DoesNotContain("--preset", arguments);
    }
```

- [ ] **Step 2: Run the targeted editor test and verify it fails**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter Build_arguments_includes_forced_disabled_features_as_generic_set_option -v q
```

Expected: FAIL at compile time because `PlatformCodegenSettingIds` does not yet define `ForcedDisabledFeatures`.

- [ ] **Step 3: Add the generic codegen setting id**

Update `C:\dev\helworks\helengine\engine\helengine.baseplatform\Definitions\PlatformCodegenSettingIds.cs` to:

```csharp
namespace helengine.baseplatform.Definitions {
    /// <summary>
    /// Defines stable setting identifiers used by platform codegen profiles.
    /// </summary>
    public static class PlatformCodegenSettingIds {
        /// <summary>
        /// Stable setting identifier for the named csharpcodegen conversion preset.
        /// </summary>
        public const string PresetId = "codegen-preset-id";

        /// <summary>
        /// Stable setting identifier for the generic forced-disabled feature list consumed by csharpcodegen.
        /// </summary>
        public const string ForcedDisabledFeatures = "codegen-forced-disabled-features";
    }
}
```

- [ ] **Step 4: Re-run the targeted editor test and verify it passes**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter Build_arguments_includes_forced_disabled_features_as_generic_set_option -v q
```

Expected: PASS.

- [ ] **Step 5: Commit the HelEngine setting-id task**

Run:

```powershell
git -C C:\dev\helworks\helengine add -- engine/helengine.baseplatform/Definitions/PlatformCodegenSettingIds.cs engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs
git -C C:\dev\helworks\helengine commit -m "Add generic codegen feature override setting"
```

### Task 2: Parse Generic Forced-Disabled Features In Codegen

**Files:**
- Create: `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPCodegenOptionNames.cs`
- Create: `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPFeatureProfileOptionResolver.cs`
- Modify: `C:\dev\helworks\csharpcodegen\codegen\CodegenCliOptionsBuilder.cs`
- Create: `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPFeatureProfileOptionResolverTests.cs`

- [ ] **Step 1: Write failing option-resolver tests**

Create `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPFeatureProfileOptionResolverTests.cs` with:

```csharp
namespace cs2.cpp.tests;

/// <summary>
/// Verifies generic codegen feature option parsing builds the expected feature profile.
/// </summary>
public sealed class CPPFeatureProfileOptionResolverTests {
    /// <summary>
    /// Ensures forced-disabled feature ids map to disabled feature-profile modes.
    /// </summary>
    [Fact]
    public void BuildProfile_with_forced_disabled_features_disables_each_feature() {
        CPPExternalFeatureCatalog featureCatalog = new CPPExternalFeatureCatalog(
            [
                new CPPExternalFeatureDefinition("debug_overlay", CPPFeatureMode.Auto, CPPFeatureConflictPolicy.Error),
                new CPPExternalFeatureDefinition("shaders", CPPFeatureMode.Auto, CPPFeatureConflictPolicy.Error),
                new CPPExternalFeatureDefinition("text2d", CPPFeatureMode.Auto, CPPFeatureConflictPolicy.Error)
            ],
            [],
            []);
        Dictionary<string, string> options = new(StringComparer.OrdinalIgnoreCase) {
            [CPPCodegenOptionNames.ForcedDisabledFeatures] = "debug_overlay; shaders ;debug_overlay"
        };

        CPPBuildFeatureProfile profile = CPPFeatureProfileOptionResolver.BuildProfile(options, featureCatalog);

        Assert.Equal(CPPFeatureMode.Disabled, profile.GetMode("debug_overlay", CPPFeatureMode.Auto));
        Assert.Equal(CPPFeatureMode.Disabled, profile.GetMode("shaders", CPPFeatureMode.Auto));
        Assert.Equal(CPPFeatureMode.Auto, profile.GetMode("text2d", CPPFeatureMode.Auto));
    }

    /// <summary>
    /// Ensures unknown forced-disabled feature ids fail fast instead of being silently ignored.
    /// </summary>
    [Fact]
    public void BuildProfile_with_unknown_forced_disabled_feature_throws() {
        CPPExternalFeatureCatalog featureCatalog = new CPPExternalFeatureCatalog(
            [
                new CPPExternalFeatureDefinition("debug_overlay", CPPFeatureMode.Auto, CPPFeatureConflictPolicy.Error)
            ],
            [],
            []);
        Dictionary<string, string> options = new(StringComparer.OrdinalIgnoreCase) {
            [CPPCodegenOptionNames.ForcedDisabledFeatures] = "missing_feature"
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => CPPFeatureProfileOptionResolver.BuildProfile(options, featureCatalog));

        Assert.Contains("missing_feature", exception.Message, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run the new option-resolver tests and verify they fail**

Run:

```powershell
rtk dotnet test C:\dev\helworks\csharpcodegen\cs2.cpp.tests\cs2.cpp.tests.csproj --filter CPPFeatureProfileOptionResolverTests -v q
```

Expected: FAIL because `CPPCodegenOptionNames` and `CPPFeatureProfileOptionResolver` do not exist.

- [ ] **Step 3: Implement the generic option-name constant and feature-profile resolver**

Create `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPCodegenOptionNames.cs`:

```csharp
namespace cs2.cpp {
    /// <summary>
    /// Defines stable generic codegen option names consumed by the C++ backend.
    /// </summary>
    public static class CPPCodegenOptionNames {
        /// <summary>
        /// Gets the generic option name that forces selected runtime features off.
        /// </summary>
        public const string ForcedDisabledFeatures = "codegen-forced-disabled-features";
    }
}
```

Create `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPFeatureProfileOptionResolver.cs`:

```csharp
namespace cs2.cpp {
    /// <summary>
    /// Resolves generic codegen option values into build-feature profile overrides.
    /// </summary>
    public static class CPPFeatureProfileOptionResolver {
        /// <summary>
        /// Builds one feature profile from generic selected options and the active external feature catalog.
        /// </summary>
        /// <param name="selectedOptions">Caller-selected generic option values.</param>
        /// <param name="featureCatalog">Loaded external feature catalog used to validate feature ids.</param>
        /// <returns>Resolved feature profile.</returns>
        public static CPPBuildFeatureProfile BuildProfile(
            IReadOnlyDictionary<string, string> selectedOptions,
            CPPExternalFeatureCatalog featureCatalog) {
            if (selectedOptions == null) {
                throw new ArgumentNullException(nameof(selectedOptions));
            }
            if (featureCatalog == null) {
                throw new ArgumentNullException(nameof(featureCatalog));
            }

            CPPBuildFeatureProfile profile = CPPBuildFeatureProfile.CreateDefault();
            if (!selectedOptions.TryGetValue(CPPCodegenOptionNames.ForcedDisabledFeatures, out string serializedFeatureIds)
                || string.IsNullOrWhiteSpace(serializedFeatureIds)) {
                return profile;
            }

            HashSet<string> knownFeatureIds = new(
                featureCatalog.Features.Select(feature => feature.Id),
                StringComparer.Ordinal);
            HashSet<string> addedFeatureIds = new(StringComparer.Ordinal);
            string[] featureIds = serializedFeatureIds.Split(
                [',', ';', ' '],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (int index = 0; index < featureIds.Length; index++) {
                string featureId = featureIds[index];
                if (!addedFeatureIds.Add(featureId)) {
                    continue;
                }
                if (!knownFeatureIds.Contains(featureId)) {
                    throw new InvalidOperationException($"Unknown forced-disabled codegen feature '{featureId}'.");
                }

                profile.WithMode(featureId, CPPFeatureMode.Disabled);
            }

            return profile;
        }
    }
}
```

Update the feature-profile assignment in `C:\dev\helworks\csharpcodegen\codegen\CodegenCliOptionsBuilder.cs` to:

```csharp
        options.FeatureCatalog = LoadFeatureCatalog(parsedArguments.FeatureCatalogPath);
        options.CollectDiagnostics = true;
        options.FailOnError = true;
        options.IncludeProjectDefinedPreprocessorSymbols = true;
        options.LoadNativeRuntimeMetadata = true;
        options.WriteConversionReport = true;
        options.WindowsHandoffOutputFolder = parsedArguments.OutputFolder;
```

and replace the bottom of `CreateConversionOptions` with:

```csharp
        options.PlatformOptionValues = new Dictionary<string, string>(parsedArguments.SelectedOptions, StringComparer.OrdinalIgnoreCase);
        options.BuildFeatureProfile = CPPFeatureProfileOptionResolver.BuildProfile(options.PlatformOptionValues, options.FeatureCatalog);

        return options;
```

- [ ] **Step 4: Re-run the option-resolver tests and verify they pass**

Run:

```powershell
rtk dotnet test C:\dev\helworks\csharpcodegen\cs2.cpp.tests\cs2.cpp.tests.csproj --filter CPPFeatureProfileOptionResolverTests -v q
```

Expected: PASS.

- [ ] **Step 5: Commit the generic option parser task**

Run:

```powershell
git -C C:\dev\helworks\csharpcodegen add -- cs2.cpp/CPPCodegenOptionNames.cs cs2.cpp/CPPFeatureProfileOptionResolver.cs codegen/CodegenCliOptionsBuilder.cs cs2.cpp.tests/CPPFeatureProfileOptionResolverTests.cs
git -C C:\dev\helworks\csharpcodegen commit -m "Parse generic forced-disabled codegen features"
```

### Task 3: Refactor Preset Aliases To Use Generic Option Defaults

**Files:**
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp\model\CPPConversionPreset.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPConversionPresetCatalog.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPConversionPresetCatalogTests.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPFeaturePruningEndToEndTests.cs`

- [ ] **Step 1: Write the failing preset-compatibility tests**

Add this test to `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPConversionPresetCatalogTests.cs`:

```csharp
    /// <summary>
    /// Ensures preset aliases contribute generic forced-disabled feature defaults instead of mutating feature profiles directly.
    /// </summary>
    [Fact]
    public void ApplyTo_PresetAlias_uses_generic_forced_disabled_feature_option() {
        CPPConversionOptions options = new CPPConversionOptions {
            PresetId = "ps2-lite",
            FeatureCatalog = new CPPExternalFeatureCatalog(
                [
                    new CPPExternalFeatureDefinition("debug_overlay", CPPFeatureMode.Auto, CPPFeatureConflictPolicy.Error),
                    new CPPExternalFeatureDefinition("shaders", CPPFeatureMode.Auto, CPPFeatureConflictPolicy.Error)
                ],
                [],
                []),
            PlatformOptionValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };

        new CPPConversionPresetCatalog().ApplyTo(options);

        Assert.Equal(CPPFeatureMode.Disabled, options.BuildFeatureProfile.GetMode("debug_overlay", CPPFeatureMode.Auto));
        Assert.Equal(CPPFeatureMode.Disabled, options.BuildFeatureProfile.GetMode("shaders", CPPFeatureMode.Auto));
        Assert.Equal("shaders;debug_overlay", options.PlatformOptionValues[CPPCodegenOptionNames.ForcedDisabledFeatures]);
    }

    /// <summary>
    /// Ensures the N64 compatibility preset routes all of its feature pruning through the generic forced-disabled option.
    /// </summary>
    [Fact]
    public void ApplyTo_N64Minimal_uses_generic_forced_disabled_feature_option() {
        CPPConversionOptions options = new CPPConversionOptions {
            PresetId = "n64-minimal",
            FeatureCatalog = new CPPExternalFeatureCatalog(
                [
                    new CPPExternalFeatureDefinition("debug_overlay", CPPFeatureMode.Auto, CPPFeatureConflictPolicy.Error),
                    new CPPExternalFeatureDefinition("shaders", CPPFeatureMode.Auto, CPPFeatureConflictPolicy.Error),
                    new CPPExternalFeatureDefinition("render2d", CPPFeatureMode.Auto, CPPFeatureConflictPolicy.Error),
                    new CPPExternalFeatureDefinition("text2d", CPPFeatureMode.Auto, CPPFeatureConflictPolicy.Error)
                ],
                [],
                []),
            PlatformOptionValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };

        new CPPConversionPresetCatalog().ApplyTo(options);

        Assert.Equal(CPPFeatureMode.Disabled, options.BuildFeatureProfile.GetMode("debug_overlay", CPPFeatureMode.Auto));
        Assert.Equal(CPPFeatureMode.Disabled, options.BuildFeatureProfile.GetMode("shaders", CPPFeatureMode.Auto));
        Assert.Equal(CPPFeatureMode.Disabled, options.BuildFeatureProfile.GetMode("render2d", CPPFeatureMode.Auto));
        Assert.Equal(CPPFeatureMode.Disabled, options.BuildFeatureProfile.GetMode("text2d", CPPFeatureMode.Auto));
        Assert.Equal("shaders;debug_overlay;render2d;text2d", options.PlatformOptionValues[CPPCodegenOptionNames.ForcedDisabledFeatures]);
    }
```

Update the existing resolve tests in `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPConversionPresetCatalogTests.cs` so they assert generic option defaults instead of preset-owned feature profiles:

```csharp
    /// <summary>
    /// Ensures the Windows no-shaders preset resolves to the expected compiler, platform, generic option, and restriction settings.
    /// </summary>
    [Fact]
    public void Resolve_WindowsNoShaders_UsesNamedPresetProfiles() {
        CPPConversionPreset preset = new CPPConversionPresetCatalog().Resolve("windows-no-shaders");

        Assert.Equal("windows-no-shaders", preset.Id);
        Assert.Equal("msvc", preset.CompilerProfile.Name);
        Assert.Equal("windows-headless", preset.PlatformProfile.Name);
        Assert.Equal("stl-lite", preset.RuntimeProfile.Name);
        Assert.Equal("shaders", preset.PlatformOptionValues[CPPCodegenOptionNames.ForcedDisabledFeatures]);
        Assert.Equal("desktop-no-shaders", preset.RestrictionProfile.Name);
        Assert.True(preset.RestrictionProfile.ForbidShaders);
    }

    /// <summary>
    /// Ensures the stripped native core-boot preset resolves to the expected compiler, platform, generic option, and restriction settings.
    /// </summary>
    [Fact]
    public void Resolve_NativeCoreBoot_UsesNamedPresetProfiles() {
        CPPConversionPreset preset = new CPPConversionPresetCatalog().Resolve("native-core-boot");

        Assert.Equal("native-core-boot", preset.Id);
        Assert.Equal("gcc", preset.CompilerProfile.Name);
        Assert.Equal("retroppc-headless", preset.PlatformProfile.Name);
        Assert.Equal("stl-lite", preset.RuntimeProfile.Name);
        Assert.Equal("shaders;debug_overlay", preset.PlatformOptionValues[CPPCodegenOptionNames.ForcedDisabledFeatures]);
        Assert.Equal("native-core-boot", preset.RestrictionProfile.Name);
        Assert.True(preset.RestrictionProfile.ForbidShaders);
        Assert.True(preset.RestrictionProfile.ForbidDebugOnlySystems);
    }
```

Replace the DS preset end-to-end test in `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPFeaturePruningEndToEndTests.cs` with a generic-option test:

```csharp
    /// <summary>
    /// Verifies generic forced-disabled feature options preserve DS runtime shape while disabling the debug overlay.
    /// </summary>
    [Fact]
    public void WriteOutput_WhenForcedDisabledFeaturesDisableDebugOverlay_PreservesDsConfig() {
        string source = """
namespace ExampleEngine {
    public class DebugOverlayComponent {
    }
}
""";

        string outputPath = RunConversion(
            source,
            CPPBuildFeatureProfile.CreateDefault(),
            string.Empty,
            options => {
                options.CompilerProfile = CPPCompilerProfile.CreateGcc();
                options.PlatformProfile = CPPPlatformProfile.CreateNintendoDsHeadless();
                options.RuntimeProfile = CPPRuntimeProfile.CreateStlLite();
                options.IncludeProjectDefinedPreprocessorSymbols = false;
                options.PlatformOptionValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                    [CPPCodegenOptionNames.ForcedDisabledFeatures] = "debug_overlay"
                };
                options.BuildFeatureProfile = CPPFeatureProfileOptionResolver.BuildProfile(options.PlatformOptionValues, options.FeatureCatalog);
            });
        string config = File.ReadAllText(Path.Combine(outputPath, "helcpp_config.hpp"));

        Assert.Contains("#define HE_CPP_PLATFORM_DS 1", config);
        Assert.Contains("#define HE_CPP_FEATURE_DEBUG_OVERLAY 0", config);
        Assert.False(File.Exists(Path.Combine(outputPath, "DebugOverlayComponent.hpp")));
        Assert.False(File.Exists(Path.Combine(outputPath, "DebugOverlayComponent.cpp")));
    }
```

- [ ] **Step 2: Run the focused preset tests and verify they fail**

Run:

```powershell
rtk dotnet test C:\dev\helworks\csharpcodegen\cs2.cpp.tests\cs2.cpp.tests.csproj --filter ApplyTo_PresetAlias_uses_generic_forced_disabled_feature_option -v q
rtk dotnet test C:\dev\helworks\csharpcodegen\cs2.cpp.tests\cs2.cpp.tests.csproj --filter ApplyTo_N64Minimal_uses_generic_forced_disabled_feature_option -v q
rtk dotnet test C:\dev\helworks\csharpcodegen\cs2.cpp.tests\cs2.cpp.tests.csproj --filter WriteOutput_WhenForcedDisabledFeaturesDisableDebugOverlay_PreservesDsConfig -v q
```

Expected: FAIL because presets do not yet populate generic option defaults and the generic-option DS end-to-end path is not yet rebuilding `BuildFeatureProfile` from `PlatformOptionValues`.

- [ ] **Step 3: Implement preset option-default merging and migrate preset definitions**

Extend `C:\dev\helworks\csharpcodegen\cs2.cpp\model\CPPConversionPreset.cs` with:

```csharp
        /// <summary>
        /// Gets or sets generic option defaults contributed by the preset before caller-selected overrides are applied.
        /// </summary>
        public IReadOnlyDictionary<string, string> PlatformOptionValues { get; set; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
```

Update `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPConversionPresetCatalog.cs` so `ApplyTo` merges preset option defaults before rebuilding `BuildFeatureProfile`:

```csharp
            options.PlatformOptionValues = MergePlatformOptionValues(
                preset.PlatformOptionValues,
                options.PlatformOptionValues);
            options.BuildFeatureProfile = CPPFeatureProfileOptionResolver.BuildProfile(
                options.PlatformOptionValues,
                options.FeatureCatalog);
```

Add this helper to the same file:

```csharp
        static IReadOnlyDictionary<string, string> MergePlatformOptionValues(
            IReadOnlyDictionary<string, string> presetValues,
            IReadOnlyDictionary<string, string> callerValues) {
            Dictionary<string, string> mergedValues = new(StringComparer.OrdinalIgnoreCase);
            AppendOptionValues(mergedValues, presetValues);
            AppendOptionValues(mergedValues, callerValues);
            return mergedValues;
        }

        static void AppendOptionValues(
            Dictionary<string, string> destination,
            IReadOnlyDictionary<string, string> values) {
            if (destination == null) {
                throw new ArgumentNullException(nameof(destination));
            }
            if (values == null) {
                return;
            }

            foreach (KeyValuePair<string, string> pair in values) {
                if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value)) {
                    continue;
                }

                destination[pair.Key] = pair.Value;
            }
        }
```

Replace preset-owned direct feature-profile construction with generic option defaults. For example, `CreatePlayStation2LitePreset` becomes:

```csharp
        static CPPConversionPreset CreatePlayStation2LitePreset() {
            return new CPPConversionPreset {
                Id = "ps2-lite",
                CompilerProfile = CPPCompilerProfile.CreateGcc(),
                PlatformProfile = CPPPlatformProfile.CreatePlayStation2Headless(),
                RuntimeProfile = CPPRuntimeProfile.CreateCustomRetro(),
                RestrictionProfile = new CPPRestrictionProfile {
                    Name = "ps2-lite",
                    ForbidShaders = true,
                    ForbidRuntimeJson = true,
                    ForbidReflectionLikeRuntime = true,
                    ForbidRegex = true,
                    ForbidDebugOnlySystems = true
                },
                IncludeProjectDefinedPreprocessorSymbols = false,
                AdditionalPreprocessorSymbols = new[] {
                    "HELENGINE_RUNTIME_MATERIAL_RESOLUTION_COOKED_PLATFORM_OWNED",
                    "HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION",
                    "HELENGINE_CODEGEN_DISABLE_MENU_REFLECTION"
                },
                PlatformOptionValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                    [CPPCodegenOptionNames.ForcedDisabledFeatures] = "shaders;debug_overlay"
                }
            };
        }
```

Apply the same pattern to:

- `CreateWindowsNoShadersPreset` with `shaders`
- `CreateNintendoDsLitePreset` with `debug_overlay`
- `CreateNativeCoreBootPreset` with `shaders;debug_overlay`
- `CreateNintendo64MinimalPreset` with `shaders;debug_overlay;render2d;text2d`

Keep the existing additional-preprocessor-symbol merge behavior unchanged.

- [ ] **Step 4: Re-run the preset compatibility tests and verify they pass**

Run:

```powershell
rtk dotnet test C:\dev\helworks\csharpcodegen\cs2.cpp.tests\cs2.cpp.tests.csproj --filter ApplyTo_PresetAlias_uses_generic_forced_disabled_feature_option -v q
rtk dotnet test C:\dev\helworks\csharpcodegen\cs2.cpp.tests\cs2.cpp.tests.csproj --filter ApplyTo_N64Minimal_uses_generic_forced_disabled_feature_option -v q
rtk dotnet test C:\dev\helworks\csharpcodegen\cs2.cpp.tests\cs2.cpp.tests.csproj --filter WriteOutput_WhenForcedDisabledFeaturesDisableDebugOverlay_PreservesDsConfig -v q
rtk dotnet test C:\dev\helworks\csharpcodegen\cs2.cpp.tests\cs2.cpp.tests.csproj --filter ApplyTo_DsLite_PreservesCallerProvidedPreprocessorSymbols -v q
rtk dotnet test C:\dev\helworks\csharpcodegen\cs2.cpp.tests\cs2.cpp.tests.csproj --filter Resolve_WindowsNoShaders_UsesNamedPresetProfiles -v q
rtk dotnet test C:\dev\helworks\csharpcodegen\cs2.cpp.tests\cs2.cpp.tests.csproj --filter Resolve_NativeCoreBoot_UsesNamedPresetProfiles -v q
rtk dotnet test C:\dev\helworks\csharpcodegen\cs2.cpp.tests\cs2.cpp.tests.csproj --filter WriteOutput_WhenPresetForbidsRuntimeJson_FailsBeforeCopyingOutput -v q
rtk dotnet test C:\dev\helworks\csharpcodegen\cs2.cpp.tests\cs2.cpp.tests.csproj --filter WriteOutput_WhenPresetIsNativeCoreBoot_WritesGenericConfigAndDisablesShaders -v q
```

Expected: PASS.

- [ ] **Step 5: Commit the preset alias refactor**

Run:

```powershell
git -C C:\dev\helworks\csharpcodegen add -- cs2.cpp/model/CPPConversionPreset.cs cs2.cpp/CPPConversionPresetCatalog.cs cs2.cpp.tests/CPPConversionPresetCatalogTests.cs cs2.cpp.tests/CPPFeaturePruningEndToEndTests.cs
git -C C:\dev\helworks\csharpcodegen commit -m "Refactor preset aliases onto generic feature options"
```

### Task 4: Migrate DS To The Generic Setting And Verify The Full Build

**Files:**
- Modify: `C:\dev\helworks\helengine-ds\builder\NintendoDsPlatformDefinitionFactory.cs`
- Modify: `C:\dev\helworks\helengine-ds\builder.tests\NintendoDsPlatformDefinitionFactoryTests.cs`

- [ ] **Step 1: Write the failing DS platform-definition test**

Replace the current DS preset-default test in `C:\dev\helworks\helengine-ds\builder.tests\NintendoDsPlatformDefinitionFactoryTests.cs` with:

```csharp
    /// <summary>
    /// Verifies the Nintendo DS codegen profile defaults to the generic forced-disabled debug-overlay feature setting.
    /// </summary>
    [Fact]
    public void Create_sets_generic_forced_disabled_feature_setting_by_default() {
        PlatformDefinition definition = NintendoDsPlatformDefinitionFactory.Create();

        PlatformCodegenProfileDefinition codegenProfile = Assert.Single(definition.CodegenProfiles);
        PlatformSettingDefinition disabledFeatureSetting = Assert.Single(
            codegenProfile.Settings.Where(candidate => candidate.SettingId == PlatformCodegenSettingIds.ForcedDisabledFeatures));

        Assert.Equal(PlatformSettingKind.Text, disabledFeatureSetting.SettingKind);
        Assert.Equal("debug_overlay", disabledFeatureSetting.DefaultValue);
    }
```

- [ ] **Step 2: Run the DS platform-definition test and verify it fails**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine-ds\builder.tests\helengine.ds.builder.tests.csproj --filter Create_sets_generic_forced_disabled_feature_setting_by_default -v q
```

Expected: FAIL because the DS builder still publishes `codegen-preset-id=ds-lite`.

- [ ] **Step 3: Switch DS to the generic setting**

Replace the DS codegen profile settings in `C:\dev\helworks\helengine-ds\builder\NintendoDsPlatformDefinitionFactory.cs` with:

```csharp
            [
                new PlatformCodegenProfileDefinition(
                    "default",
                    "Default",
                    "Nintendo DS C# to C++ codegen profile",
                    PlatformCodegenLanguage.Cpp,
                    PlatformSerializationEndianness.LittleEndian,
                    [
                        new PlatformSettingDefinition(
                            PlatformCodegenSettingIds.ForcedDisabledFeatures,
                            "Forced Disabled Features",
                            PlatformSettingKind.Text,
                            "debug_overlay",
                            true,
                            [])
                    ])
            ],
```

- [ ] **Step 4: Re-run DS tests and the full DS build**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine-ds\builder.tests\helengine.ds.builder.tests.csproj --filter Create_sets_generic_forced_disabled_feature_setting_by_default -v q
powershell -NoProfile -ExecutionPolicy Bypass -File C:\dev\helworks\helengine\artifacts\build-platform.ps1 -Project C:\dev\helprojs\city\project.heproj -Platform ds -Output C:\dev\helprojs\city\ds-generic-feature-overrides -Configuration Release
```

Expected:

- the DS builder test PASSes
- the full DS build succeeds
- the editor build summary reports `debug_overlay (ForcedDisabled)`

- [ ] **Step 5: Commit the DS migration**

Run:

```powershell
git -C C:\dev\helworks\helengine-ds add -- builder/NintendoDsPlatformDefinitionFactory.cs builder.tests/NintendoDsPlatformDefinitionFactoryTests.cs
git -C C:\dev\helworks\helengine-ds commit -m "Use generic codegen feature override for DS"
```

### Task 5: Final Cross-Repo Verification

**Files:**
- Modify: none
- Test: `C:\dev\helworks\helengine`, `C:\dev\helworks\csharpcodegen`, `C:\dev\helworks\helengine-ds`

- [ ] **Step 1: Run the focused HelEngine verification**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter Build_arguments_includes_forced_disabled_features_as_generic_set_option -v q
```

Expected: PASS.

- [ ] **Step 2: Run the focused csharpcodegen verification**

Run:

```powershell
rtk dotnet test C:\dev\helworks\csharpcodegen\cs2.cpp.tests\cs2.cpp.tests.csproj --filter CPPFeatureProfileOptionResolverTests -v q
rtk dotnet test C:\dev\helworks\csharpcodegen\cs2.cpp.tests\cs2.cpp.tests.csproj --filter ApplyTo_PresetAlias_uses_generic_forced_disabled_feature_option -v q
rtk dotnet test C:\dev\helworks\csharpcodegen\cs2.cpp.tests\cs2.cpp.tests.csproj --filter ApplyTo_DsLite_PreservesCallerProvidedPreprocessorSymbols -v q
```

Expected: PASS.

- [ ] **Step 3: Run the DS integration verification**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine-ds\builder.tests\helengine.ds.builder.tests.csproj --filter Create_sets_generic_forced_disabled_feature_setting_by_default -v q
Get-Item C:\dev\helprojs\city\ds-generic-feature-overrides\helengine_ds.nds | Select-Object FullName,Length
```

Expected:

- the DS test PASSes
- the `.nds` file exists
- the reported size is no larger than the current DS release artifact unless another unrelated change landed during the run

- [ ] **Step 4: Record the final state in the implementation notes commit**

Run:

```powershell
git -C C:\dev\helworks\helengine status --short
git -C C:\dev\helworks\csharpcodegen status --short
git -C C:\dev\helworks\helengine-ds status --short
```

Expected: only the intended tracked changes remain in each repository.
