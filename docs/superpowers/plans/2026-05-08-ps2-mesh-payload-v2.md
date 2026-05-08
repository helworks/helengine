# PS2 Mesh Payload V2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the PS2 builder accept current mesh payloads, rewrite them correctly, and unblock the city PS2 ISO build.

**Architecture:** Keep the fix local to the PS2 builder. Add one regression test that uses a version `2` mesh payload, then replace the PS2 builder's manual mesh payload parsing with version-aware decoding and version `2` re-serialization after path rewriting.

**Tech Stack:** C#, .NET 9, xUnit, helengine shared binary serializers, helengine-ps2 builder pipeline

---

### Task 1: Add the failing regression test

**Files:**
- Modify: `C:\dev\helworks\helengine-ps2\builder.tests\Ps2PlatformAssetBuilderTests.cs`

- [ ] **Step 1: Write the failing test**

Add a regression test that stages a mesh component payload using version `2` with one material-reference array entry and asserts the packaged disc scene rewrites the model and material paths to physical PS2 disc paths.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test 'C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj' --filter "BuildAsync_WhenPackagedSceneAndMaterialUseLogicalPaths_RewritesVersion2MeshPayloadToPhysicalDiscPaths"`

Expected: FAIL with `Unsupported mesh component payload version '2'`.

### Task 2: Implement version-aware PS2 mesh payload rewriting

**Files:**
- Modify: `C:\dev\helworks\helengine-ps2\builder\Ps2CookedAssetPathRewriter.cs`

- [ ] **Step 1: Replace the manual version-1-only mesh payload parser**

Decode mesh payloads using version-aware semantics, rewrite the decoded model and material references, and re-serialize the payload using the current mesh payload format instead of preserving the old manual binary layout.

- [ ] **Step 2: Keep legacy payload readability**

Preserve support for incoming version `1` payloads so older content can still build, but normalize rewritten output to version `2`.

- [ ] **Step 3: Run the regression test to verify it passes**

Run: `dotnet test 'C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj' --filter "BuildAsync_WhenPackagedSceneAndMaterialUseLogicalPaths_RewritesVersion2MeshPayloadToPhysicalDiscPaths"`

Expected: PASS

### Task 3: Verify the broader PS2 path

**Files:**
- Verify only

- [ ] **Step 1: Run the focused PS2 builder tests**

Run: `dotnet test 'C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj'`

Expected: PASS

- [ ] **Step 2: Run the city PS2 build through the CLI DLL**

Run: `dotnet 'C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\bin\Debug\net9.0-windows\helengine.editor.app.dll' --build ps2 --project 'C:\dev\helprojs\city\project.heproj' --output 'C:\dev\helprojs\output\ps2'`

Expected: exit code `0` and success output for platform `ps2`.

- [ ] **Step 3: Verify the ISO artifact exists**

Run: `Get-Item 'C:\dev\helprojs\output\ps2\game.iso'`

Expected: file exists.
