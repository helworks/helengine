#pragma once
#include "vulkan_mesh_renderer.hpp"
#include "vulkan_root_material_cache.hpp"
#include "vulkan_prepared_material.hpp"
#include <string>
#include <memory>

class RuntimeTexture;

namespace helengine::vulkan {
/// <summary>Retains an authored Vulkan pipeline and exact reflected resource identities independently of generated runtime material inheritance.</summary>
class VulkanMaterialResource final {
public:
    /// <summary>Describes one declared uniform, texture or sampler by its authored identity and unified binding slot.</summary>
    struct Binding {
        /// <summary>Preserves the authored resource name used to resolve inherited material properties.</summary>
        std::string Name;
        /// <summary>Preserves the Vulkan layout reflected from the shader program definition.</summary>
        VulkanMeshRenderer::BindingLayout Layout;
    };
    /// <summary>Owns versioned root-only CPU property copies until this native material resource is released.</summary>
    VulkanRootMaterialCache RootProperties;
    /// <summary>Retains one completed immutable root preparation until mutation, dynamic dependency change or native material release.</summary>
    VulkanPreparedMaterial Prepared;
    /// <summary>Identifies the renderer-owned native pipeline for this root material.</summary>
    size_t Pipeline;
    /// <summary>Interns exact SPIR-V, entry points, reflection and fixed pipeline state independently of material object identity.</summary>
    size_t BatchPipelineKey=0;
    /// <summary>Allows world-space baking only for the explicitly supported built-in opaque forward shader contract.</summary>
    bool SupportsBatching=false;
    /// <summary>Tracks the source shader so explicit invalidation can reject live material users.</summary>
    std::string ShaderId;
    /// <summary>Retains descriptor identities for immutable per-draw resource snapshots.</summary>
    std::vector<Binding> Bindings;
    /// <summary>Counts reflected uniform descriptors once so each draw reserves only its required buffer snapshots.</summary>
    size_t UniformBindingCount=0;
    /// <summary>Counts transform uniforms omitted from unsupported materials' baked snapshots.</summary>
    size_t TransformBindingCount=0;
    /// <summary>Keeps immutable imported GPU textures resident until the final native material using each resource is released.</summary>
    std::vector<std::shared_ptr<::RuntimeTexture>> Textures;
};
}
