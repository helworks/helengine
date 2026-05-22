# Shader Runtime Content Extraction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move shader runtime content contracts, shader package metadata, and shader-specific content registration out of `helengine.core` into `helengine.shader` while preserving generic content loading in core.

**Architecture:** The change is an ownership move, not a behavioral rewrite. `helengine.core` keeps only generic content registration and asset loading seams, while `helengine.shader` becomes the home for shader package asset types, reflection metadata, content processor ids, and shader registration/bootstrap helpers. Shader-aware backends and editor flows are then repointed to `helengine.shader`, and shader-specific bootstrap logic is removed from core.

**Tech Stack:** C#, .NET 9, existing HelEngine runtime content pipeline, xUnit, DirectX11/Vulkan renderer projects.

---

### Task 1: Inventory Core Shader Runtime Content Ownership

**Files:**
- Inspect: `engine/helengine.core/content/RuntimeContentManagerConfiguration.cs`
- Inspect: `engine/helengine.core/content/RuntimeContentProcessorIds.cs`
- Inspect: `engine/helengine.core/shaders/`
- Inspect: `engine/helengine.core/Core.cs`
- Inspect: `engine/helengine.shader/`
- Inspect: `engine/helengine.directx11/`
- Inspect: `engine/helengine.vulkan/`
- Inspect: `engine/helengine.editor/`

- [ ] **Step 1: Record the current shader-owned files and compile-time consumers**

Collect the exact current ownership surface before moving files:

```text
engine/helengine.core/content/RuntimeContentManagerConfiguration.cs
engine/helengine.core/content/RuntimeContentProcessorIds.cs
engine/helengine.core/shaders/*
engine/helengine.core/Core.cs
```

Also list every project that references these types directly so the move has an explicit repair list:

```text
helengine.shader
helengine.directx11
helengine.vulkan
helengine.editor
helengine.editor.tests
```

