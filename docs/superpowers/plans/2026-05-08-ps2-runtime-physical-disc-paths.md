# PS2 Runtime Physical Disc Paths Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the PS2 build pipeline compute and compile physical disc paths for all packaged assets so the PS2 runtime stops reconstructing filenames at boot and during asset loads.

**Architecture:** Keep the shared engine manifest contract logical and unchanged. Move all PS2 filename conversion into the `helengine-ps2` builder: stage the disc layout with PS2-safe filenames, generate a native PS2 runtime asset-path manifest into `generated-core/runtime`, compile that manifest into the ELF, and simplify the PS2 runtime to resolve startup scenes and later asset loads directly through the emitted physical-path metadata.

**Tech Stack:** C# / .NET 9, xUnit, generated C++ runtime support, PS2SDK (`sceCdSearchFile` / `sceCdRead`), Docker, `helengine-ps2` builder pipeline

---

## File Structure

### Existing files to modify

- `C:\dev\helworks\helengine-ps2\builder\Ps2DiscLayoutWriter.cs`
  Keep the physical staging logic in one place and extend it so it returns the logical-to-physical mapping for every staged cooked file.
- `C:\dev\helworks\helengine-ps2\builder\Ps2PlatformAssetBuilder.cs`
  Reorder the build so disc layout and runtime manifest generation happen before native compile, then verify all selected scenes and dependent assets are still packaged.
- `C:\dev\helworks\helengine-ps2\builder\Ps2BuildWorkspace.cs`
  Add named paths for the generated native runtime manifest files written into `generated-core/runtime`.
- `C:\dev\helworks\helengine-ps2\builder\Program.cs`
  Update the smoke test to use the physical-disc-path contract instead of assuming `main.hasset` exists as a loose logical file path in the output tree.
- `C:\dev\helworks\helengine-ps2\builder.tests\Ps2PlatformAssetBuilderTests.cs`
  Lock in the builder contract: all selected scenes remain packaged, startup resolves through the generated physical manifest, and ISO output still exists.
- `C:\dev\helworks\helengine-ps2\src\platform\ps2\Ps2BootHost.cpp`
  Remove runtime alias reconstruction and switch startup scene and packaged asset reads to the generated physical-path manifest.
- `C:\dev\helworks\helengine-ps2\src\platform\ps2\Ps2BootHost.hpp`
  Declare the focused helpers needed for manifest-based startup and packaged asset loading.

### New files to create

- `C:\dev\helworks\helengine-ps2\builder\Ps2RuntimeAssetPathManifestWriter.cs`
  Small builder-side writer that emits `runtime/runtime_ps2_asset_path_manifest.hpp/.cpp` into the generated-core tree.
- `C:\dev\helworks\helengine-ps2\builder.tests\Ps2RuntimeAssetPathManifestWriterTests.cs`
  Unit tests for generated startup-path and logical-to-physical asset mappings.

## Task 1: Lock The Builder-Owned Physical Path Contract In Tests

**Files:**
- Create: `C:\dev\helworks\helengine-ps2\builder.tests\Ps2RuntimeAssetPathManifestWriterTests.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder.tests\Ps2PlatformAssetBuilderTests.cs`
- Test: `C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj`

- [ ] **Step 1: Write the failing runtime manifest writer tests**

Create `Ps2RuntimeAssetPathManifestWriterTests.cs` with focused expectations for startup and general asset mappings:

