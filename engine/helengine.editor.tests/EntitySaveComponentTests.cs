using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the hidden editor save component attached to user-authored editor entities.
    /// </summary>
    public class EntitySaveComponentTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the properties view content manager.
        /// </summary>
        readonly string TempRootPath;
        readonly Core CoreValue;
        readonly TestGeneratedAssetGraph GeneratedAssetGraph;

        /// <summary>
        /// Initializes the core services required by the component properties view.
        /// </summary>
        public EntitySaveComponentTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-entity-save-component-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            CoreValue = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempRootPath)
            });
            CoreValue.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
            GeneratedAssetGraph = new TestGeneratedAssetGraph(CoreValue);
        }

        /// <summary>
        /// Deletes temporary test content after each test.
        /// </summary>
        public void Dispose() {
            GeneratedAssetGraph.Dispose();
            CoreValue.Dispose();
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures editor entities receive one hidden save component automatically.
        /// </summary>
        [Fact]
        public void EditorEntity_WhenConstructed_AttachesEntitySaveComponent() {
            EditorEntity entity = new EditorEntity(CoreValue, GeneratedAssetGraph.InteractionServices);

            Assert.Contains(entity.Components, component => component is EntitySaveComponent);
        }

        /// <summary>
        /// Ensures hidden editor-only components do not surface in the properties panel UI.
        /// </summary>
        [Fact]
        public void ShowComponents_WhenEntityContainsHiddenSaveComponent_DoesNotShowItInThePropertiesView() {
            EditorEntity entity = new EditorEntity(CoreValue, GeneratedAssetGraph.InteractionServices);
            entity.AddComponent(new MeshComponent());
            ComponentPropertiesView view = new ComponentPropertiesView(CoreValue, GeneratedAssetGraph.InteractionServices, CreateFont(), new ContentManager(new HostFileSystemContentStreamSource(TempRootPath)));
            view.SetRendererResources(GeneratedAssetGraph.RendererResources);

            view.ShowComponents(entity);

            List<ComponentPropertyRow> rows = GetActiveRows(view);
            Assert.DoesNotContain(rows, row => string.Equals(row.Label.Text, nameof(EntitySaveComponent), StringComparison.Ordinal));
        }

        /// <summary>
        /// Ensures per-component save-state can store and retrieve one platform override entry.
        /// </summary>
        [Fact]
        public void EntityComponentSaveState_WhenPlatformOverrideIsStored_CanReadItBack() {
            EntitySaveComponent saveComponent = new EntitySaveComponent();
            MeshComponent component = new MeshComponent();
            EntityComponentSaveState saveState = saveComponent.GetOrCreateComponentState(component);
            Type overrideStateType = ResolveRequiredType("helengine.EntityComponentPlatformOverrideState");
            object overrideState = Activator.CreateInstance(overrideStateType);

            overrideStateType.GetProperty("PlatformId").SetValue(overrideState, "windows");
            overrideStateType.GetProperty("Payload").SetValue(overrideState, new byte[] { 1, 2, 3, 4 });

            MethodInfo setMethod = typeof(EntityComponentSaveState).GetMethod("SetPlatformOverride", BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(setMethod);
            setMethod.Invoke(saveState, new[] { "windows", overrideState });

            MethodInfo tryGetMethod = typeof(EntityComponentSaveState).GetMethod("TryGetPlatformOverride", BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(tryGetMethod);

            object[] arguments = new object[] { "windows", null };
            bool found = Assert.IsType<bool>(tryGetMethod.Invoke(saveState, arguments));

            Assert.True(found);
            Assert.NotNull(arguments[1]);
            Assert.Equal("windows", Assert.IsType<string>(overrideStateType.GetProperty("PlatformId").GetValue(arguments[1])));
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, Assert.IsType<byte[]>(overrideStateType.GetProperty("Payload").GetValue(arguments[1])));
        }

        /// <summary>
        /// Ensures environment-scoped entity metadata is nested below its owning platform and does not leak into another scope.
        /// </summary>
        [Fact]
        public void EntitySaveComponent_WhenEnvironmentOverrideIsStored_ResolvesOnlyWithinOwningPlatform() {
            EntitySaveComponent saveComponent = new EntitySaveComponent();
            EditorOverrideScope windows = new EditorOverrideScope("windows");
            EditorOverrideScope windowsDebug = new EditorOverrideScope("windows", "debug");
            EditorOverrideScope linuxDebug = new EditorOverrideScope("linux", "debug");

            saveComponent.SetExistencePlatformOverride(windows, new SceneEntityPlatformExistenceOverrideAsset {
                Exists = true
            });
            saveComponent.SetExistencePlatformOverride(windowsDebug, new SceneEntityPlatformExistenceOverrideAsset {
                Exists = false
            });

            Assert.True(saveComponent.TryGetExistencePlatformOverride(windowsDebug, out SceneEntityPlatformExistenceOverrideAsset windowsDebugOverride));
            Assert.False(windowsDebugOverride.Exists);
            Assert.False(saveComponent.TryGetExistencePlatformOverride(linuxDebug, out _));
            Assert.True(saveComponent.TryGetExistencePlatformOverride("windows", out SceneEntityPlatformExistenceOverrideAsset platformOverride));
            Assert.True(platformOverride.Exists);
            Assert.Equal(2, saveComponent.EnumerateExistencePlatformOverrides().Count());
        }

        /// <summary>
        /// Ensures component property overrides can be stored at platform and nested environment scopes independently.
        /// </summary>
        [Fact]
        public void EntityComponentSaveState_WhenEnvironmentOverrideIsStored_PreservesPlatformOverride() {
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            EditorOverrideScope windows = new EditorOverrideScope("windows");
            EditorOverrideScope windowsDebug = new EditorOverrideScope("windows", "debug");
            EntityComponentPlatformOverrideState platformOverride = new EntityComponentPlatformOverrideState {
                Payload = new byte[] { 1 }
            };
            EntityComponentPlatformOverrideState environmentOverride = new EntityComponentPlatformOverrideState {
                Payload = new byte[] { 2 }
            };

            saveState.SetScopedPlatformOverride(windows, platformOverride);
            saveState.SetScopedPlatformOverride(windowsDebug, environmentOverride);

            Assert.True(saveState.TryGetScopedPlatformOverride(windows, out EntityComponentPlatformOverrideState resolvedPlatformOverride));
            Assert.Equal(new byte[] { 1 }, resolvedPlatformOverride.Payload);
            Assert.True(saveState.TryGetScopedPlatformOverride(windowsDebug, out EntityComponentPlatformOverrideState resolvedEnvironmentOverride));
            Assert.Equal(new byte[] { 2 }, resolvedEnvironmentOverride.Payload);
            Assert.Equal(2, saveState.EnumeratePlatformOverrides().Count());
        }

        /// <summary>
        /// Ensures malformed nested scopes are rejected at construction time.
        /// </summary>
        [Fact]
        public void EditorOverrideScope_WhenPlatformIsMissing_Throws() {
            Assert.Throws<ArgumentException>(() => new EditorOverrideScope(string.Empty, "debug"));
        }

        /// <summary>
        /// Reads the active component rows from the properties view.
        /// </summary>
        /// <param name="view">View whose active rows should be inspected.</param>
        /// <returns>Active component property rows.</returns>
        List<ComponentPropertyRow> GetActiveRows(ComponentPropertiesView view) {
            FieldInfo field = typeof(ComponentPropertiesView).GetField("ActiveRows", BindingFlags.Instance | BindingFlags.NonPublic);
            return Assert.IsType<List<ComponentPropertyRow>>(field.GetValue(view));
        }

        /// <summary>
        /// Resolves one required runtime type from the editor assembly.
        /// </summary>
        /// <param name="typeName">Assembly-qualified full name to resolve.</param>
        /// <returns>Resolved runtime type.</returns>
        Type ResolveRequiredType(string typeName) {
            Type type = typeof(EntityComponentSaveState).Assembly.GetType(typeName, false);
            Assert.NotNull(type);
            return type;
        }

        /// <summary>
        /// Creates a small font asset that can satisfy the layout requirements of property rows.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['E'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['y'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['v'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['C'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['M'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['h'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
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
