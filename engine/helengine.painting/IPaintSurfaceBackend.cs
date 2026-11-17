namespace helengine.painting {
    /// <summary>Borrowed surface service; it copies submitted values and retains native GPU work until completion.</summary>
    public interface IPaintSurfaceBackend {
        /// <summary>Allocates the explicit surface geometry before the session replays authoritative CPU content.</summary>
        void Reconfigure([NativeNoEscape] PaintCanvasDescriptor canvas);
        /// <summary>Drops GPU targets, pipelines and queued marks while retaining enough CPU shader data for activation.</summary>
        void Suspend();
        /// <summary>Queues a transparent reset at a content revision without blocking the caller.</summary>
        void Reset(long revision);
        /// <summary>Copies document-space stamps before returning; the backend applies its explicit canvas pixel scale.</summary>
        void Append([NativeNoEscape] PaintStamp[] stamps,int count,PaintBlendMode mode,long revision);
        /// <summary>Submits queued paint commands and returns an owned status ticket without waiting for the GPU.</summary>
        long Submit();
        /// <summary>Queries completion without waiting or reading texture pixels.</summary>
        PaintSubmissionStatus GetSubmissionStatus(long ticket);
        /// <summary>Releases the consumer's ticket; pending GPU work remains retained internally.</summary>
        void ReleaseSubmission(long ticket);
        /// <summary>Queues an immutable snapshot of the exact requested content revision.</summary>
        long AcquireSnapshot(long revision,bool mipmapped);
        /// <summary>Queries snapshot readiness without exposing a texture still being written.</summary>
        PaintSubmissionStatus GetSnapshotStatus(long snapshot);
        /// <summary>Releases a snapshot handle after its consumers finish.</summary>
        void ReleaseSnapshot(long snapshot);
    }
}