```csharp
using helengine.baseplatform.Manifest;
using helengine.ps2.builder;
using Xunit;

namespace helengine.ps2.builder.tests;

public sealed class Ps2RuntimeAssetPathManifestWriterTests {
    [Fact]
    public void Write_WhenStartupSceneExists_EmitsPhysicalStartupPathAndAssetLookupEntries() {
        string rootPath = Path.Combine(Path.GetTempPath(), "ps2-runtime-asset-manifest-tests", Guid.NewGuid().ToString("N"));
        string generatedCoreRootPath = Path.Combine(rootPath, "generated-core");
        Directory.CreateDirectory(Path.Combine(generatedCoreRootPath, "runtime"));

        PlatformBuildManifest manifest = new(
            3,
            "project",
            "1.0.0",
            "1.0.0",
            "Scenes/DemoDiscMainMenu.helen",
            [
                new PlatformBuildScene(
                    "Scenes/DemoDiscMainMenu.helen",
                    "DemoDiscMainMenu",
                    "cooked/scenes/DemoDiscMainMenu.hasset",
                    [],
                    [
                        new KeyValuePair<string, string>("cooked-relative-path", "cooked/scenes/DemoDiscMainMenu.hasset")
                    ])
            ],
            Array.Empty<PlatformBuildAsset>(),
            [
                new PlatformBuildArtifact("cooked/scenes/DemoDiscMainMenu.hasset", "scene:menu", "sha256:scene", "scene", "shared"),
                new PlatformBuildArtifact("cooked/fonts/DemoDiscBody.hefont", "font:body", "sha256:font", "font", "shared")
            ],
            Array.Empty<PlatformBuildCodeModule>(),
            Array.Empty<PlatformArtifactPlacement>(),
            new PlatformContainerWritePlan("ps2-disc-layout", Array.Empty<PlatformContainerArtifact>()));

        Dictionary<string, string> logicalToPhysicalPaths = new(StringComparer.OrdinalIgnoreCase) {
            ["cooked/scenes/DemoDiscMainMenu.hasset"] = "\\COOKED\\SCENES\\DEMODISC.HAS;1",
            ["cooked/fonts/DemoDiscBody.hefont"] = "\\COOKED\\FONTS\\DEMODISC.HEF;1"
        };

        Ps2RuntimeAssetPathManifestWriter writer = new();
        writer.Write(generatedCoreRootPath, manifest, logicalToPhysicalPaths);

        string source = File.ReadAllText(Path.Combine(generatedCoreRootPath, "runtime", "runtime_ps2_asset_path_manifest.cpp"));
        Assert.Contains("kRuntimePs2StartupScenePath[] = \"\\\\COOKED\\\\SCENES\\\\DEMODISC.HAS;1\"", source, StringComparison.Ordinal);
        Assert.Contains("{ \"cooked/scenes/DemoDiscMainMenu.hasset\", \"\\\\COOKED\\\\SCENES\\\\DEMODISC.HAS;1\" }", source, StringComparison.Ordinal);
        Assert.Contains("{ \"cooked/fonts/DemoDiscBody.hefont\", \"\\\\COOKED\\\\FONTS\\\\DEMODISC.HEF;1\" }", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_WhenStartupSceneIsMissingFromPhysicalMap_Throws() {
        string rootPath = Path.Combine(Path.GetTempPath(), "ps2-runtime-asset-manifest-tests", Guid.NewGuid().ToString("N"));
        string generatedCoreRootPath = Path.Combine(rootPath, "generated-core");
        Directory.CreateDirectory(Path.Combine(generatedCoreRootPath, "runtime"));

        PlatformBuildManifest manifest = new(
            3,
            "project",
            "1.0.0",
            "1.0.0",
            "Scenes/DemoDiscMainMenu.helen",
            [
                new PlatformBuildScene(
                    "Scenes/DemoDiscMainMenu.helen",
                    "DemoDiscMainMenu",
                    "cooked/scenes/DemoDiscMainMenu.hasset",
                    [],
                    [
                        new KeyValuePair<string, string>("cooked-relative-path", "cooked/scenes/DemoDiscMainMenu.hasset")
                    ])
            ],
            Array.Empty<PlatformBuildAsset>(),
            Array.Empty<PlatformBuildArtifact>(),
            Array.Empty<PlatformBuildCodeModule>(),
            Array.Empty<PlatformArtifactPlacement>(),
            new PlatformContainerWritePlan("ps2-disc-layout", Array.Empty<PlatformContainerArtifact>()));

        Ps2RuntimeAssetPathManifestWriter writer = new();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            writer.Write(
                generatedCoreRootPath,
                manifest,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)));

        Assert.Contains("startup scene", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 2: Tighten the builder orchestration test around the generated runtime manifest**

Add these assertions to `BuildAsync_WhenGivenGeneratedCoreAndCookedArtifacts_ProducesElfAndCookedTree` in `Ps2PlatformAssetBuilderTests.cs`:

```csharp
Assert.True(File.Exists(Path.Combine(generatedCoreRoot, "runtime", "runtime_ps2_asset_path_manifest.hpp")));
Assert.True(File.Exists(Path.Combine(generatedCoreRoot, "runtime", "runtime_ps2_asset_path_manifest.cpp")));

