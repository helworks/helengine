namespace helengine.editor {
    /// <summary>
    /// Executes one discovered project-authored editor command using the active editor-safe command context.
    /// </summary>
    public sealed class EditorCommandExecutionService {
        /// <summary>
        /// Catalog provider used to resolve the currently available project-authored editor commands.
        /// </summary>
        readonly IEditorProjectCommandCatalogProvider CommandCatalogProvider;

        /// <summary>
        /// Context supplied to the concrete command instance when it executes.
        /// </summary>
        readonly IEditorCommandContext CommandContext;

        /// <summary>
        /// Initializes one editor command execution service.
        /// </summary>
        /// <param name="commandCatalogProvider">Catalog provider that surfaces the currently available editor commands.</param>
        /// <param name="commandContext">Editor-safe context passed to executed commands.</param>
        public EditorCommandExecutionService(
            IEditorProjectCommandCatalogProvider commandCatalogProvider,
            IEditorCommandContext commandContext) {
            CommandCatalogProvider = commandCatalogProvider ?? throw new ArgumentNullException(nameof(commandCatalogProvider));
            CommandContext = commandContext ?? throw new ArgumentNullException(nameof(commandContext));
        }

        /// <summary>
        /// Executes one discovered editor command by its stable command identifier.
        /// </summary>
        /// <param name="commandId">Stable command identifier that should be executed.</param>
        public void Execute(string commandId) {
            if (string.IsNullOrWhiteSpace(commandId)) {
                throw new ArgumentException("Command id must be provided.", nameof(commandId));
            }

            EditorProjectCommandDescriptor descriptor = ResolveCommand(commandId);
            IEditorCommand command = InstantiateCommand(descriptor);
            try {
                command.Execute(CommandContext);
            } catch (Exception exception) {
                throw new InvalidOperationException($"Editor command '{descriptor.CommandId}' failed.", exception);
            }
        }

        /// <summary>
        /// Resolves one command descriptor from the current catalog by stable command identifier.
        /// </summary>
        /// <param name="commandId">Stable command identifier to resolve.</param>
        /// <returns>Resolved editor command descriptor.</returns>
        EditorProjectCommandDescriptor ResolveCommand(string commandId) {
            IReadOnlyList<EditorProjectCommandDescriptor> commands = CommandCatalogProvider.GetAvailableEditorCommands();
            for (int index = 0; index < commands.Count; index++) {
                if (string.Equals(commands[index].CommandId, commandId, StringComparison.OrdinalIgnoreCase)) {
                    return commands[index];
                }
            }

            throw new InvalidOperationException($"Editor command '{commandId}' is not available.");
        }

        /// <summary>
        /// Instantiates the concrete editor command declared by one catalog descriptor.
        /// </summary>
        /// <param name="descriptor">Descriptor whose command type should be instantiated.</param>
        /// <returns>Instantiated editor command.</returns>
        IEditorCommand InstantiateCommand(EditorProjectCommandDescriptor descriptor) {
            if (descriptor == null) {
                throw new ArgumentNullException(nameof(descriptor));
            }

            try {
                object commandInstance = Activator.CreateInstance(descriptor.CommandType)
                    ?? throw new InvalidOperationException($"Editor command type '{descriptor.CommandType.FullName}' could not be instantiated.");
                if (commandInstance is not IEditorCommand command) {
                    throw new InvalidOperationException($"Editor command type '{descriptor.CommandType.FullName}' does not implement {nameof(IEditorCommand)}.");
                }

                return command;
            } catch (Exception exception) {
                throw new InvalidOperationException($"Editor command '{descriptor.CommandId}' could not be instantiated.", exception);
            }
        }
    }
}
