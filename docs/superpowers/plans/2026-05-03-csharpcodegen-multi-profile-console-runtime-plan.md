# CSharpCodegen Multi-Profile Console Runtime Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add named `csharpcodegen` conversion presets, strict runtime restriction enforcement, type-scoped runtime helper includes, and Helengine-side preset selection so low-footprint builds can omit forbidden runtime systems instead of merely leaving them unused.

**Architecture:** Keep authored C# shared and move native-shape control into `csharpcodegen` preset resolution. A preset composes the existing compiler/platform/runtime/feature profiles plus a new restriction profile, and the converter validates reachable features and runtime helpers against that restriction set before copying runtime files or emitting reports. Helengine selects the preset through a dedicated codegen setting id, passes it to the `codegen` CLI explicitly, and continues to use the existing codegen-settings UI and persistence flow instead of introducing a second editor-specific selection surface.

**Tech Stack:** C#, .NET, Roslyn, xUnit, `cs2.cpp`, `codegen`, Helengine editor build services

---

## File Structure

### Existing files to modify

- `C:\dev\helworks\csharpcodegen\codegen\Program.cs`
  - Parse a new preset argument and resolve `CPPConversionOptions` from a named preset.
- `C:\dev\helworks\csharpcodegen\cs2.cpp\model\CPPConversionOptions.cs`
  - Carry the selected preset id and resolved restriction profile.
- `C:\dev\helworks\csharpcodegen\cs2.cpp\model\CPPFeatureKind.cs`
  - Expand the feature buckets used for pruning and restriction validation.
- `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPFeatureCatalog.cs`
  - Map runtime JSON, reflection-like runtime, host filesystem, and shader-adjacent roots into explicit feature buckets.
- `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPCodeConverter.cs`
  - Resolve presets, validate restrictions, and fail before runtime copying when forbidden systems are reachable.
- `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPRuntimeRequirementCatalog.cs`
  - Classify runtime helpers under the expanded feature buckets.
- `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPRuntimeRequirementRegistrar.cs`
  - Track global registration separately from per-type runtime helper requirements.
- `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPClassEmitter.cs`
  - Emit runtime helper includes per type instead of per conversion run.
- `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPConversionReportWriter.cs`
  - Record the resolved preset id and active restriction profile in `cpp-conversion-report.json`.
- `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorGeneratedCoreRegenerationService.cs`
  - Forward the selected preset id to `codegen`.
- `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorBuildQueueItemFactory.cs`
  - Ensure the dedicated preset setting is defaulted into the selected codegen option values.
- `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\BuildDialog.cs`
  - Continue defaulting codegen setting values so preset choice is selectable from Helengine.

### New production files to create

- `C:\dev\helworks\csharpcodegen\cs2.cpp\model\CPPRestrictionProfile.cs`
- `C:\dev\helworks\csharpcodegen\cs2.cpp\model\CPPConversionPreset.cs`
- `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPConversionPresetCatalog.cs`
- `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPRestrictionValidator.cs`
- `C:\dev\helworks\csharpcodegen\cs2.cpp\model\CPPRestrictionValidationResult.cs`
- `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPTypeRuntimeRequirementScope.cs`
- `C:\dev\helworks\helengine\engine\helengine.baseplatform\Definitions\PlatformCodegenSettingIds.cs`

### Existing test files to modify

- `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPConversionOptionsTests.cs`
- `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPFeatureProfileTests.cs`
- `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPFeatureOwnedRuntimeRequirementTests.cs`
- `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPFeaturePruningEndToEndTests.cs`
- `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPHelengineCoreAuditTests.cs`
- `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPClassEmitterIncludeFilteringTests.cs`
- `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorGeneratedCoreRegenerationServiceTests.cs`
- `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorBuildQueueItemFactoryTests.cs`

### New test files to create

- `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPConversionPresetCatalogTests.cs`
- `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPRestrictionValidatorTests.cs`
- `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPClassEmitterRuntimeRequirementScopeTests.cs`

## Notes for the implementer

- Use two isolated worktrees. Helengine plan/spec work already lives in `C:\dev\helworks\helengine\.worktrees\codegen-multi-profile-presets`. Create a separate `csharpcodegen` worktree before touching generator code.
- Use the same stable preset ids everywhere: `windows-shaders`, `windows-no-shaders`, `ps2-lite`, `n64-minimal`.
- Do not add runtime JSON, reflection-like runtime, or shader support to shared strict-console presets through fallbacks. The converter must fail when a forbidden system is reachable.
- Keep the Helengine editor surface simple. Reuse the existing codegen settings section with a dedicated setting id instead of adding a parallel modal or second profile picker.
- Keep runtime helper registration split in two layers:
  - run-level registration for reports, generated config, and copied runtime files
  - type-level registration for per-file `#include` emission

