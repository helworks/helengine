#pragma once
#include <d3d11.h>
#include <wrl/client.h>
#include <iostream>
#include <stdexcept>
#include <vector>

/// <summary>Creates deterministic offscreen GPU inputs and reports native assertions.</summary>
class NativeTestUtils {
public:
    /// <summary>Fails the current test with its meaningful assertion message.</summary>
    static void Require(bool condition, const char* message) {
        if (!condition) { throw std::runtime_error(message); }
    }
    /// <summary>Creates a software GPU explicitly for synthetic resource tests.</summary>
    static Microsoft::WRL::ComPtr<ID3D11Device> Device() {
        Microsoft::WRL::ComPtr<ID3D11Device> device;
        HRESULT result = D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_WARP, nullptr,
            D3D11_CREATE_DEVICE_BGRA_SUPPORT, nullptr, 0, D3D11_SDK_VERSION, &device, nullptr, nullptr);
        Require(SUCCEEDED(result), "Cannot create the synthetic WARP test device.");
        return device;
    }
    /// <summary>Creates an opaque BGRA pattern with source padding outside its requested content region.</summary>
    static Microsoft::WRL::ComPtr<ID3D11Texture2D> Texture(ID3D11Device* device, UINT width = 7, UINT height = 5) {
        std::vector<uint8_t> bytes(static_cast<size_t>(width) * height * 4);
        for (size_t index = 0; index < bytes.size(); index += 4) {
            bytes[index] = 0x31; bytes[index + 1] = 0x62; bytes[index + 2] = 0x93; bytes[index + 3] = 255;
        }
        D3D11_TEXTURE2D_DESC description {};
        description.Width = width; description.Height = height; description.MipLevels = description.ArraySize = 1;
        description.Format = DXGI_FORMAT_B8G8R8A8_UNORM; description.SampleDesc.Count = 1;
        description.Usage = D3D11_USAGE_DEFAULT; description.BindFlags = D3D11_BIND_SHADER_RESOURCE;
        D3D11_SUBRESOURCE_DATA data { bytes.data(), width * 4, 0 };
        Microsoft::WRL::ComPtr<ID3D11Texture2D> texture;
        Require(SUCCEEDED(device->CreateTexture2D(&description, &data, &texture)), "Cannot create the test pattern.");
        return texture;
    }
    /// <summary>Confirms every copied pixel retained its original BGRA channel order.</summary>
    static void Pattern(const std::vector<uint8_t>& bytes) {
        for (size_t index = 0; index < bytes.size(); index += 4) {
            Require(bytes[index] == 0x31 && bytes[index + 1] == 0x62 && bytes[index + 2] == 0x93 && bytes[index + 3] == 255,
                "Captured content differs from its original BGRA pattern.");
        }
    }
};
