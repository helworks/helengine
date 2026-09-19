# Platform Override Tree: Model, Format, Resolution and Build — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the fixed platform→environment override scope with a typed path (Group chain → Platform → Build Config, in a per-entity order), store platform groups as project settings, resolve overrides by deepest authored prefix in both the editor services and the build packager, and bump the scene formats so the packager can prune a PS1 build by group membership.

**Architecture:** A step kind enum and step record are added to `helengine.core` so the runtime reader, the file format and the editor share one path representation. `EditorOverrideScope` becomes an immutable step list with `Parent`/`IsPrefixOf`; every editing service that layered "platform then environment" becomes a recursion over `Parent`. A new `EditorProjectPlatformGroupsService` owns `settings/platform-groups.json`, and `EditorOverrideScopeResolver` turns (level order, platform, environment) into a target path and picks the deepest authored prefix. The packager and the viewport suppression both use the resolver. No UI changes: the existing two-row tab strip keeps working because `new EditorOverrideScope(platformId, environmentId)` still builds the default-order path.

**Tech Stack:** C# / .NET 9, xUnit, System.Text.Json, the engine's `EngineBinaryWriter`/`EngineBinaryReader`. `helengine.core` is transpiled to C++ for consoles: no LINQ, no tuples, no local functions in core changes.

**Spec:** `docs/superpowers/specs/2026-09-18-platform-override-tree-design.md`

## Global Constraints

- Work on `main` in `C:/dev/helworks/helengine`. No worktrees.
- One class per file. Substantive XML doc comments on every public member. No tuples. Nullable disabled. PascalCase fields. Braces on the same line. No local functions. Never edit generated code.
- `CurrentFormatOnlySourceContractTests` scans every `.cs` under `engine/`, `helengine.ui/`, `tools/`, `scripts/`. Production source (including comments and identifiers) must not contain the words `legacy`, `migrate`, `migration`, `upgrade`, `backward compatibility`, `compatibility path|fallback|alias|overload`, nor version comparisons of the form `version >= N`. Readers compare a version with `!=` only. Say "relocate" for reordering, "regenerate" for old data.
- Format versions move exactly once, in Task 3: `SceneEntityPayloadFormat.SceneEntityPayloadVersion` 8→9, `PackagedAssetBinarySerializer.SceneEntityPayloadVersion` 8→9, `PackagedAssetBinarySerializer.CurrentVersion` 24→25, `EditorAssetBinarySerializer.CurrentVersion` 24→25, `ComponentPlatformOverridePayloadService.WrappedPayloadVersion` 4→5.
- Common is the empty path. An existence override may sit on Common. Transform and component overrides never sit on Common (Common is the base data).
- Group id namespace is shared with platform ids: a group id may not equal a platform id or another group id (case-insensitive). A platform belongs to at most one group.
- Default level order: Platform → BuildConfig, recorded as `defaultLevelOrder` in `settings/platform-groups.json`.
- Run tests with PowerShell, full path: `& "C:\Program Files\dotnet\dotnet.exe" test <csproj> --filter "FullyQualifiedName~<Name>" --nologo -v q`. Never use the Bash tool for `dotnet` (the `rtk` shim breaks it).
- Every commit ends with `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`.
- Test projects: `engine/helengine.core.tests`, `engine/helengine.files.tests`, `engine/helengine.editor.tests` (has `InternalsVisibleTo`).

## File Structure

Created:
- `engine/helengine.core/assets/raw/scene/SceneOverrideScopeStepKind.cs` — enum Group/Platform/BuildConfig.
- `engine/helengine.core/assets/raw/scene/SceneOverrideScopeStepAsset.cs` — one `{Kind, Id}` step record.
- `engine/helengine.core/assets/raw/scene/SceneOverrideScopePath.cs` — static path helpers shared by runtime, format and editor.
- `engine/helengine.editor/model/EditorOverrideScopeStep.cs` — editor step value with case-insensitive equality.
- `engine/helengine.editor/model/EditorOverrideLevelOrder.cs` — default order, validation, path-shape check.
- `engine/helengine.editor/model/EditorOverrideScopeRelocation.cs`, `EditorOverrideScopeRelocationPlan.cs`, `EditorOverrideScopeRelocationPlanner.cs` — reorder planning (logic only; the confirmation UI is the second plan).
- `engine/helengine.editor/managers/project/EditorProjectPlatformGroupDefinition.cs`, `EditorProjectPlatformGroupsDocument.cs`, `EditorProjectPlatformGroupsService.cs` — `settings/platform-groups.json`.
- `engine/helengine.editor/managers/scene/EditorOverrideScopeResolver.cs` — target path + deepest prefix.

Modified (main ones): `EditorOverrideScope.cs`, `EditorOverrideScopeMap.cs`, the three `SceneEntityPlatform*OverrideAsset.cs`, `SceneEntityAsset.cs`, `SceneEntityPayloadFormat.cs`, `PackagedAssetBinarySerializer.cs`, `EditorAssetBinarySerializer.cs`, `EntitySaveComponent.cs`, `EntityComponentSaveState.cs`, `EntityComponentPlatformOverrideState.cs`, `EntityPlatformComponentOverrideState.cs`, `SceneSaveService.cs`, `SceneLoadService.cs`, `ComponentPlatformOverridePayloadService.cs`, `BlueprintPackagedSceneExpansionService.cs`, `EntityPlatformExistenceEditingService.cs`, `EntityPlatformTransformEditingService.cs`, `ComponentPlatformEditingService.cs`, `AnimationClipPlatformResolutionService.cs`, `AssetProcessorSettingsScopeResolver.cs`, `ComponentPropertyEditRequest.cs`, `ComponentPropertyEditController.cs`, `SceneAssetReferenceValidationService.cs`, `EditorWindowsBuildScenePackager.cs`, `EditorPlatformExistenceViewportSyncService.cs`, `EditorSession.cs`, `PlatformSceneAuthoringHelperService.cs`.

---

### Task 1: Core step model and path helpers

**Files:**
- Create: `engine/helengine.core/assets/raw/scene/SceneOverrideScopeStepKind.cs`
- Create: `engine/helengine.core/assets/raw/scene/SceneOverrideScopeStepAsset.cs`
- Create: `engine/helengine.core/assets/raw/scene/SceneOverrideScopePath.cs`
- Test: `engine/helengine.core.tests/SceneOverrideScopePathTests.cs`

**Interfaces:**
- Produces: `enum SceneOverrideScopeStepKind : byte { Group = 1, Platform = 2, BuildConfig = 3 }`; `class SceneOverrideScopeStepAsset { SceneOverrideScopeStepKind Kind; string Id }`; `static class SceneOverrideScopePath { SceneOverrideScopeStepAsset[] Common(); Platform(string); PlatformBuildConfig(string, string); Group(string); Normalize(SceneOverrideScopeStepAsset[]); string Format(SceneOverrideScopeStepAsset[]); bool AreEqual(a, b); const string CommonLabel = "common"; }`.

- [ ] **Step 1: Write the failing test**

```csharp
using Xunit;

namespace helengine.core.tests {
    /// <summary>
    /// Verifies the shared override scope path helpers used by the runtime reader, the file format and the editor.
    /// </summary>
    public sealed class SceneOverrideScopePathTests {
        [Fact]
        public void Platform_BuildsOnePlatformStep() {
            SceneOverrideScopeStepAsset[] steps = SceneOverrideScopePath.Platform("ps1");

            SceneOverrideScopeStepAsset step = Assert.Single(steps);
            Assert.Equal(SceneOverrideScopeStepKind.Platform, step.Kind);
            Assert.Equal("ps1", step.Id);
        }

        [Fact]
        public void PlatformBuildConfig_WithBlankEnvironment_OmitsTheBuildConfigStep() {
            Assert.Single(SceneOverrideScopePath.PlatformBuildConfig("ps1", " "));
            Assert.Equal(2, SceneOverrideScopePath.PlatformBuildConfig("ps1", "debug").Length);
        }

        [Fact]
        public void Format_WritesKindAndIdPerStepAndCommonForTheEmptyPath() {
            Assert.Equal("common", SceneOverrideScopePath.Format(SceneOverrideScopePath.Common()));
            Assert.Equal("common", SceneOverrideScopePath.Format(null));
            Assert.Equal("platform:ps1/buildconfig:debug", SceneOverrideScopePath.Format(SceneOverrideScopePath.PlatformBuildConfig("ps1", "debug")));
            Assert.Equal("group:handheld/platform:ds", SceneOverrideScopePath.Format(new[] {
                new SceneOverrideScopeStepAsset { Kind = SceneOverrideScopeStepKind.Group, Id = "handheld" },
                new SceneOverrideScopeStepAsset { Kind = SceneOverrideScopeStepKind.Platform, Id = "ds" }
            }));
        }

        [Fact]
        public void AreEqual_IgnoresIdCaseAndSurroundingWhitespace() {
            SceneOverrideScopeStepAsset[] left = SceneOverrideScopePath.PlatformBuildConfig("PS1", "Debug");
            SceneOverrideScopeStepAsset[] right = new[] {
                new SceneOverrideScopeStepAsset { Kind = SceneOverrideScopeStepKind.Platform, Id = " ps1 " },
                new SceneOverrideScopeStepAsset { Kind = SceneOverrideScopeStepKind.BuildConfig, Id = "debug" }
            };

            Assert.True(SceneOverrideScopePath.AreEqual(left, right));
            Assert.False(SceneOverrideScopePath.AreEqual(left, SceneOverrideScopePath.Platform("ps1")));
            Assert.True(SceneOverrideScopePath.AreEqual(null, SceneOverrideScopePath.Common()));
        }

        [Fact]
        public void Normalize_TrimsIdsDropsNullStepsAndRejectsBlankIds() {
            SceneOverrideScopeStepAsset[] normalized = SceneOverrideScopePath.Normalize(new[] {
                null,
                new SceneOverrideScopeStepAsset { Kind = SceneOverrideScopeStepKind.Platform, Id = " ps1 " }
            });

            Assert.Equal("ps1", Assert.Single(normalized).Id);
            Assert.Empty(SceneOverrideScopePath.Normalize(null));
            Assert.Throws<InvalidOperationException>(() => SceneOverrideScopePath.Normalize(new[] {
                new SceneOverrideScopeStepAsset { Kind = SceneOverrideScopeStepKind.Group, Id = "" }
            }));
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.core.tests/helengine.core.tests.csproj --filter "FullyQualifiedName~SceneOverrideScopePathTests" --nologo -v q`
Expected: build error `The type or namespace name 'SceneOverrideScopePath' could not be found`.

- [ ] **Step 3: Write the three core files**

`SceneOverrideScopeStepKind.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// Kind of one step on an override scope path. Every kind appears at most once as a level in an entity's
    /// level order; the Group level is a chain of nested group steps.
    /// </summary>
    public enum SceneOverrideScopeStepKind : byte {
        /// <summary>One platform group from <c>settings/platform-groups.json</c>.</summary>
        Group = 1,
        /// <summary>One project platform id.</summary>
        Platform = 2,
        /// <summary>One project environment id (debug, release, ...).</summary>
        BuildConfig = 3
    }
}
```

`SceneOverrideScopeStepAsset.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// One step of a serialized override scope path: the level kind and the node id at that level.
    /// </summary>
    public class SceneOverrideScopeStepAsset {
        /// <summary>
        /// Gets or sets the level kind this step belongs to.
        /// </summary>
        public SceneOverrideScopeStepKind Kind { get; set; }

        /// <summary>
        /// Gets or sets the group, platform or environment id at this step.
        /// </summary>
        public string Id { get; set; } = string.Empty;
    }
}
```

