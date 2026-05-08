# PS2 Bootable ISO Packaging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Change the PS2 build pipeline so it exports a bootable `game.iso` plus an inspectable `disc/` layout containing `SYSTEM.CNF`, `HELENGINE.ELF`, and cooked runtime content.

**Architecture:** Keep the editor-side build graph unchanged up to the PS2 builder boundary. Move PS2 packaging onto a deterministic disc-layout contract: the builder stages `disc/cooked`, writes `SYSTEM.CNF`, copies the compiled ELF to `disc/HELENGINE.ELF`, and then asks the Docker toolchain to author `game.iso` from that staged root. The Docker image remains the only place that knows the PS2 native and ISO tooling details.

**Tech Stack:** C# / .NET 9, xUnit, Docker, Ubuntu container tooling, PS2 builder in `helengine-ps2`, existing editor CLI build path in `helengine`

---

## File Structure

### Existing files to modify

- `C:\dev\helworks\helengine-ps2\builder\IPs2NativeBuildExecutor.cs`
  Extend the builder-side native contract so the PS2 builder can request ISO authoring after native compilation.
- `C:\dev\helworks\helengine-ps2\builder\Ps2BuildWorkspace.cs`
  Add explicit path properties for `disc/`, `SYSTEM.CNF`, `HELENGINE.ELF`, and `game.iso` so the packaging contract is named once and reused everywhere.
- `C:\dev\helworks\helengine-ps2\builder\Ps2PlatformAssetBuilder.cs`
  Replace loose-root export behavior with staged-disc export behavior and final ISO verification.
- `C:\dev\helworks\helengine-ps2\builder\Ps2NativeBuildExecutor.cs`
  Keep Docker image build + native compile, then add a second Docker invocation that authors the ISO from the staged disc root.
- `C:\dev\helworks\helengine-ps2\builder\Program.cs`
  Update the smoke test to validate `disc/HELENGINE.ELF`, `disc/SYSTEM.CNF`, and `game.iso` instead of the old loose ELF contract.
- `C:\dev\helworks\helengine-ps2\builder.tests\Ps2PlatformAssetBuilderTests.cs`
  Add TDD coverage for the new output layout and the new executor contract.
- `C:\dev\helworks\helengine-ps2\Dockerfile`
  Install the Docker-side ISO tool.
- `C:\dev\helworks\helengine-ps2\README.md`
  Update build and boot instructions from raw ELF boot to ISO/disc boot.

### New files to create

- `C:\dev\helworks\helengine-ps2\builder\Ps2DiscLayoutWriter.cs`
  Small focused filesystem helper that owns `disc/` staging, `SYSTEM.CNF` generation, and boot ELF naming.
- `C:\dev\helworks\helengine-ps2\builder.tests\Ps2DiscLayoutWriterTests.cs`
  Focused tests for `SYSTEM.CNF` contents and output path layout without going through the full build orchestration.

## Task 1: Lock The New Output Contract In Tests

**Files:**
- Create: `C:\dev\helworks\helengine-ps2\builder.tests\Ps2DiscLayoutWriterTests.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder.tests\Ps2PlatformAssetBuilderTests.cs`
- Test: `C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj`

- [ ] **Step 1: Write the failing disc-layout unit test**

Add this new test file:

