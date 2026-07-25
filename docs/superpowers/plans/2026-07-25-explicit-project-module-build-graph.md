# Explicit Project Module Build Graph — Implementation Plan

> **For Helena:** execute this plan using the subagent-driven development workflow. Work directly on `main`; do not create a worktree.

**Goal:** Make project code modules explicit and split script compilation into `EditorFull` and `RuntimeOnly` modes, so clean isolated platform builds compile only runtime scripts while editor sessions continue to compile editor tools and tests.

**Architecture:** The generated-code solution builder receives an explicit compilation mode. It filters manifests before dependency validation and never invokes test discovery in `RuntimeOnly`. The generic platform wrapper obtains optional editor prebuild command ids from the selected project build profile, runs those under `EditorFull`, then invokes the existing cook/package path under `RuntimeOnly`. Demo Disc declares each real code surface with a manifest; test folders remain strict sibling bindings to declared production module ids.

**Tech Stack:** C#/.NET editor services and tests, JSON `code.module.json` manifests, PowerShell platform wrapper and Pester tests.

---

## Baseline and Non-Negotiable Behavior

- Preserve per-invocation isolation introduced in `scripts/build-platform.ps1`: each platform build retains its own copied project, generated-code workspace, editor publish directory, and output directory.
- Do not edit generated code or rely on `user_settings/generated_code`.
- Do not add implicit `gameplay` fallback bindings for test folders. In `EditorFull`, `<module-id>.tests` must bind to an explicitly declared module with the same id or fail descriptively.
- A runtime module may not depend on an editor module.
- `RuntimeOnly` must not enumerate `.tests` folders, generate test projects, build editor modules, load editor command registrations, or require project authoring commands.
- A profile with no editor prebuild commands (for example, the colored-cubes diagnostic profile) must make a clean platform build without loading Demo Disc editor tools.
- Do not change renderer behavior, cook data, scene content, or the current isolated-build design in this work.

## File Map

| Area | Primary files | Change |
| --- | --- | --- |
| Compilation-mode model | `engine/helengine.editor/managers/project/EditorScriptCompilationMode.cs` (new), `EditorGeneratedCodeSolutionBuilder.cs`, `EditorGameSolutionService.cs` | Make the script graph mode explicit and filter it at the source. |
| Entry points | `EditorCliBuildRunner.cs`, `EditorCliCommandRunner.cs`, `EditorSession.cs` | Platform cook uses `RuntimeOnly`; interactive/editor commands use `EditorFull`. |
| Test discovery | `EditorGeneratedCodeTestProjectDiscoveryService.cs` | Keep strict declared-module matching, but call it only for `EditorFull`. |
| Module validation | `EditorCodeModuleManifestService.cs` and dependency resolver/validator | Validate dependency kind and provide actionable diagnostics. |
| Build-profile commands | new editor build-profile/prebuild settings model/service and CLI command routing | Resolve ordered, project-authored prebuild command ids by selected platform/profile. |
| Wrapper | `scripts/build-platform.ps1`, `scripts/tests/build-platform-streaming.tests.ps1` | Remove hard-coded Demo Disc commands and call the generic prebuild phase only when declared. |
| Demo Disc declarations | `C:\dev\helprojs\demodisc\assets\codebase\**\code.module.json` | Declare the actual runtime/editor surfaces and their dependencies. |

## Task 1: Add a First-Class Script Compilation Mode

**Files:**

- Create: `engine/helengine.editor/managers/project/EditorScriptCompilationMode.cs`
- Modify: `engine/helengine.editor/managers/project/EditorGeneratedCodeSolutionBuilder.cs`
- Modify: `engine/helengine.editor/managers/project/EditorGameSolutionService.cs`
- Test: `engine/helengine.editor.tests/EditorGameSolutionServiceTests.cs`

1. Add a small enum in its own file:

   ```csharp
   /// <summary>
   /// Selects the authored script surfaces included in a generated-code build.
   /// </summary>
   public enum EditorScriptCompilationMode {
       /// <summary>
       /// Includes runtime modules, editor modules, and their declared sibling test projects.
       /// </summary>
       EditorFull,

       /// <summary>
       /// Includes runtime production modules only for cook and native platform packaging.
       /// </summary>
       RuntimeOnly
   }
   ```

2. Write a failing `EditorGameSolutionServiceTests` case that creates a runtime module, an editor module, a valid sibling test folder, and an orphan sibling test folder. Generate a `RuntimeOnly` solution and assert:

   - only runtime production projects appear;
   - no project id ends in `.tests`;
   - the orphan folder does not throw, proving test discovery was not invoked;
   - generated project paths live under the supplied invocation workspace.

