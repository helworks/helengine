# Shared native Vulkan renderer

C++20/Vulkan graphics and optional generated-core bridge. The existing managed renderer next to this directory is independent.

`helengine_vulkan` has no SDL, Wayland or operating-system window dependency. Supply instance extensions and a `VulkanSurfaceSource` when presentation is required. The provider must outlive the context and releases the surface after device shutdown. Offscreen construction needs neither a provider nor windowing libraries.

Configure this directory directly to build the renderer; set `HELENGINE_VULKAN_BUILD_TESTS=ON` for CPU contracts. Provide Vulkan headers/loader and glslangValidator. Set `HELENGINE_CORE_CPP_ROOT` to build `helengine_vulkan_bridge` against the actual generated core headers. The application owns/link its generated core implementation. Linux composes this module from its real sibling checkout using `HELENGINE_VULKAN_ROOT`.

Platform hosts retain window/input/desktop integration and packaging. Vulkan on macOS requires a supported Vulkan-over-Metal driver and feature/extension negotiation. Extracting this module does not certify Windows/macOS host builds. Keep the float32 GPU vertex ABI and generated engine numeric policy.

The persistent standalone-build regression is `tests/test_standalone.py --build-root /absolute/external/build`. Use repeated `--cmake-argument=-DNAME=value` for Vulkan/toolchain paths. CPU tests run through CTest; the context-contract test has the `gpu` label and exercises actual device creation plus null/throwing surface providers with validation. An SDL host regression must additionally validate successful presentation surface ownership.

Linux continues to invoke configure/build for every native publication. CMake now tracks this module's production files, so an edited shared renderer causes compilation/linking through the normal dependency graph. No timestamp-only or Linux-source-only executable cache was introduced.

The common CMake controls are `HELENGINE_VULKAN_STATIC_MESH_BATCHING` (OFF), `HELENGINE_VULKAN_STATIC_MESH_BATCHING_BUDGET_MIB` (256), `HELENGINE_VULKAN_DYNAMIC_MESH_BATCHING` (OFF), `HELENGINE_VULKAN_DYNAMIC_MESH_BATCHING_MAX_VERTICES` (1024), and `HELENGINE_VULKAN_DYNAMIC_MESH_BATCHING_BUDGET_MIB` (4). Positive limits are checked as Int32 values; MiB conversion uses uint64. The Linux consumer maps its existing editor/builder selections to these controls before adding this module. Future consumers use the same controls or explicitly configure the runtime renderer before initialization.
