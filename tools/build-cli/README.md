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