```csharp
using helengine.ps2.builder;
using Xunit;

namespace helengine.ps2.builder.tests;

public sealed class Ps2DiscLayoutWriterTests {
    [Fact]
    public void WriteLayout_WritesBootConfigAndBootElfIntoDiscRoot() {
        string rootPath = Path.Combine(Path.GetTempPath(), "ps2-disc-layout-tests", Guid.NewGuid().ToString("N"));
        string outputRootPath = Path.Combine(rootPath, "out");
        string stagedCookedRootPath = Path.Combine(rootPath, "staging");
        string nativeElfPath = Path.Combine(rootPath, "native", "helengine_ps2.elf");

        Directory.CreateDirectory(Path.Combine(stagedCookedRootPath, "cooked", "scenes"));
        Directory.CreateDirectory(Path.GetDirectoryName(nativeElfPath)!);
        File.WriteAllText(Path.Combine(stagedCookedRootPath, "cooked", "scenes", "main.hasset"), "scene");
        File.WriteAllText(nativeElfPath, "elf");

        Ps2BuildWorkspace workspace = new(
            repositoryRootPath: rootPath,
            stagingRootPath: stagedCookedRootPath,
            generatedCoreRootPath: Path.Combine(rootPath, "generated"),
            outputRootPath: outputRootPath,
            nativeExecutablePath: nativeElfPath);

        Ps2DiscLayoutWriter writer = new();
        writer.Write(workspace);

        Assert.True(File.Exists(Path.Combine(outputRootPath, "disc", "SYSTEM.CNF")));
        Assert.True(File.Exists(Path.Combine(outputRootPath, "disc", "HELENGINE.ELF")));
        Assert.True(File.Exists(Path.Combine(outputRootPath, "disc", "cooked", "scenes", "main.hasset")));
        Assert.Contains(
            "BOOT2 = cdrom0:\\HELENGINE.ELF;1",
            File.ReadAllText(Path.Combine(outputRootPath, "disc", "SYSTEM.CNF")));
    }
}
```

- [ ] **Step 2: Write the failing builder orchestration test**

Replace the loose-output assertions in `Ps2PlatformAssetBuilderTests.cs` with these expectations:

```csharp
Assert.True(File.Exists(Path.Combine(outputRoot, "disc", "SYSTEM.CNF")));
Assert.True(File.Exists(Path.Combine(outputRoot, "disc", "HELENGINE.ELF")));
Assert.True(File.Exists(Path.Combine(outputRoot, "disc", "cooked", "scenes", "main.hasset")));
Assert.True(File.Exists(Path.Combine(outputRoot, "disc", "cooked", "imported", "box_a.hasset")));
Assert.True(File.Exists(Path.Combine(outputRoot, "game.iso")));
```

Update the fake executor in that file so the test can observe the new packaging call:

```csharp
sealed class FakePs2NativeBuildExecutor : IPs2NativeBuildExecutor {
    public Ps2BuildWorkspace LastWorkspace { get; private set; }
    public bool PackageIsoCalled { get; private set; }

    public void Build(Ps2BuildWorkspace workspace, CancellationToken cancellationToken) {
        LastWorkspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        string executableDirectoryPath = Path.GetDirectoryName(workspace.NativeExecutablePath)!;
        Directory.CreateDirectory(executableDirectoryPath);
        File.WriteAllText(workspace.NativeExecutablePath, "elf");
    }

    public void PackageIso(Ps2BuildWorkspace workspace, CancellationToken cancellationToken) {
        LastWorkspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        PackageIsoCalled = true;
        Directory.CreateDirectory(Path.GetDirectoryName(workspace.IsoOutputPath)!);
        File.WriteAllText(workspace.IsoOutputPath, "iso");
    }
}
```

Then add:

```csharp
Assert.True(nativeBuildExecutor.PackageIsoCalled);
```

- [ ] **Step 3: Run the tests to verify they fail**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --filter "FullyQualifiedName~Ps2DiscLayoutWriterTests|FullyQualifiedName~Ps2PlatformAssetBuilderTests.BuildAsync_WhenGivenGeneratedCoreAndCookedArtifacts_ProducesElfAndCookedTree"
```

Expected:
- `Ps2DiscLayoutWriterTests` fails because `Ps2DiscLayoutWriter` does not exist
- `Ps2PlatformAssetBuilderTests` fails because `game.iso`, `disc/SYSTEM.CNF`, and `PackageIso(...)` do not exist yet

- [ ] **Step 4: Commit the failing tests**

```bash
git -C C:\dev\helworks\helengine-ps2 add builder.tests\Ps2DiscLayoutWriterTests.cs builder.tests\Ps2PlatformAssetBuilderTests.cs
git -C C:\dev\helworks\helengine-ps2 commit -m "test: define PS2 ISO output contract"
```

## Task 2: Implement Disc Layout Staging

**Files:**
- Create: `C:\dev\helworks\helengine-ps2\builder\Ps2DiscLayoutWriter.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder\Ps2BuildWorkspace.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder\Ps2PlatformAssetBuilder.cs`
- Test: `C:\dev\helworks\helengine-ps2\builder.tests\Ps2DiscLayoutWriterTests.cs`

- [ ] **Step 1: Extend the workspace with named disc and ISO paths**

Update `Ps2BuildWorkspace.cs` to expose the packaging paths explicitly:

```csharp
public string DiscRootPath => Path.Combine(OutputRootPath, "disc");