### Task 1: Create isolated worktrees and verify clean baselines

**Files:**
- Modify: none
- Test: none

- [ ] **Step 1: Verify the `csharpcodegen` repo can host an isolated worktree safely**

```powershell
rtk powershell -Command "git -C 'C:\dev\helworks\csharpcodegen' check-ignore -q .worktrees"
```

Expected: exit code `0`, confirming `.worktrees` is ignored in the `csharpcodegen` repo before creating a project-local worktree.

- [ ] **Step 2: Create the isolated `csharpcodegen` worktree and branch**

```powershell
rtk powershell -Command "git -C 'C:\dev\helworks\csharpcodegen' worktree add '.worktrees\codegen-multi-profile-presets' -b 'codegen-multi-profile-presets'"
```

Expected: git reports a new worktree at `C:\dev\helworks\csharpcodegen\.worktrees\codegen-multi-profile-presets`.

- [ ] **Step 3: Run the focused baseline generator tests in the new worktree**

```powershell
rtk dotnet test C:\dev\helworks\csharpcodegen\.worktrees\codegen-multi-profile-presets\cs2.cpp.tests\cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPFeatureProfileTests|FullyQualifiedName~CPPFeaturePruningEndToEndTests|FullyQualifiedName~CPPHelengineCoreAuditTests" -v minimal
```

Expected: PASS. If these fail before any edits, stop and record the exact pre-existing failure before proceeding.

- [ ] **Step 4: Run the focused Helengine baseline tests in the existing Helengine worktree**

```powershell
rtk dotnet test C:\dev\helworks\helengine\.worktrees\codegen-multi-profile-presets\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests|FullyQualifiedName~EditorBuildQueueItemFactoryTests" -v minimal
```

Expected: PASS. If they fail, record the baseline failure in the work log and get explicit approval before continuing.

- [ ] **Step 5: Commit nothing**

This task only prepares isolated execution environments and validates the baseline.

### Task 2: Add preset and restriction models to `cs2.cpp`

**Files:**
- Create: `C:\dev\helworks\csharpcodegen\cs2.cpp\model\CPPRestrictionProfile.cs`
- Create: `C:\dev\helworks\csharpcodegen\cs2.cpp\model\CPPConversionPreset.cs`
- Create: `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPConversionPresetCatalog.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp\model\CPPConversionOptions.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPConversionOptionsTests.cs`
- Create: `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPConversionPresetCatalogTests.cs`

- [ ] **Step 1: Write failing preset and option-surface tests**

```csharp
[Fact]
public void CreateDefault_ExposesEmptyPresetIdAndRestrictionProfile() {
    CPPConversionOptions options = CPPConversionOptions.CreateDefault();

    Assert.Equal(string.Empty, options.PresetId);
    Assert.NotNull(options.RestrictionProfile);
    Assert.False(options.RestrictionProfile.ForbidShaders);
}

[Fact]
public void Resolve_WindowsNoShaders_UsesNamedPresetProfiles() {
    CPPConversionPreset preset = new CPPConversionPresetCatalog().Resolve("windows-no-shaders");

    Assert.Equal("windows-no-shaders", preset.Id);
    Assert.Equal("msvc", preset.CompilerProfile.Name);
    Assert.Equal("windows-headless", preset.PlatformProfile.Name);
    Assert.True(preset.RestrictionProfile.ForbidShaders);
}
```

- [ ] **Step 2: Run the focused tests and verify failure**

```powershell
rtk dotnet test C:\dev\helworks\csharpcodegen\.worktrees\codegen-multi-profile-presets\cs2.cpp.tests\cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPConversionOptionsTests|FullyQualifiedName~CPPConversionPresetCatalogTests" -v minimal
```

Expected: FAIL because `PresetId`, `RestrictionProfile`, and the preset catalog do not exist yet.

- [ ] **Step 3: Add the preset catalog and option surfaces**

