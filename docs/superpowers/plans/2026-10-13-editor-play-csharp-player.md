# Editor Play (separate C# player, PS2 first) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Play button in the editor that cooks the open scene for the selected platform (PS2 first), then runs it in a separate, debuggable desktop C# process (`helengine.player.exe`) that decodes the platform-cooked content on PC.

**Architecture:** The player runs the normal desktop `helengine.core` (raw resolution paths). A new Core hook lets the host register content processors before the shared ones; the platform builder plugin ships an `IPlatformPlayerAdapter` whose processors turn platform-cooked texture/model files into generic `TextureAsset`/`ModelAsset`, plus a material decoder and an alias map. The editor runs a new assets-only build path into a stable workspace, compiles runtime script modules with the platform's gameplay symbols, and launches/kills the player process.

**Tech Stack:** C# / .NET 9 (net9.0, net9.0-windows), WinForms host window, DirectX 11 (`helengine.directx11`), xUnit 2.9.

**Spec:** `docs/superpowers/specs/2026-10-13-editor-play-csharp-player-design.md` (revision 2).

## Global Constraints

- Repos: engine `C:\dev\helworks\helengine` (branch `main`), PS2 `C:\dev\helworks\helengine-ps2` (branch `main`). Work on main, no feature worktrees.
- The helengine working tree contains **unrelated uncommitted changes from another session**. Stage only the files your task touches (`git add -- <paths>`); never `git add -A`, never stash/reset/clean.
- Repo rules (both `AGENTS.md`): one class per file; substantive XML comments on every type and member; PascalCase fields; no `private` keyword; same-line braces; no tuples; nullable disabled (no `?` annotations on reference types); no local functions; throw instead of inventing defaults; no best-effort catches; `if / else if` chains for mutually exclusive null checks.
- Never write build outputs or test scratch to `%TEMP%`. Test scratch goes under `C:\dev\helworks\builds\helengine\play-tests\<guid>` (engine) or `C:\dev\helworks\builds\helengine-ps2\play-tests\<guid>` (PS2), deleted in `Dispose`.
- No tests that assert substrings of hand-written source files. Test host-runnable behavior.
- Engine tests: `C:\dev\helworks\helengine\dotnetw.cmd test engine\<project>\<project>.csproj --no-restore --filter "FullyQualifiedName~<Class>" -v:minimal` run from `C:\dev\helworks\helengine` via the **PowerShell** tool (the Bash `rtk` shim breaks dotnet). If `--no-restore` fails on a new project, run once without it.
- PS2 tests: `dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --filter "FullyQualifiedName~<Class>"` via PowerShell.
- Pre-existing red tests exist (editor ~28 on main). Compare by test name before/after; only your new/changed tests must be green.
- The Edit tool may normalize CRLF files to LF; before each commit run `git diff --numstat -- <files>` and make sure only intended lines changed.
- Ask Helena before launching the editor, the player, or any GUI.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **Platform-cooked file that is actually a generic engine asset** (the PS2 builder copies non-model artifacts verbatim). Expect: the adapter processors read the HELE header, decode only PS2 `FormatId == 2`, and hand everything else to the generic `AssetSerializer` path. Pinned in Task 9.
2. **Material texture path written as an 8.3 disc path** (`COOKED\I\x\I1xxxxxx.HAS`) that does not exist in staging. Expect: the adapter maps it back to the logical staged path via the reverse of the staging-derived disc map; an unknown path throws naming the material. Pinned in Task 9.
3. **Second Play with nothing changed.** Expect: no texture/model re-cook and no file rewrites in staging. Pinned in Task 8.
4. **Play pressed while a player is still running, or the player crashing.** Expect: old process killed before relaunch; a non-zero exit is reported with stderr and the session returns to Stopped. Pinned in Task 6.
5. **Scene never saved (no `CurrentScenePath`).** Expect: Play fails with a clear message and nothing is cooked or launched. Pinned in Task 6.

---

## File Structure

**helengine (engine)**
- `engine/helengine.core/content/IAssetContentManagerConfigurator.cs` — new hook interface.
- `engine/helengine.core/CoreInitializationOptions.cs` — new `AssetContentManagerConfigurator` property.
- `engine/helengine.core/Core.cs` — invoke hook before shared registration.
- `engine/helengine.core.tests/content/AssetContentManagerConfiguratorTests.cs`
- `engine/helengine.baseplatform/Player/IPlatformPlayerAdapter.cs`, `PlayerMaterialDescription.cs`, `PlayerMaterialRenderClass.cs`
- `engine/helengine.baseplatform/Definitions/PlatformPlayerCapability.cs`; `PlatformDefinition.cs` (new ctor parameter + property)
- `engine/helengine.baseplatform/Requests/PlatformBuildExecutionScope.cs`; `PlatformBuildRequest.cs` (new trailing parameter + property)
- `engine/helengine.baseplatform.tests/Player/PlatformPlayerCapabilityTests.cs`, `Requests/PlatformBuildRequestExecutionScopeTests.cs`
- `engine/helengine.editor/managers/project/EditorPlayCookResult.cs`; `EditorPlatformBuildGraphRunner.cs` (new `ExecuteAssetsOnly`)
- `engine/helengine.editor/managers/project/EditorDotNetScriptBuildTool.cs` (symbols overload); `EditorPlayScriptBuildService.cs`
- `engine/helengine.editor/managers/play/` — `EditorPlaySessionService.cs`, `EditorPlaySessionState.cs`, `EditorPlayLaunchRequest.cs`, `IEditorPlayCookRunner.cs`, `IEditorPlayScriptBuilder.cs`, `IEditorPlayProcessLauncher.cs`, `IEditorPlayProcess.cs`, `EditorPlayProcessLauncher.cs`, `EditorPlayProcess.cs`, `EditorPlayCookRunner.cs`, `EditorPlayScriptBuilder.cs`
- `engine/helengine.editor/components/ui/EditorViewport.cs`, `EditorViewportToolbarIconSet.cs`, `EditorToolbarIconLoader` — Play/Stop buttons
- `engine/helengine.editor/EditorSession.cs` — wiring, save-for-play, dispose kill
- `helengine.ui/helengine.editor.app/content/icons/toolbar/play.png`, `stop.png`
- `engine/helengine.player/` — `helengine.player.csproj`, `Program.cs`, `PlayerLaunchOptions.cs`, `PlayerScriptModuleLoader.cs`, `PlayerPlatformAdapterLoader.cs`, `PlayerAdapterContentManagerConfigurator.cs`, `PlayerRenderManager3D.cs`, `PlayerStandardMaterialFactory.cs`, `PlayerWindow.cs`, `PlayerHost.cs`
- `engine/helengine.player.tests/` — `helengine.player.tests.csproj`, `PlayerLaunchOptionsTests.cs`
- `helengine.ui/helengine.sln` — add both player projects.

**helengine-ps2**
- `builder/Ps2PlatformAssetBuilder.cs` — assets-only scope, fingerprint skips.
- `builder/Ps2CookFingerprintStore.cs` — new.
- `builder/Ps2PlatformCookWorkItemExecutor.cs` — fingerprint skip for textures.
- `builder/Ps2DiscLayoutWriter.cs` — expose staging map builder.
- `builder/player/Ps2PlayerAdapter.cs`, `Ps2PlayerTextureContentProcessor.cs`, `Ps2PlayerModelContentProcessor.cs`, `Ps2PlayerHeaderProbe.cs`
- `builder/Ps2PlatformDefinitionFactory.cs` — declare `PlatformPlayerCapability`.
- `builder.tests/Ps2AssetsOnlyBuildTests.cs`, `Ps2PlayerAdapterTests.cs`

---

### Task 1: Core content-manager configurator hook (helengine)

**Files:**
- Create: `engine/helengine.core/content/IAssetContentManagerConfigurator.cs`
- Modify: `engine/helengine.core/CoreInitializationOptions.cs` (add property)
- Modify: `engine/helengine.core/Core.cs:633-634`
- Test: `engine/helengine.core.tests/content/AssetContentManagerConfiguratorTests.cs`

