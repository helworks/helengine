# C++ Headless Core Transpiler Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restructure `cs2.cpp` so it can analyze and convert `helengine.core` into a portable headless C++ core with explicit compiler, platform, and runtime profiles plus hard-failure diagnostics for unsupported constructs.

**Architecture:** Treat `cs2.cpp` as a profile-driven backend with four layers: analysis, normalization, runtime requirement registration, and emission. Reuse the structural discipline of `cs2.ts`, but keep the generated C++ dependent on a small `helcpp` runtime abstraction layer instead of Windows APIs or unrestricted STL usage.

**Tech Stack:** C#/.NET 9, Roslyn, existing `cs2.core` pipeline infrastructure, xUnit, native C++ runtime templates under `.net.cpp`, `helengine.core` as the first real integration target

---

## File Map

### Existing files to modify

- `../csharpcodegen/codegen.sln`
  - Add the new `cs2.cpp.tests` project so backend tests run with the existing solution.
- `../csharpcodegen/cs2.cpp/cs2.cpp.csproj`
  - Include new runtime template files that must be copied to the converter output.
- `../csharpcodegen/cs2.cpp/CPPCodeConverter.cs`
  - Replace the current monolithic write flow with a profile-aware pipeline, report tracking, runtime registration, and delegated class emission.
- `../csharpcodegen/cs2.cpp/CPPConversiorProcessor.cs`
  - Expand processor coverage for the `helengine.core` feature subset and route unsupported constructs into diagnostics instead of silent omissions.
- `../csharpcodegen/cs2.cpp/CPPProgram.cs`
  - Carry backend options, runtime requirement registration state, and output metadata through the conversion process.
- `../csharpcodegen/cs2.cpp/CPPLayerContext.cs`
  - Support any additional context needed for profile-aware expression emission and diagnostics.
- `../csharpcodegen/cs2.cpp/model/CPPConversionRules.cs`
  - Reduce this to language-level toggles only, with profile/runtime selection moved into dedicated option types.
- `../csharpcodegen/cs2.cpp/model/CPPKnownClass.cs`
  - Extend known-class metadata so runtime imports can vary by profile and generated header/source split.
- `../csharpcodegen/cs2.cpp/model/CPPTypeData.cs`
  - Add enough metadata to distinguish native type aliases, runtime-owned wrapper types, and generated engine types.
- `../csharpcodegen/cs2.cpp/model/CPPVariableType.cs`
  - Support profile-aware type rendering and container/runtime mappings.
- `../csharpcodegen/cs2.cpp/util/CPPUtils.cs`
  - Centralize header/source naming, include formatting, identifier escaping, and small emission helpers.
- `../csharpcodegen/cs2.cpp/README.md`
  - Document the new profile model, runtime expectations, and how to run `helengine.core` audits.

### New files to create

- `../csharpcodegen/cs2.cpp/CPPConversionOptions.cs`
  - Single options object carrying compiler, platform, runtime, output, and diagnostic settings.
- `../csharpcodegen/cs2.cpp/CPPClassEmitter.cs`
  - Dedicated class/struct/enum emitter so `CPPCodeConverter` stops owning low-level header/source writing directly.
- `../csharpcodegen/cs2.cpp/CPPGeneratedConfigWriter.cs`
  - Emits the generated configuration header consumed by the runtime layer.
- `../csharpcodegen/cs2.cpp/CPPRuntimeRequirementCatalog.cs`
  - Lists the runtime requirement definitions available for each runtime/profile combination.
- `../csharpcodegen/cs2.cpp/CPPRuntimeRequirementDefinition.cs`
  - Describes one runtime-owned known class or template include.
- `../csharpcodegen/cs2.cpp/CPPRuntimeRequirementRegistrar.cs`
  - Registers runtime requirements with `CPPProgram` according to the active profiles.
- `../csharpcodegen/cs2.cpp/CPPConversionReportWriter.cs`
  - Writes a conversion report or manifest to disk after generation.
- `../csharpcodegen/cs2.cpp/CPPAssemblyMetadataStage.cs`
  - Captures assembly name, version, and target framework before emission.
- `../csharpcodegen/cs2.cpp/CPPResetConversionStateStage.cs`
  - Clears backend-specific state between runs.
