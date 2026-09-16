using Xunit;

namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Verifies the shared attribute probe reports missing paths without raising exceptions.
    /// </summary>
    public sealed class EditorFileAttributesProbeTests : IDisposable {
        /// <summary>
        /// Temporary root used by the current test instance.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Creates one isolated temporary root.
        /// </summary>
        public EditorFileAttributesProbeTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-attributes-probe-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);
        }

        /// <summary>
        /// Deletes temporary test state.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a missing file and a missing directory chain both report false without any exception being thrown.
        /// </summary>
        [Fact]
        public void TryGetAttributes_WhenPathIsMissing_ReturnsFalseWithoutRaisingExceptions() {
            string missingFile = Path.Combine(TempRootPath, "gone.cs");
            string missingNested = Path.Combine(TempRootPath, "no", "such", "dir");
            int firstChanceCount = 0;
            EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> handler = (sender, args) => firstChanceCount++;

            AppDomain.CurrentDomain.FirstChanceException += handler;
            bool fileFound;
            bool nestedFound;
            bool reparse;
            try {
                fileFound = EditorFileAttributesProbe.TryGetAttributes(missingFile, out _);
                nestedFound = EditorFileAttributesProbe.TryGetAttributes(missingNested, out _);
                reparse = EditorFileAttributesProbe.IsReparsePoint(missingNested);
            } finally {
                AppDomain.CurrentDomain.FirstChanceException -= handler;
            }

            Assert.False(fileFound);
            Assert.False(nestedFound);
            Assert.False(reparse);
            Assert.Equal(0, firstChanceCount);
        }

        /// <summary>
        /// Ensures existing files and directories report their attributes.
        /// </summary>
        [Fact]
        public void TryGetAttributes_WhenPathExists_ReturnsAttributes() {
            string filePath = Path.Combine(TempRootPath, "present.txt");
            File.WriteAllText(filePath, "x");

            Assert.True(EditorFileAttributesProbe.TryGetAttributes(filePath, out FileAttributes fileAttributes));
            Assert.Equal(0, (int)(fileAttributes & FileAttributes.Directory));
            Assert.True(EditorFileAttributesProbe.TryGetAttributes(TempRootPath, out FileAttributes directoryAttributes));
            Assert.NotEqual(0, (int)(directoryAttributes & FileAttributes.Directory));
            Assert.False(EditorFileAttributesProbe.IsReparsePoint(filePath));
        }
    }
}
