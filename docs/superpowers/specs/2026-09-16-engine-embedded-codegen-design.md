# Engine-embedded codegen

**Status:** approved design, 2026-09-16
**Scope:** helengine only, plus one submodule addition. No platform builder repository changes.

## Problem

Every platform entry in the engine's platform registry, `user_settings/platforms.json`, carries its own `codegenToolPath`. That registry is untracked and gitignored, and the field is a filesystem path to a build output directory, so it pins a *build location*, not a version. Whatever binary was last built there wins, per platform, with no check that it matches the engine.

On 2026-09-16 this produced a console build failure that took hours to diagnose. Eleven platforms were running three different codegen binaries. A registry rewrite silently moved GameCube from the Debug output to a stale Release output. The only symptom was a missing C++ header sixty files into a unity build, with no mention of codegen anywhere.

The codegen is the tool that lowers engine and project C# to C++ for every native target. Its output has to match the engine's generated core exactly. Nothing about one platform versus another implies a different transpiler build. The tool's version belongs to the engine.

## Decisions

These were made during design and are not open:

1. **csharpcodegen becomes a git submodule** of helengine at `engine/vendor/csharpcodegen`, beside the existing `bepuphysics2` submodule. The engine commit alone states which codegen it requires, and git enforces it. csharpcodegen keeps its own repository and cadence.
2. **The initial pin is csharpcodegen master, `d8ad75d`.** This is the newest tool. It is known to reject the engine's serializer on GameCube with `CPP1001` (see Out of scope), so this design cannot be verified by a green console build. It is verified by demonstrating the mechanism instead (see Verification).
3. **No per-platform override.** The `codegenToolPath` field is removed outright rather than kept as an escape hatch. The only apparent case for one, the N64's separate codegen tree under `builds/helengine-n64/codegen-master`, is a plain ancestor of csharpcodegen master with no local changes: a stale pin, not a fork. Platform-specific lowering already flows through codegen profiles and option values, which stay as they are.
4. **The tool is an output of the engine build.** It is published beside the editor and located relative to the editor, never by configuration.

## Architecture

The chain is: engine commit → submodule commit → published `codegen.exe` → provider → the two codegen consumers. No link in it is user-editable.

### Submodule

`engine/vendor/csharpcodegen`, pinned to `d8ad75d`. Only `codegen/codegen.csproj` and its dependencies `cs2.core` and `cs2.cpp` are ever built by the engine. Those depend on Roslyn from NuGet and nothing else. The `cs2.ts` project's dependency on a `nucleusdotnet` checkout is irrelevant because the engine never builds that project.

### Build script publishes the tool

In `scripts/build-platform.ps1`, immediately after the existing editor publish and before the editor is executed:

- verify the submodule is initialised, i.e. `engine/vendor/csharpcodegen/codegen/codegen.csproj` exists; if not, fail with a message naming the path and `git submodule update --init`;
- `dotnet restore` and `dotnet publish` `codegen/codegen.csproj`, configuration Release, output `<editor publish path>/codegen/`, using the same `--artifacts-path` as the editor publish;
- verify `<editor publish path>/codegen/codegen.exe` exists; if not, exit with wrapper exit code 6, the next free code after the existing 5 for a missing editor assembly, and document it in the README.

The tool goes in a `codegen/` subdirectory rather than being merged flat into the editor's directory. codegen ships its own Roslyn assembly set, and the editor carries compiler references of its own. Keeping them apart avoids an assembly version collision that would be miserable to diagnose.

The codegen is always published in Release regardless of the editor configuration. The configuration of a transpiler has no business varying with the configuration of the thing invoking it.

### Provider

A new class `EngineCodegenToolProvider` in `engine/helengine.editor/managers/project`, with one public method `Resolve()` that returns the absolute path of the codegen executable. Resolution order:

1. **Published copy:** `<AppContext.BaseDirectory>/codegen/codegen.exe`. This is the normal path for every script-driven build.
2. **On-demand build:** for editor runs where nothing was published, such as from an IDE. Build the submodule's `codegen/codegen.csproj` in Release into the engine artifacts cache under a directory keyed by the submodule's commit, obtained by running `git rev-parse HEAD` in the submodule directory. Reuse it on later runs while the commit is unchanged. A bumped pin therefore never reuses a stale build. This path invokes `dotnet` the same way the editor already does for project script builds. If `git` cannot be run, the fallback fails with a message saying so; it is a development convenience, not a supported build path, and the published copy is the answer.
3. **Failure:** throw an `InvalidOperationException` naming both locations that were checked and, when the submodule is not initialised, the command that fixes it.