- `../csharpcodegen/cs2.cpp/CPPPreprocessorFilterStage.cs`
  - Applies the active preprocessor symbol selection for the C++ backend.
- `../csharpcodegen/cs2.cpp/model/CPPCompilerProfile.cs`
  - Enum defining supported compiler families such as `MSVC` and `GCC`.
- `../csharpcodegen/cs2.cpp/model/CPPPlatformProfile.cs`
  - Enum defining supported platform families such as `WindowsHeadless`.
- `../csharpcodegen/cs2.cpp/model/CPPRuntimeProfile.cs`
  - Enum defining runtime contracts such as `Minimal` and `StlLite`.
- `../csharpcodegen/cs2.cpp/model/CPPConversionDiagnosticSeverity.cs`
  - Enum for `Info`, `Warning`, and `Error` diagnostic levels.
- `../csharpcodegen/cs2.cpp/model/CPPConversionDiagnostic.cs`
  - One conversion diagnostic record with source symbol, syntax kind, message, and recommendation.
- `../csharpcodegen/cs2.cpp/model/CPPConversionReport.cs`
  - Aggregates diagnostics, emitted file paths, and unsupported construct counts for one run.
- `../csharpcodegen/cs2.cpp/model/CPPPropertyLoweringKind.cs`
  - Describes how a property was lowered, for example direct field vs getter/setter.
- `../csharpcodegen/cs2.cpp/util/CPPTypeMap.cs`
  - Central map from `cs2.core` variable types and known BCL types into the active C++ runtime types.
- `../csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj`
  - Focused test project for backend-level C++ conversion work.
- `../csharpcodegen/cs2.cpp.tests/TestHelpers/CppProcessorTestHarness.cs`
  - Shared harness for processor-level tests, parallel to the existing TS test harness.
- `../csharpcodegen/cs2.cpp.tests/TestHelpers/RoslynTestHelper.cs`
  - Lightweight Roslyn parsing helper for backend tests.
- `../csharpcodegen/cs2.cpp.tests/CPPConversionOptionsTests.cs`
  - Verifies profile defaults and option validation behavior.
- `../csharpcodegen/cs2.cpp.tests/CPPGeneratedConfigWriterTests.cs`
  - Verifies compiler/platform/runtime defines in the generated config header.
- `../csharpcodegen/cs2.cpp.tests/CPPRuntimeRequirementRegistrarTests.cs`
  - Verifies runtime requirement registration varies correctly by active profile.
- `../csharpcodegen/cs2.cpp.tests/CPPClassEmitterTests.cs`
  - Verifies header/source emission for classes, structs, enums, access sections, and simple property lowering.
- `../csharpcodegen/cs2.cpp.tests/CPPConversiorProcessorTests.Expressions.cs`
  - Expression-level tests for the constructs used by `helengine.core`.
- `../csharpcodegen/cs2.cpp.tests/CPPConversiorProcessorTests.Statements.cs`
  - Statement-level tests for assignments, loops, switches, and control flow.
- `../csharpcodegen/cs2.cpp.tests/CPPCodeConverterPipelineTests.cs`
  - Verifies runtime registration, stage ordering, and report generation at the converter level.
- `../csharpcodegen/cs2.cpp.tests/CPPHelengineCoreAuditTests.cs`
  - Integration-oriented tests or audit helpers that exercise conversion diagnostics against `helengine.core` patterns.
- `../csharpcodegen/cs2.cpp/.net.cpp/helcpp/config.hpp`
  - Base config contract used by generated output and runtime templates.
- `../csharpcodegen/cs2.cpp/.net.cpp/helcpp/string.hpp`
  - Profile-aware string abstraction or alias layer.
- `../csharpcodegen/cs2.cpp/.net.cpp/helcpp/list.hpp`
  - Profile-aware sequential container abstraction.
- `../csharpcodegen/cs2.cpp/.net.cpp/helcpp/dictionary.hpp`
  - Profile-aware dictionary abstraction.

## Implementation Notes

