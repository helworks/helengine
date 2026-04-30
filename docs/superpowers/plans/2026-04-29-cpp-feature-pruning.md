# C++ Feature-Pruned Build Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add feature-pruned `cs2.cpp` builds that can auto-detect or force-enable/force-disable subsystems, emit only reachable generated C++ output, and expose the final feature set in build reports and in the generated runtime.

**Architecture:** Extend `cs2.cpp` with explicit feature-domain models, a feature scan and resolution pass, a reachability-based emission filter, and feature-aware runtime/report generation. Phase 1 proves the system on `Shaders`, `Sprites`, `Text2D`, `Render2D`, and `DebugOverlay`, with end-to-end pruning assertions focused on `Shaders` and `Sprites`.

**Tech Stack:** C#, .NET, Roslyn, xUnit, `cs2.core`, `cs2.cpp`, generated native C++ runtime stubs

---

## File Structure

### Existing files to modify

- ` /mnt/c/dev/csharpcodegen/cs2.cpp/model/CPPConversionOptions.cs`
  - Add build feature profile configuration.
- ` /mnt/c/dev/csharpcodegen/cs2.cpp/CPPCodeConverter.cs`
  - Add feature scan, resolution, reachability selection, feature-aware runtime emission, and feature report wiring.
- ` /mnt/c/dev/csharpcodegen/cs2.cpp/CPPConversionReportWriter.cs`
  - Emit build feature report JSON.
- ` /mnt/c/dev/csharpcodegen/cs2.cpp/CPPRuntimeRequirementCatalog.cs`
  - Make runtime requirements feature-aware.
- ` /mnt/c/dev/csharpcodegen/cs2.cpp/CPPClassEmitter.cs`
  - Respect the final reachable type set and feature-gated include/runtime emission.
- ` /mnt/c/dev/csharpcodegen/cs2.core/ConversionPreProcessor.cs`
  - Surface enough type/root metadata for feature tagging and reachability.

### New production files to create

- ` /mnt/c/dev/csharpcodegen/cs2.cpp/model/CPPFeatureKind.cs`
- ` /mnt/c/dev/csharpcodegen/cs2.cpp/model/CPPFeatureMode.cs`
- ` /mnt/c/dev/csharpcodegen/cs2.cpp/model/CPPFeatureConflictPolicy.cs`
- ` /mnt/c/dev/csharpcodegen/cs2.cpp/model/CPPFeatureDecisionOrigin.cs`
- ` /mnt/c/dev/csharpcodegen/cs2.cpp/model/CPPBuildFeatureProfile.cs`
- ` /mnt/c/dev/csharpcodegen/cs2.cpp/model/CPPFeatureDecision.cs`
- ` /mnt/c/dev/csharpcodegen/cs2.cpp/model/CPPBuildUsageReport.cs`
- ` /mnt/c/dev/csharpcodegen/cs2.cpp/model/CPPFeatureUsageRoot.cs`
- ` /mnt/c/dev/csharpcodegen/cs2.cpp/CPPFeatureCatalog.cs`
- ` /mnt/c/dev/csharpcodegen/cs2.cpp/CPPFeatureScanner.cs`
- ` /mnt/c/dev/csharpcodegen/cs2.cpp/CPPFeatureResolver.cs`
- ` /mnt/c/dev/csharpcodegen/cs2.cpp/CPPReachabilityPlanner.cs`
- ` /mnt/c/dev/csharpcodegen/cs2.cpp/CPPFeatureManifestWriter.cs`
- ` /mnt/c/dev/csharpcodegen/cs2.cpp/.net.cpp/runtime/feature_manifest.hpp`
- ` /mnt/c/dev/csharpcodegen/cs2.cpp/.net.cpp/runtime/feature_manifest.cpp`

### New test files to create

- ` /mnt/c/dev/csharpcodegen/cs2.cpp.tests/CPPFeatureProfileTests.cs`
- ` /mnt/c/dev/csharpcodegen/cs2.cpp.tests/CPPFeatureResolverTests.cs`
- ` /mnt/c/dev/csharpcodegen/cs2.cpp.tests/CPPFeatureScannerTests.cs`
- ` /mnt/c/dev/csharpcodegen/cs2.cpp.tests/CPPReachabilityPlannerTests.cs`
- ` /mnt/c/dev/csharpcodegen/cs2.cpp.tests/CPPFeatureManifestWriterTests.cs`
- ` /mnt/c/dev/csharpcodegen/cs2.cpp.tests/CPPFeaturePruningEndToEndTests.cs`

