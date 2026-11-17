# HelEngine UI adapter

C:\dev\helworks\helengine\engine\helengine.ui is the separate engine component adapter approved for the portable binary UI format. It references helengine.core and helengine.ui.format; neither the format nor the core references it.

Use the existing content boundary:

```csharp
using UiDocument document = core.ContentManager.Load<UiDocument>("ui/library.heui", new UiContentProcessor());
using UiView view = UiView.Create(parent, document, "artwork", bindings, drawableScale);
```

The loaded document belongs to the caller and must be disposed after its last use; view disposal does not own it. The parent and IUiBindings provider must outlive the view. Dispose the view before either owner. Textures are borrowed and remain application-owned. Theme/resource/action policy belongs to that provider, not this module. Missing bindings must throw; an explicitly absent image may return null. Accessibility labels are retained as view metadata for a future platform accessibility bridge.

Supported component defaults and native limitations are documented in C:\dev\probia\docs\helengine-web-ui.md. Button options that the current core cannot represent fail explicitly (non-centered/wrapped text or unsupported corner masks). Instantiation rolls back its subtree on failure. The content processor accepts partial/non-seekable input, bounds it to the format limit, and never closes its caller-owned stream.

Focused managed validation on 2026-11-16 used C:\dev\probia\assets\ui\library.heui, the same bytes loaded by Chrome. The executable in C:\dev\helworks\helengine\engine\helengine.ui.tests covers real core components, explicit densities1/2/3, callbacks, disposal/rollback, borrowed image lifetime, image aspect ratio, noncontiguous IDs, visibility, packed colors, stream limits/partial reads and ContentManager integration.

Build only with user authorization, from the real checkout. Reuse C:\temp\helengine-ui-format\managed with --artifacts-path and -p:UseSharedCompilation=false. Run the built helengine.ui.tests.dll with the absolute fixture path above. No app project is compiled by this test.

Integration: the editor references this module and registers a generic .heui cooker. The cooker validates bounded input with the shared codec, preserves the original bytes beneath cooked/, and records the output in the scene dependency closure. App engine-module declarations and the Apple platform's generated-core project list include helengine.ui and helengine.ui.format. The library uses its packaged SceneAssetReference through ContentManager rather than embedding its definition in app code.

Validation includes managed component tests, generated C++ wire/ownership tests, focused iOS adapter/app codegen preflights and a complete iPad native build. Managed tests and codegen reports alone do not verify rendering or interaction on a physical device.