`SceneOverrideScopePath.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// Builds, normalizes, compares and formats override scope paths. The empty path is Common. Shared by the
    /// runtime reader, the editor file format and the editor so every layer agrees on path identity.
    /// </summary>
    public static class SceneOverrideScopePath {
        /// <summary>
        /// Label used when the empty Common path is formatted.
        /// </summary>
        public const string CommonLabel = "common";

        /// <summary>
        /// Returns the empty Common path.
        /// </summary>
        public static SceneOverrideScopeStepAsset[] Common() {
            return Array.Empty<SceneOverrideScopeStepAsset>();
        }

        /// <summary>
        /// Builds a one-step path for a platform.
        /// </summary>
        public static SceneOverrideScopeStepAsset[] Platform(string platformId) {
            return new[] { CreateStep(SceneOverrideScopeStepKind.Platform, platformId) };
        }

        /// <summary>
        /// Builds a one-step path for a group.
        /// </summary>
        public static SceneOverrideScopeStepAsset[] Group(string groupId) {
            return new[] { CreateStep(SceneOverrideScopeStepKind.Group, groupId) };
        }

        /// <summary>
        /// Builds a platform path with an optional build-config step beneath it; a blank environment yields the platform-only path.
        /// </summary>
        public static SceneOverrideScopeStepAsset[] PlatformBuildConfig(string platformId, string environmentId) {
            if (string.IsNullOrWhiteSpace(environmentId)) {
                return Platform(platformId);
            }

            return new[] {
                CreateStep(SceneOverrideScopeStepKind.Platform, platformId),
                CreateStep(SceneOverrideScopeStepKind.BuildConfig, environmentId)
            };
        }

        /// <summary>
        /// Returns a copy with null steps removed and ids trimmed; a step with a blank id is a data error.
        /// </summary>
        public static SceneOverrideScopeStepAsset[] Normalize(SceneOverrideScopeStepAsset[] steps) {
            if (steps == null) {
                return Common();
            }

            int count = 0;
            for (int index = 0; index < steps.Length; index++) {
                if (steps[index] != null) {
                    count++;
                }
            }

            SceneOverrideScopeStepAsset[] normalized = new SceneOverrideScopeStepAsset[count];
            int write = 0;
            for (int index = 0; index < steps.Length; index++) {
                SceneOverrideScopeStepAsset step = steps[index];
                if (step == null) {
                    continue;
                }
                if (string.IsNullOrWhiteSpace(step.Id)) {
                    throw new InvalidOperationException("Override scope steps must define a non-blank id.");
                }

                normalized[write] = CreateStep(step.Kind, step.Id);
                write++;
            }

            return normalized;
        }

        /// <summary>
        /// Formats a path as <c>kind:id/kind:id</c>, or <see cref="CommonLabel"/> for the empty path.
        /// </summary>
        public static string Format(SceneOverrideScopeStepAsset[] steps) {
            if (steps == null || steps.Length == 0) {
                return CommonLabel;
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int index = 0; index < steps.Length; index++) {
                SceneOverrideScopeStepAsset step = steps[index];
                if (step == null) {
                    continue;
                }
                if (builder.Length > 0) {
                    builder.Append('/');
                }

                builder.Append(FormatKind(step.Kind));
                builder.Append(':');
                builder.Append((step.Id ?? string.Empty).Trim());
            }

            return builder.Length == 0 ? CommonLabel : builder.ToString();
        }

        /// <summary>
        /// Compares two paths step by step, ignoring id case and surrounding whitespace. Null equals the empty path.
        /// </summary>
        public static bool AreEqual(SceneOverrideScopeStepAsset[] left, SceneOverrideScopeStepAsset[] right) {
            SceneOverrideScopeStepAsset[] normalizedLeft = Normalize(left);
            SceneOverrideScopeStepAsset[] normalizedRight = Normalize(right);
            if (normalizedLeft.Length != normalizedRight.Length) {
                return false;
            }

            for (int index = 0; index < normalizedLeft.Length; index++) {
                if (normalizedLeft[index].Kind != normalizedRight[index].Kind
                    || !string.Equals(normalizedLeft[index].Id, normalizedRight[index].Id, StringComparison.OrdinalIgnoreCase)) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Lower-case kind label used by <see cref="Format"/>.
        /// </summary>
        public static string FormatKind(SceneOverrideScopeStepKind kind) {
            if (kind == SceneOverrideScopeStepKind.Group) {
                return "group";
            }
            if (kind == SceneOverrideScopeStepKind.Platform) {
                return "platform";
            }

            return "buildconfig";
        }

        /// <summary>
        /// Creates one trimmed step; blank ids are a programming error.
        /// </summary>
        static SceneOverrideScopeStepAsset CreateStep(SceneOverrideScopeStepKind kind, string id) {
            if (string.IsNullOrWhiteSpace(id)) {
                throw new ArgumentException("Override scope step id must be provided.", nameof(id));
            }

            return new SceneOverrideScopeStepAsset {
                Kind = kind,
                Id = id.Trim()
            };
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Same command as Step 2. Expected: 5 passed.

- [ ] **Step 5: Commit**

```
git add engine/helengine.core/assets/raw/scene/SceneOverrideScopeStepKind.cs engine/helengine.core/assets/raw/scene/SceneOverrideScopeStepAsset.cs engine/helengine.core/assets/raw/scene/SceneOverrideScopePath.cs engine/helengine.core.tests/SceneOverrideScopePathTests.cs
git commit -m "core: add the override scope step model and path helpers"
```

---

### Task 2: Generalize `EditorOverrideScope` to a step path

Keeps every existing caller compiling: the `(platformId, environmentId)` constructor stays as the default-order builder, and `PlatformId`, `EnvironmentId`, `IsPlatformOnly` stay as transitional derived members that Task 7 deletes.

**Files:**
- Create: `engine/helengine.editor/model/EditorOverrideScopeStep.cs`
- Modify: `engine/helengine.editor/model/EditorOverrideScope.cs` (rewrite)
- Modify: `engine/helengine.editor/model/EditorOverrideScopeMap.cs` (rewrite)
- Test: `engine/helengine.editor.tests/EditorOverrideScopeTests.cs`

**Interfaces:**
- Produces: `readonly struct EditorOverrideScopeStep(SceneOverrideScopeStepKind kind, string id) { Kind; Id; ToAsset() }`.
- Produces: `readonly struct EditorOverrideScope : IEquatable<EditorOverrideScope>` with `const string CommonPlatformId = "common"`, `static Common`, `IReadOnlyList<EditorOverrideScopeStep> Steps`, `int Depth`, `bool IsCommon`, `EditorOverrideScope Parent`, `EditorOverrideScope Append(EditorOverrideScopeStep)`, `bool IsPrefixOf(EditorOverrideScope other)`, `bool TryGetStepId(SceneOverrideScopeStepKind, out string)`, `bool HasStepKind(SceneOverrideScopeStepKind)`, `static ForPlatform(string)`, `static ForPlatformBuildConfig(string, string)`, `static FromSteps(SceneOverrideScopeStepAsset[])`, `SceneOverrideScopeStepAsset[] ToSteps()`, ctors `(string platformId, string environmentId = null)` and `(IReadOnlyList<EditorOverrideScopeStep>)`, `ToString()` = `SceneOverrideScopePath.Format(ToSteps())`. Transitional: `string PlatformId` (Platform step id; `CommonPlatformId` when `IsCommon`; empty otherwise), `string EnvironmentId` (BuildConfig step id or empty), `bool IsPlatformOnly` (`!HasStepKind(BuildConfig)`).
- Produces: `internal sealed class EditorOverrideScopeMap<T> { Set; GetOrCreate; TryGet; Remove; EnumerateValues; EnumerateScopes; bool TryGetDeepestPrefix(EditorOverrideScope target, out T value, out EditorOverrideScope matched); }`.

- [ ] **Step 1: Write the failing test**

```csharp
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the path-based override scope and its prefix-aware map.
    /// </summary>
    public sealed class EditorOverrideScopeTests {
        [Fact]
        public void PairConstructor_BuildsTheDefaultOrderPath() {
            EditorOverrideScope scope = new EditorOverrideScope("ps1", "debug");

            Assert.Equal(2, scope.Depth);
            Assert.Equal(SceneOverrideScopeStepKind.Platform, scope.Steps[0].Kind);
            Assert.Equal(SceneOverrideScopeStepKind.BuildConfig, scope.Steps[1].Kind);
            Assert.Equal("platform:ps1/buildconfig:debug", scope.ToString());
            Assert.Equal(new EditorOverrideScope("ps1"), scope.Parent);
            Assert.True(scope.Parent.Parent.IsCommon);
        }

        [Fact]
        public void PairConstructor_WithCommonPlatformId_YieldsTheCommonScope() {
            Assert.True(new EditorOverrideScope("common").IsCommon);
            Assert.True(new EditorOverrideScope("Common", "debug").IsCommon);
            Assert.Equal(EditorOverrideScope.Common, new EditorOverrideScope("common"));
            Assert.Equal("common", EditorOverrideScope.Common.ToString());
        }

        [Fact]
        public void Equality_IgnoresIdCase() {
            Assert.Equal(new EditorOverrideScope("PS1", "DEBUG"), new EditorOverrideScope("ps1", "debug"));
            Assert.Equal(new EditorOverrideScope("PS1").GetHashCode(), new EditorOverrideScope("ps1").GetHashCode());
            Assert.NotEqual(EditorOverrideScope.FromSteps(SceneOverrideScopePath.Group("ps1")), new EditorOverrideScope("ps1"));
        }

        [Fact]
        public void IsPrefixOf_AcceptsCommonAndProperPrefixesOnly() {
            EditorOverrideScope target = EditorOverrideScope.Common
                .Append(new EditorOverrideScopeStep(SceneOverrideScopeStepKind.Group, "handheld"))
                .Append(new EditorOverrideScopeStep(SceneOverrideScopeStepKind.Platform, "ds"))
                .Append(new EditorOverrideScopeStep(SceneOverrideScopeStepKind.BuildConfig, "debug"));

            Assert.True(EditorOverrideScope.Common.IsPrefixOf(target));
            Assert.True(EditorOverrideScope.FromSteps(SceneOverrideScopePath.Group("handheld")).IsPrefixOf(target));
            Assert.True(target.IsPrefixOf(target));
            Assert.False(new EditorOverrideScope("ds").IsPrefixOf(target));
            Assert.False(target.IsPrefixOf(target.Parent));
        }

        [Fact]
        public void ToSteps_RoundTripsThroughFromSteps() {
            EditorOverrideScope scope = new EditorOverrideScope("ps1", "release");

            Assert.Equal(scope, EditorOverrideScope.FromSteps(scope.ToSteps()));
            Assert.Empty(EditorOverrideScope.Common.ToSteps());
        }

        [Fact]
        public void TryGetStepId_FindsTheRequestedKind() {
            EditorOverrideScope scope = new EditorOverrideScope("ps1", "release");

            Assert.True(scope.TryGetStepId(SceneOverrideScopeStepKind.BuildConfig, out string environmentId));
            Assert.Equal("release", environmentId);
            Assert.False(scope.TryGetStepId(SceneOverrideScopeStepKind.Group, out _));
        }

        [Fact]
        public void ScopeMap_TryGetDeepestPrefix_PrefersTheLongestAuthoredPrefix() {
            EditorOverrideScopeMap<string> map = new EditorOverrideScopeMap<string>();
            EditorOverrideScope platform = new EditorOverrideScope("ps1");
            EditorOverrideScope config = new EditorOverrideScope("ps1", "debug");
            map.Set(EditorOverrideScope.Common, "common");
            map.Set(platform, "platform");

            Assert.True(map.TryGetDeepestPrefix(config, out string value, out EditorOverrideScope matched));
            Assert.Equal("platform", value);
            Assert.Equal(platform, matched);

            map.Set(config, "config");
            Assert.True(map.TryGetDeepestPrefix(config, out value, out _));
            Assert.Equal("config", value);

            Assert.True(map.TryGetDeepestPrefix(new EditorOverrideScope("n64"), out value, out matched));
            Assert.Equal("common", value);
            Assert.True(matched.IsCommon);

            map.Remove(EditorOverrideScope.Common);
            Assert.False(map.TryGetDeepestPrefix(new EditorOverrideScope("n64"), out _, out _));
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorOverrideScopeTests" --nologo -v q`
Expected: build errors on `Steps`, `Depth`, `IsCommon`, `Append`, `EditorOverrideScopeStep`, `TryGetDeepestPrefix`.

- [ ] **Step 3: Write `EditorOverrideScopeStep.cs`**

```csharp
namespace helengine {
    /// <summary>
    /// One step on an editor override scope path. Ids compare case-insensitively.
    /// </summary>
    public readonly struct EditorOverrideScopeStep : IEquatable<EditorOverrideScopeStep> {
        /// <summary>
        /// Initializes one step with a trimmed id.
        /// </summary>
        public EditorOverrideScopeStep(SceneOverrideScopeStepKind kind, string id) {
            if (string.IsNullOrWhiteSpace(id)) {
                throw new ArgumentException("Override scope step id must be provided.", nameof(id));
            }

            Kind = kind;
            Id = id.Trim();
        }

        /// <summary>Gets the level kind of this step.</summary>
        public SceneOverrideScopeStepKind Kind { get; }

        /// <summary>Gets the node id at this step.</summary>
        public string Id { get; }

        /// <summary>Converts the step to its serialized record.</summary>
        public SceneOverrideScopeStepAsset ToAsset() {
            return new SceneOverrideScopeStepAsset { Kind = Kind, Id = Id };
        }

        /// <inheritdoc />
        public bool Equals(EditorOverrideScopeStep other) {
            return Kind == other.Kind && string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public override bool Equals(object obj) {
            return obj is EditorOverrideScopeStep other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode() {
            return HashCode.Combine((int)Kind, StringComparer.OrdinalIgnoreCase.GetHashCode(Id ?? string.Empty));
        }
    }
}
```

- [ ] **Step 4: Rewrite `EditorOverrideScope.cs`**

```csharp
namespace helengine {
    /// <summary>
    /// Identifies one editor override scope as an ordered path of typed steps beneath Common. Common is the empty
    /// path. Paths are immutable value types and compare step by step with case-insensitive ids.
    /// </summary>
    public readonly struct EditorOverrideScope : IEquatable<EditorOverrideScope> {
        /// <summary>
        /// Platform id the properties panel uses for the shared Common tab. Equal to <c>ComponentPlatformEditingService.CommonPlatformId</c>.
        /// </summary>
        public const string CommonPlatformId = "common";

        /// <summary>
        /// The empty path.
        /// </summary>
        public static readonly EditorOverrideScope Common = new EditorOverrideScope(Array.Empty<EditorOverrideScopeStep>());

        readonly EditorOverrideScopeStep[] StepsValue;

        /// <summary>
        /// Builds the default-order path for a platform with an optional build-config beneath it. The common
        /// platform id, blank or otherwise, yields <see cref="Common"/>.
        /// </summary>
        public EditorOverrideScope(string platformId, string environmentId = null) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            if (string.Equals(platformId.Trim(), CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
                StepsValue = Array.Empty<EditorOverrideScopeStep>();
                return;
            }

            if (string.IsNullOrWhiteSpace(environmentId)) {
                StepsValue = new[] { new EditorOverrideScopeStep(SceneOverrideScopeStepKind.Platform, platformId) };
                return;
            }

            StepsValue = new[] {
                new EditorOverrideScopeStep(SceneOverrideScopeStepKind.Platform, platformId),
                new EditorOverrideScopeStep(SceneOverrideScopeStepKind.BuildConfig, environmentId)
            };
        }

        /// <summary>
        /// Builds a path from explicit steps (copied).
        /// </summary>
        public EditorOverrideScope(IReadOnlyList<EditorOverrideScopeStep> steps) {
            if (steps == null || steps.Count == 0) {
                StepsValue = Array.Empty<EditorOverrideScopeStep>();
                return;
            }

            EditorOverrideScopeStep[] copy = new EditorOverrideScopeStep[steps.Count];
            for (int index = 0; index < steps.Count; index++) {
                copy[index] = steps[index];
            }

            StepsValue = copy;
        }

        /// <summary>Gets the steps from the first level beneath Common to this node.</summary>
        public IReadOnlyList<EditorOverrideScopeStep> Steps => StepsValue ?? Array.Empty<EditorOverrideScopeStep>();

        /// <summary>Gets the number of steps; zero for Common.</summary>
        public int Depth => Steps.Count;

        /// <summary>Gets whether this is the empty Common path.</summary>
        public bool IsCommon => Depth == 0;

        /// <summary>Gets the path one step shorter; Common's parent is Common.</summary>
        public EditorOverrideScope Parent {
            get {
                if (IsCommon) {
                    return Common;
                }

                EditorOverrideScopeStep[] parentSteps = new EditorOverrideScopeStep[Depth - 1];
                for (int index = 0; index < parentSteps.Length; index++) {
                    parentSteps[index] = Steps[index];
                }

                return new EditorOverrideScope(parentSteps);
            }
        }

        /// <summary>Returns a path with one more step.</summary>
        public EditorOverrideScope Append(EditorOverrideScopeStep step) {
            EditorOverrideScopeStep[] steps = new EditorOverrideScopeStep[Depth + 1];
            for (int index = 0; index < Depth; index++) {
                steps[index] = Steps[index];
            }

            steps[Depth] = step;
            return new EditorOverrideScope(steps);
        }

        /// <summary>Returns true when every step of this path matches the leading steps of <paramref name="other"/>.</summary>
        public bool IsPrefixOf(EditorOverrideScope other) {
            if (Depth > other.Depth) {
                return false;
            }

            for (int index = 0; index < Depth; index++) {
                if (!Steps[index].Equals(other.Steps[index])) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Returns the id of the first step of the requested kind.</summary>
        public bool TryGetStepId(SceneOverrideScopeStepKind kind, out string id) {
            for (int index = 0; index < Depth; index++) {
                if (Steps[index].Kind == kind) {
                    id = Steps[index].Id;
                    return true;
                }
            }

            id = string.Empty;
            return false;
        }

        /// <summary>Returns whether any step has the requested kind.</summary>
        public bool HasStepKind(SceneOverrideScopeStepKind kind) {
            return TryGetStepId(kind, out _);
        }

        /// <summary>Builds the one-step platform path, or Common for the common platform id.</summary>
        public static EditorOverrideScope ForPlatform(string platformId) {
            return new EditorOverrideScope(platformId);
        }

        /// <summary>Builds the default-order platform/build-config path.</summary>
        public static EditorOverrideScope ForPlatformBuildConfig(string platformId, string environmentId) {
            return new EditorOverrideScope(platformId, environmentId);
        }

        /// <summary>Builds a path from serialized steps.</summary>
        public static EditorOverrideScope FromSteps(SceneOverrideScopeStepAsset[] steps) {
            SceneOverrideScopeStepAsset[] normalized = SceneOverrideScopePath.Normalize(steps);
            EditorOverrideScopeStep[] editorSteps = new EditorOverrideScopeStep[normalized.Length];
            for (int index = 0; index < normalized.Length; index++) {
                editorSteps[index] = new EditorOverrideScopeStep(normalized[index].Kind, normalized[index].Id);
            }

            return new EditorOverrideScope(editorSteps);
        }

        /// <summary>Converts the path to serialized steps.</summary>
        public SceneOverrideScopeStepAsset[] ToSteps() {
            SceneOverrideScopeStepAsset[] steps = new SceneOverrideScopeStepAsset[Depth];
            for (int index = 0; index < Depth; index++) {
                steps[index] = Steps[index].ToAsset();
            }

            return steps;
        }

        /// <summary>
        /// Transitional: id of the Platform step, <see cref="CommonPlatformId"/> for Common, empty when the path has no platform step. Removed in Task 7.
        /// </summary>
        public string PlatformId {
            get {
                if (IsCommon) {
                    return CommonPlatformId;
                }

                return TryGetStepId(SceneOverrideScopeStepKind.Platform, out string platformId) ? platformId : string.Empty;
            }
        }

        /// <summary>Transitional: id of the BuildConfig step or empty. Removed in Task 7.</summary>
        public string EnvironmentId => TryGetStepId(SceneOverrideScopeStepKind.BuildConfig, out string environmentId) ? environmentId : string.Empty;

        /// <summary>Transitional: true when the path has no BuildConfig step. Removed in Task 7.</summary>
        public bool IsPlatformOnly => !HasStepKind(SceneOverrideScopeStepKind.BuildConfig);

        /// <inheritdoc />
        public bool Equals(EditorOverrideScope other) {
            return IsPrefixOf(other) && Depth == other.Depth;
        }

        /// <inheritdoc />
        public override bool Equals(object obj) {
            return obj is EditorOverrideScope other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode() {
            HashCode hash = new HashCode();
            for (int index = 0; index < Depth; index++) {
                hash.Add(Steps[index]);
            }

            return hash.ToHashCode();
        }

        /// <inheritdoc />
        public override string ToString() {
            return SceneOverrideScopePath.Format(ToSteps());
        }

        /// <summary>Compares two scopes.</summary>
        public static bool operator ==(EditorOverrideScope left, EditorOverrideScope right) {
            return left.Equals(right);
        }

        /// <summary>Compares two scopes.</summary>
        public static bool operator !=(EditorOverrideScope left, EditorOverrideScope right) {
            return !left.Equals(right);
        }
    }
}
```

- [ ] **Step 5: Rewrite `EditorOverrideScopeMap.cs`**

```csharp
namespace helengine {
    /// <summary>
    /// Stores override payloads keyed by scope path and answers deepest-authored-prefix lookups.
    /// </summary>
    /// <typeparam name="T">Override payload type.</typeparam>
    internal sealed class EditorOverrideScopeMap<T> {
        readonly Dictionary<EditorOverrideScope, T> ValuesByScope;

        /// <summary>
        /// Initializes an empty map.
        /// </summary>
        public EditorOverrideScopeMap() {
            ValuesByScope = new Dictionary<EditorOverrideScope, T>();
        }

        /// <summary>Stores one payload at the supplied scope.</summary>
        public void Set(EditorOverrideScope scope, T value) {
            ValuesByScope[scope] = value;
        }

        /// <summary>Gets or creates one payload at the supplied scope.</summary>
        public T GetOrCreate(EditorOverrideScope scope, Func<T> valueFactory) {
            if (!ValuesByScope.TryGetValue(scope, out T value)) {
                value = valueFactory();
                ValuesByScope.Add(scope, value);
            }

            return value;
        }

        /// <summary>Attempts to resolve the payload authored exactly at the supplied scope.</summary>
        public bool TryGet(EditorOverrideScope scope, out T value) {
            return ValuesByScope.TryGetValue(scope, out value);
        }

        /// <summary>
        /// Resolves the payload whose scope is the longest prefix of <paramref name="target"/>, Common included.
        /// </summary>
        public bool TryGetDeepestPrefix(EditorOverrideScope target, out T value, out EditorOverrideScope matched) {
            bool found = false;
            int bestDepth = -1;
            value = default;
            matched = EditorOverrideScope.Common;
            foreach (KeyValuePair<EditorOverrideScope, T> entry in ValuesByScope) {
                if (entry.Key.Depth <= bestDepth || !entry.Key.IsPrefixOf(target)) {
                    continue;
                }

                found = true;
                bestDepth = entry.Key.Depth;
                value = entry.Value;
                matched = entry.Key;
            }

            return found;
        }

        /// <summary>Removes one payload.</summary>
        public bool Remove(EditorOverrideScope scope) {
            return ValuesByScope.Remove(scope);
        }

        /// <summary>Enumerates every payload in insertion order.</summary>
        public IEnumerable<T> EnumerateValues() {
            return ValuesByScope.Values;
        }

        /// <summary>Enumerates every authored scope in insertion order.</summary>
        public IEnumerable<EditorOverrideScope> EnumerateScopes() {
            return ValuesByScope.Keys;
        }
    }
}
```

- [ ] **Step 6: Build the editor and run the new test plus the existing scope consumers**

Run: `& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorOverrideScopeTests|FullyQualifiedName~EntityEnvironmentOverrideEditingServiceTests|FullyQualifiedName~EntitySaveComponentTests|FullyQualifiedName~ComponentPlatformOverridePayloadServiceTests" --nologo -v q`
Expected: all pass. The whole editor still compiles because the pair constructor and the transitional members preserve every call site.

- [ ] **Step 7: Commit**

```
git add engine/helengine.editor/model/EditorOverrideScopeStep.cs engine/helengine.editor/model/EditorOverrideScope.cs engine/helengine.editor/model/EditorOverrideScopeMap.cs engine/helengine.editor.tests/EditorOverrideScopeTests.cs
git commit -m "editor: make the override scope a typed step path"
```

### Task 3: Store the path on the override assets and bump the formats

One commit breaks the format once: the three override assets carry `Scope`, the entity carries its level order, both entity payload readers move to version 9, the two container versions move to 25 and the component wrapped payload moves to 5. Editor state objects carry an `EditorOverrideScope Scope` instead of the id pair.

**Files:**
- Modify: `engine/helengine.core/assets/raw/scene/SceneEntityPlatformExistenceOverrideAsset.cs`, `SceneEntityPlatformTransformOverrideAsset.cs`, `SceneEntityPlatformComponentOverrideAsset.cs`, `SceneEntityAsset.cs`
- Modify: `engine/helengine.files/assets/SceneEntityPayloadFormat.cs`, `engine/helengine.files/assets/EditorAssetBinarySerializer.cs:21`
- Modify: `engine/helengine.core/assets/PackagedAssetBinarySerializer.cs:19,24,596-700`
- Modify: `engine/helengine.editor/components/persistence/EntitySaveComponent.cs`, `EntityComponentSaveState.cs:113-142`, `EntityComponentPlatformOverrideState.cs`, `EntityPlatformComponentOverrideState.cs`
- Modify: `engine/helengine.editor/serialization/scene/SceneSaveService.cs:440-530`, `SceneLoadService.cs:230-370`, `ComponentPlatformOverridePayloadService.cs:14,120,173-180,233-240,325-335`, `SceneAssetReferenceValidationService.cs:138`
- Modify: `engine/helengine.editor/managers/project/BlueprintPackagedSceneExpansionService.cs:533-575`, `EditorWindowsBuildScenePackager.cs:1017-1130`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneEntityOverrideScopeFormatTests.cs` (new) plus mechanical updates listed in Step 8.

**Interfaces:**
- Consumes: Task 1 core types, Task 2 `EditorOverrideScope.FromSteps/ToSteps`.
- Produces: on each override asset `SceneOverrideScopeStepAsset[] Scope { get; set; } = Array.Empty<...>()` (replaces `PlatformId`/`EnvironmentId`); on `SceneEntityAsset` `bool HasOverrideLevelOrder` and `SceneOverrideScopeStepKind[] OverrideLevelOrder`; on `EntityComponentPlatformOverrideState` and `EntityPlatformComponentOverrideState` `EditorOverrideScope Scope { get; set; }` (replaces the pair); on `EntitySaveComponent` `EditorOverrideScope ActiveTransformScope { get; set; }` (replaces `ActiveTransformPlatformId`/`ActiveTransformEnvironmentId`) and `IReadOnlyList<SceneOverrideScopeStepKind> OverrideLevelOrder { get; set; }` (null = project default).

- [ ] **Step 1: Write the failing round-trip test**

```csharp
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies the scope path and level order survive the editor asset format and that the previous entity payload version is rejected.
    /// </summary>
    public sealed class SceneEntityOverrideScopeFormatTests {
        [Fact]
        public void SceneAsset_RoundTripsGroupChainCommonAndLevelOrder() {
            SceneEntityPlatformExistenceOverrideAsset commonOverride = new SceneEntityPlatformExistenceOverrideAsset {
                Scope = SceneOverrideScopePath.Common(),
                Exists = false
            };
            SceneEntityPlatformExistenceOverrideAsset groupOverride = new SceneEntityPlatformExistenceOverrideAsset {
                Scope = new[] {
                    new SceneOverrideScopeStepAsset { Kind = SceneOverrideScopeStepKind.Group, Id = "consoles" },
                    new SceneOverrideScopeStepAsset { Kind = SceneOverrideScopeStepKind.Group, Id = "handheld" }
                },
                Exists = true
            };
            SceneAsset scene = new SceneAsset {
                Id = "Scenes/Scope.helen",
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Rig",
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        HasOverrideLevelOrder = true,
                        OverrideLevelOrder = new[] { SceneOverrideScopeStepKind.Group, SceneOverrideScopeStepKind.Platform },
                        PlatformExistenceOverrides = new[] { groupOverride, commonOverride },
                        PlatformTransformOverrides = new[] {
                            new SceneEntityPlatformTransformOverrideAsset {
                                Scope = SceneOverrideScopePath.PlatformBuildConfig("ps1", "debug"),
                                HasLocalScaleOverride = true,
                                LocalScale = new float3(2f, 2f, 2f)
                            }
                        }
                    }
                }
            };

            SceneAsset loaded = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(new MemoryStream(AssetSerializer.SerializeToBytes(scene))));
            SceneEntityAsset entity = Assert.Single(loaded.RootEntities);

            Assert.True(entity.HasOverrideLevelOrder);
            Assert.Equal(new[] { SceneOverrideScopeStepKind.Group, SceneOverrideScopeStepKind.Platform }, entity.OverrideLevelOrder);
            Assert.Equal(2, entity.PlatformExistenceOverrides.Length);
            Assert.Equal("common", SceneOverrideScopePath.Format(entity.PlatformExistenceOverrides[0].Scope));
            Assert.False(entity.PlatformExistenceOverrides[0].Exists);
            Assert.Equal("group:consoles/group:handheld", SceneOverrideScopePath.Format(entity.PlatformExistenceOverrides[1].Scope));
            Assert.Equal("platform:ps1/buildconfig:debug", SceneOverrideScopePath.Format(Assert.Single(entity.PlatformTransformOverrides).Scope));
        }

        [Fact]
        public void SceneAsset_WithoutLevelOrder_ReadsBackAsAbsent() {
            SceneAsset scene = new SceneAsset {
                Id = "Scenes/NoOrder.helen",
                RootEntities = new[] { new SceneEntityAsset { Id = 1u, Name = "A", LocalScale = float3.One, LocalOrientation = float4.Identity } }
            };

            SceneAsset loaded = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(new MemoryStream(AssetSerializer.SerializeToBytes(scene))));

            Assert.False(loaded.RootEntities[0].HasOverrideLevelOrder);
            Assert.Empty(loaded.RootEntities[0].OverrideLevelOrder);
        }

        [Fact]
        public void SceneAsset_WithDuplicatePath_IsRejectedBeforeWriting() {
            SceneAsset scene = new SceneAsset {
                Id = "Scenes/Dup.helen",
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u, Name = "A", LocalScale = float3.One, LocalOrientation = float4.Identity,
                        PlatformExistenceOverrides = new[] {
                            new SceneEntityPlatformExistenceOverrideAsset { Scope = SceneOverrideScopePath.Platform("PS1"), Exists = true },
                            new SceneEntityPlatformExistenceOverrideAsset { Scope = SceneOverrideScopePath.Platform("ps1"), Exists = false }
                        }
                    }
                }
            };

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => AssetSerializer.SerializeToBytes(scene));
            Assert.Contains("platform:ps1", error.Message);
        }

        [Fact]
        public void Versions_MovedTogether() {
            Assert.Equal(9, helengine.files.SceneEntityPayloadFormat.SceneEntityPayloadVersion);
            Assert.Equal(25, helengine.files.EditorAssetBinarySerializer.CurrentVersion);
            Assert.Equal(25, PackagedAssetBinarySerializer.CurrentVersion);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~SceneEntityOverrideScopeFormatTests" --nologo -v q`
Expected: build error — `Scope`, `HasOverrideLevelOrder` do not exist.

- [ ] **Step 3: Change the core asset classes**

In each of the three override assets delete the `PlatformId` and `EnvironmentId` properties and add, as the first property:

```csharp
        /// <summary>
        /// Gets or sets the scope path this override is authored on. Empty is Common.
        /// </summary>
        public SceneOverrideScopeStepAsset[] Scope { get; set; } = Array.Empty<SceneOverrideScopeStepAsset>();
```

Update each class summary to say "authored on one override scope path" instead of "platform-specific". In `SceneEntityAsset.cs` add after `LocalOrientation`:

```csharp
        /// <summary>
        /// Gets or sets whether <see cref="OverrideLevelOrder"/> is authored; false means the project default order applies.
        /// </summary>
        public bool HasOverrideLevelOrder { get; set; }

        /// <summary>
        /// Gets or sets the level kinds beneath Common, outermost first, that this entity's overrides are authored against.
        /// </summary>
        public SceneOverrideScopeStepKind[] OverrideLevelOrder { get; set; } = Array.Empty<SceneOverrideScopeStepKind>();
```

- [ ] **Step 4: Update `SceneEntityPayloadFormat.cs`**

Set `SceneEntityPayloadVersion = 9`. Replace `ValidateDeterministicSceneEntityOverrides` body's three `EnsureUniquePlatformOverrideScopes(...)` calls with `EnsureUniqueOverrideScopes(entity.PlatformExistenceOverrides, item => item?.Scope)` etc. In `WriteSceneEntityAsset`, after `writer.WriteFloat4(asset.LocalOrientation);` add:

```csharp
            writer.WriteByte(asset.HasOverrideLevelOrder ? (byte)1 : (byte)0);
            writer.WriteArray(asset.HasOverrideLevelOrder ? asset.OverrideLevelOrder ?? Array.Empty<SceneOverrideScopeStepKind>() : Array.Empty<SceneOverrideScopeStepKind>(), WriteSceneOverrideScopeStepKind);
```

In `ReadSceneEntityAsset`, after `localOrientation`:

```csharp
            bool hasOverrideLevelOrder = reader.ReadByte() != 0;
            SceneOverrideScopeStepKind[] overrideLevelOrder = reader.ReadArray(ReadSceneOverrideScopeStepKind) ?? Array.Empty<SceneOverrideScopeStepKind>();
```

and set `HasOverrideLevelOrder = hasOverrideLevelOrder, OverrideLevelOrder = hasOverrideLevelOrder ? overrideLevelOrder : Array.Empty<SceneOverrideScopeStepKind>()` in the returned asset. Replace the three sort methods' first two `OrderBy/ThenBy` with one `OrderBy(item => SceneOverrideScopePath.Format(item?.Scope), StringComparer.Ordinal)` and make each start with `EnsureUniqueOverrideScopes(overrides, item => item?.Scope);`. Replace `EnsureUniquePlatformOverrideScopes` and `NormalizeOverrideScopeIdentifier` with:

```csharp
        /// <summary>
        /// Rejects two override records on one scope path before bytes are emitted.
        /// </summary>
        static void EnsureUniqueOverrideScopes<T>(T[] overrides, Func<T, SceneOverrideScopeStepAsset[]> scopeSelector) {
            if (overrides == null) {
                return;
            }

            HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < overrides.Length; index++) {
                string path = SceneOverrideScopePath.Format(SceneOverrideScopePath.Normalize(scopeSelector(overrides[index])));
                if (!paths.Add(path)) {
                    throw new InvalidOperationException($"Duplicate override scope '{path}'.");
                }
            }
        }

        static void WriteSceneOverrideScopeSteps(EngineBinaryWriter writer, SceneOverrideScopeStepAsset[] steps) {
            writer.WriteArray(SceneOverrideScopePath.Normalize(steps), WriteSceneOverrideScopeStep);
        }

        static void WriteSceneOverrideScopeStep(EngineBinaryWriter writer, SceneOverrideScopeStepAsset step) {
            writer.WriteByte((byte)step.Kind);
            writer.WriteString(step.Id);
        }

        static SceneOverrideScopeStepAsset[] ReadSceneOverrideScopeSteps(EngineBinaryReader reader) {
            return reader.ReadArray(ReadSceneOverrideScopeStep) ?? Array.Empty<SceneOverrideScopeStepAsset>();
        }

        static SceneOverrideScopeStepAsset ReadSceneOverrideScopeStep(EngineBinaryReader reader) {
            return new SceneOverrideScopeStepAsset {
                Kind = (SceneOverrideScopeStepKind)reader.ReadByte(),
                Id = reader.ReadString()
            };
        }

        static void WriteSceneOverrideScopeStepKind(EngineBinaryWriter writer, SceneOverrideScopeStepKind kind) {
            writer.WriteByte((byte)kind);
        }

        static SceneOverrideScopeStepKind ReadSceneOverrideScopeStepKind(EngineBinaryReader reader) {
            return (SceneOverrideScopeStepKind)reader.ReadByte();
        }
```

In the three `Write...OverrideAsset` methods replace `writer.WriteString(asset.PlatformId); writer.WriteString(asset.EnvironmentId ?? string.Empty);` with `WriteSceneOverrideScopeSteps(writer, asset.Scope);`. In the three `Read...OverrideAsset` methods replace `PlatformId = reader.ReadString(), EnvironmentId = reader.ReadString(),` with `Scope = ReadSceneOverrideScopeSteps(reader),`. Give the new statics XML summaries (one line each).

- [ ] **Step 5: Update the runtime reader and both container versions**

`PackagedAssetBinarySerializer.cs`: `CurrentVersion = 25`, `SceneEntityPayloadVersion = 9`. In the entity reader after `localOrientation`, read the flag and kind array exactly as in Step 4 (add `EngineBinaryReadContext.CurrentReadStage = "SceneEntity:OverrideLevelOrder";` before it) and set both properties on the returned asset. In the three override readers replace the two `ReadString()` pair lines with `Scope = ReadSceneOverrideScopeSteps(reader),` and add private statics `ReadSceneOverrideScopeSteps`, `ReadSceneOverrideScopeStep`, `ReadSceneOverrideScopeStepKind` identical to Step 4 (core has the same `ReadArray<T>`). No LINQ. `EditorAssetBinarySerializer.cs`: `CurrentVersion = 25`.

- [ ] **Step 6: Carry `Scope` on the editor state objects**

`EntityComponentPlatformOverrideState.cs` and `EntityPlatformComponentOverrideState.cs`: delete `PlatformId`/`EnvironmentId` (and their constructor initializers) and add

```csharp
        /// <summary>
        /// Gets or sets the scope path this override is authored on.
        /// </summary>
        public EditorOverrideScope Scope { get; set; }
```

`EntityComponentSaveState.cs` lines 118-119 and 139-140: replace the two assignments with `overrideState.Scope = scope;` / `Scope = scope`. `EntitySaveComponent.cs`: replace `ActiveTransformPlatformId`/`ActiveTransformEnvironmentId` with

```csharp
        /// <summary>
        /// Gets or sets the scope currently projected into the live entity transform while editing; Common when none.
        /// </summary>
        public EditorOverrideScope ActiveTransformScope { get; set; }

        /// <summary>
        /// Gets or sets the authored level order beneath Common, or null to use the project default.
        /// </summary>
        public IReadOnlyList<SceneOverrideScopeStepKind> OverrideLevelOrder { get; set; }
```

and in every `Set*PlatformOverride(EditorOverrideScope scope, ...)` and `GetOrCreate*PlatformOverride(EditorOverrideScope scope)` replace the `PlatformId = scope.PlatformId, EnvironmentId = scope.EnvironmentId` pairs with `Scope = scope.ToSteps()` for the two asset-typed maps and `Scope = scope` for `EntityPlatformComponentOverrideState`. `EntityPlatformTransformEditingService` still compiles only after Task 5; to keep this commit green replace its uses of the two removed strings now with `saveComponent.ActiveTransformScope`: `new EditorOverrideScope(NormalizePlatformId(saveComponent.ActiveTransformPlatformId), saveComponent.ActiveTransformEnvironmentId)` → `saveComponent.ActiveTransformScope`; `saveComponent.ActiveTransformPlatformId = scope.PlatformId; saveComponent.ActiveTransformEnvironmentId = scope.EnvironmentId;` → `saveComponent.ActiveTransformScope = scope;`; `string platformId = NormalizePlatformId(saveComponent.ActiveTransformPlatformId); if (IsCommonPlatformId(platformId) ...` → `EditorOverrideScope scope = saveComponent.ActiveTransformScope; if (scope.IsCommon ...` (use `scope.PlatformId` where the old code used `platformId`, transitional); `!IsCommonPlatformId(saveComponent.ActiveTransformPlatformId)` → `!saveComponent.ActiveTransformScope.IsCommon`; in `ClearActiveProjection` set `saveComponent.ActiveTransformScope = EditorOverrideScope.Common;`. Task 5 rewrites the rest.

- [ ] **Step 7: Update save, load, payload wrapper, clones, validation message and the packager lookups**

`SceneSaveService.cs`: in the three `Clone...Overrides` methods drop the `string.IsNullOrWhiteSpace(overrideState.PlatformId)` guard part and replace `PlatformId = ..., EnvironmentId = ...` with `Scope = overrideState.Scope` (asset-typed states already hold `SceneOverrideScopeStepAsset[]`; for `EntityPlatformComponentOverrideState` use `Scope = overrideState.Scope.ToSteps()`). `SceneLoadService.cs`: in the three `RestoreEntity...Overrides` replace `overrideAsset == null || string.IsNullOrWhiteSpace(overrideAsset.PlatformId)` with `overrideAsset == null`, `new EditorOverrideScope(overrideAsset.PlatformId, overrideAsset.EnvironmentId)` with `EditorOverrideScope.FromSteps(overrideAsset.Scope)`, and the `PlatformId = scope.PlatformId, EnvironmentId = scope.EnvironmentId,` pairs with `Scope = scope.ToSteps(),`; in the component-payload restore (line 239) use `overrideState.Scope` directly. Also add to the entity restore, where the save component is created for a loaded entity: `saveComponent.OverrideLevelOrder = entityAsset.HasOverrideLevelOrder ? entityAsset.OverrideLevelOrder : null;` and in `SceneSaveService` where the `SceneEntityAsset` is built from the entity: `HasOverrideLevelOrder = saveComponent?.OverrideLevelOrder != null, OverrideLevelOrder = saveComponent?.OverrideLevelOrder == null ? Array.Empty<SceneOverrideScopeStepKind>() : saveComponent.OverrideLevelOrder.ToArray()`.

`ComponentPlatformOverridePayloadService.cs`: `WrappedPayloadVersion = 5`; `GetPlatformOverrides` orders by `overrideState.Scope.ToString()` ordinal; `WriteOverrideState` drops the platform-id guard and writes `SceneOverrideScopeStepAsset[] steps = overrideState.Scope.ToSteps(); writer.WriteInt32(steps.Length); for (...) { writer.WriteByte((byte)steps[index].Kind); writer.WriteString(steps[index].Id); }`; `ReadOverrideState` reads the count and steps into an array and sets `Scope = EditorOverrideScope.FromSteps(steps)`; `ReadWrappedOverrides` uses `overrideState.Scope` as the duplicate key.

`BlueprintPackagedSceneExpansionService.cs` clones: replace the `PlatformId`/`EnvironmentId` initializers with `Scope = SceneOverrideScopePath.Normalize(overrideAsset.Scope)`. `SceneAssetReferenceValidationService.cs:138`: pass `overrideState.Scope.ToString()`.

`EditorWindowsBuildScenePackager.cs`: in the three `FindTargetPlatform*Override` methods replace the loop body with exact-path matching that reproduces today's behaviour until Task 10 installs the resolver:

```csharp
            EditorOverrideScope targetScope = new EditorOverrideScope(TargetPlatformId, SelectedEnvironmentId);
            EditorOverrideScope platformScope = new EditorOverrideScope(TargetPlatformId);
            SceneEntityPlatformExistenceOverrideAsset platformOverride = null;
            for (int index = 0; index < existenceOverrides.Length; index++) {
                SceneEntityPlatformExistenceOverrideAsset existenceOverride = existenceOverrides[index];
                if (existenceOverride == null) {
                    continue;
                }

                EditorOverrideScope scope = EditorOverrideScope.FromSteps(existenceOverride.Scope);
                if (scope == targetScope) {
                    return existenceOverride;
                }
                if (scope == platformScope) {
                    platformOverride = existenceOverride;
                }
            }

            return platformOverride;
```

(same shape for transform and component overrides).

- [ ] **Step 8: Update every test that names the old pair**

Mechanical rule, applied with the Grep tool across `engine/**/*Tests.cs` for `PlatformId = ` and `EnvironmentId = ` inside `SceneEntityPlatform*OverrideAsset`, `EntityComponentPlatformOverrideState` and `EntityPlatformComponentOverrideState` initializers only (build-config, platform-definition and animation-clip tests use unrelated `PlatformId` properties and stay):
- `PlatformId = "x"` alone → `Scope = SceneOverrideScopePath.Platform("x")`
- `PlatformId = "x", EnvironmentId = "y"` → `Scope = SceneOverrideScopePath.PlatformBuildConfig("x", "y")`
- for the two editor state types → `Scope = new EditorOverrideScope("x", "y")`
- assertions `Assert.Equal("x", o.PlatformId)` → `Assert.Equal("platform:x", SceneOverrideScopePath.Format(o.Scope))` (or `o.Scope.ToString()` for the editor state types).

Known files: `DeterministicScenePlatformSerializationTests.cs`, `BinarySerializationTests.cs`, `SceneSaveServiceTests.cs`, `ComponentPlatformOverridePayloadServiceTests.cs`, `EntitySaveComponentTests.cs`, `EntityEnvironmentOverrideEditingServiceTests.cs`, `BlueprintPackagedSceneExpansionTests.cs`, `BlueprintBuildPackagingIntegrationTests.cs`, `BlueprintSaveServiceTests.cs`, `BlueprintAssetBinarySerializerTests.cs`, `EditorWindowsBuildScenePackagerTests.cs`, `ComponentPropertyMutationServiceTests.cs`, `ComponentPropertyEditControllerTests.cs`, `PropertiesPanelComponentShellTests.cs`, and in `engine/helengine.bepu.tests`: `BepuGroundCubeProbeSceneTests.cs`, `BepuCityDynamicStackBoxesSceneTests.cs`. Any test asserting `ActiveTransformPlatformId` asserts `ActiveTransformScope` instead.

- [ ] **Step 9: Run the affected suites**

Run: `& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~Serialization|FullyQualifiedName~SceneSave|FullyQualifiedName~Blueprint|FullyQualifiedName~Packager|FullyQualifiedName~EntitySaveComponent|FullyQualifiedName~OverrideEditing|FullyQualifiedName~ComponentProperty|FullyQualifiedName~CurrentFormatOnly|FullyQualifiedName~SceneEntityOverrideScopeFormatTests" --nologo -v q`
then `& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.core.tests/helengine.core.tests.csproj --nologo -v q` and `... engine/helengine.files.tests/helengine.files.tests.csproj --nologo -v q` and `... engine/helengine.bepu.tests/helengine.bepu.tests.csproj --filter "FullyQualifiedName~Scene" --nologo -v q`.
Expected: all pass, including `CurrentFormatOnlySourceContractTests`.

- [ ] **Step 10: Commit**

```
git add -A engine/helengine.core engine/helengine.files engine/helengine.editor engine/helengine.editor.tests engine/helengine.core.tests engine/helengine.files.tests engine/helengine.bepu.tests
git commit -m "scene format: author overrides on scope paths and record the entity level order"
```

### Task 4: Existence service resolves by deepest prefix and accepts Common

**Files:**
- Modify: `engine/helengine.editor/managers/scene/EntityPlatformExistenceEditingService.cs`
- Test: `engine/helengine.editor.tests/EntityEnvironmentOverrideEditingServiceTests.cs` (add tests)

**Interfaces:**
- Produces: `bool ResolveExists(EntitySaveComponent, EditorOverrideScope)`, `bool ResolveExists(EntitySaveComponent, string platformId)` (delegates via `EditorOverrideScope.ForPlatform`), `void SetExists(EntitySaveComponent, EditorOverrideScope, bool)`, `void SetExists(EntitySaveComponent, string, bool)`, `bool HasExistenceOverride(EntitySaveComponent, EditorOverrideScope)`, `bool HasExistenceOverride(EntitySaveComponent, string)`. `CommonPlatformId` constant unchanged.

- [ ] **Step 1: Write the failing tests** (append to the existing class)

```csharp
        [Fact]
        public void ResolveExists_WhenGroupChainIsAuthored_UsesTheDeepestPrefix() {
            EntitySaveComponent saveComponent = new EntitySaveComponent();
            EntityPlatformExistenceEditingService service = new EntityPlatformExistenceEditingService();
            EditorOverrideScope handheld = EditorOverrideScope.FromSteps(SceneOverrideScopePath.Group("handheld"));
            EditorOverrideScope handheldDs = handheld.Append(new EditorOverrideScopeStep(SceneOverrideScopeStepKind.Platform, "ds"));
            EditorOverrideScope handheldPsp = handheld.Append(new EditorOverrideScopeStep(SceneOverrideScopeStepKind.Platform, "psp"));

            service.SetExists(saveComponent, EditorOverrideScope.Common, false);
            service.SetExists(saveComponent, handheld, true);
            service.SetExists(saveComponent, handheldPsp, false);

            Assert.True(service.ResolveExists(saveComponent, handheldDs));
            Assert.False(service.ResolveExists(saveComponent, handheldPsp));
            Assert.False(service.ResolveExists(saveComponent, new EditorOverrideScope("ps1")));
            Assert.False(service.ResolveExists(saveComponent, EditorOverrideScope.Common));
        }

        [Fact]
        public void SetExists_OnCommon_StoresFalseAndRemovesTrue() {
            EntitySaveComponent saveComponent = new EntitySaveComponent();
            EntityPlatformExistenceEditingService service = new EntityPlatformExistenceEditingService();

            service.SetExists(saveComponent, EditorOverrideScope.Common, false);
            Assert.True(saveComponent.TryGetExistencePlatformOverride(EditorOverrideScope.Common, out SceneEntityPlatformExistenceOverrideAsset stored));
            Assert.Empty(stored.Scope);
            Assert.False(stored.Exists);

            service.SetExists(saveComponent, EditorOverrideScope.Common, true);
            Assert.False(saveComponent.TryGetExistencePlatformOverride(EditorOverrideScope.Common, out _));
        }

        [Fact]
        public void SetExists_WhenValueMatchesParentPrefix_RemovesTheDeeperOverride() {
            EntitySaveComponent saveComponent = new EntitySaveComponent();
            EntityPlatformExistenceEditingService service = new EntityPlatformExistenceEditingService();
            EditorOverrideScope handheld = EditorOverrideScope.FromSteps(SceneOverrideScopePath.Group("handheld"));
            EditorOverrideScope handheldDsDebug = handheld
                .Append(new EditorOverrideScopeStep(SceneOverrideScopeStepKind.Platform, "ds"))
                .Append(new EditorOverrideScopeStep(SceneOverrideScopeStepKind.BuildConfig, "debug"));

            service.SetExists(saveComponent, handheld, false);
            service.SetExists(saveComponent, handheldDsDebug, true);
            Assert.True(saveComponent.TryGetExistencePlatformOverride(handheldDsDebug, out _));

            service.SetExists(saveComponent, handheldDsDebug, false);
            Assert.False(saveComponent.TryGetExistencePlatformOverride(handheldDsDebug, out _));
            Assert.True(service.HasExistenceOverride(saveComponent, handheld));
            Assert.False(service.HasExistenceOverride(saveComponent, handheldDsDebug));
        }
```

- [ ] **Step 2: Run to verify they fail**

Run: `& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EntityEnvironmentOverrideEditingServiceTests" --nologo -v q`
Expected: `SetExists_OnCommon_StoresFalseAndRemovesTrue` fails (Common is refused today); the group test fails because platform-then-environment layering ignores group steps.

- [ ] **Step 3: Rewrite the service body**

Replace everything from `ResolveExists(EntitySaveComponent saveComponent, string platformId)` to the end of the class with:

```csharp
        /// <summary>
        /// Resolves whether one entity exists on the default-order path for a platform.
        /// </summary>
        public bool ResolveExists(EntitySaveComponent saveComponent, string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            return ResolveExists(saveComponent, EditorOverrideScope.ForPlatform(platformId));
        }

        /// <summary>
        /// Resolves existence at one path: the deepest authored prefix wins, Common included; nothing authored means the entity exists.
        /// </summary>
        public bool ResolveExists(EntitySaveComponent saveComponent, EditorOverrideScope scope) {
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }

            if (saveComponent.TryGetDeepestExistencePlatformOverride(scope, out SceneEntityPlatformExistenceOverrideAsset overrideState)) {
                return overrideState.Exists;
            }

            return true;
        }

        /// <summary>
        /// Returns whether one path stores its own existence override.
        /// </summary>
        public bool HasExistenceOverride(EntitySaveComponent saveComponent, string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            return HasExistenceOverride(saveComponent, EditorOverrideScope.ForPlatform(platformId));
        }

        /// <summary>
        /// Returns whether one path stores its own existence override.
        /// </summary>
        public bool HasExistenceOverride(EntitySaveComponent saveComponent, EditorOverrideScope scope) {
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }

            return saveComponent.TryGetExistencePlatformOverride(scope, out _);
        }

        /// <summary>
        /// Stores existence for the default-order path of a platform.
        /// </summary>
        public void SetExists(EntitySaveComponent saveComponent, string platformId, bool exists) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            SetExists(saveComponent, EditorOverrideScope.ForPlatform(platformId), exists);
        }

        /// <summary>
        /// Stores a sparse override: the entry is removed when it equals what the parent path already resolves to.
        /// Common's parent value is the implicit "exists".
        /// </summary>
        public void SetExists(EntitySaveComponent saveComponent, EditorOverrideScope scope, bool exists) {
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }

            bool parentExists = scope.IsCommon ? true : ResolveExists(saveComponent, scope.Parent);
            if (exists == parentExists) {
                saveComponent.RemoveExistencePlatformOverride(scope);
                ExistenceChanged?.Invoke();
                return;
            }

            saveComponent.SetExistencePlatformOverride(scope, new SceneEntityPlatformExistenceOverrideAsset {
                Scope = scope.ToSteps(),
                Exists = exists
            });
            ExistenceChanged?.Invoke();
        }
    }
}
```

Add to `EntitySaveComponent.cs` next to `TryGetExistencePlatformOverride(EditorOverrideScope, ...)`:

```csharp
        /// <summary>
        /// Resolves the existence override authored on the deepest prefix of <paramref name="scope"/>, Common included.
        /// </summary>
        public bool TryGetDeepestExistencePlatformOverride(EditorOverrideScope scope, out SceneEntityPlatformExistenceOverrideAsset overrideState) {
            return ExistenceOverridesByScope.TryGetDeepestPrefix(scope, out overrideState, out _);
        }
```

Delete the now-unused `IsCommonPlatformId`/`NormalizePlatformId` statics and the stray duplicated `/// <summary>` line above them.

- [ ] **Step 4: Run the suite and the panel tests that toggle the Exists checkbox**

Run: `& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EntityEnvironmentOverrideEditingServiceTests|FullyQualifiedName~PropertiesPanel|FullyQualifiedName~PlatformSceneAuthoringHelper|FullyQualifiedName~ExistenceViewport" --nologo -v q`
Expected: pass. (`PropertiesPanel` still disables the checkbox on the Common tab, so Common entries only arrive through the API until the second plan.)

- [ ] **Step 5: Commit**

```
git add engine/helengine.editor/managers/scene/EntityPlatformExistenceEditingService.cs engine/helengine.editor/components/persistence/EntitySaveComponent.cs engine/helengine.editor.tests/EntityEnvironmentOverrideEditingServiceTests.cs
git commit -m "editor: resolve entity existence by deepest authored scope prefix"
```

---

### Task 5: Transform service folds overrides along the path

**Files:**
- Modify: `engine/helengine.editor/managers/scene/EntityPlatformTransformEditingService.cs`
- Test: `engine/helengine.editor.tests/EntityEnvironmentOverrideEditingServiceTests.cs` (add test)

**Interfaces:**
- Produces: `ActivateScope(Entity, EntitySaveComponent, EditorOverrideScope)` projects Common snapshot then every authored prefix from shallowest to deepest; `PersistActiveScope` diffs the live transform against the fold of the active scope's `Parent`; `ActivatePlatform/PersistActivePlatform/RestoreCommon/Is*OverrideActive/Clear*Override(string platformId)` delegate through `EditorOverrideScope.ForPlatform`. Private `void FoldScopeTransform(EntitySaveComponent, EditorOverrideScope, ref float3, ref float3, ref float4)` replaces `ApplyScopeTransform`'s two-step body.

- [ ] **Step 1: Write the failing test**

```csharp
        [Fact]
        public void ActivateScope_WhenGroupPlatformAndConfigAreAuthored_FoldsEveryPrefix() {
            EditorEntity entity = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) {
                LocalPosition = float3.Zero,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity
            };
            EntitySaveComponent saveComponent = new EntitySaveComponent();
            EntityPlatformTransformEditingService service = new EntityPlatformTransformEditingService();
            EditorOverrideScope handheld = EditorOverrideScope.FromSteps(SceneOverrideScopePath.Group("handheld"));
            EditorOverrideScope handheldDs = handheld.Append(new EditorOverrideScopeStep(SceneOverrideScopeStepKind.Platform, "ds"));
            EditorOverrideScope handheldDsDebug = handheldDs.Append(new EditorOverrideScopeStep(SceneOverrideScopeStepKind.BuildConfig, "debug"));

            saveComponent.SetTransformPlatformOverride(handheld, new SceneEntityPlatformTransformOverrideAsset { HasLocalPositionOverride = true, LocalPosition = new float3(1f, 0f, 0f) });
            saveComponent.SetTransformPlatformOverride(handheldDs, new SceneEntityPlatformTransformOverrideAsset { HasLocalScaleOverride = true, LocalScale = new float3(3f, 3f, 3f) });
            saveComponent.SetTransformPlatformOverride(handheldDsDebug, new SceneEntityPlatformTransformOverrideAsset { HasLocalPositionOverride = true, LocalPosition = new float3(2f, 0f, 0f) });

            service.ActivateScope(entity, saveComponent, handheldDsDebug);
            Assert.Equal(new float3(2f, 0f, 0f), entity.LocalPosition);
            Assert.Equal(new float3(3f, 3f, 3f), entity.LocalScale);
            Assert.Equal(handheldDsDebug, saveComponent.ActiveTransformScope);

            entity.LocalScale = new float3(3f, 3f, 3f);
            entity.LocalPosition = new float3(1f, 0f, 0f);
            service.RestoreCommonScope(entity, saveComponent);

            Assert.False(saveComponent.TryGetTransformPlatformOverride(handheldDsDebug, out _));
            Assert.True(saveComponent.ActiveTransformScope.IsCommon);
            Assert.Equal(float3.Zero, entity.LocalPosition);
        }
```

- [ ] **Step 2: Run to verify it fails**

Same filter as Task 4. Expected: position asserts `2,0,0` but gets `0,0,0` because the group step is not a platform step.

- [ ] **Step 3: Rewrite the scope-aware members**

Replace `ActivatePlatform` body with `ActivateScope(entity, saveComponent, EditorOverrideScope.ForPlatform(platformId));` (keep the null/blank guards). Replace `ActivateScope`, `PersistActiveScope`, `PersistActivePlatform`, `ApplyPlatformTransform`, `ApplyScopeTransform` with:

```csharp
        /// <summary>
        /// Projects one path into the live transform: the Common snapshot, then every authored prefix from shallowest to deepest.
        /// </summary>
        public void ActivateScope(Entity entity, EntitySaveComponent saveComponent, EditorOverrideScope scope) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }
            if (saveComponent.ActiveTransformScope == scope) {
                return;
            }

            PersistActiveScope(entity, saveComponent);
            if (scope.IsCommon) {
                RestoreCommonTransform(entity, saveComponent);
                ClearActiveProjection(saveComponent);
                return;
            }

            if (!saveComponent.HasCommonTransformSnapshot) {
                CaptureCommonTransform(entity, saveComponent);
            }

            float3 localPosition = saveComponent.CommonLocalPositionSnapshot;
            float3 localScale = saveComponent.CommonLocalScaleSnapshot;
            float4 localOrientation = saveComponent.CommonLocalOrientationSnapshot;
            FoldScopeTransform(saveComponent, scope, ref localPosition, ref localScale, ref localOrientation);
            entity.LocalPosition = localPosition;
            entity.LocalScale = localScale;
            entity.LocalOrientation = localOrientation;
            saveComponent.ActiveTransformScope = scope;
        }

        /// <summary>
        /// Persists the projected path's payload as the difference between the live transform and the fold of its parent path.
        /// </summary>
        public void PersistActiveScope(Entity entity, EntitySaveComponent saveComponent) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }

            EditorOverrideScope scope = saveComponent.ActiveTransformScope;
            if (scope.IsCommon || !saveComponent.HasCommonTransformSnapshot) {
                return;
            }

            float3 parentPosition = saveComponent.CommonLocalPositionSnapshot;
            float3 parentScale = saveComponent.CommonLocalScaleSnapshot;
            float4 parentOrientation = saveComponent.CommonLocalOrientationSnapshot;
            FoldScopeTransform(saveComponent, scope.Parent, ref parentPosition, ref parentScale, ref parentOrientation);

            SceneEntityPlatformTransformOverrideAsset overrideState = saveComponent.GetOrCreateTransformPlatformOverride(scope);
            overrideState.Scope = scope.ToSteps();
            overrideState.HasLocalPositionOverride = entity.LocalPosition != parentPosition;
            overrideState.LocalPosition = entity.LocalPosition;
            overrideState.HasLocalScaleOverride = entity.LocalScale != parentScale;
            overrideState.LocalScale = entity.LocalScale;
            overrideState.HasLocalOrientationOverride = !entity.LocalOrientation.Equals(parentOrientation);
            overrideState.LocalOrientation = entity.LocalOrientation;

            if (!overrideState.HasLocalPositionOverride
                && !overrideState.HasLocalScaleOverride
                && !overrideState.HasLocalOrientationOverride) {
                saveComponent.RemoveTransformPlatformOverride(scope);
            }
        }

        /// <summary>
        /// Persists the projected path; kept for callers that think in platforms.
        /// </summary>
        public void PersistActivePlatform(Entity entity, EntitySaveComponent saveComponent) {
            PersistActiveScope(entity, saveComponent);
        }

        /// <summary>
        /// Applies every authored prefix of <paramref name="scope"/>, shallowest first, onto the supplied transform.
        /// </summary>
        void FoldScopeTransform(EntitySaveComponent saveComponent, EditorOverrideScope scope, ref float3 position, ref float3 scale, ref float4 orientation) {
            for (int depth = 1; depth <= scope.Depth; depth++) {
                EditorOverrideScopeStep[] prefixSteps = new EditorOverrideScopeStep[depth];
                for (int index = 0; index < depth; index++) {
                    prefixSteps[index] = scope.Steps[index];
                }

                if (saveComponent.TryGetTransformPlatformOverride(new EditorOverrideScope(prefixSteps), out SceneEntityPlatformTransformOverrideAsset overrideState)) {
                    ApplyOverride(ref position, ref scale, ref orientation, overrideState);
                }
            }
        }
```

In `ClearScopeOverride` replace the trailing `activeScope` comparison with `if (saveComponent.ActiveTransformScope == scope) { float3 p = ...; FoldScopeTransform(...); entity.LocalPosition = p; ... }` — i.e. recompute from the Common snapshot exactly like `ActivateScope` does. `RestoreCommon` calls `PersistActiveScope`. `ResolveSerializedLocal*` test `!saveComponent.ActiveTransformScope.IsCommon`. The `Is*OverrideActive(string)` and `Clear*Override(string)` variants call the scope variants with `EditorOverrideScope.ForPlatform(platformId)`. Remove `IsCommonPlatformId`, `NormalizePlatformId` and `ApplyPlatformTransform`. `ClearActiveProjection` sets `ActiveTransformScope = EditorOverrideScope.Common`.

- [ ] **Step 4: Run the suite**

Run: `& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EntityEnvironmentOverrideEditingServiceTests|FullyQualifiedName~Transform|FullyQualifiedName~PropertiesPanel" --nologo -v q`
Expected: pass.

- [ ] **Step 5: Commit**

```
git add engine/helengine.editor/managers/scene/EntityPlatformTransformEditingService.cs engine/helengine.editor.tests/EntityEnvironmentOverrideEditingServiceTests.cs
git commit -m "editor: fold transform overrides along the scope path"
```

### Task 6: Component editing service recurses over the parent path

Today every scope-taking method special-cases `IsPlatformOnly` and layers "platform then environment". Each becomes: resolve the parent path, then apply this path's own override on top. The `(string platformId)` overloads become one-line delegates so there is a single implementation per operation.

**Files:**
- Modify: `engine/helengine.editor/managers/scene/ComponentPlatformEditingService.cs`
- Test: `engine/helengine.editor.tests/EntityEnvironmentOverrideEditingServiceTests.cs` (add test)

**Interfaces:**
- Produces (unchanged signatures, path semantics): `ResolveEditableComponent(common, save, scope)`, `EnsureScopeOverrideComponent`, `MarkScopePropertyOverride`, `PersistScopeOverride`, `StoreScopeAssetReference`, `IsScopePropertyOverrideActive`, `ClearScopePropertyOverride`, `IsComponentRemoved(common, save, scope)`, `GetAddedComponents(save, scope)`, `AddScopeOnlyComponent`, `RemoveComponent(component, save, scope)`, `RevertComponentExistenceOverride(component, save, scope)`, `TryGetAddedComponentState(component, save, scope, out state)`, `StoreAddedComponentAssetReference(component, save, scope, name, reference)`. Every `string platformId` overload calls its scope twin with `EditorOverrideScope.ForPlatform(platformId)`. Override component cache key is `scope.ToString()`.

- [ ] **Step 1: Write the failing test**

```csharp
        [Fact]
        public void ResolveEditableComponent_WhenGroupAndPlatformOverridesExist_LayersGroupThenPlatform() {
            CameraComponent commonComponent = new CameraComponent { FarPlaneDistance = 100f, NearPlaneDistance = 1f };
            EntitySaveComponent saveComponent = new EntitySaveComponent();
            ComponentPlatformEditingService service = new ComponentPlatformEditingService();
            EditorOverrideScope handheld = EditorOverrideScope.FromSteps(SceneOverrideScopePath.Group("handheld"));
            EditorOverrideScope handheldDs = handheld.Append(new EditorOverrideScopeStep(SceneOverrideScopeStepKind.Platform, "ds"));

            CameraComponent groupComponent = Assert.IsType<CameraComponent>(service.EnsureScopeOverrideComponent(commonComponent, saveComponent, handheld));
            groupComponent.FarPlaneDistance = 200f;
            service.MarkScopePropertyOverride(commonComponent, saveComponent, handheld, nameof(CameraComponent.FarPlaneDistance));
            service.PersistScopeOverride(commonComponent, groupComponent, saveComponent, handheld);

            CameraComponent platformComponent = Assert.IsType<CameraComponent>(service.EnsureScopeOverrideComponent(commonComponent, saveComponent, handheldDs));
            Assert.Equal(200f, platformComponent.FarPlaneDistance);
            platformComponent.NearPlaneDistance = 5f;
            service.MarkScopePropertyOverride(commonComponent, saveComponent, handheldDs, nameof(CameraComponent.NearPlaneDistance));
            service.PersistScopeOverride(commonComponent, platformComponent, saveComponent, handheldDs);

            CameraComponent resolved = Assert.IsType<CameraComponent>(service.ResolveEditableComponent(commonComponent, saveComponent, handheldDs));
            Assert.Equal(200f, resolved.FarPlaneDistance);
            Assert.Equal(5f, resolved.NearPlaneDistance);
            Assert.True(service.IsScopePropertyOverrideActive(commonComponent, resolved, saveComponent, handheldDs, nameof(CameraComponent.NearPlaneDistance)));
            Assert.False(service.IsScopePropertyOverrideActive(commonComponent, resolved, saveComponent, handheldDs, nameof(CameraComponent.FarPlaneDistance)));

            Assert.True(service.RemoveComponent(commonComponent, saveComponent, handheld));
            Assert.True(service.IsComponentRemoved(commonComponent, saveComponent, handheldDs));
            Assert.False(service.IsComponentRemoved(commonComponent, saveComponent, new EditorOverrideScope("ps1")));
        }
```

- [ ] **Step 2: Run to verify it fails**

Filter `EntityEnvironmentOverrideEditingServiceTests`. Expected: `platformComponent.FarPlaneDistance` is `100f` (the group step is treated as a platform id and finds nothing).

- [ ] **Step 3: Rewrite the scope methods**

Apply these replacements in `ComponentPlatformEditingService.cs`:

`ResolveEditableComponent(common, save, string platformId)` body → guards, then `return ResolveEditableComponent(commonComponent, saveComponent, EditorOverrideScope.ForPlatform(platformId));`.

`ResolveEditableComponent(common, save, EditorOverrideScope scope)`:

```csharp
            if (commonComponent == null) {
                throw new ArgumentNullException(nameof(commonComponent));
            }
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }
            if (scope.IsCommon) {
                return commonComponent;
            }

            Component parentComponent = ResolveEditableComponent(commonComponent, saveComponent, scope.Parent);
            if (!saveComponent.TryGetComponentState(commonComponent, out EntityComponentSaveState saveState)
                || !saveState.TryGetScopedPlatformOverride(scope, out EntityComponentPlatformOverrideState overrideState)) {
                return parentComponent;
            }

            Component snapshot = GetOrLoadOverrideSnapshotComponent(commonComponent, scope, overrideState);
            if (snapshot == null) {
                return parentComponent;
            }
            if (!overrideState.HasAnyPropertyOverrides) {
                return snapshot;
            }

            return BuildEditableComponent(parentComponent, snapshot, overrideState);
```

`EnsureScopeOverrideComponent`: drop the `IsPlatformOnly` branch; `if (scope.IsCommon) return commonComponent;` then `Component editable = ResolveEditableComponent(...scope); return ReferenceEquals(editable, commonComponent) ? CloneComponent(commonComponent) : CloneComponent(editable);` — always a clone so edits never touch the parent's cached snapshot. `EnsurePlatformOverrideComponent(string)` delegates.

`MarkScopePropertyOverride`: replace the `CommonPlatformId` string check with `if (scope.IsCommon) return;`. `MarkPropertyOverride(string)` delegates.

`PersistScopeOverride`: drop the `IsPlatformOnly` branch; add `if (scope.IsCommon) throw new InvalidOperationException("Common component state should not be persisted as an override.");`. `PersistPlatformOverride(string)` delegates. `GetOrCreatePlatformOverrideState(saveState, platformId)` callers use `componentSaveState.GetOrCreateScopedPlatformOverride(scope)`.

`StoreScopeAssetReference`: drop the `IsPlatformOnly` branch; `if (scope.IsCommon) { saveComponent.SetAssetReference(commonComponent, referenceName, assetReference); return; }`. `StoreAssetReference(string)` delegates.

`IsScopePropertyOverrideActive`: drop the branch; `if (scope.IsCommon) return false;`; the final comparison reads the parent: `ReadPropertyPathValue(ResolveEditableComponent(commonComponent, saveComponent, scope.Parent), propertyPath)`. `IsPropertyOverrideActive(string)` delegates.

`ClearScopePropertyOverride`: drop the branch; `if (scope.IsCommon) return;`; cache clear uses `ClearCachedOverrideComponent(commonComponent, scope.ToString())`. `ClearPropertyOverride(string)` delegates.

`IsComponentRemoved(scope)`:

```csharp
            if (scope.IsCommon) {
                return false;
            }

            string componentKey = EnsureComponentKey(commonComponent, saveComponent);
            if (saveComponent.TryGetComponentPlatformOverride(scope, out EntityPlatformComponentOverrideState overrideState)
                && overrideState.IsComponentRemoved(componentKey)) {
                return true;
            }

            return IsComponentRemoved(commonComponent, saveComponent, scope.Parent);
```

`GetAddedComponents(scope)`: `if (scope.IsCommon) return Array.Empty<...>();` then walk prefixes depth 1..Depth (same prefix loop as Task 5 `FoldScopeTransform`) calling `AddAddedComponentsForScope(saveComponent, prefix, addedByKey)`; return `addedByKey.Values.ToArray()`.

`AddScopeOnlyComponent`: drop the branch; `if (scope.IsCommon) throw new InvalidOperationException("Scoped components cannot be added on the common tab.");`.

`RemoveComponent(scope)`: drop the branch; add the Common throw; replace `ClearCachedOverrideComponent(component, platformId)` in the string version by delegating entirely: `return RemoveComponent(component, saveComponent, EditorOverrideScope.ForPlatform(platformId));` and in the scope version add `ClearCachedOverrideComponent(component, scope.ToString());` after `componentSaveState.RemoveScopedPlatformOverride(scope);`.

`RevertComponentExistenceOverride(scope)`: drop the branch (the body already handles one scope). `TryGetAddedComponentState(scope)`: drop the branch; after checking this scope, `if (scope.IsCommon) return false; return TryGetAddedComponentState(component, saveComponent, scope.Parent, out addedComponentState);`. `StoreAddedComponentAssetReference(scope)`: drop the branch.

`BuildEffectiveOverrideSaveState(saveState, scope)`: replace the platform-then-environment block with a prefix loop depth 1..Depth that copies each authored prefix's named asset references in order. Delete the `(saveState, string platformId)` overload. `GetOrLoadOverrideSnapshotComponent(common, string, state)` and `CacheOverrideComponent(common, string, comp)` callers pass `scope.ToString()`; delete the platform-id twins that are no longer called. `RemoveEmptyComponentPlatformOverride(saveComponent, string, state)` becomes the scope version only.

- [ ] **Step 4: Run the suites**

Run: `& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EntityEnvironmentOverrideEditingServiceTests|FullyQualifiedName~ComponentProperty|FullyQualifiedName~PropertiesPanel|FullyQualifiedName~ComponentPlatform|FullyQualifiedName~SceneSave|FullyQualifiedName~SceneLoad" --nologo -v q`
Expected: pass.

- [ ] **Step 5: Commit**

```
git add engine/helengine.editor/managers/scene/ComponentPlatformEditingService.cs engine/helengine.editor.tests/EntityEnvironmentOverrideEditingServiceTests.cs
git commit -m "editor: resolve component overrides by recursing over the scope path"
```

---

### Task 7: Convert the remaining consumers and delete the transitional members

**Files:**
- Modify: `engine/helengine.editor/managers/project/AnimationClipPlatformResolutionService.cs:32-45`
- Modify: `engine/helengine.editor/managers/asset/AssetProcessorSettingsScopeResolver.cs:3-27`
- Modify: `engine/helengine.editor/managers/inspection/ComponentPropertyEditRequest.cs:38-40`, `ComponentPropertyEditController.cs:91,117`
- Modify: `engine/helengine.editor/model/EditorOverrideScope.cs` (delete `PlatformId`, `EnvironmentId`, `IsPlatformOnly`)
- Test: `engine/helengine.editor.tests/AnimationClipEnvironmentResolutionTests.cs`, `AssetProcessorSettingsScopeResolverTests.cs` (add one test each)

**Interfaces:**
- Consumes: `EditorOverrideScope.TryGetStepId`, `IsCommon`.
- Produces: asset-level per-platform settings (animation clips, processor settings) read the Platform and BuildConfig step ids from the path; a path with no Platform step resolves the base asset.

- [ ] **Step 1: Write the failing tests**

In `AnimationClipEnvironmentResolutionTests`:

```csharp
        [Fact]
        public void ResolveForScope_WithGroupOnlyPath_ReturnsTheBaseClip() {
            AnimationClipAsset clip = CreateClipWithPlatformOverride("ds", "debug");
            AnimationClipPlatformResolutionService service = new AnimationClipPlatformResolutionService();

            AnimationClipAsset resolved = service.ResolveForScope(clip, EditorOverrideScope.FromSteps(SceneOverrideScopePath.Group("handheld")));

            Assert.Same(clip, resolved);
        }
```

(`CreateClipWithPlatformOverride` is whatever helper the file already uses to build a clip with one `PlatformOverrides` entry for `ds`/`debug`; reuse it under its existing name.) In `AssetProcessorSettingsScopeResolverTests`:

```csharp
        [Fact]
        public void Resolve_WithGroupChainAbovePlatform_UsesThePlatformAndBuildConfigSteps() {
            AssetProcessorSettings settings = new AssetProcessorSettings();
            settings.Platforms["ds"] = new AssetPlatformProcessorSettings();
            settings.Platforms["ds"].Environments["debug"] = new AssetPlatformProcessorSettings();
            EditorOverrideScope scope = EditorOverrideScope.FromSteps(SceneOverrideScopePath.Group("handheld"))
                .Append(new EditorOverrideScopeStep(SceneOverrideScopeStepKind.Platform, "ds"))
                .Append(new EditorOverrideScopeStep(SceneOverrideScopeStepKind.BuildConfig, "debug"));

            AssetPlatformProcessorSettings resolved = AssetProcessorSettingsScopeResolver.Resolve(settings, scope);

            Assert.NotNull(resolved);
        }
```

- [ ] **Step 2: Run to verify they fail**

Run: `& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AnimationClipEnvironmentResolutionTests|FullyQualifiedName~AssetProcessorSettingsScopeResolverTests" --nologo -v q`
Expected: the animation test throws or returns a resolved copy because `scope.PlatformId` is empty for a group-only path.

- [ ] **Step 3: Rewrite the consumers**

`AnimationClipPlatformResolutionService.ResolveForScope`:

```csharp
        public AnimationClipAsset ResolveForScope(AnimationClipAsset clip, EditorOverrideScope scope) {
            if (clip == null) {
                throw new ArgumentNullException(nameof(clip));
            }
            if (!scope.TryGetStepId(SceneOverrideScopeStepKind.Platform, out string platformId)) {
                return clip;
            }

            AnimationClipAsset platformClip = ResolveForPlatform(clip, platformId);
            if (!scope.TryGetStepId(SceneOverrideScopeStepKind.BuildConfig, out string environmentId)) {
                return platformClip;
            }

            AnimationClipPlatformOverrideAsset environmentOverride = ResolvePlatformOverride(clip, platformId, environmentId);
            if (environmentOverride == null || environmentOverride.Mode == AnimationClipPlatformOverrideMode.InheritBase) {
                return platformClip;
            }
            if (environmentOverride.Mode == AnimationClipPlatformOverrideMode.ReplaceWholeClip) {
                return ResolveReplaceWholeClip(platformClip, environmentOverride);
            }

            return ResolveOverrideFrames(platformClip, environmentOverride);
        }
```

`AssetProcessorSettingsScopeResolver.Resolve`: replace the first two blocks with

```csharp
            AssetPlatformProcessorSettings platformSettings = null;
            if (settings.Platforms != null && scope.TryGetStepId(SceneOverrideScopeStepKind.Platform, out string platformId)) {
                settings.Platforms.TryGetValue(platformId, out platformSettings);
            }

            AssetPlatformProcessorSettings effective = ClonePlatform(platformSettings);
            if (!scope.TryGetStepId(SceneOverrideScopeStepKind.BuildConfig, out string environmentId)
                || platformSettings?.Environments == null
                || !platformSettings.Environments.TryGetValue(environmentId, out AssetPlatformProcessorSettings environmentSettings)
                || environmentSettings == null) {
                return effective;
            }
```

`ComponentPropertyEditRequest`: delete the `string.IsNullOrWhiteSpace(scope.PlatformId)` guard (every scope value is valid). `ComponentPropertyEditController`: `bool isCommonScope = request.Scope.IsCommon;` and `if (request.Scope.IsCommon) return;`.

Then delete `PlatformId`, `EnvironmentId` and `IsPlatformOnly` from `EditorOverrideScope.cs`. Build the editor; fix every remaining compile error by the same rule: `scope.IsPlatformOnly` → `scope.Depth == 1` only where the code genuinely asks "is this the first level", otherwise restructure as parent recursion; `scope.PlatformId` comparisons with `CommonPlatformId` → `scope.IsCommon`; other `scope.PlatformId`/`scope.EnvironmentId` reads → `TryGetStepId`. Expected remaining sites after Tasks 4–6: none in production; tests use `.ToString()`.

- [ ] **Step 4: Run the whole editor suite**

Run: `& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.editor.tests/helengine.editor.tests.csproj --nologo -v q`
Expected: pass. If `DemoDiscAuthoringDeterminismTests` fails, it regenerates fixtures from the generator; read its failure text — it must be about the new payload bytes only, and its fixture refresh path is documented in the test's own summary.

- [ ] **Step 5: Commit**

```
git add engine/helengine.editor engine/helengine.editor.tests
git commit -m "editor: read platform and build-config ids from the scope path and drop the id pair"
```

### Task 8: Level order rules and relocation planning

Pure logic, no UI. The second plan's level-order control calls `EditorOverrideScopeRelocationPlanner` and shows `Dropped` before applying.

**Files:**
- Create: `engine/helengine.editor/model/EditorOverrideLevelOrder.cs`
- Create: `engine/helengine.editor/model/EditorOverrideScopeRelocation.cs`
- Create: `engine/helengine.editor/model/EditorOverrideScopeRelocationPlan.cs`
- Create: `engine/helengine.editor/model/EditorOverrideScopeRelocationPlanner.cs`
- Test: `engine/helengine.editor.tests/EditorOverrideLevelOrderTests.cs`

**Interfaces:**
- Produces: `static class EditorOverrideLevelOrder { IReadOnlyList<SceneOverrideScopeStepKind> Default; void Validate(IReadOnlyList<SceneOverrideScopeStepKind> order); bool IsValidPath(IReadOnlyList<SceneOverrideScopeStepKind> order, EditorOverrideScope scope); string Describe(IReadOnlyList<SceneOverrideScopeStepKind> order); }`
- Produces: `sealed class EditorOverrideScopeRelocation { EditorOverrideScope Source; EditorOverrideScope Target; }`, `sealed class EditorOverrideScopeRelocationPlan { List<EditorOverrideScopeRelocation> Relocated; List<EditorOverrideScope> Dropped; bool HasDrops; }`, `static class EditorOverrideScopeRelocationPlanner { EditorOverrideScopeRelocationPlan Plan(IReadOnlyList<kind> newOrder, IEnumerable<EditorOverrideScope> authored); bool TryRelocate(IReadOnlyList<kind> newOrder, EditorOverrideScope scope, out EditorOverrideScope target); void Apply(EntitySaveComponent saveComponent, EditorOverrideScopeRelocationPlan plan); }`.

- [ ] **Step 1: Write the failing tests**

```csharp
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies level-order validation, path-shape checks and relocation planning.
    /// </summary>
    public sealed class EditorOverrideLevelOrderTests {
        static readonly EditorOverrideScopeStep Handheld = new EditorOverrideScopeStep(SceneOverrideScopeStepKind.Group, "handheld");
        static readonly EditorOverrideScopeStep Portable = new EditorOverrideScopeStep(SceneOverrideScopeStepKind.Group, "portable");
        static readonly EditorOverrideScopeStep Ds = new EditorOverrideScopeStep(SceneOverrideScopeStepKind.Platform, "ds");
        static readonly EditorOverrideScopeStep Debug = new EditorOverrideScopeStep(SceneOverrideScopeStepKind.BuildConfig, "debug");

        [Fact]
        public void Default_IsPlatformThenBuildConfig() {
            Assert.Equal(new[] { SceneOverrideScopeStepKind.Platform, SceneOverrideScopeStepKind.BuildConfig }, EditorOverrideLevelOrder.Default);
        }

        [Fact]
        public void Validate_RejectsARepeatedKind() {
            Assert.Throws<InvalidOperationException>(() => EditorOverrideLevelOrder.Validate(new[] { SceneOverrideScopeStepKind.Platform, SceneOverrideScopeStepKind.Platform }));
            EditorOverrideLevelOrder.Validate(Array.Empty<SceneOverrideScopeStepKind>());
        }

        [Fact]
        public void IsValidPath_AcceptsPrefixesAndGroupChainsAndSkipsAnEmptyGroupLevel() {
            SceneOverrideScopeStepKind[] order = { SceneOverrideScopeStepKind.Group, SceneOverrideScopeStepKind.Platform, SceneOverrideScopeStepKind.BuildConfig };

            Assert.True(EditorOverrideLevelOrder.IsValidPath(order, EditorOverrideScope.Common));
            Assert.True(EditorOverrideLevelOrder.IsValidPath(order, EditorOverrideScope.Common.Append(Handheld).Append(Portable).Append(Ds).Append(Debug)));
            Assert.True(EditorOverrideLevelOrder.IsValidPath(order, EditorOverrideScope.Common.Append(Ds)));
            Assert.False(EditorOverrideLevelOrder.IsValidPath(order, EditorOverrideScope.Common.Append(Debug)));
            Assert.False(EditorOverrideLevelOrder.IsValidPath(order, EditorOverrideScope.Common.Append(Ds).Append(Handheld)));
            Assert.False(EditorOverrideLevelOrder.IsValidPath(Array.Empty<SceneOverrideScopeStepKind>(), EditorOverrideScope.Common.Append(Ds)));
        }

        [Fact]
        public void Plan_RelocatesReorderedStepsAndDropsPathsWhoseKindWasRemoved() {
            SceneOverrideScopeStepKind[] newOrder = { SceneOverrideScopeStepKind.BuildConfig, SceneOverrideScopeStepKind.Platform };
            EditorOverrideScope dsDebug = EditorOverrideScope.Common.Append(Ds).Append(Debug);
            EditorOverrideScope handheldDs = EditorOverrideScope.Common.Append(Handheld).Append(Ds);
            EditorOverrideScope dsOnly = EditorOverrideScope.Common.Append(Ds);

            EditorOverrideScopeRelocationPlan plan = EditorOverrideScopeRelocationPlanner.Plan(newOrder, new[] { dsDebug, handheldDs, dsOnly, EditorOverrideScope.Common });

            EditorOverrideScopeRelocation relocated = Assert.Single(plan.Relocated, item => item.Source == dsDebug);
            Assert.Equal(EditorOverrideScope.Common.Append(Debug).Append(Ds), relocated.Target);
            Assert.Contains(plan.Dropped, item => item == handheldDs);
            Assert.Contains(plan.Dropped, item => item == dsOnly);
            Assert.DoesNotContain(plan.Dropped, item => item.IsCommon);
            Assert.True(plan.HasDrops);
        }

        [Fact]
        public void Apply_MovesExistenceTransformAndComponentEntriesAndDeletesDrops() {
            EntitySaveComponent saveComponent = new EntitySaveComponent();
            EditorOverrideScope dsDebug = EditorOverrideScope.Common.Append(Ds).Append(Debug);
            EditorOverrideScope debugDs = EditorOverrideScope.Common.Append(Debug).Append(Ds);
            EditorOverrideScope handheldDs = EditorOverrideScope.Common.Append(Handheld).Append(Ds);
            saveComponent.SetExistencePlatformOverride(dsDebug, new SceneEntityPlatformExistenceOverrideAsset { Exists = false });
            saveComponent.SetTransformPlatformOverride(handheldDs, new SceneEntityPlatformTransformOverrideAsset { HasLocalScaleOverride = true, LocalScale = float3.One });
            saveComponent.GetOrCreateComponentPlatformOverride(dsDebug).MarkComponentRemoved("k");
            EditorOverrideScopeRelocationPlan plan = EditorOverrideScopeRelocationPlanner.Plan(
                new[] { SceneOverrideScopeStepKind.BuildConfig, SceneOverrideScopeStepKind.Platform },
                new[] { dsDebug, handheldDs });

            EditorOverrideScopeRelocationPlanner.Apply(saveComponent, plan);

            Assert.False(saveComponent.TryGetExistencePlatformOverride(dsDebug, out _));
            Assert.True(saveComponent.TryGetExistencePlatformOverride(debugDs, out SceneEntityPlatformExistenceOverrideAsset moved));
            Assert.Equal("buildconfig:debug/platform:ds", SceneOverrideScopePath.Format(moved.Scope));
            Assert.False(saveComponent.TryGetTransformPlatformOverride(handheldDs, out _));
            Assert.True(saveComponent.TryGetComponentPlatformOverride(debugDs, out EntityPlatformComponentOverrideState component));
            Assert.Equal(debugDs, component.Scope);
            Assert.Equal(new[] { SceneOverrideScopeStepKind.BuildConfig, SceneOverrideScopeStepKind.Platform }, saveComponent.OverrideLevelOrder);
        }
    }
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorOverrideLevelOrderTests" --nologo -v q`
Expected: build errors for the four new types.

- [ ] **Step 3: Write the four files**

`EditorOverrideLevelOrder.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// Rules for an entity's override level order: which kinds may follow Common and in what sequence a path may visit them.
    /// </summary>
    public static class EditorOverrideLevelOrder {
        /// <summary>
        /// Order used when neither the entity nor the project settings state one: Platform then Build Config.
        /// </summary>
        public static readonly IReadOnlyList<SceneOverrideScopeStepKind> Default = new[] {
            SceneOverrideScopeStepKind.Platform,
            SceneOverrideScopeStepKind.BuildConfig
        };

        /// <summary>
        /// Rejects an order that lists one kind twice. The empty order (Common only) is valid.
        /// </summary>
        public static void Validate(IReadOnlyList<SceneOverrideScopeStepKind> order) {
            if (order == null) {
                throw new ArgumentNullException(nameof(order));
            }

            for (int outer = 0; outer < order.Count; outer++) {
                for (int inner = outer + 1; inner < order.Count; inner++) {
                    if (order[outer] == order[inner]) {
                        throw new InvalidOperationException($"Override level order lists '{SceneOverrideScopePath.FormatKind(order[outer])}' more than once.");
                    }
                }
            }
        }

        /// <summary>
        /// Returns whether a path is a prefix walk of the order. A Group level may hold zero or more consecutive group
        /// steps (an ungrouped platform contributes none); every other level holds exactly one step.
        /// </summary>
        public static bool IsValidPath(IReadOnlyList<SceneOverrideScopeStepKind> order, EditorOverrideScope scope) {
            if (order == null) {
                throw new ArgumentNullException(nameof(order));
            }

            int level = 0;
            for (int index = 0; index < scope.Depth; index++) {
                SceneOverrideScopeStepKind kind = scope.Steps[index].Kind;
                while (level < order.Count && order[level] == SceneOverrideScopeStepKind.Group && kind != SceneOverrideScopeStepKind.Group) {
                    level++;
                }
                if (level >= order.Count || order[level] != kind) {
                    return false;
                }
                if (kind != SceneOverrideScopeStepKind.Group) {
                    level++;
                }
            }

            return true;
        }

        /// <summary>
        /// Formats an order as <c>Common → Platform → Build Config</c>.
        /// </summary>
        public static string Describe(IReadOnlyList<SceneOverrideScopeStepKind> order) {
            System.Text.StringBuilder builder = new System.Text.StringBuilder("Common");
            for (int index = 0; index < (order?.Count ?? 0); index++) {
                builder.Append(" \u2192 ");
                builder.Append(order[index] == SceneOverrideScopeStepKind.BuildConfig ? "Build Config" : order[index].ToString());
            }

            return builder.ToString();
        }
    }
}
```

`EditorOverrideScopeRelocation.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// One authored path and the path it moves to under a new level order.
    /// </summary>
    public sealed class EditorOverrideScopeRelocation {
        /// <summary>Gets or sets the path as authored under the previous order.</summary>
        public EditorOverrideScope Source { get; set; }

        /// <summary>Gets or sets the equivalent path under the new order.</summary>
        public EditorOverrideScope Target { get; set; }
    }
}
```

`EditorOverrideScopeRelocationPlan.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// Result of planning a level-order change: paths that move and paths that have no home in the new order.
    /// </summary>
    public sealed class EditorOverrideScopeRelocationPlan {
        /// <summary>Gets or sets the new level order the plan targets.</summary>
        public IReadOnlyList<SceneOverrideScopeStepKind> NewOrder { get; set; }

        /// <summary>Gets the paths that move, excluding paths that stay where they are.</summary>
        public List<EditorOverrideScopeRelocation> Relocated { get; } = new List<EditorOverrideScopeRelocation>();

        /// <summary>Gets the paths that must be deleted because a step kind was removed or the shape is invalid.</summary>
        public List<EditorOverrideScope> Dropped { get; } = new List<EditorOverrideScope>();

        /// <summary>Gets whether applying the plan deletes authored data; the UI must confirm first.</summary>
        public bool HasDrops => Dropped.Count > 0;
    }
}
```

`EditorOverrideScopeRelocationPlanner.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// Plans and applies the relocation of authored override paths when an entity's level order changes.
    /// </summary>
    public static class EditorOverrideScopeRelocationPlanner {
        /// <summary>
        /// Maps each authored path onto the new order: steps whose kind survives keep their id and are re-sequenced by
        /// the new order; a path whose kind was removed, or whose result is not a valid prefix walk, is dropped.
        /// </summary>
        public static EditorOverrideScopeRelocationPlan Plan(IReadOnlyList<SceneOverrideScopeStepKind> newOrder, IEnumerable<EditorOverrideScope> authored) {
            if (authored == null) {
                throw new ArgumentNullException(nameof(authored));
            }

            EditorOverrideLevelOrder.Validate(newOrder);
            EditorOverrideScopeRelocationPlan plan = new EditorOverrideScopeRelocationPlan { NewOrder = newOrder };
            HashSet<EditorOverrideScope> seen = new HashSet<EditorOverrideScope>();
            foreach (EditorOverrideScope scope in authored) {
                if (scope.IsCommon || !seen.Add(scope)) {
                    continue;
                }
                if (!TryRelocate(newOrder, scope, out EditorOverrideScope target)) {
                    plan.Dropped.Add(scope);
                    continue;
                }
                if (target != scope) {
                    plan.Relocated.Add(new EditorOverrideScopeRelocation { Source = scope, Target = target });
                }
            }

            return plan;
        }

        /// <summary>
        /// Computes the equivalent of one path under a new order.
        /// </summary>
        public static bool TryRelocate(IReadOnlyList<SceneOverrideScopeStepKind> newOrder, EditorOverrideScope scope, out EditorOverrideScope target) {
            if (newOrder == null) {
                throw new ArgumentNullException(nameof(newOrder));
            }

            List<EditorOverrideScopeStep> steps = new List<EditorOverrideScopeStep>(scope.Depth);
            for (int level = 0; level < newOrder.Count; level++) {
                for (int index = 0; index < scope.Depth; index++) {
                    if (scope.Steps[index].Kind == newOrder[level]) {
                        steps.Add(scope.Steps[index]);
                    }
                }
            }

            if (steps.Count != scope.Depth) {
                target = EditorOverrideScope.Common;
                return false;
            }

            target = new EditorOverrideScope(steps);
            return EditorOverrideLevelOrder.IsValidPath(newOrder, target);
        }

        /// <summary>
        /// Moves existence, transform and component-existence entries on the entity, deletes dropped ones, and records the new order.
        /// Component property overrides stored per component are moved by the same rule.
        /// </summary>
        public static void Apply(EntitySaveComponent saveComponent, EditorOverrideScopeRelocationPlan plan) {
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }
            if (plan == null) {
                throw new ArgumentNullException(nameof(plan));
            }

            for (int index = 0; index < plan.Dropped.Count; index++) {
                EditorOverrideScope dropped = plan.Dropped[index];
                saveComponent.RemoveExistencePlatformOverride(dropped);
                saveComponent.RemoveTransformPlatformOverride(dropped);
                saveComponent.RemoveComponentPlatformOverride(dropped);
                foreach (EntityComponentSaveState componentState in saveComponent.EnumerateComponentStates()) {
                    componentState.RemoveScopedPlatformOverride(dropped);
                }
            }

            for (int index = 0; index < plan.Relocated.Count; index++) {
                EditorOverrideScope source = plan.Relocated[index].Source;
                EditorOverrideScope target = plan.Relocated[index].Target;
                if (saveComponent.TryGetExistencePlatformOverride(source, out SceneEntityPlatformExistenceOverrideAsset existence)) {
                    saveComponent.RemoveExistencePlatformOverride(source);
                    saveComponent.SetExistencePlatformOverride(target, existence);
                }
                if (saveComponent.TryGetTransformPlatformOverride(source, out SceneEntityPlatformTransformOverrideAsset transform)) {
                    saveComponent.RemoveTransformPlatformOverride(source);
                    saveComponent.SetTransformPlatformOverride(target, transform);
                }
                if (saveComponent.TryGetComponentPlatformOverride(source, out EntityPlatformComponentOverrideState componentOverride)) {
                    saveComponent.RemoveComponentPlatformOverride(source);
                    componentOverride.Scope = target;
                    saveComponent.SetComponentPlatformOverride(target, componentOverride);
                }
                foreach (EntityComponentSaveState componentState in saveComponent.EnumerateComponentStates()) {
                    if (componentState.TryGetScopedPlatformOverride(source, out EntityComponentPlatformOverrideState propertyOverride)) {
                        componentState.RemoveScopedPlatformOverride(source);
                        componentState.SetScopedPlatformOverride(target, propertyOverride);
                    }
                }
            }

            saveComponent.OverrideLevelOrder = plan.NewOrder;
        }
    }
}
```

If `EntitySaveComponent` lacks `RemoveComponentPlatformOverride(EditorOverrideScope)` or `SetComponentPlatformOverride(EditorOverrideScope, EntityPlatformComponentOverrideState)`, add them next to `GetOrCreateComponentPlatformOverride(EditorOverrideScope)` as one-line wrappers over `ComponentOverridesByScope.Remove(scope)` and `.Set(scope, state)` (setting `state.Scope = scope` first).

- [ ] **Step 4: Run to verify they pass**

Same command. Expected: 5 passed.

- [ ] **Step 5: Commit**

```
git add engine/helengine.editor/model/EditorOverrideLevelOrder.cs engine/helengine.editor/model/EditorOverrideScopeRelocation.cs engine/helengine.editor/model/EditorOverrideScopeRelocationPlan.cs engine/helengine.editor/model/EditorOverrideScopeRelocationPlanner.cs engine/helengine.editor/components/persistence/EntitySaveComponent.cs engine/helengine.editor.tests/EditorOverrideLevelOrderTests.cs
git commit -m "editor: add level-order rules and scope relocation planning"
```

### Task 9: Platform groups as project settings

**Files:**
- Create: `engine/helengine.editor/managers/project/EditorProjectPlatformGroupDefinition.cs`
- Create: `engine/helengine.editor/managers/project/EditorProjectPlatformGroupsDocument.cs`
- Create: `engine/helengine.editor/managers/project/EditorProjectPlatformGroupsService.cs`
- Test: `engine/helengine.editor.tests/EditorProjectPlatformGroupsServiceTests.cs`

**Interfaces:**
- Produces: `sealed class EditorProjectPlatformGroupDefinition { string Id; string DisplayName; List<string> PlatformIds; List<EditorProjectPlatformGroupDefinition> Children; }`
- Produces: `sealed class EditorProjectPlatformGroupsDocument { List<EditorProjectPlatformGroupDefinition> Groups; List<SceneOverrideScopeStepKind> DefaultLevelOrder; }`
- Produces: `sealed class EditorProjectPlatformGroupsService(string projectRootPath)` with `const string SettingsFileName = "platform-groups.json"`, `Load()`, `Save(document)`, `AddGroup(document, parentGroupId, groupId)`, `RenameGroup(document, groupId, newGroupId)`, `DeleteGroup(document, groupId)`, `AssignPlatform(document, groupId, platformId)`, `UnassignPlatform(document, platformId)`, `Validate(document, IReadOnlyList<string> supportedPlatformIds)`, `static EditorProjectPlatformGroupDefinition FindGroup(document, groupId)`, `static IReadOnlyList<string> FindGroupChain(document, platformId)` (outermost first, empty when ungrouped), `static IReadOnlyList<string> CollectGroupIds(document)`.

- [ ] **Step 1: Write the failing tests**

```csharp
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the project platform group settings service.
    /// </summary>
    public sealed class EditorProjectPlatformGroupsServiceTests : IDisposable {
        readonly string ProjectRootPath;

        public EditorProjectPlatformGroupsServiceTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-platform-groups-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ProjectRootPath);
        }

        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        [Fact]
        public void Load_WhenFileIsMissing_SeedsAnEmptyTreeWithTheDefaultOrder() {
            EditorProjectPlatformGroupsService service = new EditorProjectPlatformGroupsService(ProjectRootPath);

            EditorProjectPlatformGroupsDocument document = service.Load();

            Assert.Empty(document.Groups);
            Assert.Equal(EditorOverrideLevelOrder.Default, document.DefaultLevelOrder);
            Assert.True(File.Exists(Path.Combine(ProjectRootPath, "settings", "platform-groups.json")));
        }

        [Fact]
        public void AddAssignAndSave_RoundTripsANestedTree() {
            EditorProjectPlatformGroupsService service = new EditorProjectPlatformGroupsService(ProjectRootPath);
            EditorProjectPlatformGroupsDocument document = service.Load();

            service.AddGroup(document, null, "consoles");
            service.AddGroup(document, "consoles", "handheld");
            service.AssignPlatform(document, "handheld", "ds");
            service.AssignPlatform(document, "handheld", "psp");
            service.AssignPlatform(document, "consoles", "ps1");
            service.Save(document);

            EditorProjectPlatformGroupsDocument loaded = service.Load();
            EditorProjectPlatformGroupDefinition consoles = Assert.Single(loaded.Groups);
            Assert.Equal("consoles", consoles.Id);
            Assert.Equal(new[] { "ps1" }, consoles.PlatformIds);
            Assert.Equal(new[] { "ds", "psp" }, Assert.Single(consoles.Children).PlatformIds);
            Assert.Equal(new[] { "consoles", "handheld" }, EditorProjectPlatformGroupsService.FindGroupChain(loaded, "PSP"));
            Assert.Empty(EditorProjectPlatformGroupsService.FindGroupChain(loaded, "windows"));
        }

        [Fact]
        public void AssignPlatform_MovesThePlatformOutOfItsPreviousGroup() {
            EditorProjectPlatformGroupsService service = new EditorProjectPlatformGroupsService(ProjectRootPath);
            EditorProjectPlatformGroupsDocument document = service.Load();
            service.AddGroup(document, null, "a");
            service.AddGroup(document, null, "b");
            service.AssignPlatform(document, "a", "ds");

            service.AssignPlatform(document, "b", "ds");

            Assert.Empty(EditorProjectPlatformGroupsService.FindGroup(document, "a").PlatformIds);
            Assert.Equal(new[] { "b" }, EditorProjectPlatformGroupsService.FindGroupChain(document, "ds"));
        }

        [Fact]
        public void AddGroup_RejectsIdsThatCollideWithGroupsOrArePlatformsOnValidate() {
            EditorProjectPlatformGroupsService service = new EditorProjectPlatformGroupsService(ProjectRootPath);
            EditorProjectPlatformGroupsDocument document = service.Load();
            service.AddGroup(document, null, "handheld");

            Assert.Throws<InvalidOperationException>(() => service.AddGroup(document, null, "Handheld"));
            Assert.Throws<ArgumentException>(() => service.AddGroup(document, null, " "));

            service.AddGroup(document, null, "ps1");
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => service.Validate(document, new[] { "ps1", "ds" }));
            Assert.Contains("ps1", error.Message);
        }

        [Fact]
        public void Validate_NamesBothGroupsWhenAPlatformAppearsTwice() {
            EditorProjectPlatformGroupsDocument document = new EditorProjectPlatformGroupsDocument {
                Groups = [
                    new EditorProjectPlatformGroupDefinition { Id = "a", PlatformIds = ["ds"] },
                    new EditorProjectPlatformGroupDefinition { Id = "b", PlatformIds = ["DS"] }
                ]
            };

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new EditorProjectPlatformGroupsService(ProjectRootPath).Validate(document, new[] { "ds" }));

            Assert.Contains("'a'", error.Message);
            Assert.Contains("'b'", error.Message);
            Assert.Contains("ds", error.Message);
        }

        [Fact]
        public void RenameAndDelete_UpdateTheTree() {
            EditorProjectPlatformGroupsService service = new EditorProjectPlatformGroupsService(ProjectRootPath);
            EditorProjectPlatformGroupsDocument document = service.Load();
            service.AddGroup(document, null, "consoles");
            service.AddGroup(document, "consoles", "handheld");
            service.AssignPlatform(document, "handheld", "ds");

            service.RenameGroup(document, "handheld", "portable");
            Assert.Equal(new[] { "consoles", "portable" }, EditorProjectPlatformGroupsService.FindGroupChain(document, "ds"));

            service.DeleteGroup(document, "consoles");
            Assert.Empty(document.Groups);
            Assert.Empty(EditorProjectPlatformGroupsService.FindGroupChain(document, "ds"));
        }
    }
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorProjectPlatformGroupsServiceTests" --nologo -v q`
Expected: build errors for the three new types.

- [ ] **Step 3: Write the document types**

`EditorProjectPlatformGroupDefinition.cs`:

```csharp
namespace helengine.editor {
    /// <summary>
    /// One platform group: its member platforms and its nested child groups.
    /// </summary>
    public sealed class EditorProjectPlatformGroupDefinition {
        /// <summary>Gets or sets the stable group id, unique across the tree and distinct from every platform id.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Gets or sets the label shown in the editor; falls back to <see cref="Id"/> when blank.</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Gets or sets the platforms that belong directly to this group.</summary>
        public List<string> PlatformIds { get; set; } = [];

        /// <summary>Gets or sets the nested groups.</summary>
        public List<EditorProjectPlatformGroupDefinition> Children { get; set; } = [];
    }
}
```

`EditorProjectPlatformGroupsDocument.cs`:

```csharp
namespace helengine.editor {
    /// <summary>
    /// Stores the platform group tree and the project's default override level order, persisted in <c>settings/platform-groups.json</c>.
    /// </summary>
    public sealed class EditorProjectPlatformGroupsDocument {
        /// <summary>Gets or sets the root groups.</summary>
        public List<EditorProjectPlatformGroupDefinition> Groups { get; set; } = [];

        /// <summary>Gets or sets the level order new entities take beneath Common.</summary>
        public List<SceneOverrideScopeStepKind> DefaultLevelOrder { get; set; } = [];
    }
}
```

- [ ] **Step 4: Write the service**

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace helengine.editor {
    /// <summary>
    /// Loads, edits, validates and persists the project platform group tree in <c>settings/platform-groups.json</c>.
    /// Shaped like <see cref="EditorProjectEnvironmentsService"/>: mutation methods edit a document in memory and the caller saves.
    /// </summary>
    public sealed class EditorProjectPlatformGroupsService {
        /// <summary>File name beneath <c>settings/</c>.</summary>
        public const string SettingsFileName = "platform-groups.json";

        static JsonSerializerOptions JsonSerializerOptions { get; } = new() {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        readonly string ProjectRootPath;

        string SettingsFilePath => Path.Combine(ProjectRootPath, "settings", SettingsFileName);

        /// <summary>
        /// Initializes the service for one project root.
        /// </summary>
        public EditorProjectPlatformGroupsService(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
        }

        /// <summary>
        /// Loads the document, seeding an empty tree with the default level order when the file is missing or malformed.
        /// </summary>
        public EditorProjectPlatformGroupsDocument Load() {
            EditorProjectPlatformGroupsDocument document = TryLoadDocument();
            if (document == null) {
                document = new EditorProjectPlatformGroupsDocument();
                Normalize(document);
                Save(document);
                return document;
            }

            Normalize(document);
            return document;
        }

        /// <summary>
        /// Normalizes and writes the document.
        /// </summary>
        public void Save(EditorProjectPlatformGroupsDocument document) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }

            Normalize(document);
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath));
            File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(document, JsonSerializerOptions));
        }

        /// <summary>
        /// Adds a group at the root (null parent) or beneath an existing group. Rejects blank or duplicate ids.
        /// </summary>
        public void AddGroup(EditorProjectPlatformGroupsDocument document, string parentGroupId, string groupId) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }
            if (string.IsNullOrWhiteSpace(groupId)) {
                throw new ArgumentException("Group id must be provided.", nameof(groupId));
            }

            string normalizedId = groupId.Trim();
            if (FindGroup(document, normalizedId) != null) {
                throw new InvalidOperationException($"Platform group '{normalizedId}' already exists.");
            }

            EditorProjectPlatformGroupDefinition group = new EditorProjectPlatformGroupDefinition { Id = normalizedId, DisplayName = normalizedId };
            if (string.IsNullOrWhiteSpace(parentGroupId)) {
                document.Groups.Add(group);
                return;
            }

            EditorProjectPlatformGroupDefinition parent = FindGroup(document, parentGroupId)
                ?? throw new InvalidOperationException($"Platform group '{parentGroupId}' was not found.");
            parent.Children.Add(group);
        }

        /// <summary>
        /// Renames a group; the display name follows when it equalled the old id.
        /// </summary>
        public void RenameGroup(EditorProjectPlatformGroupsDocument document, string groupId, string newGroupId) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }
            if (string.IsNullOrWhiteSpace(newGroupId)) {
                throw new ArgumentException("Group id must be provided.", nameof(newGroupId));
            }

            EditorProjectPlatformGroupDefinition group = FindGroup(document, groupId)
                ?? throw new InvalidOperationException($"Platform group '{groupId}' was not found.");
            string normalizedId = newGroupId.Trim();
            EditorProjectPlatformGroupDefinition existing = FindGroup(document, normalizedId);
            if (existing != null && !ReferenceEquals(existing, group)) {
                throw new InvalidOperationException($"Platform group '{normalizedId}' already exists.");
            }

            if (string.Equals(group.DisplayName, group.Id, StringComparison.Ordinal)) {
                group.DisplayName = normalizedId;
            }
            group.Id = normalizedId;
        }

        /// <summary>
        /// Deletes a group and its subtree; member platforms become ungrouped.
        /// </summary>
        public void DeleteGroup(EditorProjectPlatformGroupsDocument document, string groupId) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }
            if (!RemoveGroup(document.Groups, groupId)) {
                throw new InvalidOperationException($"Platform group '{groupId}' was not found.");
            }
        }

        /// <summary>
        /// Puts a platform in one group, removing it from any other group first.
        /// </summary>
        public void AssignPlatform(EditorProjectPlatformGroupsDocument document, string groupId, string platformId) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            EditorProjectPlatformGroupDefinition group = FindGroup(document, groupId)
                ?? throw new InvalidOperationException($"Platform group '{groupId}' was not found.");
            UnassignPlatform(document, platformId);
            group.PlatformIds.Add(platformId.Trim());
        }

        /// <summary>
        /// Removes a platform from whichever group holds it; no-op when ungrouped.
        /// </summary>
        public void UnassignPlatform(EditorProjectPlatformGroupsDocument document, string platformId) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }

            List<EditorProjectPlatformGroupDefinition> all = new List<EditorProjectPlatformGroupDefinition>();
            CollectGroups(document.Groups, all);
            for (int index = 0; index < all.Count; index++) {
                all[index].PlatformIds.RemoveAll(id => string.Equals(id, platformId?.Trim(), StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// Rejects blank ids, duplicate group ids, group ids equal to a supported platform id, and platforms in two groups.
        /// Every message names the offending ids so the build log points at the settings entry to fix.
        /// </summary>
        public void Validate(EditorProjectPlatformGroupsDocument document, IReadOnlyList<string> supportedPlatformIds) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }
            if (supportedPlatformIds == null) {
                throw new ArgumentNullException(nameof(supportedPlatformIds));
            }

            EditorOverrideLevelOrder.Validate(document.DefaultLevelOrder);
            List<EditorProjectPlatformGroupDefinition> all = new List<EditorProjectPlatformGroupDefinition>();
            CollectGroups(document.Groups, all);
            Dictionary<string, string> groupByPlatform = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> groupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < all.Count; index++) {
                EditorProjectPlatformGroupDefinition group = all[index];
                if (string.IsNullOrWhiteSpace(group.Id)) {
                    throw new InvalidOperationException("Platform groups must define a non-blank id.");
                }
                if (!groupIds.Add(group.Id)) {
                    throw new InvalidOperationException($"Platform group id '{group.Id}' is defined more than once.");
                }
                for (int platformIndex = 0; platformIndex < supportedPlatformIds.Count; platformIndex++) {
                    if (string.Equals(group.Id, supportedPlatformIds[platformIndex], StringComparison.OrdinalIgnoreCase)) {
                        throw new InvalidOperationException($"Platform group id '{group.Id}' collides with platform id '{supportedPlatformIds[platformIndex]}'.");
                    }
                }
                for (int platformIndex = 0; platformIndex < group.PlatformIds.Count; platformIndex++) {
                    string platformId = group.PlatformIds[platformIndex];
                    if (groupByPlatform.TryGetValue(platformId, out string otherGroupId)) {
                        throw new InvalidOperationException($"Platform '{platformId}' belongs to groups '{otherGroupId}' and '{group.Id}'; a platform may belong to one group.");
                    }
                    groupByPlatform.Add(platformId, group.Id);
                }
            }
        }

        /// <summary>
        /// Finds a group anywhere in the tree by id.
        /// </summary>
        public static EditorProjectPlatformGroupDefinition FindGroup(EditorProjectPlatformGroupsDocument document, string groupId) {
            if (document == null || string.IsNullOrWhiteSpace(groupId)) {
                return null;
            }

            List<EditorProjectPlatformGroupDefinition> all = new List<EditorProjectPlatformGroupDefinition>();
            CollectGroups(document.Groups, all);
            for (int index = 0; index < all.Count; index++) {
                if (string.Equals(all[index].Id, groupId.Trim(), StringComparison.OrdinalIgnoreCase)) {
                    return all[index];
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the group ids from the root down to the group holding the platform; empty when ungrouped.
        /// </summary>
        public static IReadOnlyList<string> FindGroupChain(EditorProjectPlatformGroupsDocument document, string platformId) {
            List<string> chain = new List<string>();
            if (document == null || string.IsNullOrWhiteSpace(platformId)) {
                return chain;
            }

            return TryBuildChain(document.Groups, platformId.Trim(), chain) ? chain : new List<string>();
        }

        /// <summary>
        /// Returns every group id in depth-first order.
        /// </summary>
        public static IReadOnlyList<string> CollectGroupIds(EditorProjectPlatformGroupsDocument document) {
            List<EditorProjectPlatformGroupDefinition> all = new List<EditorProjectPlatformGroupDefinition>();
            if (document != null) {
                CollectGroups(document.Groups, all);
            }

            List<string> ids = new List<string>(all.Count);
            for (int index = 0; index < all.Count; index++) {
                ids.Add(all[index].Id);
            }

            return ids;
        }

        static bool TryBuildChain(List<EditorProjectPlatformGroupDefinition> groups, string platformId, List<string> chain) {
            for (int index = 0; index < (groups?.Count ?? 0); index++) {
                EditorProjectPlatformGroupDefinition group = groups[index];
                chain.Add(group.Id);
                for (int platformIndex = 0; platformIndex < group.PlatformIds.Count; platformIndex++) {
                    if (string.Equals(group.PlatformIds[platformIndex], platformId, StringComparison.OrdinalIgnoreCase)) {
                        return true;
                    }
                }
                if (TryBuildChain(group.Children, platformId, chain)) {
                    return true;
                }
                chain.RemoveAt(chain.Count - 1);
            }

            return false;
        }

        static void CollectGroups(List<EditorProjectPlatformGroupDefinition> groups, List<EditorProjectPlatformGroupDefinition> into) {
            for (int index = 0; index < (groups?.Count ?? 0); index++) {
                if (groups[index] == null) {
                    continue;
                }
                into.Add(groups[index]);
                CollectGroups(groups[index].Children, into);
            }
        }

        static bool RemoveGroup(List<EditorProjectPlatformGroupDefinition> groups, string groupId) {
            for (int index = 0; index < (groups?.Count ?? 0); index++) {
                if (string.Equals(groups[index].Id, groupId?.Trim(), StringComparison.OrdinalIgnoreCase)) {
                    groups.RemoveAt(index);
                    return true;
                }
                if (RemoveGroup(groups[index].Children, groupId)) {
                    return true;
                }
            }

            return false;
        }

        EditorProjectPlatformGroupsDocument TryLoadDocument() {
            if (!File.Exists(SettingsFilePath)) {
                return null;
            }

            try {
                return JsonSerializer.Deserialize<EditorProjectPlatformGroupsDocument>(File.ReadAllText(SettingsFilePath), JsonSerializerOptions);
            } catch {
                return null;
            }
        }

        static void Normalize(EditorProjectPlatformGroupsDocument document) {
            document.Groups ??= [];
            document.DefaultLevelOrder ??= [];
            if (document.DefaultLevelOrder.Count == 0) {
                document.DefaultLevelOrder.AddRange(EditorOverrideLevelOrder.Default);
            }

            List<EditorProjectPlatformGroupDefinition> all = new List<EditorProjectPlatformGroupDefinition>();
            CollectGroups(document.Groups, all);
            for (int index = 0; index < all.Count; index++) {
                EditorProjectPlatformGroupDefinition group = all[index];
                group.Id = (group.Id ?? string.Empty).Trim();
                group.DisplayName = string.IsNullOrWhiteSpace(group.DisplayName) ? group.Id : group.DisplayName.Trim();
                group.PlatformIds ??= [];
                group.Children ??= [];
                List<string> platforms = [];
                for (int platformIndex = 0; platformIndex < group.PlatformIds.Count; platformIndex++) {
                    string platformId = (group.PlatformIds[platformIndex] ?? string.Empty).Trim();
                    if (platformId.Length > 0 && !platforms.Contains(platformId, StringComparer.OrdinalIgnoreCase)) {
                        platforms.Add(platformId);
                    }
                }
                group.PlatformIds = platforms;
            }
        }
    }
}
```

Give each private static a one-line XML summary (omitted above for length; the file must have them).

- [ ] **Step 5: Run to verify they pass**

Same command. Expected: 6 passed.

- [ ] **Step 6: Commit**

```
git add engine/helengine.editor/managers/project/EditorProjectPlatformGroupDefinition.cs engine/helengine.editor/managers/project/EditorProjectPlatformGroupsDocument.cs engine/helengine.editor/managers/project/EditorProjectPlatformGroupsService.cs engine/helengine.editor.tests/EditorProjectPlatformGroupsServiceTests.cs
git commit -m "editor: add platform group project settings"
```

### Task 10: The shared resolver

**Files:**
- Create: `engine/helengine.editor/managers/scene/EditorOverrideScopeResolver.cs`
- Test: `engine/helengine.editor.tests/EditorOverrideScopeResolverTests.cs`

**Interfaces:**
- Consumes: Task 8 `EditorOverrideLevelOrder`, Task 9 `EditorProjectPlatformGroupsService.FindGroupChain/Validate`.
- Produces: `sealed class EditorOverrideScopeResolver` with ctor `(EditorProjectPlatformGroupsDocument groups, IReadOnlyList<string> supportedPlatformIds)` (validates, throws naming ids), `static EditorOverrideScopeResolver Load(string projectRootPath)`, `IReadOnlyList<SceneOverrideScopeStepKind> ResolveLevelOrder(IReadOnlyList<SceneOverrideScopeStepKind> entityOrder)`, `EditorOverrideScope BuildTargetPath(IReadOnlyList<SceneOverrideScopeStepKind> levelOrder, string platformId, string environmentId)`, `static bool TrySelectDeepest<T>(IReadOnlyList<T> items, Func<T, EditorOverrideScope> scopeSelector, EditorOverrideScope target, out T selected)`.

- [ ] **Step 1: Write the failing tests**

```csharp
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies target-path construction and deepest-prefix selection against a platform group tree.
    /// </summary>
    public sealed class EditorOverrideScopeResolverTests {
        static readonly string[] Platforms = { "windows", "ps1", "ds", "psp" };

        static EditorProjectPlatformGroupsDocument CreateTree() {
            return new EditorProjectPlatformGroupsDocument {
                Groups = [
                    new EditorProjectPlatformGroupDefinition {
                        Id = "consoles",
                        PlatformIds = ["ps1"],
                        Children = [ new EditorProjectPlatformGroupDefinition { Id = "handheld", PlatformIds = ["ds", "psp"] } ]
                    }
                ],
                DefaultLevelOrder = [SceneOverrideScopeStepKind.Platform, SceneOverrideScopeStepKind.BuildConfig]
            };
        }

        [Fact]
        public void BuildTargetPath_UnderDefaultOrder_MatchesThePairConstructor() {
            EditorOverrideScopeResolver resolver = new EditorOverrideScopeResolver(CreateTree(), Platforms);

            Assert.Equal(new EditorOverrideScope("ps1", "debug"), resolver.BuildTargetPath(resolver.ResolveLevelOrder(null), "ps1", "debug"));
            Assert.Equal(new EditorOverrideScope("ps1"), resolver.BuildTargetPath(resolver.ResolveLevelOrder(null), "ps1", ""));
        }

        [Fact]
        public void BuildTargetPath_WithGroupLevel_ExpandsTheChainOutermostFirst() {
            EditorOverrideScopeResolver resolver = new EditorOverrideScopeResolver(CreateTree(), Platforms);
            SceneOverrideScopeStepKind[] order = { SceneOverrideScopeStepKind.Group, SceneOverrideScopeStepKind.Platform, SceneOverrideScopeStepKind.BuildConfig };

            EditorOverrideScope path = resolver.BuildTargetPath(order, "psp", "release");

            Assert.Equal("group:consoles/group:handheld/platform:psp/buildconfig:release", path.ToString());
            Assert.Equal("platform:windows/buildconfig:release", resolver.BuildTargetPath(order, "windows", "release").ToString());
            Assert.Equal("buildconfig:release/platform:psp", resolver.BuildTargetPath(new[] { SceneOverrideScopeStepKind.BuildConfig, SceneOverrideScopeStepKind.Platform }, "psp", "release").ToString());
            Assert.True(resolver.BuildTargetPath(Array.Empty<SceneOverrideScopeStepKind>(), "psp", "release").IsCommon);
        }

        [Fact]
        public void TrySelectDeepest_PicksTheLongestPrefixAndFallsBackToCommon() {
            EditorOverrideScopeResolver resolver = new EditorOverrideScopeResolver(CreateTree(), Platforms);
            SceneOverrideScopeStepKind[] order = { SceneOverrideScopeStepKind.Group, SceneOverrideScopeStepKind.Platform };
            SceneEntityPlatformExistenceOverrideAsset[] overrides = {
                new SceneEntityPlatformExistenceOverrideAsset { Scope = SceneOverrideScopePath.Common(), Exists = false },
                new SceneEntityPlatformExistenceOverrideAsset { Scope = new[] {
                    new SceneOverrideScopeStepAsset { Kind = SceneOverrideScopeStepKind.Group, Id = "consoles" },
                    new SceneOverrideScopeStepAsset { Kind = SceneOverrideScopeStepKind.Group, Id = "handheld" } }, Exists = true }
            };

            Assert.True(EditorOverrideScopeResolver.TrySelectDeepest(overrides, item => EditorOverrideScope.FromSteps(item.Scope), resolver.BuildTargetPath(order, "ds", ""), out SceneEntityPlatformExistenceOverrideAsset selected));
            Assert.True(selected.Exists);
            Assert.True(EditorOverrideScopeResolver.TrySelectDeepest(overrides, item => EditorOverrideScope.FromSteps(item.Scope), resolver.BuildTargetPath(order, "ps1", ""), out selected));
            Assert.False(selected.Exists);
            Assert.False(EditorOverrideScopeResolver.TrySelectDeepest(Array.Empty<SceneEntityPlatformExistenceOverrideAsset>(), item => EditorOverrideScope.FromSteps(item.Scope), EditorOverrideScope.Common, out _));
        }

        [Fact]
        public void Constructor_RejectsInconsistentGroupSettingsNamingBothIds() {
            EditorProjectPlatformGroupsDocument tree = CreateTree();
            tree.Groups.Add(new EditorProjectPlatformGroupDefinition { Id = "other", PlatformIds = ["ds"] });

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new EditorOverrideScopeResolver(tree, Platforms));

            Assert.Contains("'handheld'", error.Message);
            Assert.Contains("'other'", error.Message);
        }
    }
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorOverrideScopeResolverTests" --nologo -v q`
Expected: build error for `EditorOverrideScopeResolver`.