- Use `@brainstorming` outputs from the approved design spec as the source of truth for boundaries and scope.
- Use `@test-driven-development` discipline while implementing the backend changes even if the existing repository has thinner test coverage today.
- Keep generated code profile-neutral by default. Runtime and compiler differences belong in the config/runtime layer first, not scattered through emitted engine classes.
- Do not silently synthesize placeholder output for unsupported C# constructs. Record a diagnostic and fail the conversion for that symbol.
- Keep `helengine.core` as the first integration target, but do not hardwire its path deep inside the backend. If an audit helper needs the path, pass it through options or a test helper.
- Prefer `.hpp` for generated headers to match the existing runtime template naming under `.net.cpp`, and make generated source files include those headers consistently.

### Task 1: Add a Focused `cs2.cpp` Test Harness

**Files:**
- Create: `../csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj`
- Create: `../csharpcodegen/cs2.cpp.tests/TestHelpers/RoslynTestHelper.cs`
- Create: `../csharpcodegen/cs2.cpp.tests/TestHelpers/CppProcessorTestHarness.cs`
- Modify: `../csharpcodegen/codegen.sln`

- [ ] **Step 1: Create the C++ backend test project**

Create `cs2.cpp.tests/cs2.cpp.tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <Nullable>disable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="coverlet.collector" Version="6.0.2" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.12.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\\cs2.core\\cs2.core.csproj" />
    <ProjectReference Include="..\\cs2.cpp\\cs2.cpp.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add Roslyn helper coverage for backend tests**

Create `TestHelpers/RoslynTestHelper.cs` with the same basic responsibilities the TS tests already use:

```csharp
internal static class RoslynTestHelper {
    public static (Compilation Compilation, SemanticModel Model, CompilationUnitSyntax Root) CreateCompilation(string code) { }
    public static MethodDeclarationSyntax GetFirstMethod(CompilationUnitSyntax root) { }
    public static MethodDeclarationSyntax GetMethodByName(CompilationUnitSyntax root, string methodName) { }
    public static StatementSyntax GetSingleStatement(MethodDeclarationSyntax method) { }
}
```

- [ ] **Step 3: Add a backend processor harness**

Create `TestHelpers/CppProcessorTestHarness.cs`:

```csharp
internal static class CppProcessorTestHarness {
    public static (CPPConversiorProcessor Processor, CPPLayerContext Context, CPPProgram Program) Create() {
        var rules = new CPPConversionRules();
        var program = new CPPProgram(rules);
        var context = new CPPLayerContext(program);
        var processor = new CPPConversiorProcessor(null);
        return (processor, context, program);
    }
}
```

Then adjust the constructor usage if `CPPConversiorProcessor` is updated in later tasks to take explicit dependencies instead of a raw converter reference.

- [ ] **Step 4: Add the new test project to `codegen.sln`**

Update `codegen.sln` so `cs2.cpp.tests` sits alongside `cs2.ts.tests`.

- [ ] **Step 5: Run the empty C++ backend test project**

Run: `rtk dotnet test ../csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj -v minimal`

Expected: PASS with `0` tests or a clean compile once the harness is wired.

- [ ] **Step 6: Commit the test harness**

```bash
rtk git add ../csharpcodegen/cs2.cpp.tests ../csharpcodegen/codegen.sln
rtk git commit -m "Add cs2.cpp test harness"
```

### Task 2: Add Profile and Report Domain Models

**Files:**
- Create: `../csharpcodegen/cs2.cpp/CPPConversionOptions.cs`
- Create: `../csharpcodegen/cs2.cpp/model/CPPCompilerProfile.cs`
- Create: `../csharpcodegen/cs2.cpp/model/CPPPlatformProfile.cs`
- Create: `../csharpcodegen/cs2.cpp/model/CPPRuntimeProfile.cs`
- Create: `../csharpcodegen/cs2.cpp/model/CPPConversionDiagnosticSeverity.cs`
- Create: `../csharpcodegen/cs2.cpp/model/CPPConversionDiagnostic.cs`
- Create: `../csharpcodegen/cs2.cpp/model/CPPConversionReport.cs`
- Modify: `../csharpcodegen/cs2.cpp/model/CPPConversionRules.cs`
- Test: `../csharpcodegen/cs2.cpp.tests/CPPConversionOptionsTests.cs`

- [ ] **Step 1: Write the failing options/report tests**

Create `CPPConversionOptionsTests.cs` with tests that assert:
- defaults are `WindowsHeadless`, `MSVC`, and `StlLite`,
- invalid combinations fail fast,
- reports start empty and accumulate diagnostics deterministically.

Test shape:

```csharp
[Fact]
public void DefaultOptions_UseWindowsHeadlessMsvcAndStlLite() {
    CPPConversionOptions options = CPPConversionOptions.Default;

    Assert.Equal(CPPPlatformProfile.WindowsHeadless, options.PlatformProfile);
    Assert.Equal(CPPCompilerProfile.Msvc, options.CompilerProfile);
    Assert.Equal(CPPRuntimeProfile.StlLite, options.RuntimeProfile);
}
```

- [ ] **Step 2: Run the options/report tests to verify they fail**

Run: `rtk dotnet test ../csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPConversionOptionsTests" -v minimal`

