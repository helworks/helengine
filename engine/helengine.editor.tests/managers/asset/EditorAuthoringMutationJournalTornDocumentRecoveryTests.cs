using Xunit;

namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Replays a replace that stopped while promoting its journal document and proves boot recovery finishes it
    /// without raising a single exception, so a debugger with first-chance breaks enabled stays quiet.
    /// </summary>
    public sealed class EditorAuthoringMutationJournalTornDocumentRecoveryTests : IDisposable {
        /// <summary>
        /// Isolated project root for the current test instance.
        /// </summary>
        readonly string ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-mutation-torn-document-" + Guid.NewGuid().ToString("N"));

        /// <summary>
        /// Creates the project root with an assets folder.
        /// </summary>
        public EditorAuthoringMutationJournalTornDocumentRecoveryTests() {
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
        /// A replace interrupted while promoting the document that records its publishing payload leaves
        /// document.next and document.old with no document.json while the destination still holds its original
        /// bytes. Recovery must publish the staged payload and retire the operation without any exception.
        /// </summary>
        [Fact]
        public void Recover_WhenReplaceStoppedBeforePromotingItsDocument_FinishesWithoutFirstChanceExceptions() {
            string destination = Path.Combine(ProjectRootPath, "assets", "torn.hasset");
            byte[] originalBytes = new byte[] { 1, 2, 3 };
            byte[] replacementBytes = new byte[] { 4, 5, 6, 7 };
            File.WriteAllBytes(destination, originalBytes);

            string operationDirectory = InterruptReplaceBeforeDocumentPromotion(destination, replacementBytes);

            Assert.False(File.Exists(Path.Combine(operationDirectory, "document.json")));
            Assert.True(File.Exists(Path.Combine(operationDirectory, "document.next")));
            Assert.True(File.Exists(Path.Combine(operationDirectory, "document.old")));
            Assert.Equal(originalBytes, File.ReadAllBytes(destination));

            int firstChanceCount = 0;
            string firstStackTrace = string.Empty;
            EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> handler = (sender, args) => {
                firstChanceCount++;
                if (string.IsNullOrEmpty(firstStackTrace)) {
                    firstStackTrace = args.Exception.GetType().Name + ": " + args.Exception.Message + Environment.NewLine + Environment.StackTrace;
                }
            };

            AppDomain.CurrentDomain.FirstChanceException += handler;
            try {
                EditorAuthoringMutationJournal.Recover(ProjectRootPath);
            } finally {
                AppDomain.CurrentDomain.FirstChanceException -= handler;
            }

            Assert.True(firstChanceCount == 0, $"Saw {firstChanceCount} first-chance exception(s) during recovery. First:{Environment.NewLine}{firstStackTrace}");
            Assert.Equal(replacementBytes, File.ReadAllBytes(destination));
            Assert.Empty(Directory.GetFileSystemEntries(Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations")));
        }

        /// <summary>
        /// Runs a real atomic replace and cuts it at the rename that promotes document.next once the staged payload
        /// has been moved to its publishing name. Returns the surviving operation directory.
        /// </summary>
        string InterruptReplaceBeforeDocumentPromotion(string destination, byte[] replacementBytes) {
            bool payloadPublishing = false;
            bool interrupted = false;
            EditorAuthoringMutationScope.MutationHookForTests = point => {
                if (point == "FixedRename.BeforeSyscall:payload->payload.publishing") {
                    payloadPublishing = true;
                    return;
                }
                if (payloadPublishing && !interrupted && point == "FixedRename.BeforeSyscall:document.next->document.json") {
                    interrupted = true;
                    throw new IOException("injected document promotion cut");
                }
            };
            try {
                Assert.Throws<IOException>(() => EditorAuthoringMutationScope.WriteAllBytesAtomically(ProjectRootPath, destination, replacementBytes));
            } finally {
                EditorAuthoringMutationScope.MutationHookForTests = null;
            }

            Assert.True(interrupted, "The replace never reached the document promotion rename.");
            string journalRoot = Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations");
            return Assert.Single(Directory.GetDirectories(journalRoot));
        }
    }
}
