# Windows DirectX11 Black Window Design

## Goal

Boot the `helengine-windows` native host into the smallest real Windows runtime milestone: a visible Win32 window backed by a real DirectX 11 device and swap chain that clears to black and presents continuously.

## Scope

Included in this slice:
- one native Win32 window
- one Direct3D 11 device
- one DXGI swap chain
- one back-buffer render target view
- a standard message pump
- a clear-to-black render loop
- clean process shutdown when the window closes

Explicitly excluded from this slice:
- generated-core lifecycle hookup
- feature-pruned subsystem registration
- input abstraction
- depth buffers
- resize-aware swap-chain recreation beyond basic destruction safety
- multiple windows
- Vulkan abstraction
- higher-level renderer resource systems

## Architecture

The first milestone should stay close to raw Windows and DirectX APIs. The host repo needs to prove it can boot a native process cleanly before it starts absorbing engine lifecycle, renderer layering, or platform abstraction.

The implementation is split into four focused units:

1. `main.cpp`
   Owns only process entry and delegates to the application runner.

2. `Win32Application`
   Owns window-class registration, startup sequencing, message pumping, and orderly shutdown.

3. `Win32Window`
   Owns the `HWND`, client-size tracking, title, and the static-to-instance window-procedure bridge.

4. `DirectX11Bootstrap` and `DirectX11Presenter`
   Own device creation, swap-chain creation, back-buffer view creation, clear, bind, and present.

## Initialization Flow

1. `main()` constructs `Win32Application` and calls `Run()`.
2. `Win32Application::Run()` registers the window class and creates the main window.
3. After the window exists and has a client size, `Win32Application` creates `DirectX11Bootstrap` with the `HWND`, width, and height.
4. The main loop pumps messages with `PeekMessage`.
5. When no quit message is pending, the presenter binds the back buffer, clears it to black, and presents.
6. Closing the window posts quit and exits the loop cleanly.

## DirectX11 Policy

- Use hardware device creation only for this slice.
- Use a descending feature-level list compatible with modern Windows 10 and Windows 11.
- Use a windowed, double-buffered swap chain.
- Use `DXGI_FORMAT_B8G8R8A8_UNORM`.
- Prefer a flip-model swap effect consistent with the existing engine DirectX11 path.
- Start with `Present(1, 0)` to avoid a needless hot-spin loop.

If any required native object fails to initialize, the process should fail fast instead of silently degrading.

## CMake and Dependency Policy

The first slice should add only the Windows system libraries needed for the bootstrap:
- `d3d11`
- `dxgi`
- `user32`
- `gdi32`

Additional libraries should be added only when real usage requires them.

The generated core remains part of the repository build contract, but this milestone should not depend on any generated-core lifecycle entrypoints.

## Success Criteria

The slice is complete when:
- the Windows host opens a visible native window
- the client area clears to black every frame
- closing the window exits the process cleanly
- the code structure leaves a clean seam for later resize handling and generated-core startup

## Next Slice Boundary

After the black-window milestone, the next implementation slices should be:
1. resize-aware swap-chain recreation
2. minimal logging/assert reporting for native bootstrap failures
3. generated-core lifecycle hookup