string runtimeManifestSource = File.ReadAllText(Path.Combine(generatedCoreRoot, "runtime", "runtime_ps2_asset_path_manifest.cpp"));
Assert.Contains("cooked/scenes/main.hasset", runtimeManifestSource, StringComparison.Ordinal);
Assert.Contains("\\\\COOKED\\\\SCENES\\\\MAIN.HAS;1", runtimeManifestSource, StringComparison.Ordinal);
Assert.Contains("cooked/imported/box_a.hasset", runtimeManifestSource, StringComparison.Ordinal);
Assert.Contains("\\\\COOKED\\\\IMPORTED\\\\BOX_A.HAS;1", runtimeManifestSource, StringComparison.Ordinal);
```

Also add a second scene artifact to prove that all selected scenes still package:

```csharp
string secondSceneOutputPath = Path.Combine(stagingRoot, "cooked", "scenes", "rendering", "directional_shadow_plaza.hasset");
Directory.CreateDirectory(Path.GetDirectoryName(secondSceneOutputPath)!);
File.WriteAllText(secondSceneOutputPath, "scene payload 2");
```

and extend the manifest:

```csharp
new PlatformBuildScene(
    "Scenes/Rendering/DirectionalShadowPlaza.helen",
    "DirectionalShadowPlaza",
    "cooked/scenes/rendering/directional_shadow_plaza.hasset",
    [],
    [])
```

plus:

```csharp
new PlatformBuildArtifact("cooked/scenes/rendering/directional_shadow_plaza.hasset", "scene:plaza", "sha256:scene2", "scene", "shared")
```

Then assert:

```csharp
Assert.True(File.Exists(Path.Combine(outputRoot, "disc", "COOKED", "SCENES", "RENDERIN", "DIRECTI6B484.HAS")));
```

- [ ] **Step 3: Run the tests to verify they fail**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --filter "FullyQualifiedName~Ps2RuntimeAssetPathManifestWriterTests|FullyQualifiedName~Ps2PlatformAssetBuilderTests.BuildAsync_WhenGivenGeneratedCoreAndCookedArtifacts_ProducesElfAndCookedTree"
```

Expected:
- `Ps2RuntimeAssetPathManifestWriterTests` fails because the writer does not exist
- `Ps2PlatformAssetBuilderTests` fails because the generated runtime manifest files are not written yet

- [ ] **Step 4: Commit the failing tests**

```bash
git -C C:\dev\helworks\helengine-ps2 add builder.tests\Ps2RuntimeAssetPathManifestWriterTests.cs builder.tests\Ps2PlatformAssetBuilderTests.cs
git -C C:\dev\helworks\helengine-ps2 commit -m "test: define PS2 runtime physical path contract"
```

## Task 2: Generate The PS2 Runtime Asset-Path Manifest Before Native Compile

**Files:**
- Create: `C:\dev\helworks\helengine-ps2\builder\Ps2RuntimeAssetPathManifestWriter.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder\Ps2DiscLayoutWriter.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder\Ps2BuildWorkspace.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder\Ps2PlatformAssetBuilder.cs`
- Test: `C:\dev\helworks\helengine-ps2\builder.tests\Ps2RuntimeAssetPathManifestWriterTests.cs`

- [ ] **Step 1: Extend the workspace with generated manifest paths**

Add these properties to `Ps2BuildWorkspace.cs`:

```csharp
/// <summary>
/// Gets the generated runtime folder that receives PS2-specific native manifest source.
/// </summary>
public string GeneratedRuntimeRootPath => Path.Combine(GeneratedCoreRootPath, "runtime");

/// <summary>
/// Gets the generated PS2 runtime asset-path manifest header path.
/// </summary>
public string RuntimeAssetPathManifestHeaderPath => Path.Combine(GeneratedRuntimeRootPath, "runtime_ps2_asset_path_manifest.hpp");

/// <summary>
/// Gets the generated PS2 runtime asset-path manifest source path.
/// </summary>
public string RuntimeAssetPathManifestSourcePath => Path.Combine(GeneratedRuntimeRootPath, "runtime_ps2_asset_path_manifest.cpp");
```

- [ ] **Step 2: Make the disc writer return the staged logical-to-physical mapping**

Change `Ps2DiscLayoutWriter.Write` in `Ps2DiscLayoutWriter.cs` from `void` to:

```csharp
public IReadOnlyDictionary<string, string> Write(Ps2BuildWorkspace workspace) {
```

and return a populated dictionary:

```csharp
Dictionary<string, string> logicalToPhysicalPaths = new(StringComparer.OrdinalIgnoreCase);
CopyPackageFiles(workspace.StagingRootPath, workspace.DiscRootPath, logicalToPhysicalPaths);
File.Copy(workspace.NativeExecutablePath, workspace.DiscExecutablePath, true);
File.WriteAllText(workspace.DiscBootConfigPath, BootConfigContents);
return logicalToPhysicalPaths;
```

