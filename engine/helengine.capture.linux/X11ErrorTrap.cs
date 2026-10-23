namespace helengine.capture.linux;

/// <summary>Bounds Xlib's global protocol handler while forwarding failures from unrelated display connections.</summary>
sealed class X11ErrorTrap : IDisposable {
    /// <summary>Serializes this backend's Xlib connections and global handler lifetime.</summary>
    internal static readonly object Gate=new();
    /// <summary>Native error callback signature.</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int ErrorHandler(nint display,nint error);
    /// <summary>Display whose errors belong to the current capture operation.</summary>
    readonly nint Display;
    /// <summary>Previous process-wide handler restored after synchronization.</summary>
    readonly nint Previous;
    /// <summary>Rooted managed callback for the full native handler lifetime.</summary>
    readonly ErrorHandler Handler;
    /// <summary>Prior callback used to forward errors from other display connections.</summary>
    readonly Delegate PreviousHandler;
    /// <summary>First pending protocol failure for this operation.</summary>
    X11ErrorEvent Failure;
    /// <summary>Prevents a cleanup synchronization from reporting the same already-thrown protocol error twice.</summary>
    bool Reported;
    /// <summary>Publishes the prior handler before a concurrent foreign-display callback can forward to it.</summary>
    int HandlerReady;
    /// <summary>Installs a temporary trap while the caller holds the shared backend gate.</summary>
    internal X11ErrorTrap(nint display) {
        Display=display;Handler=Handle;
        Previous=X11Native.XSetErrorHandler(Marshal.GetFunctionPointerForDelegate(Handler));
        if(Previous==0) {
            X11Native.XSetErrorHandler(0);
            throw new PlatformNotSupportedException("Xlib did not return its previous error handler; preserving unrelated display error behavior is required.");
        }
        PreviousHandler=Marshal.GetDelegateForFunctionPointer(Previous,typeof(ErrorHandler));
        Volatile.Write(ref HandlerReady,1);
    }
    /// <summary>Synchronizes and turns any asynchronous native error into a descriptive managed failure.</summary>
    internal void Check() {
        X11Native.XSync(Display,0);
        ThrowIfFailure();
    }
    /// <summary>Reports the first native error without concealing later cleanup-only failures.</summary>
    void ThrowIfFailure() {
        if(Failure.ErrorCode==0) { return; }
        Reported=true;
        throw new InvalidOperationException($"X11 window capture failed: protocol error {Failure.ErrorCode}, request {Failure.RequestCode}.{Failure.MinorCode}, resource 0x{Failure.ResourceId:x}.");
    }
    /// <summary>Collects only this capture display's errors and preserves other clients' error handling.</summary>
    int Handle(nint display,nint error) {
        if(display!=Display) {
            while(Volatile.Read(ref HandlerReady)==0) { Thread.Yield(); }
            return (int)PreviousHandler.DynamicInvoke(display,error);
        }
        if(Failure.ErrorCode==0) { Failure=Marshal.PtrToStructure<X11ErrorEvent>(error); }
        return 0;
    }
    /// <summary>Drains native requests and restores the prior global handler even after a failed capture.</summary>
    public void Dispose() {
        try { X11Native.XSync(Display,0); }
        finally { X11Native.XSetErrorHandler(Previous);GC.KeepAlive(Handler); }
        if(!Reported) { ThrowIfFailure(); }
    }
}
