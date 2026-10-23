namespace helengine.capture.linux.tests;

/// <summary>Runs native capture checks only when explicitly enabled on an isolated Linux test display.</summary>
sealed class LinuxCaptureFactAttribute : FactAttribute {
    /// <summary>Marks native tests skipped until the caller opts into own-window tests.</summary>
    public LinuxCaptureFactAttribute() {
        if(!OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable("HELENGINE_LINUX_CAPTURE_TESTS")!="1") {
            Skip="Requires Linux and HELENGINE_LINUX_CAPTURE_TESTS=1 on an explicitly isolated DISPLAY.";
        }
    }
}
