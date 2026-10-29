#pragma once
#include "vulkan_root_material_cache.hpp"
#include "vulkan_prepared_material.hpp"

namespace helengine::vulkan {
/// <summary>Retains one material's resolved properties and ancestor collection revisions; hits walk only its depth without allocation.</summary>
class VulkanInheritedMaterialCache final {
public:
    /// <summary>Owns this material's latest immutable draw preparation independently of siblings sharing a pipeline.</summary>
    VulkanPreparedMaterial Prepared;
    /// <summary>Rejects cycles and missing/disposed shader state before any generated recursive resolution; returns the live root.</summary>
    template<class TMaterial> static TMaterial* ResolveRoot(TMaterial* material) {
        if(!material) throw std::invalid_argument("Vulkan material ancestry requires a live shader material.");
        if(!Parent(material)) return material;
        auto* slow=material;auto* fast=material;
        while(fast) {
            fast=Parent(fast);
            if(!fast) break;
            fast=Parent(fast);slow=Parent(slow);
            if(fast && fast==slow) throw std::invalid_argument("Vulkan material ancestry contains a cycle.");
        }
        while(auto* parent=Parent(material)) material=parent;
        return material;
    }
    /// <summary>Validates every current identity, parent, revision and binding collection before exposing any borrowed texture or private constant.</summary>
    template<class TMaterial> const VulkanRootMaterialCache::Payload* Read(TMaterial* material) const {
        const VulkanRootMaterialCache::Payload* values=nullptr;
        for(const auto& node:Chain) {
            if(!material || node.Material!=material) return nullptr;
            auto* layout=material->get_Layout();auto* properties=material->get_Properties();
            if(!layout || !properties || node.Parent!=material->get_ParentMaterial()) return nullptr;
            const auto* found=node.Signature.Read(Identity(material),layout->get_TextureBindings(),layout->get_ConstantBufferBindings());
            if(!found) return nullptr;
            if(!values) values=found;
            material=Parent(material);
        }
        return material?nullptr:values;
    }
    /// <summary>Captures all ancestry signatures before swapping ownership, preserving the previous complete entry if any capture fails.</summary>
    template<class TMaterial> void Publish(TMaterial* material,VulkanRootMaterialCache::Payload values) {
        ResolveRoot(material);
        std::vector<Node> replacement;
        while(material) {
            auto* layout=material->get_Layout();
            Node node{material,material->get_ParentMaterial(),{}};
            node.Signature.Publish(Identity(material),layout->get_TextureBindings(),layout->get_ConstantBufferBindings(),replacement.empty()?std::move(values):VulkanRootMaterialCache::Payload{});
            replacement.push_back(std::move(node));material=Parent(material);
        }
        Chain.swap(replacement);
    }
    /// <summary>Matches captured dependencies without dereferencing material pointers during explicit pre-disposal invalidation.</summary>
    bool DependsOn(const void* material) const {
        for(const auto& node:Chain) if(node.Material==material) return true;
        return false;
    }
    /// <summary>Discards dependency signatures, property copies and preparation while queued snapshots retain their independent ownership.</summary>
    void Clear() {std::vector<Node>().swap(Chain);Prepared.Clear();}
    /// <summary>Counts logical property/ancestry storage; compatibility diagnostics retain their historical root-cache field names.</summary>
    size_t GetRetainedBytes() const {
        size_t bytes=Chain.size()*(sizeof(Node)-sizeof(VulkanRootMaterialCache));
        for(const auto& node:Chain) bytes+=node.Signature.GetRetainedBytes();
        return bytes;
    }
    /// <summary>Counts the leaf's resolved private constants once, excluding signature-only ancestor entries.</summary>
    size_t GetPayloadBytes() const {return Chain.empty()?0:Chain.front().Signature.GetPayloadBytes();}
private:
    /// <summary>Stores borrowed identity values and collection revisions or owned legacy metadata, never dereferencing historical dependencies.</summary>
    struct Node {
        /// <summary>Records the material identity associated with this signature.</summary>
        const void* Material;
        /// <summary>Records the parent identity even when revisions did not change.</summary>
        const void* Parent;
        /// <summary>Owns layout validation state and, on the leaf only, resolved properties.</summary>
        VulkanRootMaterialCache Signature;
    };
    /// <summary>Keeps the most recently successful leaf-to-root association, replacing rather than accumulating revisions.</summary>
    std::vector<Node> Chain;
    /// <summary>Rejects missing shader state and non-shader parents before advancing along generated ancestry.</summary>
    template<class TMaterial> static TMaterial* Parent(TMaterial* material) {
        if(!material->get_Layout() || !material->get_Properties()) throw std::invalid_argument("Vulkan material ancestry contains missing or disposed shader state.");
        auto* parent=material->get_ParentMaterial();
        auto* shader=dynamic_cast<TMaterial*>(parent);
        if(parent && !shader) throw std::invalid_argument("Vulkan material ancestry requires shader material parents.");
        return shader;
    }
    /// <summary>Uses the root-cache collection-version primitive for each validated association, with ancestry tracked separately.</summary>
    template<class TMaterial> static VulkanRootMaterialCache::Identity Identity(TMaterial* material) {
        auto* properties=material->get_Properties();
        return {material->get_Layout(),properties,properties->get_Revision(),material->get_LayoutRevision(),true};
    }
};
}