- [ ] **Step 2: Verify the inventory with a focused text search**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "rg -n --glob 'engine/helengine.core/**' --glob 'engine/helengine.shader/**' --glob 'engine/helengine.directx11/**' --glob 'engine/helengine.vulkan/**' --glob 'engine/helengine.editor/**' 'Shader|shader' 'C:\dev\helworks\helengine' 2>&1 | Select-Object -First 220 | Out-String -Width 260 | Write-Output"
```

Expected: shader-specific content ids, package registration, metadata types, and shader-target decisions still appear in `helengine.core`.

- [ ] **Step 3: Commit the inventory checkpoint**

```bash
git add .
git commit -m "chore: checkpoint shader runtime content inventory"
```

Expected: one small checkpoint commit before file moves begin.

### Task 2: Move Shader Runtime Metadata Types Into `helengine.shader`

**Files:**
- Create or move: `engine/helengine.shader/shaders/*`
- Modify: `engine/helengine.shader/helengine.shader.csproj`
- Delete or move from: `engine/helengine.core/shaders/*`
- Test: `engine/helengine.editor.tests/*shader*`

- [ ] **Step 1: Write the failing compile-time expectation**

Add or update one focused test that proves shader runtime package types are no longer owned by `helengine.core`. Prefer a source-level ownership assertion in editor tests, for example:

```csharp
using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies shader runtime content contracts are no longer owned by helengine.core.
/// </summary>
public sealed class ShaderRuntimeOwnershipSourceTests {
    /// <summary>
    /// Ensures shader runtime metadata files live under helengine.shader instead of helengine.core.
    /// </summary>
    [Fact]
    public void Shader_runtime_metadata_is_not_owned_by_core() {
        Assert.False(Directory.Exists(@"C:\dev\helworks\helengine\engine\helengine.core\shaders"));
        Assert.True(Directory.Exists(@"C:\dev\helworks\helengine\engine\helengine.shader\shaders"));
    }
}
```

- [ ] **Step 2: Run the new ownership test to verify it fails**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~ShaderRuntimeOwnershipSourceTests' 2>&1 | Select-Object -Last 120 | Out-String -Width 240 | Write-Output"
```

Expected: FAIL because the shader metadata directory still exists under `helengine.core`.

- [ ] **Step 3: Move the shader runtime metadata tree into `helengine.shader`**

Move the current `engine/helengine.core/shaders/` tree into `engine/helengine.shader/shaders/`, preserving one-class-per-file and current type names unless a namespace change is required.

Concrete moved surface should include the existing files such as:

```text
engine/helengine.core/shaders/IShaderModule.cs
engine/helengine.core/shaders/ShaderBinding.cs
engine/helengine.core/shaders/ShaderCompileTarget.cs
engine/helengine.core/shaders/ShaderConstantMember.cs
engine/helengine.core/shaders/ShaderModuleDefinition.cs
engine/helengine.core/shaders/ShaderProgramBinary.cs
engine/helengine.core/shaders/ShaderProgramDefinition.cs
engine/helengine.core/shaders/ShaderResourceType.cs
engine/helengine.core/shaders/ShaderStage.cs
engine/helengine.core/shaders/ShaderVariant.cs
engine/helengine.core/shaders/ShaderVertexElement.cs
engine/helengine.core/shaders/packages/ShaderModulePackage.cs
engine/helengine.core/shaders/packages/ShaderModulePackageReader.cs
engine/helengine.core/shaders/StandardMeshShaderData.cs
```

The moved files should use shader ownership namespaces, for example:

```csharp
namespace helengine.shader {
    /// <summary>
    /// Describes a compiled shader program binary for a target backend.
    /// </summary>
    public class ShaderProgramBinary {
        // existing implementation preserved under helengine.shader ownership
    }
}
```

- [ ] **Step 4: Update `helengine.shader.csproj` to compile the moved shader runtime files**

Ensure `engine/helengine.shader/helengine.shader.csproj` includes the moved shader tree and still references whichever generic projects are actually required by those types.

Minimal expectation:

```xml
<ItemGroup>
  <ProjectReference Include="..\helengine.core\helengine.core.csproj" />
  <ProjectReference Include="..\helengine.files\helengine.files.csproj" />
</ItemGroup>
```

Only keep references actually needed by the moved types.

- [ ] **Step 5: Delete the old core shader tree**

Remove the old `engine/helengine.core/shaders/` files after the moved copies compile from `helengine.shader`.

- [ ] **Step 6: Run the ownership test again**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~ShaderRuntimeOwnershipSourceTests' 2>&1 | Select-Object -Last 120 | Out-String -Width 240 | Write-Output"
```

Expected: PASS, proving the metadata tree is no longer owned by core.

- [ ] **Step 7: Commit the metadata move**

```bash
git add engine/helengine.core engine/helengine.shader engine/helengine.editor.tests
git commit -m "move shader runtime metadata into helengine.shader"
```

### Task 3: Move Shader Content Processor Ids and Package Registration Out of Core

**Files:**
- Modify: `engine/helengine.core/content/RuntimeContentManagerConfiguration.cs`
- Modify: `engine/helengine.core/content/RuntimeContentProcessorIds.cs`
- Create: `engine/helengine.shader/content/ShaderRuntimeContentProcessorIds.cs`
- Create: `engine/helengine.shader/content/ShaderRuntimeContentRegistration.cs`
- Test: `engine/helengine.editor.tests/RuntimeContentManagerConfigurationSourceTests.cs`

- [ ] **Step 1: Write the failing source-level tests for content registration ownership**

Add focused tests proving core no longer contains shader package registration and that shader package registration exists in `helengine.shader`.

```csharp
using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies shader package runtime content registration is not owned by helengine.core.
/// </summary>
public sealed class RuntimeContentManagerConfigurationSourceTests {
    /// <summary>
    /// Ensures core runtime content registration does not hardcode shader package ids or extensions.
    /// </summary>
    [Fact]
    public void Core_runtime_content_registration_does_not_register_shader_packages() {
        string source = File.ReadAllText(@"C:\dev\helworks\helengine\engine\helengine.core\content\RuntimeContentManagerConfiguration.cs");

        Assert.DoesNotContain("runtime.shader-asset", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".shader.asset", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ShaderAsset", source, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run the content-registration ownership test to verify it fails**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~RuntimeContentManagerConfigurationSourceTests' 2>&1 | Select-Object -Last 120 | Out-String -Width 240 | Write-Output"
```

Expected: FAIL because core still registers shader packages.

- [ ] **Step 3: Create shader-owned processor ids and shader registration bootstrap**

Add shader-specific content ownership in `helengine.shader`, for example:

```csharp
namespace helengine.shader {
    /// <summary>
    /// Stores shader-runtime-specific processor ids used by shader package loading.
    /// </summary>
    public static class ShaderRuntimeContentProcessorIds {
        /// <summary>
        /// Processor id used for serialized shader assets.
        /// </summary>
        public const string ShaderAsset = "runtime.shader-asset";
    }
}
```

And one shader bootstrap helper:

```csharp
namespace helengine.shader {
    /// <summary>
    /// Registers shader package runtime content processors into a generic content manager.
    /// </summary>
    public static class ShaderRuntimeContentRegistration {
        /// <summary>
        /// Registers shader package processors and extensions onto the supplied runtime content manager.
        /// </summary>
        /// <param name="contentManager">Generic content manager receiving shader registrations.</param>
        public static void Register(ContentManager contentManager) {
            if (contentManager == null) {
                throw new ArgumentNullException(nameof(contentManager));
            }

            contentManager.RegisterProcessor(
                ShaderRuntimeContentProcessorIds.ShaderAsset,
                new AssetContentProcessor<ShaderAsset>(),
                new[] { ".shader.asset" });
        }
    }
}
```

- [ ] **Step 4: Strip shader-specific registration details out of core**

Remove shader package ids and `.shader.asset` registration from:

```text
engine/helengine.core/content/RuntimeContentManagerConfiguration.cs
engine/helengine.core/content/RuntimeContentProcessorIds.cs
```

Core should keep only generic registration helpers and generic processor ids.

- [ ] **Step 5: Repoint shader-aware call sites to shader bootstrap**

Any caller that previously depended on core shader registration should now call into `ShaderRuntimeContentRegistration.Register(...)`.

Start with whichever sites currently instantiate shader package content readers or generic runtime content managers for shader loading.

- [ ] **Step 6: Run the focused source tests again**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~RuntimeContentManagerConfigurationSourceTests' 2>&1 | Select-Object -Last 120 | Out-String -Width 240 | Write-Output"
```

Expected: PASS, showing shader package registration is no longer hardcoded into core.

- [ ] **Step 7: Commit the registration move**

```bash
git add engine/helengine.core engine/helengine.shader engine/helengine.editor.tests
git commit -m "move shader content registration into helengine.shader"
```

### Task 4: Remove Shader Target Decisions From Core Bootstrap

**Files:**
- Modify: `engine/helengine.core/Core.cs`
- Modify: shader-aware bootstrap or backend files that now own target selection
- Test: `engine/helengine.editor.tests/CoreShaderBootstrapSourceTests.cs`

- [ ] **Step 1: Write the failing source test for core shader-target ownership**

Add a focused source-level test proving `Core.cs` no longer hardcodes shader compile target selection.

```csharp
using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies generic core bootstrap does not own shader target selection.
/// </summary>
public sealed class CoreShaderBootstrapSourceTests {
    /// <summary>
    /// Ensures core bootstrap does not reference modern shader compile targets directly.
    /// </summary>
    [Fact]
    public void Core_does_not_own_shader_target_selection() {
        string source = File.ReadAllText(@"C:\dev\helworks\helengine\engine\helengine.core\Core.cs");

        Assert.DoesNotContain("ShaderCompileTarget", source, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run the failing test**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~CoreShaderBootstrapSourceTests' 2>&1 | Select-Object -Last 120 | Out-String -Width 240 | Write-Output"
```

Expected: FAIL because `Core.cs` still references shader target selection.

- [ ] **Step 3: Move shader-target decisions to shader-aware layers**

Remove the direct shader target decision from `engine/helengine.core/Core.cs`, and relocate that decision to whichever shader-aware bootstrap/helper/backend actually needs it.

The post-change shape should be:

```csharp
// Core.cs
// no direct ShaderCompileTarget ownership here
```

And shader-aware code should own the target decision, for example inside a shader bootstrap helper or renderer-side initialization path.

- [ ] **Step 4: Rerun the source test**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~CoreShaderBootstrapSourceTests' 2>&1 | Select-Object -Last 120 | Out-String -Width 240 | Write-Output"
```

Expected: PASS.

- [ ] **Step 5: Commit the bootstrap cleanup**

```bash
git add engine/helengine.core engine/helengine.shader engine/helengine.editor.tests
git commit -m "remove shader target selection from core"
```

### Task 5: Repoint Shader-Aware Consumers and Validate Build Graph

**Files:**
- Modify: `engine/helengine.directx11/**/*`
- Modify: `engine/helengine.vulkan/**/*`
- Modify: `engine/helengine.editor/**/*`
- Modify: any test files still importing shader metadata from core
- Test: focused shader-aware editor tests

- [ ] **Step 1: Write the failing compile/build expectation**

Use the existing consumer projects as the failing integration surface. The failure should be unresolved namespaces or missing references after the move.

Expected compile targets:

```text
engine/helengine.directx11/helengine.directx11.csproj
engine/helengine.vulkan/helengine.vulkan.csproj
engine/helengine.editor/helengine.editor.csproj
```

- [ ] **Step 2: Run the first focused build to capture the consumer break**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet build 'engine\helengine.directx11\helengine.directx11.csproj' 2>&1 | Select-Object -Last 120 | Out-String -Width 240 | Write-Output"
```

