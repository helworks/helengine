using System.Reflection;
using helengine.directx11;
using helengine.editor.tests.testing;
using helengine.ui;
using helengine.vulkan;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the authored Demodisc Tilt Trial level resolves the expected scene materials through editor scene-loading services before any real DirectX11 draw occurs.
/// </summary>
public sealed class DemodiscTiltTrialSceneLoadTests {
    /// <summary>
    /// Absolute Demodisc project root used by the editor-side scene loader.
    /// </summary>
    const string DemodiscProjectRootPath = @"C:\dev\helprojs\demodisc";

    /// <summary>
    /// Absolute Tilt Trial scene path used by the editor-side scene loader.
    /// </summary>
    const string TiltTrialScenePath = @"C:\dev\helprojs\demodisc\assets\scenes\games\tilt_trial_level_01.helen";

    /// <summary>
    /// Absolute authored course texture source path expected on stage mesh materials.
    /// </summary>
    const string TiltTrialCourseTextureSourcePath = @"C:\dev\helprojs\demodisc\assets\textures\rendering\tilt_trial\CourseLilacGrid.bmp";

    /// <summary>
    /// Absolute authored player-sphere diffuse texture source path expected on the playable ball mesh material.
    /// </summary>
    const string TiltTrialPlayerSphereDiffuseTextureSourcePath = @"C:\dev\helprojs\demodisc\assets\textures\rendering\tilt_trial\PlayerSphereMarble.jpg";

    /// <summary>
    /// Stable project name declared by the Demodisc project file and used by the editor-generated script solution.
    /// </summary>
    const string DemodiscProjectName = "city";

    /// <summary>
    /// Stable stage entity name used by the authored first Tilt Trial level.
    /// </summary>
    const string StartPadEntityName = "StartPad";

    /// <summary>
    /// Stable player entity name used by the authored first Tilt Trial level.
    /// </summary>
    const string PlayerSphereEntityName = "PlayerSphere";

    /// <summary>
    /// Ensures editor scene loading resolves the expected authored diffuse textures for both the stage and the player sphere before any real GPU submission occurs.
    /// </summary>
    [Fact]
    public void Load_tilt_trial_scene_in_editor_services_resolves_expected_diffuse_textures() {
        EditorCore core = null;
        EditorGameScriptHotReloadService hotReloadService = null;
        ShaderModuleManager shaderModuleManager = null;
        GeneratedAssetProviderRegistry.ResetForTests();

        try {
            core = CreateCore();
            GeneratedAssetProviderRegistry.Register(new EngineGeneratedAssetProvider());
            ConfigureShaderBackends();
            EditorProjectPaths.Initialize(DemodiscProjectRootPath);
            shaderModuleManager = CreateShaderModuleManager();
            shaderModuleManager.Start();
            EditorShaderPackageService.Initialize(shaderModuleManager, ShaderCompileTarget.DirectX11, core.ContentManager);

            ContentManager projectContentManager = CreateProjectContentManager();
            hotReloadService = CreateHotReloadService();
            EditorBuildExecutionResult scriptBuildResult = hotReloadService.BuildAndReload();
            Assert.True(scriptBuildResult.Succeeded, scriptBuildResult.Message);
            AssetImportManager assetImportManager = CreateAssetImportManager(projectContentManager);
            ComponentPersistenceRegistry persistenceRegistry = new ComponentPersistenceRegistry(hotReloadService.ScriptTypeResolver);
            EditorFileSystemModelResolver modelResolver = new EditorFileSystemModelResolver(assetImportManager);
            EditorFileSystemFontResolver fontResolver = new EditorFileSystemFontResolver(assetImportManager);
            EditorFileSystemTextureResolver textureResolver = new EditorFileSystemTextureResolver(assetImportManager);
            EditorSceneAssetReferenceResolver sceneAssetReferenceResolver = new EditorSceneAssetReferenceResolver(
                projectContentManager,
                DemodiscProjectRootPath,
                modelResolver,
                fontResolver,
                textureResolver);
            SceneFileLoadService loadService = new SceneFileLoadService(
                DemodiscProjectRootPath,
                persistenceRegistry,
                sceneAssetReferenceResolver);

            LoadedEditorSceneDocument loaded = loadService.Load(TiltTrialScenePath);
            EditorEntity startPad = FindRequiredEntityByName(loaded.RootEntities, StartPadEntityName);
            EditorEntity playerSphere = FindRequiredEntityByName(loaded.RootEntities, PlayerSphereEntityName);
            TestRuntimeTexture resolvedCourseTexture = GetRequiredDiffuseTexture(startPad);
            TestRuntimeTexture resolvedPlayerTexture = GetRequiredDiffuseTexture(playerSphere);
            TextureAsset expectedCourseTexture = LoadExpectedTextureAsset(assetImportManager, TiltTrialCourseTextureSourcePath);
            TextureAsset expectedPlayerTexture = LoadExpectedTextureAsset(assetImportManager, TiltTrialPlayerSphereDiffuseTextureSourcePath);

            Assert.Equal(expectedCourseTexture.Width, resolvedCourseTexture.Width);
            Assert.Equal(expectedCourseTexture.Height, resolvedCourseTexture.Height);
            Assert.Equal(expectedPlayerTexture.Width, resolvedPlayerTexture.Width);
            Assert.Equal(expectedPlayerTexture.Height, resolvedPlayerTexture.Height);
        } finally {
            shaderModuleManager?.Dispose();
            hotReloadService?.Dispose();
            core?.Dispose();
            GeneratedAssetProviderRegistry.ResetForTests();
            EditorSelectionService.ClearSelection();
            EditorSceneMutationService.Reset();
        }
    }