Replace the helper with:

```csharp
static void CopyPackageFiles(
    string sourceRootPath,
    string discRootPath,
    Dictionary<string, string> logicalToPhysicalPaths) {
    string[] filePaths = Directory.GetFiles(sourceRootPath, "*", SearchOption.AllDirectories);
    for (int fileIndex = 0; fileIndex < filePaths.Length; fileIndex++) {
        string filePath = filePaths[fileIndex];
        string logicalRelativePath = Path.GetRelativePath(sourceRootPath, filePath).Replace('\\', '/');
        string physicalRelativePath = Ps2DiscPathResolver.ResolveDiscRelativePath(logicalRelativePath).Replace('/', '\\');
        string destinationFilePath = Path.Combine(discRootPath, physicalRelativePath);
        string destinationDirectoryPath = Path.GetDirectoryName(destinationFilePath)!;
        Directory.CreateDirectory(destinationDirectoryPath);
        File.Copy(filePath, destinationFilePath, true);
        logicalToPhysicalPaths[logicalRelativePath] = "\\" + physicalRelativePath.Replace('/', '\\') + ";1";
    }
}
```

- [ ] **Step 3: Implement the generated native runtime manifest writer**

Create `Ps2RuntimeAssetPathManifestWriter.cs`:

```csharp
using System.Text;
using helengine.baseplatform.Manifest;

namespace helengine.ps2.builder;

/// <summary>
/// Writes PS2-specific native runtime asset-path metadata into the generated-core runtime folder.
/// </summary>
public sealed class Ps2RuntimeAssetPathManifestWriter {
    public void Write(
        string generatedCoreRootPath,
        PlatformBuildManifest manifest,
        IReadOnlyDictionary<string, string> logicalToPhysicalPaths) {
        if (string.IsNullOrWhiteSpace(generatedCoreRootPath)) {
            throw new ArgumentException("Generated core root path must be provided.", nameof(generatedCoreRootPath));
        }
        if (manifest == null) {
            throw new ArgumentNullException(nameof(manifest));
        }
        if (logicalToPhysicalPaths == null) {
            throw new ArgumentNullException(nameof(logicalToPhysicalPaths));
        }

        string runtimeRootPath = Path.Combine(generatedCoreRootPath, "runtime");
        Directory.CreateDirectory(runtimeRootPath);

        string startupLogicalPath = ResolveStartupSceneLogicalPath(manifest);
        if (!logicalToPhysicalPaths.TryGetValue(startupLogicalPath, out string startupPhysicalPath)) {
            throw new InvalidOperationException($"The startup scene '{startupLogicalPath}' was not staged into a PS2 physical disc path.");
        }

        File.WriteAllText(Path.Combine(runtimeRootPath, "runtime_ps2_asset_path_manifest.hpp"), BuildHeaderContents());
        File.WriteAllText(
            Path.Combine(runtimeRootPath, "runtime_ps2_asset_path_manifest.cpp"),
            BuildSourceContents(startupPhysicalPath, logicalToPhysicalPaths));
    }

    static string ResolveStartupSceneLogicalPath(PlatformBuildManifest manifest) {
        for (int sceneIndex = 0; sceneIndex < manifest.Scenes.Length; sceneIndex++) {
            PlatformBuildScene scene = manifest.Scenes[sceneIndex];
            if (!string.Equals(scene.SceneId, manifest.StartupSceneId, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            for (int metadataIndex = 0; metadataIndex < scene.ResolvedMetadata.Length; metadataIndex++) {
                KeyValuePair<string, string> entry = scene.ResolvedMetadata[metadataIndex];
                if (string.Equals(entry.Key, "cooked-relative-path", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(entry.Value)) {
                    return entry.Value.Replace('\\', '/');
                }
            }
        }

        throw new InvalidOperationException("The startup scene did not resolve a cooked-relative-path metadata entry.");
    }

    static string BuildHeaderContents() {
        return
            "#pragma once\n\n"
            + "const char* he_get_runtime_ps2_startup_scene_path();\n"
            + "const char* he_get_runtime_ps2_asset_physical_path(const char* logicalPath);\n";
    }

    static string BuildSourceContents(string startupPhysicalPath, IReadOnlyDictionary<string, string> logicalToPhysicalPaths) {
        StringBuilder builder = new();
        builder.AppendLine("#include \"runtime/runtime_ps2_asset_path_manifest.hpp\"");
        builder.AppendLine();
        builder.AppendLine("#include <cstddef>");
        builder.AppendLine("#include <cstring>");
        builder.AppendLine();
        builder.AppendLine("struct HERuntimePs2AssetPathEntry {");
        builder.AppendLine("    const char* LogicalPath;");
        builder.AppendLine("    const char* PhysicalPath;");
        builder.AppendLine("};");
        builder.AppendLine();
        builder.AppendLine("static const char kRuntimePs2StartupScenePath[] = \"" + EscapeCpp(startupPhysicalPath) + "\";");
        builder.AppendLine();
        builder.AppendLine("static const HERuntimePs2AssetPathEntry kRuntimePs2AssetPathEntries[] = {");
        foreach (KeyValuePair<string, string> entry in logicalToPhysicalPaths.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)) {
            builder.AppendLine("    { \"" + EscapeCpp(entry.Key.Replace('\\', '/')) + "\", \"" + EscapeCpp(entry.Value.Replace('/', '\\')) + "\" },");
        }
        builder.AppendLine("};");
        builder.AppendLine();
        builder.AppendLine("const char* he_get_runtime_ps2_startup_scene_path() {");
        builder.AppendLine("    return kRuntimePs2StartupScenePath;");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("const char* he_get_runtime_ps2_asset_physical_path(const char* logicalPath) {");
        builder.AppendLine("    if (logicalPath == nullptr || logicalPath[0] == '\\0') {");
        builder.AppendLine("        return nullptr;");
        builder.AppendLine("    }");
        builder.AppendLine("    for (std::size_t index = 0; index < sizeof(kRuntimePs2AssetPathEntries) / sizeof(kRuntimePs2AssetPathEntries[0]); index++) {");
        builder.AppendLine("        const HERuntimePs2AssetPathEntry& entry = kRuntimePs2AssetPathEntries[index];");
        builder.AppendLine("        if (std::strcmp(entry.LogicalPath, logicalPath) == 0) {");
        builder.AppendLine("            return entry.PhysicalPath;");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("    return nullptr;");
        builder.AppendLine("}");
        return builder.ToString();
    }

    static string EscapeCpp(string value) {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
```