- [ ] **Step 3: Write the resolver**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Turns a concrete build target (platform, environment) into its override scope path under an entity's level order,
    /// and selects the deepest authored prefix of that path. Used by the packager and the viewport so both agree.
    /// </summary>
    public sealed class EditorOverrideScopeResolver {
        readonly EditorProjectPlatformGroupsDocument Groups;

        /// <summary>
        /// Initializes the resolver and validates the group tree against the supported platforms; inconsistent settings throw here.
        /// </summary>
        public EditorOverrideScopeResolver(EditorProjectPlatformGroupsDocument groups, IReadOnlyList<string> supportedPlatformIds) {
            Groups = groups ?? throw new ArgumentNullException(nameof(groups));
            new EditorProjectPlatformGroupsService(Path.GetTempPath()).Validate(Groups, supportedPlatformIds ?? throw new ArgumentNullException(nameof(supportedPlatformIds)));
        }

        /// <summary>
        /// Loads <c>settings/platform-groups.json</c> and <c>settings/platforms.json</c> from a project and builds a resolver.
        /// </summary>
        public static EditorOverrideScopeResolver Load(string projectRootPath) {
            EditorProjectPlatformGroupsDocument groups = new EditorProjectPlatformGroupsService(projectRootPath).Load();
            IReadOnlyList<string> platforms = new EditorProjectPlatformsService(projectRootPath).Load().SupportedPlatforms;
            return new EditorOverrideScopeResolver(groups, platforms);
        }

        /// <summary>
        /// Returns the entity's order when authored, else the project default, else <see cref="EditorOverrideLevelOrder.Default"/>.
        /// </summary>
        public IReadOnlyList<SceneOverrideScopeStepKind> ResolveLevelOrder(IReadOnlyList<SceneOverrideScopeStepKind> entityOrder) {
            if (entityOrder != null) {
                return entityOrder;
            }
            if (Groups.DefaultLevelOrder != null && Groups.DefaultLevelOrder.Count > 0) {
                return Groups.DefaultLevelOrder;
            }

            return EditorOverrideLevelOrder.Default;
        }

        /// <summary>
        /// Builds the path for one target: per level, the group chain containing the platform, the platform, or the
        /// environment. A blank environment ends the path at the Build Config level.
        /// </summary>
        public EditorOverrideScope BuildTargetPath(IReadOnlyList<SceneOverrideScopeStepKind> levelOrder, string platformId, string environmentId) {
            if (levelOrder == null) {
                throw new ArgumentNullException(nameof(levelOrder));
            }
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            EditorOverrideLevelOrder.Validate(levelOrder);
            EditorOverrideScope path = EditorOverrideScope.Common;
            for (int level = 0; level < levelOrder.Count; level++) {
                SceneOverrideScopeStepKind kind = levelOrder[level];
                if (kind == SceneOverrideScopeStepKind.Group) {
                    IReadOnlyList<string> chain = EditorProjectPlatformGroupsService.FindGroupChain(Groups, platformId);
                    for (int index = 0; index < chain.Count; index++) {
                        path = path.Append(new EditorOverrideScopeStep(SceneOverrideScopeStepKind.Group, chain[index]));
                    }
                } else if (kind == SceneOverrideScopeStepKind.Platform) {
                    path = path.Append(new EditorOverrideScopeStep(SceneOverrideScopeStepKind.Platform, platformId));
                } else {
                    if (string.IsNullOrWhiteSpace(environmentId)) {
                        break;
                    }
                    path = path.Append(new EditorOverrideScopeStep(SceneOverrideScopeStepKind.BuildConfig, environmentId));
                }
            }

            return path;
        }

        /// <summary>
        /// Selects the item whose scope is the longest prefix of <paramref name="target"/>; Common counts as a prefix.
        /// </summary>
        public static bool TrySelectDeepest<T>(IReadOnlyList<T> items, Func<T, EditorOverrideScope> scopeSelector, EditorOverrideScope target, out T selected) {
            if (items == null) {
                throw new ArgumentNullException(nameof(items));
            }
            if (scopeSelector == null) {
                throw new ArgumentNullException(nameof(scopeSelector));
            }

            selected = default;
            int bestDepth = -1;
            for (int index = 0; index < items.Count; index++) {
                if (items[index] == null) {
                    continue;
                }

                EditorOverrideScope scope = scopeSelector(items[index]);
                if (scope.Depth > bestDepth && scope.IsPrefixOf(target)) {
                    bestDepth = scope.Depth;
                    selected = items[index];
                }
            }

            return bestDepth >= 0;
        }
    }
}
```

The `Validate` call constructs a throwaway service only to reuse its rules; if that reads badly, make `Validate` a `public static` on `EditorProjectPlatformGroupsService` (it uses no instance state) and call it statically — do that and update Task 9's test to call it statically.

- [ ] **Step 4: Run to verify they pass**

Same command. Expected: 4 passed.

- [ ] **Step 5: Commit**

```
git add engine/helengine.editor/managers/scene/EditorOverrideScopeResolver.cs engine/helengine.editor/managers/project/EditorProjectPlatformGroupsService.cs engine/helengine.editor.tests/EditorOverrideScopeResolverTests.cs engine/helengine.editor.tests/EditorProjectPlatformGroupsServiceTests.cs
git commit -m "editor: add the override scope resolver"
```

---

### Task 11: The packager resolves overrides through groups

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs` (fields ~190-240, primary ctor ~595-665, `FindTargetPlatform*Override` ~1017-1130)
- Test: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs` (add test after `Package_WhenSceneChildDefinesWindowsExistenceOverrideFalse_PrunesTheChildSubtree`)

**Interfaces:**
- Consumes: `EditorOverrideScopeResolver.Load(ProjectRootPath)`, `ResolveLevelOrder`, `BuildTargetPath`, `TrySelectDeepest`.
- Produces: packager field `readonly EditorOverrideScopeResolver OverrideScopeResolver;` and private `EditorOverrideScope BuildEntityTargetPath(SceneEntityAsset entityAsset)`.

- [ ] **Step 1: Write the failing test**

```csharp
        /// <summary>
        /// Ensures a group-scoped existence override prunes member platforms and leaves non-members alone, with Common as the fallback.
        /// </summary>
        [Fact]
        public void Package_WhenEntityIsRestrictedToAGroup_PrunesItOnPlatformsOutsideTheGroup() {
            new EditorProjectPlatformsService(ProjectRootPath).Save(new EditorProjectPlatformsDocument { SupportedPlatforms = ["windows", "ds", "ps1"] });
            EditorProjectPlatformGroupsService groupsService = new EditorProjectPlatformGroupsService(ProjectRootPath);
            EditorProjectPlatformGroupsDocument groups = groupsService.Load();
            groupsService.AddGroup(groups, null, "handheld");
            groupsService.AssignPlatform(groups, "handheld", "ds");
            groupsService.Save(groups);

            string sceneId = "Scenes/GroupExistence.helen";
            WriteSceneAsset(sceneId, new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u, Name = "Root", LocalScale = float3.One, LocalOrientation = float4.Identity,
                        Components = Array.Empty<SceneComponentAssetRecord>(),
                        Children = new[] {
                            new SceneEntityAsset {
                                Id = 2u, Name = "HandheldRig", LocalScale = float3.One, LocalOrientation = float4.Identity,
                                Components = Array.Empty<SceneComponentAssetRecord>(),
                                HasOverrideLevelOrder = true,
                                OverrideLevelOrder = new[] { SceneOverrideScopeStepKind.Group, SceneOverrideScopeStepKind.Platform, SceneOverrideScopeStepKind.BuildConfig },
                                PlatformExistenceOverrides = new[] {
                                    new SceneEntityPlatformExistenceOverrideAsset { Scope = SceneOverrideScopePath.Common(), Exists = false },
                                    new SceneEntityPlatformExistenceOverrideAsset { Scope = SceneOverrideScopePath.Group("handheld"), Exists = true }
                                },
                                Children = Array.Empty<SceneEntityAsset>()
                            }
                        }
                    }
                }
            });

            new EditorPlatformBuildScenePackager(ProjectRootPath, Array.Empty<IAssetImporterRegistration>(), "ds", BuiltInShaderAssetLibrary)
                .Package(new[] { sceneId }, Path.Combine(BuildRootPath, "ds"));
            new EditorPlatformBuildScenePackager(ProjectRootPath, Array.Empty<IAssetImporterRegistration>(), "ps1", BuiltInShaderAssetLibrary)
                .Package(new[] { sceneId }, Path.Combine(BuildRootPath, "ps1"));

            using FileStream dsStream = File.OpenRead(GetPackagedScenePath(Path.Combine(BuildRootPath, "ds"), sceneId));
            using FileStream ps1Stream = File.OpenRead(GetPackagedScenePath(Path.Combine(BuildRootPath, "ps1"), sceneId));
            Assert.Single(Assert.Single(DeserializePackagedScene(dsStream).RootEntities).Children);
            Assert.Empty(Assert.Single(DeserializePackagedScene(ps1Stream).RootEntities).Children);
        }
