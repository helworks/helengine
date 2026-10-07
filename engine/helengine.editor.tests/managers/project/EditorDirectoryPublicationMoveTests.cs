namespace helengine.editor.tests {
    /// <summary>Verifies publication under real Windows directory locks and preservation of occupied destinations.</summary>
    public sealed class EditorDirectoryPublicationMoveTests : IDisposable {
        /// <summary>Isolated scratch tree beneath the test artifact directory.</summary>
        readonly string RootPath = Path.Combine(AppContext.BaseDirectory, "publication-tests", Guid.NewGuid().ToString("N"));

        /// <summary>Creates the owned test tree.</summary>
        public EditorDirectoryPublicationMoveTests() {
            Directory.CreateDirectory(RootPath);
        }

        /// <summary>Publishes a completed tree after an actual Windows handle temporarily prevents renaming it.</summary>
        [Fact]
        public async Task Move_WhenWindowsDirectoryLockIsReleased_PublishesCompletedTree() {
            if (!OperatingSystem.IsWindows()) {
                return;
            }

            string sourcePath = Path.Combine(RootPath, "staging");
            string destinationPath = Path.Combine(RootPath, "published");
            Directory.CreateDirectory(sourcePath);
            File.WriteAllText(Path.Combine(sourcePath, "payload.txt"), "completed payload");
            using FileStream payloadHandle = File.Open(
                Path.Combine(sourcePath, "payload.txt"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Exception lockedMove = Record.Exception(() => Directory.Move(sourcePath, destinationPath));
            Assert.NotNull(lockedMove);
            Assert.True(lockedMove is IOException || lockedMove is UnauthorizedAccessException);

            Task publication = Task.Run(() => EditorDirectoryPublicationMove.Move(sourcePath, destinationPath));
            try {
                await Task.Delay(300);
            } finally {
                payloadHandle.Dispose();
            }
            await publication;
            Assert.False(Directory.Exists(sourcePath));
            Assert.Equal("completed payload", File.ReadAllText(Path.Combine(destinationPath, "payload.txt")));
        }

        /// <summary>Refuses an occupied destination without modifying either the published tree or the new source.</summary>
        [Fact]
        public void Move_WhenDestinationExists_PreservesBothTreesAndReportsFailure() {
            string sourcePath = Path.Combine(RootPath, "staging");
            string destinationPath = Path.Combine(RootPath, "published");
            Directory.CreateDirectory(sourcePath);
            Directory.CreateDirectory(destinationPath);
            File.WriteAllText(Path.Combine(sourcePath, "payload.txt"), "new payload");
            File.WriteAllText(Path.Combine(destinationPath, "payload.txt"), "previous payload");

            Assert.Throws<IOException>(() => EditorDirectoryPublicationMove.Move(sourcePath, destinationPath));
            Assert.Equal("new payload", File.ReadAllText(Path.Combine(sourcePath, "payload.txt")));
            Assert.Equal("previous payload", File.ReadAllText(Path.Combine(destinationPath, "payload.txt")));
        }

        /// <summary>Removes only this test's scratch tree.</summary>
        public void Dispose() {
            if (Directory.Exists(RootPath)) {
                Directory.Delete(RootPath, true);
            }
        }

    }
}