Expected: FAIL because the profile and report types do not exist yet.

- [ ] **Step 3: Create explicit profile enums**

Create:

`CPPCompilerProfile.cs`

```csharp
public enum CPPCompilerProfile {
    Msvc,
    Gcc
}
```

`CPPPlatformProfile.cs`

```csharp
public enum CPPPlatformProfile {
    WindowsHeadless
}
```

`CPPRuntimeProfile.cs`

```csharp
public enum CPPRuntimeProfile {
    Minimal,
    StlLite
}
```

- [ ] **Step 4: Create `CPPConversionOptions`**

Create `CPPConversionOptions.cs` with:
- static `Default`,
- profile properties,
- output folder/report switches,
- helper validation method,
- preprocessor symbol support if needed.

Minimal shape:

```csharp
public sealed class CPPConversionOptions {
    public static CPPConversionOptions Default => new CPPConversionOptions();
    public CPPCompilerProfile CompilerProfile { get; init; } = CPPCompilerProfile.Msvc;
    public CPPPlatformProfile PlatformProfile { get; init; } = CPPPlatformProfile.WindowsHeadless;
    public CPPRuntimeProfile RuntimeProfile { get; init; } = CPPRuntimeProfile.StlLite;
    public bool EmitConversionReport { get; init; } = true;
}
```

- [ ] **Step 5: Create conversion diagnostic/report models**

Create `CPPConversionDiagnosticSeverity.cs`, `CPPConversionDiagnostic.cs`, and `CPPConversionReport.cs` so the backend has one place to record:
- source symbol path,
- syntax kind,
- severity,
- failure message,
- optional recommendation,
- emitted file list.

- [ ] **Step 6: Shrink `CPPConversionRules` to language toggles**

Keep `CPPConversionRules` for low-level rendering choices such as `UseStdString`, but remove responsibility for target/compiler/runtime selection.

- [ ] **Step 7: Re-run the options/report tests**

Run: `rtk dotnet test ../csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPConversionOptionsTests" -v minimal`

Expected: PASS.

- [ ] **Step 8: Commit the domain models**

```bash
rtk git add ../csharpcodegen/cs2.cpp/CPPConversionOptions.cs ../csharpcodegen/cs2.cpp/model ../csharpcodegen/cs2.cpp.tests/CPPConversionOptionsTests.cs
rtk git commit -m "Add cs2.cpp profiles and conversion report models"
```

### Task 3: Add Runtime Requirement Registration and Generated Config Output

**Files:**
- Create: `../csharpcodegen/cs2.cpp/CPPRuntimeRequirementDefinition.cs`
- Create: `../csharpcodegen/cs2.cpp/CPPRuntimeRequirementCatalog.cs`
- Create: `../csharpcodegen/cs2.cpp/CPPRuntimeRequirementRegistrar.cs`
- Create: `../csharpcodegen/cs2.cpp/CPPGeneratedConfigWriter.cs`
- Create: `../csharpcodegen/cs2.cpp/.net.cpp/helcpp/config.hpp`
- Create: `../csharpcodegen/cs2.cpp/.net.cpp/helcpp/string.hpp`
- Create: `../csharpcodegen/cs2.cpp/.net.cpp/helcpp/list.hpp`
- Create: `../csharpcodegen/cs2.cpp/.net.cpp/helcpp/dictionary.hpp`
- Modify: `../csharpcodegen/cs2.cpp/cs2.cpp.csproj`
- Modify: `../csharpcodegen/cs2.cpp/model/CPPKnownClass.cs`
- Test: `../csharpcodegen/cs2.cpp.tests/CPPGeneratedConfigWriterTests.cs`
- Test: `../csharpcodegen/cs2.cpp.tests/CPPRuntimeRequirementRegistrarTests.cs`