**Interfaces:**
- Produces: `public interface IAssetContentManagerConfigurator { void Configure(ContentManager contentManager, RenderManager2D renderManager2D); }`; `CoreInitializationOptions.AssetContentManagerConfigurator { get; set; }` (null = no hook).

- [ ] **Step 1: Write the failing test**

```csharp
namespace helengine.core.tests.content {
    /// <summary>
    /// Verifies that a host-supplied content manager configurator runs before the shared runtime processors are registered.
    /// </summary>
    public sealed class AssetContentManagerConfiguratorTests {
        /// <summary>
        /// A processor registered by the configurator for a shared processor id must be the one the content manager uses.
        /// </summary>
        [Fact]
        public void Initialize_WhenConfiguratorRegistersTextureProcessor_ConfiguratorProcessorWins() {
            RecordingTextureProcessor processor = new RecordingTextureProcessor();
            RecordingConfigurator configurator = new RecordingConfigurator(processor);
            MemoryContentSource source = new MemoryContentSource(new Dictionary<string, byte[]> { ["cooked/imported/a"] = new byte[] { 1 } });
            Core core = new Core(new CoreInitializationOptions { ContentStreamSource = source, AssetContentManagerConfigurator = configurator });
            core.Initialize(null, null, null, new PlatformInfo("test", "1"), core.InitializationOptions);

            TextureAsset texture = core.GetContentManager().Load<TextureAsset>("cooked/imported/a", RuntimeContentProcessorIds.TextureAsset);

            Assert.Equal(1, configurator.CallCount);
            Assert.Same(processor.LastResult, texture);
        }
    }
}
```

Add three one-class files next to the test: `RecordingConfigurator` (implements `IAssetContentManagerConfigurator`, counts calls, `contentManager.RegisterProcessor<TextureAsset>(RuntimeContentProcessorIds.TextureAsset, Processor)`), `RecordingTextureProcessor` (`IContentProcessor<TextureAsset>`; `OutputType => typeof(TextureAsset)`; `Read` returns a new `TextureAsset { Width = 1, Height = 1, Colors = new byte[4] }` stored in `LastResult`; `ReadObject` calls `Read`), `MemoryContentSource` (`IContentStreamSource`; `OpenRead` returns `new MemoryStream(bytes, false)` or throws `FileNotFoundException`). If `Core.InitializationOptions` is not publicly readable, keep a local reference to the options object and pass it instead.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnetw.cmd test engine\helengine.core.tests\helengine.core.tests.csproj --filter "FullyQualifiedName~AssetContentManagerConfiguratorTests" -v:minimal`
Expected: FAIL — compile error, `AssetContentManagerConfigurator` / `IAssetContentManagerConfigurator` not defined.

- [ ] **Step 3: Implement**

`IAssetContentManagerConfigurator.cs`:
```csharp
namespace helengine {
    /// <summary>
    /// Lets a host register its own content processors on the core's shared asset content manager before the
    /// engine registers the shared runtime processors, so host processors win for the same processor id.
    /// </summary>
    public interface IAssetContentManagerConfigurator {
        /// <summary>
        /// Registers host-owned processors on the shared asset content manager.
        /// </summary>
        /// <param name="contentManager">Shared asset content manager owned by the initializing core.</param>
        /// <param name="renderManager2D">2D renderer of the initializing core; null for headless cores.</param>
        void Configure(ContentManager contentManager, RenderManager2D renderManager2D);
    }
}
```

`CoreInitializationOptions.cs` — add with the other properties:
```csharp
        /// <summary>
        /// Optional host hook that registers content processors before the shared runtime processors; null keeps the default registration only.
        /// </summary>
        public IAssetContentManagerConfigurator AssetContentManagerConfigurator { get; set; }
```

`Core.cs` replace lines 633-634 with:
```csharp
            ContentManager contentManager = GetContentManager();
            options.AssetContentManagerConfigurator?.Configure(contentManager, RenderManager2D);
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(contentManager, RenderManager2D);
```

- [ ] **Step 4: Run test to verify it passes** — same command, expected PASS.

- [ ] **Step 5: Commit**

```powershell
git -C C:\dev\helworks\helengine add -- engine/helengine.core/content/IAssetContentManagerConfigurator.cs engine/helengine.core/CoreInitializationOptions.cs engine/helengine.core/Core.cs engine/helengine.core.tests/content
git -C C:\dev\helworks\helengine commit -m "feat(core): host content-manager configurator runs before shared processors" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Base-platform Play contracts (helengine)

**Files:**
- Create: `engine/helengine.baseplatform/Player/IPlatformPlayerAdapter.cs`, `PlayerMaterialDescription.cs`, `PlayerMaterialRenderClass.cs`
- Create: `engine/helengine.baseplatform/Definitions/PlatformPlayerCapability.cs`
- Create: `engine/helengine.baseplatform/Requests/PlatformBuildExecutionScope.cs`
- Modify: `engine/helengine.baseplatform/Definitions/PlatformDefinition.cs:24-38,94` (new last ctor parameter `PlatformPlayerCapability playerCapability = null`, property `PlayerCapability`, null → `PlatformPlayerCapability.CreateUnsupported()`; thread the parameter through every overload that forwards to the full ctor)
- Modify: `engine/helengine.baseplatform/Requests/PlatformBuildRequest.cs:61` (new last parameter `PlatformBuildExecutionScope executionScope = PlatformBuildExecutionScope.Full`, property `ExecutionScope`; constructor A forwards `Full`)
- Test: `engine/helengine.baseplatform.tests/Player/PlatformPlayerCapabilityTests.cs`, `engine/helengine.baseplatform.tests/Requests/PlatformBuildRequestExecutionScopeTests.cs`

**Interfaces:**
- Produces:
  - `enum PlayerMaterialRenderClass { Opaque = 0, AlphaTest = 1, Transparent = 2 }`
  - `sealed class PlayerMaterialDescription(byte baseColorR, byte baseColorG, byte baseColorB, byte baseColorA, string diffuseTextureRelativePath, PlayerMaterialRenderClass renderClass, bool doubleSided)` with get-only properties of the same names (PascalCase); `diffuseTextureRelativePath` may be `string.Empty` (no texture), never null.
  - `interface IPlatformPlayerAdapter { void Initialize(string contentRootPath); void RegisterContentProcessors(ContentManager contentManager); PlayerMaterialDescription DecodeMaterial(Stream cookedMaterial, string materialRelativePath); IReadOnlyDictionary<string, string> BuildContentAliases(); }` — `Initialize` is called once before the others.
  - `sealed class PlatformPlayerCapability(bool supportsPlay, string adapterTypeName, string contentRootRelativePath)`; `static CreateUnsupported()` → `(false, "none", "none")`. When `supportsPlay` is true both strings must be non-blank (throw `ArgumentException`). `contentRootRelativePath` is relative to the builder working root (PS2: `"ps2-staging"`).
  - `enum PlatformBuildExecutionScope { Full = 0, AssetsOnly = 1 }`; `PlatformBuildRequest.ExecutionScope`.

- [ ] **Step 1: Write the failing tests**

```csharp
namespace helengine.baseplatform.tests.Player {
    /// <summary>
    /// Verifies Play capability validation and the platform-definition default.
    /// </summary>
    public sealed class PlatformPlayerCapabilityTests {
        /// <summary>A supported capability must name its adapter type.</summary>
        [Fact]
        public void Ctor_WhenSupportedWithoutAdapterType_Throws() {
            Assert.Throws<ArgumentException>(() => new PlatformPlayerCapability(true, " ", "ps2-staging"));
        }

        /// <summary>A supported capability must name its content root.</summary>
        [Fact]
        public void Ctor_WhenSupportedWithoutContentRoot_Throws() {
            Assert.Throws<ArgumentException>(() => new PlatformPlayerCapability(true, "a.B", ""));
        }

        /// <summary>The unsupported capability does not support Play.</summary>
        [Fact]
        public void CreateUnsupported_DoesNotSupportPlay() {
            Assert.False(PlatformPlayerCapability.CreateUnsupported().SupportsPlay);
        }
    }
}
```

