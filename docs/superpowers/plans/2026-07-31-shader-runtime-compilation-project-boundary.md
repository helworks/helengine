# Shader Runtime/Compilation Project Boundary Implementation Plan

> **For Helena:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task by task on the existing main workspaces. Do not create a worktree.

**Goal:** Prevent editor shader compiler, parser, and cache code from entering native code generation while keeping runtime shader package and binding contracts available on every platform.

**Architecture:** `helengine.shader` remains the native/runtime assembly. A new managed-only `helengine.shader.compilation` assembly links the compiler implementation sources that currently live under `helengine.shader/shaders/compilation`. Four genuine runtime contracts move out of that folder and remain in `helengine.shader`. Managed compiler consumers reference the new project explicitly; `EditorGeneratedCoreRegenerationService` continues to submit only `helengine.shader` to native codegen.

**Tech Stack:** C# 13, .NET 9 SDK projects, xUnit, csharpcodegen C++ backend, HelEngine editor CLI/build waiter, Windows native DemoDisc build.

---

## Task 1: Add a failing source-boundary test

**Files:**

- Create: `engine/helengine.editor.tests/ShaderProjectBoundaryTests.cs`

### Step 1: Write the failing project-structure test

Create `ShaderProjectBoundaryTests` with substantive XML comments on the class and every method. Reuse the repository-root walk used by `CoreShaderBootstrapSourceTests`.

The first test must assert all of the intended source boundaries without taking a compile-time dependency on the not-yet-created project:

```csharp
[Fact]
public void Shader_runtime_and_compilation_sources_have_distinct_project_boundaries() {
    string repositoryRootPath = ResolveRepositoryRootPath();
    string runtimeProjectPath = Path.Combine(
        repositoryRootPath,
        "engine",
        "helengine.shader",
        "helengine.shader.csproj");
    string compilationProjectPath = Path.Combine(
        repositoryRootPath,
        "engine",
        "helengine.shader.compilation",
        "helengine.shader.compilation.csproj");

    Assert.True(File.Exists(compilationProjectPath));

    string runtimeProject = File.ReadAllText(runtimeProjectPath);
    string compilationProject = File.ReadAllText(compilationProjectPath);

    Assert.Contains("shaders\\compilation\\**\\*.cs", runtimeProject, StringComparison.Ordinal);
    Assert.Contains("Compile Remove", runtimeProject, StringComparison.Ordinal);
    Assert.Contains("..\\helengine.shader\\shaders\\compilation\\**\\*.cs", compilationProject, StringComparison.Ordinal);
    Assert.Contains("..\\helengine.shader\\helengine.shader.csproj", compilationProject, StringComparison.Ordinal);
}
```

Add assertions that these four runtime files exist outside `shaders/compilation`:

- `shaders/runtime/ShaderCompileTarget.cs`
- `shaders/runtime/ShaderTargetNames.cs`
- `shaders/runtime/ShaderBindingPolicy.cs`
- `shaders/runtime/ShaderBindingPolicies.cs`

### Step 2: Run the focused test and confirm RED

Run:

```powershell
dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~ShaderProjectBoundaryTests
```

Expected: FAIL because `engine/helengine.shader.compilation/helengine.shader.compilation.csproj` and the four runtime-path files do not exist yet.

Do not commit the red state.

---

## Task 2: Split runtime shader contracts from compiler implementation

**Files:**

- Modify: `engine/helengine.shader/helengine.shader.csproj`
- Create: `engine/helengine.shader.compilation/helengine.shader.compilation.csproj`
- Move: `engine/helengine.shader/shaders/compilation/ShaderCompileTarget.cs` to `engine/helengine.shader/shaders/runtime/ShaderCompileTarget.cs`
- Move: `engine/helengine.shader/shaders/compilation/ShaderTargetNames.cs` to `engine/helengine.shader/shaders/runtime/ShaderTargetNames.cs`
- Move: `engine/helengine.shader/shaders/compilation/ShaderBindingPolicy.cs` to `engine/helengine.shader/shaders/runtime/ShaderBindingPolicy.cs`
- Move: `engine/helengine.shader/shaders/compilation/ShaderBindingPolicies.cs` to `engine/helengine.shader/shaders/runtime/ShaderBindingPolicies.cs`

