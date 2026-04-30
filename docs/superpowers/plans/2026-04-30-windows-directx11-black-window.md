# Windows DirectX11 Black Window Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Boot `helengine-windows` into a native Win32 window with a real DirectX 11 device, swap chain, black clear, and present loop.

**Architecture:** Keep the first Windows runtime slice minimal and raw. `main.cpp` delegates to a `Win32Application`, which owns startup and the message loop, while `Win32Window`, `DirectX11Bootstrap`, and `DirectX11Presenter` isolate native window and graphics responsibilities behind small, focused files.

**Tech Stack:** C++, Win32 API, Direct3D 11, DXGI, CMake

---

## File Structure

- Create: `C:/dev/helworks/helengine-windows/src/platform/windows/win32/win32_application.hpp`
  - Declares the application runner for startup and the message loop.
- Create: `C:/dev/helworks/helengine-windows/src/platform/windows/win32/win32_application.cpp`
  - Implements startup, DirectX bootstrap creation, and the render loop.
- Create: `C:/dev/helworks/helengine-windows/src/platform/windows/win32/win32_window.hpp`
  - Declares the native window wrapper and window-procedure bridge.
- Create: `C:/dev/helworks/helengine-windows/src/platform/windows/win32/win32_window.cpp`
  - Implements Win32 registration, creation, and message handling.
- Create: `C:/dev/helworks/helengine-windows/src/platform/windows/directx11/directx11_bootstrap.hpp`
  - Declares the D3D11 device/swap-chain bootstrap.
- Create: `C:/dev/helworks/helengine-windows/src/platform/windows/directx11/directx11_bootstrap.cpp`
  - Implements device creation, swap-chain creation, and render-target creation.
- Create: `C:/dev/helworks/helengine-windows/src/platform/windows/directx11/directx11_presenter.hpp`
  - Declares the render-target bind, clear, and present helper.
- Create: `C:/dev/helworks/helengine-windows/src/platform/windows/directx11/directx11_presenter.cpp`
  - Implements the black clear and present path.
- Modify: `C:/dev/helworks/helengine-windows/src/main.cpp`
  - Replace the current feature-bootstrap-only entry with application startup.
- Modify: `C:/dev/helworks/helengine-windows/CMakeLists.txt`
  - Add the new files and required Windows system libraries.

### Task 1: Add a failing bootstrap integration check

**Files:**
- Modify: `C:/dev/helworks/helengine-windows/CMakeLists.txt`
- Modify: `C:/dev/helworks/helengine-windows/src/main.cpp`

- [ ] **Step 1: Write the failing integration shape**

Add references in `CMakeLists.txt` and `src/main.cpp` to `Win32Application` before those files exist.

- [ ] **Step 2: Run the targeted configure/build check to verify it fails**

Run a Windows-host configure or compile command for `helengine-windows`.
Expected: failure because the new Win32 application types/files do not exist yet.

- [ ] **Step 3: Write the minimal application declarations**

Create stub declarations for `Win32Application` so the entry path can compile structurally.

- [ ] **Step 4: Re-run the targeted check**

Run the same configure/build command.
Expected: the failure moves forward to missing implementation or later DirectX pieces.

- [ ] **Step 5: Commit**

```bash
git add CMakeLists.txt src/main.cpp src/platform/windows/win32/win32_application.*
git commit -m "Start Win32 application bootstrap"
```

### Task 2: Add the native Win32 window layer

**Files:**
- Create: `C:/dev/helworks/helengine-windows/src/platform/windows/win32/win32_window.hpp`
- Create: `C:/dev/helworks/helengine-windows/src/platform/windows/win32/win32_window.cpp`
- Test: targeted Windows-host compile/configure command

- [ ] **Step 1: Write the failing integration expectation**

Make `Win32Application` depend on a `Win32Window` type and its window creation API before the implementation exists.

- [ ] **Step 2: Run the targeted check to verify it fails**

