using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
namespace helengine.tools.windowrecorder;
/// <summary>Enumerates visible top-level window metadata without selecting or capturing a source.</summary>
static class WindowCatalog {
    /// <summary>Collects handles, owning processes and titles, tolerating process disappearance.</summary>
    internal static IReadOnlyList<WindowCatalogEntry> List() {
        var entries = new List<WindowCatalogEntry>();
        if (!EnumWindows((hwnd, _) => Add(entries, hwnd), IntPtr.Zero)) { throw new Win32Exception(Marshal.GetLastWin32Error(), "Window enumeration failed."); }
        return entries;
    }
    /// <summary>Visits one handle; inaccessible process names remain empty rather than changing capture targets.</summary>
    static bool Add(List<WindowCatalogEntry> entries, IntPtr hwnd) {
        if (!IsWindowVisible(hwnd)) { return true; }
        GetWindowThreadProcessId(hwnd, out uint processId);
        var title = new StringBuilder(Math.Clamp(GetWindowTextLength(hwnd) + 1, 1, 32768)); GetWindowText(hwnd, title, title.Capacity);
        string processName = "";
        try { using var process = Process.GetProcessById(checked((int)processId)); processName = process.ProcessName; }
        catch (ArgumentException) { }
        catch (InvalidOperationException) { }
        catch (Win32Exception) { }
        entries.Add(new() { Hwnd = "0x" + hwnd.ToInt64().ToString("X", System.Globalization.CultureInfo.InvariantCulture), ProcessId = processId, ProcessName = processName, Title = title.ToString() }); return true;
    }
    /// <summary>Callback kept alive by the synchronous EnumWindows invocation.</summary>
    delegate bool EnumWindowCallback(IntPtr hwnd, IntPtr parameter);
    /// <summary>Enumerates only top-level window handles.</summary>
    [DllImport("user32", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] static extern bool EnumWindows(EnumWindowCallback callback, IntPtr parameter);
    /// <summary>Filters handles to currently visible windows.</summary>
    [DllImport("user32")] [return: MarshalAs(UnmanagedType.Bool)] static extern bool IsWindowVisible(IntPtr hwnd);
    /// <summary>Retrieves process ownership without opening a capture session.</summary>
    [DllImport("user32")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    /// <summary>Returns the current title length.</summary>
    [DllImport("user32", CharSet = CharSet.Unicode)] static extern int GetWindowTextLength(IntPtr hwnd);
    /// <summary>Copies title metadata into a bounded buffer.</summary>
    [DllImport("user32", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int maximum);
}
