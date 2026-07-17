using System.Reflection;
using helengine.directx11;
using helengine.editor.tests.testing;
using helengine.projectfile;
using helengine.ui;
using helengine.vulkan;
using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the real editor-session scene open and close path can load the authored Demodisc Tilt Trial scene and then dispose the session without throwing managed teardown exceptions.
/// </summary>
public sealed class DemodiscTiltTrialEditorSessionCloseTests : IDisposable {
    /// <summary>
    /// Absolute Demodisc project file path used by the real editor-session constructor.
    /// </summary>
    const string DemodiscProjectFilePath = @"C:\dev\helprojs\demodisc\project.heproj";

    /// <summary>
    /// Absolute Tilt Trial scene path used by the editor-session open-scene flow.
    /// </summary>
    const string TiltTrialScenePath = @"C:\dev\helprojs\demodisc\assets\scenes\games\tilt_trial_level_01.helen";

    /// <summary>
    /// Runtime core owned by the editor session under test.
    /// </summary>
    EditorCore CoreValue;
    /// <summary>
    /// Editor session under test.
    /// </summary>
    EditorSession SessionValue;

    /// <summary>
    /// Clears shared static editor state after each test run.
    /// </summary>
    public void Dispose() {
        SessionValue?.Dispose();
        SessionValue = null;
        CoreValue = null;
        GeneratedAssetProviderRegistry.ResetForTests();
        EditorSelectionService.Reset();
        EditorGizmoHoverService.ClearHoveredHandle();
        EditorInputCaptureService.Reset();
        EditorSceneMutationService.Reset();
        EditorKeyboardFocusService.Reset();
    }

    /// <summary>
    /// Ensures one real editor session can open the authored Tilt Trial scene and then dispose cleanly through the same close path used by the desktop editor.
    /// </summary>
    [Fact]
    public void Open_and_close_tilt_trial_scene_through_editor_session_does_not_throw() {
        SessionValue = CreateSession();

        InvokePrivate(SessionValue, "LoadSceneIntoSession", TiltTrialScenePath);

        SessionValue.Dispose();
        SessionValue = null;
    }

    /// <summary>
    /// Creates one real editor session backed by the same importer and shader-backend registrations used by the desktop editor.
    /// </summary>
    /// <returns>Initialized editor session ready to load authored scenes.</returns>
    EditorSession CreateSession() {
        GeneratedAssetProviderRegistry.ResetForTests();
        CoreValue = new EditorCore(new Project {
            Name = "Demodisc Session Close Test",
            Path = Path.GetDirectoryName(DemodiscProjectFilePath)
        });

        EditorViewportToolbarIconSet toolbarIcons = CreateToolbarIcons();
        RuntimeTexture titleBarIcon = CreateToolbarTexture();
        FontAsset font = CreateDefaultFontAsset();
        ShaderBackendRegistry shaderBackendRegistry = CreateShaderBackendRegistry();
        IReadOnlyList<IAssetImporterRegistration> importers = LoadEditorHostImporters();

        return new EditorSession(
            CoreValue,
            DemodiscProjectFilePath,
            new EditorPreferencesSettings(new EditorUiScaleSettings(EditorUiScaleMode.Override, 100), EditorThemeCatalog.DefaultThemeId),
            EditorUiMetrics.Default,
            font,
            font,
            TestDirectX11RenderManager3D.Create(),
            new TestRenderManager2D(),
            new TestInputBackend(),
            1280,
            720,
            toolbarIcons,
            titleBarIcon,
            importers,
            ResolveBrowseOutputFolder,
            shaderBackendRegistry);
    }

    /// <summary>
    /// Creates one shader backend registry aligned with the desktop editor host.
    /// </summary>
    /// <returns>Registry populated with supported desktop shader backends.</returns>
    static ShaderBackendRegistry CreateShaderBackendRegistry() {
        ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
        shaderBackendRegistry.Register(new DirectX11ShaderBackend());
        shaderBackendRegistry.Register(new VulkanShaderBackend());
        EditorBuiltInShaderAssetLibrary.ConfigureShaderBackends(shaderBackendRegistry);
        return shaderBackendRegistry;
    }

    /// <summary>
    /// Loads the editor host's default importer registrations so the session load path mirrors the desktop Windows editor.
    /// </summary>
    /// <returns>Importer registrations used by the editor host.</returns>
    static IReadOnlyList<IAssetImporterRegistration> LoadEditorHostImporters() {
        string appAssemblyPath = @"C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\bin\Debug\net9.0-windows\helengine.editor.app.dll";
        Assembly appAssembly = Assembly.LoadFrom(appAssemblyPath);
        Type importerFactoryType = appAssembly.GetType("helengine.editor.app.EditorHostImporterFactory", throwOnError: true);
        MethodInfo createDefaultMethod = importerFactoryType.GetMethod("CreateDefault", BindingFlags.Public | BindingFlags.Static);
        if (createDefaultMethod == null) {
            throw new InvalidOperationException("Editor host importer factory did not expose its default importer set.");
        }

        object result = createDefaultMethod.Invoke(null, null);
        return Assert.IsAssignableFrom<IReadOnlyList<IAssetImporterRegistration>>(result);
    }

    /// <summary>
    /// Resolves one deterministic output folder path for session services that require a browse callback.
    /// </summary>
    /// <returns>Demodisc project root path.</returns>
    static string ResolveBrowseOutputFolder() {
        return Path.GetDirectoryName(DemodiscProjectFilePath);
    }

    /// <summary>
    /// Creates one deterministic viewport toolbar icon set backed by reusable test runtime textures.
    /// </summary>
    /// <returns>Toolbar icon set used by the session constructor.</returns>
    static EditorViewportToolbarIconSet CreateToolbarIcons() {
        RuntimeTexture icon = CreateToolbarTexture();
        return new EditorViewportToolbarIconSet(
            icon,
            icon,
            icon,
            icon,
            icon,
            icon,
            icon,
            icon,
            icon,
            icon);
    }

    /// <summary>
    /// Creates one deterministic runtime texture used by toolbar and title-bar icon slots.
    /// </summary>
    /// <returns>Runtime texture placeholder.</returns>
    static RuntimeTexture CreateToolbarTexture() {
        return new TestRuntimeTexture {
            Width = 16,
            Height = 16
        };
    }

    /// <summary>
    /// Creates the minimal default editor font asset required when authored scenes reference generated editor font resources.
    /// </summary>
    /// <returns>Minimal runtime font asset backed by one placeholder atlas.</returns>
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
    /// Invokes one private instance method on the supplied target.
    /// </summary>
    /// <param name="target">Object whose private method should run.</param>
    /// <param name="methodName">Private method name.</param>
    /// <param name="argument">Single argument supplied to the method.</param>
    static void InvokePrivate(object target, string methodName, string argument) {
        if (target == null) {
            throw new ArgumentNullException(nameof(target));
        }
        if (string.IsNullOrWhiteSpace(methodName)) {
            throw new ArgumentException("Method name must be provided.", nameof(methodName));
        }

        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null) {
            throw new InvalidOperationException(string.Concat("Expected private method '", methodName, "' was not found."));
        }

        method.Invoke(target, new object[] { argument });
    }
}