## Notes for the implementer

- Work in `/mnt/c/dev/csharpcodegen`; the spec and plan live in `/mnt/c/dev/helengine` but the code changes do not.
- Keep the feature graph explicit. Do not build a name-guessing system that will drift.
- Do not try to solve all subsystems in phase 1. Prove the plumbing on the five approved feature buckets and enforce the architecture.
- Prefer failing builds on forced-disable conflicts unless the feature explicitly supports degradation.
- Keep runtime visibility small: static arrays, no reflection, no required heap allocations.

### Task 1: Add feature domain models to `cs2.cpp`

**Files:**
- Create: `/mnt/c/dev/csharpcodegen/cs2.cpp/model/CPPFeatureKind.cs`
- Create: `/mnt/c/dev/csharpcodegen/cs2.cpp/model/CPPFeatureMode.cs`
- Create: `/mnt/c/dev/csharpcodegen/cs2.cpp/model/CPPFeatureConflictPolicy.cs`
- Create: `/mnt/c/dev/csharpcodegen/cs2.cpp/model/CPPFeatureDecisionOrigin.cs`
- Create: `/mnt/c/dev/csharpcodegen/cs2.cpp/model/CPPBuildFeatureProfile.cs`
- Create: `/mnt/c/dev/csharpcodegen/cs2.cpp/model/CPPFeatureDecision.cs`
- Create: `/mnt/c/dev/csharpcodegen/cs2.cpp/model/CPPBuildUsageReport.cs`
- Create: `/mnt/c/dev/csharpcodegen/cs2.cpp/model/CPPFeatureUsageRoot.cs`
- Modify: `/mnt/c/dev/csharpcodegen/cs2.cpp/model/CPPConversionOptions.cs`
- Test: `/mnt/c/dev/csharpcodegen/cs2.cpp.tests/CPPFeatureProfileTests.cs`

- [ ] **Step 1: Write the failing model/profile tests**

```csharp
[Fact]
public void DefaultProfile_UsesAutoForAllPhaseOneFeatures() {
    CPPBuildFeatureProfile profile = CPPBuildFeatureProfile.CreateDefault();

    Assert.Equal(CPPFeatureMode.Auto, profile.GetMode(CPPFeatureKind.Shaders));
    Assert.Equal(CPPFeatureMode.Auto, profile.GetMode(CPPFeatureKind.Sprites));
}

[Fact]
public void ConversionOptions_ExposeBuildFeatureProfile() {
    CPPConversionOptions options = new CPPConversionOptions();

    Assert.NotNull(options.BuildFeatureProfile);
}
```

- [ ] **Step 2: Run the focused tests and verify failure**

Run: `rtk dotnet test /mnt/c/dev/csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPFeatureProfileTests" -v minimal`
Expected: FAIL because the feature model types and option surface do not exist yet.

- [ ] **Step 3: Add the minimal feature model types and option wiring**

```csharp
public enum CPPFeatureKind {
    Render2D,
    Sprites,
    Text2D,
    Shaders,
    DebugOverlay,
}

public class CPPBuildFeatureProfile {
    readonly Dictionary<CPPFeatureKind, CPPFeatureMode> Modes;

    public CPPFeatureMode GetMode(CPPFeatureKind feature) {
        return Modes.TryGetValue(feature, out CPPFeatureMode mode) ? mode : CPPFeatureMode.Auto;
    }
}
```

- [ ] **Step 4: Run the focused tests and verify pass**