```csharp
public class CPPRestrictionProfile {
    public string Name { get; set; } = string.Empty;
    public bool ForbidShaders { get; set; }
    public bool ForbidRuntimeJson { get; set; }
    public bool ForbidReflectionLikeRuntime { get; set; }
    public bool ForbidRegex { get; set; }
    public bool ForbidDebugOnlySystems { get; set; }

    public static CPPRestrictionProfile CreatePermissive(string name) {
        return new CPPRestrictionProfile {
            Name = name
        };
    }
}

public class CPPConversionPreset {
    public string Id { get; set; } = string.Empty;
    public CPPCompilerProfile CompilerProfile { get; set; } = CPPCompilerProfile.CreateMsvc();
    public CPPPlatformProfile PlatformProfile { get; set; } = CPPPlatformProfile.CreateWindowsHeadless();
    public CPPRuntimeProfile RuntimeProfile { get; set; } = CPPRuntimeProfile.CreateStlLite();
    public CPPBuildFeatureProfile BuildFeatureProfile { get; set; } = CPPBuildFeatureProfile.CreateDefault();
    public CPPRestrictionProfile RestrictionProfile { get; set; } = CPPRestrictionProfile.CreatePermissive(string.Empty);
}
```

Also extend `CPPConversionOptions` so it carries:

```csharp
public string PresetId { get; set; } = string.Empty;
public CPPRestrictionProfile RestrictionProfile { get; set; } = CPPRestrictionProfile.CreatePermissive("default");
```

- [ ] **Step 4: Run the focused tests and verify pass**

```powershell
rtk dotnet test C:\dev\helworks\csharpcodegen\.worktrees\codegen-multi-profile-presets\cs2.cpp.tests\cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPConversionOptionsTests|FullyQualifiedName~CPPConversionPresetCatalogTests" -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
rtk powershell -Command "git -C 'C:\dev\helworks\csharpcodegen\.worktrees\codegen-multi-profile-presets' add cs2.cpp\model\CPPConversionOptions.cs cs2.cpp\model\CPPRestrictionProfile.cs cs2.cpp\model\CPPConversionPreset.cs cs2.cpp\CPPConversionPresetCatalog.cs cs2.cpp.tests\CPPConversionOptionsTests.cs cs2.cpp.tests\CPPConversionPresetCatalogTests.cs && git -C 'C:\dev\helworks\csharpcodegen\.worktrees\codegen-multi-profile-presets' commit -m 'Add cs2.cpp conversion preset catalog'"
```

### Task 3: Resolve presets in the CLI and record them in conversion reports

**Files:**
- Modify: `C:\dev\helworks\csharpcodegen\codegen\Program.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPConversionReportWriter.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPHelengineCoreAuditTests.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPFeatureProfileTests.cs`

- [ ] **Step 1: Write failing tests for preset-aware reporting**

```csharp
[Fact]
public void CreateDefault_LeavesPresetIdEmptyUntilResolved() {
    CPPConversionOptions options = CPPConversionOptions.CreateDefault();

    Assert.Equal(string.Empty, options.PresetId);
}

[Fact]
public void FixtureAudit_WritesPresetAndRestrictionProfile() {
    using JsonDocument document = JsonDocument.Parse(reportJson);
    JsonElement root = document.RootElement;

    Assert.Equal("windows-no-shaders", root.GetProperty("presetId").GetString());
    Assert.Equal("desktop-no-shaders", root.GetProperty("activeProfiles").GetProperty("restrictions").GetString());
}
```

- [ ] **Step 2: Run the focused tests and verify failure**

```powershell
rtk dotnet test C:\dev\helworks\csharpcodegen\.worktrees\codegen-multi-profile-presets\cs2.cpp.tests\cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPHelengineCoreAuditTests|FullyQualifiedName~CPPFeatureProfileTests" -v minimal
```

Expected: FAIL because the report does not yet serialize `presetId` or restriction profile information.

- [ ] **Step 3: Add `--preset` parsing and resolved option/report plumbing**

```csharp
case "preset":
    parsed.PresetId = RequireValue(args, ref index, optionName);
    break;
```

Resolve the preset during option creation:

```csharp
if (!string.IsNullOrWhiteSpace(parsed.PresetId)) {
    CPPConversionPreset preset = new CPPConversionPresetCatalog().Resolve(parsed.PresetId);
    options.PresetId = preset.Id;
    options.CompilerProfile = preset.CompilerProfile;
    options.PlatformProfile = preset.PlatformProfile;
    options.RuntimeProfile = preset.RuntimeProfile;
    options.BuildFeatureProfile = preset.BuildFeatureProfile;
    options.RestrictionProfile = preset.RestrictionProfile;
}
```