- [ ] **Step 1: Write the failing config/runtime registration tests**

Create tests that assert:
- `MSVC + WindowsHeadless + StlLite` emits `HE_CPP_COMPILER_MSVC`, `HE_CPP_PLATFORM_WINDOWS`, and `HE_CPP_USE_STD_STRING 1`,
- `Minimal` runtime disables STL-backed defines,
- runtime requirements register `helcpp` wrappers instead of hardcoding direct `std::` names in the converter.

Example:

```csharp
[Fact]
public void GeneratedConfig_MsvcWindowsHeadlessStlLite_EmitsExpectedDefines() {
    CPPConversionOptions options = CPPConversionOptions.Default;

    string output = CPPGeneratedConfigWriter.WriteToString(options);

    Assert.Contains("#define HE_CPP_COMPILER_MSVC 1", output);
    Assert.Contains("#define HE_CPP_PLATFORM_WINDOWS 1", output);
    Assert.Contains("#define HE_CPP_USE_STD_STRING 1", output);
}
```

- [ ] **Step 2: Run the config/runtime tests to verify they fail**

Run: `rtk dotnet test ../csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPGeneratedConfigWriterTests|FullyQualifiedName~CPPRuntimeRequirementRegistrarTests" -v minimal`

Expected: FAIL because the config writer and runtime registrar do not exist yet.

- [ ] **Step 3: Create runtime requirement definition and catalog**

Create:

```csharp
public sealed class CPPRuntimeRequirementDefinition {
    public string Name { get; init; }
    public string HeaderPath { get; init; }
    public string SourcePath { get; init; }
    public Func<CPPConversionOptions, bool> AppliesTo { get; init; }
}
```

Then create a catalog that returns requirement definitions for:
- `helcpp/string`
- `helcpp/list`
- `helcpp/dictionary`
- existing `system/io` helpers already copied by `.net.cpp`

- [ ] **Step 4: Create the runtime registrar**

Create `CPPRuntimeRequirementRegistrar.cs` so `CPPProgram` receives known classes through one profile-aware registration point instead of inline `AddDotNet()` logic alone.

- [ ] **Step 5: Emit the generated config header**

Create `CPPGeneratedConfigWriter.cs` and `helcpp/config.hpp` so output folders always receive one generated header with the active profile defines.

- [ ] **Step 6: Add `helcpp` runtime template headers**

Create minimal profile-aware headers:

`string.hpp`

```cpp
#pragma once
#include "config.hpp"
#if HE_CPP_USE_STD_STRING
#include <string>
namespace helcpp { using string = std::string; }
#else
namespace helcpp { class string; }
#endif
```

`list.hpp`

```cpp
#pragma once
#include "config.hpp"
#if HE_CPP_USE_STL_VECTOR
#include <vector>
namespace helcpp { template<typename T> using list = std::vector<T>; }
#endif
```

Keep these thin for now. They exist to stabilize the generated contract, not to finish every retro-console runtime detail in one task.

- [ ] **Step 7: Update `cs2.cpp.csproj` to copy the new runtime files**

Add the `helcpp` headers to the `None Include=... CopyToOutputDirectory` item group.

- [ ] **Step 8: Re-run the config/runtime tests**

Run: `rtk dotnet test ../csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPGeneratedConfigWriterTests|FullyQualifiedName~CPPRuntimeRequirementRegistrarTests" -v minimal`

Expected: PASS.

- [ ] **Step 9: Commit runtime registration and config output**

```bash
rtk git add ../csharpcodegen/cs2.cpp ../csharpcodegen/cs2.cpp.tests
rtk git commit -m "Add cs2.cpp runtime profiles and config generation"
```

### Task 4: Refactor `CPPCodeConverter` Into a Profile-Driven Pipeline

