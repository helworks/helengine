#pragma once
#include "vulkan_mesh_batcher.hpp"
#include "float4x4.hpp"
#include "vulkan_matrix_key.hpp"
#include <string>
namespace helengine::vulkan {
/// <summary>Bakes the fixed GPU32 vertex layout with generated engine matrix/scalar arithmetic before the final float32 conversion.</summary>
class VulkanMeshBatchGeometry final {
public:
    /// <summary>Captures affine world and inverse-transpose matrices once for an immutable draw snapshot.</summary>
    explicit VulkanMeshBatchGeometry(::float4x4 world):World(world) { ::float4x4::InverseTranspose__ref0_out1(World,Normal); TransformKey=VulkanMatrixKey::Create(World); }
    /// <summary>Reuses the drawable's exact world key while computing the required normal transform only for supported geometry.</summary>
    VulkanMeshBatchGeometry(::float4x4 world,const std::string& key):TransformKey(key),World(world) { ::float4x4::InverseTranspose__ref0_out1(World,Normal); }
    /// <summary>Transforms positions and unnormalized normals exactly as the supported standard vertex contract; UVs and winding remain authored.</summary>
    VulkanMeshRenderer::Vertex Bake(const VulkanMeshRenderer::Vertex& input) const {
        const ::Real x(input.Position[0]),y(input.Position[1]),z(input.Position[2]);
        const ::Real nx(input.Normal[0]),ny(input.Normal[1]),nz(input.Normal[2]);
        auto output=input;
        output.Position[0]=static_cast<float>(x*World.M11+y*World.M21+z*World.M31+World.M41);
        output.Position[1]=static_cast<float>(x*World.M12+y*World.M22+z*World.M32+World.M42);
        output.Position[2]=static_cast<float>(x*World.M13+y*World.M23+z*World.M33+World.M43);
        output.Normal[0]=static_cast<float>(nx*Normal.M11+ny*Normal.M21+nz*Normal.M31);
        output.Normal[1]=static_cast<float>(nx*Normal.M12+ny*Normal.M22+nz*Normal.M32);
        output.Normal[2]=static_cast<float>(nx*Normal.M13+ny*Normal.M23+nz*Normal.M33);
        return output;
    }
    /// <summary>Snapshots actual configured scalar storage without losing transform changes through GPU32 rounding.</summary>
    const std::string& Key() const { return TransformKey; }
private:
    /// <summary>Owns the exact transform key for every submesh using this snapshot.</summary>
    std::string TransformKey;
    /// <summary>Retains selected-policy world values for positions.</summary>
    ::float4x4 World;
    /// <summary>Retains the engine's inverse-transpose normal transform.</summary>
    ::float4x4 Normal;
};
}
