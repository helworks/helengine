# Platform neutrality migration design

Status: proposed implementation design requested after the platform-boundary audit. Documentation only; implementation and cross-repository commits are separate work.

## Scope and decision

Audit baseline: C:/dev/helworks/helengine, main at b9307947. F:/dev/helengine is a different checkout and must not be used. Revalidate HEAD and dirty files at execution time.

Shared engine code owns reusable behavior and contracts. Platform modules own target rules and native implementations. Host adapters own operating-system integration. Project documents own game-specific scene and content choices.

Preserve DirectX11, Vulkan and console support. This is a relocation of responsibility, not backend removal. An interface without migrated production callers and removal of the old branch is incomplete.

Prefer existing IContentStreamSource, CoreInitializationOptions, PlatformDefinition, RuntimeGenerationContract, cook capabilities and builder loading boundaries. Extend these only where they lack a required concept. Do not create an all-purpose platform service locator or an editor dependency from a console builder.

Three approaches were considered: moving files without changing callers leaves coupling; replacing every platform identifier with a service creates unnecessary abstraction; explicit policies and narrow adapters remove executable special cases while retaining legitimate platform metadata. Use the third approach.

## Boundaries

- helengine.core: no concrete graphics/OS SDK dependencies, console ID dispatch, or DemoDisc scene identifiers.
- helengine.baseplatform: neutral metadata and builder contracts; platform IDs are data, not dispatch tables.
- helengine.editor: authoring/orchestration and injected contracts; no concrete renderer construction, native filesystem interop or baked-in console output policies.
- helengine.directx11 / helengine.vulkan: rendering implementation; renderer-specific editor bridges belong in new editor backend adapters or the existing Windows adapter as described in plan 04.
- Windows and Linux host adapters: native filesystem operations, preserving existing atomicity, identity and locking semantics.
- Separate helengine-ds, helengine-3ds, helengine-ps2 and helengine-wiiu repositories: console policy values, encoders, startup composition and package transforms.
- Project configuration and project tooling: scene aliases, selected boot scenes, demo-specific generated assets.

OS-dependent path comparison and shader target enums are not automatically violations. Check behavior and dependency ownership. A using directive alone is not proof of a public SDK type leak.

## Compatibility and execution constraints

- Keep current net9.0 / net9.0-windows target frameworks; no framework upgrade.
- No loss of supported platform behavior.
- Never edit generated C++ output; change its C# emitter or source staging input.
- Preserve serialized asset bytes, runtime IDs, paths and scene routing unless a documented migration explicitly changes them.
- Never silently fall back when a required capability is missing.
- Keep existing unrelated work unstaged; integrate with current dirty-file owners before editing overlapping files.
- Use visible workspace-owned build outputs; never direct agent artifacts to AppData/Temp.
- Follow each repository's AGENTS.md, including substantive XML documentation and one class per file.
- Do not auto-publish packages, push branches, deploy builds or edit game projects during planning.
- Updating shared contracts requires compatible platform builder releases and version negotiation before consuming them.
- Do not add a shared-engine dependency on a concrete platform repository.

## Acceptance

All seven plans include both migration and deletion checks. Passing mock tests alone does not demonstrate native platform compatibility. Record focused test counts, actual host/platform smoke runs, and unavailable validation explicitly. Platform dependency/source guards have no permanent baseline exemptions for these findings.

## Coverage

1. Core content alias: plan 01.
2. DemoDisc scene identifiers and DS scene inference: plan 02.
3. PS2/3DS overlay exceptions: plan 03.
4. Shared rendering, readback, material factories and CLI GPU composition: plan 04.
5. DS paths/audio policy/encoding and platform capability compatibility: plan 05.
6. Native authoring filesystem: plan 06.
7. DS debug font ownership and regression guards: plan 07.

The earlier editor plans are partial implementation history, not completion evidence. Plan 04 supersedes their remaining backend-boundary work; plan 05 addresses platform ownership left in the earlier audio extraction.
