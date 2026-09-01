# Generated Editor Assimp Reference Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make clean generated editor-module test hosts load `AssimpNetter.dll` by emitting the dependency beside the existing `helengine.editor.dll` reference.

**Architecture:** Fix the generated project source in `EditorGameSolutionService`; never patch generated `.csproj` output. Editor-kind modules will receive an explicit `AssimpNetter` assembly reference resolved from the same deployed editor assembly directory, while runtime-only modules remain unchanged. Validate the result by regenerating ignored DemoDisc scaffolding and running the four model-generation tests plus the full suite.

**Tech Stack:** C# 13, MSBuild project generation, xUnit, .NET 9, HelEngine editor command host

**Spec:** `C:\dev\helprojs\demodisc\.worktrees\software-path-tracer-core\docs\superpowers\plans\2026-09-01-demodisc-game-tools-baseline-repair.md`

## Global Constraints

- Work in `C:\dev\helprojs\.worktrees\helengine-software-path-tracer-engine-seams` for the engine change and the existing DemoDisc feature worktree only for validation.
- Preserve the unrelated dirty `engine/helengine.core/scene/runtime/AutomaticScriptComponentRuntimeDeserializer.cs` file.
- Never hand-edit generated DemoDisc project files; regenerate them through the editor host.
- Emit the dependency only for `EditorCodeModuleKind.Editor` projects.
- Resolve `AssimpNetter.dll` from the deployed directory containing `helengine.editor.dll`; throw if that required distribution file is missing.
- Do not add a DemoDisc `PackageReference`, copy command, runtime resolver, catch/fallback, or skipped test.
- If `codegen.exe` displays a MessageBox, terminate only the process launched by the current command and stop without retrying.

---

### Task 1: Emit AssimpNetter for generated editor modules

**Files:**
- Modify: `engine/helengine.editor.tests/EditorGameSolutionServiceTests.cs`
- Modify: `engine/helengine.editor/managers/project/EditorGameSolutionService.cs`

**Interfaces:**
- Consumes: `typeof(EditorGameSolutionService).Assembly.Location` and the deployed sibling `AssimpNetter.dll`.
- Produces: one `<Reference Include="AssimpNetter">` with an absolute escaped `<HintPath>` in every generated editor-kind project.

- [ ] **Step 1: Add a failing generator regression assertion**

Extend `GenerateSolutionFiles_WhenEditorModuleExists_WritesEditorProjectWithGenericTargetFrameworkAndEditorReference` after its existing editor-reference assertion:

```csharp
string editorAssemblyDirectoryPath = Path.GetDirectoryName(typeof(EditorGameSolutionService).Assembly.Location);
string assimpNetterAssemblyPath = Path.Combine(editorAssemblyDirectoryPath, "AssimpNetter.dll");
Assert.Contains("<Reference Include=\"AssimpNetter\">", projectFileContents, StringComparison.Ordinal);
Assert.Contains("<HintPath>" + EscapeXml(assimpNetterAssemblyPath) + "</HintPath>", projectFileContents, StringComparison.Ordinal);
```

- [ ] **Step 2: Run RED**

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorGameSolutionServiceTests.GenerateSolutionFiles_WhenEditorModuleExists_WritesEditorProjectWithGenericTargetFrameworkAndEditorReference" -v:minimal
```

Expected: the test fails because the generated editor project contains no `AssimpNetter` reference.

- [ ] **Step 3: Emit the required deployed dependency**

Inside the existing `if (moduleProject.ModuleKind == EditorCodeModuleKind.Editor)` block in `AppendAssemblyReferences`, retain the current editor reference and add:

```csharp
string editorAssemblyDirectoryPath = Path.GetDirectoryName(typeof(EditorGameSolutionService).Assembly.Location);
if (string.IsNullOrWhiteSpace(editorAssemblyDirectoryPath)) {
    throw new InvalidOperationException("The deployed HelEngine editor assembly directory could not be resolved.");
}
string assimpNetterAssemblyPath = Path.Combine(editorAssemblyDirectoryPath, "AssimpNetter.dll");
if (!File.Exists(assimpNetterAssemblyPath)) {
    throw new FileNotFoundException("The deployed AssimpNetter editor dependency was not found.", assimpNetterAssemblyPath);
}
builder.AppendLine("    <Reference Include=\"AssimpNetter\">");
builder.AppendLine("      <HintPath>" + EscapeXml(assimpNetterAssemblyPath) + "</HintPath>");
builder.AppendLine("    </Reference>");
```

Do not load an Assimp type in the generator and do not emit this reference for runtime-kind modules.

- [ ] **Step 4: Run GREEN and the focused generator class**

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorGameSolutionServiceTests.GenerateSolutionFiles_WhenEditorModuleExists_WritesEditorProjectWithGenericTargetFrameworkAndEditorReference" -v:minimal
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorGameSolutionServiceTests" -v:minimal
```

Expected: the focused test and full generator test class pass.

- [ ] **Step 5: Commit exact files**

```powershell
rtk git diff --check -- engine/helengine.editor.tests/EditorGameSolutionServiceTests.cs engine/helengine.editor/managers/project/EditorGameSolutionService.cs
rtk git add -- engine/helengine.editor.tests/EditorGameSolutionServiceTests.cs engine/helengine.editor/managers/project/EditorGameSolutionService.cs
rtk git diff --cached --check
rtk git commit -m "Reference Assimp in generated editor projects"
```

