Get-Process helengine_windows -ErrorAction SilentlyContinue | Stop-Process -Force

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName Microsoft.VisualBasic
Add-Type @"
using System;
using System.Runtime.InteropServices;

public static class NativeWindowCapture {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
"@

$process = Start-Process -FilePath 'C:\dev\helprojs\city\windows-build\Build\helengine_windows.exe' -PassThru
Start-Sleep -Seconds 4

$null = [Microsoft.VisualBasic.Interaction]::AppActivate($process.Id)
Start-Sleep -Milliseconds 500
$null = [NativeWindowCapture]::ShowWindow($process.MainWindowHandle, 5)
$null = [NativeWindowCapture]::SetForegroundWindow($process.MainWindowHandle)
Start-Sleep -Milliseconds 500

$windowRect = New-Object NativeWindowCapture+RECT
$null = [NativeWindowCapture]::GetWindowRect($process.MainWindowHandle, [ref]$windowRect)

$width = $windowRect.Right - $windowRect.Left
$height = $windowRect.Bottom - $windowRect.Top
$bitmap = New-Object System.Drawing.Bitmap $width, $height
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($windowRect.Left, $windowRect.Top, 0, 0, $bitmap.Size)
$bitmap.Save('C:\dev\helengine\tmp\city-render-capture.png', [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()

if ($process -and -not $process.HasExited) {
    Stop-Process -Id $process.Id -Force
}
