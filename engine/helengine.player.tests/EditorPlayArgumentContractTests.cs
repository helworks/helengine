using helengine.editor;

namespace helengine.player.tests {
    /// <summary>
    /// Ties the argument list the editor's Play session launches the player with to the player's own parser, so a
    /// renamed, missing or reordered option on either side fails here instead of at the first real Play.
    /// </summary>
    public sealed class EditorPlayArgumentContractTests {
        /// <summary>Platform identifier used for the Play request.</summary>
        const string PlatformId = "ps2";

        /// <summary>Saved scene path handed to the Play request.</summary>
        const string ScenePath = @"C:\project\assets\scenes\cube_test.hscene";

        /// <summary>Platform adapter assembly path passed through to the player.</summary>
        const string AdapterAssemblyPath = @"C:\platforms\ps2\helengine.ps2.builder.dll";

        /// <summary>Platform adapter type name passed through to the player.</summary>
        const string AdapterTypeName = "helengine.ps2.builder.Ps2PlayerAdapter";

        /// <summary>
        /// The arguments the editor launches the player with parse back into exactly the values the editor prepared,
        /// with and without the debugger wait.
        /// </summary>
        /// <param name="waitForDebugger">Whether the Play request asks the player to wait for a debugger.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void EditorPlayArguments_ParseBackIntoTheSameValues(bool waitForDebugger) {
            ContractPlayProcessLauncher launcher = new ContractPlayProcessLauncher();
            EditorPlaySessionService service = new EditorPlaySessionService(
                new ContractPlayCookRunner(),
                new ContractPlayScriptBuilder(),
                launcher,
                @"C:\editor\player\helengine.player.exe");

            EditorBuildExecutionResult result = service.Play(PlatformId, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, waitForDebugger);
            PlayerLaunchOptions options = PlayerLaunchOptions.Parse(launcher.LastRequest.Arguments.ToArray());

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal(ContractPlayCookRunner.ContentRootPath, options.ContentRootPath);
            Assert.Equal(ContractPlayCookRunner.StartupSceneId, options.SceneId);
            Assert.Equal(ContractPlayCookRunner.StartupSceneCookedRelativePath, options.SceneCookedRelativePath);
            Assert.Equal(AdapterAssemblyPath, options.AdapterAssemblyPath);
            Assert.Equal(AdapterTypeName, options.AdapterTypeName);
            Assert.Equal(ContractPlayScriptBuilder.RuntimeModulesRootPath, options.ScriptModulesRootPath);
            Assert.Equal(waitForDebugger, options.WaitForDebugger);
        }
    }
}
