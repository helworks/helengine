using helengine;

namespace helengine.core.tests.storage {
    /// <summary>Models the explicit caller-owned lifecycle without pretending to test native filesystem behavior.</summary>
    public sealed class UserFileTestOperation : IUserFileOperation {
        /// <summary>Gets whether the caller explicitly polled this operation.</summary>
        public bool WasAdvanced { get; private set; }
        /// <summary>Gets whether the caller explicitly disposed this operation.</summary>
        public bool WasDisposed { get; private set; }
        /// <summary>Gets the current lifecycle state.</summary>
        public UserFileOperationStatus Status { get; private set; }
        /// <summary>Reports no artificial backend failures.</summary>
        public UserFileError Error => UserFileError.None;
        /// <summary>Reports no artificial backend diagnostic details.</summary>
        public string ErrorMessage => string.Empty;
        /// <summary>Reports an empty listing.</summary>
        public int EntryCount => 0;
        /// <summary>Reports no available metadata entries.</summary>
        public UserFileEntry GetEntry(int index) { throw new ArgumentOutOfRangeException(nameof(index)); }
        /// <summary>Returns empty borrowed read data.</summary>
        public byte[] Bytes { get; } = Array.Empty<byte>();
        /// <summary>Publishes fake completion only in response to an explicit caller poll.</summary>
        public void Advance() { WasAdvanced = true; if (Status == UserFileOperationStatus.Pending) { Status = UserFileOperationStatus.Succeeded; } }
        /// <summary>Preserves cancellation across subsequent fake completion polls.</summary>
        public void Cancel() { Status = UserFileOperationStatus.Canceled; }
        /// <summary>Records caller-owned disposal independently of Core's lifecycle.</summary>
        public void Dispose() { WasDisposed = true; Cancel(); }
    }
}