Run: `rtk dotnet test /mnt/c/dev/csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPFeatureProfileTests" -v minimal`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git -C /mnt/c/dev/csharpcodegen add cs2.cpp/model cs2.cpp.tests/CPPFeatureProfileTests.cs
git -C /mnt/c/dev/csharpcodegen commit -m "Add cs2.cpp feature profile models"
```

### Task 2: Implement feature resolution with precedence and conflict policy

**Files:**
- Create: `/mnt/c/dev/csharpcodegen/cs2.cpp/CPPFeatureResolver.cs`
- Modify: `/mnt/c/dev/csharpcodegen/cs2.cpp/model/CPPBuildUsageReport.cs`
- Test: `/mnt/c/dev/csharpcodegen/cs2.cpp.tests/CPPFeatureResolverTests.cs`

- [ ] **Step 1: Write failing resolver tests for precedence and conflicts**

```csharp
[Fact]
public void Resolve_WhenFeatureIsForceDisabled_WinsOverDetectedUsage() {
    CPPBuildFeatureProfile profile = CPPBuildFeatureProfile.CreateDefault()
        .WithMode(CPPFeatureKind.Shaders, CPPFeatureMode.Disabled)
        .WithConflictPolicy(CPPFeatureKind.Shaders, CPPFeatureConflictPolicy.Error);

    CPPBuildUsageReport report = CPPFeatureResolver.Resolve(profile, new[] {
        CPPFeatureUsageRoot.ForDetectedType(CPPFeatureKind.Shaders, "helengine.core.shaders.ShaderAsset")
    });

    Assert.False(report.IsEnabled(CPPFeatureKind.Shaders));
    Assert.Single(report.Conflicts);
}
```

- [ ] **Step 2: Run the focused tests and verify failure**

Run: `rtk dotnet test /mnt/c/dev/csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPFeatureResolverTests" -v minimal`
Expected: FAIL because the resolver does not exist yet.

- [ ] **Step 3: Implement the resolver and decision recording**

```csharp
public static CPPBuildUsageReport Resolve(
    CPPBuildFeatureProfile profile,
    IReadOnlyList<CPPFeatureUsageRoot> detectedRoots) {
    // Apply Disabled > Enabled > Auto and record conflicts.
}
```

- [ ] **Step 4: Run the focused tests and verify pass**

Run: `rtk dotnet test /mnt/c/dev/csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPFeatureResolverTests" -v minimal`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git -C /mnt/c/dev/csharpcodegen add cs2.cpp/CPPFeatureResolver.cs cs2.cpp/model/CPPBuildUsageReport.cs cs2.cpp.tests/CPPFeatureResolverTests.cs
git -C /mnt/c/dev/csharpcodegen commit -m "Add cs2.cpp feature resolution rules"
```

### Task 3: Add explicit phase-1 feature tagging and scanning

**Files:**
- Create: `/mnt/c/dev/csharpcodegen/cs2.cpp/CPPFeatureCatalog.cs`
- Create: `/mnt/c/dev/csharpcodegen/cs2.cpp/CPPFeatureScanner.cs`
- Modify: `/mnt/c/dev/csharpcodegen/cs2.core/ConversionPreProcessor.cs`
- Test: `/mnt/c/dev/csharpcodegen/cs2.cpp.tests/CPPFeatureScannerTests.cs`

- [ ] **Step 1: Write failing scanner tests for known shader and sprite roots**

```csharp
[Fact]
public void Scan_WhenShaderNamespaceIsReferenced_DetectsShaders() {
    CPPFeatureUsageRoot[] roots = CPPFeatureScanner.Scan(program);

    Assert.Contains(roots, root => root.Feature == CPPFeatureKind.Shaders);
}

[Fact]
public void Scan_WhenSpriteInterfaceIsReferenced_DetectsSpritesAndRender2D() {
    CPPFeatureUsageRoot[] roots = CPPFeatureScanner.Scan(program);

    Assert.Contains(roots, root => root.Feature == CPPFeatureKind.Sprites);
    Assert.Contains(roots, root => root.Feature == CPPFeatureKind.Render2D);
}
```

- [ ] **Step 2: Run the focused tests and verify failure**

Run: `rtk dotnet test /mnt/c/dev/csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPFeatureScannerTests" -v minimal`
Expected: FAIL because no scanner/catalog exists.

- [ ] **Step 3: Implement explicit phase-1 tagging and scanner hooks**

```csharp
public sealed class CPPFeatureCatalog {
    public bool TryMapType(string fullTypeName, out CPPFeatureKind feature) {
        // Map known namespaces and roots only.
    }
}
```

- [ ] **Step 4: Run the focused tests and verify pass**