```csharp
namespace helengine.baseplatform.tests.Requests {
    /// <summary>
    /// Verifies the build execution scope carried by platform build requests.
    /// </summary>
    public sealed class PlatformBuildRequestExecutionScopeTests {
        /// <summary>The short constructor keeps full builds.</summary>
        [Fact]
        public void ShortCtor_DefaultsToFullScope() {
            PlatformBuildRequest request = PlatformBuildRequestTestFactory.Create(PlatformBuildExecutionScope.Full, useShortCtor: true);
            Assert.Equal(PlatformBuildExecutionScope.Full, request.ExecutionScope);
        }

        /// <summary>The long constructor carries an assets-only scope.</summary>
        [Fact]
        public void LongCtor_CarriesAssetsOnlyScope() {
            PlatformBuildRequest request = PlatformBuildRequestTestFactory.Create(PlatformBuildExecutionScope.AssetsOnly, useShortCtor: false);
            Assert.Equal(PlatformBuildExecutionScope.AssetsOnly, request.ExecutionScope);
        }
    }
}
```

`PlatformBuildRequestTestFactory` (own file in the same test folder): look for an existing request-building helper in `helengine.baseplatform.tests` first (grep `new PlatformBuildRequest(`) and copy its manifest / target-variant / cook-profile construction. It returns either `new PlatformBuildRequest(manifest, variants, cookProfiles, outputRoot, workingRoot)` or the long constructor with `"", "", "", empty dictionaries, "", "", "", "release", "", scope`. Use `C:\dev\helworks\builds\helengine\play-tests\request` as both roots; it is never created on disk.

- [ ] **Step 2: Run to verify failure**

Run: `dotnetw.cmd test engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj --filter "FullyQualifiedName~PlatformPlayerCapabilityTests|FullyQualifiedName~PlatformBuildRequestExecutionScopeTests" -v:minimal`
Expected: FAIL (types missing).

- [ ] **Step 3: Implement** the five new files exactly as described in **Interfaces**. Give each XML docs. Follow `PlatformHostDebugCapability.cs` for style. `IPlatformPlayerAdapter.cs` uses `ContentManager` from `helengine.core` (baseplatform already references core). Then make the `PlatformDefinition` and `PlatformBuildRequest` modifications.

- [ ] **Step 4: Run to verify pass** — same command. Then run the whole `helengine.baseplatform.tests` project and compare failures by name against a pre-change run (`git stash` is forbidden; to get the baseline, run the suite before Step 3).

- [ ] **Step 5: Commit** — `feat(baseplatform): Play adapter contract, player capability, assets-only build scope`.

---

### Task 3: Assets-only build path in the editor build graph (helengine)

**Files:**
- Create: `engine/helengine.editor/managers/project/EditorPlayCookResult.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs` (new `public virtual EditorPlayCookResult ExecuteAssetsOnly(EditorBuildQueueItemDocument queueItem)`)
- Modify: `RunPackagePlatform` / the `PlatformBuildRequest` construction at `EditorPlatformBuildGraphRunner.cs:1543-1559` (pass the scope)
- Test: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerAssetsOnlyTests.cs`

**Interfaces:**
- Consumes: `PlatformBuildExecutionScope`, `PlatformPlayerCapability` (Task 2).
- Produces: `sealed class EditorPlayCookResult(bool succeeded, string message, string contentRootPath, string startupSceneId, string startupSceneCookedRelativePath)` with get-only properties; `static Failure(string message)` (empty paths). `EditorPlatformBuildGraphRunner.ExecuteAssetsOnly(queueItem)`.

**Behavior of `ExecuteAssetsOnly` (mirror `Execute`, `EditorPlatformBuildGraphRunner.cs:152-263`, with these differences):**
1. Load the builder. If `!builder.Definition.PlayerCapability.SupportsPlay`, return `EditorPlayCookResult.Failure($"Platform '{PlatformDescriptor.Id}' does not support Play.")`.
2. Workspace: `WorkspaceFactory.Create(PlatformDescriptor.Id, selectedBuildProfileId, "play")`. The constant queue id gives a **stable** workspace. Read `EditorPlatformBuildGraphWorkspaceFactory.Create` and `EditorBuildIsolationPathResolver.ResolveWorkspaceExecutionRootPath` first. If the execution root includes a fresh `Guid`, add an overload `Create(platformId, buildProfileId, queueItemId, bool stableExecutionRoot)` that omits the Guid segment when `stableExecutionRoot` is true. Use it here only.
3. Reset `CookRootPath`, `PackageRootPath` and `LayoutRootPath` as `Execute` does. **Do not** reset `BuilderWorkingRootPath`, because the platform's fingerprint store and staging live there. Do not touch `GeneratedCoreRootPath`.
4. Run: `PrepareTemporaryGeneratedScenePathOverrides` → `RunCookAssets` (with the same `finally` cleanup) → `RunResolveVariants` → `RunLayoutMedia` → `RunWriteContainers` → `RunPackagePlatform(..., PlatformBuildExecutionScope.AssetsOnly)`.
5. **Skip:** `RunRegenerateCore`, `RunCompileCode`, `CopySceneReferencedRuntimeModuleSourcesIntoGeneratedCore`, `EmitGeneratedRuntimeComponentDeserializersForCookedScenes`, scene code-module plan, `WriteGeneratedCoreTranslationUnit`, `ValidateAndWriteRuntimeFeatureManifest`, `WriteRuntimeNativeManifestSources`, `FinalizeBuildExecution`.
6. Before step 4, read `RunCookAssets`, `RunResolveVariants`, `RunLayoutMedia`, `RunWriteContainers` and `RunPackagePlatform`. If any of them reads `GeneratedCoreRootPath` contents or `codeModules`, report back instead of guessing (BLOCKED, with file:line).
7. Result:
   - `contentRootPath = Path.Combine(workspace.BuilderWorkingRootPath, builder.Definition.PlayerCapability.ContentRootRelativePath)`
   - `startupSceneId = cookedManifest.StartupSceneId`
   - `startupSceneCookedRelativePath` = the matching `cookedManifest.Scenes[i].ResolvedMetadata[PlatformBuildSceneMetadataKeys.CookedRelativePath]`, with `\` replaced by `/`. If it is missing, throw `InvalidOperationException` naming the scene id.
   - A failed package result maps to `Failure(packageResult.Message)`.
8. Add an optional parameter `PlatformBuildExecutionScope executionScope = PlatformBuildExecutionScope.Full` to `RunPackagePlatform`, and pass it to the `PlatformBuildRequest` constructor's new last argument.

- [ ] **Step 1: Write the failing test.** Find the existing runner tests first: grep `EditorPlatformBuildGraphRunner` in `engine/helengine.editor.tests` and reuse their fake builder and runner subclass pattern. Write `ExecuteAssetsOnly_WhenPlatformDoesNotSupportPlay_ReturnsFailureWithoutBuilding`. The fake builder's `Definition.PlayerCapability` is unsupported. Assert `Succeeded == false`, the message contains the platform id, and the fake `BuildAsync` was never called. Write `ExecuteAssetsOnly_PassesAssetsOnlyScopeAndReturnsStagingContentRoot`. The fake builder declares `new PlatformPlayerCapability(true, "x.Adapter", "staging")` and records the `PlatformBuildRequest`. Assert `request.ExecutionScope == AssetsOnly` and `result.ContentRootPath == Path.Combine(request.WorkingRoot, "staging")`. If the existing harness cannot drive a real `RunCookAssets` end to end, override the protected/virtual phase methods in the test subclass. Make the needed phase methods `protected virtual` when they are private, and say so in the commit message.
- [ ] **Step 2: Run to verify failure**: `dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlatformBuildGraphRunnerAssetsOnlyTests" -v:minimal` → FAIL.
- [ ] **Step 3: Implement** per **Behavior** above.
- [ ] **Step 4: Run to verify pass**, then run all `EditorPlatformBuildGraphRunner*` tests by filter and compare with the baseline by name.
- [ ] **Step 5: Commit** — `feat(editor): assets-only build graph path for Play`.