Expected: build failure on missing `Win32Window` definitions.

- [ ] **Step 3: Write the minimal `Win32Window` implementation**

Implement:
- class registration
- window creation
- `HWND` storage
- client-size query
- static window proc forwarding
- quit-on-destroy behavior

- [ ] **Step 4: Re-run the targeted check**

Expected: `Win32Window` compiles and the failure moves forward to the DirectX bootstrap layer.

- [ ] **Step 5: Commit**

```bash
git add src/platform/windows/win32/win32_window.* src/platform/windows/win32/win32_application.*
git commit -m "Add Win32 window bootstrap"
```

### Task 3: Add the DirectX11 device and swap-chain bootstrap

**Files:**
- Create: `C:/dev/helworks/helengine-windows/src/platform/windows/directx11/directx11_bootstrap.hpp`
- Create: `C:/dev/helworks/helengine-windows/src/platform/windows/directx11/directx11_bootstrap.cpp`
- Modify: `C:/dev/helworks/helengine-windows/CMakeLists.txt`

- [ ] **Step 1: Write the failing integration expectation**

Make `Win32Application` construct `DirectX11Bootstrap` before the implementation exists.

- [ ] **Step 2: Run the targeted check to verify it fails**

Expected: build failure on missing D3D11 bootstrap symbols.

- [ ] **Step 3: Write the minimal `DirectX11Bootstrap` implementation**

Implement:
- hardware D3D11 device creation
- immediate context acquisition
- DXGI factory access through the device adapter
- windowed swap-chain creation
- back-buffer render-target-view creation
- strict failure behavior with native exceptions/messages

- [ ] **Step 4: Re-run the targeted check**

Expected: failure moves forward to the presenter/render loop instead of initialization symbols.

- [ ] **Step 5: Commit**

```bash
git add CMakeLists.txt src/platform/windows/directx11/directx11_bootstrap.* src/platform/windows/win32/win32_application.*
git commit -m "Add DirectX11 device bootstrap"
```

### Task 4: Add the presenter and render loop

**Files:**
- Create: `C:/dev/helworks/helengine-windows/src/platform/windows/directx11/directx11_presenter.hpp`
- Create: `C:/dev/helworks/helengine-windows/src/platform/windows/directx11/directx11_presenter.cpp`
- Modify: `C:/dev/helworks/helengine-windows/src/platform/windows/win32/win32_application.cpp`

- [ ] **Step 1: Write the failing integration expectation**

Have `Win32Application` call a presenter clear/present path before the presenter implementation exists.

- [ ] **Step 2: Run the targeted check to verify it fails**

Expected: failure on missing presenter type or methods.

- [ ] **Step 3: Write the minimal presenter implementation**

Implement:
- bind render target
- clear to black
- present once per loop iteration

- [ ] **Step 4: Re-run the targeted check**

Expected: the host compiles/configures cleanly and the executable boots to a black window on a Windows host with the required toolchain.

- [ ] **Step 5: Commit**

```bash
git add src/platform/windows/directx11/directx11_presenter.* src/platform/windows/win32/win32_application.cpp
git commit -m "Add DirectX11 black window loop"
```

### Task 5: Final verification

**Files:**
- Verify the files above without broad unrelated changes.

- [ ] **Step 1: Run the focused native verification command**

Run the Windows-host configure/build command for `helengine-windows`.
Expected: successful configure/build for the black-window slice.

- [ ] **Step 2: Run the host manually**

Launch the built executable on a Windows host.
Expected: visible black window, responsive close behavior, clean exit.

- [ ] **Step 3: Inspect repo state**

Run `git status --short` in `C:/dev/helworks/helengine-windows`.
Expected: only intended tracked changes remain before commit, or clean after the final commit.

- [ ] **Step 4: Commit**

```bash
git add CMakeLists.txt src/main.cpp src/platform/windows/win32 src/platform/windows/directx11
git commit -m "Boot Windows host to a DirectX11 black window"
```
