# HelEngine UI binary format, version 1

Implementation source is C# in this directory. TypeScript is exported from that source using `tools/ui-format-export`; there is no separate production JS codec. This is a format module, not a renderer or app integration. Managed compilation, TypeScript export, strict TS compilation and bidirectional byte conformance passed on 2026-11-16. Generated C++ ownership/codegen and host wire tests now pass; renderer integration and iPad execution remain separate boundaries.

## Encoding

All multibyte values are little-endian. Floats are finite IEEE-754 binary32, independent of the engine's configurable math precision. Strings are `uint32 byteLength` followed by strictly valid UTF-8, no terminator or normalization. A BOM inside a string is literal U+FEFF. Integer ids are positive signed int32; parent -1 means root. Boolean values are exactly 0 or 1. Packed colors are uint32 RRGGBBAA (so RGBA 11,22,33,44 has numeric value 0x11223344 and wire bytes 44 33 22 11).

Header (16 bytes): `HELE` (4), endian=1 (u8), version=1 (u8), format=0x5549 (u16), record=1 (u16), value=1 (u16), payloadBytes (u32). The first 12 bytes follow EngineBinaryHeaderSerializer's convention. Payload length excludes all 16 header bytes and must equal the remaining file exactly.

Payload:

1. `metricCount:u16`; each metric: name:string, value:f32.
2. `templateCount:u16`; each template: name:string, nodeCount:u32, nodes.
3. Each node: id:i32, parentId:i32, name:string, x/y/z/width/height:f32, visible:u8, componentCount:u16, components.
4. Each component: kind:u16, propertyCount:u16, properties.
5. Each property: id:u16, valueKind:u8, payload selected by the value kind.

Value kinds: 1=f32 Number, 2=i32 Integer, 3=u8 Boolean, 4=String, 5=u32 Color, 6=TextBinding, 7=ResourceBinding, 8=ActionBinding, 9=Theme. Kinds 4 and 6–9 carry a string. Keys are literal names with no `$` prefix convention, expression language or executable code.

Component kinds: Rectangle=1, Text=2, Image=3, Clip=4, Button=5. Stable property IDs and their admissible component/value combinations are defined in UiPropertyId.cs and UiPropertyRules.cs. FontWeight integers: Regular=0, Medium=1, Semibold=2, Bold=3. Alignment: Left=0, Center=1, Right=2. ImageFit: Stretch=0, Contain=1. Corner flags: top-left=1, top-right=2, bottom-left=4, bottom-right=8. FontSize is 1..256 logical points.

Required properties: Text.Text, Image.Image, Button.Action. Other properties may be absent; the model preserves absence rather than inventing a stored value. Rendering defaults/policy are a separate adapter contract; this codec alone does not define pixel-identical rendering. Node visibility composes with ancestors. Local x/y/z and bounds retain declared order and values; z represents ordering input, not platform depth-buffer units. Named metrics have no built-in library-specific meaning.

## Admission and determinism

Maximum file 4 MiB; one string 4096 encoded bytes; 256 metrics; 64 templates; 4096 total nodes; 5 components/node; 19 properties/component. Counts and minimum record lengths are checked before allocation. Names are nonempty; template/metric names are unique; node names and ids are unique within a template; parents must appear earlier in the same template. One instance of each component kind per node; no repeated property id. Width/height and radius/border width are nonnegative. All numbers must fit finite float32; generated JS integer fields must also be integral and within their wire ranges.

The writer preserves declared ordering; identical ordered models produce identical bytes. It does not sort semantically equivalent permutations. Inactive tagged-property fields are not serialized. Floating point conversion occurs at the wire boundary; negative zero is retained. Fixed-size output buffers prevent runtime stream capacity from affecting output. Unknown headers/components/properties/value kinds, malformed Unicode, truncation and trailing bytes are errors, never fallback defaults. No compression, embedded executable code or migration is part of v1.

## Verification status and workflow

Verified on 2026-11-16: the C# module and console suite compiled without warnings and the suite passed. The exporter built successfully (its first build reported nine warnings in existing converter/Nucleus sources), generated TypeScript, and the complete generated strict configuration compiled. Both the card and extended property/hierarchy fixtures completed C# → generated TypeScript → C# with identical bytes. All nine value kinds, parent links, unsigned colors, literal U+FEFF, Unicode, signed coordinates and negative zero were exercised. Truncations and malformed inputs in the conformance suites were rejected. This proves the tested wire contract, not renderer or native C++ parity.

After explicit build/codegen authorization, reuse a single output root (for this development task, C:\temp\helengine-ui-format) for managed outputs, exported TS and test exchange files. Do not create per-attempt outputs. Build the test executable and exporter from the real main checkout. Run the exporter against this real csproj; compile its TS with the generated strict configuration, then run the managed suite, managed `--emit`, JS harness and managed `--verify`. No native app build, packaging or UI changes are required for that isolated check. Do not patch generated TS: fix the C# source or converter.

The exporter references the engine's existing `engine/vendor/csharpcodegen` revision. It does not switch, update or reset that checkout. It rejects a nonempty output folder lacking its ownership marker. A failed first export may leave files requiring inspection before cleanup/retry; it never deletes arbitrary preexisting output.

### Repeating the authorized cross-runtime check

