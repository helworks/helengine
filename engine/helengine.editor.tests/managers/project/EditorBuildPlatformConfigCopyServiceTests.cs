using helengine.editor;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Covers the copy and per-scene ordering mutations the build dialog applies to persisted platform build configurations.
/// </summary>
public class EditorBuildPlatformConfigCopyServiceTests {
    /// <summary>
    /// Creates one platform configuration carrying the supplied scene-order entries.
    /// </summary>
    /// <param name="platformId">Platform identifier assigned to the configuration.</param>
    /// <param name="sceneOrders">Scene-order entries stored on the configuration.</param>
    /// <returns>Platform configuration usable by the copy service.</returns>
    static EditorBuildPlatformConfigDocument CreatePlatformConfig(string platformId, params EditorBuildSceneOrderDocument[] sceneOrders) {
        EditorBuildPlatformConfigDocument platformConfig = new() {
            PlatformId = platformId
        };

        for (int index = 0; index < sceneOrders.Length; index++) {
            platformConfig.SceneOrders.Add(sceneOrders[index]);
        }

        return platformConfig;
    }

    [Fact]
    public void CopyStringValues_replaces_the_destination_contents_in_place() {
        List<string> destinationValues = ["stale-a", "stale-b"];

        EditorBuildPlatformConfigCopyService.CopyStringValues(["windows", "ps2"], destinationValues);

        Assert.Equal(["windows", "ps2"], destinationValues);
    }

    [Fact]
    public void CopyStringValues_throws_when_the_destination_is_missing() {
        Assert.Throws<ArgumentNullException>(() => EditorBuildPlatformConfigCopyService.CopyStringValues(["windows"], null));
    }

    [Fact]
    public void CopySelectedSceneIds_replaces_the_destination_selection() {
        EditorBuildPlatformConfigDocument sourcePlatformConfig = CreatePlatformConfig("windows");
        sourcePlatformConfig.SelectedSceneIds.Add("scenes/a.scene");
        sourcePlatformConfig.SelectedSceneIds.Add("scenes/b.scene");
        EditorBuildPlatformConfigDocument destinationPlatformConfig = CreatePlatformConfig("ps2");
        destinationPlatformConfig.SelectedSceneIds.Add("scenes/stale.scene");

        EditorBuildPlatformConfigCopyService.CopySelectedSceneIds(sourcePlatformConfig, destinationPlatformConfig);

        Assert.Equal(["scenes/a.scene", "scenes/b.scene"], destinationPlatformConfig.SelectedSceneIds);
    }

    [Fact]
    public void CopySceneOrders_clones_entries_instead_of_sharing_them() {
        EditorBuildPlatformConfigDocument sourcePlatformConfig = CreatePlatformConfig(
            "windows",
            new EditorBuildSceneOrderDocument { SceneId = "scenes/a.scene", OrderNumber = 7 });
        EditorBuildPlatformConfigDocument destinationPlatformConfig = CreatePlatformConfig(
            "ps2",
            new EditorBuildSceneOrderDocument { SceneId = "scenes/stale.scene", OrderNumber = 1 });

        EditorBuildPlatformConfigCopyService.CopySceneOrders(sourcePlatformConfig, destinationPlatformConfig);

        EditorBuildSceneOrderDocument copiedSceneOrder = Assert.Single(destinationPlatformConfig.SceneOrders);
        Assert.Equal("scenes/a.scene", copiedSceneOrder.SceneId);
        Assert.Equal(7, copiedSceneOrder.OrderNumber);
        Assert.NotSame(sourcePlatformConfig.SceneOrders[0], copiedSceneOrder);
    }

    [Fact]
    public void CopySceneOrders_throws_when_the_source_is_missing() {
        Assert.Throws<ArgumentNullException>(() => EditorBuildPlatformConfigCopyService.CopySceneOrders(null, CreatePlatformConfig("ps2")));
    }

    [Fact]
    public void EnsureSceneOrderEntries_drops_stale_entries_and_appends_new_scenes_after_the_highest_order() {
        EditorBuildPlatformConfigDocument platformConfig = CreatePlatformConfig(
            "windows",
            new EditorBuildSceneOrderDocument { SceneId = "scenes/removed.scene", OrderNumber = 1 },
            new EditorBuildSceneOrderDocument { SceneId = "scenes/a.scene", OrderNumber = 4 });

        EditorBuildPlatformConfigCopyService.EnsureSceneOrderEntries(platformConfig, ["scenes/a.scene", "scenes/b.scene", "scenes/c.scene"]);

        Assert.Equal(3, platformConfig.SceneOrders.Count);
        Assert.Equal(4, EditorBuildPlatformConfigCopyService.FindSceneOrder(platformConfig, "scenes/a.scene").OrderNumber);
        Assert.Equal(5, EditorBuildPlatformConfigCopyService.FindSceneOrder(platformConfig, "scenes/b.scene").OrderNumber);
        Assert.Equal(6, EditorBuildPlatformConfigCopyService.FindSceneOrder(platformConfig, "scenes/c.scene").OrderNumber);
    }

