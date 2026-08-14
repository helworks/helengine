# GDI Font Buffer Lifetime Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prevent GDI from substituting a system font while importing a TTF from memory.

**Architecture:** Keep the unmanaged font bytes alive through `GdiFontImporter.ImportFont`, dispose them only after the private collection and `System.Drawing.Font` have finished rasterization. Add a test asserting the imported source family identity.

**Tech Stack:** .NET 9, System.Drawing, xUnit.

---

### Task 1: Protect the native TTF lifetime

**Files:**
- Modify: `engine/helengine.editor.windows/content/font/GdiFontImporter.cs`
- Test: `engine/helengine.editor.windows.tests/content/font/GdiFontImporterTests.cs`

- [ ] **Step 1: Write the failing test**

Add a test that imports `Carlito-Regular.ttf` and asserts `fontAsset.FontInfo.Name` is `Carlito`.

- [ ] **Step 2: Run test to verify it fails**

Run: `rtk dotnet test engine/helengine.editor.windows.tests/helengine.editor.windows.tests.csproj --no-restore --filter FullyQualifiedName~GdiFontImporterTests.ImportFont_WhenUsingVendorTrueTypeSource_PreservesSourceFamilyName`

Expected: FAIL because the current importer frees the registered font buffer before GDI creates the font.

- [ ] **Step 3: Write minimal implementation**

Move the native allocation ownership into a disposable font-collection wrapper so it remains allocated while `ImportFont` creates and rasterizes the `System.Drawing.Font`.

- [ ] **Step 4: Run test to verify it passes**

Run the same test. Expected: PASS with `Carlito`.

- [ ] **Step 5: Run focused regression suite and commit**

Run: `rtk dotnet test engine/helengine.editor.windows.tests/helengine.editor.windows.tests.csproj --no-restore --filter FullyQualifiedName~GdiFontImporterTests`

Stage only the importer, its test, and this plan; commit with `Fix GDI font importer buffer lifetime`.