The exporter requires existing runtime metadata and formatter before construction; it fails rather than allowing the converter to install npm dependencies automatically. Its MSBuild bootstrap uses the same Microsoft.Build.Locator 1.7.8 dependency as the vendor sample. The first authorized run may reveal missing prepared dependencies; dependency installation remains a separate decision.

After the managed executables and exporter have been built into the fixed C:\temp\helengine-ui-format tree and the exporter has written C:\temp\helengine-ui-format\typescript, use an **already installed** TypeScript compiler. The generated strict configuration intentionally has noEmit=true. For the Node test only, emit CommonJS with the override below; browser bundling remains a later integration step. No edits to generated code are necessary.

```powershell
tsc -p C:\temp\helengine-ui-format\typescript\tsconfig.strict.json --noEmit false --module commonjs --outDir C:\temp\helengine-ui-format\javascript
dotnet <built-test-dll> --emit C:\temp\helengine-ui-format\csharp.bin
node C:\dev\helworks\helengine\engine\helengine.ui.format.tests\generated-conformance.mjs C:\temp\helengine-ui-format\javascript\helengine-ui-format.js C:\temp\helengine-ui-format\csharp.bin C:\temp\helengine-ui-format\typescript.bin
dotnet <built-test-dll> --verify C:\temp\helengine-ui-format\typescript.bin
```

Run the managed suite without arguments as well. `<built-test-dll>` is the actual output path selected by the authorized build, not a source snapshot. The CommonJS output must be outside a parent package declaring type=module; otherwise give that test output directory a CommonJS package declaration. All test outputs here are temporary and must be removed after validation.

### Recorded verification details

- Card fixture: 114 bytes, SHA-256 `b6b2436c86610962283e55f3922f1b6bd1f39f394735c2e46b859432123d95fd` in both runtimes.
- Extended fixture: SHA-256 `9b4bdd9009e0853a7d61fb8a5a20d58eb0917b386329565a44a6adfad8b3cad9` in both runtimes.
- Emit the extended C# fixture using `--emit-rich`; pass its input/output paths as the optional final two arguments to `generated-conformance.mjs`; validate the TS result with `--verify-rich`.
- Build commands used `--artifacts-path C:\temp\helengine-ui-format\managed`; the exporter build additionally used `-p:TargetFramework=net9.0 -m:1`. The actual test DLL was `C:\temp\helengine-ui-format\managed\bin\helengine.ui.format.tests\debug\helengine.ui.format.tests.dll`.
- Missing runtime metadata was explicitly generated over the built `.net.ts` runtime with the vendor `extractor.js`, using the already installed TypeScript dependency via `NODE_PATH`. No npm install was run. The existing native formatter executable was selected with `CS2_TS_TSGO`.
- Strict TS checking exposed an existing overload mismatch in `engine/vendor/csharpcodegen/cs2.ts/.net.ts/system/argument-null.exception.ts`. Its source declaration now accepts `string | null | undefined`, matching its existing implementation and caller. Output was regenerated from source; generated TS was never patched.
- Only this format, exporter and their dependencies were built. No app build, native codegen, install, simulator, Mac GUI or user-document operations occurred. Temporary outputs and logs were removed after verification.

### Generated C++ wire verification (2026-11-16 continuation)

The C++ format conversion now passes and its generated unity source compiles with MSVC on Windows using the iOS conversion profile. The native harness at C:\dev\helworks\helengine\engine\helengine.ui.format.tests\native-conformance.cpp reads and rewrites the 114-byte and 221-byte golden fixtures and the 1239-byte library asset with identical bytes, including fixtures written by generated TypeScript. This verifies host-side wire behavior, not iPad execution or native lifetime safety.

UiBinaryCodec.Write now owns a pre-sized MemoryStream and obtains its content with ToArray, instead of assuming the input array is shared by every runtime. Byte output is unchanged. The generated TypeScript was regenerated and passed strict compilation and cross-runtime tests after this correction. BCL ownership metadata, native binary IO/UTF-8 methods, and reserved-identifier cleanup emission were fixed in converter/runtime sources; generated output was never patched.

The helengine.nativeownership dependency is approved and implemented. UiDocument/UiTemplate/UiNode/UiComponent are disposable owners of their arrays and elements. Getters borrow; setters take exclusive ownership and dispose/delete the prior contents; self-assignment is retained. Arrays must not share owned elements. Detach retained elements from their old slots before replacing an owning array. The caller must explicitly release an overwritten slot's old value. Dispose tolerates partial arrays and repeated calls. In native code, Dispose must precede deleting the root. Read borrows bytes and returns an owned tree; Write borrows the tree and returns owned bytes.

Native input reader/stream storage and UTF-8 buffers are explicitly released. Failed reads release partially constructed trees when the runtime supports exceptions. The current default iOS profile uses fatal errors, without recovery; that platform policy was not changed.

Ownership verification: MSVC debug CRT checkpoints found zero net live allocations for 20 repeated read/write/replace/dispose operations per 114/221/1239-byte fixture. Both fatal and exception-enabled profiles passed valid paths; the exception profile additionally passed consistent-header truncations and trailing-payload cleanup. Managed ownership tests, generated TypeScript strict compilation and byte/disposal conformance, 11 focused TS intrinsic tests, 14 web UI checks and 3 route checks passed. The isolated helengine.ui codegen preflight passed. These are host tests, not iPad graphics execution. Full evidence is in C:\dev\probia\docs\helengine-web-ui.md.
