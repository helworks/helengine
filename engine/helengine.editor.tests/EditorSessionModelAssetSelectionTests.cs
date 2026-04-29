using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies selecting filesystem model assets does not leak editor UI entities into the scene hierarchy.
    /// </summary>
    public class EditorSessionModelAssetSelectionTests : IDisposable {
        /// <summary>
        /// Temporary project root used by the model-selection session tests.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Temporary assets root used by the import manager.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Initializes isolated editor services required by the model-selection tests.
        /// </summary>
        public EditorSessionModelAssetSelectionTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-model-selection-tests", Guid.NewGuid().ToString("N"));
            AssetsRootPath = Path.Combine(TempProjectRootPath, "assets");
            Directory.CreateDirectory(AssetsRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempProjectRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Deletes the temporary project directories after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures selecting one filesystem model asset does not add visible scene entities.
        /// </summary>
        [Fact]
        public void HandleAssetSelected_WhenFileSystemModelIsSelected_DoesNotAddVisibleSceneHierarchyNodes() {
            string sourcePath = WriteSourceModel("Sponza.obj");
            EditorSession session = CreateSession();
            SceneHierarchyPanel hierarchyPanel = GetPrivateField<SceneHierarchyPanel>(session, "sceneHierarchyPanel");
            AssetBrowserEntry entry = AssetBrowserEntry.CreateFileSystemFile(
                "Sponza.obj",
                "Models/Sponza.obj",
                sourcePath,
                ".obj",
                AssetEntryKind.Model);

            hierarchyPanel.RefreshHierarchy();
            int hierarchyCountBeforeSelection = GetHierarchyNodeCount(hierarchyPanel);

            InvokePrivate(session, "HandleAssetSelected", entry);

            hierarchyPanel.RefreshHierarchy();
            int hierarchyCountAfterSelection = GetHierarchyNodeCount(hierarchyPanel);

            Assert.Equal(hierarchyCountBeforeSelection, hierarchyCountAfterSelection);
        }

        /// <summary>
        /// Creates one partially initialized editor session containing the collaborators used by asset selection.
        /// </summary>
        /// <returns>Editor session configured for filesystem model asset selection tests.</returns>
        EditorSession CreateSession() {
            EditorSession session = (EditorSession)RuntimeHelpers.GetUninitializedObject(typeof(EditorSession));
            ContentManager contentManager = new ContentManager(AssetsRootPath);
            AssetImportManager manager = new AssetImportManager(TempProjectRootPath, contentManager);
            PropertiesPanel propertiesPanel = new PropertiesPanel(CreateFont(), contentManager);
            PreviewPanel previewPanel = new PreviewPanel(CreateFont());
            SceneHierarchyPanel sceneHierarchyPanel = new SceneHierarchyPanel(CreateFont());
            IReadOnlyList<string> supportedPlatforms = new List<string> { "windows" };
            EditorProjectLocalSettingsService localSettingsService = new EditorProjectLocalSettingsService(TempProjectRootPath, supportedPlatforms);

            manager.CurrentPlatformId = "windows";
            manager.RegisterModelImporter(new ModelImporterRegistration("test-model", new TestModelImporter(), new[] { ".obj" }));

            SetPrivateField(session, "assetImportManager", manager);
            SetPrivateField(session, "propertiesPanel", propertiesPanel);
            SetPrivateField(session, "previewPanel", previewPanel);
            SetPrivateField(session, "sceneHierarchyPanel", sceneHierarchyPanel);
            SetPrivateField(session, "ProjectSupportedPlatforms", supportedPlatforms);
            SetPrivateField(session, "ProjectLocalSettingsService", localSettingsService);
            SetPrivateField(session, "ActiveProjectPlatform", "windows");

            return session;
        }

        /// <summary>
        /// Reads the flattened hierarchy node count from one scene hierarchy panel.
        /// </summary>
        /// <param name="panel">Scene hierarchy panel to inspect.</param>
        /// <returns>Current number of visible hierarchy nodes.</returns>
        int GetHierarchyNodeCount(SceneHierarchyPanel panel) {
            FieldInfo nodesField = panel.GetType().GetField("nodes", BindingFlags.Instance | BindingFlags.NonPublic);
            ICollection nodes = Assert.IsAssignableFrom<ICollection>(nodesField.GetValue(panel));
            return nodes.Count;
        }

        /// <summary>
        /// Invokes one non-public instance method with the supplied arguments.
        /// </summary>
        /// <param name="target">Object that owns the method.</param>
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
        /// Writes one minimal model source file inside the temporary assets root.
        /// </summary>
        /// <param name="fileName">Source file name to create.</param>
        /// <returns>Absolute path to the created model source file.</returns>
        string WriteSourceModel(string fileName) {
            if (string.IsNullOrWhiteSpace(fileName)) {
                throw new ArgumentException("File name must be provided.", nameof(fileName));
            }

            string sourcePath = Path.Combine(AssetsRootPath, fileName);
            File.WriteAllText(sourcePath, "test model source");
            return sourcePath;
        }

        /// <summary>
        /// Creates a small font asset that can satisfy properties and hierarchy layout requirements.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['A'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['D'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['H'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['I'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['M'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['h'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['w'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['y'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['z'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f)
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