3. Extend `EditorGeneratedCodeSolutionBuilder.Build(...)` with an `EditorScriptCompilationMode compilationMode` parameter. Before resolving project dependency order, select manifests as follows:

   - `EditorFull`: runtime and editor manifests;
   - `RuntimeOnly`: runtime manifests only.

4. Run dependency resolution only on the selected manifests. If a selected runtime module names an excluded editor dependency, throw an exception that names the runtime module id, dependency id, and that `RuntimeOnly` cannot load editor modules. This makes an invalid graph visible rather than silently omitting code.

5. Invoke `EditorGeneratedCodeTestProjectDiscoveryService.Discover(...)` and apply test-folder exclusions only in `EditorFull`. Construct the generated solution with an empty test-project set in `RuntimeOnly`.

6. Make `EditorGameSolutionService` store the mode as a constructor dependency, expose it only through its normal generated project collections, and pass it to the builder. Preserve the current constructor behavior for existing editor callers by making the existing public construction path explicitly select `EditorFull`; do not use an implicit nullable/default fallback.

7. Run the focused test before and after implementation:

   ```powershell
   dotnet test engine/helengine.editor.tests --filter FullyQualifiedName~EditorGameSolutionServiceTests
   ```

8. Commit: `feat(editor): separate runtime-only generated script builds`

## Task 2: Route Every Existing Entry Point to the Correct Mode

**Files:**

