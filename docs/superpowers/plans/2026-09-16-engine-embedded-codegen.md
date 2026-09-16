# Engine-Embedded Codegen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the C# to C++ codegen an output of the engine build, published beside the editor and resolved by the editor for every platform build, and delete the per-platform `codegenToolPath` from the untracked platform registry.

**Architecture:** csharpcodegen becomes a git submodule at `engine/vendor/csharpcodegen` pinned to `d8ad75d`. The build script publishes `codegen/codegen.csproj` in Release into `<editor publish path>/codegen/`. A new `EngineCodegenToolProvider` in the editor returns that published executable, or for IDE runs builds the submodule into a commit-keyed cache directory. The build graph runner and GUI executor take the provider; the registry types lose the field and warn once if an old file still carries it.

**Tech Stack:** C# / .NET 9 (helengine.editor, helengine.platforms), xUnit, PowerShell 5.1 build wrapper, git submodules.

**Spec:** `docs/superpowers/specs/2026-09-16-engine-embedded-codegen-design.md`

## Global Constraints

- All work happens in the worktree `C:\dev\helworks\helengine\.worktrees\engine-embedded-codegen` on branch `feature/engine-embedded-codegen`. Never `cd` to the main checkout at `C:\dev\helworks\helengine`; other sessions are active there. Never use bare `git stash`.
- Before the first build in the worktree run `git submodule update --init --recursive` (bepuphysics2 is a submodule and the build needs it).
- Run `dotnet` through PowerShell with the full path `& "C:\Program Files\dotnet\dotnet.exe"`. The Bash `dotnet` and `grep` commands on this machine are wrapped by a shim that mangles output. Use the Grep tool, not Bash grep, for searching source.
- Initial submodule pin is exactly `d8ad75d` (csharpcodegen master on 2026-09-16). Do not pin anything else.
- The published codegen is always built in `Release`, regardless of the editor configuration.
- The published tool lives in a `codegen/` subdirectory of the editor publish directory, never flattened into it.
- New wrapper exit code for a missing published codegen tool is `6`.
- A registry entry that still contains `codegenToolPath` must log one warning and be ignored. It must never be a fatal error.
- Commit messages end with `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`.
- Landing commits, in this order, one per task: (1) submodule + script publish, (2) provider, (3) locator repoint, (4) runner and executor wiring, (5) registry field removal + warning, (6) README. This refines the spec's four-commit outline into six by splitting its second commit three ways; the spec's actual requirement, that every commit builds and passes tests on its own, holds for each.

---

## File Structure

**Created**
- `engine/vendor/csharpcodegen` — git submodule, pinned `d8ad75d`.
- `engine/helengine.editor/managers/project/IEngineCodegenToolProvider.cs` — one-method interface the runner depends on, so tests inject a fake.
- `engine/helengine.editor/managers/project/EngineCodegenToolProvider.cs` — resolution order: published copy, then commit-keyed on-demand build, then loud failure.
- `engine/helengine.editor/managers/project/IEngineCodegenToolPublisher.cs` — two-method interface abstracting `git rev-parse` and `dotnet publish`, so the provider is unit-testable without running either.
- `engine/helengine.editor/managers/project/DotNetEngineCodegenToolPublisher.cs` — real publisher that shells out to `git` and `dotnet`.
- `engine/helengine.editor.tests/managers/project/EngineCodegenToolProviderTests.cs`
- `engine/helengine.platforms.tests/PlatformInstallationStoreTests.cs`

**Modified**
- `.gitmodules` — new submodule entry.
- `scripts/build-platform.ps1` — publish the codegen after the editor publish; exit 6 if missing.
- `README.md` — exit code 6; note that the codegen is an engine build output.
- `engine/helengine.editor/managers/project/EditorSourceBuildWorkspaceLocator.cs` — `ResolveCSharpCodegenRootPath` points at the submodule.
- `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs` — takes `IEngineCodegenToolProvider`; two call sites use it.
- `engine/helengine.editor/managers/project/EditorPlatformBuildExecutor.cs` — drops the descriptor guard; constructs the real provider.
- `engine/helengine.editor/managers/project/EditorProjectBootstrapContext.cs` — passes a warning sink into `PlatformDiscoveryOptions`.
- `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs` — inject a fake provider; drop the removed descriptor argument.
- `engine/helengine.editor.tests/managers/project/EditorSourceBuildWorkspaceLocatorTests.cs` — two new tests.
- `engine/helengine.platforms/PlatformInstallationEntry.cs`, `AvailablePlatformDescriptor.cs`, `PlatformInstallationResolver.cs`, `PlatformInstallationStore.cs`, `PlatformDiscoveryOptions.cs`, `DevelopmentPlatformProvider.cs` — field removal and warning sink.

---

### Task 1: Add the submodule and publish the tool from the build script

**Files:**
- Create: `engine/vendor/csharpcodegen` (submodule)
- Modify: `.gitmodules`
- Modify: `scripts/build-platform.ps1:472-484`
- Test: `scripts/tests/build-platform-workspace.tests.ps1:789-837` (fake `dotnet.cmd`), `:1161-1163`, `:1216-1229`

**Interfaces:**
- Produces: the published file `<editor publish path>/codegen/codegen.exe`, which Task 2's provider looks for at `Path.Combine(AppContext.BaseDirectory, "codegen", "codegen.exe")`. `AppContext.BaseDirectory` for the published editor *is* the editor publish path, so the two agree.

- [ ] **Step 1: Add the submodule at the pinned commit**

```powershell
Set-Location C:\dev\helworks\helengine\.worktrees\engine-embedded-codegen
git submodule add C:\dev\helworks\csharpcodegen engine/vendor/csharpcodegen
git -C engine/vendor/csharpcodegen checkout d8ad75d
git add .gitmodules engine/vendor/csharpcodegen
```

Then open `.gitmodules` and change the new entry's `url` from the local path to the repository's real remote, which is `git@github.com:distrohelena/csharpcodegen.git` (verified with `git -C C:\dev\helworks\csharpcodegen remote -v`). The file must read:

```
[submodule "engine/vendor/bepuphysics2"]
	path = engine/vendor/bepuphysics2
	url = git@github.com:helworks/bepuphysics2.git
[submodule "engine/vendor/csharpcodegen"]
	path = engine/vendor/csharpcodegen
	url = git@github.com:distrohelena/csharpcodegen.git
```

Then run `git submodule sync` so `.git/config` picks up the corrected URL. Do not leave a local filesystem path in `.gitmodules`.

- [ ] **Step 2: Verify the pin and that only the C++ tool's projects exist**

```powershell
git -C engine/vendor/csharpcodegen rev-parse --short HEAD
Test-Path engine/vendor/csharpcodegen/codegen/codegen.csproj
Test-Path engine/vendor/csharpcodegen/cs2.cpp/cs2.cpp.csproj
Test-Path engine/vendor/csharpcodegen/cs2.core/cs2.core.csproj
```

Expected: `d8ad75d`, then `True` three times.

- [ ] **Step 3: Confirm the tool builds standalone from the submodule**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" publish engine/vendor/csharpcodegen/codegen/codegen.csproj -c Release -o "$env:TEMP\codegen-submodule-check" --nologo
Test-Path "$env:TEMP\codegen-submodule-check\codegen.exe"
Remove-Item "$env:TEMP\codegen-submodule-check" -Recurse -Force
```

Expected: `Build succeeded.` and `True`. This proves the submodule needs nothing from outside itself.

- [ ] **Step 4: Add the codegen publish to the build script**

In `scripts/build-platform.ps1`, directly after the block that verifies `$EditorAssemblyPath` exists (the `if (-not (Test-Path -LiteralPath $EditorAssemblyPath -PathType Leaf))` block ending with `exit 5`), insert:

```powershell
    $CodegenProjectPath = Join-Path $ResolvedHelEngineRootPath "engine\vendor\csharpcodegen\codegen\codegen.csproj"
    if (-not (Test-Path -LiteralPath $CodegenProjectPath -PathType Leaf)) {
        [Console]::Error.WriteLine(
            "Codegen submodule is not initialised: '$CodegenProjectPath' was not found. " +
            "Run 'git submodule update --init --recursive' in the engine checkout.")
        $BuildTerminalExitCode = 6
        exit 6
    }

    $CodegenPublishPath = Join-Path $EditorPublishPath "codegen"
    $CodegenRestoreArguments = @(
        "restore",
        $CodegenProjectPath
    ) + $DotNetSharedPropertyArguments
    $CodegenPublishArguments = @(
        "publish",
        $CodegenProjectPath,
        "--no-restore",
        "-c",
        "Release",
        "-o",
        $CodegenPublishPath
    ) + $DotNetSharedPropertyArguments

    Write-Host ("Restoring codegen: dotnet " + ($CodegenRestoreArguments -join " "))
    $CodegenRestoreExitCode = Invoke-StreamingNativeProcess -FilePath $DotNetExecutablePath -ArgumentList $CodegenRestoreArguments
    if ($CodegenRestoreExitCode -ne 0) {
        [Console]::Error.WriteLine("Codegen restore failed with exit code $CodegenRestoreExitCode.")
        $BuildTerminalExitCode = $CodegenRestoreExitCode
        exit $CodegenRestoreExitCode
    }

    Write-Host ("Publishing codegen: dotnet " + ($CodegenPublishArguments -join " "))
    $CodegenPublishExitCode = Invoke-StreamingNativeProcess -FilePath $DotNetExecutablePath -ArgumentList $CodegenPublishArguments
    if ($CodegenPublishExitCode -ne 0) {
        [Console]::Error.WriteLine("Codegen publish failed with exit code $CodegenPublishExitCode.")
        $BuildTerminalExitCode = $CodegenPublishExitCode
        exit $CodegenPublishExitCode
    }

    $CodegenToolPath = Join-Path $CodegenPublishPath "codegen.exe"
    if (-not (Test-Path -LiteralPath $CodegenToolPath -PathType Leaf)) {
        [Console]::Error.WriteLine("Published codegen tool was not found at '$CodegenToolPath'.")
        $BuildTerminalExitCode = 6
        exit 6
    }
    Write-Host "Codegen tool: $CodegenToolPath"