**Files:**
- Create: `../csharpcodegen/cs2.cpp/CPPResetConversionStateStage.cs`
- Create: `../csharpcodegen/cs2.cpp/CPPAssemblyMetadataStage.cs`
- Create: `../csharpcodegen/cs2.cpp/CPPPreprocessorFilterStage.cs`
- Create: `../csharpcodegen/cs2.cpp/CPPConversionReportWriter.cs`
- Modify: `../csharpcodegen/cs2.cpp/CPPCodeConverter.cs`
- Modify: `../csharpcodegen/cs2.cpp/CPPProgram.cs`
- Test: `../csharpcodegen/cs2.cpp.tests/CPPCodeConverterPipelineTests.cs`

- [ ] **Step 1: Write the failing converter pipeline tests**

Create tests that assert:
- converter stages run in a predictable order,
- runtime requirements are registered before emission,
- generated config and report files are written when enabled,
- repeated runs clear prior report state.

Example:

```csharp
[Fact]
public void Converter_WithReportEnabled_WritesConfigAndReport() {
    // Arrange converter + temp output
    // Act
    // Assert generated config header and report file exist
}
```

- [ ] **Step 2: Run the converter pipeline tests to verify they fail**

Run: `rtk dotnet test ../csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPCodeConverterPipelineTests" -v minimal`

Expected: FAIL because `CPPCodeConverter` still owns a monolithic flow and no pipeline/report writer exists.

- [ ] **Step 3: Add C++-specific pipeline stages**

Create:
- `CPPResetConversionStateStage.cs`
- `CPPAssemblyMetadataStage.cs`
- `CPPPreprocessorFilterStage.cs`

Keep them small and aligned with the way `TypeScriptCodeConverter` wires its stages.

- [ ] **Step 4: Refactor `CPPCodeConverter` to accept `CPPConversionOptions`**

Update the constructor so it receives options explicitly:

```csharp
public CPPCodeConverter(CPPConversionRules rules, CPPConversionOptions options = null) : base(rules) { }
```

Then:
- store the resolved options,
- build preprocessor symbols from the options,
- register runtime requirements,
- configure the conversion pipeline,
- delegate class emission and config/report writing to dedicated helpers.

- [ ] **Step 5: Add `CPPConversionReportWriter`**

Create one writer that serializes `CPPConversionReport` to disk in a deterministic text or JSON format.

- [ ] **Step 6: Update `CPPProgram` to carry report/runtime state**

Add explicit properties for:
- active options,
- collected diagnostics,
- emitted file list,
- runtime requirement tracking.

- [ ] **Step 7: Re-run the converter pipeline tests**

Run: `rtk dotnet test ../csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPCodeConverterPipelineTests" -v minimal`

Expected: PASS.

- [ ] **Step 8: Commit the pipeline refactor**

```bash
rtk git add ../csharpcodegen/cs2.cpp ../csharpcodegen/cs2.cpp.tests/CPPCodeConverterPipelineTests.cs
rtk git commit -m "Refactor cs2.cpp into profile-driven pipeline"
```

### Task 5: Extract Class Emission and Stabilize Type/Member Output

**Files:**
- Create: `../csharpcodegen/cs2.cpp/CPPClassEmitter.cs`
- Create: `../csharpcodegen/cs2.cpp/model/CPPPropertyLoweringKind.cs`
- Create: `../csharpcodegen/cs2.cpp/util/CPPTypeMap.cs`
- Modify: `../csharpcodegen/cs2.cpp/CPPCodeConverter.cs`
- Modify: `../csharpcodegen/cs2.cpp/model/CPPTypeData.cs`
- Modify: `../csharpcodegen/cs2.cpp/model/CPPVariableType.cs`
- Modify: `../csharpcodegen/cs2.cpp/util/CPPUtils.cs`
- Test: `../csharpcodegen/cs2.cpp.tests/CPPClassEmitterTests.cs`

- [ ] **Step 1: Write the failing class-emission tests**

Create tests that assert:
- classes generate one `.hpp` and one `.cpp`,
- structs emit value-like declarations,
- enums emit stable underlying values,
- access sections appear only when needed,
- constructors and methods write correct signatures,
- trivial auto-properties lower to fields,
- non-trivial properties lower to getter/setter methods.

Test shape:

```csharp
[Fact]
public void AutoProperty_LowersToFieldInHeader() {
    // Arrange class with auto-property metadata
    // Act
    // Assert header contains field form instead of get_/set_ methods
}
```

- [ ] **Step 2: Run the class-emission tests to verify they fail**

