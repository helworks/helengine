namespace helengine.media.windows;
/// <summary>Resolves pinned owned files into independent Windows media cursors.</summary>
public sealed class WindowsMediaSourceResolver : IMediaSourceResolver {
    /// <summary>Authorized root, never supplied by model effect parameters.</summary>
    readonly string Root;
    /// <summary>Authorized assets root used by portable caption font references.</summary>
    public string AssetsRoot=>Root;
    /// <summary>Graphics device owned by the render session.</summary>
    readonly Device Device;
    /// <summary>Cursors tracked for deterministic cleanup even when a render fails.</summary>
    readonly List<IMediaSource> Sources=[];
    /// <summary>Serializes source construction and tracked ownership across independent export producers.</summary>
    readonly object Gate=new();
    /// <summary>Prevents new source cursors after cleanup.</summary>
    bool Disposed;
    /// <summary>Stores a real authorized directory and session device.</summary>
    public WindowsMediaSourceResolver(string root,Device device) {Root=Path.GetFullPath(root);if(!Directory.Exists(Root)) {throw new DirectoryNotFoundException(Root);}Device=device ?? throw new ArgumentNullException(nameof(device));}
    /// <summary>Checks root ownership, source hash and dimensions before decoder or texture allocation.</summary>
    public IMediaSource Open(MediaReference reference) {lock(Gate) {return OpenOwned(reference);}}
    /// <summary>Opens one source while tracked ownership is protected by the resolver gate.</summary>
    IMediaSource OpenOwned(MediaReference reference) {
        if(Disposed) {throw new ObjectDisposedException(nameof(WindowsMediaSourceResolver));}ArgumentNullException.ThrowIfNull(reference);
        if(string.IsNullOrWhiteSpace(reference.Path) || Path.IsPathRooted(reference.Path) || reference.Path.Contains(':')) {throw new InvalidDataException("Media must use an owned relative path.");}
        string path=Path.GetFullPath(Path.Combine(Root,reference.Path));
        if(!path.StartsWith(Root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) {throw new InvalidDataException("Media path escaped the authorized root.");}
        CheckReparsePoints(path);
        var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        try {
            string hash=Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();stream.Position=0;
            if(!string.Equals(hash,reference.Sha256,StringComparison.OrdinalIgnoreCase)) {throw new InvalidDataException("Media bytes changed since the composition was resolved.");}
            var file=new OwnedMediaFile(path,stream);IMediaSource source;
            if(reference.Kind=="image") {source=new ImageMediaSource(file,reference,Device);}
            else if(reference.Kind=="video") {source=new VideoMediaSource(file,reference,Device);}
            else if(reference.Kind=="audio") {source=new AudioMediaSource(file);}
            else {throw new InvalidDataException("Unsupported media source kind.");}
            Sources.Add(source);return source;
        } catch {stream.Dispose();throw;}
    }
    /// <summary>Releases all tracked cursors; repeated source Dispose calls are idempotent.</summary>
    public void Dispose() {lock(Gate) {if(!Disposed) {Disposed=true;foreach(var source in Sources) {source.Dispose();}Sources.Clear();}}}
    /// <summary>Rejects symlink/reparse traversal in both the file and every parent through the authorized root.</summary>
    void CheckReparsePoints(string path) {
        string current=path;
        while(current!=null) {
            if((System.IO.File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0) {throw new InvalidDataException("Media path cannot traverse a reparse point.");}
            if(string.Equals(current,Root,StringComparison.OrdinalIgnoreCase)) {break;}current=Path.GetDirectoryName(current);
        }
    }
}