```

If the `(projectRoot, importers, targetPlatformId, shaderLibrary)` constructor is not one of the existing overloads at lines 320-560, use the one the neighbouring pruning test uses.

- [ ] **Step 2: Run to verify it fails**

Run: `& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~Package_WhenEntityIsRestrictedToAGroup" --nologo -v q`
Expected: the `ds` assertion fails — the exact-match lookup from Task 3 never sees the group path, so `HandheldRig` is pruned on both.

- [ ] **Step 3: Install the resolver**

Add the field with a summary near `SelectedEnvironmentId`. In the primary constructor after `SelectedEnvironmentId = ...;` add `OverrideScopeResolver = EditorOverrideScopeResolver.Load(ProjectRootPath);` (a settings error surfaces here, before any scene is read, naming the offending ids). Add:

```csharp
        /// <summary>
        /// Builds the scope path this packaging target occupies under the entity's level order.
        /// </summary>
        EditorOverrideScope BuildEntityTargetPath(SceneEntityAsset entityAsset) {
            IReadOnlyList<SceneOverrideScopeStepKind> order = OverrideScopeResolver.ResolveLevelOrder(entityAsset.HasOverrideLevelOrder ? entityAsset.OverrideLevelOrder : null);
            return OverrideScopeResolver.BuildTargetPath(order, TargetPlatformId, SelectedEnvironmentId);
        }
```

