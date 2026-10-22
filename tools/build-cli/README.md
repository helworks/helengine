# Portable build CLI

This host runs the existing build and editor-command pipeline without WinForms or a GPU. It uses CPU authoring resources and explicit file-backed font rasterization. It does not run a player.

Supply `--default-font-source` explicitly because a headless host has no editor font registry. Authored font assets continue to use their own faces and settings. Existing sidecar importer identifiers remain stable: raster sources use the portable Magick/FreeType implementation; no fallback to installed fonts occurs. The Windows desktop host keeps its existing GDI importers.

Build the CLI from the real engine checkout using its pinned .NET SDK and a fixed external artifacts/output root. Pass the existing `--project`, `--build`, `--build-profile` and `--output` arguments after the host option. The platform builder still owns the native build and its host requirements.

The Roslyn/MSBuild workspace used by codegen must resolve the same restore directory as the authoring build. When using external artifacts, export `UseArtifactsOutput=true` and `ArtifactsPath` for the CLI process as well as the initial build. Otherwise a fresh checkout has no default `obj/project.assets.json`; missing framework references can produce misleading ownership errors. Do not disable ownership validation to work around missing restore inputs.

For the Linux development host, build from `/home/helena/dev/helworks/helengine`:

```sh
export UseArtifactsOutput=true
export ArtifactsPath=/tmp/helengine/linux/managed
export HELENGINE_SOURCE_ROOT=/home/helena/dev/helworks/helengine
export HELENGINE_BUILD_WORKSPACE_ROOT=/tmp/helengine/linux/workspace
export HELENGINE_BUILD_CACHE_ROOT=/tmp/helengine/linux/cache
export HELENGINE_BUILD_CONFIGURATION=Debug
export HELENGINE_BUILD_PROFILE=debug
export HELENGINE_LINUX_REPOSITORY_ROOT=/home/helena/dev/helworks/helengine-linux
export HELENGINE_LINUX_NATIVE_PREFIX=/home/helena/dev/helworks/tools/linux-native/usr
export LD_LIBRARY_PATH="$HELENGINE_LINUX_NATIVE_PREFIX/lib/x86_64-linux-gnu${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
dotnet build /home/helena/dev/helworks/helengine-linux/builder/helengine.linux.builder.csproj \
  -c Debug --artifacts-path "$ArtifactsPath"
dotnet build tools/build-cli/helengine.buildcli.csproj -c Debug --artifacts-path "$ArtifactsPath"
dotnet publish engine/vendor/csharpcodegen/codegen/codegen.csproj -c Release \
  --artifacts-path /tmp/helengine/linux/codegen-managed \
  -o /tmp/helengine/linux/managed/bin/helengine.buildcli/debug/codegen
```

Codegen additionally needs Doxygen on `PATH` or an explicit `CS2_DOXYGEN_PATH`. Publishing beside the CLI uses the normal host executable name (`codegen` on Unix, `codegen.exe` on Windows) without creating a second repository checkout. Platform builder registration belongs in `/home/helena/dev/helworks/helengine/user_settings/platforms.json` and must point to the actual native builder assembly and sibling source checkout.

This initial portable registration supports textures, source fonts, text and Assimp models. Audio source importers are not registered; an authored audio source requiring decoding is a reported integration gap rather than a successful empty asset.

Example server invocation after publishing the tool:

```sh
dotnet /tmp/helengine/linux/managed/bin/helengine.buildcli/debug/helengine.buildcli.dll \
  --default-font-source /home/helena/dev/helprojs/demodisc/assets/fonts/DemoDiscBody.ttf \
  --project /home/helena/dev/helprojs/demodisc/project.heproj \
  --build linux --build-profile debug --output /tmp/helengine/linux/package
```

On the Mac development host, build from `/Users/distro/dev/helworks/helengine` after installing the Apple sibling's locked SDK dependencies:

