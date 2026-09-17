using System.ComponentModel;
using Xunit;

namespace helengine.editor.tests.managers.project {
    /// <summary>
    /// Verifies the engine codegen provider prefers the published tool, builds on demand keyed by submodule commit, and fails loudly.
    /// </summary>
    public sealed class EngineCodegenToolProviderTests : IDisposable {
        readonly string RootPath;
        readonly string EditorBaseDirectoryPath;
        readonly string SubmoduleRootPath;
        readonly string CacheRootPath;

        public EngineCodegenToolProviderTests() {
            RootPath = Path.Combine(Path.GetTempPath(), "helengine-codegen-provider-tests", Guid.NewGuid().ToString("N"));
            EditorBaseDirectoryPath = Path.Combine(RootPath, "editor");
            SubmoduleRootPath = Path.Combine(RootPath, "submodule");
            CacheRootPath = Path.Combine(RootPath, "cache");
            Directory.CreateDirectory(EditorBaseDirectoryPath);
            Directory.CreateDirectory(Path.Combine(SubmoduleRootPath, "codegen"));
            File.WriteAllText(Path.Combine(SubmoduleRootPath, "codegen", "codegen.csproj"), "<Project />");
        }

        public void Dispose() {
            if (Directory.Exists(RootPath)) {
                Directory.Delete(RootPath, true);
            }
        }

        /// <summary>
        /// Fake publisher that records calls and materialises the tool on publish.
        /// </summary>
        sealed class FakePublisher : IEngineCodegenToolPublisher {
            public string Commit = "abc123";
            public int PublishCallCount;
            public string LastOutputDirectoryPath = string.Empty;
            public bool ThrowOnReadCommit;

            public string ReadCommit(string submoduleRootPath) {
                if (ThrowOnReadCommit) {
                    throw new InvalidOperationException("git is unavailable");
                }
                return Commit;
            }

            public void Publish(string codegenProjectPath, string outputDirectoryPath) {
                PublishCallCount++;
                LastOutputDirectoryPath = outputDirectoryPath;
                Directory.CreateDirectory(outputDirectoryPath);
                File.WriteAllText(Path.Combine(outputDirectoryPath, "codegen.exe"), string.Empty);
            }
        }

        [Fact]
        public void Resolve_WhenPublishedToolExists_ReturnsItWithoutPublishing() {
            string publishedDirectoryPath = Path.Combine(EditorBaseDirectoryPath, "codegen");
            Directory.CreateDirectory(publishedDirectoryPath);
            string publishedToolPath = Path.Combine(publishedDirectoryPath, "codegen.exe");
            File.WriteAllText(publishedToolPath, string.Empty);
            FakePublisher publisher = new();
            EngineCodegenToolProvider provider = new(EditorBaseDirectoryPath, SubmoduleRootPath, CacheRootPath, publisher);

            string resolvedPath = provider.Resolve();

            Assert.Equal(publishedToolPath, resolvedPath);
            Assert.Equal(0, publisher.PublishCallCount);
        }

        [Fact]
        public void Resolve_WhenNothingIsPublished_BuildsIntoCommitKeyedCacheDirectory() {
            FakePublisher publisher = new() { Commit = "deadbeef" };
            EngineCodegenToolProvider provider = new(EditorBaseDirectoryPath, SubmoduleRootPath, CacheRootPath, publisher);

            string resolvedPath = provider.Resolve();

            string expectedDirectoryPath = Path.Combine(CacheRootPath, "codegen", "deadbeef");
            Assert.Equal(Path.Combine(expectedDirectoryPath, "codegen.exe"), resolvedPath);
            Assert.Equal(expectedDirectoryPath, publisher.LastOutputDirectoryPath);
            Assert.Equal(1, publisher.PublishCallCount);
            Assert.True(File.Exists(resolvedPath));
        }

