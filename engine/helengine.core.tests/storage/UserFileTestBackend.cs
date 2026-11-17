using helengine;

namespace helengine.core.tests.storage {
    /// <summary>Records backend calls to verify Core forwards requests without implicit polling.</summary>
    public sealed class UserFileTestBackend : IUserFileBackend {
        /// <summary>Stores the exact root-relative path supplied by the application.</summary>
        public string Path { get; private set; }
        /// <summary>Stores the application's requested bound.</summary>
        public int Limit { get; private set; }
        /// <summary>Owns the test request until its explicit transfer to the test caller.</summary>
        public UserFileTestOperation Operation { get; } = new UserFileTestOperation();
        /// <summary>Records a listing without doing filesystem work.</summary>
        public IUserFileOperation BeginList(string relativePath, int maximumEntries) { Path = relativePath; Limit = maximumEntries; return Operation; }
        /// <summary>Records a read without doing filesystem work.</summary>
        public IUserFileOperation BeginRead(string relativePath, int maximumBytes) { Path = relativePath; Limit = maximumBytes; return Operation; }
    }
}