- [ ] **Step 4: Reorder the platform builder so manifest generation happens before compile**

In `Ps2PlatformAssetBuilder.cs`, add the dependency:

```csharp
readonly Ps2RuntimeAssetPathManifestWriter RuntimeAssetPathManifestWriter;
```

initialize it in both constructors:

```csharp
RuntimeAssetPathManifestWriter = new Ps2RuntimeAssetPathManifestWriter();
```

then replace the current happy-path block with:

```csharp
if (diagnostics.Count == 0) {
    Ps2BuildWorkspace workspace = CreateWorkspace(request);
    IReadOnlyDictionary<string, string> logicalToPhysicalPaths = DiscLayoutWriter.Write(workspace);
    RuntimeAssetPathManifestWriter.Write(workspace.GeneratedCoreRootPath, request.Manifest, logicalToPhysicalPaths);
    NativeBuildExecutor.Build(workspace, cancellationToken);
    NativeBuildExecutor.PackageIso(workspace, cancellationToken);
    VerifyPackagedOutputs(workspace);
}
```

and remove the old logical-path `disc` staging in `StageCookedArtifacts`:

```csharp
string destinationPath = Path.Combine(request.WorkingRoot, "ps2-staging", NormalizeRelativePath(artifact.RelativePath));
```

That keeps `request.OutputRoot\disc` as the disc layout owned only by `Ps2DiscLayoutWriter`.