Expected: the unrelated dirty runtime deserializer is not staged.

---

### Task 2: Regenerate scaffolding and prove the clean DemoDisc gate

**Files:**
- Verify ignored generated scaffolding only; no tracked file should change.

**Interfaces:**
- Consumes: Task 1's generated reference, the existing engine editor host, and DemoDisc `HEAD`.
- Produces: a generated `game.tools.tests.csproj` with the Assimp reference and green model-generation/full-suite evidence.

- [ ] **Step 1: Build the updated editor host**

```powershell
rtk dotnet build helengine.ui/helengine.editor.app/helengine.editor.app.csproj --no-restore -p:UseSharedCompilation=false -v:minimal
```

Expected: build exits `0` and deploys `helengine.editor.dll` beside `AssimpNetter.dll`.

- [ ] **Step 2: Regenerate through the editor host**

First verify the six presentation assets in the DemoDisc worktree are clean. Then run:

```powershell
rtk dotnet run --no-build --project C:\dev\helprojs\.worktrees\helengine-software-path-tracer-engine-seams\helengine.ui\helengine.editor.app\helengine.editor.app.csproj -- --project C:\dev\helprojs\demodisc\.worktrees\software-path-tracer-core\project.heproj --editor-command menu.attach-tilt-trial-presentation-blueprints
```

Expected: exit `0`; the idempotent transaction leaves tracked presentation assets byte-identical and refreshes ignored generated projects.

- [ ] **Step 3: Prove the generated reference instead of patching it**

```powershell
rtk rg -n -F '<Reference Include="AssimpNetter">' C:\dev\helprojs\demodisc\.worktrees\software-path-tracer-core\user_settings\generated_code\editor-command\EditorFull\projects\game.tools\game.tools.csproj C:\dev\helprojs\demodisc\.worktrees\software-path-tracer-core\user_settings\generated_code\editor-command\EditorFull\projects\game.tools.tests\game.tools.tests.csproj
```

Expected: one match in each generated editor project. Do not edit either file.

- [ ] **Step 4: Run the four previously failing tests and the complete game-tools suite**

From the DemoDisc feature worktree:

```powershell
rtk dotnet test user_settings/generated_code/editor-command/EditorFull/projects/game.tools.tests/game.tools.tests.csproj --no-restore --filter "FullyQualifiedName~EditorGenerationCommandTransactionTests|FullyQualifiedName~SplitPlayGoalFlagAssetGenerationTests.Generate_writes_goal_flag_models_materials_and_blueprint_with_ds_model_override|FullyQualifiedName~SplitPlayGoldenCoinAssetGenerationTests.Generate_writes_coin_models_material_and_blueprint_with_ds_model_override" -p:UseSharedCompilation=false -v:minimal
rtk dotnet test user_settings/generated_code/editor-command/EditorFull/projects/game.tools.tests/game.tools.tests.csproj --no-restore --no-build -v:minimal
```

Expected: four focused tests pass, then all 100 game-tools tests pass.

- [ ] **Step 5: Re-run the remaining full projects and tracer matrix**

Use the existing clean snapshot root `C:\dev\helprojs\_demodisc-final-79db9a2b90a1` through `HELENGINE_TEST_PROJECT_ROOT`; run gameplay, menu, and rendering full projects with `--no-build`, then the four filtered tracer commands. Expected counts remain 245 gameplay, 35 menu, 93 rendering, and tracer minimums 180/17/4/2.

- [ ] **Step 6: Audit both repositories**

```powershell
git diff --check main..HEAD
rtk git status --short
Get-Process codegen -ErrorAction SilentlyContinue
```

Run the Git commands once in each feature worktree. Expected: no whitespace errors, no new tracked DemoDisc changes, only the two committed engine source/test files in Task 1, all unrelated dirt preserved, and no `codegen.exe` process.

---

### Task 3: Clear the inherited engine-plan whitespace audit

**Files:**
- Modify: `docs/superpowers/plans/2026-09-01-native-font-processor-lifetime.md`

**Interfaces:**
- Consumes: the final `main..HEAD` engine whitespace audit from Task 2.
- Produces: the same native-font plan content with one normal terminal newline and no blank line at EOF.

- [ ] **Step 1: Reproduce the single audit failure**

```powershell
git diff --check main..HEAD
```

Expected: one `new blank line at EOF` error at line 68 of the listed plan.

- [ ] **Step 2: Remove only the extra final empty line**

Use `apply_patch` to delete the final blank line. Retain the newline terminating the last content line and do not reflow any text.

- [ ] **Step 3: Verify and commit**

```powershell
git diff --check main..HEAD
rtk git diff --numstat -- docs/superpowers/plans/2026-09-01-native-font-processor-lifetime.md
rtk git add -- docs/superpowers/plans/2026-09-01-native-font-processor-lifetime.md
rtk git diff --cached --check
rtk git commit -m "Repair native font plan whitespace"
```

Expected: the whitespace audit exits `0`; the task diff contains exactly one deleted blank line and no other file.