Run: `rtk dotnet test /mnt/c/dev/csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPFeatureScannerTests" -v minimal`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git -C /mnt/c/dev/csharpcodegen add cs2.cpp/CPPFeatureCatalog.cs cs2.cpp/CPPFeatureScanner.cs cs2.core/ConversionPreProcessor.cs cs2.cpp.tests/CPPFeatureScannerTests.cs
git -C /mnt/c/dev/csharpcodegen commit -m "Add cs2.cpp feature tagging and scanning"
```

### Task 4: Add reachability-pruned emission planning

**Files:**
- Create: `/mnt/c/dev/csharpcodegen/cs2.cpp/CPPReachabilityPlanner.cs`
- Modify: `/mnt/c/dev/csharpcodegen/cs2.cpp/CPPCodeConverter.cs`
- Modify: `/mnt/c/dev/csharpcodegen/cs2.cpp/CPPClassEmitter.cs`
- Test: `/mnt/c/dev/csharpcodegen/cs2.cpp.tests/CPPReachabilityPlannerTests.cs`

- [ ] **Step 1: Write failing tests for feature-pruned reachable type selection**

```csharp
[Fact]
public void Plan_WhenShadersAreDisabled_ExcludesShaderTypesFromReachableSet() {
    CPPReachabilityPlan plan = CPPReachabilityPlanner.Build(program, report);

    Assert.DoesNotContain(plan.Types, type => type.Namespace.StartsWith("helengine.core.shaders"));
}
```

- [ ] **Step 2: Run the focused tests and verify failure**

Run: `rtk dotnet test /mnt/c/dev/csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPReachabilityPlannerTests" -v minimal`
Expected: FAIL because emission still works as whole-assembly output.

- [ ] **Step 3: Implement reachable-type planning and wire emission through it**

```csharp
public sealed class CPPReachabilityPlanner {
    public CPPReachabilityPlan Build(ConversionProgram program, CPPBuildUsageReport report) {
        // Walk selected roots and keep only reachable types/runtime requirements.
    }
}
```

- [ ] **Step 4: Run the focused tests and verify pass**

Run: `rtk dotnet test /mnt/c/dev/csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPReachabilityPlannerTests" -v minimal`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git -C /mnt/c/dev/csharpcodegen add cs2.cpp/CPPReachabilityPlanner.cs cs2.cpp/CPPCodeConverter.cs cs2.cpp/CPPClassEmitter.cs cs2.cpp.tests/CPPReachabilityPlannerTests.cs
git -C /mnt/c/dev/csharpcodegen commit -m "Add cs2.cpp reachability-pruned emission"
```

### Task 5: Make runtime requirement registration feature-aware

**Files:**
- Modify: `/mnt/c/dev/csharpcodegen/cs2.cpp/CPPRuntimeRequirementCatalog.cs`
- Modify: `/mnt/c/dev/csharpcodegen/cs2.cpp/CPPCodeConverter.cs`
- Test: `/mnt/c/dev/csharpcodegen/cs2.cpp.tests/CPPFeaturePruningEndToEndTests.cs`

- [ ] **Step 1: Write failing end-to-end test for shader runtime pruning**

```csharp
[Fact]
public void WriteOutput_WhenShadersAreDisabled_DoesNotEmitShaderRuntimeContracts() {
    CPPConversionResult result = ConvertWithFeatureDisabled(CPPFeatureKind.Shaders);

    Assert.DoesNotContain("Shader", result.GeneratedFiles.Keys);
    Assert.DoesNotContain(result.Report.RuntimeRequirements, requirement => requirement.Name.Contains("Shader"));
}
```

- [ ] **Step 2: Run the focused test and verify failure**

Run: `rtk dotnet test /mnt/c/dev/csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPFeaturePruningEndToEndTests.WriteOutput_WhenShadersAreDisabled_DoesNotEmitShaderRuntimeContracts" -v minimal`
Expected: FAIL because runtime requirements are still unconditional.

- [ ] **Step 3: Implement feature-aware runtime requirement filtering**

```csharp
public bool IsRequirementAllowed(string requirementName, CPPBuildUsageReport report) {
    // Keep only requirements reachable from enabled features or shared core.
}
```

- [ ] **Step 4: Run the focused test and verify pass**

Run: `rtk dotnet test /mnt/c/dev/csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPFeaturePruningEndToEndTests.WriteOutput_WhenShadersAreDisabled_DoesNotEmitShaderRuntimeContracts" -v minimal`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git -C /mnt/c/dev/csharpcodegen add cs2.cpp/CPPRuntimeRequirementCatalog.cs cs2.cpp/CPPCodeConverter.cs cs2.cpp.tests/CPPFeaturePruningEndToEndTests.cs
git -C /mnt/c/dev/csharpcodegen commit -m "Prune cs2.cpp runtime requirements by feature"
```

### Task 6: Emit feature reports and the native runtime feature manifest

**Files:**
- Create: `/mnt/c/dev/csharpcodegen/cs2.cpp/CPPFeatureManifestWriter.cs`
- Create: `/mnt/c/dev/csharpcodegen/cs2.cpp/.net.cpp/runtime/feature_manifest.hpp`
- Create: `/mnt/c/dev/csharpcodegen/cs2.cpp/.net.cpp/runtime/feature_manifest.cpp`
- Modify: `/mnt/c/dev/csharpcodegen/cs2.cpp/CPPConversionReportWriter.cs`
- Modify: `/mnt/c/dev/csharpcodegen/cs2.cpp/CPPCodeConverter.cs`
- Test: `/mnt/c/dev/csharpcodegen/cs2.cpp.tests/CPPFeatureManifestWriterTests.cs`

- [ ] **Step 1: Write failing manifest/report tests**

```csharp
[Fact]
public void WriteManifest_EmitsStaticFeatureEntriesForResolvedBuild() {
    string header = CPPFeatureManifestWriter.WriteHeader(report);

    Assert.Contains("he_feature_enabled", header);
    Assert.Contains("HEFeatureEntry", header);
}

