namespace helengine {
    /// <summary>Stable categories for user document failures across native hosts.</summary>
    public enum UserFileError {
        /// <summary>No failure has occurred.</summary>
        None,
        /// <summary>The platform cannot supply its Documents root.</summary>
        Unavailable,
        /// <summary>The path is rooted, contains traversal, or cannot be represented safely.</summary>
        InvalidPath,
        /// <summary>A requested filesystem object no longer exists.</summary>
        NotFound,
        /// <summary>The operating system denied access.</summary>
        AccessDenied,
        /// <summary>A traversed component is a symbolic link.</summary>
        SymbolicLink,
        /// <summary>The file exceeds the requested byte bound.</summary>
        TooLarge,
        /// <summary>The complete listing exceeds the requested entry bound.</summary>
        TooManyEntries,
        /// <summary>The bounded worker queue or retained-result memory budget is full.</summary>
        Busy,
        /// <summary>A native coordination or filesystem operation failed.</summary>
        IoError,
        /// <summary>The requested bound is zero, negative, or exceeds the platform maximum.</summary>
        InvalidLimit,
        /// <summary>The destination revision or a referenced file identity no longer matches the caller's expected contents.</summary>
        Conflict,
        /// <summary>The supplied SHA256, exact byte length or precondition identity is malformed or differs from the payload.</summary>
        InvalidPayload,
        /// <summary>The write was canceled before publication committed.</summary>
        Canceled
    }
}
