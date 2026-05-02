# Editor CLI Build Entrypoint Design

## Goal

Add a small command-line entrypoint to the existing editor executable so build automation can run without the GUI.

The first supported form should be:

```text
helengine.editor --project C:/dev/helworks/mygame --build windows --output C:/dev/builds/windows
```

This mode should:

- load the specified project
- read the project’s already-saved editor build settings
- resolve the requested platform through `user_settings/platforms.json`
- regenerate generated core through the editor-owned regeneration step
- execute the selected platform builder in-process
- write the build result to the supplied output path
- exit with a process code that reflects success or failure

## Non-Goals

- No separate headless editor application
- No new build system
- No per-scene CLI selection in the first version
- No replacement for the GUI build dialogs
- No platform-specific build logic inside the CLI parser itself

## CLI Contract

The first CLI version should accept:

- `--project <absolute-or-relative-project-path>`
- `--build <platform-id>`
- `--output <absolute-or-relative-output-path>`

Behavior rules:

- `--project` identifies the project root that contains the editor settings and platform manifests.
- `--build` selects one platform id that must already be installed in `user_settings/platforms.json`.
- `--output` overrides the final output root for that invocation.
- If the arguments are invalid, the process should print a clear message and exit non-zero.

The CLI should not require:

- a scene id
- a build profile id
- a graphics profile id
- a codegen profile id

Those come from the project’s saved editor build settings.

## Architecture

The editor executable should keep its normal GUI startup path, but add a small command-line preflight path before UI initialization.

The CLI path should:

1. parse the arguments
2. resolve the project root
3. load the project’s existing build configuration
4. resolve the requested platform descriptor from the platform catalog
5. run generated-core regeneration using the platform-provided codegen metadata
6. execute the platform build using the same in-process executor used by the GUI
7. return success or failure and exit

This keeps the CLI thin:

- the editor still owns orchestration
- the platform builder still owns platform-specific build behavior
- the saved project settings still own the build profile selections

## Data Flow

### Input

The CLI receives:

- project root
- target platform id
- output root override

### Project State

The editor reads:

- `user_settings/platforms.json`
- `user_settings/build_config.json`
- the project scene and asset catalog

### Build Selection

The CLI should use the same persisted build selection model as the GUI:

- the active platform entry from the build config
- the selected build profile
- the selected graphics profile
- the selected codegen profile
- the selected option values for each profile group
- the selected queue item scene list

The only CLI-specific override should be the output root.

### Execution

The CLI should hand the resolved request to the same build executor used by the editor UI.

That executor must still:

- regenerate generated core in the editor-owned step
- package staged payloads
- invoke the platform builder
- write the final output tree

## Error Handling

The CLI should fail early with actionable messages.

Expected error classes:

- missing or invalid `--project`
- missing or invalid `--build`
- missing or invalid `--output`
- project not found
- build config missing required platform state
- platform not installed or missing builder metadata
- generated-core regeneration failure
- platform build failure

The exit code should be non-zero for any failure.

The CLI should print:

- the failing phase
- the platform id
- the output root
- the underlying error message

## Testing

The first test slice should cover:

- parsing `--project`, `--build`, and `--output`
- rejecting invalid argument combinations
- loading the requested platform from the catalog
- loading the build config for an existing project
- executing the build path without initializing the GUI
- surfacing a non-zero exit code when the build fails

Good first integration test:

- seed a test project with a saved build config
- run the editor executable with the new CLI args
- assert that the build output root contains the expected native player output
- assert that the process exits successfully

## Success Criteria

This feature is done when:

- `helengine.editor` can be invoked directly from the shell with `--project`, `--build`, and `--output`
- the CLI uses the same saved project settings that the GUI uses
- the GUI path still works unchanged
- the build output is written to the requested path
- the process exit code reflects the build result

