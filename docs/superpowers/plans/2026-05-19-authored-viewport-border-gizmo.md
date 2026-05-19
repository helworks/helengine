# Authored Viewport Border Gizmo Plan

1. Add editor-only viewport border resources.
   - Add one shared plane mesh resource.
   - Add one border-only shader and material factory.
   - Add one parameter helper for border thickness and color.

2. Add authored viewport border synchronization.
   - Add one sync component that scans authored `ViewportComponent` instances.
   - Create and remove internal gizmo entities as authored viewports appear or disappear.
   - Synchronize transform, size, and material parameters each frame.

3. Attach the sync component to editor viewport stacks.
   - Wire the new component into viewport workspace setup and state.

4. Verify.
   - Add focused editor tests for authored/internal viewport behavior and transform/size synchronization.
   - Run the smallest relevant `dotnet test` slice.
   - Rebuild `helengine.editor.app`.