public string DiscBootConfigPath => Path.Combine(DiscRootPath, "SYSTEM.CNF");

public string DiscExecutablePath => Path.Combine(DiscRootPath, "HELENGINE.ELF");

public string IsoOutputPath => Path.Combine(OutputRootPath, "game.iso");
```

- [ ] **Step 2: Implement the disc-layout writer**

Create `Ps2DiscLayoutWriter.cs`:

```csharp
namespace helengine.ps2.builder;

/// <summary>
/// Writes the staged PS2 disc layout consumed by ISO packaging.
/// </summary>
public sealed class Ps2DiscLayoutWriter {
    const string BootConfigContents = "BOOT2 = cdrom0:\\\\HELENGINE.ELF;1\r\nVER = 1.00\r\n";

    public void Write(Ps2BuildWorkspace workspace) {
        if (workspace == null) {
            throw new ArgumentNullException(nameof(workspace));
        }
        if (!File.Exists(workspace.NativeExecutablePath)) {
            throw new FileNotFoundException("Native PS2 executable is required before disc staging.", workspace.NativeExecutablePath);
        }

        string stagedCookedRootPath = Path.Combine(workspace.StagingRootPath, "cooked");
        if (!Directory.Exists(stagedCookedRootPath)) {
            throw new DirectoryNotFoundException($"Staged cooked root '{stagedCookedRootPath}' was not found.");
        }

        if (Directory.Exists(workspace.DiscRootPath)) {
            Directory.Delete(workspace.DiscRootPath, recursive: true);
        }

        Directory.CreateDirectory(workspace.DiscRootPath);
        CopyDirectory(stagedCookedRootPath, Path.Combine(workspace.DiscRootPath, "cooked"));
        File.Copy(workspace.NativeExecutablePath, workspace.DiscExecutablePath, true);
        File.WriteAllText(workspace.DiscBootConfigPath, BootConfigContents);
    }

    static void CopyDirectory(string sourcePath, string destinationPath) {
        Directory.CreateDirectory(destinationPath);
        foreach (string filePath in Directory.GetFiles(sourcePath)) {
            string destinationFilePath = Path.Combine(destinationPath, Path.GetFileName(filePath));
            File.Copy(filePath, destinationFilePath, true);
        }
        foreach (string directoryPath in Directory.GetDirectories(sourcePath)) {
            string destinationDirectoryPath = Path.Combine(destinationPath, Path.GetFileName(directoryPath));
            CopyDirectory(directoryPath, destinationDirectoryPath);
        }
    }
}
```

- [ ] **Step 3: Rewire the platform builder to use the new layout writer**

Update `Ps2PlatformAssetBuilder.cs` so it stages cooked artifacts into `output/disc/cooked/...`, then writes the full disc layout after native compile:

```csharp
readonly Ps2DiscLayoutWriter DiscLayoutWriter;

