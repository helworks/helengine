using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the script build fingerprint reacts to every input class that can change compiled output.
    /// </summary>
    public sealed class EditorScriptBuildFingerprintTests : IDisposable {
        /// <summary>
        /// Temporary root used by the current test instance.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Creates one isolated temporary root.
        /// </summary>
        public EditorScriptBuildFingerprintTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-script-fingerprint-tests", Guid.NewGuid().ToString("N"));
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
        /// Ensures identical inputs always produce the same fingerprint.
        /// </summary>
        [Fact]
        public void Compute_WhenInputsAreUnchanged_ReturnsSameValue() {
            EditorScriptBuildInputs inputs = CreateInputs();

            string first = EditorScriptBuildFingerprint.Compute(inputs);
            string second = EditorScriptBuildFingerprint.Compute(inputs);

            Assert.Equal(first, second);
            Assert.False(string.IsNullOrWhiteSpace(first));
        }

        /// <summary>
        /// Ensures editing a source file changes the fingerprint.
        /// </summary>
        [Fact]
        public void Compute_WhenSourceFileChanges_ReturnsDifferentValue() {
            EditorScriptBuildInputs inputs = CreateInputs();
            string before = EditorScriptBuildFingerprint.Compute(inputs);

            File.WriteAllText(Path.Combine(TempRootPath, "src", "Player.cs"), "public sealed class Player { public int Health; }");

            Assert.NotEqual(before, EditorScriptBuildFingerprint.Compute(inputs));
        }

        /// <summary>
        /// Ensures adding a source file in a nested folder changes the fingerprint.
        /// </summary>
        [Fact]
        public void Compute_WhenSourceFileIsAdded_ReturnsDifferentValue() {
            EditorScriptBuildInputs inputs = CreateInputs();
            string before = EditorScriptBuildFingerprint.Compute(inputs);

            Directory.CreateDirectory(Path.Combine(TempRootPath, "src", "Nested"));
            File.WriteAllText(Path.Combine(TempRootPath, "src", "Nested", "Enemy.cs"), "public sealed class Enemy { }");

            Assert.NotEqual(before, EditorScriptBuildFingerprint.Compute(inputs));
        }

        /// <summary>
        /// Ensures non-C# files under the source folder do not affect the fingerprint.
        /// </summary>
        [Fact]
        public void Compute_WhenNonSourceFileChanges_ReturnsSameValue() {
            EditorScriptBuildInputs inputs = CreateInputs();
            string before = EditorScriptBuildFingerprint.Compute(inputs);

            File.WriteAllText(Path.Combine(TempRootPath, "src", "notes.txt"), "irrelevant");

            Assert.Equal(before, EditorScriptBuildFingerprint.Compute(inputs));
        }

        /// <summary>
        /// Ensures a changed generated project file changes the fingerprint.
        /// </summary>
        [Fact]
        public void Compute_WhenGeneratedFileChanges_ReturnsDifferentValue() {
            EditorScriptBuildInputs inputs = CreateInputs();
            string before = EditorScriptBuildFingerprint.Compute(inputs);

            File.WriteAllText(Path.Combine(TempRootPath, "gameplay.csproj"), "<Project><PropertyGroup /></Project>");

            Assert.NotEqual(before, EditorScriptBuildFingerprint.Compute(inputs));
        }

        /// <summary>
        /// Ensures a rebuilt engine reference assembly changes the fingerprint.
        /// </summary>
        [Fact]
        public void Compute_WhenReferencedAssemblyChanges_ReturnsDifferentValue() {
            EditorScriptBuildInputs inputs = CreateInputs();
            string before = EditorScriptBuildFingerprint.Compute(inputs);

            File.WriteAllBytes(Path.Combine(TempRootPath, "helengine.core.dll"), new byte[] { 1, 2, 3, 4, 5 });

            Assert.NotEqual(before, EditorScriptBuildFingerprint.Compute(inputs));
        }

        /// <summary>
        /// Ensures a changed configuration token changes the fingerprint.
        /// </summary>
        [Fact]
        public void Compute_WhenTokenChanges_ReturnsDifferentValue() {
            EditorScriptBuildInputs inputs = CreateInputs();
            string before = EditorScriptBuildFingerprint.Compute(inputs);

            EditorScriptBuildInputs changed = new EditorScriptBuildInputs(
                inputs.GeneratedFilePaths,
                inputs.SourceDirectoryPaths,
                inputs.ReferencedAssemblyPaths,
                new[] { "mode=RuntimeOnly" });

            Assert.NotEqual(before, EditorScriptBuildFingerprint.Compute(changed));
        }

        /// <summary>
        /// Ensures a stored fingerprint round-trips through its file and mismatches after a change.
        /// </summary>
        [Fact]
        public void Store_WhenWrittenThenRead_MatchesOnlyTheStoredValue() {
            string filePath = Path.Combine(TempRootPath, "script-build.fingerprint");

            Assert.False(EditorScriptBuildFingerprint.MatchesStored(filePath, "abc"));

            EditorScriptBuildFingerprint.WriteStored(filePath, "abc");

            Assert.True(EditorScriptBuildFingerprint.MatchesStored(filePath, "abc"));
            Assert.False(EditorScriptBuildFingerprint.MatchesStored(filePath, "abd"));
        }

        /// <summary>
        /// Creates a representative input set backed by files in the temporary root.
        /// </summary>
        /// <returns>Inputs referencing one generated file, one source folder, and one reference assembly.</returns>
        EditorScriptBuildInputs CreateInputs() {
            Directory.CreateDirectory(Path.Combine(TempRootPath, "src"));
            File.WriteAllText(Path.Combine(TempRootPath, "src", "Player.cs"), "public sealed class Player { }");
            File.WriteAllText(Path.Combine(TempRootPath, "gameplay.csproj"), "<Project />");
            File.WriteAllBytes(Path.Combine(TempRootPath, "helengine.core.dll"), new byte[] { 1, 2, 3 });

            return new EditorScriptBuildInputs(
                new[] { Path.Combine(TempRootPath, "gameplay.csproj") },
                new[] { Path.Combine(TempRootPath, "src") },
                new[] { Path.Combine(TempRootPath, "helengine.core.dll") },
                new[] { "mode=EditorFull" });
        }
    }
}
