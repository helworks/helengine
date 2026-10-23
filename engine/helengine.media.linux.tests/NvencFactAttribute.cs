namespace helengine.media.linux.tests;

/// <summary>Runs real NVIDIA driver initialization only when a Linux host explicitly opts into hardware integration tests.</summary>
public sealed class NvencFactAttribute : FactAttribute {
    /// <summary>Reports a visible skip unless HELENGINE_TEST_NVENC=1 was deliberately selected on Linux.</summary>
    public NvencFactAttribute() {
        if(!OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable("HELENGINE_TEST_NVENC")!="1") {
            Skip="Requires Linux with HELENGINE_TEST_NVENC=1 and an installed working NVIDIA HEVC encoder.";
        }
    }
}