- Modify: `engine/helengine.editor/managers/build/EditorCliBuildRunner.cs`
- Modify: `engine/helengine.editor/managers/commands/EditorCliCommandRunner.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Test: `engine/helengine.editor.tests/EditorCliBuildRunnerTests.cs`
- Test: `engine/helengine.editor.tests/EditorGameSolutionServiceTests.cs`

1. Add a failing CLI-build test with an orphan `.tests` folder in an isolated project. Call the normal CLI cook/build runner and assert it reaches its test double/native-build boundary without the test-surface exception. This pins platform preparation to `RuntimeOnly`.

2. In `EditorCliBuildRunner`, construct `EditorGameSolutionService` with `RuntimeOnly`. Keep its existing per-invocation output and workspace paths intact.

3. In `EditorCliCommandRunner`, construct the service with `EditorFull`, because commands are authoring operations and must retain access to tool assemblies and tests.

4. In `EditorSession`, construct the service with `EditorFull`, so opening a project and explicit regeneration remain full editor behavior.

5. Add/retain an `EditorFull` test proving a declared `<module-id>.tests` folder still produces exactly one test project that references the declared production module. Retain the orphan-surface failure test in this mode.

6. Run:

   ```powershell
   dotnet test engine/helengine.editor.tests --filter "FullyQualifiedName~EditorCliBuildRunnerTests|FullyQualifiedName~EditorGameSolutionServiceTests"
   ```

7. Commit: `fix(editor): use runtime-only scripts for platform cooks`

## Task 3: Make Module-Graph Validation Explicit and Useful

**Files:**

- Modify: `engine/helengine.editor/managers/project/EditorCodeModuleManifestService.cs`
- Modify: the existing module dependency resolver/validator used by `EditorGeneratedCodeSolutionBuilder`
- Test: `engine/helengine.editor.tests/managers/project/EditorCodeModuleManifestServiceTests.cs`
- Test: `engine/helengine.editor.tests/EditorGameSolutionServiceTests.cs`

1. Write failing tests for the three graph errors that must be stable:

   - duplicate declared module id reports both manifest paths;
   - a module dependency whose id is not declared reports owner id and missing id;
   - a runtime module depending on an editor module reports both ids and the forbidden direction.

2. Keep manifest parsing strict: valid `kind` values are `runtime` and `editor`; reject any other value with manifest path and property name. Do not reintroduce synthetic default module manifests for folders that own C# source.

3. Validate module kinds and dependencies before project emission. `EditorFull` validates the complete declared graph; `RuntimeOnly` validates the selected runtime graph and explicitly rejects an editor dependency from a selected module.

4. Leave `EditorGeneratedCodeTestProjectDiscoveryService` exact-match behavior unchanged. Its test-folder scan is now an `EditorFull` concern only.

5. Run:

   ```powershell
   dotnet test engine/helengine.editor.tests --filter "FullyQualifiedName~EditorCodeModuleManifestServiceTests|FullyQualifiedName~EditorGameSolutionServiceTests"
   ```

6. Commit: `fix(editor): validate declared module dependency kinds`

## Task 4: Declare Demo Disc’s Real Runtime and Editor Modules

**Files:**

- Create: `C:\dev\helprojs\demodisc\assets\codebase\code.module.json`
- Create: `C:\dev\helprojs\demodisc\assets\codebase\game\code.module.json`
- Create: `C:\dev\helprojs\demodisc\assets\codebase\menu\code.module.json`
- Create: `C:\dev\helprojs\demodisc\assets\codebase\rendering\code.module.json`
- Create: `C:\dev\helprojs\demodisc\assets\codebase\game.tools\code.module.json`
- Create: `C:\dev\helprojs\demodisc\assets\codebase\menu.tools\code.module.json`
- Create: `C:\dev\helprojs\demodisc\assets\codebase\physics.tools\code.module.json`
- Create: `C:\dev\helprojs\demodisc\assets\codebase\rendering.tools\code.module.json`
- Create: `C:\dev\helprojs\demodisc\assets\codebase\scene.tools\code.module.json`
- Test: the editor solution integration tests with a copied Demo Disc module layout

1. Before writing dependencies, inspect namespace/usings and compile references in each folder. Declare only actual dependencies—do not guess or add broad references merely to make compilation pass.

2. Create the root composition manifest:

   ```json
   {
     "id": "gameplay",
     "kind": "runtime",
     "dependencies": ["game", "menu", "rendering"]
   }
   ```

   The root owns loose shared runtime code and gives `gameplay.tests` its exact production binding.

3. Declare `game`, `menu`, and `rendering` as `runtime`; declare `game.tools`, `menu.tools`, `physics.tools`, `rendering.tools`, and `scene.tools` as `editor`. Use the smallest reference graph discovered in step 1. Editor modules may depend on runtime modules; no runtime manifest may list a `.tools` module.

4. Do not add a manifest for `diagnostics.tools` unless it contains compiled C# source at implementation time. An empty directory is not a build surface.

5. Add a focused integration test that points manifest discovery at this layout and asserts the expected nine production module ids, kinds, and no runtime-to-editor edges.

6. Run a clean `EditorFull` script generation against Demo Disc. Confirm `game.tools.tests`, `menu.tools.tests`, `rendering.tools.tests`, and `gameplay.tests` bind to their exact declared production module ids.

7. Commit in the Demo Disc repository only: `build: declare Demo Disc code modules`

## Task 5: Make Editor Prebuild Commands Profile-Declared

**Files:**

- Create: `engine/helengine.editor/definitions/build/EditorBuildPrebuildProfileDefinition.cs`
- Create: `engine/helengine.editor/managers/build/EditorBuildPrebuildProfileService.cs`
- Modify: the existing project build-platform configuration document and serializer (currently `EditorBuildPlatformConfigDocument.cs` and its persistence service)
- Modify: editor CLI option parsing/routing (`Program.cs` and the existing CLI command host)
- Test: `engine/helengine.editor.tests/...BuildPrebuildProfile...Tests.cs` (new)

1. Add a project-authored configuration surface keyed by the selected platform build profile. It must contain only ordered command ids, for example:

   ```json
   {
     "profiles": {
       "demo-disc-full": [
         "menu.generate-game-scenes",
         "menu.regenerate-demo-disc-main-menu",
         "menu.attach-tilt-trial-presentation-blueprints"
       ],
       "colored-cube-grid": []
     }
   }
   ```

   Keep it in project settings rather than `PlatformBuildProfileDefinition`: platform definitions remain generic engine data and must not acquire Demo Disc command ids.

2. Write failing service tests for profile lookup:

   - missing profile returns an empty ordered sequence;
   - declared commands preserve authored order;
   - malformed configuration reports the file path and invalid profile key;
   - unknown command id fails with the profile id and command id.

3. Add a CLI operation dedicated to build preparation (for example `--run-build-prebuild <platform> <profile>`). It creates an `EditorFull` editor service in the invocation-specific workspace, resolves the selected profile commands, and runs them through the normal command registry.

4. A command must not be silently skipped. If a profile names a command that is absent after `EditorFull` load, fail before cook/package with a direct message naming both ids.

5. Keep the normal `--build` CLI operation `RuntimeOnly`; prebuild and cook/package are separate phases with separate script modes.

6. Add an integration-oriented test with a fake command registry proving a no-command profile performs no editor-command load and a declared profile executes commands in order.

7. Run:

   ```powershell
   dotnet test engine/helengine.editor.tests --filter FullyQualifiedName~BuildPrebuildProfile
   ```

8. Commit: `feat(editor): declare build precommands by profile`

## Task 6: Remove Demo Disc Knowledge from the Generic Platform Wrapper

**Files:**

- Modify: `scripts/build-platform.ps1`
- Modify: `scripts/tests/build-platform-streaming.tests.ps1`
- Modify: any focused PowerShell fixture/configuration used by the wrapper tests

1. Add a failing Pester test that reads/exercises the wrapper and proves it contains no literal Demo Disc command ids (`menu.generate-game-scenes`, `menu.regenerate-demo-disc-main-menu`, or `menu.attach-tilt-trial-presentation-blueprints`).

2. Replace the unconditional three-command block with one generic build-preparation invocation. Pass the isolated project path, target platform, selected build profile, invocation workspace, and isolated generated-code output path. It must be safe for an empty profile and must preserve existing streaming/log forwarding.

3. Run the final `--build` only after the generic preparation succeeds. The build command remains the runtime-only path from Task 2.

4. Ensure the wrapper creates no shared temp paths and does not reuse a prior invocation’s generated output. Keep current unique invocation id behavior.

5. Add Pester coverage for:

   - empty prebuild profile invokes no project command and proceeds to final build;
   - declared prebuild commands run before final build;
   - a failed prebuild blocks cook/package and retains the invocation log/output for diagnosis;
   - two platform invocations receive distinct project/generated/editor-publish/output directories.

6. Run:

   ```powershell
   Invoke-Pester scripts/tests/build-platform-streaming.tests.ps1 -Output Detailed
   ```

7. Commit: `fix(build): run only profile-declared editor precommands`

## Task 7: Configure and Validate the Two Demo Disc Profiles

**Files:**

- Modify/Create: Demo Disc’s persisted build-prebuild profile settings file from Task 5
- Modify only the relevant existing platform build-profile selection/configuration files if required
- Test: engine integration test plus a clean build command

1. Configure the full Demo Disc profile to declare, in order:

   1. `menu.generate-game-scenes`
   2. `menu.regenerate-demo-disc-main-menu`
   3. `menu.attach-tilt-trial-presentation-blueprints`

2. Configure `colored-cube-grid` to contain no editor prebuild commands. Do not alter user-authored scene selection or cook settings while making this change.

3. From a clean invocation directory, run the minimal PS2 colored-cubes build while all test folders remain present. Confirm the log shows `RuntimeOnly` script generation and no command loading/test discovery.

4. Run the full Demo Disc PS2 build. Confirm its three declared preparation commands run in order under `EditorFull`, then the final cook/package begins under `RuntimeOnly`.

5. Run one independent second-platform build (PS Vita) concurrently with PS2. Verify the two logs report unique invocation roots and neither references the other build’s generated-code/editor-publish/output directories.

6. Run the smallest relevant native/editor tests introduced in Tasks 1–6, then the two smoke builds. Do not claim success from a stale ISO; verify the resulting artifact’s invocation id/timestamp is newer than the build start.

7. Commit Demo Disc config separately: `build: declare Demo Disc build precommands`

## Task 8: Documentation and Handoff

**Files:**

- Modify: `C:\dev\helworks\helengine\README.md` or the existing build documentation page that owns platform build workflow
- Modify: `C:\dev\helprojs\demodisc\README.md` or existing project build documentation

1. Document the two modes in one short table: editor open/regenerate/commands use `EditorFull`; platform cook/package uses `RuntimeOnly`.

2. Document `code.module.json` requirements, permitted dependency direction, exact sibling test-module naming, and that empty directories do not need manifests.

3. Document how a project adds ordered prebuild commands to a build profile and that generic engine scripts never hard-code project command ids.

4. Document the isolation invariant: parallel builds use separate invocation workspaces and do not share generated output.

5. Commit documentation separately: `docs: explain module graph and build precommands`

## Final Verification Checklist

1. `EditorFull` builds declared runtime/editor modules and exact sibling tests; it fails for an orphan test surface.
2. `RuntimeOnly` succeeds with the same orphan test surface present and emits only runtime projects.
3. A clean isolated colored-cubes PS2 build succeeds without any Demo Disc precommand or test assembly load.
4. A full Demo Disc build executes only its explicitly configured commands, in order, then performs its runtime-only cook/package.
5. A concurrent PS2 + PS Vita run has distinct invocation roots, generated-code roots, editor publish paths, and artifacts.
6. `git diff --check` passes in each touched repository; stage and commit only files owned by this change, preserving other agents’ work.