```

`$ResolvedHelEngineRootPath`, `$EditorPublishPath`, `$DotNetSharedPropertyArguments`, `$DotNetExecutablePath`, `Invoke-StreamingNativeProcess` and `$BuildTerminalExitCode` all already exist in the script at that point; do not redefine them. Note the publish configuration is the literal string `"Release"`, not `$Configuration`.

- [ ] **Step 5: Teach the script test harness about the codegen publish**

`scripts/tests/build-platform-workspace.tests.ps1` drives the wrapper with a fake `dotnet.cmd` that records every invocation to a capture file and fabricates `helengine.editor.app.dll` on any `-o` publish. Three things must change or the whole suite fails on the new exit-6 check and the new invocation count.

(a) Make the fake produce the codegen tool when the codegen project is published. In the here-string that writes `dotnet.cmd` (starts `Set-Content -LiteralPath (Join-Path $FakeToolsPath "dotnet.cmd")`), change the argument scan and the publish block. Add a fourth reset line and a `publish` capture:

```bat
set "PublishOutputPath="
set "PublishProjectPath="
set "BuildOutputPath="
set "ProjectFilePath="
set "IsEditorBuild="
:FindArguments
if "%~1"=="" goto RunInvocation
if /I "%~1"=="publish" set "PublishProjectPath=%~2"
if /I "%~1"=="-o" set "PublishOutputPath=%~2"
if /I "%~1"=="--output" set "BuildOutputPath=%~2"
if /I "%~1"=="--project" set "ProjectFilePath=%~2"
if /I "%~1"=="--build" set "IsEditorBuild=1"
shift
goto FindArguments
```

and replace the publish block with:

```bat
:RunInvocation
if not "%PublishOutputPath%"=="" (
    if not exist "%PublishOutputPath%" mkdir "%PublishOutputPath%"
    echo %PublishProjectPath%| findstr /I /C:"codegen.csproj" >nul
    if errorlevel 1 (
        type nul > "%PublishOutputPath%\helengine.editor.app.dll"
    ) else (
        type nul > "%PublishOutputPath%\codegen.exe"
    )
)
```

`if errorlevel` is evaluated at run time even inside a parenthesised block, so no delayed expansion is needed.

(b) The workspace-only case takes the last `publish` invocation and later asserts its `-o` equals the editor publish path. The codegen publish is now the last one. At the line `Where-Object { $_ -match '^publish ' } |` inside the `$WorkspaceOnlyPublishInvocation` assignment, change the pattern to `'^publish .*editor\.csproj'`.

(c) The stable-cache case asserts exactly two publish invocations across two wrapper runs. Change its filter to the editor project and add codegen assertions after it. Replace:

```powershell
        $PublishInvocations = @($InitialInvocations | Where-Object { $_ -match '^publish ' })
        if ($PublishInvocations.Count -ne 2) {
            throw "Expected two publish invocations, captured $($PublishInvocations.Count)."
        }
```

with:

```powershell
        $PublishInvocations = @($InitialInvocations | Where-Object { $_ -match '^publish .*editor\.csproj' })
        if ($PublishInvocations.Count -ne 2) {
            throw "Expected two editor publish invocations, captured $($PublishInvocations.Count)."
        }
        $CodegenPublishInvocations = @($InitialInvocations | Where-Object { $_ -match '^publish .*codegen\.csproj' })
        if ($CodegenPublishInvocations.Count -ne 2) {
            throw "Expected two codegen publish invocations, captured $($CodegenPublishInvocations.Count)."
        }
        $ExpectedCodegenPublishPath = Join-Path $ExpectedEditorPublishPath "codegen"
        foreach ($CodegenPublishInvocation in $CodegenPublishInvocations) {
            if ((Get-CapturedArgumentValue -Invocation $CodegenPublishInvocation -ArgumentName "-o") -cne $ExpectedCodegenPublishPath) {
                throw "The codegen was not published beside the editor: '$CodegenPublishInvocation'."
            }
            if ($CodegenPublishInvocation -notmatch '(^|\s)-c\s+Release(\s|$)') {
                throw "The codegen was not published in Release: '$CodegenPublishInvocation'."
            }
        }
```

`Get-CapturedArgumentValue` already exists in that file. The four existing lines that read `$PublishInvocations[0]` and `[1]` for `--artifacts-path` and `-o` stay as they are; they now see only the editor publishes, which is what they were written to check.

The real-editor smoke test needs no change: its `Get-PublishOutputPath` requires exactly one line starting `Publishing: `, and the new line starts `Publishing codegen: `, which does not match.

- [ ] **Step 5b: Run the script test suite**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-workspace.tests.ps1
```

Expected: exits 0 with no thrown assertion. The submodule check inside the wrapper resolves against the real worktree root (`$PSScriptRoot\..`), so with Step 1 done it passes; if it exits 6 with "Codegen submodule is not initialised", Step 1 was not completed in this worktree.

- [ ] **Step 6: Smoke-check the published tree with a real wrapper run**

Run the wrapper for the windows platform against the demodisc project (windows is the fastest target on this machine):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build-platform.ps1 -Project C:\dev\helprojs\demodisc\project.heproj -Platform windows -Output "$env:TEMP\codegen-smoke-windows" -Configuration Release 1> "$env:TEMP\codegen-smoke.out.log" 2> "$env:TEMP\codegen-smoke.err.log"
Select-String -Path "$env:TEMP\codegen-smoke.out.log" -Pattern "^Codegen tool: " | ForEach-Object { $_.Line }
```

Then take the path printed after `Codegen tool: ` and confirm:

```powershell
Test-Path "<that path>"
```

Expected: `True`. The windows build itself may fail later for unrelated reasons; what matters here is that the `Codegen tool:` line printed and the file exists. Record the printed path, Task 7 reuses it.

- [ ] **Step 7: Commit**

```powershell
git add .gitmodules engine/vendor/csharpcodegen scripts/build-platform.ps1
git commit -F - @'
build: embed csharpcodegen and publish it with the engine

Add csharpcodegen as a submodule at engine/vendor/csharpcodegen, pinned
to d8ad75d, and have the platform build wrapper publish
codegen/codegen.csproj in Release into a codegen/ directory beside the
published editor. A missing submodule or a missing published tool is
exit code 6.

The codegen lowers engine and project C# to C++ for every native
target, so its version belongs to the engine commit, not to a per-
platform path in the untracked platform registry.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
'@
```

---

### Task 2: The provider

**Files:**
- Create: `engine/helengine.editor/managers/project/IEngineCodegenToolProvider.cs`
- Create: `engine/helengine.editor/managers/project/IEngineCodegenToolPublisher.cs`
- Create: `engine/helengine.editor/managers/project/EngineCodegenToolProvider.cs`
- Create: `engine/helengine.editor/managers/project/DotNetEngineCodegenToolPublisher.cs`
- Test: `engine/helengine.editor.tests/managers/project/EngineCodegenToolProviderTests.cs`

**Interfaces:**
- Produces: `interface IEngineCodegenToolProvider { string Resolve(); }` — Task 4 makes the runner depend on this.
- Produces: `sealed class EngineCodegenToolProvider : IEngineCodegenToolProvider` with public constructor `EngineCodegenToolProvider()` (real behaviour) and internal constructor `EngineCodegenToolProvider(string editorBaseDirectoryPath, string submoduleRootPath, string onDemandCacheRootPath, IEngineCodegenToolPublisher publisher)` (tests).
- Produces: `interface IEngineCodegenToolPublisher { string ReadCommit(string submoduleRootPath); void Publish(string codegenProjectPath, string outputDirectoryPath); }`.
- Consumes: `EditorSourceBuildWorkspaceLocator.ResolveCSharpCodegenRootPath()` for the submodule root (repointed in Task 3; until then it still returns the sibling directory, which is fine because Task 2's real constructor is not exercised by Task 2's tests).

- [ ] **Step 1: Write the failing tests**

Create `engine/helengine.editor.tests/managers/project/EngineCodegenToolProviderTests.cs`:

```csharp
using Xunit;

namespace helengine.editor.tests.managers.project {
    /// <summary>
    /// Verifies the engine codegen provider prefers the published tool, builds on demand keyed by submodule commit, and fails loudly.
    /// </summary>
    public sealed class EngineCodegenToolProviderTests : IDisposable {
        readonly string RootPath;
        readonly string EditorBaseDirectoryPath;
        readonly string SubmoduleRootPath;
        readonly string CacheRootPath;

