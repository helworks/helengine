using System.Text.Json;
using helengine.baseplatform.Definitions;
using helengine.editor.tests.testing;
using helengine.platforms;
using Xunit;

namespace helengine.editor.tests.managers.play;

/// <summary>
/// Verifies the Play platform selector's option filtering, persisted choice and fallback resolution, kept apart from the
/// editor preview platform.
/// </summary>
public sealed class EditorPlayPlatformSelectionServiceTests : IDisposable {
    /// <summary>
    /// Scratch project root holding the `user_settings/project.json` file under test.
    /// </summary>
    readonly string ProjectRootPath;

    /// <summary>
    /// Warnings the service under test reported, in order.
    /// </summary>
    readonly List<string> Warnings;

    /// <summary>
    /// Creates an isolated scratch project root for one test.
    /// </summary>
    public EditorPlayPlatformSelectionServiceTests() {
        ProjectRootPath = Path.Combine(@"C:\dev\helworks\builds\helengine\play-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ProjectRootPath);
        Warnings = new List<string>();
    }

    /// <summary>
    /// Deletes the scratch project root.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(ProjectRootPath)) {
            Directory.Delete(ProjectRootPath, true);
        }
    }

    /// <summary>
    /// Ensures the options list holds only platforms that are project-supported, installed and Play-capable, in the
    /// project's supported-platform order.
    /// </summary>
    [Fact]
    public void Refresh_ListsOnlyPlayableInstalledSupportedPlatformsInProjectOrder() {
        string[] supported = ["windows", "ps2", "psp", "gc", "n64"];
        EditorPlayPlatformSelectionService service = CreateService(supported);

        service.Refresh(supported, [
            CreateBuilder("n64", "Nintendo 64", true, true),
            CreateBuilder("windows", "Windows", false, true),
            CreateBuilder("psp", "PSP", true, false),
            CreateBuilder("ps2", "PlayStation 2", true, true),
            CreateBuilder("dc", "Dreamcast", true, true)
        ]);

        Assert.Equal(new[] { "ps2", "n64" }, service.Options.Select(option => option.PlatformId).ToArray());
        Assert.Equal("PlayStation 2", service.Options[0].DisplayName);
        Assert.Equal(@"C:\builders\ps2.dll", service.Options[0].BuilderAssemblyPath);
        Assert.Equal("ps2.adapter", service.Options[0].PlayerCapability.AdapterTypeName);
    }

    /// <summary>
    /// Ensures a chosen Play platform is persisted and restored by a fresh service, as on the next editor boot.
    /// </summary>
    [Fact]
    public void Select_ThenReload_RestoresTheLastChosenPlatform() {
        string[] supported = ["ps2", "n64"];
        EditorPlayPlatformSelectionService first = CreateService(supported);
        first.Refresh(supported, CreatePlayableBuilders("ps2", "n64"));

        first.Select("n64");

        EditorPlayPlatformSelectionService second = CreateService(supported);
        second.Refresh(supported, CreatePlayableBuilders("ps2", "n64"));
        Assert.True(second.HasSelection);
        Assert.Equal("n64", second.SelectedOption.PlatformId);
        Assert.Equal("n64", ReadSettingsProperty("lastPlayPlatform"));
        Assert.Empty(Warnings);
    }

    /// <summary>
    /// Ensures a missing key resolves to the first playable platform without a warning.
    /// </summary>
    [Fact]
    public void Refresh_WithoutSavedChoice_SelectsFirstPlayableWithoutWarning() {
        string[] supported = ["windows", "n64", "ps2"];
        EditorPlayPlatformSelectionService service = CreateService(supported);

        service.Refresh(supported, CreatePlayableBuilders("ps2", "n64"));

        Assert.Equal("n64", service.SelectedOption.PlatformId);
        Assert.Empty(Warnings);
    }

    /// <summary>
    /// Ensures a saved platform that is no longer playable falls back to the first playable one and is reported once,
    /// even when the selection is resolved again.
    /// </summary>
    [Fact]
    public void Refresh_WhenSavedPlatformIsNotPlayable_FallsBackAndWarnsOnce() {
        string[] supported = ["windows", "ps2"];
        WriteSettingsFile("{ \"activePlatform\": \"windows\", \"lastPlayPlatform\": \"windows\" }");
        EditorPlayPlatformSelectionService service = CreateService(supported);
        IReadOnlyList<EditorLoadedPlatformBuilder> builders = [
            CreateBuilder("windows", "Windows", true, false),
            CreateBuilder("ps2", "PlayStation 2", true, true)
        ];

        service.Refresh(supported, builders);
        service.Refresh(supported, builders);

        Assert.Equal("ps2", service.SelectedOption.PlatformId);
        Assert.Single(Warnings);
        Assert.Contains("windows", Warnings[0]);
        Assert.Equal("windows", ReadSettingsProperty("lastPlayPlatform"));
    }

    /// <summary>
    /// Ensures a saved platform that is not installed falls back to the first playable one with a warning.
    /// </summary>
    [Fact]
    public void Refresh_WhenSavedPlatformIsNotInstalled_FallsBackWithWarning() {
        string[] supported = ["n64", "ps2"];
        WriteSettingsFile("{ \"activePlatform\": \"ps2\", \"lastPlayPlatform\": \"n64\" }");
        EditorPlayPlatformSelectionService service = CreateService(supported);

        service.Refresh(supported, [
            CreateBuilder("n64", "Nintendo 64", false, true),
            CreateBuilder("ps2", "PlayStation 2", true, true)
        ]);

        Assert.Equal("ps2", service.SelectedOption.PlatformId);
        Assert.Single(Warnings);
    }

    /// <summary>
    /// Ensures no playable platform leaves the selector without a selection and refuses to resolve a Play target.
    /// </summary>
    [Fact]
    public void Refresh_WhenNothingIsPlayable_HasNoSelection() {
        string[] supported = ["windows"];
        EditorPlayPlatformSelectionService service = CreateService(supported);

        service.Refresh(supported, [CreateBuilder("windows", "Windows", true, false)]);

        Assert.Empty(service.Options);
        Assert.False(service.HasSelection);
        Assert.Throws<InvalidOperationException>(() => service.SelectedOption);
        Assert.Throws<InvalidOperationException>(() => service.Select("windows"));
    }

    /// <summary>
    /// Ensures the Play platform is independent of the editor preview platform: choosing a Play platform leaves
    /// `activePlatform` alone, and changing the preview platform leaves the Play choice alone.
    /// </summary>
    [Fact]
    public void PlayPlatform_IsIndependentOfThePreviewPlatform() {
        string[] supported = ["windows", "ps2", "n64"];
        EditorProjectLocalSettingsService settings = new EditorProjectLocalSettingsService(ProjectRootPath, supported);
        settings.SaveActivePlatform("windows");
        EditorPlayPlatformSelectionService service = new EditorPlayPlatformSelectionService(settings, Warnings.Add);
        service.Refresh(supported, CreatePlayableBuilders("ps2", "n64"));

        service.Select("n64");
        settings.SaveActivePlatform("ps2");

        Assert.Equal("ps2", settings.LoadActivePlatform());
        Assert.Equal("n64", settings.LoadLastPlayPlatform());
        Assert.Equal("n64", service.SelectedOption.PlatformId);
    }

    /// <summary>
    /// Ensures selecting a platform that is not offered is rejected instead of being persisted.
    /// </summary>
    [Fact]
    public void Select_WhenPlatformIsNotAnOption_Throws() {
        string[] supported = ["windows", "ps2"];
        EditorPlayPlatformSelectionService service = CreateService(supported);
        service.Refresh(supported, CreatePlayableBuilders("ps2"));

        Assert.Throws<InvalidOperationException>(() => service.Select("windows"));
        Assert.Equal("ps2", service.SelectedOption.PlatformId);
    }

    /// <summary>
    /// Creates the service under test over the scratch project root.
    /// </summary>
    /// <param name="supported">Project-supported platform ids.</param>
    /// <returns>Selection service reporting warnings into <see cref="Warnings"/>.</returns>
    EditorPlayPlatformSelectionService CreateService(IReadOnlyList<string> supported) {
        return new EditorPlayPlatformSelectionService(new EditorProjectLocalSettingsService(ProjectRootPath, supported), Warnings.Add);
    }

    /// <summary>
    /// Creates installed, Play-capable loaded builders for the supplied platform ids.
    /// </summary>
    /// <param name="platformIds">Platform ids to create builders for.</param>
    /// <returns>Loaded builders in the supplied order.</returns>
    static IReadOnlyList<EditorLoadedPlatformBuilder> CreatePlayableBuilders(params string[] platformIds) {
        List<EditorLoadedPlatformBuilder> builders = new List<EditorLoadedPlatformBuilder>();
        for (int index = 0; index < platformIds.Length; index++) {
            builders.Add(CreateBuilder(platformIds[index], platformIds[index].ToUpperInvariant(), true, true));
        }

        return builders;
    }

    /// <summary>
    /// Creates one loaded builder whose catalog descriptor and Play capability follow the supplied flags.
    /// </summary>
    /// <param name="platformId">Catalog platform id.</param>
    /// <param name="displayName">Catalog display name.</param>
    /// <param name="installed">Whether the catalog reports the platform payload as installed.</param>
    /// <param name="supportsPlay">Whether the builder definition declares Play support.</param>
    /// <returns>Loaded builder for the selection service.</returns>
    static EditorLoadedPlatformBuilder CreateBuilder(string platformId, string displayName, bool installed, bool supportsPlay) {
        PlatformPlayerCapability capability = supportsPlay
            ? new PlatformPlayerCapability(true, platformId + ".adapter", platformId + "-staging")
            : PlatformPlayerCapability.CreateUnsupported();
        AvailablePlatformDescriptor descriptor = new AvailablePlatformDescriptor(
            platformId,
            displayName,
            @"C:\builders\" + platformId + ".dll",
            isInstalled: installed);
        return new EditorLoadedPlatformBuilder(descriptor, new RecordingPlayPlatformAssetBuilder(capability));
    }

    /// <summary>
    /// Writes raw `user_settings/project.json` content into the scratch project.
    /// </summary>
    /// <param name="json">JSON payload to persist.</param>
    void WriteSettingsFile(string json) {
        string directoryPath = Path.Combine(ProjectRootPath, "user_settings");
        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(Path.Combine(directoryPath, "project.json"), json);
    }

    /// <summary>
    /// Reads one string property from the persisted `user_settings/project.json`.
    /// </summary>
    /// <param name="propertyName">JSON property name.</param>
    /// <returns>Stored property value.</returns>
    string ReadSettingsProperty(string propertyName) {
        string json = File.ReadAllText(Path.Combine(ProjectRootPath, "user_settings", "project.json"));
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty(propertyName).GetString();
    }
}