Write the same metadata into the report:

```csharp
presetId = options?.PresetId ?? string.Empty,
activeProfiles = new {
    compiler = options?.CompilerProfile?.Name ?? string.Empty,
    platform = options?.PlatformProfile?.Name ?? string.Empty,
    runtime = options?.RuntimeProfile?.Name ?? string.Empty,
    restrictions = options?.RestrictionProfile?.Name ?? string.Empty
},
```

- [ ] **Step 4: Run the focused tests and verify pass**

```powershell
rtk dotnet test C:\dev\helworks\csharpcodegen\.worktrees\codegen-multi-profile-presets\cs2.cpp.tests\cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPHelengineCoreAuditTests|FullyQualifiedName~CPPFeatureProfileTests" -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
rtk powershell -Command "git -C 'C:\dev\helworks\csharpcodegen\.worktrees\codegen-multi-profile-presets' add codegen\Program.cs cs2.cpp\CPPConversionReportWriter.cs cs2.cpp.tests\CPPHelengineCoreAuditTests.cs cs2.cpp.tests\CPPFeatureProfileTests.cs && git -C 'C:\dev\helworks\csharpcodegen\.worktrees\codegen-multi-profile-presets' commit -m 'Wire cs2.cpp preset selection into CLI and reports'"
```

### Task 4: Expand feature buckets and enforce restriction validation

**Files:**
- Create: `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPRestrictionValidator.cs`
- Create: `C:\dev\helworks\csharpcodegen\cs2.cpp\model\CPPRestrictionValidationResult.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp\model\CPPFeatureKind.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPFeatureCatalog.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPCodeConverter.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPRuntimeRequirementCatalog.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPRuntimeRequirementRegistrar.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPFeatureOwnedRuntimeRequirementTests.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPFeaturePruningEndToEndTests.cs`
- Create: `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPRestrictionValidatorTests.cs`

- [ ] **Step 1: Write failing tests for strict preset validation**

```csharp
[Fact]
public void Validate_WhenShadersAreForbiddenAndReachable_ReturnsDiagnostic() {
    CPPBuildUsageReport usageReport = CPPFeatureResolver.Resolve(
        CPPBuildFeatureProfile.CreateDefault(),
        [
            new CPPFeatureUsageRoot {
                Feature = CPPFeatureKind.Shaders,
                RootId = "helengine.core.shaders.ShaderAsset",
                SourceKind = "Type"
            }
        ]);

    CPPRestrictionProfile profile = new CPPRestrictionProfile {
        Name = "ps2-lite",
        ForbidShaders = true
    };

    CPPRestrictionValidationResult result = CPPRestrictionValidator.Validate(usageReport, [], profile);

    Assert.False(result.IsValid);
    Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Contains("ps2-lite") && diagnostic.Contains("Shaders"));
}

[Fact]
public void WriteOutput_WhenPresetForbidsRuntimeJson_FailsBeforeCopyingOutput() {
    Assert.Throws<InvalidOperationException>(() => RunConversion(source, "ps2-lite"));
}
```

- [ ] **Step 2: Run the focused tests and verify failure**

```powershell
rtk dotnet test C:\dev\helworks\csharpcodegen\.worktrees\codegen-multi-profile-presets\cs2.cpp.tests\cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPRestrictionValidatorTests|FullyQualifiedName~CPPFeatureOwnedRuntimeRequirementTests|FullyQualifiedName~CPPFeaturePruningEndToEndTests" -v minimal
```

Expected: FAIL because there is no restriction validator and strict presets do not block forbidden systems yet.

- [ ] **Step 3: Implement the expanded feature and restriction enforcement**

```csharp
public enum CPPFeatureKind {
    Render2D,
    Sprites,
    Text2D,
    Shaders,
    DebugOverlay,
    RuntimeJson,
    ReflectionLikeRuntime,
    HostFileSystem,
    TextProcessing,
}
```

Validate both feature usage and runtime helper ownership:

```csharp
CPPRestrictionValidationResult validation = CPPRestrictionValidator.Validate(
    BuildUsageReport,
    RuntimeRequirementRegistrar.RegisteredRequirements,
    Options.RestrictionProfile);

if (!validation.IsValid) {
    throw new InvalidOperationException(validation.Diagnostics[0]);
}
```