Replace the body of `FindTargetPlatformExistenceOverride` (after the null guard and the common-target early return) with:

```csharp
            SceneEntityPlatformExistenceOverrideAsset[] overrides = entityAsset.PlatformExistenceOverrides ?? Array.Empty<SceneEntityPlatformExistenceOverrideAsset>();
            return EditorOverrideScopeResolver.TrySelectDeepest(overrides, item => EditorOverrideScope.FromSteps(item.Scope), BuildEntityTargetPath(entityAsset), out SceneEntityPlatformExistenceOverrideAsset selected)
                ? selected
                : null;
```

and the same three-line shape for the transform and component lookups with their own types. Keep the existing `TargetPlatformId` blank/common early return.

- [ ] **Step 4: Run the packager suites**

Run: `& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~Packager|FullyQualifiedName~BlueprintBuildPackaging" --nologo -v q`
Expected: pass. Existing per-platform tests still pass because under the default order the target path equals the pair path.

- [ ] **Step 5: Commit**

```
git add engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs
git commit -m "packager: resolve entity overrides through platform groups"
```

### Task 12: Viewport suppression uses the same resolver

**Files:**
- Modify: `engine/helengine.editor/managers/scene/EditorPlatformExistenceViewportSyncService.cs`
- Modify: `engine/helengine.editor/EditorSession.cs:791` (constructor call)
- Test: `engine/helengine.editor.tests/EditorPlatformExistenceViewportSyncServiceTests.cs` (new)

