namespace helengine.core.tests.content {
    /// <summary>
    /// Verifies the content-root packaged path policy that lets desktop hosts load content cooked with rooted packaged paths.
    /// </summary>
    public sealed class ContentRootPackagedAssetPathPolicyTests {
        /// <summary>
        /// A forward-slash rooted path is treated as relative to the content root and loses its leading separator.
        /// </summary>
        [Fact]
        public void ResolvePackagedPath_ForwardSlashRootedPath_StripsLeadingSeparator() {
            ContentRootPackagedAssetPathPolicy policy = new ContentRootPackagedAssetPathPolicy();

            Assert.Equal("cooked/imported/abc", policy.ResolvePackagedPath("/cooked/imported/abc"));
        }

        /// <summary>
        /// A backslash-rooted path is also rooted at the content root; its canonical remainder resolves like the forward-slash form.
        /// </summary>
        [Fact]
        public void ResolvePackagedPath_BackslashRootedCanonicalRemainder_StripsLeadingSeparator() {
            ContentRootPackagedAssetPathPolicy policy = new ContentRootPackagedAssetPathPolicy();

            Assert.Equal("cooked/x", policy.ResolvePackagedPath("\\cooked/x"));
        }

        /// <summary>
        /// A backslash-rooted path whose remainder still uses backslashes is not canonical and is rejected like any other non-canonical path.
        /// </summary>
        [Fact]
        public void ResolvePackagedPath_BackslashRootedNonCanonicalRemainder_Throws() {
            ContentRootPackagedAssetPathPolicy policy = new ContentRootPackagedAssetPathPolicy();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => policy.ResolvePackagedPath("\\cooked\\x"));

            Assert.Contains("not canonical", exception.Message);
        }

        /// <summary>
        /// An already content-relative canonical path passes through unchanged.
        /// </summary>
        [Fact]
        public void ResolvePackagedPath_RelativeCanonicalPath_PassesThrough() {
            ContentRootPackagedAssetPathPolicy policy = new ContentRootPackagedAssetPathPolicy();

            Assert.Equal("cooked/imported/abc", policy.ResolvePackagedPath("cooked/imported/abc"));
        }

        /// <summary>
        /// A drive-letter absolute path points outside the content root and is rejected with a message naming the path.
        /// </summary>
        [Fact]
        public void ResolvePackagedPath_DriveLetterPath_ThrowsNamingPath() {
            ContentRootPackagedAssetPathPolicy policy = new ContentRootPackagedAssetPathPolicy();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => policy.ResolvePackagedPath("C:/x"));

            Assert.Contains("'C:/x'", exception.Message);
        }

        /// <summary>
        /// A device-scheme path such as a console disc root points outside the content root and is rejected with a message naming the path.
        /// </summary>
        [Fact]
        public void ResolvePackagedPath_SchemePath_ThrowsNamingPath() {
            ContentRootPackagedAssetPathPolicy policy = new ContentRootPackagedAssetPathPolicy();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => policy.ResolvePackagedPath("cdrom0:/cooked/x"));

            Assert.Contains("'cdrom0:/cooked/x'", exception.Message);
        }

        /// <summary>
        /// A rooted path whose remainder is not canonical (wrong casing) is rejected by the canonical packaged path rules.
        /// </summary>
        [Fact]
        public void ResolvePackagedPath_RootedNonCanonicalCasing_Throws() {
            ContentRootPackagedAssetPathPolicy policy = new ContentRootPackagedAssetPathPolicy();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => policy.ResolvePackagedPath("/Cooked/X"));

            Assert.Contains("not canonical", exception.Message);
        }

        /// <summary>
        /// A rooted path whose remainder escapes the content root through traversal segments is rejected.
        /// </summary>
        [Fact]
        public void ResolvePackagedPath_RootedTraversal_Throws() {
            ContentRootPackagedAssetPathPolicy policy = new ContentRootPackagedAssetPathPolicy();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => policy.ResolvePackagedPath("/cooked/../x"));

            Assert.Contains("traversal", exception.Message);
        }

        /// <summary>
        /// Only one leading separator marks the content root; a doubly rooted path is still rooted after stripping and is rejected.
        /// </summary>
        [Fact]
        public void ResolvePackagedPath_DoublyRootedPath_Throws() {
            ContentRootPackagedAssetPathPolicy policy = new ContentRootPackagedAssetPathPolicy();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => policy.ResolvePackagedPath("//cooked/x"));

            Assert.Contains("must be relative", exception.Message);
        }

        /// <summary>
        /// A missing path is a caller error rather than an empty content-root path.
        /// </summary>
        [Fact]
        public void ResolvePackagedPath_BlankPath_ThrowsArgumentException() {
            ContentRootPackagedAssetPathPolicy policy = new ContentRootPackagedAssetPathPolicy();

            Assert.Throws<ArgumentException>(() => policy.ResolvePackagedPath(" "));
        }
    }
}