Run: `rtk dotnet test ../csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPClassEmitterTests" -v minimal`

Expected: FAIL because class emission is still embedded in `CPPCodeConverter` and properties/constructors are incomplete.

- [ ] **Step 3: Extract `CPPClassEmitter`**

Move low-level header/source generation out of `CPPCodeConverter` into `CPPClassEmitter` with methods shaped like:

```csharp
public sealed class CPPClassEmitter {
    public IReadOnlyList<string> EmitFiles(ConversionClass conversionClass) { }
}
```

The exact API can vary, but `CPPCodeConverter` should stop writing classes directly.

- [ ] **Step 4: Centralize type mapping**

Create `CPPTypeMap.cs` so the backend has one place to map:
- `string` -> `helcpp::string` or equivalent runtime alias,
- arrays -> runtime-aware container forms,
- `List<T>` -> `helcpp::list<T>`,
- `Dictionary<TKey, TValue>` -> `helcpp::dictionary<TKey, TValue>`.

- [ ] **Step 5: Implement property lowering rules**

Create `CPPPropertyLoweringKind.cs` and use it so the emitter follows the approved rule:
- trivial auto-properties become fields,
- non-trivial properties become accessor methods.

- [ ] **Step 6: Re-run the class-emission tests**

Run: `rtk dotnet test ../csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPClassEmitterTests" -v minimal`

Expected: PASS.

- [ ] **Step 7: Commit the class emitter**

```bash
rtk git add ../csharpcodegen/cs2.cpp ../csharpcodegen/cs2.cpp.tests/CPPClassEmitterTests.cs
rtk git commit -m "Extract cs2.cpp class emitter and type map"
```

### Task 6: Expand `CPPConversiorProcessor` for the `helengine.core` Subset

**Files:**
- Modify: `../csharpcodegen/cs2.cpp/CPPConversiorProcessor.cs`
- Modify: `../csharpcodegen/cs2.cpp/CPPLayerContext.cs`
- Modify: `../csharpcodegen/cs2.cpp/model/CPPVariableType.cs`
- Test: `../csharpcodegen/cs2.cpp.tests/CPPConversiorProcessorTests.Expressions.cs`
- Test: `../csharpcodegen/cs2.cpp.tests/CPPConversiorProcessorTests.Statements.cs`

- [ ] **Step 1: Write the failing processor expression tests**

Create expression tests for the `helengine.core` subset:
- identifier access,
- member access,
- object creation,
- static member access,
- `this` and `base`,
- casts,
- enum values,
- arithmetic and comparisons.

Example:

```csharp
[Fact]
public void MemberAccess_EmitsCppDotAccess() {
    var code = "class C { int a; void M(){ this.a; } }";
    // Arrange + Act
    Assert.Contains(".", output);
}
```

- [ ] **Step 2: Write the failing processor statement tests**

Create statement tests for:
- assignments,
- `if/else`,
- `switch`,
- `for`,
- `foreach` if required by actual engine patterns,
- `while`,
- `return`,
- local declarations.

- [ ] **Step 3: Run the processor tests to verify they fail**

Run: `rtk dotnet test ../csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPConversiorProcessorTests" -v minimal`

Expected: FAIL in multiple places because the current processor only covers a thin subset.

- [ ] **Step 4: Implement the missing expression handling**

Expand `CPPConversiorProcessor` incrementally to satisfy the tests, keeping all unsupported constructs routed through `CPPConversionReport` diagnostics rather than omitted.

- [ ] **Step 5: Implement the missing statement/control-flow handling**

Add the minimum logic needed for the `helengine.core` subset. If a construct proves ambiguous for C++ lowering, add an explicit failure diagnostic instead of guessing.

- [ ] **Step 6: Re-run the processor tests**

Run: `rtk dotnet test ../csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPConversiorProcessorTests" -v minimal`

Expected: PASS.

- [ ] **Step 7: Commit the processor expansion**

```bash
rtk git add ../csharpcodegen/cs2.cpp/CPPConversiorProcessor.cs ../csharpcodegen/cs2.cpp/CPPLayerContext.cs ../csharpcodegen/cs2.cpp.tests/CPPConversiorProcessorTests.Expressions.cs ../csharpcodegen/cs2.cpp.tests/CPPConversiorProcessorTests.Statements.cs
rtk git commit -m "Expand cs2.cpp processor for helengine core subset"
```

