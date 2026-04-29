# C++ Headless Core Transpiler Design

## Goal

Add the first real design for `cs2.cpp` so it can convert `helengine.core` into portable C++ for a headless engine core.

The generated output should target a platform-neutral core first, then allow a native Windows runner to plug into that core later. Windows is only the first validation host. The design must avoid locking the generated code to Windows-specific assumptions because the long-term target includes retro consoles such as PlayStation 2, GameCube, Wii, and PSP.

## Scope

This phase focuses on converting `engine/helengine.core` as completely as possible within an explicitly supported feature subset.

The intended first-pass converted domain is:

- Math types and utility structs.
- Core object and component data.
- Scene and entity model data.
- Serialization and binary reader/writer abstractions.
- Asset metadata and content structures that do not require a live graphics or operating-system backend.

Out of scope for this phase:

- A native Windows runner.
- Graphics device integration.
- Audio integration.
- Input-device integration.
- Platform window creation.
- Threading abstractions beyond what `helengine.core` already requires structurally.
- Reflection-heavy managed behavior emulation.
- Silent best-effort fallback output for unsupported constructs.

## Architecture

`cs2.cpp` should be reshaped around the same broad pipeline discipline that already exists in `cs2.ts`, but with native-focused runtime rules.

The backend should be split into four layers:

1. `Analysis`
   - Uses Roslyn to discover types, members, inheritance, generic usage, and constructs that need special lowering.
   - Produces enough metadata to decide whether a type can be emitted or must fail with an explicit diagnostic.

2. `Normalization`
   - Maps C# source constructs into a portable intermediate representation suitable for C++ emission.
   - Resolves namespace and type remaps before code generation.
   - Lowers C#-specific surface features into explicit C++ shapes.

3. `Runtime Requirement Registration`
   - Registers the native support types and helpers that generated code is allowed to depend on.
   - Keeps generated engine code dependent on a small `helcpp` runtime layer instead of directly depending on compiler-specific or Windows-specific APIs.

4. `Emission`
   - Writes one header and one source file per converted type.
   - Writes generated configuration headers and support runtime files.
   - Emits a manifest or diagnostic report for unsupported constructs and conversion failures.

The generated engine core must depend on a controlled runtime abstraction layer rather than on arbitrary direct STL or platform usage. That is the main protection against Windows-first assumptions leaking into the long-term portable core.

## Target Profiles

The converter should not hardcode compiler or platform assumptions into the emitter. Instead, it should expose three explicit profile dimensions.

### Platform Profile

Describes operating-system and runtime constraints for the generated output.

Initial profile:

- `windows-headless`

Planned future profiles:

- `ps2-headless`
- `gamecube-headless`
- `wii-headless`
- `psp-headless`

This profile controls behavior such as:

- Path and filesystem assumptions.
- Endianness-related support hooks.
- Alignment or packing assumptions when needed.
- Availability of runtime helpers.

### Compiler Profile

Describes the toolchain and dialect-specific behavior.

Initial profiles:

- `msvc`
- `gcc`

This profile controls behavior such as:

- Compiler-specific pragmas.
- Warning suppression strategy.
- Inline and force-inline attributes.
- Calling-convention macros.
- Compiler-specific workarounds required by emitted code or runtime support.

### Runtime Profile

Describes what the generated code is allowed to use from the support runtime and the standard library.

Initial profiles:

- `minimal`
- `stl-lite`

This profile controls whether generated code may use:

- `std::string`
- `std::vector`
- `std::unordered_map`
- Exceptions
- RTTI

## Generated Configuration Contract

`cs2.cpp` should emit a generated configuration header that exposes stable compile-time flags consumed by the runtime layer.

Examples:

- `HE_CPP_COMPILER_MSVC`
- `HE_CPP_COMPILER_GCC`
- `HE_CPP_PLATFORM_WINDOWS`
- `HE_CPP_RUNTIME_MINIMAL`
- `HE_CPP_USE_STD_STRING`
- `HE_CPP_USE_EXCEPTIONS`
- `HE_CPP_USE_RTTI`