Tie runtime helpers to the new buckets:

```csharp
Make("NativeType", "runtime/native_type.hpp", "HE_CPP_REQ_NATIVE_TYPE", "...", CPPFeatureKind.ReflectionLikeRuntime),
Make("Regex", "system/text/regular_expressions/regex.hpp", "HE_CPP_REQ_REGEX", "...", CPPFeatureKind.Shaders, CPPFeatureKind.TextProcessing),
Make("File", "system/io/file.hpp", "HE_CPP_REQ_FILE", "...", CPPFeatureKind.HostFileSystem),
```

- [ ] **Step 4: Run the focused tests and verify pass**

```powershell
rtk dotnet test C:\dev\helworks\csharpcodegen\.worktrees\codegen-multi-profile-presets\cs2.cpp.tests\cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPRestrictionValidatorTests|FullyQualifiedName~CPPFeatureOwnedRuntimeRequirementTests|FullyQualifiedName~CPPFeaturePruningEndToEndTests" -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
rtk powershell -Command "git -C 'C:\dev\helworks\csharpcodegen\.worktrees\codegen-multi-profile-presets' add cs2.cpp\model\CPPFeatureKind.cs cs2.cpp\CPPFeatureCatalog.cs cs2.cpp\CPPCodeConverter.cs cs2.cpp\CPPRuntimeRequirementCatalog.cs cs2.cpp\CPPRuntimeRequirementRegistrar.cs cs2.cpp\CPPRestrictionValidator.cs cs2.cpp.tests\CPPFeatureOwnedRuntimeRequirementTests.cs cs2.cpp.tests\CPPFeaturePruningEndToEndTests.cs cs2.cpp.tests\CPPRestrictionValidatorTests.cs && git -C 'C:\dev\helworks\csharpcodegen\.worktrees\codegen-multi-profile-presets' commit -m 'Enforce cs2.cpp preset restrictions'"
```

### Task 5: Scope runtime helper includes per emitted type

**Files:**
- Create: `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPTypeRuntimeRequirementScope.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPClassEmitter.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp\CPPRuntimeRequirementRegistrar.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPClassEmitterIncludeFilteringTests.cs`
- Create: `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPClassEmitterRuntimeRequirementScopeTests.cs`

- [ ] **Step 1: Write failing type-scoped include tests**

```csharp
[Fact]
public void Emit_WhenCurrentTypeUsesStringBuilder_SourceIncludesOnlyStringBuilderRuntimeHelper() {
    string outputPath = RunConversion("""
using System.Text;

public class BuilderUser {
    public string Build() {
        StringBuilder builder = new StringBuilder();
        builder.Append(""x"");
        return builder.ToString();
    }
}
""", CPPBuildFeatureProfile.CreateDefault());

    string source = File.ReadAllText(Path.Combine(outputPath, "BuilderUser.cpp"));

    Assert.Contains("#include \"system/text/string-builder.hpp\"", source);
    Assert.DoesNotContain("#include \"runtime/native_type.hpp\"", source);
}
```

- [ ] **Step 2: Run the focused tests and verify failure**

```powershell
rtk dotnet test C:\dev\helworks\csharpcodegen\.worktrees\codegen-multi-profile-presets\cs2.cpp.tests\cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPClassEmitterIncludeFilteringTests|FullyQualifiedName~CPPClassEmitterRuntimeRequirementScopeTests" -v minimal
```

Expected: FAIL because `CPPClassEmitter` currently writes every registered runtime helper include into every `.cpp`.

- [ ] **Step 3: Implement type-local runtime requirement tracking**

```csharp
public sealed class CPPTypeRuntimeRequirementScope {
    readonly HashSet<string> RuntimeRequirements = new(StringComparer.Ordinal);

    public void Register(string requirementName) {
        RuntimeRequirements.Add(requirementName);
    }

    public IReadOnlyCollection<string> GetRegisteredRequirements() {
        return RuntimeRequirements.ToArray();
    }
}
```

Use this scope inside `CPPClassEmitter.Emit(...)` so the source preamble iterates only the current type’s requirements:

```csharp
foreach (string requirementName in typeScope.GetRegisteredRequirements()) {
    if (processor.RuntimeRequirementRegistrar.TryGet(requirementName, out CPPRuntimeRequirementDefinition definition)) {
        sourceWriter.WriteLine($"#include \"{definition.IncludePath}\"");
    }
}
```