        public EngineCodegenToolProviderTests() {
            RootPath = Path.Combine(Path.GetTempPath(), "helengine-codegen-provider-tests", Guid.NewGuid().ToString("N"));
            EditorBaseDirectoryPath = Path.Combine(RootPath, "editor");
            SubmoduleRootPath = Path.Combine(RootPath, "submodule");
            CacheRootPath = Path.Combine(RootPath, "cache");
            Directory.CreateDirectory(EditorBaseDirectoryPath);
            Directory.CreateDirectory(Path.Combine(SubmoduleRootPath, "codegen"));
            File.WriteAllText(Path.Combine(SubmoduleRootPath, "codegen", "codegen.csproj"), "<Project />");
        }

        public void Dispose() {
            if (Directory.Exists(RootPath)) {
                Directory.Delete(RootPath, true);
            }
        }

        /// <summary>
        /// Fake publisher that records calls and materialises the tool on publish.
        /// </summary>
        sealed class FakePublisher : IEngineCodegenToolPublisher {
            public string Commit = "abc123";
            public int PublishCallCount;
            public string LastOutputDirectoryPath = string.Empty;
            public bool ThrowOnReadCommit;

            public string ReadCommit(string submoduleRootPath) {
                if (ThrowOnReadCommit) {
                    throw new InvalidOperationException("git is unavailable");
                }
                return Commit;
            }

            public void Publish(string codegenProjectPath, string outputDirectoryPath) {
                PublishCallCount++;
                LastOutputDirectoryPath = outputDirectoryPath;
                Directory.CreateDirectory(outputDirectoryPath);
                File.WriteAllText(Path.Combine(outputDirectoryPath, "codegen.exe"), string.Empty);
            }
        }

        [Fact]
        public void Resolve_WhenPublishedToolExists_ReturnsItWithoutPublishing() {
            string publishedDirectoryPath = Path.Combine(EditorBaseDirectoryPath, "codegen");
            Directory.CreateDirectory(publishedDirectoryPath);
            string publishedToolPath = Path.Combine(publishedDirectoryPath, "codegen.exe");
            File.WriteAllText(publishedToolPath, string.Empty);
            FakePublisher publisher = new();
            EngineCodegenToolProvider provider = new(EditorBaseDirectoryPath, SubmoduleRootPath, CacheRootPath, publisher);

            string resolvedPath = provider.Resolve();

            Assert.Equal(publishedToolPath, resolvedPath);
            Assert.Equal(0, publisher.PublishCallCount);
        }

        [Fact]
        public void Resolve_WhenNothingIsPublished_BuildsIntoCommitKeyedCacheDirectory() {
            FakePublisher publisher = new() { Commit = "deadbeef" };
            EngineCodegenToolProvider provider = new(EditorBaseDirectoryPath, SubmoduleRootPath, CacheRootPath, publisher);

            string resolvedPath = provider.Resolve();

            string expectedDirectoryPath = Path.Combine(CacheRootPath, "codegen", "deadbeef");
            Assert.Equal(Path.Combine(expectedDirectoryPath, "codegen.exe"), resolvedPath);
            Assert.Equal(expectedDirectoryPath, publisher.LastOutputDirectoryPath);
            Assert.Equal(1, publisher.PublishCallCount);
            Assert.True(File.Exists(resolvedPath));
        }

        [Fact]
        public void Resolve_WhenCachedBuildForCommitExists_DoesNotPublishAgain() {
            FakePublisher publisher = new() { Commit = "deadbeef" };
            string cachedDirectoryPath = Path.Combine(CacheRootPath, "codegen", "deadbeef");
            Directory.CreateDirectory(cachedDirectoryPath);
            File.WriteAllText(Path.Combine(cachedDirectoryPath, "codegen.exe"), string.Empty);
            EngineCodegenToolProvider provider = new(EditorBaseDirectoryPath, SubmoduleRootPath, CacheRootPath, publisher);

            string resolvedPath = provider.Resolve();

            Assert.Equal(Path.Combine(cachedDirectoryPath, "codegen.exe"), resolvedPath);
            Assert.Equal(0, publisher.PublishCallCount);
        }

        [Fact]
        public void Resolve_WhenCommitChanges_BuildsANewDirectory() {
            FakePublisher publisher = new() { Commit = "old000" };
            EngineCodegenToolProvider provider = new(EditorBaseDirectoryPath, SubmoduleRootPath, CacheRootPath, publisher);
            provider.Resolve();
            publisher.Commit = "new111";

            string resolvedPath = provider.Resolve();

            Assert.Equal(Path.Combine(CacheRootPath, "codegen", "new111", "codegen.exe"), resolvedPath);
            Assert.Equal(2, publisher.PublishCallCount);
        }

        [Fact]
        public void Resolve_WhenSubmoduleIsNotInitialised_ThrowsNamingSubmoduleAndCommand() {
            Directory.Delete(SubmoduleRootPath, true);
            FakePublisher publisher = new();
            EngineCodegenToolProvider provider = new(EditorBaseDirectoryPath, SubmoduleRootPath, CacheRootPath, publisher);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => provider.Resolve());