Expected: FAIL until namespaces or references are repaired.

- [ ] **Step 3: Repoint the renderer and editor consumers to `helengine.shader`**

Update namespaces, project references, and imports so consumers now resolve moved shader types from `helengine.shader`.

Typical change shape:

```csharp
using helengine.shader;
```

And ensure the relevant `.csproj` files reference:

```xml
<ProjectReference Include="..\helengine.shader\helengine.shader.csproj" />
```

- [ ] **Step 4: Run focused builds until consumers compile cleanly**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet build 'engine\helengine.directx11\helengine.directx11.csproj' 2>&1 | Select-Object -Last 120 | Out-String -Width 240 | Write-Output"
rtk proxy powershell -NoProfile -Command "dotnet build 'engine\helengine.vulkan\helengine.vulkan.csproj' 2>&1 | Select-Object -Last 120 | Out-String -Width 240 | Write-Output"
rtk proxy powershell -NoProfile -Command "dotnet build 'engine\helengine.editor\helengine.editor.csproj' 2>&1 | Select-Object -Last 120 | Out-String -Width 240 | Write-Output"
```

Expected: all three builds succeed.

- [ ] **Step 5: Run focused shader-aware content tests**

Run the smallest tests that prove shader package loading still works through generic content registration:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~BuiltInMaterialIdsTests|FullyQualifiedName~MaterialLayoutBuilderTests|FullyQualifiedName~MaterialPropertyBlockTests|FullyQualifiedName~RuntimeMaterialTests' 2>&1 | Select-Object -Last 180 | Out-String -Width 240 | Write-Output"
```

