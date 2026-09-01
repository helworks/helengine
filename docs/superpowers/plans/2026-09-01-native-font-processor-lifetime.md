# Native Font Processor Lifetime Fix

## Goal

Keep the packaged runtime's `RenderManager2D` alive and directly addressable by the
font content processor after `Core.Initialize` returns, so generated C++ can load
the DemoDisc menu font without dereferencing a dangling lambda capture.

## Proven failure

The generated `RuntimeContentManagerConfiguration.cpp` currently lowers the font
processor delegate to a C++ lambda with `[&]`. That delegate is stored by the
`ContentManager`, but it captures the `ConfigureSharedAssetContentManager`
parameter by reference. The parameter's stack lifetime ends when configuration
returns. On the first packaged font load, `FontAssetBinarySerializer::Deserialize`
receives null and throws `ArgumentNullException("renderManager2D")`.

## Constraints

- Keep this engine-side seam small; do not add a collection of path-tracer helpers.
- Do not change DirectX texture-region behavior or activate Vulkan.
- Do not globally change C++ lambda lowering in this fix. C# closures can share
  mutable captured variables, so blindly changing all `[&]` captures to `[=]`
  would trade the lifetime bug for incorrect closure semantics elsewhere.
- Preserve unrelated worktree changes, especially
  `AutomaticScriptComponentRuntimeDeserializer.cs`.

## Implementation

### 1. Add a failing lifetime regression

Add a focused `helengine.core.tests` test that configures a content manager with a
test `RenderManager2D`, returns from the configuration helper, and then loads a
minimal packaged font through the registered processor. The test must prove that
the renderer supplied during configuration is the renderer used later.

Where a direct managed test cannot expose native capture lifetime, add a focused
generated-source contract assertion that the native output does not contain an
escaping `[&]` font lambda and does contain a processor-owned renderer field.

### 2. Replace the escaping delegate with one processor object

Add one dedicated `IContentProcessor<FontAsset>` implementation in
`helengine.core/content`. Its constructor requires and stores `RenderManager2D`;
`Read(Stream)` validates the stream and calls
`FontAssetBinarySerializer.Deserialize(stream, storedRenderer)`. Implement
`OutputType` and `IContentProcessor.ReadObject` consistently with the existing
binary processor.

Change `RuntimeContentManagerConfiguration` to register this processor whenever a
2D renderer is supplied. Keep headless behavior unchanged when the renderer is
null.

### 3. Verify managed and generated behavior

Run the focused core tests, then regenerate the native core with the contained
codegen executable. Confirm generated C++ stores the renderer as an object field
and no longer retains a reference to the configuration method's stack parameter.

### 4. Verify the actual Windows failure path

Rebuild the retained DirectX11 Debug CMake target and launch it for at least 15
seconds. It must complete the first `EngineCore->Draw`, stay alive and responsive,
and produce no `renderManager2D`, fatal, unhandled-exception, or Vulkan log entry.

After that passes, rebuild the retained Release target and run the packaged
DemoDisc navigation smoke through the Software Path Tracer scene.