- [ ] **Step 5: Run the focused tests to verify they pass**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --filter "FullyQualifiedName~Ps2RuntimeAssetPathManifestWriterTests|FullyQualifiedName~Ps2PlatformAssetBuilderTests.BuildAsync_WhenGivenGeneratedCoreAndCookedArtifacts_ProducesElfAndCookedTree"
```

Expected:
- PASS

- [ ] **Step 6: Commit the builder-side manifest generation**

```bash
git -C C:\dev\helworks\helengine-ps2 add builder\Ps2BuildWorkspace.cs builder\Ps2DiscLayoutWriter.cs builder\Ps2PlatformAssetBuilder.cs builder\Ps2RuntimeAssetPathManifestWriter.cs builder.tests\Ps2RuntimeAssetPathManifestWriterTests.cs builder.tests\Ps2PlatformAssetBuilderTests.cs
git -C C:\dev\helworks\helengine-ps2 commit -m "feat: generate PS2 runtime physical path manifest"
```

## Task 3: Remove Runtime Path Reconstruction From The PS2 Host

**Files:**
- Modify: `C:\dev\helworks\helengine-ps2\src\platform\ps2\Ps2BootHost.cpp`
- Modify: `C:\dev\helworks\helengine-ps2\src\platform\ps2\Ps2BootHost.hpp`
- Test: `C:\dev\helworks\helengine-ps2\src\platform\ps2\Ps2BootHost.cpp`

- [ ] **Step 1: Replace the generated manifest include and delete the alias helpers**

In `Ps2BootHost.cpp`, replace:

```cpp
#include "runtime/runtime_startup_manifest.hpp"
```

with:

```cpp
#include "runtime/runtime_ps2_asset_path_manifest.hpp"
```

Then delete the entire helper block that starts at:

```cpp
std::string SanitizePs2DiscToken(const std::string& token) {
```

and ends at:

```cpp
bool TryReadPs2DiscFileBytes(const std::string& logicalPath, Array<uint8_t>* destinationBytes) {
```

Replace it with two focused helpers:

```cpp
bool TryReadPs2DiscFileBytes(const std::string& physicalPath, Array<uint8_t>* destinationBytes) {
    if (destinationBytes == nullptr) {
        return false;
    }

    sceCdlFILE fileInfo {};
    if (sceCdSearchFile(&fileInfo, physicalPath.c_str()) == 0) {
        BootLog(std::string("packaged asset missing on disc: ") + physicalPath);
        return false;
    }

    if (fileInfo.size <= 0) {
        *destinationBytes = Array<uint8_t>(0);
        return true;
    }

    constexpr std::size_t SectorSize = 2048;
    std::size_t fileSize = static_cast<std::size_t>(fileInfo.size);
    *destinationBytes = Array<uint8_t>(static_cast<int32_t>(fileSize));
    std::size_t sectorCount = (fileSize + SectorSize - 1) / SectorSize;
    std::size_t alignedSize = sectorCount * SectorSize;
    void* sectorBuffer = memalign(64, alignedSize);
    if (sectorBuffer == nullptr) {
        BootLog("packaged asset disc read allocation failed");
        return false;
    }

    sceCdRMode readMode {};
    readMode.trycount = 0;
    readMode.spindlctrl = SCECdSpinNom;
    readMode.datapattern = SCECdSecS2048;

    if (sceCdRead(fileInfo.lsn, static_cast<u32>(sectorCount), sectorBuffer, &readMode) == 0) {
        free(sectorBuffer);
        BootLog(std::string("packaged asset disc read failed: ") + physicalPath);
        return false;
    }

    sceCdSync(0);
    std::memcpy(destinationBytes->Data, sectorBuffer, fileSize);
    free(sectorBuffer);
    return true;
}

const char* ResolvePhysicalAssetPath(const std::string& logicalPath) {
    return he_get_runtime_ps2_asset_physical_path(logicalPath.c_str());
}
```

- [ ] **Step 2: Make startup scene load consume the physical startup path directly**

Replace the current `LoadPackagedStartupScene()` body:

```cpp
const char* startupSceneRelativePath = he_get_runtime_startup_scene_relative_path();
```

with:

```cpp
const char* startupScenePhysicalPath = he_get_runtime_ps2_startup_scene_path();
if (startupScenePhysicalPath == nullptr || startupScenePhysicalPath[0] == '\0') {
    BootLog("no startup scene configured");
    return false;
}

Asset* startupAsset = LoadPackagedAssetFromPhysicalPath(startupScenePhysicalPath);
if (startupAsset == nullptr) {
    BootLog("startup scene asset load returned null");
    return false;
}
```

and update the success log to:

```cpp
BootLog(std::string("startup scene loaded from physical path: ") + startupScenePhysicalPath);
```

- [ ] **Step 3: Change general packaged asset loads to look up the physical path from the generated manifest**

Replace the existing `LoadPackagedAsset` function with:

```cpp
Asset* Ps2BootHost::LoadPackagedAsset(const std::string& logicalPath) {
    BootLog(std::string("packaged asset logical path: ") + logicalPath);
    const char* physicalPath = ResolvePhysicalAssetPath(logicalPath);
    if (physicalPath == nullptr || physicalPath[0] == '\0') {
        BootLog(std::string("packaged asset path mapping missing: ") + logicalPath);
        return nullptr;
    }

    BootLog(std::string("packaged asset physical path: ") + physicalPath);
    return LoadPackagedAssetFromPhysicalPath(physicalPath);
}

Asset* Ps2BootHost::LoadPackagedAssetFromPhysicalPath(const std::string& physicalPath) {
    Array<uint8_t> fileBytes(0);
    if (!TryReadPs2DiscFileBytes(physicalPath, &fileBytes)) {
        BootLog(std::string("packaged asset missing: ") + physicalPath);
        return nullptr;
    }

    MemoryStream stream(&fileBytes, false);
    return AssetSerializer::Deserialize(&stream);
}
```

Add the declaration to `Ps2BootHost.hpp`:

```cpp
Asset* LoadPackagedAssetFromPhysicalPath(const std::string& physicalPath);
```

- [ ] **Step 4: Run the PS2 builder smoke test**

Run:

```powershell
dotnet run --project C:\dev\helworks\helengine-ps2\builder\helengine.ps2.builder.csproj -- --smoke-test
```

Expected:
- `Smoke test passed.`

- [ ] **Step 5: Commit the runtime cleanup**

```bash
git -C C:\dev\helworks\helengine-ps2 add src\platform\ps2\Ps2BootHost.cpp src\platform\ps2\Ps2BootHost.hpp
git -C C:\dev\helworks\helengine-ps2 commit -m "refactor: use generated PS2 physical asset paths at runtime"
```

## Task 4: Update Smoke Coverage For Multiple Scenes And Physical Startup Resolution

**Files:**
- Modify: `C:\dev\helworks\helengine-ps2\builder\Program.cs`
- Test: `C:\dev\helworks\helengine-ps2\builder\Program.cs`

- [ ] **Step 1: Expand the smoke manifest so it proves startup and secondary scenes both package**

In `Program.cs`, replace:

```csharp
string sceneSourcePath = Path.Combine(sourceRoot, "cooked", "scenes", "main.hasset");
```

with:

```csharp
string startupSceneSourcePath = Path.Combine(sourceRoot, "cooked", "scenes", "DemoDiscMainMenu.hasset");
string secondarySceneSourcePath = Path.Combine(sourceRoot, "cooked", "scenes", "rendering", "directional_shadow_plaza.hasset");
```

Create both:

```csharp
Directory.CreateDirectory(Path.GetDirectoryName(startupSceneSourcePath)!);
Directory.CreateDirectory(Path.GetDirectoryName(secondarySceneSourcePath)!);
File.WriteAllText(startupSceneSourcePath, "scene payload");
File.WriteAllText(secondarySceneSourcePath, "scene payload 2");
```

Replace the smoke manifest scene list with:

```csharp
[
    new PlatformBuildScene(
        "Scenes/DemoDiscMainMenu.helen",
        "DemoDiscMainMenu",
        "cooked/scenes/DemoDiscMainMenu.hasset",
        [],
        [
            new KeyValuePair<string, string>("cooked-relative-path", "cooked/scenes/DemoDiscMainMenu.hasset")
        ]),
    new PlatformBuildScene(
        "Scenes/Rendering/DirectionalShadowPlaza.helen",
        "DirectionalShadowPlaza",
        "cooked/scenes/rendering/directional_shadow_plaza.hasset",
        [],
        [
            new KeyValuePair<string, string>("cooked-relative-path", "cooked/scenes/rendering/directional_shadow_plaza.hasset")
        ])
]
```

and the cooked artifacts with:

```csharp
[
    new PlatformBuildArtifact("cooked/scenes/DemoDiscMainMenu.hasset", "scene:menu", "sha256:scene", "scene", "shared"),
    new PlatformBuildArtifact("cooked/scenes/rendering/directional_shadow_plaza.hasset", "scene:plaza", "sha256:scene2", "scene", "shared")
]
```

- [ ] **Step 2: Update smoke assertions to use physical staged filenames**

Replace:

```csharp
if (!File.Exists(Path.Combine(outputRoot, "disc", "cooked", "scenes", "main.hasset"))) {
    throw new InvalidOperationException("Smoke test scene output is missing.");
}
```

with:

```csharp
if (!File.Exists(Path.Combine(outputRoot, "disc", "COOKED", "SCENES", "DEMODISC.HAS"))) {
    throw new InvalidOperationException("Smoke test startup scene output is missing.");
}

if (!File.Exists(Path.Combine(outputRoot, "disc", "COOKED", "SCENES", "RENDERIN", "DIRECTI6B484.HAS"))) {
    throw new InvalidOperationException("Smoke test secondary scene output is missing.");
}
```

Also inspect the generated runtime manifest:

```csharp
string runtimeManifestSourcePath = Path.Combine(generatedCoreRoot, "runtime", "runtime_ps2_asset_path_manifest.cpp");
if (!File.Exists(runtimeManifestSourcePath)) {
    throw new InvalidOperationException("Smoke test PS2 runtime asset-path manifest is missing.");
}

string runtimeManifestSource = File.ReadAllText(runtimeManifestSourcePath);
if (!runtimeManifestSource.Contains("\\\\COOKED\\\\SCENES\\\\DEMODISC.HAS;1", StringComparison.Ordinal)) {
    throw new InvalidOperationException("Smoke test startup scene physical path mapping is missing.");
}
```

- [ ] **Step 3: Run the smoke test**

Run:

```powershell
dotnet run --project C:\dev\helworks\helengine-ps2\builder\helengine.ps2.builder.csproj -- --smoke-test
```

Expected:
- `Smoke test passed.`

- [ ] **Step 4: Commit the smoke coverage update**

```bash
git -C C:\dev\helworks\helengine-ps2 add builder\Program.cs
git -C C:\dev\helworks\helengine-ps2 commit -m "test: cover PS2 physical startup and multi-scene packaging"
```

## Task 5: Run End-To-End Verification With City

**Files:**
- Modify: none
- Test: `C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\bin\Debug\net9.0-windows\helengine.editor.app.dll`

- [ ] **Step 1: Run the focused PS2 builder tests**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --filter "FullyQualifiedName~Ps2RuntimeAssetPathManifestWriterTests|FullyQualifiedName~Ps2PlatformAssetBuilderTests"
```

Expected:
- PASS

- [ ] **Step 2: Build `city` for PS2 through the real editor CLI**

Run:

```powershell
dotnet C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\bin\Debug\net9.0-windows\helengine.editor.app.dll --project "C:\dev\helprojs\city" --build ps2 --output "C:\dev\helprojs\output\ps2"
```

Expected:
- `Build completed for platform 'ps2': C:\dev\helprojs\output\ps2`

- [ ] **Step 3: Verify the generated runtime manifest and disc layout**

Run:

```powershell
Get-ChildItem C:\dev\helprojs\output\ps2\disc -Recurse | Select-Object FullName
Get-Content C:\Users\Helena\AppData\Local\Temp\helengine-platform-build\ps2\*\generated-core\runtime\runtime_ps2_asset_path_manifest.cpp
```

Expected to include:

```text
C:\dev\helprojs\output\ps2\disc\COOKED\SCENES\DEMODISC.HAS
C:\dev\helprojs\output\ps2\disc\COOKED\FONTS\DEMODISC.HEF
```

and the runtime manifest should include:

```text
"cooked/scenes/DemoDiscMainMenu.hasset"
"\\COOKED\\SCENES\\DEMODISC.HAS;1"
```

- [ ] **Step 4: Boot the ISO in PCSX2**

Manual verification:

1. Boot `C:\dev\helprojs\output\ps2\game.iso` in PCSX2.
2. Confirm the BIOS loads `HELENGIN.ELF`.
3. Confirm the startup scene does not halt on `packaged asset missing`.
4. Confirm the demo-disc menu scene appears instead of the old fallback/test scene.

Expected:
- the menu scene loads
- font loads no longer fail on logical-to-physical translation

- [ ] **Step 5: Commit any final follow-up fixes from verification**

```bash
git -C C:\dev\helworks\helengine-ps2 add builder\*.cs builder.tests\*.cs src\platform\ps2\Ps2BootHost.cpp src\platform\ps2\Ps2BootHost.hpp
git -C C:\dev\helworks\helengine-ps2 commit -m "test: verify PS2 physical runtime paths end to end"
```

## Self-Review

- Spec coverage:
  - builder-owned physical disc paths: covered by Tasks 1 and 2
  - runtime uses emitted startup physical path directly: covered by Task 3
  - runtime uses emitted mapping for fonts and all packaged assets: covered by Task 3
  - all scenes selected in the build dialog remain packaged: covered by Tasks 1, 4, and 5
  - no runtime alias reconstruction remains in PS2 host: covered by Task 3
- Placeholder scan:
  - no `TODO`, `TBD`, or vague “handle later” steps remain
  - each code-changing step includes concrete code snippets or exact replacement targets
- Type consistency:
  - `Ps2RuntimeAssetPathManifestWriter`, `runtime_ps2_asset_path_manifest.hpp`, `he_get_runtime_ps2_startup_scene_path`, and `he_get_runtime_ps2_asset_physical_path` are named consistently across tasks
