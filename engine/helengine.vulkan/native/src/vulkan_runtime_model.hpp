#pragma once
#include "RuntimeModel.hpp"
#include <cstddef>
#include "vulkan_mesh_batcher.hpp"

namespace helengine::vulkan {
/// <summary>Associates generated model bounds and copied submesh metadata with one native indexed mesh owned by the Linux renderer.</summary>
class VulkanRuntimeModel final : public ::RuntimeModel {
public:
    /// <summary>Constructs the generated runtime model and records its validated uploaded mesh handle.</summary>
    explicit VulkanRuntimeModel(size_t mesh) : Mesh(mesh) { }
    /// <summary>Identifies the renderer-owned mesh; the wrapper never deletes GPU resources independently.</summary>
    size_t Mesh;
    /// <summary>Retains immutable source vertices/indices only when a configured batching mode can consume them.</summary>
    std::shared_ptr<const VulkanMeshBatcher::Geometry> BatchSource;
};
}