Compiler and platform differences should live in the runtime or configuration layer first. The emitter should only vary generated syntax when the C++ source itself genuinely must differ.

## Phase 1 Feature Set

Phase 1 should be defined as: convert `helengine.core` with explicit exclusions, generate structurally correct C++, and fail hard on unsupported constructs.

### Pipeline Structure

Add native equivalents for the TypeScript backend organization:

- A `CPPConversionOptions` model.
- Compiler, platform, and runtime profile selection.
- Staged preprocessing and filtering.
- Runtime requirement registration.
- Output manifest and diagnostic reporting.

### Type Emission

Support these type categories first:

- Classes.
- Structs.
- Enums.
- Abstract classes.
- Interfaces lowered to pure abstract base classes.
- Inheritance.
- Nested types.
- Static classes.

### Member Emission

Support these member categories first:

- Fields.
- Constructors.
- Methods.
- Method overloads.
- Static methods.
- Constants and readonly fields where representable.
- Properties lowered with a simple and consistent rule.

Recommended property lowering rule:

- Trivial auto-properties become fields.
- Non-trivial properties become getter and setter methods.

This keeps the output simpler and more portable than trying to emulate C# property syntax directly.

### Type Mapping

Add an explicit type-mapping layer for:

- Numeric primitives.
- `bool`.
- `string`.
- Arrays.
- Selected known generics such as `List<T>` and `Dictionary<TKey, TValue>`.
- `IDisposable`.
- Simple `IEquatable<T>` patterns where needed by `helengine.core`.

### Expression and Statement Support

Prioritize the constructs most likely to appear across `helengine.core`:

- Object creation.
- Member access.
- Method calls.
- `if` and `else`.
- `switch`.
- Loops.
- Assignments.
- Arithmetic and comparison operations.
- `this` and `base`.
- Casts.
- Enum access.
- Static member access.

### Runtime Support

Add a small native support runtime for:

- String handling through either wrappers or profile-selected aliases.
- List and dictionary abstractions.
- Binary reader and writer base abstractions.
- Utility helpers required by generated code.

This runtime should be designed so the initial Windows-friendly setup can use `stl-lite`, while later retro-console profiles can replace that implementation with stricter runtime support.

### Failure Reporting

Unsupported constructs must never silently degrade into placeholder output.

The converter should emit diagnostics that identify:

- The source type or member.
- The syntax construct that failed.
- The reason the construct is unsupported.
- The recommended mapping or next implementation action when known.

This is required so `helengine.core` conversion can proceed as a measured gap-closing effort instead of guesswork.

## Implementation Strategy

The work should proceed in three milestones.

### Milestone 1: Restructure `cs2.cpp`

Refactor the current backend into a profile-driven conversion pipeline without attempting to solve all missing language features immediately.

Primary additions:

- `CPPConversionOptions`
- `CompilerProfile`
- `PlatformProfile`
- `RuntimeProfile`
- Runtime requirement catalog and registrar
- Conversion report and diagnostic model
- Pipeline wiring in `CPPCodeConverter`

### Milestone 2: Analyze `helengine.core` End-to-End

Run the converter against `helengine.core` and produce partially emitted output plus a precise unsupported-feature report.

This milestone exists to replace speculation with actual failure data from the real engine package.

### Milestone 3: Close the `helengine.core` Gap

Implement missing constructs in priority order based on the diagnostics gathered from the previous milestone until `helengine.core` converts cleanly within the supported phase-1 profile.

## Recommended Defaults

The initial development defaults should be:

- Platform profile: `windows-headless`
- Compiler profile: `msvc`
- Runtime profile: `stl-lite`

These defaults are recommended only as the first validation configuration. The generated architecture must remain portable enough to move later to stricter runtime and compiler environments without requiring a backend rewrite.

## Open Decisions

No blocking design questions remain for phase 1.

The next task should be an implementation plan focused on restructuring `cs2.cpp` before feature-by-feature syntax expansion.