Expected: PASS for the focused shader-aware regression slice.

- [ ] **Step 6: Commit the consumer rewiring**

```bash
git add engine/helengine.directx11 engine/helengine.vulkan engine/helengine.editor engine/helengine.editor.tests engine/helengine.shader
git commit -m "repoint shader consumers to helengine.shader"
```

### Task 6: Final Verification and Cleanup

**Files:**
- Verify: `engine/helengine.core/**/*`
- Verify: `engine/helengine.shader/**/*`
- Verify: `engine/helengine.directx11/**/*`
- Verify: `engine/helengine.vulkan/**/*`
- Verify: `engine/helengine.editor/**/*`

- [ ] **Step 1: Run the final focused verification suite**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet build 'engine\helengine.shader\helengine.shader.csproj' 2>&1 | Select-Object -Last 120 | Out-String -Width 240 | Write-Output"
rtk proxy powershell -NoProfile -Command "dotnet build 'engine\helengine.core\helengine.core.csproj' 2>&1 | Select-Object -Last 120 | Out-String -Width 240 | Write-Output"
rtk proxy powershell -NoProfile -Command "dotnet build 'engine\helengine.directx11\helengine.directx11.csproj' 2>&1 | Select-Object -Last 120 | Out-String -Width 240 | Write-Output"
rtk proxy powershell -NoProfile -Command "dotnet build 'engine\helengine.vulkan\helengine.vulkan.csproj' 2>&1 | Select-Object -Last 120 | Out-String -Width 240 | Write-Output"
rtk proxy powershell -NoProfile -Command "dotnet build 'engine\helengine.editor\helengine.editor.csproj' 2>&1 | Select-Object -Last 120 | Out-String -Width 240 | Write-Output"
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~ShaderRuntimeOwnershipSourceTests|FullyQualifiedName~RuntimeContentManagerConfigurationSourceTests|FullyQualifiedName~CoreShaderBootstrapSourceTests|FullyQualifiedName~BuiltInMaterialIdsTests|FullyQualifiedName~MaterialLayoutBuilderTests|FullyQualifiedName~MaterialPropertyBlockTests|FullyQualifiedName~RuntimeMaterialTests' 2>&1 | Select-Object -Last 220 | Out-String -Width 240 | Write-Output"
```

Expected: all builds pass and the focused editor test slice passes.

- [ ] **Step 2: Verify core no longer owns shader runtime content**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "rg -n --glob 'engine/helengine.core/**' 'runtime.shader-asset|\\.shader\\.asset|ShaderModuleDefinition|ShaderProgramDefinition|ShaderProgramBinary|ShaderBinding|ShaderCompileTarget' 'C:\dev\helworks\helengine' 2>&1 | Select-Object -First 120 | Out-String -Width 240 | Write-Output"
```

Expected: no remaining shader-runtime ownership hits in `helengine.core`, except generic seams or names that are explicitly intended to remain.

- [ ] **Step 3: Commit the final boundary cleanup**

```bash
git add engine/helengine.core engine/helengine.shader engine/helengine.directx11 engine/helengine.vulkan engine/helengine.editor engine/helengine.editor.tests
git commit -m "extract shader runtime content from core"
```

- [ ] **Step 4: Record any residual follow-up work**

If the grep in Step 2 still finds legitimate generic seams that mention shader names indirectly, document that explicitly in the final summary rather than silently leaving ambiguity.
