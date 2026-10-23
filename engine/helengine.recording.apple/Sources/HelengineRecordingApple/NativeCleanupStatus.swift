/// Separates native lifecycle work still pending from ordinary empty or failed output.
struct NativeCleanupStatus {
    /// Is true only when the native start callback has actually returned.
    let startCompleted: Bool
    /// Is true only when stopping capture has failed or timed out.
    let stopPending: Bool
    /// Is true only when MP4 finalization still owns an outstanding callback.
    let writerPending: Bool

    /// Computes current native pending state without retaining an obsolete startup snapshot.
    var isPending: Bool { !startCompleted || stopPending || writerPending }
}