- [ ] **Step 4: Run the focused tests and verify pass**

```powershell
rtk dotnet test C:\dev\helworks\csharpcodegen\.worktrees\codegen-multi-profile-presets\cs2.cpp.tests\cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPClassEmitterIncludeFilteringTests|FullyQualifiedName~CPPClassEmitterRuntimeRequirementScopeTests" -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
rtk powershell -Command "git -C 'C:\dev\helworks\csharpcodegen\.worktrees\codegen-multi-profile-presets' add cs2.cpp\CPPTypeRuntimeRequirementScope.cs cs2.cpp\CPPClassEmitter.cs cs2.cpp\CPPRuntimeRequirementRegistrar.cs cs2.cpp.tests\CPPClassEmitterIncludeFilteringTests.cs cs2.cpp.tests\CPPClassEmitterRuntimeRequirementScopeTests.cs && git -C 'C:\dev\helworks\csharpcodegen\.worktrees\codegen-multi-profile-presets' commit -m 'Scope cs2.cpp runtime helper includes per type'"
```

### Task 6: Pass the preset from Helengine using a dedicated codegen setting id

**Files:**
- Create: `C:\dev\helworks\helengine\engine\helengine.baseplatform\Definitions\PlatformCodegenSettingIds.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorGeneratedCoreRegenerationService.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorBuildQueueItemFactory.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\BuildDialog.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorGeneratedCoreRegenerationServiceTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorBuildQueueItemFactoryTests.cs`

- [ ] **Step 1: Write failing Helengine tests for preset forwarding and defaulting**

```csharp
[Fact]
public void Create_codegen_arguments_includes_selected_preset_id() {
    PlatformDefinition platformDefinition = new(
        "windows",
        "Windows",
        [],
        [],
        [],
        [],
        []);
    PlatformCodegenProfileDefinition codegenProfile = new(
        "default",
        "Default",
        "Default codegen profile",
        PlatformCodegenLanguage.Cpp,
        PlatformSerializationEndianness.LittleEndian,
        []);
    Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase) {
        [PlatformCodegenSettingIds.PresetId] = "ps2-lite"
    };
    string projectPath = @"C:\tmp\fixture.csproj";
    string outputRootPath = @"C:\tmp\generated";

    IReadOnlyList<string> arguments = EditorGeneratedCoreRegenerationService.BuildArguments(
        projectPath,
        outputRootPath,
        platformDefinition,
        codegenProfile,
        values,
        []);

    Assert.Contains("--preset", arguments);
    Assert.Contains("ps2-lite", arguments);
}

[Fact]
public void Create_WhenCodegenProfileDefinesPreset_SeedsPresetDefault() {
    EditorProjectSceneCatalogService sceneCatalogService = new EditorProjectSceneCatalogService(TempProjectRootPath);
    EditorBuildQueueItemFactory factory = new EditorBuildQueueItemFactory(sceneCatalogService);
    EditorBuildPlatformConfigDocument platformConfig = new EditorBuildPlatformConfigDocument {
        PlatformId = "windows"
    };
    EditorPlatformBuildSelectionModel selectionModel = EditorPlatformBuildSelectionModel.From(CreateSelectionModel());
    string outputRootPath = Path.Combine(TempProjectRootPath, "Build");
    EditorBuildQueueItemDocument queueItem = factory.Create(platformConfig, selectionModel, outputRootPath);

    Assert.Equal("windows-no-shaders", queueItem.SelectedCodegenOptionValues[PlatformCodegenSettingIds.PresetId]);
}
```

- [ ] **Step 2: Run the focused tests and verify failure**

```powershell
rtk dotnet test C:\dev\helworks\helengine\.worktrees\codegen-multi-profile-presets\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests|FullyQualifiedName~EditorBuildQueueItemFactoryTests" -v minimal
```

Expected: FAIL because Helengine does not yet recognize a dedicated preset setting id or pass `--preset`.

- [ ] **Step 3: Add the preset setting id and wire it into codegen invocation**

```csharp
public static class PlatformCodegenSettingIds {
    public const string PresetId = "codegen-preset-id";
}
```

In `EditorGeneratedCoreRegenerationService`, append the CLI argument before the generic `--set` loop:

