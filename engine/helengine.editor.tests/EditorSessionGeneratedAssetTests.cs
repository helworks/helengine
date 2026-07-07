using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies generated assets selected from the asset browser bypass import workflows.
    /// </summary>
    public class EditorSessionGeneratedAssetTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the properties panel content manager.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required by the properties and preview panels.
        /// </summary>
        public EditorSessionGeneratedAssetTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-generated-session-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempRootPath)
            });
            core.Initialize(null, new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Deletes temporary test content after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures generated asset selections show a read-only summary instead of import settings.
        /// </summary>
        [Fact]
        public void HandleAssetSelected_WhenEntryIsGenerated_ShowsReadOnlySummaryInsteadOfImportSettings() {
            EditorSession session = CreateSessionForGeneratedSelection();
            AssetBrowserEntry generatedEntry = AssetBrowserEntry.CreateGeneratedAsset("Cube", "Engine/Models/Cube", AssetEntryKind.Model, "engine", EngineGeneratedModelCache.CubeAssetId);

            InvokePrivate(session, "HandleAssetSelected", generatedEntry);

            PropertiesPanel panel = GetPrivateField<PropertiesPanel>(session, "propertiesPanel");
            TextComponent header = GetPrivateField<TextComponent>(panel, "headerText");
            TextComponent status = GetPrivateField<TextComponent>(panel, "statusText");

            Assert.Equal("Properties", header.Text);
            Assert.Equal("Source: Generated", status.Text);
        }

        /// <summary>
        /// Ensures scene assets show a read-only scene summary instead of import settings.
        /// </summary>
        [Fact]
        public void HandleAssetSelected_WhenEntryIsScene_ShowsSceneSummaryInsteadOfImportSettings() {
            EditorSession session = CreateSessionForGeneratedSelection();
            string scenePath = Path.Combine(TempRootPath, "Sample.helen");
            using (FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetSerializer.Serialize(stream, new SceneAsset {
                    Id = "Scenes/Sample.helen",
                    RootEntities = Array.Empty<SceneEntityAsset>()
                });
            }

            AssetBrowserEntry sceneEntry = AssetBrowserEntry.CreateFileSystemFile(
                "Sample.helen",
                "Scenes/Sample.helen",
                scenePath,
                SceneAsset.FileExtension,
                AssetEntryKind.Scene);

            InvokePrivate(session, "HandleAssetSelected", sceneEntry);

            PropertiesPanel panel = GetPrivateField<PropertiesPanel>(session, "propertiesPanel");
            TextComponent header = GetPrivateField<TextComponent>(panel, "headerText");
            TextComponent status = GetPrivateField<TextComponent>(panel, "statusText");

            Assert.Equal("Properties", header.Text);
            Assert.Equal("Kind: Scene", status.Text);
        }

        /// <summary>
        /// Creates a partially initialized editor session containing only the collaborators used by HandleAssetSelected.
        /// </summary>
        /// <returns>Editor session instance configured for generated-asset selection tests.</returns>
        EditorSession CreateSessionForGeneratedSelection() {
            EditorSession session = (EditorSession)RuntimeHelpers.GetUninitializedObject(typeof(EditorSession));
            PropertiesPanel propertiesPanel = new PropertiesPanel(CreateFont(), new ContentManager(new HostFileSystemContentStreamSource(TempRootPath)));
            PreviewPanel previewPanel = new PreviewPanel(CreateFont());

            SetPrivateField(session, "propertiesPanel", propertiesPanel);
            SetPrivateField(session, "previewPanel", previewPanel);

            return session;
        }

        /// <summary>
        /// Invokes one non-public instance method.
        /// </summary>
        /// <param name="target">Target object that owns the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        /// <param name="arguments">Arguments passed to the method.</param>
        void InvokePrivate(object target, string methodName, params object[] arguments) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(target, arguments);
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
        /// Assigns one non-public instance field.
        /// </summary>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to assign.</param>
        /// <param name="value">Value assigned to the field.</param>
        void SetPrivateField(object target, string fieldName, object value) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }

        /// <summary>
        /// Creates a small font asset that can satisfy properties-panel layout.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                [':'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['G'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
            };

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                16f,
                64,
                64);
        }
    }
}

