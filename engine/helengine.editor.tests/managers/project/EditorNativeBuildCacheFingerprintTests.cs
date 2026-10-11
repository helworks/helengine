using Xunit;

namespace helengine.editor.tests.managers.project {
    /// <summary>
    /// Guards complete native-cache identities and the MSVC probe path that failed under the canonical Windows cache layout.
    /// </summary>
    public sealed class EditorNativeBuildCacheFingerprintTests {
        /// <summary>Checks compact encoding against the published SHA-256 digest of the ASCII text abc.</summary>
        [Fact]
        public void Encode_KnownSha256Digest_PreservesAllBits() {
            byte[] digest = Convert.FromHexString("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
            Assert.Equal("xj4bnp4pahh6uqkbidpf3lrceoyagyndsylxvhfucd7wd4qacwwq",
                EditorNativeBuildCacheFingerprint.Encode(digest));
        }

        /// <summary>Ensures the final digest bit still separates namespaces after Windows filename case folding.</summary>
        [Fact]
        public void Encode_DigestsDifferingInLastBit_RemainDistinctOnWindows() {
            byte[] first = new byte[32];
            byte[] second = new byte[32];
            second[31] = 1;
            string firstKey = EditorNativeBuildCacheFingerprint.Encode(first);
            string secondKey = EditorNativeBuildCacheFingerprint.Encode(second);
            Assert.Equal(new string('a', 52), firstKey);
            Assert.Equal(new string('a', 51) + "q", secondKey);
            Assert.False(string.Equals(firstKey, secondKey, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Reproduces the default cache's 260-character CMake object path and verifies the compact identity leaves it below the MSVC limit.</summary>
        [Fact]
        public void Encode_CanonicalCMakeProbePath_LeavesRoomForObjectFile() {
            byte[] digest = new byte[32];
            string cacheRoot = Path.Combine(@"C:\dev\helworks\builds\helengine\cache\v2", new string('a', 32),
                "b", "windows", "debug", "debug", "native");
            string probeObject = Path.Combine("CMakeFiles", "CMakeScratch", "TryCompile-8514t8",
                "CMakeFiles", "cmTC_22991.dir", "testCXXCompiler.cxx.obj");
            string hexadecimalPath = Path.Combine(cacheRoot, Convert.ToHexString(digest), probeObject);
            string compactPath = Path.Combine(cacheRoot, EditorNativeBuildCacheFingerprint.Encode(digest), probeObject);
            Assert.Equal(260, hexadecimalPath.Length);
            Assert.Equal(248, compactPath.Length);
            Assert.Matches("^[a-z2-7]{52}$", EditorNativeBuildCacheFingerprint.Encode(digest));
        }

        /// <summary>Rejects missing or incomplete fingerprints instead of assigning them a cache namespace.</summary>
        [Fact]
        public void Encode_InvalidDigest_Throws() {
            Assert.Throws<ArgumentNullException>(() => EditorNativeBuildCacheFingerprint.Encode(null));
            Assert.Throws<ArgumentException>(() => EditorNativeBuildCacheFingerprint.Encode(new byte[31]));
            Assert.Throws<ArgumentException>(() => EditorNativeBuildCacheFingerprint.Encode(new byte[33]));
        }
    }
}