### Step 1: Move the four runtime contracts without changing their APIs

Use an `apply_patch` move for each file so history and current content remain intact. Do not rename the namespace or public types. These types are runtime data/package contracts despite their current directory.

### Step 2: Exclude compiler implementation from `helengine.shader`

Add this item group to `helengine.shader.csproj`:

```xml
<ItemGroup>
  <Compile Remove="shaders\compilation\**\*.cs" />
</ItemGroup>
```

The runtime project must retain only its existing `helengine.core` reference. It must never reference `helengine.shader.compilation`.

### Step 3: Create the managed compiler project

Create `helengine.shader.compilation.csproj` with the same target framework/usings/nullability conventions as `helengine.shader`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\helengine.shader\helengine.shader.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Compile Include="..\helengine.shader\shaders\compilation\**\*.cs"
             Link="shaders\compilation\%(RecursiveDir)%(Filename)%(Extension)" />
  </ItemGroup>
</Project>
```

No compiler files are copied or generated. The new project compiles the existing authoritative C# sources directly.

### Step 4: Run the source-boundary test and focused project builds

Run:

```powershell
dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~ShaderProjectBoundaryTests
dotnet build engine\helengine.shader\helengine.shader.csproj --no-restore
dotnet build engine\helengine.shader.compilation\helengine.shader.compilation.csproj
```

Expected: the boundary test passes; both projects build. If the test project build fails before executing because downstream consumers still lack the new reference, continue directly to Task 3 and rerun the test there. Do not weaken the boundary to restore compilation.

---

## Task 3: Give managed compiler consumers explicit dependencies

**Files:**

- Modify: `engine/helengine.directx11/helengine.directx11.csproj`
- Modify: `engine/helengine.vulkan/helengine.vulkan.csproj`
- Modify: `engine/helengine.editor/helengine.editor.csproj`
- Modify: `engine/helengine.render.validation/helengine.render.validation.csproj`
- Modify: `engine/helengine.editor.tests/helengine.editor.tests.csproj`
- Modify: `helengine.ui/helengine.editor.app/helengine.editor.app.csproj`
- Modify: `helengine.ui/helengine.sln`
- Modify: `engine/helengine.editor.tests/ShaderProjectBoundaryTests.cs`
- Modify: `C:/dev/helworks/helengine-psvita/builder/helengine.psvita.builder.csproj`
- Modify: `C:/dev/helworks/helengine-psvita/builder.tests/helengine.psvita.builder.tests.csproj`
- Modify: `C:/dev/helworks/helengine-wiiu/builder/helengine.wiiu.builder.csproj`
- Modify: `C:/dev/helworks/helengine-wiiu/builder.tests/helengine.wiiu.builder.tests.csproj`

### Step 1: Add direct project references

Add `helengine.shader.compilation` to every project that directly names compiler APIs:

```xml
<ProjectReference Include="..\helengine.shader.compilation\helengine.shader.compilation.csproj" />
```

Use the appropriate `..\..\engine\...` relative path in `helengine.editor.app.csproj`. Preserve each file's existing `SkipGetTargetFrameworkProperties` convention.

The DirectX11 project needs the reference because `DirectX11ShaderBackend`, `DirectX11ShaderAssetBuilder`, and the runtime source compiler use generic compile services/resolvers. The Vulkan project needs it because `VulkanShaderBackend` implements the generic backend contract. Editor, render validation, editor tests, and the editor app directly use registry/request/service types.

Do not add this reference to runtime-only `helengine.files`, `helengine.editor.assimp`, or `helengine.shader`.

The out-of-tree PS Vita and Wii U builders are also direct consumers. Add a project reference using each repository's existing `HelengineRoot`/`HelEngineRoot` property to both builder projects and both focused test projects:

```xml
<ProjectReference Include="$(HelengineRoot)\engine\helengine.shader.compilation\helengine.shader.compilation.csproj" />
```

Preserve the exact property capitalization already used by each repository (`HelengineRoot` for PS Vita, `HelEngineRoot` for Wii U). The Nintendo DS source stager only contains generated C++ text mentioning compiler type names and does not receive this managed project reference.

### Step 2: Add the project to the editor solution

From `C:\dev\helworks\helengine\helengine.ui`, run:

```powershell
dotnet sln helengine.sln add ..\engine\helengine.shader.compilation\helengine.shader.compilation.csproj
```

Review the solution diff and ensure only the new project declaration/configuration entries were added.

### Step 3: Add assembly-identity assertions

Extend `ShaderProjectBoundaryTests` with a second test:

```csharp
[Fact]
public void Runtime_contracts_and_compiler_services_are_emitted_by_separate_assemblies() {
    System.Reflection.Assembly runtimeAssembly = typeof(ShaderCompileTarget).Assembly;
    System.Reflection.Assembly compilationAssembly = typeof(ShaderCompileService).Assembly;

    Assert.Equal("helengine.shader", runtimeAssembly.GetName().Name);
    Assert.Equal("helengine.shader.compilation", compilationAssembly.GetName().Name);
    Assert.Same(runtimeAssembly, typeof(ShaderBindingPolicy).Assembly);
    Assert.Null(runtimeAssembly.GetType("helengine.ShaderCompileService"));
    Assert.Null(runtimeAssembly.GetType("helengine.HlslShaderBindingParser"));
    Assert.NotNull(compilationAssembly.GetType("helengine.ShaderCompileService"));
    Assert.NotNull(compilationAssembly.GetType("helengine.HlslShaderBindingParser"));
}
```

### Step 4: Build all direct consumers and rerun the boundary test

Run:

```powershell
dotnet build engine\helengine.directx11\helengine.directx11.csproj
dotnet build engine\helengine.vulkan\helengine.vulkan.csproj
dotnet build engine\helengine.editor\helengine.editor.csproj
dotnet build engine\helengine.render.validation\helengine.render.validation.csproj
dotnet build helengine.ui\helengine.editor.app\helengine.editor.app.csproj
dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~ShaderProjectBoundaryTests
dotnet test C:\dev\helworks\helengine-psvita\builder.tests\helengine.psvita.builder.tests.csproj --filter "FullyQualifiedName~PsVitaShaderBackend"
dotnet test C:\dev\helworks\helengine-wiiu\builder.tests\helengine.wiiu.builder.tests.csproj --filter "FullyQualifiedName~WiiUShaderBackendTests|FullyQualifiedName~WiiURuntimeSourceTests"
```

Expected: all projects compile and both boundary tests pass.

---

## Task 4: Remove the diagnostic-only compiler ownership patches

**Files:**

- Modify: `engine/helengine.shader/shaders/compilation/HlslShaderBindingParser.cs`
- Modify: `engine/helengine.shader/shaders/compilation/IShaderCompileCache.cs`
- Modify: `engine/helengine.shader/shaders/compilation/ShaderCompileRequest.cs`
- Modify: `engine/helengine.shader/shaders/compilation/ShaderCompileService.cs`
- Modify: `engine/helengine.shader/shaders/compilation/ShaderConditionalPreprocessor.cs`
- Modify: `engine/helengine.shader/shaders/compilation/ShaderMemoryCompileCache.cs`
- Delete: `engine/helengine.editor.tests/ShaderCompileServiceNativeOwnershipContractTests.cs`
- Delete: `engine/helengine.editor.tests/ShaderConditionalPreprocessorNativeOwnershipContractTests.cs`

### Step 1: Re-inspect only the compiler diagnostic diff

Run:

```powershell
git diff -- engine/helengine.shader/shaders/compilation engine/helengine.editor.tests/ShaderCompileServiceNativeOwnershipContractTests.cs engine/helengine.editor.tests/ShaderConditionalPreprocessorNativeOwnershipContractTests.cs
```

Confirm the hunks still match the current diagnostic pass. Preserve every unrelated user/agent edit elsewhere in the dirty workspace.

### Step 2: Reverse only the known diagnostic hunks with `apply_patch`

Remove:

- `NativeNoEscape` cache-key parameters added to `IShaderCompileCache` and `ShaderMemoryCompileCache`;
- `IDisposable`, `NativeOwnedMember`, `NativeTakesOwnership`, `NativeBorrowedReturn`, and `NativeOwnership.Release` changes added to `ShaderCompileRequest`;
- borrowed-return annotations and explicit cache-key deletion added to `ShaderCompileService`;
- the ownership-only `AddConstantBufferBinding` extraction in `HlslShaderBindingParser`, restoring the original inline binding construction;
- `NativeBorrowedReturn` added to `ShaderConditionalPreprocessor.GetCurrentFrame`;
- the two untracked compiler-native ownership contract test files.

Do not remove or alter the separate runtime ownership changes/tests for `ShaderContentProcessorBase`, `ShaderDefinition`, or `ShaderRuntimeMaterial`.

### Step 3: Run compiler behavior tests

Run:

```powershell
dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~ShaderProjectBoundaryTests|FullyQualifiedName~HlslShaderBindingParserTests|FullyQualifiedName~ForwardStandardShaderTests|FullyQualifiedName~EditorBuiltInStandardShaderTests"
```

Expected: compiler behavior remains green without native ownership annotations because compiler methods are no longer part of the native project.

### Step 4: Commit the managed project boundary

In `helengine`, stage only the files listed in Tasks 1-4 that belong to that repository, including the four moves and exact project files. Verify with `git diff --cached --stat` and `git diff --cached --check`.

Commit:

```powershell
git commit -m "refactor: separate shader compiler from runtime"
```

In `helengine-psvita`, stage only the two project files, verify the staged diff, and commit:

```powershell
git commit -m "build: reference shader compilation project"
```

In `helengine-wiiu`, stage only the two project files, verify the staged diff, and commit with the same message.

---

## Task 5: Add a hard native-regeneration inventory guard

**Files:**

- Modify: `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`

### Step 1: Write the failing inventory assertion

Add a source-contract test that reads `EditorGeneratedCoreRegenerationService.cs` and verifies the built-in shader native project remains `helengine.shader.csproj` while the compilation project is absent:

```csharp
[Fact]
public void Generated_core_regeneration_never_submits_shader_compilation_project() {
    string sourcePath = Path.Combine(
        ResolveRepositoryRootPath(),
        "engine",
        "helengine.editor",
        "managers",
        "project",
        "EditorGeneratedCoreRegenerationService.cs");
    string source = File.ReadAllText(sourcePath);

    Assert.Contains("helengine.shader.csproj", source, StringComparison.Ordinal);
    Assert.DoesNotContain("helengine.shader.compilation", source, StringComparison.Ordinal);
}
```

Temporarily change the expected forbidden token to `helengine.shader.csproj`, run the test once, and confirm RED. Restore the intended assertion before continuing. This proves the guard executes rather than silently passing because of test discovery.

### Step 2: Run the focused inventory guard

Run:

```powershell
dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~Generated_core_regeneration_never_submits_shader_compilation_project
```

Expected: PASS with the intended assertion.

### Step 3: Commit the guard

Stage only `EditorGeneratedCoreRegenerationServiceTests.cs`, check the staged diff, and commit:

```powershell
git commit -m "test: guard native shader project inventory"
```

---

## Task 6: Prove generated C++ and the Windows native build exclude compiler code

**Files:**

- Verify only; generated outputs belong under `C:\dev\helworks\builds`, never `%TEMP%`.

### Step 1: Build the current codegen executable

From `C:\dev\helworks\csharpcodegen`, run:

```powershell
dotnet build codegen\codegen.csproj -c Release
```

Expected: build succeeds.

### Step 2: Convert only the runtime shader project into a fresh visible output

Use a new non-existing build-id directory such as:

```text
C:\dev\helworks\builds\helengine\windows\shader-project-boundary-B01
```

Run:

```powershell
C:\dev\helworks\csharpcodegen\codegen\bin\Release\net9.0\codegen.exe `
  --cpp `
  --project C:\dev\helworks\helengine\engine\helengine.shader\helengine.shader.csproj `
  --output C:\dev\helworks\builds\helengine\windows\shader-project-boundary-B01 `
  --feature-catalog C:\dev\helworks\helengine\engine\helengine.editor\codegen\features\helengine-feature-catalog.json `
  --platform windows `
  --language cpp `
  --endianness little `
  --set include-project-defined-preprocessor-symbols=false `
  --set load-native-runtime-metadata=true `
  --set type-remaps=System.Numerics.Vector2=helengine.float2`;System.Numerics.Vector3=helengine.float3`;System.Numerics.Vector4=helengine.float4`;System.Numerics.Quaternion=helengine.float4 `
  --set write-conversion-report=true `
  --set additional-preprocessor-symbols=DESKTOP_PLATFORM
```

