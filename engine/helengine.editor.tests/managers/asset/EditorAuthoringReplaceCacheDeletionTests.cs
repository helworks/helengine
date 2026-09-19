using Xunit;

namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Proves that deleting the authoring mutation cache at any point of a replace leaves the user's destination
    /// file either untouched or fully replaced, so the cache folder is safe to remove at any moment.
    /// </summary>
    public sealed class EditorAuthoringReplaceCacheDeletionTests : IDisposable {
        /// <summary>
        /// Isolated project root for the current test instance.
        /// </summary>
        readonly string ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-replace-cache-deletion-" + Guid.NewGuid().ToString("N"));

        /// <summary>
        /// Creates the project root with an assets folder.
        /// </summary>
        public EditorAuthoringReplaceCacheDeletionTests() {
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets"));
        }

        /// <summary>
        /// Clears the mutation hook and deletes the project root.
        /// </summary>
        public void Dispose() {
            EditorAuthoringMutationScope.MutationHookForTests = null;
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Records every mutation hook point a replace over an existing file passes, then for each point cuts a fresh
        /// replace there, deletes the whole authoring-mutations cache folder, and asserts the destination is either the
        /// original bytes or the replacement bytes. Recovery afterwards must be a no-op that raises nothing.
        /// </summary>
        [Fact]
        public void WriteAllBytesAtomically_CacheDeletedAtAnyCutPoint_LeavesDestinationOriginalOrReplaced() {
            byte[] originalBytes = new byte[] { 1, 2, 3 };
            byte[] replacementBytes = new byte[] { 9, 8, 7, 6 };
            string destination = Path.Combine(ProjectRootPath, "assets", "cache-cut.hasset");
            File.WriteAllBytes(destination, originalBytes);

            List<string> points = new List<string>();
            EditorAuthoringMutationScope.MutationHookForTests = point => points.Add(point);
            try {
                EditorAuthoringMutationScope.WriteAllBytesAtomically(ProjectRootPath, destination, replacementBytes);
            } finally {
                EditorAuthoringMutationScope.MutationHookForTests = null;
            }
            Assert.Equal(replacementBytes, File.ReadAllBytes(destination));
            Assert.NotEmpty(points);

            string journalRoot = Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations");
            for (int cutIndex = 0; cutIndex < points.Count; cutIndex++) {
                File.WriteAllBytes(destination, originalBytes);
                int seen = 0;
                bool interrupted = false;
                int capturedIndex = cutIndex;
                EditorAuthoringMutationScope.MutationHookForTests = point => {
                    if (!interrupted && seen == capturedIndex) {
                        interrupted = true;
                        throw new IOException("injected cache deletion cut at " + point);
                    }
                    seen++;
                };
                // Most cuts abort the replace, but a cut inside the journal retirement that follows a completed
                // publish is absorbed on purpose: the mutation already succeeded and only its bookkeeping folder is
                // left behind for startup recovery. Either outcome must satisfy the destination invariant below.
                try {
                    EditorAuthoringMutationScope.WriteAllBytesAtomically(ProjectRootPath, destination, replacementBytes);
                } catch (Exception) {
                } finally {
                    EditorAuthoringMutationScope.MutationHookForTests = null;
                }
                Assert.True(interrupted, $"Cut {cutIndex} ('{points[cutIndex]}') was never reached.");

                DeleteJournalRoot(journalRoot);

                Assert.True(File.Exists(destination), $"Cut {cutIndex} ('{points[cutIndex]}') lost the destination after the cache was deleted.");
                byte[] actual = File.ReadAllBytes(destination);
                Assert.True(actual.SequenceEqual(originalBytes) || actual.SequenceEqual(replacementBytes),
                    $"Cut {cutIndex} ('{points[cutIndex]}') left neither original nor replacement bytes.");

                int firstChanceCount = 0;
                EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> handler = (sender, args) => firstChanceCount++;
                AppDomain.CurrentDomain.FirstChanceException += handler;
                try {
                    EditorAuthoringMutationJournal.Recover(ProjectRootPath);
                } finally {
                    AppDomain.CurrentDomain.FirstChanceException -= handler;
                }
                Assert.Equal(0, firstChanceCount);
                Assert.Equal(actual, File.ReadAllBytes(destination));
            }
        }

        /// <summary>
        /// Removes the authoring mutation cache folder, retrying a few times because a Windows handle released by the
        /// interrupted cut can still hold a brief sharing lock on the operation directory.
        /// </summary>
        static void DeleteJournalRoot(string journalRoot) {
            for (int attempt = 0; attempt < 3; attempt++) {
                if (!Directory.Exists(journalRoot)) {
                    return;
                }
                try {
                    Directory.Delete(journalRoot, true);
                    return;
                } catch (IOException) {
                    Thread.Sleep(50);
                } catch (UnauthorizedAccessException) {
                    Thread.Sleep(50);
                }
            }
            if (Directory.Exists(journalRoot)) {
                Directory.Delete(journalRoot, true);
            }
        }
    }
}
