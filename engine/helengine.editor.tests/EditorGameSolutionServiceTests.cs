using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the game-solution generator writes the expected C# project structure for the editor scripting workflow.
    /// </summary>
    public sealed class EditorGameSolutionServiceTests : IDisposable {
        /// <summary>
        /// Temporary project root used by the current test instance.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Initializes one isolated temporary project root for solution-generation tests.
        /// </summary>
        public EditorGameSolutionServiceTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-game-solution-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets", "Scripts"));
            File.WriteAllText(Path.Combine(TempProjectRootPath, "assets", "Scripts", "Player.cs"), "public sealed class Player { }");
        }

        /// <summary>
        /// Deletes the temporary project root after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the generator writes both the solution and the project file with the assets glob enabled.
        /// </summary>
        [Fact]
        public void GenerateSolutionFiles_WhenInvoked_WritesSolutionAndProjectFiles() {
            EditorGameSolutionService service = new EditorGameSolutionService(TempProjectRootPath, "SkyRider", new TestIdeLauncher());

            string solutionPath = service.GenerateSolutionFiles();

            string projectFilePath = Path.Combine(TempProjectRootPath, "user_settings", "generated_code", "projects", "gameplay", "gameplay.csproj");
            Assert.Equal(Path.Combine(TempProjectRootPath, "SkyRider.sln"), solutionPath);
            Assert.True(File.Exists(projectFilePath));
            Assert.True(File.Exists(solutionPath));
            Assert.False(File.Exists(Path.Combine(TempProjectRootPath, "assets", "SkyRider.csproj")));

            string projectFileContents = File.ReadAllText(projectFilePath);
            string solutionFileContents = File.ReadAllText(solutionPath);

            Assert.Contains("<OutputType>Library</OutputType>", projectFileContents);
            Assert.Contains("<EnableDefaultCompileItems>false</EnableDefaultCompileItems>", projectFileContents);
            Assert.Contains("<EnableDefaultNoneItems>false</EnableDefaultNoneItems>", projectFileContents);
            Assert.Contains("<EnableDefaultContentItems>false</EnableDefaultContentItems>", projectFileContents);
            Assert.Contains("<EnableDefaultEmbeddedResourceItems>false</EnableDefaultEmbeddedResourceItems>", projectFileContents);
            Assert.Contains("<GenerateAssemblyInfo>false</GenerateAssemblyInfo>", projectFileContents);
            Assert.Contains("<GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>", projectFileContents);
            Assert.Contains("<ImplicitUsings>disable</ImplicitUsings>", projectFileContents);
            Assert.Contains("<AssemblyName>gameplay</AssemblyName>", projectFileContents);
            Assert.Contains("<RootNamespace>gameplay</RootNamespace>", projectFileContents);
            Assert.Contains("<BaseIntermediateOutputPath>" + EscapeXml(Path.Combine(TempProjectRootPath, "user_settings", "generated_code", "obj", "gameplay") + Path.DirectorySeparatorChar) + "</BaseIntermediateOutputPath>", projectFileContents);
            Assert.Contains("<MSBuildProjectExtensionsPath>" + EscapeXml(Path.Combine(TempProjectRootPath, "user_settings", "generated_code", "obj", "gameplay") + Path.DirectorySeparatorChar) + "</MSBuildProjectExtensionsPath>", projectFileContents);
            Assert.Contains("<BaseOutputPath>" + EscapeXml(Path.Combine(TempProjectRootPath, "user_settings", "generated_code", "bin", "gameplay") + Path.DirectorySeparatorChar) + "</BaseOutputPath>", projectFileContents);
            Assert.Contains("<Compile Include=\"" + EscapeXml(Path.Combine(TempProjectRootPath, "assets", "**", "*.cs")) + "\" />", projectFileContents);
            Assert.Contains("gameplay", solutionFileContents);
            Assert.Contains("user_settings/generated_code/projects/gameplay/gameplay.csproj", solutionFileContents);
            Assert.Equal(Path.Combine(TempProjectRootPath, "user_settings", "generated_code", "bin", "gameplay", "Debug", "net9.0"), service.GeneratedOutputDirectoryPath);
            Assert.Equal(Path.Combine(TempProjectRootPath, "user_settings", "generated_code", "bin", "gameplay", "Debug", "net9.0", "gameplay.dll"), service.GeneratedOutputAssemblyPath);
        }

        /// <summary>
        /// Ensures opening the generated solution delegates to the configured launcher after generating files.
        /// </summary>
        [Fact]
        public void OpenSolutionInIde_WhenInvoked_UsesTheConfiguredLauncher() {
            TestIdeLauncher launcher = new TestIdeLauncher();
            EditorGameSolutionService service = new EditorGameSolutionService(TempProjectRootPath, "SkyRider", launcher);

            service.OpenSolutionInIde();

            Assert.Equal(Path.Combine(TempProjectRootPath, "SkyRider.sln"), launcher.OpenedSolutionPath);
            Assert.True(File.Exists(launcher.OpenedSolutionPath));
        }

        /// <summary>
        /// Ensures the generator still writes files but skips reopening Visual Studio when the same solution is already active.
        /// </summary>
        [Fact]
        public void OpenSolutionInIde_WhenSolutionAlreadyOpen_SkipsLauncher() {
            TestIdeLauncher launcher = new TestIdeLauncher();
            TestSolutionDetector detector = new TestSolutionDetector(true);
            EditorGameSolutionService service = new EditorGameSolutionService(TempProjectRootPath, "SkyRider", launcher, detector);

            service.OpenSolutionInIde();

            Assert.Equal(1, detector.QueryCount);
            Assert.Equal(0, launcher.OpenCount);
            Assert.True(File.Exists(Path.Combine(TempProjectRootPath, "user_settings", "generated_code", "projects", "gameplay", "gameplay.csproj")));
            Assert.True(File.Exists(Path.Combine(TempProjectRootPath, "SkyRider.sln")));
        }

        /// <summary>
        /// Ensures regeneration removes legacy nested output folders from earlier project layouts.
        /// </summary>
        [Fact]
        public void GenerateSolutionFiles_WhenLegacyOutputFoldersExist_RemovesThem() {
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets", "obj"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets", "bin"));

            EditorGameSolutionService service = new EditorGameSolutionService(TempProjectRootPath, "SkyRider", new TestIdeLauncher());

            service.GenerateSolutionFiles();

            Assert.False(Directory.Exists(Path.Combine(TempProjectRootPath, "assets", "obj")));
            Assert.False(Directory.Exists(Path.Combine(TempProjectRootPath, "assets", "bin")));
            Assert.True(File.Exists(Path.Combine(TempProjectRootPath, "user_settings", "generated_code", "projects", "gameplay", "gameplay.csproj")));
        }

        /// <summary>
        /// Ensures module manifests produce one generated project per module outside authored assets.
        /// </summary>
        [Fact]
        public void GenerateSolutionFiles_WhenModulesExist_WritesOneProjectPerModuleOutsideAssets() {
            File.Delete(Path.Combine(TempProjectRootPath, "assets", "Scripts", "Player.cs"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets", "Scripts", "gameplay"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets", "Scripts", "gameplay", "ui"));
            File.WriteAllText(Path.Combine(TempProjectRootPath, "assets", "Scripts", "gameplay", "Player.cs"), "public sealed class Player { }");
            File.WriteAllText(Path.Combine(TempProjectRootPath, "assets", "Scripts", "gameplay", "code.module.json"), """
{
  "moduleId": "gameplay",
  "dependencyModuleIds": [],
  "loadScopes": [ "always-loaded" ]
}
""");
            File.WriteAllText(Path.Combine(TempProjectRootPath, "assets", "Scripts", "gameplay", "ui", "code.module.json"), """
{
  "moduleId": "gameplay.ui",
  "dependencyModuleIds": [ "gameplay" ],
  "loadScopes": [ "scene-loaded" ]
}
""");

            EditorGameSolutionService service = new EditorGameSolutionService(TempProjectRootPath, "SkyRider", new TestIdeLauncher());

            string solutionPath = service.GenerateSolutionFiles();

            Assert.True(File.Exists(Path.Combine(TempProjectRootPath, "user_settings", "generated_code", "projects", "gameplay", "gameplay.csproj")));
            Assert.True(File.Exists(Path.Combine(TempProjectRootPath, "user_settings", "generated_code", "projects", "gameplay.ui", "gameplay.ui.csproj")));
            Assert.Contains("user_settings/generated_code/projects/gameplay/gameplay.csproj", File.ReadAllText(solutionPath), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("user_settings/generated_code/projects/gameplay.ui/gameplay.ui.csproj", File.ReadAllText(solutionPath), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("assets/SkyRider.csproj", File.ReadAllText(solutionPath), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Escapes one text value for inclusion in XML text content so string assertions can match generated project values.
        /// </summary>
        /// <param name="value">Text value to escape.</param>
        /// <returns>XML-safe text value.</returns>
        static string EscapeXml(string value) {
            if (string.IsNullOrEmpty(value)) {
                return string.Empty;
            }

            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        /// <summary>
        /// Minimal launcher used to verify solution-open wiring without starting a real IDE.
        /// </summary>
        sealed class TestIdeLauncher : IEditorIdeLauncher {
            /// <summary>
            /// Gets the solution path passed to the fake launcher.
            /// </summary>
            public string OpenedSolutionPath { get; private set; }

            /// <summary>
            /// Gets the number of times the fake launcher was invoked.
            /// </summary>
            public int OpenCount { get; private set; }

            /// <summary>
            /// Records the solution path passed by the generator.
            /// </summary>
            /// <param name="solutionPath">Absolute path to the generated solution.</param>
            public void OpenSolution(string solutionPath) {
                OpenedSolutionPath = solutionPath;
                OpenCount++;
            }
        }

        /// <summary>
        /// Deterministic detector used to verify the no-reopen path.
        /// </summary>
        sealed class TestSolutionDetector : IEditorIdeSolutionDetector {
            /// <summary>
            /// Initializes one detector that returns a fixed response.
            /// </summary>
            /// <param name="isAlreadyOpen">Whether the detector should report the solution as already open.</param>
            public TestSolutionDetector(bool isAlreadyOpen) {
                IsAlreadyOpen = isAlreadyOpen;
            }

            /// <summary>
            /// Gets the fixed response returned by the detector.
            /// </summary>
            public bool IsAlreadyOpen { get; }

            /// <summary>
            /// Gets the number of queries received by the detector.
            /// </summary>
            public int QueryCount { get; private set; }

            /// <summary>
            /// Reports the fixed open state and tracks how often the generator checked it.
            /// </summary>
            /// <param name="solutionPath">Absolute path to the generated solution.</param>
            /// <returns>Configured open state.</returns>
            public bool IsSolutionAlreadyOpen(string solutionPath) {
                QueryCount++;
                return IsAlreadyOpen;
            }
        }
    }
}
