using System.Diagnostics;
using Xunit;

namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Verifies verified reads inside a read batch share directory scopes and stay correct before, during, and after the batch.
    /// </summary>
    public sealed class EditorAuthoringReadBatchTests : IDisposable {
        /// <summary>
        /// Temporary project root used by the current test instance.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Creates one isolated project root with a few files spread over two directories.
        /// </summary>
        public EditorAuthoringReadBatchTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-read-batch-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "a"));
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "b"));
            File.WriteAllText(Path.Combine(ProjectRootPath, "assets", "a", "one.txt"), "one");
            File.WriteAllText(Path.Combine(ProjectRootPath, "assets", "a", "two.txt"), "two");
            File.WriteAllText(Path.Combine(ProjectRootPath, "assets", "b", "three.txt"), "three");
        }

        /// <summary>
        /// Deletes temporary test state.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures reads return the file contents whether or not a batch is active, including nested batches.
        /// </summary>
        [Fact]
        public void ReadAllBytes_WithAndWithoutBatch_ReturnsFileContents() {
            string one = Path.Combine(ProjectRootPath, "assets", "a", "one.txt");
            string three = Path.Combine(ProjectRootPath, "assets", "b", "three.txt");

            Assert.Equal("one", ReadText(one));

            using (EditorAuthoringReadBatch.Begin(ProjectRootPath)) {
                Assert.Equal("one", ReadText(one));
                Assert.Equal("three", ReadText(three));
                using (EditorAuthoringReadBatch.Begin(ProjectRootPath)) {
                    Assert.Equal("two", ReadText(Path.Combine(ProjectRootPath, "assets", "a", "two.txt")));
                }
                Assert.Equal("one", ReadText(one));
            }

            Assert.Equal("three", ReadText(three));
        }

        /// <summary>
        /// Ensures a batch for a different project root does not serve reads for this one.
        /// </summary>
        [Fact]
        public void TryGetScope_WhenBatchIsForAnotherRoot_ReturnsNull() {
            string otherRoot = Path.Combine(ProjectRootPath, "other");
            string otherAssets = Path.Combine(otherRoot, "assets");
            Directory.CreateDirectory(otherAssets);

            using (EditorAuthoringReadBatch.Begin(otherRoot)) {
                Assert.Null(EditorAuthoringReadBatch.TryGetScope(ProjectRootPath, Path.Combine(ProjectRootPath, "assets", "a")));
                Assert.NotNull(EditorAuthoringReadBatch.TryGetScope(otherRoot, otherAssets));
            }

            Assert.Null(EditorAuthoringReadBatch.TryGetScope(otherRoot, otherAssets));
        }

        /// <summary>
        /// Ensures a batch never pins directories outside the assets tree, where writers stage and rename temporary folders.
        /// </summary>
        [Fact]
        public void TryGetScope_WhenDirectoryIsOutsideAssets_ReturnsNull() {
            string cacheDirectory = Path.Combine(ProjectRootPath, "cache", "editor");
            Directory.CreateDirectory(cacheDirectory);

            using (EditorAuthoringReadBatch.Begin(ProjectRootPath)) {
                Assert.Null(EditorAuthoringReadBatch.TryGetScope(ProjectRootPath, cacheDirectory));
                Assert.False(EditorAuthoringReadBatch.TryPinDirectory(ProjectRootPath, cacheDirectory));
                Assert.False(EditorAuthoringReadBatch.TryPinDirectory(ProjectRootPath, Path.Combine(ProjectRootPath, "assets", "missing")));
                Assert.NotNull(EditorAuthoringReadBatch.TryGetScope(ProjectRootPath, Path.Combine(ProjectRootPath, "assets", "a")));
            }
        }

        /// <summary>
        /// Ensures batched reads of many files in one directory are markedly cheaper than unbatched ones.
        /// </summary>
        [Fact]
        public void ReadAllBytes_WhenBatched_IsFasterThanUnbatchedForManyFiles() {
            string directory = Path.Combine(ProjectRootPath, "assets", "many");
            Directory.CreateDirectory(directory);
            string[] files = new string[200];
            for (int index = 0; index < files.Length; index++) {
                files[index] = Path.Combine(directory, $"file{index}.txt");
                File.WriteAllText(files[index], index.ToString());
            }

            Stopwatch unbatched = Stopwatch.StartNew();
            for (int index = 0; index < files.Length; index++) {
                EditorAuthoringMutationScope.ReadAllBytes(ProjectRootPath, files[index]);
            }
            unbatched.Stop();

            Stopwatch batched = Stopwatch.StartNew();
            using (EditorAuthoringReadBatch.Begin(ProjectRootPath)) {
                for (int index = 0; index < files.Length; index++) {
                    EditorAuthoringMutationScope.ReadAllBytes(ProjectRootPath, files[index]);
                }
            }
            batched.Stop();

            Assert.True(batched.ElapsedMilliseconds * 3 < unbatched.ElapsedMilliseconds + 30,
                $"Batched {batched.ElapsedMilliseconds} ms was not clearly faster than unbatched {unbatched.ElapsedMilliseconds} ms.");
        }

        string ReadText(string path) {
            return System.Text.Encoding.UTF8.GetString(EditorAuthoringMutationScope.ReadAllBytes(ProjectRootPath, path));
        }
    }
}
