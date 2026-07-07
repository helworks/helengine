# Visual Studio Editor Output Isolation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Isolate Visual Studio IDE builds of `helengine.editor.app` into `bin\vs` and `obj\vs` so they no longer overwrite the live CLI or agent output tree.

**Architecture:** Keep the change local to `helengine.editor.app.csproj` by adding a conditional `PropertyGroup` gated on `$(BuildingInsideVisualStudio)`. Verify the Visual Studio path changes through MSBuild property evaluation, and confirm non-IDE builds still resolve to the existing default output path.

**Tech Stack:** .NET SDK-style MSBuild project properties, PowerShell verification, `dotnet msbuild`

---

### Task 1: Add Visual Studio-only output roots to the editor project

**Files:**
- Modify: `helengine.ui/helengine.editor.app/helengine.editor.app.csproj`

- [ ] **Step 1: Write the failing verification command**

```powershell
$ProjectPath = 'C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\helengine.editor.app.csproj'
$Result = dotnet msbuild $ProjectPath -nologo -p:BuildingInsideVisualStudio=true -getProperty:BaseOutputPath,BaseIntermediateOutputPath,OutputPath | ConvertFrom-Json

if ($Result.Properties.BaseOutputPath -ne 'bin\vs\') {
    throw "Expected BaseOutputPath 'bin\vs\' but got '$($Result.Properties.BaseOutputPath)'."
}

if ($Result.Properties.BaseIntermediateOutputPath -ne 'obj\vs\') {
    throw "Expected BaseIntermediateOutputPath 'obj\vs\' but got '$($Result.Properties.BaseIntermediateOutputPath)'."
}

if ($Result.Properties.OutputPath -ne 'bin\vs\Debug\net9.0-windows\') {
    throw "Expected OutputPath 'bin\vs\Debug\net9.0-windows\' but got '$($Result.Properties.OutputPath)'."
}
```

- [ ] **Step 2: Run the verification command to confirm it fails before the project change**

Run:

```powershell
powershell -NoProfile -Command "
$ProjectPath = 'C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\helengine.editor.app.csproj'
$Result = dotnet msbuild $ProjectPath -nologo -p:BuildingInsideVisualStudio=true -getProperty:BaseOutputPath,BaseIntermediateOutputPath,OutputPath | ConvertFrom-Json
if ($Result.Properties.BaseOutputPath -ne 'bin\vs\') { throw \"Expected BaseOutputPath 'bin\vs\' but got '$($Result.Properties.BaseOutputPath)'.\" }
if ($Result.Properties.BaseIntermediateOutputPath -ne 'obj\vs\') { throw \"Expected BaseIntermediateOutputPath 'obj\vs\' but got '$($Result.Properties.BaseIntermediateOutputPath)'.\" }
if ($Result.Properties.OutputPath -ne 'bin\vs\Debug\net9.0-windows\') { throw \"Expected OutputPath 'bin\vs\Debug\net9.0-windows\' but got '$($Result.Properties.OutputPath)'.\" }
"
```

Expected: `FAIL` with the current values resolving to `bin\`, `obj\`, and `bin\Debug\net9.0-windows\`.

- [ ] **Step 3: Add the minimal project property override**

Insert this property group near the main editor project properties in `helengine.ui/helengine.editor.app/helengine.editor.app.csproj`:

```xml
  <PropertyGroup Condition="'$(BuildingInsideVisualStudio)' == 'true'">
    <BaseOutputPath>bin\vs\</BaseOutputPath>
    <BaseIntermediateOutputPath>obj\vs\</BaseIntermediateOutputPath>
  </PropertyGroup>
```

- [ ] **Step 4: Run the verification command again and confirm it passes**

Run:

```powershell
powershell -NoProfile -Command "
$ProjectPath = 'C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\helengine.editor.app.csproj'
$Result = dotnet msbuild $ProjectPath -nologo -p:BuildingInsideVisualStudio=true -getProperty:BaseOutputPath,BaseIntermediateOutputPath,OutputPath | ConvertFrom-Json
if ($Result.Properties.BaseOutputPath -ne 'bin\vs\') { throw \"Expected BaseOutputPath 'bin\vs\' but got '$($Result.Properties.BaseOutputPath)'.\" }
if ($Result.Properties.BaseIntermediateOutputPath -ne 'obj\vs\') { throw \"Expected BaseIntermediateOutputPath 'obj\vs\' but got '$($Result.Properties.BaseIntermediateOutputPath)'.\" }
if ($Result.Properties.OutputPath -ne 'bin\vs\Debug\net9.0-windows\') { throw \"Expected OutputPath 'bin\vs\Debug\net9.0-windows\' but got '$($Result.Properties.OutputPath)'.\" }
"
```

Expected: `PASS` with no exception.

- [ ] **Step 5: Commit the isolated Visual Studio output change**

```bash
git add helengine.ui/helengine.editor.app/helengine.editor.app.csproj
git commit -m "fix: isolate Visual Studio editor outputs"
```

### Task 2: Confirm non-IDE editor builds keep the existing output path

**Files:**
- Modify: `helengine.ui/helengine.editor.app/helengine.editor.app.csproj`

- [ ] **Step 1: Write the default-path verification command**

```powershell
$ProjectPath = 'C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\helengine.editor.app.csproj'
$Result = dotnet msbuild $ProjectPath -nologo -getProperty:BaseOutputPath,BaseIntermediateOutputPath,OutputPath | ConvertFrom-Json

if ($Result.Properties.BaseOutputPath -ne 'bin\') {
    throw "Expected BaseOutputPath 'bin\' but got '$($Result.Properties.BaseOutputPath)'."
}

if ($Result.Properties.BaseIntermediateOutputPath -ne 'obj\') {
    throw "Expected BaseIntermediateOutputPath 'obj\' but got '$($Result.Properties.BaseIntermediateOutputPath)'."
}

if ($Result.Properties.OutputPath -ne 'bin\Debug\net9.0-windows\') {
    throw "Expected OutputPath 'bin\Debug\net9.0-windows\' but got '$($Result.Properties.OutputPath)'."
}
```

- [ ] **Step 2: Run the command and confirm the default path still passes**

Run:

```powershell
powershell -NoProfile -Command "
$ProjectPath = 'C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\helengine.editor.app.csproj'
$Result = dotnet msbuild $ProjectPath -nologo -getProperty:BaseOutputPath,BaseIntermediateOutputPath,OutputPath | ConvertFrom-Json
if ($Result.Properties.BaseOutputPath -ne 'bin\') { throw \"Expected BaseOutputPath 'bin\' but got '$($Result.Properties.BaseOutputPath)'.\" }
if ($Result.Properties.BaseIntermediateOutputPath -ne 'obj\') { throw \"Expected BaseIntermediateOutputPath 'obj\' but got '$($Result.Properties.BaseIntermediateOutputPath)'.\" }
if ($Result.Properties.OutputPath -ne 'bin\Debug\net9.0-windows\') { throw \"Expected OutputPath 'bin\Debug\net9.0-windows\' but got '$($Result.Properties.OutputPath)'.\" }
"
```

Expected: `PASS` with no exception.

- [ ] **Step 3: Commit after confirming non-IDE behavior remains unchanged**

```bash
git add helengine.ui/helengine.editor.app/helengine.editor.app.csproj
git commit -m "test: verify default editor output path remains unchanged"
```
