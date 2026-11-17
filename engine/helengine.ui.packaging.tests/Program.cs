using helengine;
using helengine.editor;
using helengine.ui.format;

namespace helengine.ui.packaging.tests {
    /// <summary>Runs source-linked packaging regressions without compiling the editor, application or runtime.</summary>
    public static class Program {
        /// <summary>Fixed disposable fixture root; compiler artifacts use the caller's --artifacts-path.</summary>
        static readonly string TestRoot = @"C:\temp\helengine-ui-format\packaging-tests";

        /// <summary>Counts writer calls so invalid inputs cannot accidentally create package output.</summary>
        static int Writes;

        /// <summary>Verifies binary preservation, propagated failures and confined output paths.</summary>
        /// <param name="args">Zero arguments, or the absolute path of one additional real UI fixture.</param>
        public static void Main(string[] args) {
            if (args.Length > 1) {
                throw new ArgumentException("Expected zero arguments or one UI fixture path.");
            }
            Directory.CreateDirectory(TestRoot);
            try {
                using UiDocument document = new UiDocument();
                VerifyRoundTrip(UiBinaryCodec.Write(document));
                if (args.Length == 1) {
                    VerifyRoundTrip(File.ReadAllBytes(args[0]));
                }
                VerifyInvalidInputs();
                VerifyConfinedWrites();
                Console.WriteLine("PASS: UI packaging preserves binary bytes and packaged references; missing, corrupt, oversized and escaping inputs fail before output.");
            } finally {
                Directory.Delete(TestRoot, true);
            }
        }

        /// <summary>Ensures validated input reaches the tracked writer and returned reference without reserialization.</summary>
        /// <param name="bytes">Complete valid UI binary fixture.</param>
        static void VerifyRoundTrip(byte[] bytes) {
            string sourcePath = Path.Combine(TestRoot, "authored.heui");
            File.WriteAllBytes(sourcePath, bytes);
            Writes = 0;
            SceneAssetReference reference = new SceneAssetReference(SceneAssetReferenceSourceKind.FileSystem, "UI/Library.heui", "", "", "");
            SceneAssetReference packaged = new UiBinaryAssetCooker().Cook(reference, CreateContext(sourcePath));
            Require(Writes == 1, "Expected one tracked output write.");
            Require(packaged.RelativePath == "cooked/ui/library.heui", "Cooker returned the wrong runtime reference.");
            Require(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(TestRoot, "build", "cooked", "ui", "library.heui"))), "Cooker changed the authored bytes.");
        }

        /// <summary>Ensures invalid or missing authored input never invokes the writer or reference factory.</summary>
        static void VerifyInvalidInputs() {
            string sourcePath = Path.Combine(TestRoot, "invalid.heui");
            SceneAssetReference reference = new SceneAssetReference(SceneAssetReferenceSourceKind.FileSystem, "ui/invalid.heui", "", "", "");
            File.Delete(sourcePath);
            Writes = 0;
            Expect<FileNotFoundException>(() => new UiBinaryAssetCooker().Cook(reference, CreateContext(sourcePath)));
            File.WriteAllBytes(sourcePath, new byte[20]);
            Expect<FormatException>(() => new UiBinaryAssetCooker().Cook(reference, CreateContext(sourcePath)));
            using (FileStream stream = File.OpenWrite(sourcePath)) {
                stream.SetLength((long)UiFormatLimits.Bytes + 1);
            }
            Expect<FormatException>(() => new UiBinaryAssetCooker().Cook(reference, CreateContext(sourcePath)));
            Require(Writes == 0, "Invalid inputs reached the package writer.");
        }

        /// <summary>Checks binary writes share the serialized-asset and reference confinement boundary.</summary>
        static void VerifyConfinedWrites() {
            PackagedAssetCookContext context = CreateContext(Path.Combine(TestRoot, "authored.heui"));
            Writes = 0;
            foreach (string path in new[] { "/escape", "\\escape", "C:\\escape", "C:escape", "\\\\server\\share\\escape", "../escape", "cooked/../../escape", "cooked\\..\\..\\escape" }) {
                Expect<ArgumentException>(() => context.WriteBytes(path, new byte[0]));
                Expect<ArgumentException>(() => context.CreatePackagedReference(path));
            }
            Expect<ArgumentNullException>(() => context.WriteBytes("cooked/null.heui", null));
            PackagedAssetCookContext noBinaryWriter = new PackagedAssetCookContext(
                Path.Combine(TestRoot, "authored.heui"), Path.Combine(TestRoot, "build"), "ios", "",
                (path, asset) => throw new InvalidOperationException("Unexpected asset serialization."),
                path => throw new InvalidOperationException("Unexpected reference creation."));
            Expect<InvalidOperationException>(() => noBinaryWriter.WriteBytes("cooked/no-writer.heui", new byte[0]));
            Require(Writes == 0, "Invalid output paths reached the package writer.");
        }

        /// <summary>Supplies real byte output through the callback boundary used by the scene packager.</summary>
        /// <param name="sourcePath">Authored path passed to the generic cooker.</param>
        /// <returns>A context backed by the fixed disposable build root.</returns>
        static PackagedAssetCookContext CreateContext(string sourcePath) {
            return new PackagedAssetCookContext(
                sourcePath, Path.Combine(TestRoot, "build"), "ios", "",
                (path, asset) => throw new InvalidOperationException("UI binaries must not enter the asset serializer."),
                path => {
                    Require(Writes > 0, "Reference was created before output validation and writing.");
                    return new SceneAssetReference(SceneAssetReferenceSourceKind.FileSystem, path, "", "", "");
                },
                (path, bytes) => {
                    Writes++;
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, bytes);
                });
        }

        /// <summary>Fails the regression run when a required invariant is false.</summary>
        /// <param name="condition">Invariant evaluated by the test.</param>
        /// <param name="message">Diagnostic naming the broken contract.</param>
        static void Require(bool condition, string message) {
            if (!condition) {
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>Requires the specified propagated failure instead of accepting a fallback or unrelated exception.</summary>
        /// <typeparam name="T">Expected exception category.</typeparam>
        /// <param name="action">Operation expected to reject its input.</param>
        static void Expect<T>(Action action) where T : Exception {
            try {
                action();
            } catch (T) {
                return;
            }
            throw new InvalidOperationException("Expected " + typeof(T).Name + ".");
        }
    }
}
