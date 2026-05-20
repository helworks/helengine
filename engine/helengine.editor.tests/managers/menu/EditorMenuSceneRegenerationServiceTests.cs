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
    /// Ensures regenerating the desktop menu scene attaches one debug overlay under the generated root and removes the FPS overlay.
    /// </summary>
    [Fact]
    public void Regenerate_WhenInvokedForDesktop_DoesNotWriteDebugOverlayOnGeneratedRoot() {
        ScriptTypeResolver resolver = new ScriptTypeResolver();
        resolver.Register("gameplay", typeof(TestMenuDefinitionProvider).Assembly);
        EditorMenuSceneRegenerationService service = new EditorMenuSceneRegenerationService(ProjectRootPath, resolver);

        service.Regenerate("Scenes/DemoDiscMainMenu.helen", typeof(TestMenuDefinitionProvider).FullName + ", gameplay");

        string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", "DemoDiscMainMenu.helen");
        using FileStream stream = File.OpenRead(scenePath);
        SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));

        SceneEntityAsset menuEntity = Assert.Single(sceneAsset.RootEntities, entity => entity.Name == "DemoDiscMenuRoot");
        SceneEntityAsset generatedRoot = Assert.Single(menuEntity.Children, entity => entity.Name == DemoMenuLayout.GeneratedRootEntityName);
        Assert.DoesNotContain(
            generatedRoot.Components ?? Array.Empty<SceneComponentAssetRecord>(),
            component => component.ComponentTypeId == "helengine.DebugComponent");

        Assert.Equal(0, CountComponents(sceneAsset.RootEntities, "helengine.DebugComponent"));
        Assert.Equal(0, CountComponents(sceneAsset.RootEntities, "helengine.FPSComponent"));
    }

    /// <summary>
    /// Ensures regenerating the Nintendo DS menu scene writes the dual-screen camera structure with branding on top and the interactive menu on the bottom.
    /// </summary>
    [Fact]
    public void Regenerate_WhenInvokedForNintendoDs_WritesDualScreenMenuScene() {
        InitializeCore();
        MenuDefinition definition = new TestMenuDefinitionProvider().CreateMenuDefinition();
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
        CameraComponent topCameraComponent = ReadCameraComponent(Assert.Single(topCameraEntity.Components, component => component.ComponentTypeId == "helengine.CameraComponent"));
        CameraComponent bottomCameraComponent = ReadCameraComponent(Assert.Single(bottomCameraEntity.Components, component => component.ComponentTypeId == "helengine.CameraComponent"));
        float4 expectedClearColor = new float4(
            definition.AccentColor.X / 255f,
            definition.AccentColor.Y / 255f,
            definition.AccentColor.Z / 255f,
            definition.AccentColor.W / 255f);
        Assert.Equal(expectedClearColor, topCameraComponent.ClearSettings.ClearColor);
        Assert.Equal(expectedClearColor, bottomCameraComponent.ClearSettings.ClearColor);

        SceneEntityAsset topRootEntity = Assert.Single(topCameraEntity.Children, entity => entity.Name == "DemoDiscTopScreenRoot");
        Assert.Contains(topRootEntity.Components, component => component.ComponentTypeId == "helengine.ViewportComponent, helengine.core");
        Assert.DoesNotContain(topRootEntity.Components, component => component.ComponentTypeId == MenuComponent.SerializedComponentTypeId);
        SceneEntityAsset topVersionEntity = Assert.Single(topRootEntity.Children, entity => entity.Name == "DemoDiscTopScreenVersion");
        SceneComponentAssetRecord topVersionRecord = Assert.Single(topVersionEntity.Components, component => component.ComponentTypeId == "helengine.TextComponent");
        TextComponent topVersionComponent = ReadTextComponent(topVersionRecord, FontAssetScenePersistenceSupport.BuildEditorFontReference());
        Assert.Equal("version: 1.2", topVersionComponent.Text);
        Assert.Equal(8f, topVersionEntity.LocalPosition.X);
        Assert.Equal(168f, topVersionEntity.LocalPosition.Y);
        SceneAssetReference topVersionFontReference = ReadTextFontReference(topVersionRecord, FontAssetScenePersistenceSupport.BuildEditorFontReference());
        Assert.Equal(SceneAssetReferenceSourceKind.Generated, topVersionFontReference.SourceKind);
        Assert.Equal("editor", topVersionFontReference.ProviderId);
        Assert.Equal("ui-font", topVersionFontReference.AssetId);
        Assert.Equal("generated/editor/fonts/ui.hefont", topVersionFontReference.RelativePath);
        Assert.DoesNotContain(
            FlattenEntityNames(topRootEntity),
            name => name.Contains("heading", StringComparison.OrdinalIgnoreCase));
        SceneEntityAsset bottomMenuEntity = Assert.Single(bottomCameraEntity.Children, entity => entity.Name == "DemoDiscMenuRoot");
        Assert.Contains(bottomMenuEntity.Components, component => component.ComponentTypeId == MenuComponent.SerializedComponentTypeId);
        Assert.Contains(bottomMenuEntity.Components, component => component.ComponentTypeId == "helengine.ViewportComponent, helengine.core");
        Assert.DoesNotContain(bottomMenuEntity.Components, component => component.ComponentTypeId == "helengine.FPSComponent");
        Assert.DoesNotContain(FlattenEntityNames(bottomMenuEntity), name => name == "DemoDiscOverlayImage");
        Assert.DoesNotContain(
            FlattenEntityNames(bottomMenuEntity),
            name => name.Contains("heading", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, CountComponents(sceneAsset.RootEntities, "helengine.FPSComponent"));

        string serializedContents = System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(scenePath));
        Assert.DoesNotContain("helengine.ReferenceCanvasFitComponent, helengine.core", serializedContents, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the Nintendo DS menu generator leaves footer space below the top-screen logo without changing aspect ratio.
    /// </summary>
    [Fact]
    public void BuildSceneAsset_WhenNintendoDsDefinitionHasOverlayImage_LeavesFooterSpaceBelowTopScreenLogo() {
        MenuDefinition definition = CreateNintendoDsMenuDefinitionWithOverlayImage();
        NintendoDsDemoMenuSceneAssetFactory factory = new NintendoDsDemoMenuSceneAssetFactory();

        SceneAsset sceneAsset = factory.BuildSceneAsset("DemoDiscMainMenuDs", "gameplay.TestMenuDefinitionProvider, gameplay", definition);

        SceneEntityAsset topCameraEntity = Assert.Single(sceneAsset.RootEntities, entity => entity.Name == "DemoDiscTopScreenCamera");
        SceneEntityAsset topRootEntity = Assert.Single(topCameraEntity.Children, entity => entity.Name == "DemoDiscTopScreenRoot");
        SceneEntityAsset topLogoEntity = Assert.Single(topRootEntity.Children, entity => entity.Name == "DemoDiscOverlayImage");
        SpriteComponent topLogoComponent = ReadSpriteComponent(
            Assert.Single(topLogoEntity.Components, component => component.ComponentTypeId == "helengine.SpriteComponent"),
            definition.OverlayImage.TexturePath);

        Assert.Equal(152, topLogoComponent.Size.Y);
        Assert.Equal(0f, topLogoEntity.LocalPosition.Y);
        Assert.Equal(0f, topLogoEntity.LocalPosition.Z);
    }

    /// <summary>
    /// Ensures the Nintendo DS menu generator strips panel chrome and emits flat item visuals for the bottom-screen menu.
    /// </summary>
    [Fact]
    public void Regenerate_WhenInvokedForNintendoDs_StripsPanelChromeAndUsesFlatButtons() {
        MenuDefinition definition = new TestMenuDefinitionProvider().CreateMenuDefinition();
        ScriptTypeResolver resolver = new ScriptTypeResolver();
        resolver.Register("gameplay", typeof(TestMenuDefinitionProvider).Assembly);
        EditorMenuSceneRegenerationService service = new EditorMenuSceneRegenerationService(ProjectRootPath, resolver);

        service.Regenerate("Scenes/DemoDiscMainMenuDs.helen", typeof(TestMenuDefinitionProvider).FullName + ", gameplay");

        string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", "DemoDiscMainMenuDs.helen");
        using FileStream stream = File.OpenRead(scenePath);
        SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));

        Assert.DoesNotContain(FlattenEntityNames(sceneAsset.RootEntities[1]), name => name == "panel-main-surface");
        Assert.DoesNotContain(FlattenEntityNames(sceneAsset.RootEntities[1]), name => name == "panel-main-top-band");

        SceneEntityAsset buttonEntity = FindFirstEntityWithComponentType(sceneAsset.RootEntities, "helengine.MenuItemComponent");
        SceneComponentAssetRecord roundedRectRecord = Assert.Single(buttonEntity.Components, component => component.ComponentTypeId == "helengine.RoundedRectComponent");
        RoundedRectComponent roundedRectComponent = ReadRoundedRectComponent(roundedRectRecord);
        SceneEntityAsset buttonViewportEntity = FindEntityByName(sceneAsset.RootEntities, "Panel-main-ItemsViewport");
        SceneEntityAsset buttonPanelEntity = FindEntityByName(sceneAsset.RootEntities, "Panel-main");

        Assert.Equal(0f, roundedRectComponent.Radius);
        Assert.Equal(0f, roundedRectComponent.BorderThickness);
        Assert.Equal(roundedRectComponent.FillColor, roundedRectComponent.BorderColor);
        Assert.Equal(definition.SurfaceBorderColor, roundedRectComponent.FillColor);
        Assert.Equal(DemoMenuNintendoDsLayout.ScreenWidth, roundedRectComponent.Size.X);
        Assert.Equal(DemoMenuNintendoDsLayout.ButtonHeight, roundedRectComponent.Size.Y);
        Assert.Equal(0f, buttonPanelEntity.LocalPosition.X);
        Assert.Equal(0f, buttonViewportEntity.LocalPosition.X);
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
    /// Reads the file-backed font reference assigned to one serialized debug component.
    /// </summary>
    /// <param name="record">Serialized debug component record to inspect.</param>
    /// <returns>Project-relative font path referenced by the debug component.</returns>
    static string ReadDebugFontRelativePath(SceneComponentAssetRecord record) {
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
                RelativePath = "fonts/body.hefont",
                ProviderId = string.Empty,
                AssetId = string.Empty
            },
            placeholderFont);
        EntitySaveComponent saveComponent = new EntitySaveComponent();
        DebugComponentPersistenceDescriptor descriptor = new DebugComponentPersistenceDescriptor();
        DebugComponent debugComponent = Assert.IsType<DebugComponent>(descriptor.DeserializeComponent(record, saveComponent, referenceResolver));
        Assert.Same(placeholderFont, debugComponent.Font);
        Assert.True(saveComponent.TryGetComponentState(debugComponent, out EntityComponentSaveState saveState));
        Assert.True(saveState.TryGetAssetReference(FontAssetScenePersistenceSupport.FontReferenceName, out SceneAssetReference fontReference));
        return fontReference.RelativePath;
    }

    /// <summary>
    /// Reads the serialized refresh interval assigned to one debug component record.
    /// </summary>
    /// <param name="record">Serialized debug component record to inspect.</param>
    /// <returns>Refresh interval seconds deserialized from the component payload.</returns>
    static double ReadDebugRefreshIntervalSeconds(SceneComponentAssetRecord record) {
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
                RelativePath = "fonts/body.hefont",
                ProviderId = string.Empty,
                AssetId = string.Empty
            },
            placeholderFont);
        EntitySaveComponent saveComponent = new EntitySaveComponent();
        DebugComponentPersistenceDescriptor descriptor = new DebugComponentPersistenceDescriptor();
        DebugComponent debugComponent = Assert.IsType<DebugComponent>(descriptor.DeserializeComponent(record, saveComponent, referenceResolver));
        return debugComponent.RefreshIntervalSeconds;
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

        if (TryFindEntityByName(entities, name, out SceneEntityAsset entity)) {
            return entity;
        }

        throw new InvalidOperationException("Expected scene entity '" + name + "' to exist.");
    }

    /// <summary>
    /// Attempts to find one scene entity by name across the supplied hierarchy.
    /// </summary>
    /// <param name="entities">Scene entities to search.</param>
    /// <param name="name">Entity name to resolve.</param>
    /// <param name="entity">Matching scene entity when found.</param>
    /// <returns>True when the requested entity exists.</returns>
    static bool TryFindEntityByName(SceneEntityAsset[] entities, string name, out SceneEntityAsset entity) {
        if (entities == null) {
            throw new ArgumentNullException(nameof(entities));
        }
        if (string.IsNullOrWhiteSpace(name)) {
            throw new ArgumentException("Entity name must be provided.", nameof(name));
        }

        entity = null;
        for (int index = 0; index < entities.Length; index++) {
            SceneEntityAsset currentEntity = entities[index];
            if (currentEntity == null) {
                continue;
            }

            if (string.Equals(currentEntity.Name, name, StringComparison.Ordinal)) {
                entity = currentEntity;
                return true;
            }

            if (TryFindEntityByName(currentEntity.Children ?? Array.Empty<SceneEntityAsset>(), name, out entity)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Finds the first scene entity that carries the requested serialized component type.
    /// </summary>
    /// <param name="entities">Scene entities to search.</param>
    /// <param name="componentTypeId">Serialized component type identifier to match.</param>
    /// <returns>First matching scene entity.</returns>
    static SceneEntityAsset FindFirstEntityWithComponentType(SceneEntityAsset[] entities, string componentTypeId) {
        if (entities == null) {
            throw new ArgumentNullException(nameof(entities));
        }
        if (string.IsNullOrWhiteSpace(componentTypeId)) {
            throw new ArgumentException("Component type id must be provided.", nameof(componentTypeId));
        }

        for (int index = 0; index < entities.Length; index++) {
            SceneEntityAsset entity = entities[index];
            if (entity == null) {
                continue;
            }

            SceneComponentAssetRecord[] components = entity.Components ?? Array.Empty<SceneComponentAssetRecord>();
            for (int componentIndex = 0; componentIndex < components.Length; componentIndex++) {
                if (string.Equals(components[componentIndex].ComponentTypeId, componentTypeId, StringComparison.Ordinal)) {
                    return entity;
                }
            }

            try {
                return FindFirstEntityWithComponentType(entity.Children ?? Array.Empty<SceneEntityAsset>(), componentTypeId);
            } catch (InvalidOperationException) {
            }
        }

        throw new InvalidOperationException("Expected scene entity with component '" + componentTypeId + "' to exist.");
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

    /// <summary>
    /// Deserializes one sprite component record from the generated scene payload.
    /// </summary>
    /// <param name="record">Serialized sprite component record.</param>
    /// <returns>Deserialized sprite component.</returns>
    static SpriteComponent ReadSpriteComponent(SceneComponentAssetRecord record, string texturePath) {
        if (record == null) {
            throw new ArgumentNullException(nameof(record));
        }
        if (string.IsNullOrWhiteSpace(texturePath)) {
            throw new ArgumentException("Texture path must be provided.", nameof(texturePath));
        }

        SpriteComponentPersistenceDescriptor descriptor = new SpriteComponentPersistenceDescriptor();
        EntitySaveComponent saveComponent = new EntitySaveComponent();
        TestSceneAssetReferenceResolver referenceResolver = new TestSceneAssetReferenceResolver();
        referenceResolver.RegisterTexture(
            new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = texturePath.Replace('\\', '/'),
                ProviderId = string.Empty,
                AssetId = string.Empty
            },
            new ManagedRuntimeTexture {
                Width = 1,
                Height = 1
            });
        return Assert.IsType<SpriteComponent>(descriptor.DeserializeComponent(record, saveComponent, referenceResolver));
    }

    /// <summary>
    /// Deserializes one text component record from the generated scene payload.
    /// </summary>
    /// <param name="record">Serialized text component record.</param>
    /// <param name="fontReference">Scene asset reference resolved by the generated text component.</param>
    /// <returns>Deserialized text component.</returns>
    static TextComponent ReadTextComponent(SceneComponentAssetRecord record, SceneAssetReference fontReference) {
        if (record == null) {
            throw new ArgumentNullException(nameof(record));
        }
        if (fontReference == null) {
            throw new ArgumentNullException(nameof(fontReference));
        }

        TextComponentPersistenceDescriptor descriptor = new TextComponentPersistenceDescriptor();
        EntitySaveComponent saveComponent = new EntitySaveComponent();
        TestSceneAssetReferenceResolver referenceResolver = new TestSceneAssetReferenceResolver();
        referenceResolver.RegisterFont(
            fontReference,
            new FontAsset(
                new FontInfo("Test", 16, 4f),
                new ManagedRuntimeTexture {
                    Width = 1,
                    Height = 1
                },
                new Dictionary<char, FontChar>(),
                16f,
                1,
                1));
        return Assert.IsType<TextComponent>(descriptor.DeserializeComponent(record, saveComponent, referenceResolver));
    }

    /// <summary>
    /// Reads the serialized font reference assigned to one generated text component.
    /// </summary>
    /// <param name="record">Serialized text component record to inspect.</param>
    /// <param name="fontReference">Scene asset reference resolved by the generated text component.</param>
    /// <returns>Stored scene asset reference assigned to the text component.</returns>
    static SceneAssetReference ReadTextFontReference(SceneComponentAssetRecord record, SceneAssetReference fontReference) {
        if (record == null) {
            throw new ArgumentNullException(nameof(record));
        }
        if (fontReference == null) {
            throw new ArgumentNullException(nameof(fontReference));
        }

        TextComponentPersistenceDescriptor descriptor = new TextComponentPersistenceDescriptor();
        EntitySaveComponent saveComponent = new EntitySaveComponent();
        TestSceneAssetReferenceResolver referenceResolver = new TestSceneAssetReferenceResolver();
        referenceResolver.RegisterFont(
            fontReference,
            new FontAsset(
                new FontInfo("Test", 16, 4f),
                new ManagedRuntimeTexture {
                    Width = 1,
                    Height = 1
                },
                new Dictionary<char, FontChar>(),
                16f,
                1,
                1));
        TextComponent textComponent = Assert.IsType<TextComponent>(descriptor.DeserializeComponent(record, saveComponent, referenceResolver));
        Assert.True(saveComponent.TryGetComponentState(textComponent, out EntityComponentSaveState saveState));
        Assert.True(saveState.TryGetAssetReference(FontAssetScenePersistenceSupport.FontReferenceName, out SceneAssetReference storedReference));
        return storedReference;
    }

    /// <summary>
    /// Deserializes one camera component record from the generated scene payload.
    /// </summary>
    /// <param name="record">Serialized camera component record.</param>
    /// <returns>Deserialized camera component.</returns>
    static CameraComponent ReadCameraComponent(SceneComponentAssetRecord record) {
        if (record == null) {
            throw new ArgumentNullException(nameof(record));
        }

        CameraComponentPersistenceDescriptor descriptor = new CameraComponentPersistenceDescriptor();
        return Assert.IsType<CameraComponent>(descriptor.DeserializeComponent(record, null, new TestSceneAssetReferenceResolver()));
    }

    /// <summary>
    /// Initializes a core instance so camera components can allocate runtime-owned queues during deserialization.
    /// </summary>
    static void InitializeCore() {
        Core core = new Core(new CoreInitializationOptions {
            RenderList3DInitialCapacity = 4,
            RenderList2DInitialCapacity = 4
        });
        core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
    }

    /// <summary>
    /// Creates one Nintendo DS menu definition with an overlay image so logo sizing can be asserted directly.
    /// </summary>
    /// <returns>Menu definition that includes an authored overlay image.</returns>
    static MenuDefinition CreateNintendoDsMenuDefinitionWithOverlayImage() {
        MenuDefinition baseDefinition = new TestMenuDefinitionProvider().CreateMenuDefinition();
        return new MenuDefinition(
            baseDefinition.Title,
            baseDefinition.Subtitle,
            baseDefinition.InitialPanelId,
            baseDefinition.TitleFontPath,
            baseDefinition.BodyFontPath,
            baseDefinition.BackgroundColor,
            baseDefinition.SurfaceColor,
            baseDefinition.SurfaceBorderColor,
            baseDefinition.AccentColor,
            baseDefinition.AccentSecondaryColor,
            baseDefinition.TextColor,
            baseDefinition.MutedTextColor,
            baseDefinition.Panels,
            new MenuOverlayImageDefinition("images/menu/logo.png", 220, 110, 0, 0),
            baseDefinition.PlatformInfoOverlay);
    }
}