        [Fact]
        public void Resolve_WhenCachedBuildForCommitExists_DoesNotPublishAgain() {
            FakePublisher publisher = new() { Commit = "deadbeef" };
            string cachedDirectoryPath = Path.Combine(CacheRootPath, "codegen", "deadbeef");
            Directory.CreateDirectory(cachedDirectoryPath);
            File.WriteAllText(Path.Combine(cachedDirectoryPath, "codegen.exe"), string.Empty);
            EngineCodegenToolProvider provider = new(EditorBaseDirectoryPath, SubmoduleRootPath, CacheRootPath, publisher);

            string resolvedPath = provider.Resolve();

            Assert.Equal(Path.Combine(cachedDirectoryPath, "codegen.exe"), resolvedPath);
            Assert.Equal(0, publisher.PublishCallCount);
        }

        [Fact]
        public void Resolve_WhenCommitChanges_BuildsANewDirectory() {
            FakePublisher publisher = new() { Commit = "old000" };
            EngineCodegenToolProvider provider = new(EditorBaseDirectoryPath, SubmoduleRootPath, CacheRootPath, publisher);
            provider.Resolve();
            publisher.Commit = "new111";

            string resolvedPath = provider.Resolve();

            Assert.Equal(Path.Combine(CacheRootPath, "codegen", "new111", "codegen.exe"), resolvedPath);
            Assert.Equal(2, publisher.PublishCallCount);
        }

        [Fact]
        public void Resolve_WhenSubmoduleIsNotInitialised_ThrowsNamingSubmoduleAndCommand() {
            Directory.Delete(SubmoduleRootPath, true);
            FakePublisher publisher = new();
            EngineCodegenToolProvider provider = new(EditorBaseDirectoryPath, SubmoduleRootPath, CacheRootPath, publisher);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => provider.Resolve());

            Assert.Contains(Path.Combine(EditorBaseDirectoryPath, "codegen", "codegen.exe"), exception.Message, StringComparison.Ordinal);
            Assert.Contains(SubmoduleRootPath, exception.Message, StringComparison.Ordinal);
            Assert.Contains("git submodule update --init", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Resolve_WhenCommitCannotBeRead_ThrowsSayingGitIsRequired() {
            FakePublisher publisher = new() { ThrowOnReadCommit = true };
            EngineCodegenToolProvider provider = new(EditorBaseDirectoryPath, SubmoduleRootPath, CacheRootPath, publisher);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => provider.Resolve());

            Assert.Contains("git", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(Path.Combine(EditorBaseDirectoryPath, "codegen", "codegen.exe"), exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Resolve_WhenPublishDoesNotProduceTool_Throws() {
            EngineCodegenToolProvider provider = new(EditorBaseDirectoryPath, SubmoduleRootPath, CacheRootPath, new NoOutputPublisher());

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => provider.Resolve());

            Assert.Contains(Path.Combine(CacheRootPath, "codegen", "abc123", "codegen.exe"), exception.Message, StringComparison.Ordinal);
        }

        sealed class NoOutputPublisher : IEngineCodegenToolPublisher {
            public string ReadCommit(string submoduleRootPath) {
                return "abc123";
            }

            public void Publish(string codegenProjectPath, string outputDirectoryPath) {
                Directory.CreateDirectory(outputDirectoryPath);
            }
        }

        [Fact]
        public void Resolve_WhenPublishCannotRun_ThrowsNamingLocationsAndPreservesCause() {
            UnlaunchablePublisher publisher = new();
            EngineCodegenToolProvider provider = new(EditorBaseDirectoryPath, SubmoduleRootPath, CacheRootPath, publisher);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => provider.Resolve());

            Assert.Contains(Path.Combine(EditorBaseDirectoryPath, "codegen", "codegen.exe"), exception.Message, StringComparison.Ordinal);
            Assert.Contains(Path.Combine(SubmoduleRootPath, "codegen", "codegen.csproj"), exception.Message, StringComparison.Ordinal);
            Assert.Same(publisher.Thrown, exception.InnerException);
        }

        /// <summary>
        /// Fake publisher standing in for a missing dotnet on PATH, which surfaces as a Win32Exception from Process.Start.
        /// </summary>
        sealed class UnlaunchablePublisher : IEngineCodegenToolPublisher {
            public readonly Win32Exception Thrown = new("The system cannot find the file specified");

            public string ReadCommit(string submoduleRootPath) {
                return "abc123";
            }

            public void Publish(string codegenProjectPath, string outputDirectoryPath) {
                throw Thrown;
            }
        }
    }
}
