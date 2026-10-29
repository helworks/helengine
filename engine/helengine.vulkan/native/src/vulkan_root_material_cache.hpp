#pragma once
#include "vulkan_mesh_renderer.hpp"
#include <optional>
#include <stdexcept>
#include <string>
#include <type_traits>

class RuntimeTexture;

namespace helengine::vulkan {
/// <summary>Owns copied root-material properties across visits, guarded by collection identity/revisions or exact legacy binding metadata.</summary>
class VulkanRootMaterialCache final {
public:
    /// <summary>Identifies a root's current property/layout association independently of allocator address reuse.</summary>
    struct Identity {
        /// <summary>Borrows the current layout only for equality, never dereferencing it after capture.</summary>
        const void* Layout;
        /// <summary>Borrows the current property-block identity, whose lifetime remains owned by the material.</summary>
        const void* Properties;
        /// <summary>Tracks all property writes, including partially applied copy operations.</summary>
        uint64_t PropertyRevision;
        /// <summary>Tracks property-block/layout replacement even if its old address is reused.</summary>
        uint64_t LayoutRevision;
        /// <summary>Disallows use by inherited materials, which retain the original resolution path.</summary>
        bool IsRoot;
        /// <summary>Requires every invalidation component to remain equal.</summary>
        bool operator==(const Identity&) const=default;
    };
    /// <summary>Retains a resolved borrowed texture identity, revalidated as a GPU resource at each actual visit.</summary>
    struct TextureBinding {
        /// <summary>Identifies the authored unified shader binding.</summary>
        uint32_t Slot;
        /// <summary>Borrows the material's texture without changing its lifetime or renderer ownership.</summary>
        ::RuntimeTexture* Value;
    };
    /// <summary>Owns only material constants and resolved texture identities; dynamic scene uniforms never enter the cache.</summary>
    struct Payload {
        /// <summary>Retains private immutable constant-buffer copies by reflected slot.</summary>
        std::vector<VulkanMeshRenderer::BufferBinding> Constants;
        /// <summary>Retains borrowed texture identities, excluding camera/shadow samplers and images.</summary>
        std::vector<TextureBinding> Textures;
        /// <summary>Returns a captured constant payload without exposing mutable cache storage.</summary>
        const std::vector<uint8_t>& Constant(uint32_t slot) const {
            for(const auto& item:Constants) if(item.Slot==slot) return item.Bytes;
            throw std::logic_error("Root material cache is missing a captured constant binding.");
        }
        /// <summary>Returns a borrowed texture for immediate validation by the owning renderer.</summary>
        ::RuntimeTexture* Texture(uint32_t slot) const {
            for(const auto& item:Textures) if(item.Slot==slot) return item.Value;
            throw std::logic_error("Root material cache is missing a captured texture binding.");
        }
        /// <summary>Counts private constant bytes independently of signature/container/allocator overhead.</summary>
        size_t GetPayloadBytes() const {
            size_t bytes=0;for(const auto& item:Constants) bytes+=item.Bytes.size();return bytes;
        }
    };
    /// <summary>Reads no cached payload until root identity and current collection revisions or legacy layout metadata match.</summary>
    template<class TTextures,class TConstants> const Payload* Read(const Identity& identity,TTextures* textures,TConstants* constants) const {
        if(!identity.IsRoot || !Data || !(Data->Key==identity)) return nullptr;
        if(!Matches(Data->Textures,textures) || !Matches(Data->Constants,constants)) return nullptr;
        return &Data->Values;
    }
    /// <summary>Builds all replacement state first; exceptions leave the old entry intact but unable to hit a changed identity/layout.</summary>
    template<class TTextures,class TConstants> void Publish(const Identity& identity,TTextures* textures,TConstants* constants,Payload values) {
        if(!identity.IsRoot) throw std::invalid_argument("Only root materials may publish persistent property copies.");
        Entry replacement{identity,Capture(textures),Capture(constants),std::move(values)};
        static_assert(std::is_nothrow_move_constructible_v<Entry> && std::is_nothrow_move_assignable_v<Entry>);
        Data=std::move(replacement);
    }
    /// <summary>Discards all owned CPU copies and signatures without releasing any borrowed engine/GPU resource.</summary>
    void Clear() {Data.reset();}
    /// <summary>Reports retained logical owned bytes, including signature/container records and name contents, excluding allocator slack/metadata.</summary>
    size_t GetRetainedBytes() const {
        if(!Data) return 0;
        size_t bytes=sizeof(Entry)+Data->Values.GetPayloadBytes()+Data->Values.Constants.size()*sizeof(VulkanMeshRenderer::BufferBinding)+Data->Values.Textures.size()*sizeof(TextureBinding);
        for(const auto* bindings:{&Data->Textures,&Data->Constants}) {
            bytes+=bindings->Items.size()*sizeof(Binding);
            for(const auto& item:bindings->Items) bytes+=item.Name.size();
        }
        return bytes;
    }
    /// <summary>Reports only private constant bytes, allowing diagnostics to separate useful payload from cache control state.</summary>
    size_t GetPayloadBytes() const {return Data?Data->Values.GetPayloadBytes():0;}
private:
    /// <summary>Captures exact binding metadata, including null slots and managed null/empty string distinction.</summary>
    struct Binding {
        /// <summary>Preserves a null array entry independently of any default metadata values.</summary>
        bool Present=false;
        /// <summary>Preserves managed string null identity.</summary>
        bool NameNull=false;
        /// <summary>Owns exact name bytes without borrowing a disposed layout.</summary>
        std::string Name;
        /// <summary>Records the authored resource type.</summary>
        int32_t Type=0;
        /// <summary>Records the authored descriptor set.</summary>
        int32_t Set=0;
        /// <summary>Records the authored slot.</summary>
        int32_t Slot=0;
        /// <summary>Records the authored byte size.</summary>
        int32_t Size=0;
    };
    /// <summary>Retains controlled collection identity/revision or exact legacy array configuration, distinguishing null from empty.</summary>
    struct Bindings {
        /// <summary>Records whether the original array was null.</summary>
        bool Null;
        /// <summary>Borrows collection identity only; no old collection is dereferenced.</summary>
        const void* Collection=nullptr;
        /// <summary>Captures the collection version before resolved values are published.</summary>
        uint64_t Revision=0;
        /// <summary>Owns every ordered entry, including duplicates and null slots.</summary>
        std::vector<Binding> Items;
    };
    /// <summary>Publishes one complete immutable association and property payload transactionally.</summary>
    struct Entry {
        /// <summary>Stores the exact invalidation identity captured with these values.</summary>
        Identity Key;
        /// <summary>Owns the texture lookup configuration.</summary>
        Bindings Textures;
        /// <summary>Owns the constant lookup configuration.</summary>
        Bindings Constants;
        /// <summary>Owns material-only private copies.</summary>
        Payload Values;
    };
    /// <summary>Remains empty until a complete successful miss has resolved all required properties.</summary>
    std::optional<Entry> Data;
    /// <summary>Checks versioned collections in constant time; legacy arrays retain exact ordered field comparison.</summary>
    template<class TBindings> static bool Matches(const Bindings& expected,TBindings* current) {
        if(!current) return expected.Null;
        if constexpr(requires {current->get_Revision();}) {
            return !expected.Null && expected.Collection==current && expected.Revision==current->get_Revision();
        } else {
            if(expected.Collection || expected.Null || current->Length<0 || static_cast<size_t>(current->Length)!=expected.Items.size()) return false;
            for(int32_t index=0;index<current->Length;++index) {
                auto* binding=(*current)[index];const auto& item=expected.Items[static_cast<size_t>(index)];
                if(!binding) {if(item.Present) return false;continue;}
                if(!item.Present) return false;
                const auto& name=binding->get_Name();
                if(name.is_null()!=item.NameNull || name.raw()!=item.Name || static_cast<int32_t>(binding->get_ResourceType())!=item.Type || binding->get_Set()!=item.Set || binding->get_Slot()!=item.Slot || binding->get_Size()!=item.Size) return false;
            }
            return true;
        }
    }
    /// <summary>Captures collection identity/revision without enumerating; legacy arrays copy exact metadata transactionally.</summary>
    template<class TBindings> static Bindings Capture(TBindings* current) {
        Bindings result{};result.Null=current==nullptr;
        if(!current) return result;
        if constexpr(requires {current->get_Revision();}) {
            result.Collection=current;result.Revision=current->get_Revision();
        } else {
            if(current->Length<0) throw std::invalid_argument("Material binding array has negative length.");
            result.Items.reserve(static_cast<size_t>(current->Length));
            for(int32_t index=0;index<current->Length;++index) {
                auto* binding=(*current)[index];Binding item;
                if(binding) {
                    const auto& name=binding->get_Name();item.Present=true;item.NameNull=name.is_null();item.Name=name.raw();
                    item.Type=static_cast<int32_t>(binding->get_ResourceType());item.Set=binding->get_Set();item.Slot=binding->get_Slot();item.Size=binding->get_Size();
                }
                result.Items.push_back(std::move(item));
            }
        }
        return result;
    }
};
}