```sh
export UseArtifactsOutput=true
export ArtifactsPath=/tmp/helengine/macos/managed
export HELENGINE_SOURCE_ROOT=/Users/distro/dev/helworks/helengine
export HELENGINE_BUILD_WORKSPACE_ROOT=/tmp/helengine/macos/workspace
export HELENGINE_BUILD_CACHE_ROOT=/tmp/helengine/macos/cache
export HELENGINE_BUILD_CONFIGURATION=Debug
export HELENGINE_BUILD_PROFILE=debug
export HELENGINE_APPLE_ROOT=/Users/distro/dev/helworks/helengine-apple
export DYLD_LIBRARY_PATH=/Users/distro/dev/helworks/tools/shaderc/lib
export PATH="/Users/distro/.local/bin:/Users/distro/.dotnet:/Users/distro/dev/helworks/tools/cmake-3.31.12-macos-universal/CMake.app/Contents/bin:$PATH"
dotnet build /Users/distro/dev/helworks/helengine-apple/builder/macos/helengine.macos.builder.csproj \
  -c Debug --artifacts-path "$ArtifactsPath"
dotnet build tools/build-cli/helengine.buildcli.csproj -c Debug --artifacts-path "$ArtifactsPath"
dotnet publish engine/vendor/csharpcodegen/codegen/codegen.csproj -c Release \
  --artifacts-path /tmp/helengine/macos/codegen-managed \
  -o /tmp/helengine/macos/managed/bin/helengine.buildcli/debug/codegen
dotnet /tmp/helengine/macos/managed/bin/helengine.buildcli/debug/helengine.buildcli.dll \
  --default-font-source /Users/distro/dev/helprojs/demodisc/assets/fonts/DemoDiscBody.ttf \
  --project /Users/distro/dev/helprojs/demodisc/project.heproj \
  --build macos --build-profile debug --output /tmp/helengine/macos/package
```

The macOS authoring build uses Xcode's `xcrun clang` to compile a universal arm64/x86_64 library, `libhelengine.authoring.native.dylib`. It keeps the Darwin variadic `open`, `openat` and `fcntl` calls inside C, exposing fixed-arity entrypoints to managed code. MSBuild copies this dependency beside the CLI for both build and publish; distribute it with the CLI. It is an authoring-host dependency and is not part of the generated iOS player.

Build and publish the CLI and converter natively on each host. A Windows-built CLI folder does not include the Darwin shim or a Unix converter executable. The native player profile is independently validated by its sibling; a successful cook or staged manifest alone does not establish that the player links or renders every selected scene.

For a native CPU profiling build, retain the same fixed platform directories, set `HELENGINE_BUILD_PROFILE=profiler` and pass `--build-profile profiler` to the CLI. Linux and macOS use optimized native code with symbols and the pinned Tracy transport. Their profile defaults enable generated scopes for `helengine.Core.`, `helengine.SceneManager.` and `helengine.RenderCommandListBuilder2D.`; the converter separates these prefixes with semicolons. Component, entity and frame-stage counters use the existing Core profiling sink. GPU timestamp profiling is not included. Enabling generated scopes under native Debug or Release is rejected before staging because those profiles do not link Tracy.

For a Release build, set `HELENGINE_BUILD_CONFIGURATION=Release`, `HELENGINE_BUILD_PROFILE=release` and pass `--build-profile release`. This generates a separate Release Core through the normal pipeline, then overwrites the same final package. Reusing Profiler-generated C++ under a different native CMake profile does not validate a Release build.

Apple builds capture `HELENGINE_APPLE_SDK` once before shader registration or cache lookup. The iOS choices are `iphoneos` and `iphonesimulator`; macOS uses `macosx`. The immutable `PlatformCookContext` carries the selected SDK, its version and the compiler producer identity through cooking, cache keys, managed manifest provenance and the native request. The Apple consumer rejects mismatched manifest/request contexts before staging. Changing the environment after capture does not change that build's SDK. These authoring contracts do not enter the generated Core or change the asset ABI.

Use the iOS builder registration, `--build ios` and the fixed output `/tmp/helengine/ios/package` for either iOS SDK. Both SDKs share that final output directory; changing SDK overwrites the previous application. The SDK identity separates cached cooked inputs without clearing the cache or changing builder DLLs. An unsigned application confirms cook, codegen and native linking only; it does not establish device or simulator execution. Follow the Apple sibling's pinned toolchain setup and validation commands before building on a fresh Mac.
