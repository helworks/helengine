using System.Reflection;
using System.Runtime.CompilerServices;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the editor-session preference wiring for global UI scale changes.
    /// </summary>
    public class EditorSessionPreferencesTests : IDisposable {
        /// <summary>
        /// Stores the theme that was active before the test modified it.
        /// </summary>
        readonly ThemeManager.ThemePalette OriginalTheme;

        /// <summary>
        /// Temporary content root used by the editor-session preferences tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required by the preferences dialog shell used in these tests.
        /// </summary>
        public EditorSessionPreferencesTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-session-preferences-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);
            OriginalTheme = ThemeManager.Current;

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(TestDirectX11RenderManager3D.Create(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Deletes temporary test state after each test.
        /// </summary>
        public void Dispose() {
            EditorKeyboardFocusService.Reset();
            EditorInputCaptureService.Reset();
            ThemeManager.SetTheme(OriginalTheme);
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures confirming the preferences dialog raises the session-level UI scale settings event.
        /// </summary>
        [Fact]
        public void HandlePreferencesDialogConfirmed_WhenInvoked_RaisesUiScaleSettingsChanged() {
            EditorSession session = CreateSessionForPreferences();
            EditorUiScaleSettings raisedSettings = null;
            session.UiScaleSettingsChanged += settings => raisedSettings = settings;

            InvokePrivate(
                session,
                "HandlePreferencesDialogConfirmed",
                new EditorPreferencesSettings(
                    new EditorUiScaleSettings(EditorUiScaleMode.Override, 175),
                    "dark"));

            Assert.NotNull(raisedSettings);
            Assert.Equal(EditorUiScaleMode.Override, raisedSettings.Mode);
            Assert.Equal(175, raisedSettings.OverridePercent);
        }

        /// <summary>
        /// Ensures confirming the preferences dialog applies the selected editor theme only when Apply is invoked.
        /// </summary>
        [Fact]
        public void HandlePreferencesDialogConfirmed_WhenInvoked_AppliesThemeOnlyOnConfirm() {
            ThemeManager.SetTheme(ThemeManager.CreateNeon90s());
            EditorSession session = CreateSessionForPreferences();

            InvokePrivate(
                session,
                "HandlePreferencesDialogConfirmed",
                new EditorPreferencesSettings(
                    new EditorUiScaleSettings(EditorUiScaleMode.Auto, 100),
                    "light"));

            Assert.Equal("light", GetPrivateField<string>(session, "CurrentThemeId"));
            Assert.Equal(ThemeManager.CreateLightTheme().Colors.BackgroundPrimary, ThemeManager.Current.Colors.BackgroundPrimary);
        }

        /// <summary>
        /// Ensures canceling the preferences workflow leaves the current theme unchanged.
        /// </summary>
        [Fact]
        public void HandlePreferencesDialogCanceled_WhenInvoked_DoesNotApplyPendingThemeChanges() {
            ThemeManager.ThemePalette initialTheme = ThemeManager.CreateDarkTheme();
            ThemeManager.SetTheme(initialTheme);
            EditorSession session = CreateSessionForPreferences();

            InvokePrivate(session, "HandlePreferencesDialogCanceled");

            Assert.Equal(initialTheme.Colors.BackgroundPrimary, ThemeManager.Current.Colors.BackgroundPrimary);
        }

        /// <summary>
        /// Ensures applying one new UI scale updates the reusable title-bar, dialogs, viewport chrome, and properties panel offsets in place.
        /// </summary>
        [Fact]
        public void ApplyUiScale_WhenCalled_UpdatesScaledEditorChromeIncludingViewportAndPropertiesPanel() {
            EditorUiMetrics initialMetrics = new EditorUiMetrics(1d);
            EditorUiMetrics scaledMetrics = new EditorUiMetrics(1.5d);
            EditorSession session = CreateSessionForPreferences(initialMetrics);
            FontAsset scaledUiFont = CreateFont(18f);
            FontAsset scaledSnapModifierFont = CreateFont(23f);

            InvokePrivate(
                session,
                "ApplyUiScale",
                new EditorUiScaleSettings(EditorUiScaleMode.Override, 150),
                scaledMetrics,
                scaledUiFont,
                scaledSnapModifierFont);

            EditorTitleBar titleBar = GetPrivateField<EditorTitleBar>(session, "titleBar");
            EditorPreferencesDialog preferencesDialog = GetPrivateField<EditorPreferencesDialog>(session, "preferencesDialog");
            AssetBrowserPanel assetBrowserPanel = GetPrivateField<AssetBrowserPanel>(session, "assetBrowserPanel");
            EditorViewport mainViewport = GetPrivateField<EditorViewport>(session, "mainViewport");
            PropertiesPanel propertiesPanel = GetPrivateField<PropertiesPanel>(session, "propertiesPanel");
            AssetBrowserView browserView = GetPrivateField<AssetBrowserView>(assetBrowserPanel, "BrowserView");
            EditorEntity toolbarRoot = GetPrivateField<EditorEntity>(mainViewport, "ToolbarRoot");
            EditorEntity contentRoot = GetPrivateField<EditorEntity>(propertiesPanel, "contentRoot");
            EditorEntity assetContentRoot = GetPrivateField<EditorEntity>(assetBrowserPanel, "ContentRoot");
            SpriteComponent assetToolbarBackground = GetPrivateField<SpriteComponent>(browserView, "ToolbarBackground");
            TextComponent pathText = GetPrivateField<TextComponent>(browserView, "PathText");
            ButtonComponent upButton = GetPrivateField<ButtonComponent>(browserView, "UpButton");
            TextComponent[] snapLabelModifierTexts = GetPrivateField<TextComponent[]>(mainViewport, "SnapLabelModifierTexts");
            TextComponent[] snapValueTexts = GetPrivateField<TextComponent[]>(mainViewport, "SnapValueTexts");

            Assert.Equal(41, titleBar.Height);
            Assert.Equal(
                new int2(
                    scaledMetrics.ScalePixels(EditorPreferencesDialog.PanelWidth),
                    scaledMetrics.ScalePixels(EditorPreferencesDialog.PanelHeight)),
                GetDialogMinimumSize(preferencesDialog));
            Assert.Equal(scaledMetrics.DockTitleBarHeight, assetBrowserPanel.TitleBarHeightPixels);
            Assert.Equal(scaledMetrics.DockTitleBarHeight, mainViewport.TitleBarHeightPixels);
            Assert.Equal(scaledMetrics.DockTitleBarHeight, propertiesPanel.TitleBarHeightPixels);
            Assert.Equal(scaledMetrics.DockTitleBarHeight, assetContentRoot.LocalPosition.Y, 3);
            Assert.Equal(scaledMetrics.DockTitleBarHeight, toolbarRoot.LocalPosition.Y, 3);
            Assert.Equal(scaledMetrics.DockTitleBarHeight, contentRoot.LocalPosition.Y, 3);
            Assert.Equal(scaledMetrics.ScalePixels(28), assetToolbarBackground.Size.Y);
            Assert.Same(scaledUiFont, pathText.Font);
            Assert.Same(scaledUiFont, upButton.Font);
            Assert.Equal(new int2(scaledMetrics.ScalePixels(64), scaledMetrics.ScalePixels(22)), upButton.Size);
            Assert.Equal(42f + scaledMetrics.DockTitleBarHeight + 24f, mainViewport.Camera.Viewport.Y, 3);
            Assert.Same(scaledSnapModifierFont, snapLabelModifierTexts[0].Font);
            Assert.Same(scaledSnapModifierFont, snapLabelModifierTexts[1].Font);
            Assert.Same(scaledUiFont, snapValueTexts[0].Font);
            Assert.Same(scaledUiFont, snapValueTexts[1].Font);
        }

        /// <summary>
        /// Creates one partially initialized editor session containing only the collaborators used by the preferences flow.
        /// </summary>
        /// <returns>Editor session configured for isolated preferences tests.</returns>
        EditorSession CreateSessionForPreferences() {
            return CreateSessionForPreferences(EditorUiMetrics.Default);
        }

        /// <summary>
        /// Creates one partially initialized editor session containing only the collaborators used by the preferences flow.
        /// </summary>
        /// <param name="metrics">Scaled editor UI metrics used to construct the title bar and preferences dialog.</param>
        /// <returns>Editor session configured for isolated preferences tests.</returns>
        EditorSession CreateSessionForPreferences(EditorUiMetrics metrics) {
            EditorSession session = (EditorSession)RuntimeHelpers.GetUninitializedObject(typeof(EditorSession));
            SetPrivateField(session, "titleBar", new EditorTitleBar(CreateFont(), metrics, 1280, 720, "Hel"));
            SetPrivateField(session, "preferencesDialog", new EditorPreferencesDialog(CreateFont(), metrics));
            SetPrivateField(session, "assetBrowserPanel", CreateAssetBrowserPanel(metrics));
            SetPrivateField(session, "mainViewport", CreateViewport(metrics));
            SetPrivateField(session, "propertiesPanel", CreatePropertiesPanel(metrics));
            SetPrivateField(session, "CurrentUiScaleSettings", new EditorUiScaleSettings(EditorUiScaleMode.Auto, 100));
            SetPrivateField(session, "CurrentThemeId", EditorThemeCatalog.DefaultThemeId);
            SetPrivateField(session, "CurrentEditorPreferences", new EditorPreferencesSettings(new EditorUiScaleSettings(EditorUiScaleMode.Auto, 100), EditorThemeCatalog.DefaultThemeId));
            SetPrivateField(session, "CurrentUiMetrics", metrics);
            return session;
        }

        /// <summary>
        /// Creates one asset browser panel configured with the supplied dock metrics.
        /// </summary>
        /// <param name="metrics">Scaled dock metrics used by the asset browser panel.</param>
        /// <returns>Asset browser panel instance configured for preferences tests.</returns>
        AssetBrowserPanel CreateAssetBrowserPanel(EditorUiMetrics metrics) {
            Directory.CreateDirectory(Path.Combine(TempRootPath, "assets"));

            AssetBrowserPanel panel = new AssetBrowserPanel(CreateFont(), TempRootPath, metrics);
            panel.Size = new int2(500, 240);
            return panel;
        }

        /// <summary>
        /// Creates one viewport configured with deterministic toolbar assets and the supplied dock metrics.
        /// </summary>
        /// <param name="metrics">Scaled dock metrics used by the viewport.</param>
        /// <returns>Viewport instance configured for preferences tests.</returns>
        EditorViewport CreateViewport(EditorUiMetrics metrics) {
            EditorEntity cameraEntity = new EditorEntity();
            CameraComponent camera = new CameraComponent();
            cameraEntity.AddComponent(camera);

            EditorViewport viewport = new EditorViewport(
                camera,
                CreateFont(),
                CreateFont(),
                CreateToolbarIcons(),
                metrics);
            viewport.Position = new float3(18f, 42f, 0f);
            viewport.Size = new int2(640, 360);
            return viewport;
        }

        /// <summary>
        /// Creates one properties panel configured with the supplied dock metrics.
        /// </summary>
        /// <param name="metrics">Scaled dock metrics used by the properties panel.</param>
        /// <returns>Properties panel instance configured for preferences tests.</returns>
        PropertiesPanel CreatePropertiesPanel(EditorUiMetrics metrics) {
            return new PropertiesPanel(
                CreateFont(),
                new ContentManager(TempRootPath),
                null,
                new EditorEntity(),
                null,
                metrics);
        }

        /// <summary>
        /// Invokes one non-public instance method with a single argument.
        /// </summary>
        /// <param name="target">Target object that owns the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        /// <param name="argument">Argument passed to the invoked method.</param>
        void InvokePrivate(object target, string methodName, params object[] arguments) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            method.Invoke(target, arguments);
        }

        /// <summary>
        /// Assigns one non-public instance field to the provided value.
        /// </summary>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to assign.</param>
        /// <param name="value">Value assigned to the field.</param>
        void SetPrivateField(object target, string fieldName, object value) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }

        /// <summary>
        /// Reads one non-public instance field and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return Assert.IsType<T>(field.GetValue(target));
        }

        /// <summary>
        /// Reads the protected dialog minimum-size property from one dialog instance.
        /// </summary>
        /// <param name="dialog">Dialog whose minimum size should be inspected.</param>
        /// <returns>Current scaled dialog minimum size.</returns>
        int2 GetDialogMinimumSize(EditorDialogBase dialog) {
            PropertyInfo property = typeof(EditorDialogBase).GetProperty("DialogMinimumSize", BindingFlags.Instance | BindingFlags.NonPublic);
            return Assert.IsType<int2>(property.GetValue(dialog));
        }

        /// <summary>
        /// Creates deterministic toolbar icon textures for viewport tests.
        /// </summary>
        /// <returns>Toolbar icon set with stable texture sizes.</returns>
        EditorViewportToolbarIconSet CreateToolbarIcons() {
            return new EditorViewportToolbarIconSet(
                CreateIconTexture(),
                CreateIconTexture(),
                CreateIconTexture(),
                CreateIconTexture(),
                CreateIconTexture(),
                CreateIconTexture(),
                CreateIconTexture(),
                CreateIconTexture(),
                CreateIconTexture(),
                CreateIconTexture());
        }

        /// <summary>
        /// Creates one deterministic runtime texture used by viewport toolbar icons.
        /// </summary>
        /// <returns>Runtime texture with a stable size.</returns>
        RuntimeTexture CreateIconTexture() {
            return new TestRuntimeTexture {
                Width = 16,
                Height = 16
            };
        }

        /// <summary>
        /// Creates a small font asset that can satisfy preferences dialog layout requirements.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            return CreateFont(16f);
        }

        /// <summary>
        /// Creates a small font asset that can satisfy preferences dialog layout requirements at the supplied line height.
        /// </summary>
        /// <param name="lineHeight">Line height to expose on the created test font.</param>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont(float lineHeight) {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['A'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['C'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['D'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['F'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['G'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['H'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['L'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['M'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['N'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['O'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['R'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['T'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['U'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['V'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['f'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['h'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['k'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['v'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['x'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['y'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['z'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['+'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['-'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['.'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['%'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['0'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['1'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['2'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['3'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['4'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['7'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['5'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['6'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['8'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['9'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
            };

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                lineHeight,
                64,
                64);
        }
    }
}
