# Build Waiter Design

## Purpose

Provide a small console application that owns a platform build process and reports one trustworthy terminal result. Codex launches this tool instead of launching a build command directly, so build completion is observed automatically rather than requiring the user to ask whether an output is ready.

## Scope

The tool will live at `tools/build-waiter` and run on the host machine. It accepts a child build command, an output directory, and one or more required relative artifact paths.

The initial PS2 invocation will require:

- `game.iso`
- `disc/SYSTEM.CNF`
- `disc/HELENGIN.ELF`

The tool remains platform-neutral: each build invocation supplies its own artifact list.

## Command Contract

The console entry point will use this form:

```text
helengine.buildwaiter --output <directory> --require <relative-path> [--require <relative-path> ...] -- <build-command> [arguments ...]
```

The separator marks the child process command. The tool writes concise status lines for launch, periodic waiting, child-process completion, artifact verification, and the final result.

## Completion Rules

At startup, the tool records a UTC start timestamp. A build succeeds only when all of the following are true:

1. The child process exits with code `0`.
2. The output root exists.
3. Every required artifact exists beneath the output root.
4. Every required artifact has a last-write timestamp at or after the build start timestamp.
5. Required files have non-zero length.

The tool returns `0` on success. A child-process failure, missing artifact, stale artifact, invalid argument, or launch failure returns non-zero and identifies the reason on stderr.

## Safety and Isolation

The tool is read-only with respect to build outputs: it never creates, deletes, or replaces output artifacts. The build process remains responsible for publication. Path validation rejects required paths that escape the output directory.

The child process inherits the current working directory and environment by default. Its stdout and stderr are forwarded live so build diagnostics remain visible.

## Agent Workflow

For a build, Codex starts the waiter in the background and waits for its result. It reports completion only after the waiter verifies the current build's artifacts. This removes the stale-build ambiguity and avoids manual status checks.

## Tests

Unit tests will cover argument parsing, artifact path containment, missing artifacts, stale artifacts, zero-byte artifacts, and success verification. An integration-style test will run a short host child process that writes required files and assert a successful wait result.