**Interfaces:**
- Consumes: `EditorOverrideScopeResolver.Load`, `EntityPlatformExistenceEditingService.ResolveExists(save, scope)`.
- Produces: ctor `EditorPlatformExistenceViewportSyncService(ObjectManager objectManager, string projectRootPath)`; `Apply(string activePlatformId)` unchanged signature.

- [ ] **Step 1: Write the failing test**

```csharp
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies viewport suppression follows group-scoped existence overrides.
    /// </summary>
    public sealed class EditorPlatformExistenceViewportSyncServiceTests : IDisposable {
        readonly string ProjectRootPath;
        readonly Core CoreValue;

        public EditorPlatformExistenceViewportSyncServiceTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-existence-sync-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ProjectRootPath);
            CoreValue = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            CoreValue.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }

        public void Dispose() {
            CoreValue.Dispose();
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        [Fact]
        public void Apply_WhenEntityExistsOnlyInAGroup_SuppressesPlatformsOutsideIt() {
            new EditorProjectPlatformsService(ProjectRootPath).Save(new EditorProjectPlatformsDocument { SupportedPlatforms = ["windows", "ds"] });
            EditorProjectPlatformGroupsService groupsService = new EditorProjectPlatformGroupsService(ProjectRootPath);
            EditorProjectPlatformGroupsDocument groups = groupsService.Load();
            groupsService.AddGroup(groups, null, "handheld");
            groupsService.AssignPlatform(groups, "handheld", "ds");
            groups.DefaultLevelOrder = [SceneOverrideScopeStepKind.Group, SceneOverrideScopeStepKind.Platform];
            groupsService.Save(groups);

            EditorEntity entity = new EditorEntity(CoreValue, new helengine.editor.EditorSessionInteractionServices()) { IsSceneOwned = true };
            CoreValue.ObjectManager.AddEntity(entity);
            EntitySaveComponent saveComponent = entity.Components.OfType<EntitySaveComponent>().Single();
            EntityPlatformExistenceEditingService existence = new EntityPlatformExistenceEditingService();
            existence.SetExists(saveComponent, EditorOverrideScope.Common, false);
            existence.SetExists(saveComponent, EditorOverrideScope.FromSteps(SceneOverrideScopePath.Group("handheld")), true);
            EditorPlatformExistenceViewportSyncService sync = new EditorPlatformExistenceViewportSyncService(CoreValue.ObjectManager, ProjectRootPath);

            sync.Apply("windows");
            Assert.True(entity.RuntimeSuppressed);

            sync.Apply("ds");
            Assert.False(entity.RuntimeSuppressed);
        }
    }
}
```