### Task 7: Add `helengine.core` Audit Coverage and Close the First Gap List

**Files:**
- Create: `../csharpcodegen/cs2.cpp.tests/CPPHelengineCoreAuditTests.cs`
- Modify: `../csharpcodegen/cs2.cpp/README.md`
- Modify: `../csharpcodegen/cs2.cpp/CPPCodeConverter.cs`
- Modify: `../csharpcodegen/cs2.cpp/CPPConversionReportWriter.cs`

- [ ] **Step 1: Write the failing audit test or audit helper**

Create `CPPHelengineCoreAuditTests.cs` with one test or helper-oriented assertion that runs the converter against a small real-project target configuration and verifies that diagnostics are captured deterministically.

Preferred shape:

```csharp
[Fact]
public void HelengineCoreAudit_WritesDeterministicDiagnosticReport() {
    // Arrange options with a known sample project path or injected project path
    // Act
    // Assert report exists and contains stable headings / counts
}
```

If a fully automated real-path test is too environment-dependent, make this a helper-backed test over a captured mini-project fixture and keep the full `helengine.core` run as a documented manual command.

- [ ] **Step 2: Run the audit test to verify it fails**

Run: `rtk dotnet test ../csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPHelengineCoreAuditTests" -v minimal`

Expected: FAIL because the backend does not yet produce a stable audit report.

- [ ] **Step 3: Add deterministic report output**

Update `CPPConversionReportWriter.cs` so reports contain:
- active profiles,
- emitted file count,
- error/warning counts,
- diagnostics grouped by type/member,
- unsupported syntax summary.

- [ ] **Step 4: Document the real `helengine.core` audit command**

Update `cs2.cpp/README.md` with a concrete audit example, for example:

```bash
rtk dotnet run --project ../csharpcodegen/cs2.cpp/cs2.cpp.csproj -- \
  --project /mnt/c/dev/helengine/engine/helengine.core/helengine.core.csproj \
  --output /tmp/helengine-core-cpp \
  --platform windows-headless \
  --compiler msvc \
  --runtime stl-lite \
  --report /tmp/helengine-core-cpp/conversion-report.json
```

The exact CLI flags can differ, but the README must show how to produce the first real gap report for `helengine.core`.

- [ ] **Step 5: Re-run the audit test**

Run: `rtk dotnet test ../csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPHelengineCoreAuditTests" -v minimal`

Expected: PASS for the deterministic report assertion or fixture-backed audit helper.

- [ ] **Step 6: Run the first real `helengine.core` audit manually**

Run the documented command against:

`/mnt/c/dev/helengine/engine/helengine.core/helengine.core.csproj`

Expected:
- generated output folder is created,
- generated config header is present,
- conversion report is written,
- unsupported constructs are listed explicitly rather than silently skipped.

- [ ] **Step 7: Commit the audit/report work**

```bash
rtk git add ../csharpcodegen/cs2.cpp ../csharpcodegen/cs2.cpp.tests/CPPHelengineCoreAuditTests.cs
rtk git commit -m "Add helengine.core audit reporting for cs2.cpp"
```

## Final Verification

- [ ] Run the full backend test suite:

```bash
rtk dotnet test ../csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj -v minimal
```

Expected: PASS.

- [ ] Run the existing TypeScript backend tests to catch shared-core regressions:

```bash
rtk dotnet test ../csharpcodegen/cs2.ts.tests/cs2.ts.tests.csproj -v minimal
```

Expected: PASS.

- [ ] Run the first real `helengine.core` audit command and review the report:

```bash
rtk dotnet run --project ../csharpcodegen/cs2.cpp/cs2.cpp.csproj -- \
  --project /mnt/c/dev/helengine/engine/helengine.core/helengine.core.csproj \
  --output /tmp/helengine-core-cpp \
  --platform windows-headless \
  --compiler msvc \
  --runtime stl-lite \
  --report /tmp/helengine-core-cpp/conversion-report.json
```

Expected:
- output files are emitted,
- `helcpp/config.hpp` is generated,
- a deterministic conversion report is produced,
- remaining unsupported constructs are explicit and actionable.
