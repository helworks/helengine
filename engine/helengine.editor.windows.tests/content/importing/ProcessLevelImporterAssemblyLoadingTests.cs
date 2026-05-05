using System.Diagnostics;
using System.Text.Json;

namespace helengine.editor.windows.tests.content.importing {
    /// <summary>
    /// Verifies importer backend assemblies remain unloaded at process start and only load after the first matching import.
    /// </summary>
    public sealed class ProcessLevelImporterAssemblyLoadingTests {
        /// <summary>
        /// Ensures the isolated probe process observes clean startup state followed by on-demand backend assembly loading.
        /// </summary>
        [Fact]
        public void ImporterBackends_WhenObservedFromFreshProcess_LoadOnlyAfterFirstImport() {
            string probeAssemblyPath = Path.Combine(AppContext.BaseDirectory, "helengine.editor.windows.importerprobe.dll");
            Assert.True(File.Exists(probeAssemblyPath), $"Probe assembly was not found at '{probeAssemblyPath}'.");

            ProcessStartInfo startInfo = new ProcessStartInfo {
                FileName = "dotnet",
                Arguments = $"\"{probeAssemblyPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory
            };

            using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("The importer probe process could not be started.");
            string standardOutput = process.StandardOutput.ReadToEnd();
            string standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.True(process.ExitCode == 0, $"Importer probe exited with code {process.ExitCode}.{Environment.NewLine}STDOUT:{Environment.NewLine}{standardOutput}{Environment.NewLine}STDERR:{Environment.NewLine}{standardError}");

            ProcessLevelImporterAssemblyLoadResult result = JsonSerializer.Deserialize<ProcessLevelImporterAssemblyLoadResult>(standardOutput) ?? throw new InvalidOperationException("The importer probe returned invalid JSON.");
            Assert.False(result.GdiLoadedBeforeRegistration);
            Assert.False(result.GdiLoadedAfterRegistration);
            Assert.True(result.GdiLoadedAfterImport);
            Assert.False(result.PfimLoadedBeforeRegistration);
            Assert.False(result.PfimLoadedAfterRegistration);
            Assert.True(result.PfimLoadedAfterImport);
            Assert.False(result.MagickLoadedBeforeRegistration);
            Assert.False(result.MagickLoadedAfterRegistration);
            Assert.True(result.MagickLoadedAfterImport);
            Assert.False(result.AssimpLoadedBeforeRegistration);
            Assert.False(result.AssimpLoadedAfterRegistration);
            Assert.True(result.AssimpLoadedAfterImport);
        }
    }
}
