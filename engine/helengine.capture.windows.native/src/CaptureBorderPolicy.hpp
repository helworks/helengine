#pragma once
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Metadata.h>
#include <winrt/Windows.Graphics.Capture.h>
#include <winrt/Windows.Security.Authorization.AppCapabilityAccess.h>

/// <summary>Applies border removal only on explicit request and only after Windows authorization.</summary>
class CaptureBorderPolicy {
public:
    /// <summary>The default path does not require the optional borderless API.</summary>
    static void RequireProperty(bool removeBorder, bool supported) {
        if (removeBorder && !supported) { throw winrt::hresult_not_implemented(L"Borderless capture is unavailable on this Windows version."); }
    }
    /// <summary>Denial is an error because Windows can otherwise ignore IsBorderRequired(false).</summary>
    static void RequireAllowed(winrt::Windows::Security::Authorization::AppCapabilityAccess::AppCapabilityAccessStatus status) {
        using Status = winrt::Windows::Security::Authorization::AppCapabilityAccess::AppCapabilityAccessStatus;
        if (status != Status::Allowed) { throw winrt::hresult_access_denied(L"Windows did not authorize borderless capture."); }
    }
    /// <summary>Runs consent on the MTA worker, without blocking the caller's graphics callbacks.</summary>
    static void Apply(const winrt::Windows::Graphics::Capture::GraphicsCaptureSession& session, bool removeBorder) {
        if (!removeBorder) { return; }
        RequireProperty(true, winrt::Windows::Foundation::Metadata::ApiInformation::IsPropertyPresent(
            L"Windows.Graphics.Capture.GraphicsCaptureSession", L"IsBorderRequired"));
        RequireAllowed(winrt::Windows::Graphics::Capture::GraphicsCaptureAccess::RequestAccessAsync(
            winrt::Windows::Graphics::Capture::GraphicsCaptureAccessKind::Borderless).get());
        session.IsBorderRequired(false);
    }
};
