#pragma once
#include "vulkan_matrix_key.hpp"
#include "float3.hpp"
#include <array>
#include <cstdint>
#include <vector>
#include <stdexcept>
namespace helengine::vulkan {
/// <summary>Captures all dynamic camera dependencies exactly once after successful light and shadow preparation.</summary>
class VulkanCameraPreparationKey final {
public:
    /// <summary>Serializes native camera scalars, fixed GPU uniform bytes and selected resource identities without hashing or padding.</summary>
    static std::string Create(const ::float4x4& matrix,const ::float3& camera,const std::vector<uint8_t>& lights,const std::vector<uint8_t>& shadows,size_t atlas,const std::array<size_t,4>& cubes,size_t neutral) {
        if(lights.size()!=288 || shadows.size()!=400) throw std::runtime_error("Linux prepared camera requires completed light and shadow uniforms.");
        auto key=VulkanMatrixKey::Create(matrix);
        const he_real_storage position[]={camera.X.Value,camera.Y.Value,camera.Z.Value};
        const size_t resources[]={atlas,cubes[0],cubes[1],cubes[2],cubes[3],neutral};
        key.reserve(key.size()+sizeof(position)+lights.size()+shadows.size()+sizeof(resources));
        key.append(reinterpret_cast<const char*>(position),sizeof(position));
        key.append(reinterpret_cast<const char*>(lights.data()),lights.size());
        key.append(reinterpret_cast<const char*>(shadows.data()),shadows.size());
        key.append(reinterpret_cast<const char*>(resources),sizeof(resources));
        return key;
    }
};
}
