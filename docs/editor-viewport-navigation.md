# Editor viewport navigation

Each workspace scene viewport has a navigation cube at the upper-right of its content area. It controls that viewport's editor camera; it does not modify authored cameras, entities, or scene assets.

Click a visible face to align the camera to Front, Back, Left, Right, Top, or Bottom and switch to orthographic projection. Top and Bottom use a stable up direction. Click an edge or corner to move to the corresponding diagonal while retaining the current projection. Click the cube center to switch between perspective and orthographic projection without changing the view orientation or orbit pivot. Hovering a face, edge, corner, or the center highlights the target and labels the destination; camera changes occur only after a click. The view transition takes about 200 ms.

Drag the cube to orbit around the viewport's current pivot. A movement of four logical UI pixels starts orbiting; smaller movement remains a click. The `Persp` / `Ortho` control below the cube also switches projection while keeping orientation and apparent scale at the pivot. The overlay captures the pointer through release, including when it leaves the cube, and releases capture on focus loss, resize below the minimum size, or viewport closure.

The existing viewport controls remain available: right mouse button plus WASD/QE for free-look movement, middle mouse button to pan, Alt plus middle mouse button to orbit, and the scroll wheel to zoom. Orthographic zoom changes the visible vertical span, and pan and transform tools use projection-aware world-space rays and scale.

Projection support is an optional `ICameraProjectionSettings` capability. Ordinary runtime cameras continue to use perspective projection. The editor viewport camera and the picker camera are editor-only components with explicit `[RunInEditor]` opt-ins; the navigation cube updater also opts in explicitly because the editor suppresses unmarked update components during scene authoring. The overlay is viewport-owned, excluded from scene picking and scene depth, and disposes its private texture and pointer blockers when detached.

Workspace state stores each viewport's projection mode, orthographic vertical span, camera transform, and explicit orbit pivot. `HasOrbitPivot` distinguishes an origin pivot from an older document with no pivot field. Older workspace documents restore to perspective and derive a usable pivot from the camera and the controller's default orbit distance; a missing orthographic span is derived from that pivot and the camera's actual field of view. These additions affect workspace panel state only. Authored scene formats and serialized camera components remain unchanged.

## Validation

Behavioral tests cover all 26 face, edge, and corner targets; visible-surface hit testing; click/orbit threshold and pointer capture; focus loss and disposal; projection rays, matrices, and span; camera framing and all transform gizmo tools; viewport-local ownership; old and new workspace documents; interrupted-transition capture; and scene/gizmo/picker camera synchronization. The plan's combined changed-area filter passed 552 tests with no skips, including live UI-scale alignment, unchanged-layout drag retention, and disjoint clip-range synchronization. DirectX11 and Vulkan project builds each succeeded with zero warnings and zero errors. The Windows input project also built successfully; it reported one unrelated IDE0040 warning in `WindowsAudioVoice.cs`. Test logs and build output are recorded under `C:/dev/helworks/builds/viewport-navigation-cube`.

This change was not manually exercised in an interactive editor window. No screenshots were taken.