            Assert.Contains(Path.Combine(EditorBaseDirectoryPath, "codegen", "codegen.exe"), exception.Message, StringComparison.Ordinal);
            Assert.Contains(SubmoduleRootPath, exception.Message, StringComparison.Ordinal);
            Assert.Contains("git submodule update --init", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Resolve_WhenCommitCannotBeRead_ThrowsSayingGitIsRequired() {
            FakePublisher publisher = new() { ThrowOnReadCommit = true };
            EngineCodegenToolProvider provider = new(EditorBaseDirectoryPath, SubmoduleRootPath, CacheRootPath, publisher);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => provider.Resolve());

            Assert.Contains("git", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(Path.Combine(EditorBaseDirectoryPath, "codegen", "codegen.exe"), exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Resolve_WhenPublishDoesNotProduceTool_Throws() {
            EngineCodegenToolProvider provider = new(EditorBaseDirectoryPath, SubmoduleRootPath, CacheRootPath, new NoOutputPublisher());

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => provider.Resolve());

            Assert.Contains(Path.Combine(CacheRootPath, "codegen", "abc123", "codegen.exe"), exception.Message, StringComparison.Ordinal);
        }

        sealed class NoOutputPublisher : IEngineCodegenToolPublisher {
            public string ReadCommit(string submoduleRootPath) {
                return "abc123";
            }

            public void Publish(string codegenProjectPath, string outputDirectoryPath) {
                Directory.CreateDirectory(outputDirectoryPath);
            }
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail to compile**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EngineCodegenToolProviderTests" --nologo
```

Expected: build error, `EngineCodegenToolProvider` and the two interfaces do not exist.

- [ ] **Step 3: Create the two interfaces**

`engine/helengine.editor/managers/project/IEngineCodegenToolProvider.cs`:

```csharp
namespace helengine.editor {
    /// <summary>
    /// Resolves the csharpcodegen executable that belongs to the running engine build.
    /// </summary>
    public interface IEngineCodegenToolProvider {
        /// <summary>
        /// Returns the absolute path of the codegen executable, building it on demand when nothing was published.
        /// </summary>
        /// <returns>Absolute path to <c>codegen.exe</c>.</returns>
        string Resolve();
    }
}
```

`engine/helengine.editor/managers/project/IEngineCodegenToolPublisher.cs`:

```csharp
namespace helengine.editor {
    /// <summary>
    /// Abstracts the two external commands the on-demand codegen build needs, so the provider is testable without them.
    /// </summary>
    public interface IEngineCodegenToolPublisher {
        /// <summary>
        /// Reads the checked-out commit of the codegen submodule.
        /// </summary>
        /// <param name="submoduleRootPath">Absolute submodule root path.</param>
        /// <returns>Full commit hash.</returns>
        string ReadCommit(string submoduleRootPath);

        /// <summary>
        /// Publishes the codegen project in Release into the supplied directory.
        /// </summary>
        /// <param name="codegenProjectPath">Absolute path to <c>codegen/codegen.csproj</c>.</param>
        /// <param name="outputDirectoryPath">Absolute publish output directory.</param>
        void Publish(string codegenProjectPath, string outputDirectoryPath);
    }
}
```

- [ ] **Step 4: Create the provider**

`engine/helengine.editor/managers/project/EngineCodegenToolProvider.cs`:

```csharp
namespace helengine.editor {
    /// <summary>
    /// Resolves the codegen executable produced by the engine build: the published copy beside the editor first, then an on-demand build keyed by the submodule commit.
    /// </summary>
    public sealed class EngineCodegenToolProvider : IEngineCodegenToolProvider {
        /// <summary>
        /// Directory beside the editor assembly that the build script publishes the tool into.
        /// </summary>
        public const string PublishedToolDirectoryName = "codegen";

        /// <summary>
        /// Executable file name of the codegen tool.
        /// </summary>
        public const string ToolFileName = "codegen.exe";

        /// <summary>
        /// Relative path of the codegen project inside the submodule.
        /// </summary>
        const string CodegenProjectRelativePath = "codegen/codegen.csproj";

        /// <summary>
        /// Environment setting that lets build hosts pick the on-demand cache root; matches the editor's other isolated build state.
        /// </summary>
        const string WorkspaceRootEnvironmentVariableName = "HELENGINE_BUILD_WORKSPACE_ROOT";

        /// <summary>
        /// Default on-demand cache folder under the temp directory, shared with other isolated build state.
        /// </summary>
        const string IsolationFolderName = "helengine-builds";

        readonly string EditorBaseDirectoryPath;
        readonly string SubmoduleRootPath;
        readonly string OnDemandCacheRootPath;
        readonly IEngineCodegenToolPublisher Publisher;

        /// <summary>
        /// Initializes the provider for the running editor using the real submodule and real external commands.
        /// </summary>
        public EngineCodegenToolProvider()
            : this(
                AppContext.BaseDirectory,
                new EditorSourceBuildWorkspaceLocator().ResolveCSharpCodegenRootPath(),
                ResolveDefaultOnDemandCacheRootPath(),
                new DotNetEngineCodegenToolPublisher()) {
        }

        /// <summary>
        /// Initializes the provider with explicit roots and publisher; used by tests.
        /// </summary>
        /// <param name="editorBaseDirectoryPath">Directory that contains the editor assembly.</param>
        /// <param name="submoduleRootPath">Codegen submodule root.</param>
        /// <param name="onDemandCacheRootPath">Root under which commit-keyed on-demand builds are stored.</param>
        /// <param name="publisher">External command abstraction.</param>
        internal EngineCodegenToolProvider(
            string editorBaseDirectoryPath,
            string submoduleRootPath,
            string onDemandCacheRootPath,
            IEngineCodegenToolPublisher publisher) {
            if (string.IsNullOrWhiteSpace(editorBaseDirectoryPath)) {
                throw new ArgumentException("Editor base directory path must be provided.", nameof(editorBaseDirectoryPath));
            }
            if (string.IsNullOrWhiteSpace(submoduleRootPath)) {
                throw new ArgumentException("Submodule root path must be provided.", nameof(submoduleRootPath));
            }
            if (string.IsNullOrWhiteSpace(onDemandCacheRootPath)) {
                throw new ArgumentException("On-demand cache root path must be provided.", nameof(onDemandCacheRootPath));
            }

            EditorBaseDirectoryPath = Path.GetFullPath(editorBaseDirectoryPath);
            SubmoduleRootPath = Path.GetFullPath(submoduleRootPath);
            OnDemandCacheRootPath = Path.GetFullPath(onDemandCacheRootPath);
            Publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        }

        /// <inheritdoc />
        public string Resolve() {
            string publishedToolPath = Path.Combine(EditorBaseDirectoryPath, PublishedToolDirectoryName, ToolFileName);
            if (File.Exists(publishedToolPath)) {
                return publishedToolPath;
            }

            string codegenProjectPath = Path.Combine(SubmoduleRootPath, CodegenProjectRelativePath);
            if (!File.Exists(codegenProjectPath)) {
                throw new InvalidOperationException(
                    $"The engine codegen tool was not found. No published copy at '{publishedToolPath}', and the codegen submodule at '{SubmoduleRootPath}' is not initialised ('{codegenProjectPath}' is missing). Run 'git submodule update --init --recursive' in the engine checkout, or build through scripts/build-platform.ps1 which publishes the tool.");
            }

            string commit;
            try {
                commit = Publisher.ReadCommit(SubmoduleRootPath);
            } catch (Exception ex) {
                throw new InvalidOperationException(
                    $"The engine codegen tool was not found at '{publishedToolPath}', and the on-demand build could not read the submodule commit because git could not be run. Build through scripts/build-platform.ps1, which publishes the tool.", ex);
            }
            if (string.IsNullOrWhiteSpace(commit)) {
                throw new InvalidOperationException(
                    $"The engine codegen tool was not found at '{publishedToolPath}', and git returned no commit for the submodule at '{SubmoduleRootPath}'.");
            }

            string onDemandDirectoryPath = Path.Combine(OnDemandCacheRootPath, PublishedToolDirectoryName, commit.Trim());
            string onDemandToolPath = Path.Combine(onDemandDirectoryPath, ToolFileName);
            if (File.Exists(onDemandToolPath)) {
                return onDemandToolPath;
            }

            Publisher.Publish(codegenProjectPath, onDemandDirectoryPath);
            if (!File.Exists(onDemandToolPath)) {
                throw new InvalidOperationException(
                    $"Publishing the engine codegen tool from '{codegenProjectPath}' did not produce '{onDemandToolPath}'.");
            }

            return onDemandToolPath;
        }

        /// <summary>
        /// Resolves the default on-demand cache root, honouring the same workspace override the editor's other isolated build state uses.
        /// </summary>
        /// <returns>Absolute cache root.</returns>
        static string ResolveDefaultOnDemandCacheRootPath() {
            string configuredWorkspaceRootPath = Environment.GetEnvironmentVariable(WorkspaceRootEnvironmentVariableName);
            if (!string.IsNullOrWhiteSpace(configuredWorkspaceRootPath)) {
                return Path.GetFullPath(configuredWorkspaceRootPath);
            }

            return Path.Combine(Path.GetTempPath(), IsolationFolderName);
        }
    }
}
```

- [ ] **Step 5: Create the real publisher**

`engine/helengine.editor/managers/project/DotNetEngineCodegenToolPublisher.cs`:

```csharp
using System.Diagnostics;
using System.Text;

namespace helengine.editor {
    /// <summary>
    /// Runs <c>git rev-parse</c> and <c>dotnet publish</c> for the on-demand engine codegen build.
    /// </summary>
    public sealed class DotNetEngineCodegenToolPublisher : IEngineCodegenToolPublisher {
        const string GitExecutableName = "git";
        const string DotNetExecutableName = "dotnet";
        const string PublishConfiguration = "Release";

        /// <inheritdoc />
        public string ReadCommit(string submoduleRootPath) {
            if (string.IsNullOrWhiteSpace(submoduleRootPath)) {
                throw new ArgumentException("Submodule root path must be provided.", nameof(submoduleRootPath));
            }

            ProcessStartInfo startInfo = new ProcessStartInfo {
                FileName = GitExecutableName,
                WorkingDirectory = submoduleRootPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("rev-parse");
            startInfo.ArgumentList.Add("HEAD");

            Run(startInfo, out string stdout, out string stderr, out int exitCode);
            if (exitCode != 0) {
                throw new InvalidOperationException($"git rev-parse HEAD failed in '{submoduleRootPath}' with exit code {exitCode}. {stderr.Trim()}");
            }

            return stdout.Trim();
        }

        /// <inheritdoc />
        public void Publish(string codegenProjectPath, string outputDirectoryPath) {
            if (string.IsNullOrWhiteSpace(codegenProjectPath)) {
                throw new ArgumentException("Codegen project path must be provided.", nameof(codegenProjectPath));
            }
            if (string.IsNullOrWhiteSpace(outputDirectoryPath)) {
                throw new ArgumentException("Output directory path must be provided.", nameof(outputDirectoryPath));
            }

            Directory.CreateDirectory(outputDirectoryPath);
            ProcessStartInfo startInfo = new ProcessStartInfo {
                FileName = DotNetExecutableName,
                WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(codegenProjectPath)) ?? Environment.CurrentDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("publish");
            startInfo.ArgumentList.Add(codegenProjectPath);
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(PublishConfiguration);
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add(outputDirectoryPath);
            startInfo.ArgumentList.Add("--nologo");

            Run(startInfo, out string stdout, out string stderr, out int exitCode);
            if (exitCode != 0) {
                string output = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                throw new InvalidOperationException($"dotnet publish of '{codegenProjectPath}' failed with exit code {exitCode}. {output.Trim()}");
            }
        }

        /// <summary>
        /// Runs one process and drains both streams asynchronously so a chatty child cannot deadlock the pipes.
        /// </summary>
        static void Run(ProcessStartInfo startInfo, out string stdout, out string stderr, out int exitCode) {
            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to launch '{startInfo.FileName}'.");
            StringBuilder stdoutBuilder = new StringBuilder();
            StringBuilder stderrBuilder = new StringBuilder();
            process.OutputDataReceived += (sender, eventArgs) => { if (eventArgs.Data != null) { stdoutBuilder.AppendLine(eventArgs.Data); } };
            process.ErrorDataReceived += (sender, eventArgs) => { if (eventArgs.Data != null) { stderrBuilder.AppendLine(eventArgs.Data); } };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
            stdout = stdoutBuilder.ToString();
            stderr = stderrBuilder.ToString();
            exitCode = process.ExitCode;
        }
    }
}
```

- [ ] **Step 6: Run the provider tests**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EngineCodegenToolProviderTests" --nologo
```

Expected: 7 passed, 0 failed.

- [ ] **Step 7: Commit**

```powershell
git add engine/helengine.editor/managers/project/IEngineCodegenToolProvider.cs engine/helengine.editor/managers/project/IEngineCodegenToolPublisher.cs engine/helengine.editor/managers/project/EngineCodegenToolProvider.cs engine/helengine.editor/managers/project/DotNetEngineCodegenToolPublisher.cs engine/helengine.editor.tests/managers/project/EngineCodegenToolProviderTests.cs
git commit -F - @'
feat(editor): resolve the engine's own codegen tool

EngineCodegenToolProvider returns the codegen.exe the build script
publishes beside the editor. When nothing was published, such as an IDE
run, it builds the submodule in Release into a directory keyed by the
submodule commit so a bumped pin never reuses a stale build. Every
failure names the locations checked and, for an uninitialised
submodule, the command that fixes it.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
'@
```

---

### Task 3: Point the locator at the submodule

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorSourceBuildWorkspaceLocator.cs:208-221`
- Test: `engine/helengine.editor.tests/managers/project/EditorSourceBuildWorkspaceLocatorTests.cs`

**Interfaces:**
- Produces: `EditorSourceBuildWorkspaceLocator.ResolveCSharpCodegenRootPath()` now returns `<engine root>/engine/vendor/csharpcodegen` and throws if `codegen/codegen.csproj` is absent there. Task 2's public constructor already calls it.

- [ ] **Step 1: Write the failing tests**

Append inside the class in `EditorSourceBuildWorkspaceLocatorTests.cs`, before the final closing braces:

```csharp
        /// <summary>
        /// Ensures the codegen root is the vendored submodule inside the engine, not a sibling checkout.
        /// </summary>
        [Fact]
        public void ResolveCSharpCodegenRootPath_WhenSubmoduleIsInitialised_ReturnsVendorSubmoduleRoot() {
            string submoduleRootPath = Path.Combine(TemporaryRepositoryRootPath, "engine", "vendor", "csharpcodegen");
            Directory.CreateDirectory(Path.Combine(submoduleRootPath, "codegen"));
            File.WriteAllText(Path.Combine(submoduleRootPath, "codegen", "codegen.csproj"), "<Project />");
            Environment.SetEnvironmentVariable(HelEngineSourceRootEnvironmentVariableName, TemporaryRepositoryRootPath);
            EditorSourceBuildWorkspaceLocator locator = new();

            string resolvedRootPath = locator.ResolveCSharpCodegenRootPath();

            Assert.Equal(Path.GetFullPath(submoduleRootPath), resolvedRootPath);
        }

        /// <summary>
        /// Ensures an uninitialised submodule fails with the path and the command that fixes it.
        /// </summary>
        [Fact]
        public void ResolveCSharpCodegenRootPath_WhenSubmoduleIsNotInitialised_ThrowsNamingCommand() {
            Environment.SetEnvironmentVariable(HelEngineSourceRootEnvironmentVariableName, TemporaryRepositoryRootPath);
            EditorSourceBuildWorkspaceLocator locator = new();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => locator.ResolveCSharpCodegenRootPath());

            Assert.Contains(Path.Combine("engine", "vendor", "csharpcodegen"), exception.Message, StringComparison.Ordinal);
            Assert.Contains("git submodule update --init", exception.Message, StringComparison.Ordinal);
        }
```

- [ ] **Step 2: Run them to verify they fail**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSourceBuildWorkspaceLocatorTests" --nologo
```

Expected: the two new tests fail. The first because the current code returns `<parent>/csharpcodegen`, the second because the current message says "Expected source-build csharpcodegen repo was not found".

- [ ] **Step 3: Replace the method**

In `EditorSourceBuildWorkspaceLocator.cs`, replace the whole `ResolveCSharpCodegenRootPath` method (its XML doc comment through its closing brace) with:

```csharp
        /// <summary>
        /// Relative path of the vendored csharpcodegen submodule inside the engine source root.
        /// </summary>
        const string CSharpCodegenSubmoduleRelativePath = "engine/vendor/csharpcodegen";

        /// <summary>
        /// Relative path of the codegen project inside the submodule, used to detect an uninitialised submodule.
        /// </summary>
        const string CSharpCodegenProjectRelativePath = "codegen/codegen.csproj";

        /// <summary>
        /// Resolves the vendored `csharpcodegen` submodule that the engine build publishes its codegen tool from.
        /// </summary>
        /// <returns>Absolute submodule root path.</returns>
        public string ResolveCSharpCodegenRootPath() {
            string helEngineRootPath = ResolveHelEngineRootPath();
            string submoduleRootPath = Path.GetFullPath(Path.Combine(helEngineRootPath, CSharpCodegenSubmoduleRelativePath));
            string projectPath = Path.Combine(submoduleRootPath, CSharpCodegenProjectRelativePath);
            if (!File.Exists(projectPath)) {
                throw new InvalidOperationException(
                    $"The csharpcodegen submodule at '{submoduleRootPath}' is not initialised ('{projectPath}' is missing). Run 'git submodule update --init --recursive' in the engine checkout.");
            }

            return submoduleRootPath;
        }
```

Note this uses `ResolveHelEngineRootPath()` (the current checkout, which for a worktree is the worktree itself) rather than `ResolveSharedHelEngineRootPath()`. A worktree has its own submodule checkout and must use it, otherwise a worktree that bumps the pin would build the wrong codegen. The `ResolveWorkspaceParentDirectoryPath` helper below the old method is now unused; delete it.

- [ ] **Step 4: Run the locator tests**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSourceBuildWorkspaceLocatorTests" --nologo
```

Expected: all pass, including the four pre-existing tests.

- [ ] **Step 5: Commit**

```powershell
git add engine/helengine.editor/managers/project/EditorSourceBuildWorkspaceLocator.cs engine/helengine.editor.tests/managers/project/EditorSourceBuildWorkspaceLocatorTests.cs
git commit -F - @'
fix(editor): locate csharpcodegen as the vendored submodule

ResolveCSharpCodegenRootPath had no callers and pointed at a sibling
checkout. It now returns engine/vendor/csharpcodegen inside the current
engine root and fails with the initialising command when the submodule
is absent. It uses the current checkout rather than the shared root so a
worktree builds the codegen it has pinned.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
'@
```

---

### Task 4: Wire the provider through the runner and executor

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs:58-100` (constructors), `:498-506` (RunRegenerateCore), `:644-653` (RunCompileCode)
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildExecutor.cs:62-64`, `:70-83`
- Test: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs`

**Interfaces:**
- Consumes: `IEngineCodegenToolProvider` from Task 2.
- Produces: both `EditorPlatformBuildGraphRunner` constructors gain a trailing parameter `IEngineCodegenToolProvider codegenToolProvider`. `EditorPlatformBuildExecutor`'s public constructor is unchanged; it constructs `new EngineCodegenToolProvider()` internally. Task 5 relies on `PlatformDescriptor.CodegenToolPath` no longer being read anywhere in the editor after this task.

- [ ] **Step 1: Add a fake provider to the runner tests and make every construction pass it**

At the top of the test class in `EditorPlatformBuildGraphRunnerTests.cs`, next to the other nested fakes, add:

```csharp
    /// <summary>
    /// Provider that returns a fixed tool path so runner tests never touch the real submodule.
    /// </summary>
    sealed class FakeCodegenToolProvider : IEngineCodegenToolProvider {
        public string Resolve() {
            return "codegen.exe";
        }
    }
```

Then update every construction of the runner. There are two shapes:

(a) Target-typed `new(` calling the 12-parameter public constructor, ending with `TestGeneratedAssetGraph.CreateShaderLibrary());`. Use the Grep tool with pattern `TestGeneratedAssetGraph\.CreateShaderLibrary\(\)\);` on this file to list them. In each, change the last argument line to:

```csharp
                TestGeneratedAssetGraph.CreateShaderLibrary(),
                new FakeCodegenToolProvider());
```

(b) `Activator.CreateInstance` calling the internal 14-parameter constructor with an argument array that ends `TestGeneratedAssetGraph.CreateShaderLibrary()` followed by `],`. There are two, near lines 951 and 1022. In each array append one element after the shader library:

```csharp
                TestGeneratedAssetGraph.CreateShaderLibrary(),
                new FakeCodegenToolProvider()
            ],
```

- [ ] **Step 2: Run the runner tests to verify they fail to compile**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlatformBuildGraphRunnerTests" --nologo
```

Expected: compile errors, no constructor takes that many arguments.

- [ ] **Step 3: Add the parameter to both runner constructors and store it**

In `EditorPlatformBuildGraphRunner.cs`, add a field beside the other `readonly` fields near the top of the class:

```csharp
        /// <summary>
        /// Resolves the engine's own codegen executable for core regeneration and project module compilation.
        /// </summary>
        readonly IEngineCodegenToolProvider CodegenToolProvider;
```

Public constructor: add `IEngineCodegenToolProvider codegenToolProvider` as the last parameter after `EditorBuiltInShaderAssetLibrary builtInShaderAssetLibrary`, and pass it through as the last argument of the `: this(` chain (after the `builtInShaderAssetLibrary` argument there).

Internal constructor: add the same last parameter after `EditorBuiltInShaderAssetLibrary builtInShaderAssetLibrary`, and in the body, next to the other assignments, add:

```csharp
            CodegenToolProvider = codegenToolProvider ?? throw new ArgumentNullException(nameof(codegenToolProvider));
```

- [ ] **Step 4: Use the provider at both call sites**

In `RunRegenerateCore`, replace the argument line `PlatformDescriptor.CodegenToolPath,` with:

```csharp
                CodegenToolProvider.Resolve(),
```

In `RunCompileCode`, replace the argument line `PlatformDescriptor.CodegenToolPath,` with:

```csharp
                CodegenToolProvider.Resolve(),
```

Search the file with the Grep tool for `CodegenToolPath` afterwards. Expected: zero matches.

- [ ] **Step 5: Update the executor**

In `EditorPlatformBuildExecutor.cs`, delete the three-line guard:

```csharp
            if (string.IsNullOrWhiteSpace(platformDescriptor.CodegenToolPath)) {
                throw new ArgumentException("Platform descriptor must provide a csharpcodegen tool path.", nameof(platformDescriptor));
            }
```

and in the `new EditorPlatformBuildGraphRunner(` call add a final named argument after `builtInShaderAssetLibrary: builtInShaderAssetLibrary`:

```csharp
                builtInShaderAssetLibrary: builtInShaderAssetLibrary,
                codegenToolProvider: new EngineCodegenToolProvider());
```

- [ ] **Step 6: Build the editor and run its full test project**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" build engine/helengine.editor/helengine.editor.csproj --nologo
& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.editor.tests/helengine.editor.tests.csproj --nologo
```

Expected: build succeeds; the test project passes with the same set of green tests as before this task plus nothing newly red. If a pre-existing test constructs `EditorPlatformBuildExecutor` with a descriptor whose `CodegenToolPath` is empty and expected an exception, that expectation is now wrong by design; delete that assertion.

- [ ] **Step 7: Commit**

```powershell
git add engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs engine/helengine.editor/managers/project/EditorPlatformBuildExecutor.cs engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs
git commit -F - @'
feat(editor): platform builds use the engine's codegen

The build graph runner takes an IEngineCodegenToolProvider and passes
its result to core regeneration and project module compilation instead
of reading a tool path off the platform descriptor. The GUI and CLI
executor constructs the real provider; the descriptor guard requiring a
codegen path is gone. Builders never see a codegen path again.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
'@
```

---

### Task 5: Remove the registry field and warn on stale files

**Files:**
- Modify: `engine/helengine.platforms/PlatformInstallationEntry.cs`
- Modify: `engine/helengine.platforms/AvailablePlatformDescriptor.cs`
- Modify: `engine/helengine.platforms/PlatformInstallationResolver.cs:51`, `:122-141`, `:163-173`, `:219-230`
- Modify: `engine/helengine.platforms/PlatformInstallationStore.cs`
- Modify: `engine/helengine.platforms/PlatformDiscoveryOptions.cs`
- Modify: `engine/helengine.platforms/DevelopmentPlatformProvider.cs`
- Modify: `engine/helengine.editor/managers/project/EditorProjectBootstrapContext.cs:263`
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs` (descriptor constructions)
- Test: `engine/helengine.platforms.tests/PlatformInstallationStoreTests.cs`

**Interfaces:**
- Produces: `PlatformInstallationStore(string sharedToolchainRootPath, Action<string> warningSink = null)`; `PlatformInstallationResolver(string sharedToolchainRootPath, Action<string> warningSink = null)`; `PlatformDiscoveryOptions(string engineUserSettingsRootPath = "", Action<string> warningSink = null)` with property `WarningSink`.
- Produces: `PlatformInstallationEntry` constructor parameters are now `(engineVersion, platformId, displayName, builderAssemblyPath, playerSourceRootPath, generatedCoreCppRootPath = "", pluginManifestPath = "")`.
- Produces: `AvailablePlatformDescriptor` constructor parameters are now `(id, displayName, builderAssemblyPath = "", playerSourceRootPath = "", isInstalled = true, generatedCoreCppRootPath = "", generatedCoreProjectPaths = null)`.

- [ ] **Step 1: Write the failing store tests**

Create `engine/helengine.platforms.tests/PlatformInstallationStoreTests.cs`:

```csharp
using helengine.platforms;
using Xunit;

namespace helengine.platforms.tests;

/// <summary>
/// Verifies the platform registry store ignores the retired codegenToolPath field and reports it once.
/// </summary>
public sealed class PlatformInstallationStoreTests : IDisposable {
    readonly string TempDirectoryPath;

    public PlatformInstallationStoreTests() {
        TempDirectoryPath = Path.Combine(Path.GetTempPath(), "helengine-platform-store-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempDirectoryPath);
    }

    public void Dispose() {
        if (Directory.Exists(TempDirectoryPath)) {
            Directory.Delete(TempDirectoryPath, true);
        }
    }

    [Fact]
    public void Load_WhenEntryStillCarriesCodegenToolPath_WarnsOncePerEntryAndLoadsIt() {
        File.WriteAllText(Path.Combine(TempDirectoryPath, "platforms.json"), """
        {
          "platforms": [
            {
              "engineVersion": "1.0.0",
              "platformId": "gamecube",
              "displayName": "Nintendo GameCube",
              "builderAssemblyPath": "",
              "playerSourceRootPath": "../helengine-gc",
              "codegenToolPath": "../../csharpcodegen/codegen/bin/Release/net9.0/codegen.exe"
            },
            {
              "engineVersion": "1.0.0",
              "platformId": "windows",
              "displayName": "Windows",
              "builderAssemblyPath": "",
              "playerSourceRootPath": "../helengine-windows"
            }
          ]
        }
        """);
        List<string> warnings = new();
        PlatformInstallationStore store = new PlatformInstallationStore(TempDirectoryPath, warnings.Add);

        PlatformInstallationManifest manifest = store.Load();

        Assert.Equal(2, manifest.Platforms.Count);
        Assert.Equal("gamecube", manifest.Platforms[0].PlatformId);
        string warning = Assert.Single(warnings);
        Assert.Contains("gamecube", warning, StringComparison.Ordinal);
        Assert.Contains("codegenToolPath", warning, StringComparison.Ordinal);
        Assert.Contains("ignored", warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_WhenNoWarningSinkIsSupplied_StillLoadsEntryWithRetiredField() {
        File.WriteAllText(Path.Combine(TempDirectoryPath, "platforms.json"), """
        {
          "platforms": [
            {
              "engineVersion": "1.0.0",
              "platformId": "gamecube",
              "displayName": "Nintendo GameCube",
              "builderAssemblyPath": "",
              "playerSourceRootPath": "../helengine-gc",
              "codegenToolPath": "anything"
            }
          ]
        }
        """);
        PlatformInstallationStore store = new PlatformInstallationStore(TempDirectoryPath);

        PlatformInstallationManifest manifest = store.Load();

        Assert.Single(manifest.Platforms);
    }

    [Fact]
    public void Load_WhenNoEntryCarriesCodegenToolPath_DoesNotWarn() {
        File.WriteAllText(Path.Combine(TempDirectoryPath, "platforms.json"), """
        {
          "platforms": [
            {
              "engineVersion": "1.0.0",
              "platformId": "windows",
              "displayName": "Windows",
              "builderAssemblyPath": "",
              "playerSourceRootPath": "../helengine-windows"
            }
          ]
        }
        """);
        List<string> warnings = new();
        PlatformInstallationStore store = new PlatformInstallationStore(TempDirectoryPath, warnings.Add);

        store.Load();

        Assert.Empty(warnings);
    }
}
```

- [ ] **Step 2: Run them to verify they fail to compile**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.platforms.tests/helengine.platforms.tests.csproj --filter "FullyQualifiedName~PlatformInstallationStoreTests" --nologo
```

Expected: compile error, no two-argument store constructor.

- [ ] **Step 3: Remove the field from the entry**

In `PlatformInstallationEntry.cs`: delete the `codegenToolPath` XML `<param>` line, delete the constructor parameter `string codegenToolPath = "",`, delete the assignment `CodegenToolPath = codegenToolPath ?? string.Empty;`, and delete the `CodegenToolPath` property with its doc comment. The constructor becomes:

```csharp
        public PlatformInstallationEntry(
            string engineVersion,
            string platformId,
            string displayName,
            string builderAssemblyPath,
            string playerSourceRootPath,
            string generatedCoreCppRootPath = "",
            string pluginManifestPath = "") {
```

- [ ] **Step 4: Remove the field from the descriptor**

In `AvailablePlatformDescriptor.cs`: delete the `codegenToolPath` `<param>` line, the parameter `string codegenToolPath = "",`, the assignment `CodegenToolPath = codegenToolPath ?? string.Empty;`, and the `CodegenToolPath` property with its doc comment. The constructor becomes:

```csharp
        public AvailablePlatformDescriptor(
            string id,
            string displayName,
            string builderAssemblyPath = "",
            string playerSourceRootPath = "",
            bool isInstalled = true,
            string generatedCoreCppRootPath = "",
            IReadOnlyList<string> generatedCoreProjectPaths = null) {
```

- [ ] **Step 5: Update the store to warn and ignore**

Replace the whole of `PlatformInstallationStore.cs` with:

```csharp
using System.Text.Json;

namespace helengine.platforms {

    /// <summary>
    /// Reads one engine-level platform manifest that declares known platform payload roots.
    /// </summary>
    public sealed class PlatformInstallationStore {
        /// <summary>
        /// Registry field retired when the codegen became an engine build output; still tolerated on disk.
        /// </summary>
        const string RetiredCodegenToolPathPropertyName = "codegenToolPath";

        /// <summary>
        /// Stores the root directory that owns the installation manifest.
        /// </summary>
        string SharedToolchainRootPath { get; }

        /// <summary>
        /// Receives one message per tolerated problem in the manifest, or null to stay silent.
        /// </summary>
        Action<string> WarningSink { get; }

        /// <summary>
        /// Initializes one installation manifest store for the supplied root directory.
        /// </summary>
        /// <param name="sharedToolchainRootPath">Directory that owns the installation manifest file.</param>
        /// <param name="warningSink">Optional receiver for non-fatal manifest problems.</param>
        public PlatformInstallationStore(string sharedToolchainRootPath, Action<string> warningSink = null) {
            SharedToolchainRootPath = sharedToolchainRootPath ?? string.Empty;
            WarningSink = warningSink;
        }

        /// <summary>
        /// Gets the absolute installation manifest file path.
        /// </summary>
        public string ManifestFilePath {
            get {
                return Path.Combine(SharedToolchainRootPath, "platforms.json");
            }
        }

        /// <summary>
        /// Determines whether the installation manifest exists.
        /// </summary>
        /// <returns><c>true</c> when the manifest exists; otherwise <c>false</c>.</returns>
        public bool Exists() {
            return File.Exists(ManifestFilePath);
        }

        /// <summary>
        /// Loads the platform manifest from disk.
        /// </summary>
        /// <returns>Platform manifest containing platform entries.</returns>
        public PlatformInstallationManifest Load() {
            using FileStream stream = File.OpenRead(ManifestFilePath);
            using JsonDocument document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("platforms", out JsonElement platformsElement) || platformsElement.ValueKind != JsonValueKind.Array) {
                throw new InvalidOperationException($"Installation manifest at {ManifestFilePath} does not contain a platforms array.");
            }

            List<PlatformInstallationEntry> platforms = new();
            foreach (JsonElement platformElement in platformsElement.EnumerateArray()) {
                string engineVersion = platformElement.GetProperty("engineVersion").GetString() ?? throw new InvalidOperationException($"Installation manifest at {ManifestFilePath} contains a platform without engineVersion.");
                string platformId = platformElement.GetProperty("platformId").GetString() ?? throw new InvalidOperationException($"Installation manifest at {ManifestFilePath} contains a platform without platformId.");
                string displayName = platformElement.GetProperty("displayName").GetString() ?? throw new InvalidOperationException($"Installation manifest at {ManifestFilePath} contains a platform without displayName.");
                string builderAssemblyPath = platformElement.TryGetProperty("builderAssemblyPath", out JsonElement builderAssemblyPathElement) ? builderAssemblyPathElement.GetString() ?? string.Empty : string.Empty;
                string playerSourceRootPath = platformElement.GetProperty("playerSourceRootPath").GetString() ?? throw new InvalidOperationException($"Installation manifest at {ManifestFilePath} contains a platform without playerSourceRootPath.");
                string generatedCoreCppRootPath = platformElement.TryGetProperty("generatedCoreCppRootPath", out JsonElement generatedCoreCppRootPathElement) ? generatedCoreCppRootPathElement.GetString() ?? string.Empty : string.Empty;
                string pluginManifestPath = platformElement.TryGetProperty("pluginManifestPath", out JsonElement pluginManifestPathElement) ? pluginManifestPathElement.GetString() ?? string.Empty : string.Empty;

                if (platformElement.TryGetProperty(RetiredCodegenToolPathPropertyName, out _)) {
                    WarningSink?.Invoke(
                        $"Platform '{platformId}' in {ManifestFilePath} still sets '{RetiredCodegenToolPathPropertyName}'. That field is ignored: the codegen now comes from the engine build. Remove it from the entry.");
                }

                platforms.Add(new PlatformInstallationEntry(engineVersion, platformId, displayName, builderAssemblyPath, playerSourceRootPath, generatedCoreCppRootPath, pluginManifestPath));
            }

            return new PlatformInstallationManifest(platforms);
        }
    }
}
```

- [ ] **Step 6: Update the resolver**

In `PlatformInstallationResolver.cs`:

Constructor and field: replace the class header through the constructor with:

```csharp
    public sealed class PlatformInstallationResolver {
        /// <summary>
        /// Stores the root directory that owns the installation manifest and its linked descriptor files.
        /// </summary>
        string SharedToolchainRootPath { get; }

        /// <summary>
        /// Optional receiver for non-fatal manifest problems, forwarded to the store.
        /// </summary>
        Action<string> WarningSink { get; }

        /// <summary>
        /// Initializes one installation resolver for the supplied shared toolchain root.
        /// </summary>
        /// <param name="sharedToolchainRootPath">Shared toolchain root that owns the installation manifest.</param>
        /// <param name="warningSink">Optional receiver for non-fatal manifest problems.</param>
        public PlatformInstallationResolver(string sharedToolchainRootPath, Action<string> warningSink = null) {
            SharedToolchainRootPath = sharedToolchainRootPath ?? string.Empty;
            WarningSink = warningSink;
        }
```

Both `new PlatformInstallationStore(SharedToolchainRootPath)` calls become `new PlatformInstallationStore(SharedToolchainRootPath, WarningSink)`.

Line 51's placeholder descriptor `new AvailablePlatformDescriptor(string.Empty, string.Empty, string.Empty, string.Empty, false, string.Empty, string.Empty)` becomes:

```csharp
            platform = new AvailablePlatformDescriptor(string.Empty, string.Empty, string.Empty, string.Empty, false, string.Empty);
```

In `BuildPlatformDescriptor`: delete the line `string resolvedCodegenToolPath = ResolvePayloadPath(manifestRootPath, entry.CodegenToolPath);`, change the `IsInstalled(...)` call to `IsInstalled(resolvedBuilderAssemblyPath, resolvedPlayerSourceRootPath, resolvedGeneratedCoreCppRootPath)`, and change the descriptor construction to:

```csharp
            return new AvailablePlatformDescriptor(
                entry.PlatformId,
                entry.DisplayName,
                resolvedBuilderAssemblyPath,
                resolvedPlayerSourceRootPath,
                isInstalled,
                resolvedGeneratedCoreCppRootPath,
                resolvedGeneratedCoreProjectPaths);
```

Replace `IsInstalled` with:

```csharp
        static bool IsInstalled(string builderAssemblyPath, string playerSourceRootPath, string generatedCoreCppRootPath) {
            if (!string.IsNullOrWhiteSpace(builderAssemblyPath) && File.Exists(builderAssemblyPath)) {
                return !string.IsNullOrWhiteSpace(playerSourceRootPath) && Directory.Exists(playerSourceRootPath)
                    && (string.IsNullOrWhiteSpace(generatedCoreCppRootPath) || Directory.Exists(generatedCoreCppRootPath));
            }

            return !string.IsNullOrWhiteSpace(playerSourceRootPath)
                && Directory.Exists(playerSourceRootPath)
                && (string.IsNullOrWhiteSpace(generatedCoreCppRootPath) || Directory.Exists(generatedCoreCppRootPath));
        }
```

Search the file with the Grep tool for `odegenToolPath`. Expected: zero matches.

- [ ] **Step 7: Thread the sink through discovery options and the development provider**

Replace `PlatformDiscoveryOptions.cs` with:

```csharp
namespace helengine.platforms {

    /// <summary>
    /// Stores optional engine-level platform-discovery overrides used by source or debug editor builds.
    /// </summary>
    public sealed class PlatformDiscoveryOptions {
        /// <summary>
        /// Initializes one platform-discovery options instance.
        /// </summary>
        /// <param name="engineUserSettingsRootPath">Optional engine user-settings root that should override launcher-managed discovery.</param>
        /// <param name="warningSink">Optional receiver for non-fatal platform registry problems.</param>
        public PlatformDiscoveryOptions(string engineUserSettingsRootPath = "", Action<string> warningSink = null) {
            EngineUserSettingsRootPath = engineUserSettingsRootPath ?? string.Empty;
            WarningSink = warningSink;
        }

        /// <summary>
        /// Gets the optional engine user-settings root that overrides launcher-managed discovery when configured.
        /// </summary>
        public string EngineUserSettingsRootPath { get; }

        /// <summary>
        /// Gets the optional receiver for non-fatal platform registry problems.
        /// </summary>
        public Action<string> WarningSink { get; }
    }
}
```

Replace `DevelopmentPlatformProvider.cs` with:

```csharp
namespace helengine.platforms {

    /// <summary>
    /// Loads available platforms from one explicitly configured engine user-settings root.
    /// </summary>
    public sealed class DevelopmentPlatformProvider : IAvailablePlatformProvider {
        /// <summary>
        /// Stores the engine user-settings root override used by source or debug builds.
        /// </summary>
        string EngineUserSettingsRootPath { get; }

        /// <summary>
        /// Optional receiver for non-fatal platform registry problems, forwarded to the resolver.
        /// </summary>
        Action<string> WarningSink { get; }

        /// <summary>
        /// Initializes one development platform provider.
        /// </summary>
        /// <param name="options">Platform-discovery options containing the engine user-settings override path.</param>
        public DevelopmentPlatformProvider(PlatformDiscoveryOptions options) {
            EngineUserSettingsRootPath = options.EngineUserSettingsRootPath;
            WarningSink = options.WarningSink;
        }

        /// <summary>
        /// Attempts to load the available platforms for the supplied engine version from the configured engine user-settings root.
        /// </summary>
        /// <param name="engineVersion">Exact engine version whose available platforms should be loaded.</param>
        /// <param name="platforms">Resolved platforms when the engine override is configured.</param>
        /// <returns><c>true</c> when the engine override is configured; otherwise <c>false</c>.</returns>
        public bool TryLoadPlatforms(string engineVersion, out IReadOnlyList<AvailablePlatformDescriptor> platforms) {
            platforms = Array.Empty<AvailablePlatformDescriptor>();

            if (string.IsNullOrWhiteSpace(EngineUserSettingsRootPath)) {
                return false;
            }

            PlatformInstallationResolver installationResolver = new PlatformInstallationResolver(EngineUserSettingsRootPath, WarningSink);
            if (installationResolver.TryLoadPlatforms(engineVersion, out platforms)) {
                return true;
            }

            return false;
        }
    }
}
```

`InstalledPlatformProvider.cs` constructs `new PlatformInstallationResolver(SharedToolchainRootPath)` with one argument; that still compiles because the sink parameter is optional, and the launcher path stays silent by design.

In `EditorProjectBootstrapContext.cs` line 263, change to:

```csharp
            PlatformDiscoveryOptions options = new PlatformDiscoveryOptions(sharedEngineUserSettingsRootPath, Logger.WriteWarning);
```

`Logger.WriteWarning(string)` exists in `helengine.core/Logger.cs`.

- [ ] **Step 8: Fix descriptor constructions in editor tests**

The runner tests construct descriptors with seven positional arguments ending in `"codegen.exe"`. Use the Grep tool with pattern `"codegen\.exe"\)` on `EditorPlatformBuildGraphRunnerTests.cs`. Each match looks like:

```csharp
                    Path.Combine(rootPath, "descriptor-generated-core"),
                    "codegen.exe"),
```

Change each to end at the sixth argument:

```csharp
                    Path.Combine(rootPath, "descriptor-generated-core")),
```

(The sixth argument text varies: some say `"generated-core"`. Keep whatever the sixth argument is; only delete the `"codegen.exe"` seventh.) Then use the Grep tool with pattern `codegen\.exe` across `engine/` excluding the provider tests to find any other test that passed a seventh argument, and fix each the same way.

- [ ] **Step 9: Build both projects and run both test suites**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" build engine/helengine.platforms/helengine.platforms.csproj --nologo
& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.platforms.tests/helengine.platforms.tests.csproj --nologo
& "C:\Program Files\dotnet\dotnet.exe" build engine/helengine.editor/helengine.editor.csproj --nologo
& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.editor.tests/helengine.editor.tests.csproj --nologo
```

Expected: both build; platforms tests all green including the three new ones; editor tests green.

Also build the launcher, which references `helengine.platforms`:

```powershell
& "C:\Program Files\dotnet\dotnet.exe" build helengine.ui/helengine.launcher/helengine.launcher.csproj --nologo
```

Expected: succeeds. If it referenced `CodegenToolPath`, the compiler will say where; remove that use.

- [ ] **Step 10: Commit**

```powershell
git add engine/helengine.platforms engine/helengine.platforms.tests engine/helengine.editor/managers/project/EditorProjectBootstrapContext.cs engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs
git commit -F - @'
refactor(platforms): retire codegenToolPath from the platform registry

The codegen is now an engine build output, so no platform entry pins
one. The field is removed from the installation entry, the descriptor
and the resolver, and no longer counts toward a platform being
installed. A registry file that still carries it loads normally and
reports one warning per entry through a sink threaded from
PlatformDiscoveryOptions, which the editor points at its logger. The
warning is deliberately not fatal: the registry is untracked and lives
only on each machine.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
'@
```

---

### Task 6: README

**Files:**
- Modify: `README.md:69-74`

- [ ] **Step 1: Add the exit code and the note**

In the exit-code list, insert after the line for `5`:

```markdown
- `6`: the csharpcodegen submodule was not initialised, or the published codegen tool was missing after a successful publish command
```

Directly after the exit-code list, add:

```markdown
### Codegen comes from the engine

The C# to C++ codegen is a git submodule at `engine/vendor/csharpcodegen`, pinned by the engine commit. `scripts/build-platform.ps1` publishes it in Release into a `codegen/` directory beside the published editor, and the editor resolves that copy for every platform build. Platform entries in the platform registry no longer carry a `codegenToolPath`; an entry that still has one is ignored with a warning. To change which codegen the engine uses, bump the submodule and commit the pin. After cloning or switching branches run `git submodule update --init --recursive`.
```

- [ ] **Step 2: Commit**

```powershell
git add README.md
git commit -F - @'
docs: document the engine-embedded codegen and exit code 6

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
'@
```

---

### Task 7: End-to-end verification

No new files. This task proves the mechanism as the spec's Verification section requires. It changes nothing unless a step fails, in which case return to the task that owns the failing piece.

- [ ] **Step 1: Initialise the submodule in the worktree and confirm the pin**

```powershell
Set-Location C:\dev\helworks\helengine\.worktrees\engine-embedded-codegen
git submodule update --init --recursive
git -C engine/vendor/csharpcodegen rev-parse --short HEAD
```

Expected: `d8ad75d`.

- [ ] **Step 2: Run a GameCube build through the worktree's script and confirm the tool is published**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build-platform.ps1 -Project C:\dev\helprojs\demodisc\project.heproj -Platform gamecube -Output C:\dev\helprojs\demodisc\output\gamecube -Configuration Release -BuildProfile gamecube-default 1> C:\dev\helprojs\demodisc\build-logs\gamecube-embedded-codegen.stdout.log 2> C:\dev\helprojs\demodisc\build-logs\gamecube-embedded-codegen.stderr.log
Select-String -Path C:\dev\helprojs\demodisc\build-logs\gamecube-embedded-codegen.stdout.log -Pattern "^Codegen tool: " | ForEach-Object { $_.Line }
```

Expected: one `Codegen tool: <path>` line where `<path>` ends in `\p\codegen\codegen.exe`, and the file exists.

- [ ] **Step 3: Confirm the build invoked the engine's copy and nothing under the old path**

```powershell
Select-String -Path C:\dev\helprojs\demodisc\build-logs\gamecube-embedded-codegen.stderr.log -Pattern "codegen\.exe" | ForEach-Object { $_.Line } | Select-Object -First 3
Select-String -Path C:\dev\helprojs\demodisc\build-logs\gamecube-embedded-codegen.stdout.log,C:\dev\helprojs\demodisc\build-logs\gamecube-embedded-codegen.stderr.log -Pattern "csharpcodegen\\codegen\\bin" | Measure-Object | Select-Object -ExpandProperty Count
```

Expected: the first command shows the invocation `Process '<editor publish path>\codegen\codegen.exe --cpp ...` and the second prints `0`.

- [ ] **Step 4: Confirm the failure is CPP1001 and nowhere earlier**

```powershell
Select-String -Path C:\dev\helprojs\demodisc\build-logs\gamecube-embedded-codegen.stderr.log -Pattern "CPP1001|No loaded platform builder|No such file or directory" | ForEach-Object { $_.Line.Trim().Substring(0, [Math]::Min(140, $_.Line.Trim().Length)) } | Select-Object -First 3
```

Expected: exactly the `CPP1001 ... PackagedAssetBinarySerializer.cs ... Generic implementation dispatch requires RTTI` line; neither of the other two patterns.

- [ ] **Step 5: Negative test on the registry**

Back up and corrupt the GameCube `codegenToolPath` on this machine, run once, then restore. Do this in PowerShell so the JSON is edited by a parser, not by hand:

```powershell
$registry = "C:\dev\helworks\helengine\user_settings\platforms.json"
Copy-Item $registry "$registry.verify-backup"
$doc = Get-Content $registry -Raw | ConvertFrom-Json
($doc.platforms | Where-Object { $_.platformId -eq "gamecube" }).codegenToolPath = "Z:\this\does\not\exist\codegen.exe"
$doc | ConvertTo-Json -Depth 5 | Set-Content $registry -Encoding UTF8
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build-platform.ps1 -Project C:\dev\helprojs\demodisc\project.heproj -Platform gamecube -Output C:\dev\helprojs\demodisc\output\gamecube -Configuration Release -BuildProfile gamecube-default 1> C:\dev\helprojs\demodisc\build-logs\gamecube-embedded-codegen-negative.stdout.log 2> C:\dev\helprojs\demodisc\build-logs\gamecube-embedded-codegen-negative.stderr.log
Select-String -Path C:\dev\helprojs\demodisc\build-logs\gamecube-embedded-codegen-negative.stdout.log,C:\dev\helprojs\demodisc\build-logs\gamecube-embedded-codegen-negative.stderr.log -Pattern "still sets 'codegenToolPath'|Z:\\this\\does\\not\\exist" | ForEach-Object { $_.Line.Trim().Substring(0, [Math]::Min(160, $_.Line.Trim().Length)) }
Move-Item "$registry.verify-backup" $registry -Force
Get-Content $registry -Raw | ConvertFrom-Json | Out-Null
```

Expected: the warning line naming `gamecube` and `codegenToolPath` appears; the nonsense path appears in no invocation; the build again reaches CPP1001. The last command must not throw, proving the registry was restored intact. The `ConvertTo-Json` reformatting of the temporary file does not matter because the original is restored by `Move-Item`.

- [ ] **Step 6: Run every affected test project once more**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.platforms.tests/helengine.platforms.tests.csproj --nologo
& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.editor.tests/helengine.editor.tests.csproj --nologo
```

Expected: green.

- [ ] **Step 7: Report**

State plainly: the four commits on `feature/engine-embedded-codegen`, the published tool path observed, that the old path appeared zero times, that the build failed at CPP1001 as the spec predicts, and that the registry on this machine still has its stale `codegenToolPath` lines (now ignored) which can be deleted by hand at leisure. Merging to main is a separate decision for Helena, because main is shared and active.
