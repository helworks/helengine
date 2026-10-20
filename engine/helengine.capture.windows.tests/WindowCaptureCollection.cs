namespace helengine.capture.windows.tests;
/// <summary>Serializes synthetic windows and capture sessions to avoid desktop-dependent test interference.</summary>
[CollectionDefinition("Window capture", DisableParallelization = true)]
public sealed class WindowCaptureCollection {}
