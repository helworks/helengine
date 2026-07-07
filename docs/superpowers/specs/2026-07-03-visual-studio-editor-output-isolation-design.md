# Visual Studio Editor Output Isolation

## Goal

Prevent Visual Studio IDE builds of `helengine.editor.app` from writing into the same output directory used by agent and CLI builds, so a running editor process does not lock the binaries that Visual Studio is trying to overwrite.

## Current Problem

`helengine.editor.app` currently uses the default SDK output layout under `bin\<Configuration>\net9.0-windows\` and `obj\<Configuration>\net9.0-windows\`. When the editor is already running from that output path, a Visual Studio rebuild tries to copy referenced engine assemblies into the same live directory. Windows then blocks the copy because the running process holds those files open.

## Scope

This change applies only to `helengine.ui/helengine.editor.app/helengine.editor.app.csproj`.

It does not change:

- engine project output rules in `Directory.Build.props`
- CLI or agent builds of the editor project
- output paths for unrelated projects

## Chosen Approach

Add a `PropertyGroup` in `helengine.editor.app.csproj` that sets:

- `BaseOutputPath` to `bin\vs\`
- `BaseIntermediateOutputPath` to `obj\vs\`

The property group will be conditioned on `$(BuildingInsideVisualStudio)` being `true`.

## Why This Approach

This is the narrowest fix that matches the requested behavior:

- Visual Studio IDE builds get isolated outputs.
- Existing CLI and agent workflows keep their current paths.
- The change stays local to the project that actually experiences the file-lock conflict.

Using `Directory.Build.props` would widen the effect beyond the editor app and risk changing build behavior for other projects that do not need output isolation.

## Expected Behavior

When the editor project is built inside Visual Studio, the resulting files should be written under:

- `bin\vs\Debug\net9.0-windows\`
- `obj\vs\Debug\net9.0-windows\`

For non-IDE builds, the existing default layout remains in place:

- `bin\Debug\net9.0-windows\`
- `obj\Debug\net9.0-windows\`

This separation allows a running editor instance from the standard output path to coexist with a Visual Studio rebuild that stages assemblies into the `vs` subtree.

## Verification

Use the smallest proof that exercises the condition:

1. Inspect the updated project file to confirm the conditional property group exists only for Visual Studio builds.
2. Run a targeted build or MSBuild property evaluation with `BuildingInsideVisualStudio=true` and confirm the effective output path resolves under `bin\vs` and `obj\vs`.
3. Optionally confirm that the same project without that property still resolves to the existing default output layout.

## Risks

- If some developer workflow expects Visual Studio to launch the editor from the old default path, that launch behavior will now follow the isolated `vs` output tree instead.
- If another IDE does not set `BuildingInsideVisualStudio`, it will continue using the standard output path, which is acceptable for the current requirement.