    [Fact]
    public void EnsureSceneOrderEntries_creates_the_order_list_when_the_document_has_none() {
        EditorBuildPlatformConfigDocument platformConfig = CreatePlatformConfig("windows");
        platformConfig.SceneOrders = null;

        EditorBuildPlatformConfigCopyService.EnsureSceneOrderEntries(platformConfig, ["scenes/a.scene"]);

        Assert.Single(platformConfig.SceneOrders);
        Assert.Equal(1, platformConfig.SceneOrders[0].OrderNumber);
    }

    [Fact]
    public void GetNextSceneOrderNumber_returns_one_past_the_highest_saved_order() {
        EditorBuildPlatformConfigDocument platformConfig = CreatePlatformConfig(
            "windows",
            new EditorBuildSceneOrderDocument { SceneId = "scenes/a.scene", OrderNumber = 2 },
            new EditorBuildSceneOrderDocument { SceneId = "scenes/b.scene", OrderNumber = 9 });

        Assert.Equal(10, EditorBuildPlatformConfigCopyService.GetNextSceneOrderNumber(platformConfig));
    }

    [Fact]
    public void GetSceneOrderNumber_falls_back_to_the_catalog_position_when_no_usable_entry_exists() {
        EditorBuildPlatformConfigDocument platformConfig = CreatePlatformConfig(
            "windows",
            new EditorBuildSceneOrderDocument { SceneId = "scenes/b.scene", OrderNumber = 0 });

        Assert.Equal(2, EditorBuildPlatformConfigCopyService.GetSceneOrderNumber(platformConfig, "scenes/b.scene", ["scenes/a.scene", "scenes/b.scene"]));
    }

    [Fact]
    public void GetSceneOrderNumber_returns_max_value_for_scenes_outside_the_catalog() {
        EditorBuildPlatformConfigDocument platformConfig = CreatePlatformConfig("windows");

        Assert.Equal(int.MaxValue, EditorBuildPlatformConfigCopyService.GetSceneOrderNumber(platformConfig, "scenes/unknown.scene", ["scenes/a.scene"]));
    }

    [Fact]
    public void CreateSceneIdsOrderedByOrderNumber_sorts_by_saved_order_then_catalog_position() {
        EditorBuildPlatformConfigDocument platformConfig = CreatePlatformConfig(
            "windows",
            new EditorBuildSceneOrderDocument { SceneId = "scenes/a.scene", OrderNumber = 3 },
            new EditorBuildSceneOrderDocument { SceneId = "scenes/b.scene", OrderNumber = 1 },
            new EditorBuildSceneOrderDocument { SceneId = "scenes/c.scene", OrderNumber = 1 });
        List<string> catalogSceneIds = ["scenes/a.scene", "scenes/b.scene", "scenes/c.scene"];

        List<string> orderedSceneIds = EditorBuildPlatformConfigCopyService.CreateSceneIdsOrderedByOrderNumber(platformConfig, catalogSceneIds, catalogSceneIds);

        Assert.Equal(["scenes/b.scene", "scenes/c.scene", "scenes/a.scene"], orderedSceneIds);
        Assert.Equal(["scenes/a.scene", "scenes/b.scene", "scenes/c.scene"], catalogSceneIds);
    }

    [Fact]
    public void CreateSceneIdsOrderedByOrderNumber_only_returns_the_requested_subset() {
        EditorBuildPlatformConfigDocument platformConfig = CreatePlatformConfig(
            "windows",
            new EditorBuildSceneOrderDocument { SceneId = "scenes/a.scene", OrderNumber = 3 },
            new EditorBuildSceneOrderDocument { SceneId = "scenes/c.scene", OrderNumber = 1 });

        List<string> orderedSceneIds = EditorBuildPlatformConfigCopyService.CreateSceneIdsOrderedByOrderNumber(
            platformConfig,
            ["scenes/a.scene", "scenes/c.scene"],
            ["scenes/a.scene", "scenes/b.scene", "scenes/c.scene"]);

        Assert.Equal(["scenes/c.scene", "scenes/a.scene"], orderedSceneIds);
    }
}