public Ps2PlatformAssetBuilder() {
    NativeBuildExecutor = new Ps2NativeBuildExecutor();
    MaterialCooker = new Ps2MaterialCooker();
    DiscLayoutWriter = new Ps2DiscLayoutWriter();
    Descriptor = new PlatformBuilderDescriptor(
        "helengine.ps2.builder",
        "1.0.0",
        "ps2",
        new EngineCompatibilityRange("1.0.0", "999.0.0"),
        new ManifestCompatibilityRange(1, 3),
        ["ps2"],
        ["ps2"]);
    Definition = Ps2PlatformDefinitionFactory.Create();
}
```

Then change the happy path:

```csharp
if (diagnostics.Count == 0) {
    Ps2BuildWorkspace workspace = CreateWorkspace(request);
    NativeBuildExecutor.Build(workspace, cancellationToken);
    DiscLayoutWriter.Write(workspace);
    NativeBuildExecutor.PackageIso(workspace, cancellationToken);
    VerifyPackagedOutputs(workspace);
}
```

And redirect staged cooked artifacts into the disc root:

```csharp
string destinationPath = Path.Combine(request.OutputRoot, "disc", NormalizeRelativePath(artifact.RelativePath));
```

- [ ] **Step 4: Add final packaged-output verification**

Add this helper to `Ps2PlatformAssetBuilder.cs`:

```csharp
static void VerifyPackagedOutputs(Ps2BuildWorkspace workspace) {
    if (!File.Exists(workspace.DiscBootConfigPath)) {
        throw new FileNotFoundException("PS2 disc boot config was not produced.", workspace.DiscBootConfigPath);
    }
    if (!File.Exists(workspace.DiscExecutablePath)) {
        throw new FileNotFoundException("PS2 disc boot executable was not produced.", workspace.DiscExecutablePath);
    }
    if (!File.Exists(workspace.IsoOutputPath)) {
        throw new FileNotFoundException("PS2 ISO output was not produced.", workspace.IsoOutputPath);
    }
}
```

- [ ] **Step 5: Run the focused tests to verify they pass**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --filter "FullyQualifiedName~Ps2DiscLayoutWriterTests|FullyQualifiedName~Ps2PlatformAssetBuilderTests.BuildAsync_WhenGivenGeneratedCoreAndCookedArtifacts_ProducesElfAndCookedTree"
```

Expected:
- PASS

- [ ] **Step 6: Commit the disc-layout implementation**

```bash
git -C C:\dev\helworks\helengine-ps2 add builder\Ps2BuildWorkspace.cs builder\Ps2DiscLayoutWriter.cs builder\Ps2PlatformAssetBuilder.cs builder.tests\Ps2DiscLayoutWriterTests.cs builder.tests\Ps2PlatformAssetBuilderTests.cs
git -C C:\dev\helworks\helengine-ps2 commit -m "feat: stage PS2 disc layout outputs"
```

## Task 3: Add Docker ISO Authoring

**Files:**
- Modify: `C:\dev\helworks\helengine-ps2\builder\IPs2NativeBuildExecutor.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder\Ps2NativeBuildExecutor.cs`
- Modify: `C:\dev\helworks\helengine-ps2\Dockerfile`
- Test: `C:\dev\helworks\helengine-ps2\builder.tests\Ps2PlatformAssetBuilderTests.cs`

- [ ] **Step 1: Extend the native executor contract**

Update `IPs2NativeBuildExecutor.cs`:

```csharp
namespace helengine.ps2.builder;

/// <summary>
/// Builds and packages the native PS2 output through the Docker-based toolchain.
/// </summary>
public interface IPs2NativeBuildExecutor {
    void Build(Ps2BuildWorkspace workspace, CancellationToken cancellationToken);
    void PackageIso(Ps2BuildWorkspace workspace, CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Install the ISO authoring dependency in Docker**

Update `Dockerfile`:

```dockerfile
RUN apt-get update \
    && apt-get install -y \
        bash \
        ca-certificates \
        curl \
        g++ \
        make \
        pkg-config \
        xorriso \
    && rm -rf /var/lib/apt/lists/*
```

- [ ] **Step 3: Add the Docker ISO packaging step**

Update `Ps2NativeBuildExecutor.cs`:

```csharp
public void PackageIso(Ps2BuildWorkspace workspace, CancellationToken cancellationToken) {
    if (workspace == null) {
        throw new ArgumentNullException(nameof(workspace));
    }

    Directory.CreateDirectory(workspace.OutputRootPath);

    RunProcess(
        "docker",
        [
            "run",
            "--rm",
            "-v",
            $"{workspace.OutputRootPath}:/export",
            "-w",
            "/export",
            DockerImageTag,
            "xorriso",
            "-as",
            "mkisofs",
            "-V",
            "HELENGINE_PS2",
            "-o",
            "/export/game.iso",
            "/export/disc"
        ],
        workspace.RepositoryRootPath,
        cancellationToken);
}
```

While touching this file, also improve process diagnostics so Docker stderr/stdout is preserved:

```csharp
RedirectStandardOutput = true,
RedirectStandardError = true
```

and include the captured output in the thrown exception:

```csharp
throw new InvalidOperationException(
    $"Process '{fileName}' exited with code {process.ExitCode}.{Environment.NewLine}{standardOutput}{Environment.NewLine}{standardError}");
```

- [ ] **Step 4: Run the builder test suite**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --filter FullyQualifiedName~Ps2PlatformAssetBuilderTests
```

Expected:
- PASS

- [ ] **Step 5: Commit the Docker ISO packaging**

```bash
git -C C:\dev\helworks\helengine-ps2 add builder\IPs2NativeBuildExecutor.cs builder\Ps2NativeBuildExecutor.cs Dockerfile builder.tests\Ps2PlatformAssetBuilderTests.cs
git -C C:\dev\helworks\helengine-ps2 commit -m "feat: package PS2 outputs as bootable ISO"
```

## Task 4: Update Smoke Tests And Docs

**Files:**
- Modify: `C:\dev\helworks\helengine-ps2\builder\Program.cs`
- Modify: `C:\dev\helworks\helengine-ps2\README.md`
- Test: `C:\dev\helworks\helengine-ps2\builder\Program.cs`

- [ ] **Step 1: Update the smoke test expectations**

In `Program.cs`, replace the old smoke-test checks:

```csharp
if (!File.Exists(Path.Combine(outputRoot, "helengine_ps2.elf"))) {
    throw new InvalidOperationException("Smoke test PS2 ELF is missing.");
}
```

with:

```csharp
if (!File.Exists(Path.Combine(outputRoot, "disc", "SYSTEM.CNF"))) {
    throw new InvalidOperationException("Smoke test PS2 boot config is missing.");
}

if (!File.Exists(Path.Combine(outputRoot, "disc", "HELENGINE.ELF"))) {
    throw new InvalidOperationException("Smoke test PS2 disc executable is missing.");
}

if (!File.Exists(Path.Combine(outputRoot, "game.iso"))) {
    throw new InvalidOperationException("Smoke test PS2 ISO is missing.");
}
```

Update the smoke-test fake executor to implement the new contract:

```csharp
sealed class SmokeTestNativeBuildExecutor : IPs2NativeBuildExecutor {
    public void Build(Ps2BuildWorkspace workspace, CancellationToken cancellationToken) {
        string executableDirectoryPath = Path.GetDirectoryName(workspace.NativeExecutablePath)!;
        Directory.CreateDirectory(executableDirectoryPath);
        File.WriteAllText(workspace.NativeExecutablePath, "elf");
    }

    public void PackageIso(Ps2BuildWorkspace workspace, CancellationToken cancellationToken) {
        Directory.CreateDirectory(Path.GetDirectoryName(workspace.IsoOutputPath)!);
        File.WriteAllText(workspace.IsoOutputPath, "iso");
    }
}
```

- [ ] **Step 2: Update the PS2 README**

Change the README sections from:

```markdown
The build emits `build/helengine_ps2.elf`.

Load `build/helengine_ps2.elf` in PCSX2. The expected result for this milestone is a solid black frame.
```

to:

```markdown
The build emits:

- `build/helengine_ps2.elf` inside the repository as the intermediate native executable
- `game.iso` in the requested export root
- `disc/` in the requested export root for inspection

Boot `game.iso` in PCSX2. The expected result for this milestone is that the image is recognized as bootable and enters the Helengine PS2 runtime instead of returning to the BIOS browser.
```

- [ ] **Step 3: Run the smoke test**

Run:

```powershell
dotnet run --project C:\dev\helworks\helengine-ps2\builder\helengine.ps2.builder.csproj -- --smoke-test
```

Expected:
- `Smoke test passed.`

- [ ] **Step 4: Commit the smoke-test and docs update**

```bash
git -C C:\dev\helworks\helengine-ps2 add builder\Program.cs README.md
git -C C:\dev\helworks\helengine-ps2 commit -m "docs: update PS2 boot contract to ISO output"
```

## Task 5: Run End-To-End Verification With City

**Files:**
- Modify: none
- Test: `C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\bin\Debug\net9.0-windows\helengine.editor.app.dll`

- [ ] **Step 1: Rebuild the City PS2 output through the real editor CLI**

Run:

```powershell
dotnet C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\bin\Debug\net9.0-windows\helengine.editor.app.dll --project "C:\dev\helprojs\city" --build ps2 --output "C:\dev\helprojs\output\ps2"
```

Expected:
- `Build completed for platform 'ps2': C:\dev\helprojs\output\ps2`

- [ ] **Step 2: Verify the output contract on disk**

Run:

```powershell
Get-ChildItem C:\dev\helprojs\output\ps2 -Recurse | Select-Object FullName,Length
```

Expected to include:

```text
C:\dev\helprojs\output\ps2\game.iso
C:\dev\helprojs\output\ps2\disc\SYSTEM.CNF
C:\dev\helprojs\output\ps2\disc\HELENGINE.ELF
C:\dev\helprojs\output\ps2\disc\cooked\scenes\main.hasset
```

- [ ] **Step 3: Verify the native Docker toolchain still succeeds**

Run:

```powershell
docker run --rm -v C:\dev\helworks\helengine-ps2:/workspace -v C:\Users\Helena\AppData\Local\Temp\helengine-platform-build\ps2:/generated-core-root -w /workspace helengine-ps2 bash -lc "which xorriso && make clean && make"
```

Expected:
- `xorriso` path is printed
- native compile completes with exit code `0`

- [ ] **Step 4: Boot the ISO in PCSX2**

Manual verification:

1. Open PCSX2.
2. Boot `C:\dev\helprojs\output\ps2\game.iso`.
3. Confirm the emulator does not return to the BIOS browser.
4. Confirm the Helengine PS2 runtime starts and reaches its current boot loop.

Expected:
- PCSX2 recognizes the image as bootable.
- Control passes into the runtime.

- [ ] **Step 5: Commit any final follow-up fixes from verification**

```bash
git -C C:\dev\helworks\helengine-ps2 add builder\*.cs builder.tests\*.cs Dockerfile README.md
git -C C:\dev\helworks\helengine-ps2 commit -m "test: verify PS2 ISO boot packaging end to end"
```

## Self-Review

- Spec coverage:
  - output both `disc/` and `game.iso`: covered by Tasks 1, 2, and 5
  - boot filename `HELENGINE.ELF`: covered by Tasks 1 and 2
  - `SYSTEM.CNF` generation: covered by Tasks 1 and 2
  - Docker-side ISO authoring: covered by Task 3
  - smoke/doc contract update: covered by Task 4
  - real `city` build verification: covered by Task 5
- Placeholder scan:
  - no `TODO`, `TBD`, or vague “add tests” steps remain
  - every code-changing step includes concrete code snippets
- Type consistency:
  - `IPs2NativeBuildExecutor.PackageIso(...)`, `Ps2BuildWorkspace.IsoOutputPath`, `DiscBootConfigPath`, and `DiscExecutablePath` are named consistently across all tasks