[Fact]
public void WriteReport_EmitsFeatureDecisionSection() {
    string json = CPPConversionReportWriter.Write(report);

    Assert.Contains("featureDecisions", json);
    Assert.Contains("ForcedDisabled", json);
}
```

- [ ] **Step 2: Run the focused tests and verify failure**

Run: `rtk dotnet test /mnt/c/dev/csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPFeatureManifestWriterTests" -v minimal`
Expected: FAIL because the manifest writer and report section do not exist yet.

- [ ] **Step 3: Implement manifest/config/report emission**

```csharp
public sealed class CPPFeatureManifestWriter {
    public string WriteHeader(CPPBuildUsageReport report) { }
    public string WriteSource(CPPBuildUsageReport report) { }
}
```

- [ ] **Step 4: Run the focused tests and verify pass**

Run: `rtk dotnet test /mnt/c/dev/csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPFeatureManifestWriterTests" -v minimal`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git -C /mnt/c/dev/csharpcodegen add cs2.cpp/CPPFeatureManifestWriter.cs cs2.cpp/.net.cpp/runtime/feature_manifest.hpp cs2.cpp/.net.cpp/runtime/feature_manifest.cpp cs2.cpp/CPPConversionReportWriter.cs cs2.cpp.tests/CPPFeatureManifestWriterTests.cs
git -C /mnt/c/dev/csharpcodegen commit -m "Emit cs2.cpp feature manifest and build report"
```

### Task 7: Prove shader and sprite pruning end-to-end

**Files:**
- Modify: `/mnt/c/dev/csharpcodegen/cs2.cpp.tests/CPPFeaturePruningEndToEndTests.cs`
- Modify: `/mnt/c/dev/csharpcodegen/cs2.cpp.tests/TestHelpers/CppProcessorTestHarness.cs`
- Modify: `/mnt/c/dev/csharpcodegen/cs2.cpp.tests/TestHelpers/RoslynTestHelper.cs`
- Modify: `/mnt/c/dev/csharpcodegen/cs2.cpp/CPPCompileHarnessWriter.cs`

- [ ] **Step 1: Write failing end-to-end shader and sprite pruning tests**

```csharp
[Fact]
public void WriteOutput_WhenSpritesAreDisabled_DoesNotEmitSpriteSubsystemFiles() {
    CPPConversionResult result = ConvertWithFeatureDisabled(CPPFeatureKind.Sprites);

    Assert.DoesNotContain(result.GeneratedFiles.Keys, path => path.Contains("Sprite", StringComparison.Ordinal));
}

[Fact]
public void WriteOutput_ReportsForcedDisableConflict_WhenShaderAssetIsReachable() {
    CPPConversionResult result = ConvertShaderFixtureWithFeatureDisabled();

    Assert.Contains(result.BuildUsageReport.Conflicts, conflict => conflict.Feature == CPPFeatureKind.Shaders);
}
```

- [ ] **Step 2: Run the focused tests and verify failure**

Run: `rtk dotnet test /mnt/c/dev/csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPFeaturePruningEndToEndTests" -v minimal`
Expected: FAIL because the output is not yet being validated on end-to-end pruning behavior.

- [ ] **Step 3: Add test helper coverage and compile-harness assertions**

```csharp
public CPPConversionResult ConvertWithFeatureOverrides(
    string source,
    Action<CPPBuildFeatureProfile> configureProfile) {
    // Build a fixture, run conversion, and return generated files + report.
}
```

- [ ] **Step 4: Run the focused tests and verify pass**

