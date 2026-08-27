# Current test-project rendering fixture generator

This maintained engine tool regenerates the committed rendering scene catalog and its two material-settings dependencies through the current editor authoring APIs.

From the engine repository root, run:

```powershell
dotnet run --project tools/current-test-project-scene-generator/helengine.current-test-project-scene-generator.csproj
```

The default target is the repository's `test-project` directory. To target another test-project root, pass:

```powershell
dotnet run --project tools/current-test-project-scene-generator/helengine.current-test-project-scene-generator.csproj -- --project-root C:\path\to\test-project
```

The tool writes the ten files listed by `RenderingSceneFixtureGenerator.SceneFileNames` under `assets/Scenes/rendering`, the current `Scenes/Bootstrap.helen` fixture, the `TransparentStandard.helmat` and `DoubleSidedStandard.helmat` common-settings documents, and the seven PS2 basis material families (each with current common, PS2 override, and Windows override documents) under `assets/Materials/rendering`. This is 34 deterministic native outputs. Generated native identities are derived from stable logical paths, so a second run must produce identical bytes.