---

### Task 4: Play script build with platform gameplay symbols (helengine)

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorDotNetScriptBuildTool.cs:137-147,209-223` (write optional extra define constants into the execution-output `.props` file)
- Create: `engine/helengine.editor/managers/project/EditorPlayScriptBuildService.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorDotNetScriptBuildToolDefineConstantsTests.cs`

**Interfaces:**
- Produces:
  - `EditorDotNetScriptBuildTool.Build(string solutionPath, string executionOutputRootPath, IReadOnlyList<string> additionalDefineConstants)`. It throws on null. An empty list behaves exactly like the existing two-argument overload.
  - `public sealed class EditorPlayScriptBuildService(string projectRootPath, string projectName)` with `EditorBuildExecutionResult Build(string platformId, string environmentId, out string runtimeModulesRootPath)`.
    - It generates the solution files (`EditorGameSolutionService.GenerateSolutionFiles()`).
    - It builds `GeneratedProductionSolutionFilterFilePath` into `<project>/cache/generated_code/play/<platformId>` with `EditorPlatformPreprocessorSymbolService.ResolveGameplaySymbols(platformId, environmentId)`.
    - It sets `runtimeModulesRootPath` to `<that root>/generated_code/bin`. Confirm the folder against `EditorGameSolutionService.cs:836-845`.
  - `out` is used here because tuples are forbidden. If the house style prefers a result class, add `EditorPlayScriptBuildResult` instead and use that name consistently in Tasks 6 and 7.

**Mechanism:** the generated module csprojs already import the `.props` file named by env var `HelengineExecutionOutputRootFile`. Next to `<HelengineExecutionOutputRoot>`, write `<DefineConstants>$(DefineConstants);SYM1;SYM2</DefineConstants>` when symbols are given. Read lines 137-147 and 209-223 to find where the props XML is produced, and add the element there. Extract the props-XML creation into a `static string BuildExecutionOutputPropsXml(string outputRootPath, IReadOnlyList<string> defineConstants)` so the next step can test it. If `EditorPlatformPreprocessorSymbolService` is `internal`, call it from inside `helengine.editor`, which is fine.

- [ ] **Step 1: Failing test.** Call `BuildExecutionOutputPropsXml("C:/x", new[] { "A", "HELENGINE_ENV_RELEASE" })`. Parse it with `XDocument` and assert that the `DefineConstants` element value is `$(DefineConstants);A;HELENGINE_ENV_RELEASE` and that `HelengineExecutionOutputRoot` is `C:/x`. Add a second case with an empty list: no `DefineConstants` element. This tests generated XML, not hand-written source. If the method is `internal`, check that `InternalsVisibleTo` for `helengine.editor.tests` exists; otherwise make it `public`.
- [ ] **Step 2:** Run the test, expect FAIL.
- [ ] **Step 3:** Implement the overload, the helper and `EditorPlayScriptBuildService`.
- [ ] **Step 4:** Run the test, expect PASS. Then do a one-off check that the symbols actually reach the compiler:
  - Build `C:\dev\helworks\demodisc` (or `test-project`) scripts through `EditorPlayScriptBuildService.Build("ps2", "", out root)` from a throwaway xUnit `[Fact(Skip = "manual")]`, or from the editor CLI if it is quicker.
  - Then inspect the built dll with `System.Reflection.Metadata`. If the project has no `#if` code, temporarily add a script under `assets/codebase` that defines a type only under `#if !DESKTOP_PLATFORM`, and confirm the type exists. Remove the temporary script afterwards.
  - Report the result in the task summary.
- [ ] **Step 5: Commit** — `feat(editor): build runtime script modules with platform gameplay symbols for Play`.

---

### Task 5: `helengine.player` executable (helengine)

**Files:**
- Create: `engine/helengine.player/helengine.player.csproj`, plus `Program.cs`, `PlayerLaunchOptions.cs`, `PlayerScriptModuleLoader.cs`, `PlayerPlatformAdapterLoader.cs`, `PlayerAdapterContentManagerConfigurator.cs`, `PlayerStandardMaterialFactory.cs`, `PlayerRenderManager3D.cs`, `PlayerWindow.cs`, `PlayerHost.cs`
- Create: `engine/helengine.player.tests/helengine.player.tests.csproj`, `PlayerLaunchOptionsTests.cs`
- Modify: `helengine.ui/helengine.sln` (add both projects): `dotnetw.cmd sln helengine.ui\helengine.sln add engine\helengine.player\helengine.player.csproj engine\helengine.player.tests\helengine.player.tests.csproj`

**Interfaces:**
- Consumes: `IAssetContentManagerConfigurator` (Task 1), `IPlatformPlayerAdapter`, `PlayerMaterialDescription` (Task 2).
- Produces: the command line consumed by Task 6:
  `helengine.player.exe --content-root <dir> --scene-id <id> --scene-path <cooked/relative.hasset> --adapter-assembly <dll> --adapter-type <full type name> --script-modules <dir> [--wait-for-debugger]`
- `PlayerLaunchOptions` exposes the get-only properties `ContentRootPath`, `SceneId`, `SceneCookedRelativePath`, `AdapterAssemblyPath`, `AdapterTypeName`, `ScriptModulesRootPath` and `WaitForDebugger`, plus `static PlayerLaunchOptions Parse(string[] args)`.