If `IsSceneOwned` is not settable or `ObjectManager.AddEntity` has another name, use whatever `EntitySaveComponentTests`/`EditorSession` use to register a scene-owned entity; the assertion is the point.

- [ ] **Step 2: Run to verify it fails**

Run: `& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlatformExistenceViewportSyncServiceTests" --nologo -v q`
Expected: build error (no two-argument constructor).

- [ ] **Step 3: Rewrite the service**

```csharp
        readonly EntityPlatformExistenceEditingService ExistenceService;
        readonly ObjectManager ObjectManager;
        readonly string ProjectRootPath;

        /// <summary>
        /// Initializes one platform-existence viewport sync service for a project.
        /// </summary>
        public EditorPlatformExistenceViewportSyncService(ObjectManager objectManager, string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ExistenceService = new EntityPlatformExistenceEditingService();
            ObjectManager = objectManager ?? throw new ArgumentNullException(nameof(objectManager));
            ProjectRootPath = projectRootPath;
        }

        /// <summary>
        /// Applies runtime suppression for every authored scene entity against the active platform's scope path.
        /// Group settings are re-read on each call because the call is event-driven and the file is small.
        /// </summary>
        public void Apply(string activePlatformId) {
            if (string.IsNullOrWhiteSpace(activePlatformId)) {
                return;
            }

            EditorOverrideScopeResolver resolver = EditorOverrideScopeResolver.Load(ProjectRootPath);
            List<Entity> entities = ObjectManager.Entities;
            for (int index = 0; index < entities.Count; index++) {
                if (entities[index] is not EditorEntity editorEntity
                    || editorEntity.IsDisposed
                    || !editorEntity.IsSceneOwned
                    || editorEntity.InternalEntity) {
                    continue;
                }

                EntitySaveComponent saveComponent = FindSaveComponent(editorEntity);
                if (saveComponent == null) {
                    continue;
                }

                EditorOverrideScope target = resolver.BuildTargetPath(resolver.ResolveLevelOrder(saveComponent.OverrideLevelOrder), activePlatformId, string.Empty);
                editorEntity.RuntimeSuppressed = !ExistenceService.ResolveExists(saveComponent, target);
            }
        }
```

