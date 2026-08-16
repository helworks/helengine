using helengine.tools.buildwaiter;

namespace helengine.tools.buildwaiter.tests {
    /// <summary>
    /// Verifies the public console entry-point returns process-compatible failure codes for invalid invocations.
    /// </summary>
    public sealed class ProgramTests {
        /// <summary>
        /// Ensures incomplete command-line arguments fail without launching a child process.
        /// </summary>
        [Fact]
        public async Task RunAsync_WhenArgumentsAreInvalid_ReturnsOne() {
            int exitCode = await Program.RunAsync(["--output", "C:\\output"]);

            Assert.Equal(1, exitCode);
        }

        /// <summary>
        /// Ensures the public entry point wires state and artifact verification for a complete successful invocation.
        /// </summary>
        [Fact]
        public async Task RunAsync_WhenChildWritesCurrentStateAndArtifact_ReturnsZero() {
            string outputRootPath = Path.Combine(Path.GetTempPath(), "build-waiter-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outputRootPath);
            try {
                string artifactPath = ConvertToPowerShellLiteral(Path.Combine(outputRootPath, "game.iso"));
                string outputRootLiteral = ConvertToPowerShellLiteral(outputRootPath);
                string sharedStatePath = ConvertToPowerShellLiteral(Path.Combine(outputRootPath, ".helengine-build-state.json"));
                string command = string.Join("; ", [
                    "$startedUtc = [DateTime]::UtcNow",
                    $"[System.IO.File]::WriteAllText({artifactPath}, 'iso')",
                    "$completedUtc = [DateTime]::UtcNow",
                    "$state = [ordered]@{ buildId = $env:HELENGINE_BUILD_INVOCATION_ID; projectPath = 'C:\\project\\project.heproj'; platform = 'ps2'; buildProfile = 'debug'; configuration = 'Debug'; startedUtc = $startedUtc.ToString('o'); completedUtc = $completedUtc.ToString('o'); status = 'succeeded'; exitCode = 0 }",
                    "$stateJson = $state | ConvertTo-Json",
                    $"$proofPath = Join-Path {outputRootLiteral} ('.helengine-build-state.' + $env:HELENGINE_BUILD_INVOCATION_ID + '.json')",
                    "$stateJson | Set-Content -LiteralPath $proofPath -Encoding UTF8",
                    $"$stateJson | Set-Content -LiteralPath {sharedStatePath} -Encoding UTF8"
                ]);

                int exitCode = await Program.RunAsync([
                    "--output", outputRootPath,
                    "--require", "game.iso",
                    "--",
                    "powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", command
                ]);

                Assert.Equal(0, exitCode);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Quotes one value as a single-quoted PowerShell literal.
        /// </summary>
        /// <param name="value">Raw value to quote.</param>
        /// <returns>PowerShell-safe single-quoted literal.</returns>
        static string ConvertToPowerShellLiteral(string value) {
            return "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
        }
    }
}
