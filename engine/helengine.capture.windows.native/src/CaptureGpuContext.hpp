#pragma once
#include <d3d11.h>
#include <d3d11_4.h>
#include <wrl/client.h>
#include <winrt/base.h>
#include <memory>
#include <mutex>
#include <stdexcept>

/// <summary>Retains the GPU and synchronizes all library context operations independently of the capture session.</summary>
class CaptureGpuContext {
public:
    /// <summary>Owns a COM reference; a caller's original device reference is never consumed.</summary>
    Microsoft::WRL::ComPtr<ID3D11Device> Device;
    /// <summary>Retains the immediate context used for texture copies and explicit readback.</summary>
    Microsoft::WRL::ComPtr<ID3D11DeviceContext> Context;
    /// <summary>Serializes library work with other library frame readbacks.</summary>
    std::mutex Mutex;
    /// <summary>Imports a valid device or creates a hardware device with BGRA support.</summary>
    static std::shared_ptr<CaptureGpuContext> Create(ID3D11Device* supplied) {
        auto result = std::make_shared<CaptureGpuContext>();
        if (supplied) {
            winrt::check_hresult(supplied->QueryInterface(IID_PPV_ARGS(&result->Device)));
            if ((result->Device->GetCreationFlags() & D3D11_CREATE_DEVICE_SINGLETHREADED) != 0) {
                throw std::invalid_argument("A single-threaded D3D11 device cannot support capture callbacks.");
            }
        } else {
            winrt::check_hresult(D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr,
                D3D11_CREATE_DEVICE_BGRA_SUPPORT, nullptr, 0, D3D11_SDK_VERSION, &result->Device, nullptr, nullptr));
        }
        result->Device->GetImmediateContext(&result->Context);
        Microsoft::WRL::ComPtr<ID3D11Multithread> multithread;
        winrt::check_hresult(result->Context.As(&multithread)); multithread->SetMultithreadProtected(TRUE);
        return result;
    }
};
