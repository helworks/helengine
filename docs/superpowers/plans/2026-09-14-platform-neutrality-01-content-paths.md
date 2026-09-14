# Generic content path resolution Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans task-by-task. Delegation requires the user's applicable opt-in; do not launch an independent review without permission. Steps use checkbox syntax.

**Goal:** Remove the Wii U material alias from core without changing packaged runtime lookup.

**Architecture:** Use the existing IContentStreamSource boundary. Add a neutral alias decorator only if runtime references cannot all be rewritten by the platform packager; the initial migration uses this decorator to preserve old package compatibility.

**Tech Stack:** C#/.NET 9, xUnit, existing engine and platform builder contracts.

**Spec:** [Platform neutrality migration design](../specs/2026-09-14-platform-neutrality-design.md)

## Global constraints

Read [the shared design](../specs/2026-09-14-platform-neutrality-design.md). All its compatibility and execution constraints apply. Paths are relative to C:/dev/helworks/helengine unless an absolute sibling-repository path is given. New paths are explicitly marked Create. Revalidate external repository instructions, branch and dirty state before implementing there.

For each task: run the named characterization tests first, add the new failing regression, implement the described change, rerun the focused tests, inspect the diff, then commit only that task's explicit files. Do not treat a zero-test filter as success. Build outputs must use a visible workspace-owned directory.

## File map

- Modify: engine/helengine.core/content/HostFileSystemContentStreamSource.cs.
- Create: engine/helengine.core/content/AliasedContentStreamSource.cs.
- Create: engine/helengine.core.tests/content/AliasedContentStreamSourceTests.cs and HostFileSystemContentStreamSourceTests.cs.
- Modify externally: C:/dev/helworks/helengine-wiiu/builder/WiiUDockerNativeBuildExecutor.cs and builder.tests/WiiURuntimeSourceTests.cs.
- Create externally: C:/dev/helworks/helengine-wiiu/builder/WiiUContentAliasDefinition.cs.

### Task 1: Introduce explicit aliases

**Interface:** `AliasedContentStreamSource(IContentStreamSource source, IReadOnlyDictionary<string,string> aliases)`, implementing existing `Stream OpenRead(string assetPath)`. Copy the dictionary using ordinal comparison; one lookup only, no recursive alias expansion. Underlying provider lifetime remains caller-owned.

- [ ] Add a recording IContentStreamSource fixture and these assertions:
```csharp
var aliases = new Dictionary<string, string> { ["logical.bin"] = "stored.bin" };
var source = new RecordingContentStreamSource();
var mapped = new AliasedContentStreamSource(source, aliases);
using var stream = mapped.OpenRead("logical.bin");
Assert.Equal("stored.bin", source.LastPath);
```
RecordingContentStreamSource implements OpenRead by assigning its public LastPath property and returning a new MemoryStream. Add cases for unmapped names, case differences, null provider/map, blank keys/values, and propagated IO exceptions.
- [ ] Run `dotnet test engine/helengine.core.tests/helengine.core.tests.csproj --no-restore --filter FullyQualifiedName~AliasedContentStreamSourceTests`; expect a missing-type failure initially.
- [ ] Implement the decorator with constructor validation and exact one-hop lookup:
```csharp
return Source.OpenRead(Aliases.TryGetValue(assetPath, out string mappedPath) ? mappedPath : assetPath);
```
Reject blank incoming paths before lookup. Do not dispose the borrowed provider.
- [ ] Rerun tests and commit: `refactor: add explicit content aliases`.

### Task 2: Supply the alias from Wii U startup

**Interface:** WiiUContentAliasDefinition supplies the ordinal mapping from cooked/engine/materials/standard.hasset to wiiu_standard_material.hasset; the engine has no knowledge of these names.

- [ ] Trace the existing material alias declaration in WiiUDockerNativeBuildExecutor through source staging, generated startup and WUHB packaging; record each writer and reader in the implementation commit description.
- [ ] Add a builder regression asserting generated startup uses an alias-aware source and the package includes the exact matching file. Preserve reference IDs and the existing alias spelling.
- [ ] Add WiiUContentAliasDefinition and update the C# startup emission/staging source to construct the decorator around the fs:/vol/content provider. If generated code needs source inclusion, explicitly stage the new core class. Never patch emitted C++.
- [ ] Run `dotnet test C:/dev/helworks/helengine-wiiu/builder.tests/helengine.wiiu.builder.tests.csproj --no-restore --filter "FullyQualifiedName~WiiURuntimeSourceTests|FullyQualifiedName~WiiUDockerNativeBuildExecutorSourceTests"`.
- [ ] Compile generated Wii U output and smoke-test standard material loading with a newly packaged build and an existing alias package. Record unavailable toolchain/device checks rather than assuming compatibility.
- [ ] Commit engine and platform changes separately; align required engine versions before deleting the old core branch.

### Task 3: Remove the implicit rewrite

- [ ] Add a core regression: resolving cooked/engine/materials/standard.hasset beneath fs:/vol/content preserves that relative name; no alias is applied without an explicit decorator. Use the existing private resolver through a test-accessible seam rather than opening a device path.
- [ ] Remove all three WiiU constants and the special conditional from HostFileSystemContentStreamSource.
- [ ] Preserve relative, absolute, virtual-root and drive-path behavior; add cases for dvd:/, fs:/vol/content, C:/ and empty paths.
- [ ] Run the two named core test classes, rebuild the Wii U staged sources, and assert the core file contains neither WiiU nor wiiu_standard_material.
- [ ] Commit: `refactor: move Wii U content alias into platform startup`.

## Completion

Normal host content access applies no platform alias. Wii U owns and installs its alias at the real runtime startup boundary, with packaging and lookup verified together.