`EditorSession.cs:791`: `PlatformExistenceSyncService = new EditorPlatformExistenceViewportSyncService(core.ObjectManager, this.projectPath);` (the field that `projectPlatformsService` is built from at line 680).

- [ ] **Step 4: Run the suite**

Run: `& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~ViewportSync|FullyQualifiedName~EditorSessionPlatforms" --nologo -v q`
Expected: pass.

- [ ] **Step 5: Commit**

```
git add engine/helengine.editor/managers/scene/EditorPlatformExistenceViewportSyncService.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/EditorPlatformExistenceViewportSyncServiceTests.cs
git commit -m "editor: suppress viewport entities through the override scope resolver"
```

---

### Task 13: Scope-based authoring helpers for generators

Keeps the existing per-platform methods (the demodisc generator still calls them) and adds the two-entry form the spec describes, so the project-side follow-up is a call-site swap.

**Files:**
- Modify: `engine/helengine.editor/managers/scene/PlatformSceneAuthoringHelperService.cs`
- Test: `engine/helengine.editor.tests/PlatformSceneAuthoringHelperServiceTests.cs` (add or create)

**Interfaces:**
- Produces: `void RestrictEntitySubtreeToScope(EditorEntity rootEntity, EditorOverrideScope scope)` — writes `Common: Exists=false` and `scope: Exists=true` on every entity in the subtree; `void ExcludeEntitySubtreeFromScope(EditorEntity rootEntity, EditorOverrideScope scope)` — writes `scope: Exists=false`; `void SetEntitySubtreeLevelOrder(EditorEntity rootEntity, IReadOnlyList<SceneOverrideScopeStepKind> order)`.

- [ ] **Step 1: Write the failing test**

```csharp
        [Fact]
        public void RestrictEntitySubtreeToScope_WritesCommonFalseAndScopeTrueOnEveryDescendant() {
            EditorEntity root = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices());
            EditorEntity child = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices());
            root.AddChild(child);
            EditorOverrideScope handheld = EditorOverrideScope.FromSteps(SceneOverrideScopePath.Group("handheld"));
            PlatformSceneAuthoringHelperService helper = new PlatformSceneAuthoringHelperService();
            EntityPlatformExistenceEditingService existence = new EntityPlatformExistenceEditingService();

            helper.SetEntitySubtreeLevelOrder(root, new[] { SceneOverrideScopeStepKind.Group, SceneOverrideScopeStepKind.Platform });
            helper.RestrictEntitySubtreeToScope(root, handheld);

            EntitySaveComponent childSave = child.Components.OfType<EntitySaveComponent>().Single();
            Assert.False(existence.ResolveExists(childSave, EditorOverrideScope.Common));
            Assert.True(existence.ResolveExists(childSave, handheld.Append(new EditorOverrideScopeStep(SceneOverrideScopeStepKind.Platform, "ds"))));
            Assert.False(existence.ResolveExists(childSave, new EditorOverrideScope("ps1")));
            Assert.Equal(new[] { SceneOverrideScopeStepKind.Group, SceneOverrideScopeStepKind.Platform }, childSave.OverrideLevelOrder);
        }
```

Use the file's existing `Core` fixture pattern (`EntityEnvironmentOverrideEditingServiceTests` shows it) if the test class is new.

- [ ] **Step 2: Run to verify it fails**

Run: `& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~PlatformSceneAuthoringHelperServiceTests" --nologo -v q`
Expected: build error on the three new methods.

- [ ] **Step 3: Add the methods**

```csharp
        /// <summary>
        /// Makes the subtree exist only beneath one scope: Common says absent, the scope says present. Adding a platform
        /// to the group later applies without touching the scene.
        /// </summary>
        public void RestrictEntitySubtreeToScope(EditorEntity rootEntity, EditorOverrideScope scope) {
            if (rootEntity == null) {
                throw new ArgumentNullException(nameof(rootEntity));
            }
            if (scope.IsCommon) {
                throw new ArgumentException("Restricting to Common would hide the subtree everywhere.", nameof(scope));
            }

            foreach (EditorEntity entity in EnumerateSubtree(rootEntity)) {
                EntitySaveComponent saveComponent = EnsureEntitySaveComponent(entity);
                EntityExistenceEditingService.SetExists(saveComponent, EditorOverrideScope.Common, false);
                EntityExistenceEditingService.SetExists(saveComponent, scope, true);
            }
        }

        /// <summary>
        /// Removes the subtree beneath one scope while leaving every other path as authored.
        /// </summary>
        public void ExcludeEntitySubtreeFromScope(EditorEntity rootEntity, EditorOverrideScope scope) {
            if (rootEntity == null) {
                throw new ArgumentNullException(nameof(rootEntity));
            }

            foreach (EditorEntity entity in EnumerateSubtree(rootEntity)) {
                EntityExistenceEditingService.SetExists(EnsureEntitySaveComponent(entity), scope, false);
            }
        }

        /// <summary>
        /// Records the level order the subtree's overrides are authored against.
        /// </summary>
        public void SetEntitySubtreeLevelOrder(EditorEntity rootEntity, IReadOnlyList<SceneOverrideScopeStepKind> order) {
            if (rootEntity == null) {
                throw new ArgumentNullException(nameof(rootEntity));
            }

            EditorOverrideLevelOrder.Validate(order);
            SceneOverrideScopeStepKind[] copy = order.ToArray();
            foreach (EditorEntity entity in EnumerateSubtree(rootEntity)) {
                EnsureEntitySaveComponent(entity).OverrideLevelOrder = copy;
            }
        }
```

`EnumerateSubtree` and `EnsureEntitySaveComponent`: the file already walks subtrees in `ApplyEntitySubtreePlatformRestrictions` and has `EnsureEntitySaveComponent`; if there is no reusable enumerator, add `static IEnumerable<EditorEntity> EnumerateSubtree(EditorEntity root)` yielding the root then each child depth-first over `root.Children` cast to `EditorEntity`.

- [ ] **Step 4: Run to verify it passes**

Same command. Expected: pass, plus the file's existing per-platform tests.

- [ ] **Step 5: Commit**

```
git add engine/helengine.editor/managers/scene/PlatformSceneAuthoringHelperService.cs engine/helengine.editor.tests/PlatformSceneAuthoringHelperServiceTests.cs
git commit -m "editor: add scope-based subtree restriction helpers for generators"
```

---

### Task 14: Full verification and docs

**Files:**
- Modify: `docs/superpowers/specs/2026-09-18-platform-override-tree-design.md` (status line only)
- Create: `docs/PlatformOverrideTree.md`

- [ ] **Step 1: Run every affected suite**

```
& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.core.tests/helengine.core.tests.csproj --nologo -v q
& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.files.tests/helengine.files.tests.csproj --nologo -v q
& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.editor.tests/helengine.editor.tests.csproj --nologo -v q
& "C:\Program Files\dotnet\dotnet.exe" test engine/helengine.bepu.tests/helengine.bepu.tests.csproj --nologo -v q
```

Expected: green. The `cs2.cpp.tests` project is red on master independently of this work (~153/854) and is not part of this gate; do run `& "C:\Program Files\dotnet\dotnet.exe" build engine/helengine.core/helengine.core.csproj -c Release --nologo -v q` to prove the core changes compile in the configuration the transpiler consumes.

- [ ] **Step 2: Write `docs/PlatformOverrideTree.md`**

Contents, in this order, one short paragraph each: what a scope path is and that Common is the empty path; the three level kinds and the per-entity order with its default; how `settings/platform-groups.json` is shaped (paste the JSON the Task 9 round-trip test produces); resolution ("deepest authored prefix wins") with the PS1/handheld example from the spec; how the packager and the viewport share `EditorOverrideScopeResolver`; the format versions now in force (entity payload 9, containers 25, component wrapped payload 5) and that older authored scenes are regenerated with the project's generator; the generator API (`RestrictEntitySubtreeToScope`, `ExcludeEntitySubtreeFromScope`, `SetEntitySubtreeLevelOrder`); and a pointer to the second plan for the editor UI. Set the spec's status line to `**Status:** model/format/build plan implemented <date>; editor UI plan pending`.

- [ ] **Step 3: Commit**

```
git add docs/PlatformOverrideTree.md docs/superpowers/specs/2026-09-18-platform-override-tree-design.md
git commit -m "docs: describe the platform override tree model and build resolution"
```

---

## Self-review notes

- Spec coverage: decisions 1–8 map to Tasks 1–3 (path, storage, versioning), 4–6 (resolution in editing services), 8 (level order, reordering logic), 9 (groups as settings, id namespace), 10–12 (shared resolver, build, viewport), 13 (generator two-entry form). Decision 6's UI (tab strip per level, level-order control) and the Platform Groups dialog are the second plan by design; the spec's Delivery section says so.
- Type consistency checked: `SceneOverrideScopePath.Format/Normalize/Platform/PlatformBuildConfig/Group/Common`, `EditorOverrideScope.FromSteps/ToSteps/Append/Parent/IsPrefixOf/TryGetStepId/IsCommon/Depth`, `EditorOverrideScopeMap.TryGetDeepestPrefix`, `EntitySaveComponent.TryGetDeepestExistencePlatformOverride/ActiveTransformScope/OverrideLevelOrder`, `EditorOverrideLevelOrder.Default/Validate/IsValidPath`, `EditorProjectPlatformGroupsService.FindGroupChain/Validate/AddGroup/AssignPlatform`, `EditorOverrideScopeResolver.Load/ResolveLevelOrder/BuildTargetPath/TrySelectDeepest` are spelled identically in every task that uses them.
- Contract scan: no production identifier or comment in this plan contains "legacy", "migrate", "upgrade" or a `version >=` comparison; the reader keeps `payloadVersion != SceneEntityPayloadVersion`.