    /// <summary>
    /// Creates the editor core used by the scene-load verification.
    /// </summary>
    /// <returns>Initialized editor core backed by the lightweight test renderers.</returns>
    static EditorCore CreateCore() {
        EditorCore core = new EditorCore(new Project {
            Name = "Demodisc Scene Load",
            Path = DemodiscProjectRootPath
        });
        core.Initialize(
            TestDirectX11RenderManager3D.Create(),
            new TestRenderManager2D(),
            null,
            new PlatformInfo("editor-test", "test-version"),
            new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(DemodiscProjectRootPath)
            });
        core.SetDefaultFontAssetForEditor(CreateDefaultFontAsset());
        return core;
    }

    /// <summary>
    /// Configures the shared built-in shader backend registry required by file-backed material resolution during scene load.
    /// </summary>
    static void ConfigureShaderBackends() {
        ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
        shaderBackendRegistry.Register(new DirectX11ShaderBackend());
        shaderBackendRegistry.Register(new VulkanShaderBackend());
        EditorBuiltInShaderAssetLibrary.ConfigureShaderBackends(shaderBackendRegistry);
    }

    /// <summary>
    /// Creates the project content manager used by the editor-side scene asset resolver.
    /// </summary>
    /// <returns>Configured project content manager rooted at the Demodisc assets folder.</returns>
    static ContentManager CreateProjectContentManager() {
        ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(Path.Combine(DemodiscProjectRootPath, "assets")));
        EditorContentManagerConfiguration.ConfigureEditorContentManager(contentManager);
        return contentManager;
    }

    /// <summary>
    /// Creates the asset import manager that mirrors the real editor's importer registrations for Windows-authored content.
    /// </summary>
    /// <param name="projectContentManager">Project content manager used by the import manager.</param>
    /// <returns>Configured asset import manager.</returns>
    static AssetImportManager CreateAssetImportManager(ContentManager projectContentManager) {
        if (projectContentManager == null) {
            throw new ArgumentNullException(nameof(projectContentManager));
        }

        AssetImportManager manager = new AssetImportManager(DemodiscProjectRootPath, projectContentManager);
        manager.CurrentPlatformId = "windows";

        IReadOnlyList<IAssetImporterRegistration> importers = LoadEditorHostImporters();
        for (int index = 0; index < importers.Count; index++) {
            importers[index].Register(manager);
        }

        return manager;
    }

    /// <summary>
    /// Creates the real editor script hot-reload service so scene loading uses the same generated module build and collectible assembly-load path as the desktop editor.
    /// </summary>
    /// <returns>Configured hot-reload service rooted at the Demodisc project.</returns>
    static EditorGameScriptHotReloadService CreateHotReloadService() {
        EditorGameSolutionService solutionService = new EditorGameSolutionService(
            DemodiscProjectRootPath,
            DemodiscProjectName,
            new TestEditorIdeLauncher());
        EditorGameScriptAssemblyHost assemblyHost = new EditorGameScriptAssemblyHost(DemodiscProjectRootPath);
        return new EditorGameScriptHotReloadService(
            solutionService,
            new EditorDotNetScriptBuildTool(),
            assemblyHost);
    }

    /// <summary>
    /// Creates the shader module manager required by editor-side file-backed material reconstruction for the real Demodisc project.
    /// </summary>
    /// <returns>Started shader module manager rooted to the Demodisc shader sources and cache output.</returns>
    static ShaderModuleManager CreateShaderModuleManager() {
        string shaderRootPath = Path.Combine(DemodiscProjectRootPath, "assets", "shaders");
        string packageOutputPath = Path.Combine(DemodiscProjectRootPath, "cache", "shader-cache");
        Directory.CreateDirectory(shaderRootPath);
        Directory.CreateDirectory(packageOutputPath);

        ShaderTargetBuildOptions targetOptions = new ShaderTargetBuildOptions(ShaderCompileTarget.DirectX11, new ShaderModel(4, 0));
        ShaderPackageBuildOptions buildOptions = new ShaderPackageBuildOptions(
            new[] { targetOptions },
            ShaderBindingPolicies.Default,
            true,
            false,
            false,
            Array.Empty<ShaderDefine>());
        ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
        shaderBackendRegistry.Register(new DirectX11ShaderBackend());
        EditorBuiltInShaderAssetLibrary.ConfigureShaderBackends(shaderBackendRegistry);
        ShaderModuleManagerOptions options = new ShaderModuleManagerOptions(
            shaderRootPath,
            packageOutputPath,
            buildOptions,
            ShaderCompileTarget.DirectX11,
            shaderBackendRegistry,
            100);
        return new ShaderModuleManager(options);
    }

    /// <summary>
    /// Creates the minimal default editor font asset required when authored scenes reference the generated editor font.
    /// </summary>
    /// <returns>Minimal runtime font asset backed by a 1x1 placeholder atlas.</returns>
    static FontAsset CreateDefaultFontAsset() {
        TextureAsset sourceTexture = new TextureAsset {
            Width = 1,
            Height = 1,
            Colors = new byte[] { 255, 255, 255, 255 }
        };

        FontAsset font = new FontAsset(
            new FontInfo("EditorTest", 16, 4f),
            new TestRuntimeTexture {
                Width = 1,
                Height = 1
            },
            new Dictionary<char, FontChar>(),
            16f,
            1,
            1) {
            SourceTextureAsset = sourceTexture
        };

        return font;
    }

    /// <summary>
    /// Loads the editor host's default importer registrations so the scene-load verification matches the real Windows editor path.
    /// </summary>
    /// <returns>Importer registrations used by the editor host.</returns>
    static IReadOnlyList<IAssetImporterRegistration> LoadEditorHostImporters() {
        string appAssemblyPath = @"C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\bin\Debug\net9.0-windows\helengine.editor.app.dll";
        Assembly appAssembly = Assembly.LoadFrom(appAssemblyPath);
        Type importerFactoryType = appAssembly.GetType("helengine.editor.app.EditorHostImporterFactory", throwOnError: true);
        MethodInfo createDefaultMethod = importerFactoryType.GetMethod(
            "CreateDefault",
            BindingFlags.Public | BindingFlags.Static);
        if (createDefaultMethod == null) {
            throw new InvalidOperationException("Editor host importer factory did not expose its default importer set.");
        }

        object result = createDefaultMethod.Invoke(null, null);
        return Assert.IsAssignableFrom<IReadOnlyList<IAssetImporterRegistration>>(result);
    }

    /// <summary>
    /// Loads the expected imported texture asset for one authored source texture path.
    /// </summary>
    /// <param name="assetImportManager">Configured import manager rooted to the Demodisc project.</param>
    /// <param name="sourceTexturePath">Absolute authored source texture path.</param>
    /// <returns>Imported texture asset that should back the editor scene material.</returns>
    static TextureAsset LoadExpectedTextureAsset(AssetImportManager assetImportManager, string sourceTexturePath) {
        if (assetImportManager == null) {
            throw new ArgumentNullException(nameof(assetImportManager));
        }
        if (string.IsNullOrWhiteSpace(sourceTexturePath)) {
            throw new ArgumentException("Source texture path must be provided.", nameof(sourceTexturePath));
        }
        if (!assetImportManager.TryLoadTextureAsset(sourceTexturePath, out TextureAsset textureAsset) || textureAsset == null) {
            throw new InvalidOperationException($"Expected texture asset '{sourceTexturePath}' could not be imported.");
        }

        return textureAsset;
    }

    /// <summary>
    /// Finds one required entity by name anywhere in the loaded scene hierarchy.
    /// </summary>
    /// <param name="roots">Root entities returned by the scene-load service.</param>
    /// <param name="entityName">Stable entity name to locate.</param>
    /// <returns>Matching editor entity.</returns>
    static EditorEntity FindRequiredEntityByName(IReadOnlyList<EditorEntity> roots, string entityName) {
        if (roots == null) {
            throw new ArgumentNullException(nameof(roots));
        }
        if (string.IsNullOrWhiteSpace(entityName)) {
            throw new ArgumentException("Entity name must be provided.", nameof(entityName));
        }

        Queue<Entity> pending = new Queue<Entity>();
        for (int index = 0; index < roots.Count; index++) {
            pending.Enqueue(roots[index]);
        }

        while (pending.Count > 0) {
            Entity entity = pending.Dequeue();
            if (entity is EditorEntity editorEntity &&
                string.Equals(editorEntity.Name, entityName, StringComparison.Ordinal)) {
                return editorEntity;
            }

            for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                pending.Enqueue(entity.Children[childIndex]);
            }
        }

        throw new InvalidOperationException($"Entity '{entityName}' was not found in the loaded scene hierarchy.");
    }

    /// <summary>
    /// Resolves the required diffuse runtime texture assigned to one entity's first mesh material.
    /// </summary>
    /// <param name="entity">Entity whose first mesh material should expose the authored diffuse texture.</param>
    /// <returns>Resolved diffuse runtime texture created by the test 2D renderer.</returns>
    static TestRuntimeTexture GetRequiredDiffuseTexture(Entity entity) {
        if (entity == null) {
            throw new ArgumentNullException(nameof(entity));
        }

        string entityDisplayName = GetEntityDisplayName(entity);

        MeshComponent meshComponent = entity.Components.OfType<MeshComponent>().FirstOrDefault();
        if (meshComponent == null) {
            throw new InvalidOperationException($"Entity '{entityDisplayName}' did not include a MeshComponent.");
        }
        if (meshComponent.Materials == null || meshComponent.Materials.Length == 0 || meshComponent.Materials[0] == null) {
            throw new InvalidOperationException($"Entity '{entityDisplayName}' did not include a first runtime material.");
        }

        ShaderRuntimeMaterial material = ShaderRuntimeMaterialAccess.Require(meshComponent.Materials[0]);
        int diffuseBindingIndex = material.Layout.FindTextureBindingIndex(StandardMaterialTextureBindingDefaults.DiffuseTextureBindingName);
        if (diffuseBindingIndex < 0) {
            throw new InvalidOperationException($"Entity '{entityDisplayName}' material did not expose the standard diffuse texture binding.");
        }

        return Assert.IsType<TestRuntimeTexture>(material.Properties.GetTexture(diffuseBindingIndex));
    }

    /// <summary>
    /// Builds a readable entity label for diagnostic messages without assuming every runtime entity exposes an editor name property.
    /// </summary>
    /// <param name="entity">Entity being described.</param>
    /// <returns>Editor entity name when available; otherwise the runtime entity type name.</returns>
    static string GetEntityDisplayName(Entity entity) {
        if (entity == null) {
            throw new ArgumentNullException(nameof(entity));
        }

        if (entity is EditorEntity editorEntity &&
            !string.IsNullOrWhiteSpace(editorEntity.Name)) {
            return editorEntity.Name;
        }

        return entity.GetType().Name;
    }
}