**csproj** — copy `engine/helengine.render.validation/helengine.render.validation.csproj` and change only these parts:
- `<OutputType>WinExe</OutputType>`, `net9.0-windows`, `UseWindowsForms`.
- References: `helengine.core`, `helengine.core.windows`, `helengine.directx11`, `helengine.editor` (for `EditorBuiltInShaderAssetLibrary`, `EditorSessionShaderMaterialBuilder`, `StandardMaterial*Defaults`), `helengine.baseplatform`, `helengine.shader.compilation`, `helengine.input` if `InputBackendWindows` lives there (check `render.validation`'s usings).
- Keep the `shaders\*.fx` copy item.
- Add `engine\helengine.editor\shaders\builtin\*.hlsl` copied to `shaders\builtin\%(Filename)%(Extension)`, so `EditorBuiltInShaderAssetLibrary` finds them via `AppContext.BaseDirectory`.

The tests csproj: net9.0-windows, xunit 2.9.3, Microsoft.NET.Test.Sdk 17.14.1, xunit.runner.visualstudio 3.1.4, `<Using Include="Xunit" />`, ProjectReference to the player csproj. Copy the package versions from `helengine.core.tests.csproj`.

- [ ] **Step 1: Failing tests (`PlayerLaunchOptionsTests`)**

```csharp
namespace helengine.player.tests {
    /// <summary>
    /// Verifies command-line parsing for the Play player.
    /// </summary>
    public sealed class PlayerLaunchOptionsTests {
        static readonly string[] ValidArgs = {
            "--content-root", "C:/c", "--scene-id", "cube_test", "--scene-path", "cooked/scenes/cube_test.hasset",
            "--adapter-assembly", "C:/p/helengine.ps2.builder.dll", "--adapter-type", "helengine.ps2.builder.Ps2PlayerAdapter",
            "--script-modules", "C:/s" };

        /// <summary>All required options are read.</summary>
        [Fact]
        public void Parse_WhenAllRequiredPresent_ReadsValues() {
            PlayerLaunchOptions options = PlayerLaunchOptions.Parse(ValidArgs);
            Assert.Equal("C:/c", options.ContentRootPath);
            Assert.Equal("cube_test", options.SceneId);
            Assert.Equal("cooked/scenes/cube_test.hasset", options.SceneCookedRelativePath);
            Assert.Equal("helengine.ps2.builder.Ps2PlayerAdapter", options.AdapterTypeName);
            Assert.False(options.WaitForDebugger);
        }

        /// <summary>The debugger flag is a switch.</summary>
        [Fact]
        public void Parse_WhenWaitForDebuggerPresent_SetsFlag() {
            Assert.True(PlayerLaunchOptions.Parse(ValidArgs.Append("--wait-for-debugger").ToArray()).WaitForDebugger);
        }

        /// <summary>Missing required options throw and name the option.</summary>
        [Theory]
        [InlineData("--content-root")]
        [InlineData("--scene-id")]
        [InlineData("--scene-path")]
        [InlineData("--adapter-assembly")]
        [InlineData("--adapter-type")]
        [InlineData("--script-modules")]
        public void Parse_WhenRequiredMissing_Throws(string option) {
            List<string> args = ValidArgs.ToList();
            int index = args.IndexOf(option);
            args.RemoveRange(index, 2);
            ArgumentException exception = Assert.Throws<ArgumentException>(() => PlayerLaunchOptions.Parse(args.ToArray()));
            Assert.Contains(option, exception.Message);
        }

        /// <summary>Unknown options are rejected instead of ignored.</summary>
        [Fact]
        public void Parse_WhenUnknownOption_Throws() {
            Assert.Throws<ArgumentException>(() => PlayerLaunchOptions.Parse(ValidArgs.Append("--bogus").ToArray()));
        }
    }
}
```

- [ ] **Step 2:** `dotnetw.cmd test engine\helengine.player.tests\helengine.player.tests.csproj -v:minimal` → FAIL.
- [ ] **Step 3: Implement `PlayerLaunchOptions`.** Use a plain index loop over `args`; a value option consumes `args[i + 1]`. Then run the tests → PASS. Commit (`feat(player): player project and launch options`).
- [ ] **Step 4: Implement the rest.** These pieces need a GPU/window and are validated manually in Task 10.
  - `PlayerScriptModuleLoader.LoadAll(string modulesRootPath)`: for each `<modulesRootPath>/<moduleId>/<moduleId>.dll`, call `Assembly.LoadFrom(path)`. That puts the assemblies in the default, non-collectible context, which is what `PersistedComponentTypeResolver.TryLoadAssembly` scans; `.pdb` files next to them are picked up by the debugger. Throw if the folder does not exist; an empty folder is fine.
  - `PlayerPlatformAdapterLoader.Load(string assemblyPath, string typeName)`: `Assembly.LoadFrom` → `GetType(typeName, throwOnError: true)` → `Activator.CreateInstance` → cast to `IPlatformPlayerAdapter`, otherwise throw `InvalidOperationException` naming the type.
  - `PlayerAdapterContentManagerConfigurator : IAssetContentManagerConfigurator` holds the adapter; `Configure` calls `Adapter.RegisterContentProcessors(contentManager)`.
  - `PlayerStandardMaterialFactory(RenderManager3D renderer, EditorBuiltInShaderAssetLibrary shaders)` with `RuntimeMaterial Create(PlayerMaterialDescription description, RuntimeTexture diffuseTexture)`:
    - `EditorSessionShaderMaterialBuilder.BuildBuiltInRuntimeMaterial(renderer, shaders, "ForwardStandardShader.hlsl")`.
    - `ShaderRuntimeMaterial shaderMaterial = ShaderRuntimeMaterialAccess.Require(material)`.
    - `StandardMaterialTextureBindingDefaults.Apply(shaderMaterial, renderer.OwnerCore.RenderManager2D)`.
    - `shaderMaterial.Properties.SetConstantBufferData(StandardMaterialBaseColorDefaults.BaseColorBufferName, StandardMaterialBaseColorDefaults.CreateConstantBufferData(new float4(r / 255f, g / 255f, b / 255f, a / 255f)))`.
    - If the texture is non-null, `SetTexture(StandardMaterialTextureBindingDefaults.DiffuseTextureBindingName, texture)`.
    - `material.RenderState.BlendMode = renderClass == Transparent ? MaterialBlendMode.AlphaBlend : MaterialBlendMode.Opaque`.
    - `material.RenderState.CullMode = doubleSided ? MaterialCullMode.None : MaterialCullMode.Back`.
    - AlphaTest renders as Opaque in this first cut, which is an accepted approximation per the spec. Mirror `EngineGeneratedMaterialCache.CreateRuntimeMaterial` (`managers/asset/EngineGeneratedMaterialCache.cs:86-109`) for the constant-buffer setup if `BuildBuiltInRuntimeMaterial` leaves `EmissiveColorBuffer` unset.
  - `PlayerRenderManager3D : DirectX11Renderer3D`, ctor `(IPlatformPlayerAdapter adapter, EditorBuiltInShaderAssetLibrary shaders)`, overriding `BuildMaterialFromRawAsset(ContentManager assetContentManager, string materialAssetPath)`:
    1. Open the stream with `assetContentManager.ContentStreamSource.OpenRead(materialAssetPath)`.
    2. `Adapter.DecodeMaterial(stream, materialAssetPath)`.
    3. If `DiffuseTextureRelativePath` is not empty, load the texture with `assetContentManager.Load<TextureAsset>(path, RuntimeContentProcessorIds.TextureAsset)`, then `OwnerCore.RenderManager2D.BuildTextureFromRaw(textureAsset)`, then `NativeOwnership.DisposeAndDelete(textureAsset)`, as `TextureUtils.BuildSolidPixelTexture` does.
    4. Call the factory.
    Copy the exact base signature from `RenderManager3D.cs:238`.
  - `PlayerWindow : Form`: a fixed 640×480 client area, title `"helengine player — <sceneId>"`, `DoubleBuffered = false`.
  - `PlayerHost.Run(PlayerLaunchOptions options)`:
    1. If `WaitForDebugger`, write `"[player] waiting for debugger, pid " + Environment.ProcessId` to stdout and spin `while (!Debugger.IsAttached) Thread.Sleep(100);`. This is a host wait, not a frame loop sleep.
    2. Load the adapter, then `adapter.Initialize(options.ContentRootPath)`, then load the scripts.
    3. Create the shader library as `RenderValidationRunner.cs:157-161` does, with a `DirectX11ShaderBackend`.
    4. Create `PlayerRenderManager3D`, the window and `InputBackendWindows(window.Handle)`.
    5. Build the source: `IContentStreamSource source = new AliasedContentStreamSource(new HostFileSystemContentStreamSource(options.ContentRootPath), adapter.BuildContentAliases())`.
    6. `Core core = new Core(new CoreInitializationOptions { ContentStreamSource = source, AssetContentManagerConfigurator = new PlayerAdapterContentManagerConfigurator(adapter), SceneCatalog = new RuntimeSceneCatalog(new[] { new RuntimeSceneCatalogEntry(options.SceneId, options.SceneCookedRelativePath) }) })`.
    7. `core.Initialize(renderer3D, renderer3D.Render2D, input, new PlatformInfo("player", "1"), options)`. Pass the same options instance the Core was constructed with; check the `Core` constructor/`Initialize` contract in `Core.cs:583-600`.
    8. `renderer3D.AddWindow(window.Handle, w, h)`, then `core.SceneManager.LoadScene(options.SceneId, SceneLoadMode.Single)`. Check how `SceneManager` is exposed on `Core`.
    9. Frame loop: show the window and loop `while (window.Visible) { Application.DoEvents(); core.Update(deltaSeconds); core.Draw(); }` with a `Stopwatch` delta, as in `RenderValidationRunner.cs:1210-1225`.
    10. Dispose the core and the shader library on exit.
  - `Program.Main`: `[STAThread]`. Parse the options, then `PlayerHost.Run`. Unhandled exceptions write `exception.ToString()` to stderr and `return 1`. This is the process boundary, so reporting and exiting non-zero is correct here, not masking.
- [ ] **Step 5:** Build the player: `dotnetw.cmd build engine\helengine.player\helengine.player.csproj -v:minimal`. Expected: success with no new warnings in the player files. Do **not** run it.
- [ ] **Step 6: Commit** — `feat(player): desktop C# player host with platform adapter and standard material`.

---

### Task 6: `EditorPlaySessionService` (helengine)

**Files:**
- Create in `engine/helengine.editor/managers/play/`: `EditorPlaySessionState.cs`, `EditorPlayLaunchRequest.cs`, `IEditorPlayCookRunner.cs`, `IEditorPlayScriptBuilder.cs`, `IEditorPlayProcessLauncher.cs`, `IEditorPlayProcess.cs`, `EditorPlaySessionService.cs`
- Test: `engine/helengine.editor.tests/managers/play/EditorPlaySessionServiceTests.cs`, plus one file each for the fakes: `FakePlayCookRunner.cs`, `FakePlayScriptBuilder.cs`, `FakePlayProcessLauncher.cs`, `FakePlayProcess.cs`

**Interfaces:**
- Consumes: `EditorPlayCookResult` (Task 3), the Play script build (Task 4), the player command line (Task 5).
- Produces:
  - `enum EditorPlaySessionState { Stopped = 0, Preparing = 1, Running = 2 }`
  - `interface IEditorPlayCookRunner { EditorPlayCookResult Cook(string platformId); }`
  - `interface IEditorPlayScriptBuilder { EditorBuildExecutionResult Build(string platformId, out string runtimeModulesRootPath); }`
  - `interface IEditorPlayProcess { int ProcessId { get; } bool HasExited { get; } int ExitCode { get; } void Kill(); event Action<string> OutputLine; event Action<string> ErrorLine; event Action Exited; }`
  - `interface IEditorPlayProcessLauncher { IEditorPlayProcess Start(EditorPlayLaunchRequest request); }`
  - `sealed class EditorPlayLaunchRequest(string playerExecutablePath, IReadOnlyList<string> arguments)`
  - `sealed class EditorPlaySessionService` with constructor `(IEditorPlayCookRunner cookRunner, IEditorPlayScriptBuilder scriptBuilder, IEditorPlayProcessLauncher launcher, string playerExecutablePath)`. Members:
    - `EditorPlaySessionState State { get; }`
    - `event Action<EditorPlaySessionState> StateChanged`
    - `bool IsPlayAvailable(PlatformPlayerCapability capability)`
    - `EditorBuildExecutionResult Play(string platformId, string currentScenePath, Func<bool> saveScene, string adapterAssemblyPath, string adapterTypeName, bool waitForDebugger)`
    - `void Stop()`
  - `Play` order:
    1. If `currentScenePath` is blank, return `Failure("Save the scene before pressing Play.")`.
    2. Kill the running player if any.
    3. State `Preparing`.
    4. If `!saveScene()`, return `Failure("Scene save failed; Play aborted.")`.
    5. `cookRunner.Cook`, stopping on failure.
    6. `scriptBuilder.Build`, stopping on failure.
    7. Build the arguments and start the process.
    8. State `Running`.
    9. Hook `OutputLine` to `Logger.WriteLine("[player] " + line)` and `ErrorLine` to `Logger.WriteError("[player] " + line)`.
    10. Hook `Exited`: if `ExitCode != 0`, `Logger.WriteError($"[player] exited with code {code}")`; then state `Stopped`.
    11. Return `Success("Player started (pid N).")`.
  - Every failure path ends in state `Stopped`. `Stop()` kills and sets `Stopped`.
  - Use the existing `EditorBuildExecutionResult.Success` / `Failure` factories; check their exact names in the file.

- [ ] **Step 1: Failing tests.** One `[Fact]` each:
  - `Play_WhenSceneNeverSaved_FailsWithoutCooking`
  - `Play_WhenSaveFails_FailsWithoutCooking`
  - `Play_WhenCookFails_DoesNotBuildScriptsOrLaunch`
  - `Play_WhenScriptBuildFails_DoesNotLaunch`
  - `Play_WhenAllSucceed_LaunchesWithExpectedArgumentsAndIsRunning`: assert the exact argument list, in order: `--content-root`, `--scene-id`, `--scene-path`, `--adapter-assembly`, `--adapter-type`, `--script-modules`, plus `--wait-for-debugger` only when requested.
  - `Play_WhenAlreadyRunning_KillsPreviousBeforeLaunching`
  - `Stop_KillsProcessAndReturnsToStopped`
  - `ProcessExit_WithNonZeroCode_ReturnsToStoppedAndLogsError`: subscribe to `Logger.ErrorLogged` in the test and unsubscribe in `finally`.
  - `IsPlayAvailable_FollowsCapability`

  The fakes record calls. `FakePlayProcess.RaiseExit(int code)` sets `HasExited` and `ExitCode` and raises `Exited`.
- [ ] **Step 2:** `dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlaySessionServiceTests" -v:minimal` → FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run the tests → PASS.
- [ ] **Step 5: Commit** — `feat(editor): Play session service (save, cook, script build, launch, stop)`.

---

### Task 7: Real adapters, toolbar buttons and EditorSession wiring (helengine)

**Files:**
- Create `engine/helengine.editor/managers/play/` `EditorPlayProcessLauncher.cs` and `EditorPlayProcess.cs`. These wrap `System.Diagnostics.Process` with `UseShellExecute = false`, `RedirectStandardOutput/Error = true`, `EnableRaisingEvents = true`, and `ArgumentList` filled from the request (no string concatenation). `OutputDataReceived` and `ErrorDataReceived` raise the line events, and `BeginOutputReadLine`/`BeginErrorReadLine` are called after `Start`. `Kill` calls `Process.Kill(entireProcessTree: true)` and does nothing when the process has already exited. The console events arrive on a thread-pool thread, so check how `LoggerPanel` handles cross-thread `Logger` calls and marshal to the editor thread if it requires it.
- Create `EditorPlayCookRunner.cs`. It wraps an `EditorPlatformBuildGraphRunner` for the active platform, built the way `EditorCliBuildRunner.cs:149-173` builds `EditorPlatformBuildExecutor`. It calls `ExecuteAssetsOnly` with a queue item from `EditorBuildQueueItemDocument.Create(sceneCatalogService, platformConfig, selectionModel, <project>/cache/play/<platformId>/output)`, then sets `SelectedSceneReferences` to only the current scene. If `EditorPlatformBuildExecutor` hides the runner, add a public `ExecuteAssetsOnly` pass-through on it instead.
- Create `EditorPlayScriptBuilder.cs`. It wraps `EditorPlayScriptBuildService` (Task 4), using the active environment id.
- Modify `EditorViewportToolbarIconSet.cs`: add the optional ctor parameters `RuntimeTexture playIcon = null, RuntimeTexture stopIcon = null` and the matching properties. Also update the icon loader to load `play.png` and `stop.png`.
- Add `helengine.ui/helengine.editor.app/content/icons/toolbar/play.png` and `stop.png`: 16×16 white glyphs on transparent, the same style as the existing toolbar PNGs. Generate them with a short throwaway script (System.Drawing) that you do not commit; place the script in the scratchpad.
- Modify `EditorViewport.cs`:
  - Add two buttons after the stats/settings group, built with the same pattern as `CreateToolButton` (`:922-984`) but without a tool mode.
  - Add the fields `PlayButtonRoot`, `StopButtonRoot` and their sprite/interactable/focus companions.
  - Add the public events `PlayRequested` and `StopRequested`.
  - Add `public void SetPlayState(EditorPlaySessionState state, bool playAvailable)`. It dims Play when unavailable, highlights Stop when running, and sets the tooltip text if tooltips exist (grep `Tooltip` in `EditorViewport.cs`).
  - Lay the buttons out in the toolbar layout code (`:1728-1995`).
- Modify `EditorSession.cs`:
  - Construct `EditorPlaySessionService` with the real adapters. The player path is `Path.Combine(AppContext.BaseDirectory, "helengine.player.exe")`. Make `helengine.editor.app.csproj` reference `helengine.player` with `ReferenceOutputAssembly=false` and an item that copies its output next to the editor. If that is awkward, resolve the path from `<repo>/engine/helengine.player/bin/<cfg>/net9.0-windows/helengine.player.exe` via `EditorSourceBuildWorkspaceLocator`. Choose whichever existing pattern the codebase already uses for the codegen tool, and say which one you chose.
  - Add `bool SaveCurrentSceneForPlay()`. If `CurrentScenePath` is blank, return false. Otherwise call `HandleSceneSaveRequested(CurrentScenePath)` and return true. Do not catch exceptions; a save exception propagates to the Play click, which logs it through `Logger.WriteError` in the click handler, and that is the editor's existing UI error boundary. Check how other toolbar handlers report exceptions and mirror it.
  - Wire `PlayRequested` to `PlaySession.Play(CurrentProjectPlatform, CurrentScenePath, SaveCurrentSceneForPlay, descriptor.BuilderAssemblyPath, capability.AdapterTypeName, waitForDebugger: false)`, and log the returned message.
  - Wire `StopRequested` to `Stop()`, and `StateChanged` to `viewport.SetPlayState`.
  - Register `ConstructionLedger.Register(() => PlaySession.Stop(), EditorSessionCleanupPhase.Dispose)` next to `EditorSession.cs:2130`.
  - Get the capability from the builder definition already loaded by the platform catalog (`EditorPlatformCatalogService`). Do not reload the builder per click.
- [ ] **Step 1:** Write a `FakeProcess`-free unit test for `EditorPlayProcess`'s argument mapping only if the launcher exposes a pure `CreateStartInfo(EditorPlayLaunchRequest)` helper; assert `ArgumentList` equals the request arguments. Write that helper so it is testable. Run it (FAIL), implement it, run it (PASS).
- [ ] **Step 2:** Implement the rest. Build the editor app: `dotnetw.cmd build helengine.ui\helengine.editor.app\helengine.editor.app.csproj -v:minimal`. Expected: success.
- [ ] **Step 3:** Run the editor test suite and compare failures by name against the baseline: only the pre-existing reds are allowed.
- [ ] **Step 4: Commit** — `feat(editor): Play/Stop toolbar buttons wired to the Play session`.

---

### Task 8: PS2 assets-only scope and incremental cook (helengine-ps2)

**Files:**
- Create: `builder/Ps2CookFingerprintStore.cs`
- Modify: `builder/Ps2PlatformAssetBuilder.cs` (`ValidateRequest` 496-506, `BuildAsync` 80-146, `StageCookedArtifacts` 149-187, `WritePs2ModelAsset` 234-267)
- Modify: `builder/Ps2PlatformCookWorkItemExecutor.cs` (`Prepare` 77-115, `Publish` 119+)
- Test: `builder.tests/Ps2AssetsOnlyBuildTests.cs`

**Interfaces:**
- Consumes: `PlatformBuildRequest.ExecutionScope` (Task 2).
- Produces: `internal sealed class Ps2CookFingerprintStore`.
  - Ctor `(string stagingRootPath)` loads `<staging>/.helengine-cook-fingerprints.json` when present. The file holds a `Dictionary<string, string>` of output relative path → fingerprint, written with `System.Text.Json`.
  - Members: `bool IsUpToDate(string outputRelativePath, string fingerprint, string destinationPath)`, which is true only if the stored fingerprint matches **and** the destination file exists; `void Record(string outputRelativePath, string fingerprint)`; `void Save()`.
  - The fingerprint is lowercase hex of `SHA256.HashData`. Its inputs are the source file bytes, the serialized settings string (textures), and the constant `Ps2CookFingerprintStore.CookerVersion = "1"`, concatenated through an `IncrementalHash`.

**Behavior:**
- `ValidateRequest`: when `request.ExecutionScope == AssetsOnly`, skip the generated-core checks.
- `BuildAsync`: when `AssetsOnly`, return the `PlatformBuildReport` right after the Prepare/Stage/Publish block. Do not run `CreateWorkspace` or any later step.
- Textures:
  - In `Prepare`, compute the fingerprint from the source asset file bytes plus `workItem.SerializedPlatformSettings`. If `IsUpToDate`, mark the plan `Skipped = true` and do not cook.
  - `Publish` skips plans marked skipped and records the rest.
  - Find the source asset file path in `ResolveSourceTexture`. If the work item exposes `SourceContentHash` and `SettingsHash`, combine those instead of re-reading the file, and say which you used.
- Models and copied artifacts in `StageCookedArtifacts`:
  - The fingerprint is the source bytes plus the artifact kind.
  - If up to date, skip `WritePs2ModelAsset` / `File.Copy`.
  - `Reserve` still runs for every output, so collision detection is unchanged.
- Call `Save()` once at the end of a successful staging, in both scopes.
- Full builds use the store too. That is harmless, because the editor resets the working root for full builds.

- [ ] **Step 1: Failing tests**, built on the existing full-build test setup (`Ps2PlatformAssetBuilderTests.BuildAsync_WhenGivenGeneratedCoreAndCookedArtifacts_ProducesElfAndCookedTree`, lines 890-1019). Copy its manifest/request construction and change the scratch root to `C:\dev\helworks\builds\helengine-ps2\play-tests\<guid>`. Keep the existing `Directory.SetCurrentDirectory(stagingRoot)` requirement, and restore the CWD in `finally`.
  - `BuildAsync_WhenAssetsOnly_StagesModelAndSkipsNativeBuild`: use `FakePs2NativeBuildExecutor`. Assert `<working>/ps2-staging/cooked/imported/box_a.phm` exists, the fake's `Build`/`PackageIso` were never called, and the report succeeded. Pass an empty `GeneratedCoreCppRootPath` to prove validation is skipped.
  - `BuildAsync_WhenAssetsOnlyRunTwiceUnchanged_DoesNotRewriteStagedModel`: run twice. Between the runs, set the staged `.phm` last-write time to `2000-01-01` and assert it is unchanged after run 2.
  - `BuildAsync_WhenSourceChangesBetweenRuns_RewritesStagedModel`: change one byte of the source model (re-serialize a cube with a different size) and assert the last-write time moved.
- [ ] **Step 2:** `dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --filter "FullyQualifiedName~Ps2AssetsOnlyBuildTests"` → FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run the tests → PASS. Then run the full PS2 builder test project and compare by name with a baseline taken before Step 3.
- [ ] **Step 5: Commit** in helengine-ps2 — `feat(ps2): assets-only build scope and fingerprinted incremental staging`.

---

### Task 9: PS2 player adapter (helengine-ps2)

**Files:**
- Create: `builder/player/Ps2PlayerHeaderProbe.cs`, `Ps2PlayerTextureContentProcessor.cs`, `Ps2PlayerModelContentProcessor.cs`, `Ps2PlayerAdapter.cs`
- Modify: `builder/Ps2DiscLayoutWriter.cs:19-32`. Make `CollectPackageFileMappings` callable without a workspace by adding `internal static Dictionary<string, string> BuildLogicalToPhysicalPathMap(string stagingRootPath)`, used by the existing workspace overload.
- Modify: `builder/Ps2PlatformDefinitionFactory.cs:361-368`. Pass `new PlatformPlayerCapability(true, "helengine.ps2.builder.Ps2PlayerAdapter", "ps2-staging")` as the new last `PlatformDefinition` argument (Task 2).
- Test: `builder.tests/Ps2PlayerAdapterTests.cs`

**Interfaces:**
- Consumes: `IPlatformPlayerAdapter`, `PlayerMaterialDescription`, `PlayerMaterialRenderClass`, `PlatformPlayerCapability` (Task 2).
- Produces: `public sealed class Ps2PlayerAdapter : IPlatformPlayerAdapter` with a public parameterless ctor, in namespace `helengine.ps2.builder`.

**Behavior:**
- `Ps2PlayerHeaderProbe.IsPs2Asset(Stream stream)`:
  1. Remember the position.
  2. `EngineBinaryHeaderSerializer.Read(stream)`.
  3. Restore the position.
  4. Return `header.FormatId == 2`. Use the `FormatId` constant from `Ps2AssetBinarySerializer`; make it `internal` if needed, since `managed/helengine.ps2` → builder needs `InternalsVisibleTo`, or expose `public const`.
  5. A stream shorter than a header returns false.
  - Seekable streams only: wrap non-seekable input in a `MemoryStream` copy first.
- Texture processor (`IContentProcessor<TextureAsset>`, `OutputType => typeof(TextureAsset)`):
  - If the stream is PS2: `Ps2TextureAsset ps2 = (Ps2TextureAsset)Ps2AssetSerializer.Deserialize(stream)`.
    - If `ps2.Format` is any `Depth*` value, throw `NotSupportedException($"PS2 texture format {ps2.Format} cannot be played on PC.")`.
    - Otherwise `byte[] rgba = Ps2NativeTextureCodec.Decode(ps2)`. It already rescales GS alpha to 0..255, so no extra scaling.
    - Return `new TextureAsset { Id = ps2.Id, Width = ps2.Width, Height = ps2.Height, ColorFormat = TextureAssetColorFormat.Rgba32, AlphaPrecision = TextureAssetAlphaPrecision.A8, Colors = rgba }`.
    - Dispose `ps2`.
  - Otherwise: `new AssetContentProcessor<TextureAsset>().Read(stream)`.
- Model processor (`IContentProcessor<ModelAsset>`):
  - If the stream is PS2: `Ps2ModelAsset`. Throw if `Positions`, `Normals` or `TexCoords` is null or the lengths differ.
    - Return `new ModelAsset { Id, Positions, Normals, TexCoords, Indices16, BoundsMin, BoundsMax }`, with the bounds computed by a min/max loop over the positions. Put that loop in a static method on an existing utils type, such as `ModelUtils` in helengine.core if one exists. Otherwise add `Ps2PlayerModelBounds` as its own class.
    - `Submeshes` stays null, so one default submesh is synthesized.
  - Otherwise: generic `AssetContentProcessor<ModelAsset>`.
- `Initialize(contentRootPath)`:
  - Store the root.
  - `LogicalToPhysical = Ps2DiscLayoutWriter.BuildLogicalToPhysicalPathMap(contentRootPath)`.
  - Build `PhysicalToLogical`, a reverse dictionary. Its key is the physical value with the leading `\` and trailing `;1` stripped, compared `OrdinalIgnoreCase`; its value is the logical path.
  - Skip the `.hasset` alias entries when building the reverse map, and prefer the real staged file name.
- `RegisterContentProcessors(cm)`: `cm.RegisterProcessor<TextureAsset>(RuntimeContentProcessorIds.TextureAsset, new Ps2PlayerTextureContentProcessor())` and the same for model.
- `DecodeMaterial(stream, path)`:
  - If the stream is not PS2, throw `NotSupportedException` naming the path, because a non-PS2 material is not expected in PS2 staging.
  - Otherwise deserialize the `Ps2MaterialAsset`.
  - The texture path: an empty `TextureRelativePath` gives `string.Empty`. Otherwise `PhysicalToLogical[TextureRelativePath]`; if it is missing, throw `InvalidOperationException($"Material '{path}' references disc texture '{x}' that is not in the PS2 staging tree.")`.
  - The render class maps `Ps2RenderClass` → `PlayerMaterialRenderClass` by name. Check the enum members and use a `switch` that throws on unknown values.
  - Copy `DoubleSided` and the base color bytes.
- `BuildContentAliases()`: for every logical `.phm` path in `LogicalToPhysical`, alias `Path.ChangeExtension(logical, ".hasset")` → `logical`, in a dictionary with `StringComparer.Ordinal`. Paths stay canonical lowercase.

- [ ] **Step 1: Failing tests (`Ps2PlayerAdapterTests`)**. Use a scratch staging root under `C:\dev\helworks\builds\helengine-ps2\play-tests\<guid>\ps2-staging`, and add `IDisposable` cleanup.
  - `Texture_T4_T8_Ct32_DecodeToSourceRgba`:
    - Build a 4×4 RGBA `TextureAsset` source with distinct opaque colors (≤16 colors so indexed formats are lossless).
    - Cook it with `Ps2RuntimeTextureCooker` once per format id (`Rgba32`, `Indexed8`, `Indexed4`). Copy the settings construction from `Ps2RuntimeTextureCookerTests`.
    - Serialize with `Ps2AssetSerializer.SerializeToBytes`, then read it back through `new Ps2PlayerTextureContentProcessor().Read(new MemoryStream(bytes))`.
    - Assert `Colors` equals the source RGBA bytes.
  - `Texture_WhenGenericEngineAsset_PassesThrough`: serialize a generic `TextureAsset` with `AssetSerializer` (helengine.files) and assert width/height/colors round-trip unchanged.
  - `Texture_WhenDepthFormat_Throws`: a `Ps2TextureAsset` with `Format = Ps2TextureFormat.Depth32`, `NotSupportedException`.
  - `Model_DecodesIndexedArraysAndBounds`:
    - `ModelUtils.GenerateCubeMesh(float3(0,0,0), float3(1,1,1))`, written to staging as a `.phm` via the same code path the builder uses. If `WritePs2ModelAsset` is not reachable, construct a `Ps2ModelAsset` from the cube arrays directly.
    - Read it through the model processor and assert the arrays are equal, the bounds are min (-0.5…) and max (0.5…) per `GenerateCubeMesh` semantics (check them), and the indices are equal.
  - `Material_MapsDiscTexturePathBackToLogical`:
    - Create the staged file `cooked/imported/tex_a` (any bytes).
    - Cook a material with `new Ps2PlatformAssetBuilder().CookMaterial(new PlatformMaterialCookRequest("m", "m", "ps2", "ps2-default", "ps2-standard-forward", "ps2-simple-lit-textured", fields with ["texture-relative-path"] = "cooked/imported/tex_a", ["alpha-mode"] = "opaque", ["double-sided"] = "true", ...))`. Copy the field set from `Ps2PlatformAssetBuilderTests.cs:669-699`.
    - `adapter.Initialize(stagingRoot)`, then `DecodeMaterial`.
    - Assert `DiffuseTextureRelativePath == "cooked/imported/tex_a"`, `DoubleSided`, and `RenderClass == Opaque`.
  - `Material_WhenTextureNotStaged_Throws`: the same, but without the staged file. Expect `InvalidOperationException` whose message contains the material path.
  - `Aliases_MapHassetToStagedPhm`: stage `cooked/imported/box_a.phm`, then `Initialize`, then assert `BuildContentAliases()["cooked/imported/box_a.hasset"] == "cooked/imported/box_a.phm"`.
  - `Definition_DeclaresPlayerCapability`: `Ps2PlatformDefinitionFactory.Create().PlayerCapability.SupportsPlay` is true, `AdapterTypeName == typeof(Ps2PlayerAdapter).FullName`, and `Activator.CreateInstance(typeof(Ps2PlayerAdapter))` succeeds.
- [ ] **Step 2:** Run `--filter "FullyQualifiedName~Ps2PlayerAdapterTests"` → FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run the tests → PASS. Run the full PS2 builder tests and compare by name.
- [ ] **Step 5: Commit** in helengine-ps2 — `feat(ps2): Play player adapter (texture/model processors, material decode, aliases)`.

---

### Task 10: End-to-end manual validation (both repos)

- [ ] **Step 1:** Make the editor load the freshly built PS2 builder dll. Find how `platform-plugin.json`'s `builder/helengine.ps2.builder.dll` gets populated on this machine: check `user_settings/platforms.json` pins and the installed platform folder. Report the exact copy or publish command used, and do not invent a new deployment mechanism.
- [ ] **Step 2: Ask Helena** before launching anything. When she agrees, open demodisc with PS2 as the active platform, open `cube_test`, and press Play.
  - Expect a player window showing the scene: textures look PS2-quantized, every material looks like the standard material, and there is no audio.
  - Expect the `[player]` lines and the `[build]` phase markers in the editor console.
- [ ] **Step 3:** Press Play again without changes. Expect a faster cook, and the staged files' timestamps should not change.
- [ ] **Step 4:** Launch with `--wait-for-debugger` (temporarily flip `waitForDebugger: true` in the wiring, or run the player from VS with the logged arguments), attach VS, and hit a breakpoint in a demodisc gameplay script.
- [ ] **Step 5:** Write down any failure as a new task. Do not patch around it.
