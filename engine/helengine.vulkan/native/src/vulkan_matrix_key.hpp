#pragma once
#include "float4x4.hpp"
#include <string>
namespace helengine::vulkan {
/// <summary>Serializes selected-policy native matrix scalars without padding, inverses, hashing or GPU32 rounding.</summary>
class VulkanMatrixKey final {
public:
    /// <summary>Captures every native scalar storage value in matrix element order.</summary>
    static std::string Create(const ::float4x4& world) {
        const he_real_storage values[]={world.M11.Value,world.M12.Value,world.M13.Value,world.M14.Value,world.M21.Value,world.M22.Value,world.M23.Value,world.M24.Value,world.M31.Value,world.M32.Value,world.M33.Value,world.M34.Value,world.M41.Value,world.M42.Value,world.M43.Value,world.M44.Value};
        return std::string(reinterpret_cast<const char*>(values),sizeof(values));
    }
};
}