Expected: conversion succeeds with ownership validation enabled.

### Step 3: Inspect generated type inventory

Run:

```powershell
rg -n "ShaderCompileService|HlslShaderBindingParser|ShaderMemoryCompileCache|ShaderCompileRequest" C:\dev\helworks\builds\helengine\windows\shader-project-boundary-B01
rg -n "ShaderCompileTarget|ShaderTargetNames|ShaderBindingPolicy" C:\dev\helworks\builds\helengine\windows\shader-project-boundary-B01
```

Expected:

- first command: no matches;
- second command: runtime contract matches are present.

If compiler types appear, stop and fix project inclusion; do not add name-based generated-file deletion or codegen exclusions.

### Step 4: Run the exact full Windows DemoDisc build through build-waiter

Use a fresh output directory such as `C:\dev\helworks\builds\demodisc\windows\shader-project-boundary-B01`:

```powershell
dotnet run --project C:\dev\helworks\helengine\tools\build-waiter\helengine.buildwaiter.csproj -- `
  --output C:\dev\helworks\builds\demodisc\windows\shader-project-boundary-B01 `
  --require helengine_windows.exe `
  -- powershell -NoProfile -ExecutionPolicy Bypass -File C:\dev\helworks\helengine\scripts\build-platform.ps1 `
  -Project C:\dev\helprojs\demodisc\project.heproj `
  -Platform windows `
  -BuildProfile debug `
  -Output C:\dev\helworks\builds\demodisc\windows\shader-project-boundary-B01
```

Do not impose an arbitrary timeout. Let build-waiter determine completion from child exit plus the fresh artifact contract.

Expected: build-waiter exits `0` and confirms the new `helengine_windows.exe` is non-empty and newer than the build start.

### Step 5: Launch the exact Windows artifact

Launch `helengine_windows.exe` with its working directory set to the new output directory. Verify it remains alive and reaches splash/loading/menu. Do not launch an older artifact and do not begin PS2 validation until this Windows build passes.

### Step 6: Final repository verification

Run in `C:\dev\helworks\helengine`:

```powershell
git status --short
git log -2 --oneline
```

Confirm the intended HelEngine commits plus the focused PS Vita and Wii U project-reference commits exist, and unrelated dirty files remain untouched in every repository.