```csharp
internal static List<string> BuildArguments(
    string projectPath,
    string outputRootPath,
    PlatformDefinition platformDefinition,
    PlatformCodegenProfileDefinition codegenProfile,
    IReadOnlyDictionary<string, string> selectedCodegenOptionValues,
    IReadOnlyList<string> additionalPreprocessorSymbols) {
    List<string> arguments = [
        "--cpp",
        "--project",
        projectPath,
        "--output",
        outputRootPath,
        "--platform",
        platformDefinition.PlatformId,
        "--language",
        codegenProfile.OutputLanguage.ToString().ToLowerInvariant(),
        "--endianness",
        codegenProfile.Endianness == PlatformSerializationEndianness.LittleEndian ? "little" : "big"
    ];

    return arguments;
}

if (selectedCodegenOptionValues.TryGetValue(PlatformCodegenSettingIds.PresetId, out string presetId)
    && !string.IsNullOrWhiteSpace(presetId)) {
    arguments.Add("--preset");
    arguments.Add(presetId);
}
```

Skip duplicating the same value through `--set`:

```csharp
if (string.Equals(selectedOption.Key, PlatformCodegenSettingIds.PresetId, StringComparison.OrdinalIgnoreCase)) {
    continue;
}
```

- [ ] **Step 4: Run the focused tests and verify pass**

```powershell
rtk dotnet test C:\dev\helworks\helengine\.worktrees\codegen-multi-profile-presets\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests|FullyQualifiedName~EditorBuildQueueItemFactoryTests" -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
rtk powershell -Command "git -C 'C:\dev\helworks\helengine\.worktrees\codegen-multi-profile-presets' add engine\helengine.baseplatform\Definitions\PlatformCodegenSettingIds.cs engine\helengine.editor\managers\project\EditorGeneratedCoreRegenerationService.cs engine\helengine.editor\managers\project\EditorBuildQueueItemFactory.cs engine\helengine.editor\components\ui\BuildDialog.cs engine\helengine.editor.tests\managers\project\EditorGeneratedCoreRegenerationServiceTests.cs engine\helengine.editor.tests\managers\project\EditorBuildQueueItemFactoryTests.cs && git -C 'C:\dev\helworks\helengine\.worktrees\codegen-multi-profile-presets' commit -m 'Pass codegen preset ids from Helengine builds'"
```

### Task 7: Run end-to-end verification in both worktrees

**Files:**
- Modify: none
- Test: `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\cs2.cpp.tests.csproj`
- Test: `C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj`

- [ ] **Step 1: Run the full focused `cs2.cpp` test suite for the touched areas**

```powershell
rtk dotnet test C:\dev\helworks\csharpcodegen\.worktrees\codegen-multi-profile-presets\cs2.cpp.tests\cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPConversionPresetCatalogTests|FullyQualifiedName~CPPRestrictionValidatorTests|FullyQualifiedName~CPPFeatureOwnedRuntimeRequirementTests|FullyQualifiedName~CPPFeaturePruningEndToEndTests|FullyQualifiedName~CPPClassEmitterRuntimeRequirementScopeTests|FullyQualifiedName~CPPHelengineCoreAuditTests" -v minimal
```

Expected: PASS.

- [ ] **Step 2: Run the focused Helengine editor tests**

```powershell
rtk dotnet test C:\dev\helworks\helengine\.worktrees\codegen-multi-profile-presets\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests|FullyQualifiedName~EditorBuildQueueItemFactoryTests" -v minimal
```

Expected: PASS.

- [ ] **Step 3: Run one manual `codegen` smoke conversion with a strict preset**

```powershell
rtk dotnet run --project C:\dev\helworks\csharpcodegen\.worktrees\codegen-multi-profile-presets\codegen\codegen.csproj -- --cpp --project C:\dev\helworks\csharpcodegen\.worktrees\codegen-multi-profile-presets\codegen.testproj\codegen.testproj.csproj --output C:\tmp\codegen-preset-smoke --platform windows --preset windows-no-shaders
```

Expected: `C++ conversion completed.` and a `cpp-conversion-report.json` in `C:\tmp\codegen-preset-smoke` containing `"presetId": "windows-no-shaders"`.

- [ ] **Step 4: Inspect the smoke report and confirm restriction metadata**

```powershell
rtk powershell -Command "Get-Content 'C:\tmp\codegen-preset-smoke\cpp-conversion-report.json' | Select-String 'presetId|restrictions'"
```

Expected: lines showing `presetId` and `restrictions`.

- [ ] **Step 5: Commit verification-only notes if and only if code changed**

No commit is required when this task only validates prior implementation commits.