The provider is resolved once per build and the result passed down. It does not cache across builds in the GUI editor beyond the on-disk artifact.

`EditorSourceBuildWorkspaceLocator.ResolveCSharpCodegenRootPath` currently resolves a sibling `csharpcodegen` directory and has no callers. It is repointed at the submodule, made to validate that `codegen/codegen.csproj` exists there, and becomes the provider's source of the submodule path.

### Consumers

`CodegenToolPath` is deleted from `AvailablePlatformDescriptor`. The consumers change as follows:

- `EditorPlatformBuildGraphRunner`: the provider is a constructor dependency. `RunRegenerateCore` and `RunCompileCode` pass the provider's path where they passed `PlatformDescriptor.CodegenToolPath`.
- `EditorPlatformBuildExecutor`: the guard "Platform descriptor must provide a csharpcodegen tool path" is removed. It constructs the provider for the GUI build path and hands it to the runner.
- `EditorGeneratedCoreRegenerationService` and `EditorPlatformCodeCookService`: signatures unchanged. Both already take a tool path as a parameter; only the supplier changes.

Platform builders never receive a codegen path. A search of all 24 `helengine-*` repositories' `builder` and `builder.tests` folders found no reference to `CodegenToolPath`, so no builder repository changes.

### Registry

In `engine/helengine.platforms`:

- `PlatformInstallationEntry`, `AvailablePlatformDescriptor`, `PlatformInstallationResolver`: the `codegenToolPath` field, constructor parameter and property are removed. `IsInstalled` drops the clause that checked the tool path.
- `PlatformInstallationStore.Load`: if an entry still contains `codegenToolPath`, log one warning naming the platform id and stating the field is ignored because the codegen now comes from the engine. Then proceed. This is deliberately not fatal: the registry is untracked and irreplaceable on every machine, and a fatal here would strand every machine at once for no gain.

No migration of existing registries is required. They keep working unchanged; the field is simply inert.

## Error handling

Every failure this design can produce happens before any scene is cooked and names the tool:

| Situation | Where it fails | Message names |
|---|---|---|
| Submodule not initialised | Script, before publish; provider, if script bypassed | Submodule path and `git submodule update --init` |
| Codegen publish fails | Script, new exit code, editor never runs | The publish command's own output |
| Published tool missing after publish | Script, new exit code | Expected `codegen.exe` path |
| Neither published nor buildable | Provider, at build start | Both locations checked |
| Registry still has `codegenToolPath` | Nowhere; one warning | Platform id |

## Verification

The master pin means the CPP1001 rejection still stands, so a green GameCube build is not an available proof. The mechanism is proven instead:

1. The engine build's published tree contains `codegen/codegen.exe` beside the editor assembly.
2. A GameCube build's log shows the codegen invocation using that published path, and nothing under `csharpcodegen/codegen/bin`.
3. Negative test: set this machine's registry `codegenToolPath` for GameCube to a nonsense value, confirm the build logs the warning and still invokes the published tool, restore the value.
4. The GameCube build then fails at `CPP1001` in `PackagedAssetBinarySerializer.cs`, the known codegen regression, and nowhere earlier.
5. Unit tests: provider published-first, on-demand fallback, and loud failure; store warn-and-ignore; build graph runner tests updated for constructor injection. The full `helengine.editor.tests` and `helengine.platforms.tests` projects run green.

## Landing

helengine's main is shared and active. The work is done on branch `feature/engine-embedded-codegen` in a worktree, merged to main when green, in four commits that each build and test on their own:

1. Submodule addition and build script publish, with the script's smoke check.
2. `EngineCodegenToolProvider`, locator repoint, editor wiring, and their tests.
3. Registry field removal, store warning, and their tests.
4. README: exit code, and a note that the codegen is an engine build output.

## Out of scope

Recorded as follow-ups, not done here:

- **CPP1001.** csharpcodegen commit `7d6fe36`, capability-aware lowering for freestanding targets, causes current codegen to reject `reader.ReadArray<T>` in `PackagedAssetBinarySerializer.cs` for GameCube and, by the same mechanism, every other reflection-free console. That is a codegen behaviour question owned by the freestanding work. When it is fixed, delivering it to every console is a one-line submodule bump.
- **`generatedCoreCppRootPath`** is the same class of untracked path configuration and should get the same treatment.
- **Stale artifacts:** the N64 codegen tree under `builds/helengine-n64/codegen-master` and the three shared `csharpcodegen/codegen/bin` outputs become unused and can be deleted.
