using System.Text;
using System.Text.Json;
using helengine.tools.buildwaiter;

namespace helengine.tools.buildwaiter.tests {
    /// <summary>
    /// Verifies the active-child output verification and acknowledgment protocol.
    /// </summary>
    public sealed class BuildVerificationHandshakeTests {
        const string InvocationId = "b40ab19d-4d81-4db0-a0d4-9b818b49c7c0";

        [Fact]
        public async Task VerifyAndAcknowledgeAsync_WhenProofAndArtifactAreValid_WritesExactAcknowledgement() {
            string outputRootPath = CreateOutputRoot();
            try {
                DateTime waiterStartedUtc = DateTime.UtcNow.AddSeconds(-1);
                WriteState(outputRootPath, InvocationId, waiterStartedUtc);
                File.WriteAllText(Path.Combine(outputRootPath, "game.iso"), "iso");
                TaskCompletionSource childExit = new(TaskCreationOptions.RunContinuationsAsynchronously);

                Task<BuildVerificationHandshakeResult> verification = CreateHandshake().VerifyAndAcknowledgeAsync(
                    outputRootPath, ["game.iso"], waiterStartedUtc, InvocationId, childExit.Task);

                string acknowledgementPath = BuildInvocationProofPaths.GetAcknowledgementPath(outputRootPath, InvocationId);
                await WaitForFileAsync(acknowledgementPath);
                byte[] acknowledgementBytes = File.ReadAllBytes(acknowledgementPath);
                Assert.Equal(InvocationId, Encoding.ASCII.GetString(acknowledgementBytes));
                Assert.Equal(36, acknowledgementBytes.Length);

                BuildVerificationHandshakeResult result = await verification;
                Assert.True(result.StateVerificationResult.Succeeded);
                Assert.True(result.ArtifactVerificationResult.Succeeded);
                Assert.Null(result.AcknowledgementFailureMessage);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        [Theory]
        [InlineData("missing")]
        [InlineData("malformed")]
        [InlineData("stale")]
        [InlineData("failed")]
        [InlineData("foreign")]
        [InlineData("wrong-case")]
        public async Task VerifyAndAcknowledgeAsync_WhenProofIsInvalid_DoesNotAcknowledgeAndPreservesStateDiagnostic(string proofKind) {
            string outputRootPath = CreateOutputRoot();
            try {
                DateTime waiterStartedUtc = DateTime.UtcNow.AddSeconds(-1);
                if (proofKind == "malformed") {
                    File.WriteAllText(BuildInvocationProofPaths.GetProofPath(outputRootPath, InvocationId), "{ not-json");
                } else if (proofKind == "stale") {
                    WriteState(outputRootPath, InvocationId, waiterStartedUtc.AddMinutes(-2));
                } else if (proofKind == "failed") {
                    WriteState(outputRootPath, InvocationId, waiterStartedUtc, "failed", 17);
                } else if (proofKind == "foreign") {
                    WriteState(outputRootPath, "22222222-2222-2222-2222-222222222222", waiterStartedUtc);
                    File.Move(
                        BuildInvocationProofPaths.GetProofPath(outputRootPath, "22222222-2222-2222-2222-222222222222"),
                        BuildInvocationProofPaths.GetProofPath(outputRootPath, InvocationId));
                } else if (proofKind == "wrong-case") {
                    WriteStateFile(
                        BuildInvocationProofPaths.GetProofPath(outputRootPath, InvocationId),
                        InvocationId.ToUpperInvariant(), waiterStartedUtc, "succeeded", 0);
                }
                TaskCompletionSource childExit = new(TaskCreationOptions.RunContinuationsAsynchronously);
                childExit.SetResult();

                BuildVerificationHandshakeResult result = await CreateHandshake().VerifyAndAcknowledgeAsync(
                    outputRootPath, ["game.iso"], waiterStartedUtc, InvocationId, childExit.Task);

                Assert.False(File.Exists(BuildInvocationProofPaths.GetAcknowledgementPath(outputRootPath, InvocationId)));
                Assert.False(result.StateVerificationResult.Succeeded);
                Assert.Null(result.ArtifactVerificationResult);
                Assert.Null(result.AcknowledgementFailureMessage);
                string proofPath = BuildInvocationProofPaths.GetProofPath(outputRootPath, InvocationId);
                string expectedDiagnostic = proofKind switch {
                    "missing" => $"Build state proof file '{proofPath}' is missing.",
                    "malformed" => $"Build state proof file '{proofPath}' contains malformed JSON:",
                    "stale" => "Build state is stale because its start time predates this waiter invocation.",
                    "failed" => "Build state status is 'failed', not succeeded.",
                    "foreign" => "Build state build id does not match this waiter invocation.",
                    "wrong-case" => "Build state build id does not match this waiter invocation.",
                    _ => throw new InvalidOperationException($"Unsupported proof fixture '{proofKind}'.")
                };
                if (proofKind == "malformed") {
                    Assert.StartsWith(expectedDiagnostic, result.StateVerificationResult.Message, StringComparison.Ordinal);
                } else {
                    Assert.Equal(expectedDiagnostic, result.StateVerificationResult.Message);
                }
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        [Theory]
        [InlineData("missing", "game.iso")]
        [InlineData("empty", "game.iso")]
        [InlineData("stale", "game.iso")]
        [InlineData("rooted", "C:\\outside.iso")]
        [InlineData("escaping", "..\\outside.iso")]
        public async Task VerifyAndAcknowledgeAsync_WhenArtifactsAreInvalid_AcknowledgesAndPreservesArtifactDiagnostic(
            string artifactKind,
            string requiredArtifactPath) {
            string outputRootPath = CreateOutputRoot();
            try {
                DateTime waiterStartedUtc = DateTime.UtcNow.AddSeconds(-1);
                WriteState(outputRootPath, InvocationId, waiterStartedUtc);
                string artifactPath = Path.Combine(outputRootPath, "game.iso");
                if (artifactKind == "empty") {
                    File.WriteAllText(artifactPath, string.Empty);
                } else if (artifactKind == "stale") {
                    File.WriteAllText(artifactPath, "old");
                    File.SetLastWriteTimeUtc(artifactPath, waiterStartedUtc.AddMinutes(-1));
                }
                TaskCompletionSource childExit = new(TaskCreationOptions.RunContinuationsAsynchronously);

                BuildVerificationHandshakeResult result = await CreateHandshake().VerifyAndAcknowledgeAsync(
                    outputRootPath, [requiredArtifactPath], waiterStartedUtc, InvocationId, childExit.Task);

                Assert.True(File.Exists(BuildInvocationProofPaths.GetAcknowledgementPath(outputRootPath, InvocationId)));
                Assert.True(result.StateVerificationResult.Succeeded);
                Assert.False(result.ArtifactVerificationResult.Succeeded);
                string expectedDiagnostic = artifactKind switch {
                    "missing" => "Required artifact 'game.iso' is missing.",
                    "empty" => "Required artifact 'game.iso' is empty.",
                    "stale" => "Required artifact 'game.iso' is stale because it predates this build.",
                    "rooted" => "Required artifact path 'C:\\outside.iso' must be relative.",
                    "escaping" => "Required artifact path '..\\outside.iso' escapes the build output directory.",
                    _ => throw new InvalidOperationException($"Unsupported artifact fixture '{artifactKind}'.")
                };
                Assert.Equal(expectedDiagnostic, result.ArtifactVerificationResult.Message);
                Assert.Null(result.AcknowledgementFailureMessage);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        [Fact]
        public async Task VerifyAndAcknowledgeAsync_WhenAcknowledgementCannotBeCreated_PreservesVerificationResults() {
            string outputRootPath = CreateOutputRoot();
            try {
                DateTime waiterStartedUtc = DateTime.UtcNow.AddSeconds(-1);
                WriteState(outputRootPath, InvocationId, waiterStartedUtc);
                File.WriteAllText(Path.Combine(outputRootPath, "game.iso"), "iso");
                string acknowledgementPath = BuildInvocationProofPaths.GetAcknowledgementPath(outputRootPath, InvocationId);
                Directory.CreateDirectory(acknowledgementPath);
                TaskCompletionSource childExit = new(TaskCreationOptions.RunContinuationsAsynchronously);

                BuildVerificationHandshakeResult result = await CreateHandshake().VerifyAndAcknowledgeAsync(
                    outputRootPath, ["game.iso"], waiterStartedUtc, InvocationId, childExit.Task);

                Assert.True(result.StateVerificationResult.Succeeded);
                Assert.True(result.ArtifactVerificationResult.Succeeded);
                Assert.Contains(acknowledgementPath, result.AcknowledgementFailureMessage, StringComparison.Ordinal);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        static BuildVerificationHandshake CreateHandshake() => new(new BuildStateVerifier(), new BuildArtifactVerifier(), TimeSpan.FromMilliseconds(10));

        static async Task WaitForFileAsync(string path) {
            DateTime timeoutUtc = DateTime.UtcNow.AddSeconds(2);
            while (!File.Exists(path) && DateTime.UtcNow < timeoutUtc) {
                await Task.Delay(10);
            }
            Assert.True(File.Exists(path), $"Timed out waiting for '{path}'.");
        }

        static string CreateOutputRoot() {
            string outputRootPath = Path.Combine(Path.GetTempPath(), "build-waiter-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outputRootPath);
            return outputRootPath;
        }

        static void WriteState(string outputRootPath, string buildId, DateTime startedUtc, string status = "succeeded", int? exitCode = 0) {
            WriteStateFile(BuildInvocationProofPaths.GetProofPath(outputRootPath, buildId), buildId, startedUtc, status, exitCode);
        }

        static void WriteStateFile(string path, string buildId, DateTime startedUtc, string status, int? exitCode) {
            string json = JsonSerializer.Serialize(new {
                buildId,
                projectPath = "C:\\project\\project.heproj",
                platform = "ps2",
                buildProfile = "debug",
                configuration = "Debug",
                startedUtc,
                completedUtc = startedUtc.AddMilliseconds(100),
                status,
                exitCode
            });
            File.WriteAllText(path, json);
        }
    }
}
