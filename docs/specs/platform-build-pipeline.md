# Platform Build Execution Pipeline

Status: living document — reflects the headless build path under `engine/helengine.editor` and the shared contracts in `engine/helengine.baseplatform` as built. The wrapper script and its exit codes are documented in the top-level `README.md`. The `.heproj` format and the platform installation/SDK registry are covered elsewhere.

## Entry points & isolation

The headless build entry point (`EditorCliBuildRunner`) fails fast rather than defaulting: it requires build settings already configured through the interactive editor for the requested platform. Before the cook, it runs the selected profile's ordered prebuild commands (each a full editor-authored command, not part of the runtime-only cook) — the first failure aborts before any cook work starts. Every build invocation gets its own isolated, GUID-suffixed workspace and generated-code output directory (redirectable off the OS temp folder via an environment variable), so repeated or concurrent runs never share mutable state.

## Module resolution mode

The platform cook's script build is hardcoded to `RuntimeOnly` (README's module-build-mode terms) — it must never pull in editor modules or test assemblies. Prebuild commands and interactive editor sessions use `EditorFull`.

## The build graph

A queued build runs through a fixed phase sequence — regenerate generated core, cook assets, compile code, resolve target variants, lay out media, write containers, package the platform — each phase in its own workspace subdirectory with its own log. The build manifest is replaced (a new instance per change), not mutated in place, as it flows through phases. Every phase also appends a timestamped marker to one durable log file, specifically so a build that crashes or is killed mid-run leaves a trail of which phase it reached.

Guidelines:
- Keep phase order stable — later phases depend on earlier ones' output.
- Keep writing the phase-marker log; it's the only postmortem signal for a headless build that dies without reporting a result.

## Platform builders

Each target platform implements a shared builder interface and is loaded dynamically from its own assembly — new platforms are added as new assemblies, not as branches in the shared build graph. The request handed to a builder is validated for internal consistency (no duplicate/missing profile references) before the builder ever sees it, and every builder returns results in the same uniform report shape (success flag, diagnostics, per-item outcomes).

## Cook caching & the runtime-feature gate

Individual cook units carry independent content and settings hashes so cacheability decisions don't conflate "the source changed" with "the settings changed." Separately, required-runtime-feature usage is aggregated across the build and checked against anything the user explicitly disabled — if cooked content still needs a disabled feature, the build fails hard rather than shipping silently with it missing.
