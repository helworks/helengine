using helengine.editor.tests.testing;

namespace helengine.editor.tests.managers.menu;

/// <summary>
/// Verifies the editor-side menu scene regeneration service rewrites baked menu scenes through the normal serializer.
/// </summary>
public sealed class EditorMenuSceneRegenerationServiceTests : IDisposable {
    /// <summary>
    /// Temporary project root used by the current test instance.
    /// </summary>
    readonly string ProjectRootPath;

    /// <summary>
    /// Initializes one isolated project root for scene regeneration tests.
    /// </summary>
    public EditorMenuSceneRegenerationServiceTests() {
        ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-menu-scene-regeneration-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Scenes"));
    }

    /// <summary>
    /// Deletes the temporary project root after each test.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(ProjectRootPath)) {
            Directory.Delete(ProjectRootPath, true);
        }
    }

    /// <summary>
    /// Ensures regenerating one menu scene writes current generic menu component ids.
    /// </summary>
    [Fact]
    public void Regenerate_WhenInvoked_WritesMenuComponentTypeIds() {
        ScriptTypeResolver resolver = new ScriptTypeResolver();
        resolver.Register("gameplay", typeof(TestMenuDefinitionProvider).Assembly);
        EditorMenuSceneRegenerationService service = new EditorMenuSceneRegenerationService(ProjectRootPath, resolver);

        service.Regenerate("Scenes/DemoDiscMainMenu.helen", typeof(TestMenuDefinitionProvider).FullName + ", gameplay");

        string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", "DemoDiscMainMenu.helen");
        using FileStream stream = File.OpenRead(scenePath);
        SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));

        SceneEntityAsset menuEntity = Assert.Single(sceneAsset.RootEntities, entity => entity.Name == "DemoDiscMenuRoot");
        Assert.Contains(menuEntity.Components, component => component.ComponentTypeId == MenuComponent.SerializedComponentTypeId);
        Assert.Contains(menuEntity.Components, component => component.ComponentTypeId == "helengine.ViewportComponent, helengine.core");
        string serializedContents = System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(scenePath));
        Assert.DoesNotContain("helengine.DemoMenuBuildComponent", serializedContents, StringComparison.Ordinal);
        Assert.DoesNotContain("helengine.ReferenceCanvasFitComponent, helengine.core", serializedContents, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures regenerating the Nintendo DS menu scene writes the dual-screen camera structure and viewport-backed menu root.
    /// </summary>
    [Fact]
    public void Regenerate_WhenInvokedForNintendoDs_WritesDualScreenMenuScene() {
        ScriptTypeResolver resolver = new ScriptTypeResolver();
        resolver.Register("gameplay", typeof(TestMenuDefinitionProvider).Assembly);
        EditorMenuSceneRegenerationService service = new EditorMenuSceneRegenerationService(ProjectRootPath, resolver);

        service.Regenerate("Scenes/DemoDiscMainMenuDs.helen", typeof(TestMenuDefinitionProvider).FullName + ", gameplay");

        string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", "DemoDiscMainMenuDs.helen");
        using FileStream stream = File.OpenRead(scenePath);
        SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));

        Assert.Collection(
            sceneAsset.RootEntities,
            entity => Assert.Equal("DemoDiscTopScreenCamera", entity.Name),
            entity => Assert.Equal("DemoDiscBottomScreenCamera", entity.Name));
        SceneEntityAsset topCameraEntity = sceneAsset.RootEntities[0];
        SceneEntityAsset bottomCameraEntity = sceneAsset.RootEntities[1];
        Assert.Contains(topCameraEntity.Components, component => component.ComponentTypeId == "helengine.CameraComponent");
        Assert.Contains(bottomCameraEntity.Components, component => component.ComponentTypeId == "helengine.CameraComponent");

        SceneEntityAsset topMenuEntity = Assert.Single(topCameraEntity.Children, entity => entity.Name == "DemoDiscMenuRoot");
        Assert.Contains(topMenuEntity.Components, component => component.ComponentTypeId == MenuComponent.SerializedComponentTypeId);
        Assert.Contains(topMenuEntity.Components, component => component.ComponentTypeId == "helengine.ViewportComponent, helengine.core");
        SceneEntityAsset bottomRootEntity = Assert.Single(bottomCameraEntity.Children, entity => entity.Name == "DemoDiscBottomScreenRoot");
        Assert.Contains(bottomRootEntity.Components, component => component.ComponentTypeId == "helengine.ViewportComponent, helengine.core");
        Assert.DoesNotContain(bottomRootEntity.Components, component => component.ComponentTypeId == "helengine.FPSComponent");
        Assert.DoesNotContain(FlattenEntityNames(bottomRootEntity), name => name == "DemoDiscOverlayImage");
        Assert.DoesNotContain(
            FlattenEntityNames(bottomRootEntity),
            name => name.Contains("heading", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, CountComponents(sceneAsset.RootEntities, "helengine.FPSComponent"));

        string serializedContents = System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(scenePath));
        Assert.DoesNotContain("helengine.ReferenceCanvasFitComponent, helengine.core", serializedContents, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the Nintendo DS menu generator flattens the large translucent panel surface against the menu background so the DS software renderer can blit it through the opaque rounded-rect cache.
    /// </summary>
    [Fact]
    public void Regenerate_WhenInvokedForNintendoDs_FlattensPanelSurfaceFillToOpaqueBackgroundComposite() {
        ScriptTypeResolver resolver = new ScriptTypeResolver();
        resolver.Register("gameplay", typeof(TestMenuDefinitionProvider).Assembly);
        EditorMenuSceneRegenerationService service = new EditorMenuSceneRegenerationService(ProjectRootPath, resolver);

        service.Regenerate("Scenes/DemoDiscMainMenuDs.helen", typeof(TestMenuDefinitionProvider).FullName + ", gameplay");

        string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", "DemoDiscMainMenuDs.helen");
        using FileStream stream = File.OpenRead(scenePath);
        SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
        SceneEntityAsset panelSurfaceEntity = FindEntityByName(sceneAsset.RootEntities, "panel-main-surface");
        SceneComponentAssetRecord roundedRectRecord = Assert.Single(panelSurfaceEntity.Components, component => component.ComponentTypeId == "helengine.RoundedRectComponent");
        RoundedRectComponent roundedRectComponent = ReadRoundedRectComponent(roundedRectRecord);

        Assert.Equal(new byte4(60, 40, 80, 255), roundedRectComponent.FillColor);
        Assert.Equal(new byte4(120, 86, 153, 255), roundedRectComponent.BorderColor);
    }

    /// <summary>
    /// Counts matching component records throughout one scene hierarchy.
    /// </summary>
    /// <param name="entities">Scene entities to inspect.</param>
    /// <param name="componentTypeId">Serialized component type identifier to count.</param>
    /// <returns>Total matching component count.</returns>
    static int CountComponents(SceneEntityAsset[] entities, string componentTypeId) {
        if (entities == null) {
            throw new ArgumentNullException(nameof(entities));
        } else if (string.IsNullOrWhiteSpace(componentTypeId)) {
            throw new ArgumentException("Component type id must be provided.", nameof(componentTypeId));
        }

        int count = 0;
        for (int index = 0; index < entities.Length; index++) {
            SceneEntityAsset entity = entities[index];
            if (entity == null) {
                continue;
            }

            SceneComponentAssetRecord[] components = entity.Components ?? Array.Empty<SceneComponentAssetRecord>();
            for (int componentIndex = 0; componentIndex < components.Length; componentIndex++) {
                if (string.Equals(components[componentIndex].ComponentTypeId, componentTypeId, StringComparison.Ordinal)) {
                    count++;
                }
            }

            count += CountComponents(entity.Children ?? Array.Empty<SceneEntityAsset>(), componentTypeId);
        }

        return count;
    }

    /// <summary>
    /// Flattens one scene-entity subtree into a sequence of entity names.
    /// </summary>
    /// <param name="entity">Root scene entity to traverse.</param>
    /// <returns>Flattened entity names in depth-first order.</returns>
    static IEnumerable<string> FlattenEntityNames(SceneEntityAsset entity) {
        if (entity == null) {
            throw new ArgumentNullException(nameof(entity));
        }

        List<string> names = new List<string>();
        CollectEntityNames(entity, names);
        return names;
    }

    /// <summary>
    /// Collects entity names from one scene-entity subtree.
    /// </summary>
    /// <param name="entity">Root scene entity to traverse.</param>
    /// <param name="names">Destination list receiving collected entity names.</param>
    static void CollectEntityNames(SceneEntityAsset entity, List<string> names) {
        if (entity == null) {
            throw new ArgumentNullException(nameof(entity));
        }
        if (names == null) {
            throw new ArgumentNullException(nameof(names));
        }

        names.Add(entity.Name ?? string.Empty);
        SceneEntityAsset[] children = entity.Children ?? Array.Empty<SceneEntityAsset>();
        for (int index = 0; index < children.Length; index++) {
            CollectEntityNames(children[index], names);
        }
    }

    /// <summary>
    /// Reads the file-backed font reference assigned to one serialized FPS component.
    /// </summary>
    /// <param name="record">Serialized FPS component record to inspect.</param>
    /// <returns>Project-relative font path referenced by the FPS component.</returns>
    static string ReadFpsFontRelativePath(SceneComponentAssetRecord record) {
        if (record == null) {
            throw new ArgumentNullException(nameof(record));
        }

        FontAsset placeholderFont = new FontAsset(
            new FontInfo("Test", 16, 4f),
            new TestRuntimeTexture {
                Width = 1,
                Height = 1
            },
            new Dictionary<char, FontChar>(),
            16f,
            1,
            1);
        TestSceneAssetReferenceResolver referenceResolver = new TestSceneAssetReferenceResolver();
        SceneAssetReference titleFontReference = new SceneAssetReference {
            SourceKind = SceneAssetReferenceSourceKind.FileSystem,
            RelativePath = "Fonts/DemoDiscTitle.ttf",
            ProviderId = string.Empty,
            AssetId = string.Empty
        };
        referenceResolver.RegisterFont(titleFontReference, placeholderFont);
        referenceResolver.RegisterFont(
            new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = "fonts/title.hefont",
                ProviderId = string.Empty,
                AssetId = string.Empty
            },
            placeholderFont);
        EntitySaveComponent saveComponent = new EntitySaveComponent();
        FPSComponentPersistenceDescriptor descriptor = new FPSComponentPersistenceDescriptor();
        FPSComponent fpsComponent = Assert.IsType<FPSComponent>(descriptor.DeserializeComponent(record, saveComponent, referenceResolver));
        Assert.Same(placeholderFont, fpsComponent.Font);
        Assert.True(saveComponent.TryGetComponentState(fpsComponent, out EntityComponentSaveState saveState));
        Assert.True(saveState.TryGetAssetReference(FontAssetScenePersistenceSupport.FontReferenceName, out SceneAssetReference fontReference));
        return fontReference.RelativePath;
    }

    /// <summary>
    /// Reads the serialized refresh interval assigned to one FPS component record.
    /// </summary>
    /// <param name="record">Serialized FPS component record to inspect.</param>
    /// <returns>Refresh interval seconds deserialized from the component payload.</returns>
    static double ReadFpsRefreshIntervalSeconds(SceneComponentAssetRecord record) {
        if (record == null) {
            throw new ArgumentNullException(nameof(record));
        }

        FontAsset placeholderFont = new FontAsset(
            new FontInfo("Test", 16, 4f),
            new TestRuntimeTexture {
                Width = 1,
                Height = 1
            },
            new Dictionary<char, FontChar>(),
            16f,
            1,
            1);
        TestSceneAssetReferenceResolver referenceResolver = new TestSceneAssetReferenceResolver();
        referenceResolver.RegisterFont(
            new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = "Fonts/DemoDiscTitle.ttf",
                ProviderId = string.Empty,
                AssetId = string.Empty
            },
            placeholderFont);
        referenceResolver.RegisterFont(
            new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = "fonts/title.hefont",
                ProviderId = string.Empty,
                AssetId = string.Empty
            },
            placeholderFont);
        EntitySaveComponent saveComponent = new EntitySaveComponent();
        FPSComponentPersistenceDescriptor descriptor = new FPSComponentPersistenceDescriptor();
        FPSComponent fpsComponent = Assert.IsType<FPSComponent>(descriptor.DeserializeComponent(record, saveComponent, referenceResolver));
        return fpsComponent.RefreshIntervalSeconds;
    }

    /// <summary>
    /// Finds one scene entity by name across the supplied hierarchy.
    /// </summary>
    /// <param name="entities">Scene entities to search.</param>
    /// <param name="name">Entity name to resolve.</param>
    /// <returns>Matching scene entity.</returns>
    static SceneEntityAsset FindEntityByName(SceneEntityAsset[] entities, string name) {
        if (entities == null) {
            throw new ArgumentNullException(nameof(entities));
        }
        if (string.IsNullOrWhiteSpace(name)) {
            throw new ArgumentException("Entity name must be provided.", nameof(name));
        }

        for (int index = 0; index < entities.Length; index++) {
            SceneEntityAsset entity = entities[index];
            if (entity == null) {
                continue;
            }

            if (string.Equals(entity.Name, name, StringComparison.Ordinal)) {
                return entity;
            }

            try {
                return FindEntityByName(entity.Children ?? Array.Empty<SceneEntityAsset>(), name);
            } catch (InvalidOperationException) {
            }
        }

        throw new InvalidOperationException("Expected scene entity '" + name + "' to exist.");
    }

    /// <summary>
    /// Deserializes one rounded-rectangle component record from the generated scene payload.
    /// </summary>
    /// <param name="record">Serialized rounded-rectangle component record.</param>
    /// <returns>Deserialized rounded-rectangle component.</returns>
    static RoundedRectComponent ReadRoundedRectComponent(SceneComponentAssetRecord record) {
        if (record == null) {
            throw new ArgumentNullException(nameof(record));
        }

        RoundedRectComponentPersistenceDescriptor descriptor = new RoundedRectComponentPersistenceDescriptor();
        EntitySaveComponent saveComponent = new EntitySaveComponent();
        TestSceneAssetReferenceResolver referenceResolver = new TestSceneAssetReferenceResolver();
        return Assert.IsType<RoundedRectComponent>(descriptor.DeserializeComponent(record, saveComponent, referenceResolver));
    }
}