Run: `rtk dotnet test /mnt/c/dev/csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPFeaturePruningEndToEndTests" -v minimal`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git -C /mnt/c/dev/csharpcodegen add cs2.cpp.tests/CPPFeaturePruningEndToEndTests.cs cs2.cpp.tests/TestHelpers/CppProcessorTestHarness.cs cs2.cpp.tests/TestHelpers/RoslynTestHelper.cs cs2.cpp/CPPCompileHarnessWriter.cs
git -C /mnt/c/dev/csharpcodegen commit -m "Prove cs2.cpp shader and sprite feature pruning"
```

### Task 8: Run the real `helengine.core` proof and document the result

**Files:**
- Modify: `/mnt/c/dev/csharpcodegen/cs2.cpp.tests/CPPHelengineCoreAuditTests.cs`
- Modify: `/mnt/c/dev/csharpcodegen/cs2.cpp/CPPCodeConverter.cs`
- Modify: `/mnt/c/dev/csharpcodegen/cs2.cpp/CPPConversionReportWriter.cs`

- [ ] **Step 1: Write a failing regression around real build usage report content**

```csharp
[Fact]
public void HelengineCoreAudit_WhenShadersAreForceDisabled_ReportsFeatureConflictOrPrunedOutput() {
    CPPConversionResult result = RunHelengineCoreAuditWithProfile(CPPFeatureKind.Shaders, CPPFeatureMode.Disabled);

    Assert.NotNull(result.BuildUsageReport);
}
```

- [ ] **Step 2: Run the focused regression and verify failure**

Run: `rtk dotnet test /mnt/c/dev/csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPHelengineCoreAuditTests" -v minimal`
Expected: FAIL because the real audit path does not yet exercise feature profiles.

- [ ] **Step 3: Wire the real audit path through feature profiles and reports**

```csharp
CPPConversionOptions options = new CPPConversionOptions {
    BuildFeatureProfile = CPPBuildFeatureProfile.CreateDefault()
        .WithMode(CPPFeatureKind.Shaders, CPPFeatureMode.Disabled),
};
```

- [ ] **Step 4: Run the real proof commands and verify pass**

Run: `rtk dotnet test /mnt/c/dev/csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPHelengineCoreAuditTests|FullyQualifiedName~CPPFeaturePruningEndToEndTests|FullyQualifiedName~CPPFeatureManifestWriterTests|FullyQualifiedName~CPPReachabilityPlannerTests|FullyQualifiedName~CPPFeatureScannerTests|FullyQualifiedName~CPPFeatureResolverTests|FullyQualifiedName~CPPFeatureProfileTests" -v minimal`
Expected: PASS

Run: `rtk dotnet run --project /tmp/cs2cpp-audit-runner/AuditRunner.csproj -v minimal`
Expected: conversion succeeds and emits `cpp-build-feature-report.json`

Run: `rtk bash /tmp/helengine-core-cpp/build_gcc.sh`
Expected: compile front is at or beyond the previous baseline, with feature-pruned output available for inspection

- [ ] **Step 5: Commit**

```bash
git -C /mnt/c/dev/csharpcodegen add cs2.cpp.tests/CPPHelengineCoreAuditTests.cs cs2.cpp/CPPCodeConverter.cs cs2.cpp/CPPConversionReportWriter.cs
git -C /mnt/c/dev/csharpcodegen commit -m "Validate cs2.cpp feature-pruned helengine.core builds"
```

## Final verification checklist

- [ ] Run: `rtk dotnet test /mnt/c/dev/csharpcodegen/cs2.cpp.tests/cs2.cpp.tests.csproj --filter "FullyQualifiedName~CPPFeatureProfileTests|FullyQualifiedName~CPPFeatureResolverTests|FullyQualifiedName~CPPFeatureScannerTests|FullyQualifiedName~CPPReachabilityPlannerTests|FullyQualifiedName~CPPFeatureManifestWriterTests|FullyQualifiedName~CPPFeaturePruningEndToEndTests|FullyQualifiedName~CPPHelengineCoreAuditTests" -v minimal`
- [ ] Run: `rtk dotnet run --project /tmp/cs2cpp-audit-runner/AuditRunner.csproj -v minimal`
- [ ] Run: `rtk bash /tmp/helengine-core-cpp/build_gcc.sh`
- [ ] Confirm generated output includes `cpp-build-feature-report.json`
- [ ] Confirm generated runtime includes feature manifest files
- [ ] Confirm force-disabled `Shaders` and `Sprites` remove subsystem output or report hard conflicts deterministically
