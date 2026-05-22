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
        EditorCommandExecutionService service = new EditorCommandExecutionService(
            new TestCommandCatalogProvider([
                new EditorProjectCommandDescriptor(
                    "menu.invoke",
                    "Invoke Menu Command",
                    typeof(TestInvokableEditorCommand),
                    "menu.tools")
            ]),
            new TestEditorCommandContext(Path.GetTempPath(), new ScriptTypeResolver()));

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
        EditorCommandExecutionService service = new EditorCommandExecutionService(
            new TestCommandCatalogProvider([
                new EditorProjectCommandDescriptor(
                    "menu.throw",
                    "Throwing Command",
                    typeof(ThrowingEditorCommand),
                    "menu.tools")
            ]),
            new TestEditorCommandContext(Path.GetTempPath(), new ScriptTypeResolver()));

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
    sealed class TestEditorCommandContext : IEditorCommandContext {
        /// <summary>
        /// Initializes one fake editor command context.
        /// </summary>
        /// <param name="projectRootPath">Absolute project root path surfaced by the context.</param>
        /// <param name="scriptTypeResolver">Resolver surfaced by the context.</param>
        public TestEditorCommandContext(
            string projectRootPath,
            IScriptTypeResolver scriptTypeResolver) {
            ProjectRootPath = projectRootPath ?? throw new ArgumentNullException(nameof(projectRootPath));
            ScriptTypeResolver = scriptTypeResolver ?? throw new ArgumentNullException(nameof(scriptTypeResolver));
        }

        /// <summary>
        /// Gets the absolute project root path surfaced by the context.
        /// </summary>
        public string ProjectRootPath { get; }

        /// <summary>
        /// Gets the resolver surfaced by the context.
        /// </summary>
        public IScriptTypeResolver ScriptTypeResolver { get; }
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
