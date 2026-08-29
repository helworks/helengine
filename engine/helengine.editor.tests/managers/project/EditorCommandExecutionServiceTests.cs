using helengine.editor.tests.testing;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies project-authored editor commands can be resolved and executed through the editor command runner.
/// </summary>
public sealed class EditorCommandExecutionServiceTests {
    /// <summary>
    /// Ensures executing one discovered command invokes its concrete command type.
    /// </summary>
    [Fact]
    public void Execute_WhenCommandExists_InvokesItsExecuteMethod() {
        TestInvokableEditorCommand.Reset();
        using TestEditorCommandContext context = new TestEditorCommandContext(Path.GetTempPath(), new ScriptTypeResolver());
        EditorCommandExecutionService service = new EditorCommandExecutionService(
            new TestCommandCatalogProvider([
                new EditorProjectCommandDescriptor(
                    "menu.invoke",
                    "Invoke Menu Command",
                    typeof(TestInvokableEditorCommand),
                    "menu.tools")
            ]),
            context);

        service.Execute("menu.invoke");

        Assert.True(TestInvokableEditorCommand.WasExecuted);
    }

    /// <summary>
    /// Ensures command failures are surfaced with the command identifier preserved in the exception message.
    /// </summary>
    [Fact]
    public void Execute_WhenCommandThrows_WrapsTheFailureWithCommandId() {
        string sentinelFilePath = Path.Combine(Path.GetTempPath(), "helengine-editor-command-sentinel-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(sentinelFilePath, "unchanged");
        using TestEditorCommandContext context = new TestEditorCommandContext(Path.GetTempPath(), new ScriptTypeResolver());
        EditorCommandExecutionService service = new EditorCommandExecutionService(
            new TestCommandCatalogProvider([
                new EditorProjectCommandDescriptor(
                    "menu.throw",
                    "Throwing Command",
                    typeof(ThrowingEditorCommand),
                    "menu.tools")
            ]),
            context);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => service.Execute("menu.throw"));

        Assert.Contains("menu.throw", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("unchanged", File.ReadAllText(sentinelFilePath));
        File.Delete(sentinelFilePath);
    }

    /// <summary>
    /// Minimal command catalog provider used to supply one deterministic command list to the runner.
    /// </summary>
    sealed class TestCommandCatalogProvider : IEditorProjectCommandCatalogProvider {
        /// <summary>
        /// Initializes one fake provider with a fixed command list.
        /// </summary>
        /// <param name="commands">Commands surfaced by the fake provider.</param>
        public TestCommandCatalogProvider(IReadOnlyList<EditorProjectCommandDescriptor> commands) {
            Commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>
        /// Gets the fixed command list surfaced by the fake provider.
        /// </summary>
        IReadOnlyList<EditorProjectCommandDescriptor> Commands { get; }

        /// <summary>
        /// Returns the fixed command list.
        /// </summary>
        /// <returns>Fixed command list.</returns>
        public IReadOnlyList<EditorProjectCommandDescriptor> GetAvailableEditorCommands() {
            return Commands;
        }
    }

    /// <summary>
    /// Minimal editor command context used to drive command-execution tests.
    /// </summary>
    sealed class TestEditorCommandContext : IEditorCommandContext, IDisposable {
        /// <summary>
        /// Session created when this test context owns its asset-authoring surface.
        /// </summary>
        readonly EditorProjectAuthoringSession OwnedAuthoringSession;
        readonly TestGeneratedAssetGraph OwnedGeneratedAssetGraph;
        /// <summary>
        /// Initializes one fake editor command context.
        /// </summary>
        /// <param name="projectRootPath">Absolute project root path surfaced by the context.</param>
        /// <param name="scriptTypeResolver">Resolver surfaced by the context.</param>
        public TestEditorCommandContext(
            string projectRootPath,
            IScriptTypeResolver scriptTypeResolver,
            IEditorProjectAssetAuthoringService assetAuthoring = null) {
            ProjectRootPath = projectRootPath ?? throw new ArgumentNullException(nameof(projectRootPath));
            ScriptTypeResolver = scriptTypeResolver ?? throw new ArgumentNullException(nameof(scriptTypeResolver));
            OwnedAuthoringSession = null;
            OwnedGeneratedAssetGraph = null;
            if (assetAuthoring == null) {
                Core core = new Core(new CoreInitializationOptions {
                    ContentStreamSource = new HostFileSystemContentStreamSource(Path.Combine(ProjectRootPath, "assets"))
                });
                core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
                OwnedGeneratedAssetGraph = new TestGeneratedAssetGraph(core);
                OwnedAuthoringSession = CreateAssetAuthoringCapability(ProjectRootPath, OwnedGeneratedAssetGraph);
                AssetAuthoring = OwnedAuthoringSession;
            } else {
                AssetAuthoring = assetAuthoring;
            }
            Authoring = new TestEditorCommandAuthoringSession();
        }

        /// <summary>
        /// Creates the lower-level capability directly for this context test double.
        /// </summary>
        /// <param name="projectRootPath">Project root used by the context.</param>
        /// <returns>Directly composed authoring capability.</returns>
        static EditorProjectAuthoringSession CreateAssetAuthoringCapability(string projectRootPath, TestGeneratedAssetGraph graph) {
            return Assert.IsType<EditorProjectAuthoringSession>(
                new EditorProjectAssetAuthoringServiceFactory(Array.Empty<IAssetImporterRegistration>()).CreateSession(
                    projectRootPath,
                    graph.Registry,
                    graph.ModelCache,
                    graph.MaterialCache,
                    graph.RendererResources));
        }

        /// <summary>
        /// Gets the absolute project root path surfaced by the context.
        /// </summary>
        public string ProjectRootPath { get; }

        /// <summary>
        /// Gets the resolver surfaced by the context.
        /// </summary>
        public IScriptTypeResolver ScriptTypeResolver { get; }

        /// <summary>
        /// Gets the asset-authoring capability surfaced by the fake context.
        /// </summary>
        public IEditorProjectAssetAuthoringService AssetAuthoring { get; }

        /// <summary>
        /// Gets the project authoring session surfaced by the fake context.
        /// </summary>
        public IEditorProjectAuthoringSession Authoring { get; }

        public Core Core => OwnedGeneratedAssetGraph.OwnerCore;

        public EditorSessionInteractionServices InteractionServices => OwnedGeneratedAssetGraph.InteractionServices;

        public GeneratedAssetProviderRegistry GeneratedAssetProviders => OwnedGeneratedAssetGraph.Registry;

        public EditorSessionRendererResources RendererResources => OwnedGeneratedAssetGraph.RendererResources;

        /// <summary>
        /// Releases the session owned by this test context, when it created one.
        /// </summary>
        public void Dispose() {
            OwnedAuthoringSession?.Dispose();
            OwnedGeneratedAssetGraph?.Dispose();
        }
    }

    /// <summary>
    /// Supplies only the current authoring-session contract to command execution tests.
    /// </summary>
    sealed class TestEditorCommandAuthoringSession : IEditorProjectAuthoringSession {
        /// <summary>
        /// Creates an empty authoring-session test double.
        /// </summary>
        public TestEditorCommandAuthoringSession() {
            RepairReport = new EditorAssetRepairReport();
        }

        public string ProjectRootPath => throw new NotSupportedException();

        public Core OwningCore => throw new NotSupportedException();

        public GeneratedAssetProviderRegistry GeneratedAssetProviders => throw new NotSupportedException();

        public EngineGeneratedModelCache GeneratedModelCache => throw new NotSupportedException();

        public EngineGeneratedMaterialCache GeneratedMaterialCache => throw new NotSupportedException();

        public EditorSessionRendererResources RendererResources => throw new NotSupportedException();

        /// <summary>
        /// Gets the empty repair report used by the test double.
        /// </summary>
        public EditorAssetRepairReport RepairReport { get; }

        /// <summary>
        /// Rejects unsupported reference creation in this command-execution test double.
        /// </summary>
        public SceneAssetReference CreateReference(string relativePath, AssetEntryKind expectedKind) {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Rejects unsupported reference resolution in this command-execution test double.
        /// </summary>
        public AssetReferenceResolution ResolveReference(SceneAssetReference reference, AssetEntryKind expectedKind) {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Rejects unsupported model loading in this command-execution test double.
        /// </summary>
        public RuntimeModel LoadImportedRuntimeModel(string relativePath) {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Rejects unsupported native writes in this command-execution test double.
        /// </summary>
        public EditorAssetWriteResult WriteAsset(string relativePath, Asset asset) {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Rejects unsupported transactions in this command-execution test double.
        /// </summary>
        public EditorAuthoringTransaction BeginTransaction() {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Performs no refresh in this command-execution test double.
        /// </summary>
        public void RefreshExternalChanges() {
        }

        /// <summary>
        /// Releases no resources in this command-execution test double.
        /// </summary>
        public void Dispose() {
        }
    }

    /// <summary>
    /// Minimal command used to verify that the runner invokes discovered command types.
    /// </summary>
    sealed class TestInvokableEditorCommand : IEditorCommand {
        /// <summary>
        /// Gets whether the command has been executed.
        /// </summary>
        public static bool WasExecuted { get; private set; }

        /// <summary>
        /// Resets the command execution marker.
        /// </summary>
        public static void Reset() {
            WasExecuted = false;
        }

        /// <summary>
        /// Gets the stable command identifier.
        /// </summary>
        public string CommandId => "menu.invoke";

        /// <summary>
        /// Gets the display name surfaced by the test provider.
        /// </summary>
        public string DisplayName => "Invoke Menu Command";

        /// <summary>
        /// Marks the command as executed.
        /// </summary>
        /// <param name="context">Editor command context supplied by the runner.</param>
        public void Execute(IEditorCommandContext context) {
            WasExecuted = true;
        }
    }

    /// <summary>
    /// Minimal command that always throws to verify command failure wrapping.
    /// </summary>
    sealed class ThrowingEditorCommand : IEditorCommand {
        /// <summary>
        /// Gets the stable command identifier.
        /// </summary>
        public string CommandId => "menu.throw";

        /// <summary>
        /// Gets the display name surfaced by the test provider.
        /// </summary>
        public string DisplayName => "Throwing Command";

        /// <summary>
        /// Throws a deterministic failure for the command runner test.
        /// </summary>
        /// <param name="context">Editor command context supplied by the runner.</param>
        public void Execute(IEditorCommandContext context) {
            throw new InvalidOperationException("boom");
        }
    }
}
