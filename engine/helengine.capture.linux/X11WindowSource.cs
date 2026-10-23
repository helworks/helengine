namespace helengine.capture.linux;

/// <summary>Reads only a PID-validated window's XComposite pixmap on an explicitly chosen Linux display.</summary>
sealed class X11WindowSource : ILinuxWindowSource {
    /// <summary>Dedicated Xlib connection, closed when capture is disposed.</summary>
    nint Display;
    /// <summary>Exact caller-selected window; root drawables are rejected.</summary>
    readonly nuint Window;
    /// <summary>Expected process identifier checked before and after every pixel readback.</summary>
    readonly int ProcessId;
    /// <summary>Existing _NET_WM_PID atom on this display.</summary>
    nuint PidAtom;
    /// <summary>Initial TrueColor visual used to decode pixmap images.</summary>
    X11Visual Visual;
    /// <summary>Initial visual identifier, required to remain stable for the capture lifetime.</summary>
    nint VisualPointer;
    /// <summary>Opens an explicit X11 window and retains its offscreen storage without changing focus or input.</summary>
    internal X11WindowSource(ulong windowId,int expectedProcessId) {
        if(windowId==0 || windowId>uint.MaxValue) { throw new ArgumentOutOfRangeException(nameof(windowId),"An explicit nonzero X11 window identifier is required."); }
        if(expectedProcessId<=0) { throw new ArgumentOutOfRangeException(nameof(expectedProcessId)); }
        if(!OperatingSystem.IsLinux() || IntPtr.Size!=8) { throw new PlatformNotSupportedException("Linux window capture requires a 64-bit Linux X11/Xwayland process."); }
        string displayName=Environment.GetEnvironmentVariable("DISPLAY");
        if(string.IsNullOrWhiteSpace(displayName)) { throw new PlatformNotSupportedException("Set DISPLAY explicitly for the target X11/Xwayland session. Native Wayland windows are unsupported."); }
        Window=(nuint)windowId;ProcessId=expectedProcessId;
        lock(X11ErrorTrap.Gate) {
            try {
                Display=X11Native.XOpenDisplay(displayName);
                if(Display==0) { throw new InvalidOperationException($"Cannot open X11 display '{displayName}'; verify DISPLAY and XAUTHORITY for the target session."); }
                using var trap=new X11ErrorTrap(Display);
                if(X11Native.XCompositeQueryVersion(Display,out int major,out int minor)==0 || (major==0 && minor<2)) { throw new PlatformNotSupportedException("The selected display requires XComposite 0.2 or later."); }
                PidAtom=X11Native.XInternAtom(Display,"_NET_WM_PID",1);
                var attributes=ValidateWindow(trap,false);
                Width=attributes.Width;Height=attributes.Height;VisualPointer=attributes.Visual;
                Visual=Marshal.PtrToStructure<X11Visual>(VisualPointer);
                if(Visual.Class!=4) { throw new NotSupportedException("Only X11 TrueColor window visuals are supported."); }
                X11Native.XCompositeRedirectWindow(Display,Window,0);trap.Check();
            } catch {
                if(Display!=0) { X11Native.XCloseDisplay(Display);Display=0; }
                throw;
            }
        }
    }
    /// <summary>Fixed initial client width.</summary>
    public int Width {get;}
    /// <summary>Fixed initial client height.</summary>
    public int Height {get;}
    /// <summary>Names the current selected-window storage and copies its client pixels into an independent RGBA array.</summary>
    public byte[] ReadRgba() {
        lock(X11ErrorTrap.Gate) {
            ObjectDisposedException.ThrowIf(Display==0,this);
            using var trap=new X11ErrorTrap(Display);
            var attributes=ValidateWindow(trap,true);
            nuint pixmap=X11Native.XCompositeNameWindowPixmap(Display,Window);trap.Check();
            nint image=0;
            try {
                image=X11Native.XGetImage(Display,pixmap,attributes.BorderWidth,attributes.BorderWidth,(uint)Width,(uint)Height,nuint.MaxValue,2);trap.Check();
                if(image==0) { throw new InvalidOperationException("X11 could not read the selected window's redirected pixmap."); }
                var header=Marshal.PtrToStructure<X11ImageHeader>(image);
                if(header.Width!=Width || header.Height!=Height || header.XOffset!=0 || header.Format!=2 || header.Data==0 || header.BytesPerLine<=0) { throw new InvalidDataException("X11 returned incompatible window image geometry or storage."); }
                var pixels=new byte[checked(header.BytesPerLine*Height)];Marshal.Copy(header.Data,pixels,0,pixels.Length);
                ValidateWindow(trap,true);
                return X11RgbaConverter.Convert(pixels,Width,Height,header.BytesPerLine,header.BitsPerPixel,header.ByteOrder,(ulong)Visual.RedMask,(ulong)Visual.GreenMask,(ulong)Visual.BlueMask);
            } finally {
                if(image!=0) { X11Native.XDestroyImage(image); }
                X11Native.XFreePixmap(Display,pixmap);
            }
        }
    }
    /// <summary>Rejects desktop capture, unmapped/destroyed windows, PID changes, resized frames and visual changes.</summary>
    X11WindowAttributes ValidateWindow(X11ErrorTrap trap,bool requireInitialGeometry) {
        int found=X11Native.XGetWindowAttributes(Display,Window,out var attributes);trap.Check();
        if(found==0) { throw new InvalidOperationException("The selected X11 window no longer exists."); }
        if(attributes.Root==Window || attributes.Class!=1) { throw new ArgumentException("Capture requires an application InputOutput window; desktop/root windows are forbidden."); }
        if(attributes.MapState!=2 || attributes.Width<=0 || attributes.Height<=0) { throw new InvalidOperationException("The selected X11 window must remain mapped and viewable."); }
        if(requireInitialGeometry && (attributes.Width!=Width || attributes.Height!=Height || attributes.Visual!=VisualPointer)) { throw new InvalidOperationException("The selected X11 window resized or changed visual; start a new recording with its new geometry."); }
        if(PidAtom==0) { throw new InvalidOperationException("The selected window has no _NET_WM_PID property for process validation."); }
        nint property=0;
        try {
            int status=X11Native.XGetWindowProperty(Display,Window,PidAtom,0,1,0,6,out nuint type,out int format,out nuint count,out nuint remaining,out property);trap.Check();
            if(status!=0 || type!=6 || format!=32 || count!=1 || remaining!=0 || property==0 || unchecked((uint)Marshal.ReadInt64(property))!=(uint)ProcessId) { throw new InvalidOperationException($"The selected window's _NET_WM_PID does not match process {ProcessId}."); }
        } finally { if(property!=0) { X11Native.XFree(property); } }
        return attributes;
    }
    /// <summary>Closes the dedicated connection, releasing this client's redirection without touching other clients.</summary>
    public void Dispose() {
        lock(X11ErrorTrap.Gate) {
            if(Display==0) { return; }
            X11Native.XCloseDisplay(Display);Display=0;
        }
    }
}
