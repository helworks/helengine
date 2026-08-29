namespace helengine.editor.tests.testing {
    /// <summary>
    /// Minimal project-authored editor command used to verify command discovery from loaded editor assemblies.
    /// </summary>
    internal sealed class TestEditorCommand : IEditorCommand {
        /// <summary>
        /// Gets the number of times the test command has been executed in the current process.
        /// </summary>
        public static int ExecuteCount { get; private set; }

        /// <summary>
        /// Gets or sets whether the next command execution should fail for host error-routing tests.
        /// </summary>
        public static bool ThrowOnExecute { get; set; }

        /// <summary>
        /// Gets the stable test command identifier.
        /// </summary>
        public string CommandId => "menu.regenerate-demo-disc-main-menu";

        /// <summary>
        /// Gets the display label surfaced by the editor command catalog.
        /// </summary>
        public string DisplayName => "Regenerate Demo Disc Main Menu";

        /// <summary>
        /// Executes the test command. The discovery test does not require any behavior here.
        /// </summary>
        /// <param name="context">Editor command context supplied by the command runner.</param>
        public void Execute(IEditorCommandContext context) {
            ExecuteCount++;
            if (ThrowOnExecute) {
                throw new InvalidOperationException("Injected editor command failure.");
            }
        }

        /// <summary>
        /// Resets the process-wide execution count tracked by the test command.
        /// </summary>
        public static void Reset() {
            ExecuteCount = 0;
            ThrowOnExecute = false;
        }
    }

    /// <summary>
    /// Real command implementation used by CLI graph-isolation tests. It
    /// materializes generated preview resources and an editor entity through
    /// only the explicit command graph supplied by the runner.
    /// </summary>
    internal sealed class TestGraphMutationEditorCommand : IEditorCommand {
        public static Core LastCore { get; private set; }
        public static EditorSessionInteractionServices LastInteractionServices { get; private set; }
        public static RuntimeModel LastPreviewModel { get; private set; }
        public static EditorEntity LastEntity { get; private set; }

        public string CommandId => "test.graph.mutate";
        public string DisplayName => "Mutate Explicit Graph";

        public void Execute(IEditorCommandContext context) {
            LastCore = context.Core;
            LastInteractionServices = context.InteractionServices;
            LastPreviewModel = context.RendererResources.WorldSpace2DPreviewMeshes.GetRuntimeModel();
            AssetBrowserEntry cubeEntry = AssetBrowserEntry.CreateGeneratedAsset(
                "Cube",
                EngineGeneratedAssetProvider.CubeRelativePath,
                AssetEntryKind.Model,
                EngineGeneratedAssetProvider.ProviderIdValue,
                EngineGeneratedModelCache.CubeAssetId);
            context.GeneratedAssetProviders.ResolveRuntimeModel(cubeEntry);
            LastEntity = new EditorEntity(context.Core, context.InteractionServices) {
                Name = "CLI graph entity"
            };
            context.InteractionServices.Selection.SetSelectedEntity(LastEntity);
            context.InteractionServices.InputCapture.SetBlocker(LastEntity, new int2(3, 4), new int2(10, 10));
        }

        public static void Reset() {
            LastEntity?.Dispose();
            LastEntity = null;
            LastCore = null;
            LastInteractionServices = null;
            LastPreviewModel = null;
        }
    }
}
