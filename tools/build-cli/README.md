# Portable build CLI

This host runs the existing build and editor-command pipeline without WinForms or a GPU. It uses CPU authoring resources and explicit file-backed font rasterization. It does not run a player.

Supply `--default-font-source` explicitly because a headless host has no editor font registry. Authored font assets continue to use their own faces and settings. Existing sidecar importer identifiers remain stable: raster sources use the portable Magick/FreeType implementation; no fallback to installed fonts occurs. The Windows desktop host keeps its existing GDI importers.

Build the CLI from the real engine checkout using its pinned .NET SDK and a fixed external artifacts/output root. Pass the existing `--project`, `--build`, `--build-profile` and `--output` arguments after the host option. The platform builder still owns the native build and its host requirements.

This initial portable registration supports textures, source fonts, text and Assimp models. Audio source importers are not registered; an authored audio source requiring decoding is a reported integration gap rather than a successful empty asset.

Example server invocation after publishing the tool:

```sh
dotnet /tmp/helengine/linux/cli/helengine.buildcli.dll \
  --default-font-source /home/helena/dev/helprojs/demodisc/assets/fonts/DemoDiscBody.ttf \
  --project /home/helena/dev/helprojs/demodisc/project.heproj \
  --build linux --build-profile debug --output /tmp/helengine/linux/package
```
