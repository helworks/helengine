# Component execution in the editor

Scene components are data by default while owned by `EditorCore`. The editor
attaches their instances and loads their serialized properties, but does not run
their gameplay lifecycle callbacks or updates. This applies to every `Component`,
including classes that do not inherit `UpdateComponent`, and does not depend on a
hidden component being present on the entity.

Use `[RunInEditor]` on a concrete component class when its behavior is intentionally
needed during authoring:

```csharp
[RunInEditor]
public sealed class AuthoringPreviewComponent : UpdateComponent {
    public override void Update() {
        // Explicit authoring preview behavior.
    }
}
```

The attribute is not inherited. A gameplay subclass of a renderer or another
opted-in component must declare its own attribute to execute in the editor.
Built-in visual, layout, input, and editor-tool components explicitly opt in;
gameplay animation, audio playback, and scene switching do not.

Scene `ScrollComponent` and `ScrollBarComponent` also remain data during authoring:
they do not process scrolling or generate runtime scrollbar entities in the scene
hierarchy. Editor panels use `EditorScrollComponent` and `EditorScrollBarComponent`,
which explicitly opt in so editor UI scrolling and dragging remain available.

The policy covers attachment, hierarchy initialization, enabled/static changes,
removal, reparent registration, and component updates. The update loop also checks
previously registered components. Editor ownership enforces the policy outside
frame callbacks, while explicit editor execution scopes remain available to
authoring services that use a runtime core.

Constructors, serialized property accessors, and `Dispose` still run: they are
needed to materialize, inspect, and release component data. Put gameplay side
effects in lifecycle callbacks rather than constructors or property accessors.
Ordinary runtime cores keep normal gameplay execution without requiring the
attribute.
