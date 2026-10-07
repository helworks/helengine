namespace helengine.media.windows;
/// <summary>Keeps verified bytes locked against replacement or write for the cursor lifetime.</summary>
public sealed class OwnedMediaFile : IDisposable {
    /// <summary>Stores a verified file lease and its immutable resolved native path.</summary>
    public OwnedMediaFile(string path,FileStream stream) {Path=path;Stream=stream ?? throw new ArgumentNullException(nameof(stream));}
    /// <summary>Verified full path used by the platform codec backend.</summary>
    public string Path {get;}
    /// <summary>Read-only lease that denies source mutation during a render.</summary>
    public FileStream Stream {get;}
    /// <summary>Releases the immutable source lease.</summary>
    public void Dispose() => Stream.Dispose();
}
