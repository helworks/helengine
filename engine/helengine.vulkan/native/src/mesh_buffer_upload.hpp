#pragma once
#include <memory>
#include <stdexcept>
#include <utility>
namespace helengine::vulkan {
/// <summary>Publishes mesh vertex/index ownership only after both resource factories complete; a failed second factory releases the first owner.</summary>
class MeshBufferUpload final {
public:
    /// <summary>Creates a transactional pair for immutable staged uploads or coherent transient allocation, preserving original exceptions and single ownership.</summary>
    template<typename Buffer,typename VertexFactory,typename IndexFactory>
    static void Initialize(std::unique_ptr<Buffer>& vertices,std::unique_ptr<Buffer>& indices,VertexFactory&& vertexFactory,IndexFactory&& indexFactory) {
        if(vertices || indices) throw std::logic_error("Mesh buffer upload requires unpublished empty owners.");
        auto vertex=std::forward<VertexFactory>(vertexFactory)();
        if(!vertex) throw std::logic_error("Mesh vertex factory returned no resource owner.");
        auto index=std::forward<IndexFactory>(indexFactory)();
        if(!index) throw std::logic_error("Mesh index factory returned no resource owner.");
        vertices=std::move(vertex); indices=std::move(index);
    }
};
}
