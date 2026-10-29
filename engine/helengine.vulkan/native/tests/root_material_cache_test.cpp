#include "vulkan_root_material_cache.hpp"
#include <array>
#include <iostream>
#include <stdexcept>
using namespace helengine::vulkan;
/// <summary>Models live generated binding getters without substituting a second cache implementation.</summary>
class Fixture {
public:
    /// <summary>Stores exact managed name bytes and null identity for layout-signature checks.</summary>
    struct Name {
        /// <summary>Contains ordinal name bytes.</summary>
        std::string Value;
        /// <summary>Distinguishes null from empty managed strings.</summary>
        bool Null=false;
        /// <summary>Exposes borrowed bytes like HeCppString.</summary>
        const std::string& raw() const {return Value;}
        /// <summary>Exposes the managed null marker.</summary>
        bool is_null() const {return Null;}
    };
    /// <summary>Supplies mutable test descriptors so every observable field is checked on a hit.</summary>
    struct Binding {
        /// <summary>Stores the current name.</summary>
        Name Text;
        /// <summary>Stores a descriptor field tested for stale signatures.</summary>
        int32_t Size=4;
        /// <summary>Injects a getter failure after earlier signature entries have already been captured.</summary>
        bool FailName=false;
        /// <summary>Returns the live name or reproduces an interrupted signature capture.</summary>
        const Name& get_Name() const {if(FailName) throw std::runtime_error("capture interrupted");return Text;}
        /// <summary>Returns a fixed type for this fixture.</summary>
        int32_t get_ResourceType() const {return 1;}
        /// <summary>Returns the only supported set.</summary>
        int32_t get_Set() const {return 0;}
        /// <summary>Returns a fixed slot; ordering is independently observable.</summary>
        int32_t get_Slot() const {return 3;}
        /// <summary>Returns current size metadata.</summary>
        int32_t get_Size() const {return Size;}
    };
    /// <summary>Preserves null entries and duplicates in their exact order.</summary>
    std::array<Binding*,3> Values{};
    /// <summary>Matches generated array length access.</summary>
    int32_t Length=3;
    /// <summary>Returns each current entry without caching its index.</summary>
    Binding* operator[](int32_t index) const {return Values.at(index);}
};
/// <summary>Models controlled collection revisions while counting every metadata enumeration.</summary>
class VersionedFixture final : public Fixture {
public:
    /// <summary>Tracks writes before the collection publishes them.</summary>
    uint64_t Revision=0;
    /// <summary>Counts enumeration through either API to catch legacy scans.</summary>
    mutable int Reads=0;
    /// <summary>Models disposed collections and interrupted revision capture.</summary>
    bool Fail=false;
    /// <summary>Returns the live version or rejects disposed access.</summary>
    uint64_t get_Revision() const {if(Fail) throw std::runtime_error("disposed collection");return Revision;}
    /// <summary>Returns the fixed slot count.</summary>
    int get_Length() const {return Length;}
    /// <summary>Counts generated indexed reads.</summary>
    Binding* get_Item(int index) const {++Reads;return Values.at(index);}
    /// <summary>Counts legacy scans so the regression fails against the old cache.</summary>
    Binding* operator[](int index) const {return get_Item(index);}
    /// <summary>Publishes effective writes with a new revision.</summary>
    void set_Item(int index,Binding* value) {if(Values.at(index)!=value){++Revision;Values.at(index)=value;}}
};
/// <summary>Fails immediately if stale data could be reused.</summary>
static void Require(bool value,const char* message) {if(!value) throw std::runtime_error(message);}
/// <summary>Verifies revisions, exact mutable layout checks, ownership and transactional publication in the production cache.</summary>
int main() {
    try {
        using Cache=VulkanRootMaterialCache;
        Fixture::Binding first{{"Params"}},unicode{{"\xc3\xa9"}};
        Fixture layout;layout.Values={&first,nullptr,&unicode};
        Cache cache;Cache::Identity identity{&layout,&first,1,2,true};
        Require(!cache.Read(identity,&layout,&layout),"empty cache hit");
        Cache::Payload payload;payload.Constants.push_back({3,{1,2,3,4}});
        auto* texture=reinterpret_cast<::RuntimeTexture*>(uintptr_t(13));payload.Textures.push_back({4,texture});
        cache.Publish(identity,&layout,&layout,std::move(payload));
        auto* found=cache.Read(identity,&layout,&layout);
        Require(found && found->Constant(3)==std::vector<uint8_t>({1,2,3,4}) && found->Texture(4)==texture,"published payload changed");
        Require(cache.GetRetainedBytes()>4 && cache.GetPayloadBytes()==4,"byte accounting invalid");
        auto changed=identity;++changed.PropertyRevision;Require(!cache.Read(changed,&layout,&layout),"property revision ignored");
        changed=identity;++changed.LayoutRevision;Require(!cache.Read(changed,&layout,&layout),"layout generation ignored");
        changed=identity;changed.Properties=&unicode;Require(!cache.Read(changed,&layout,&layout),"property identity ignored");
        changed=identity;changed.IsRoot=false;Require(!cache.Read(changed,&layout,&layout),"parented material hit");
        first.Size=8;Require(!cache.Read(identity,&layout,&layout),"binding metadata mutation ignored");first.Size=4;
        first.Text.Value="Other";Require(!cache.Read(identity,&layout,&layout),"name mutation ignored");first.Text.Value="Params";
        layout.Values[1]=&first;Require(!cache.Read(identity,&layout,&layout),"null slot replacement ignored");layout.Values[1]=nullptr;
        std::swap(layout.Values[0],layout.Values[2]);Require(!cache.Read(identity,&layout,&layout),"ordered Unicode layout mutation ignored");std::swap(layout.Values[0],layout.Values[2]);
        first.Text.Null=true;Require(!cache.Read(identity,&layout,&layout),"managed null name marker ignored");first.Text.Null=false;
        Require(!cache.Read(identity,static_cast<Fixture*>(nullptr),&layout),"null array incorrectly hit");
        bool rejected=false;try {cache.Publish(changed,&layout,&layout,Cache::Payload{});}catch(const std::invalid_argument&) {rejected=true;}
        Require(rejected && cache.Read(identity,&layout,&layout),"failed publish destroyed a valid entry");
        const auto retained=cache.GetRetainedBytes();unicode.FailName=true;bool interrupted=false;
        try {cache.Publish(identity,&layout,&layout,Cache::Payload{});}catch(const std::runtime_error&) {interrupted=true;}
        unicode.FailName=false;
        Require(interrupted && cache.GetRetainedBytes()==retained && cache.Read(identity,&layout,&layout)->Constant(3)==std::vector<uint8_t>({1,2,3,4}),"partial capture failure published or lost a prior entry");
        cache.Clear();Require(!cache.Read(identity,&layout,&layout) && cache.GetRetainedBytes()==0,"clear retained stale entry");
        VersionedFixture versioned,other;versioned.Values={&first,nullptr,&unicode};other.Values=versioned.Values;
        cache.Publish(identity,&versioned,&versioned,Cache::Payload{});
        const auto reads=versioned.Reads;
        Require(cache.Read(identity,&versioned,&versioned) && versioned.Reads==reads,"warm versioned hit enumerated bindings");
        Require(!cache.Read(identity,&other,&versioned),"equal revision on distinct collection hit");
        versioned.set_Item(1,&first);Require(!cache.Read(identity,&versioned,&versioned),"replacement revision ignored");
        cache.Publish(identity,&versioned,&versioned,Cache::Payload{});
        versioned.set_Item(0,&unicode);versioned.set_Item(2,&first);
        Require(!cache.Read(identity,&versioned,&versioned),"reorder revision ignored");
        cache.Publish(identity,&versioned,&versioned,Cache::Payload{});
        versioned.set_Item(1,nullptr);Require(!cache.Read(identity,&versioned,&versioned),"null replacement revision ignored");
        cache.Publish(identity,&versioned,&versioned,Cache::Payload{});
        Require(!cache.Read(identity,static_cast<VersionedFixture*>(nullptr),&versioned),"null collection hit");
        other.Fail=true;interrupted=false;
        try {cache.Publish(identity,&versioned,&other,Cache::Payload{});}catch(const std::runtime_error&) {interrupted=true;}
        Require(interrupted && cache.Read(identity,&versioned,&versioned),"failed revision capture replaced valid state");
        versioned.Fail=true;interrupted=false;
        try {cache.Read(identity,&versioned,&versioned);}catch(const std::runtime_error&) {interrupted=true;}
        Require(interrupted,"disposed versioned collection hit");
        std::cout<<"PASS root material cache exact revisions/layout, ownership and transactional publication\n";return 0;
    } catch(const std::exception& error) {std::cerr<<error.what()<<'\n';return 1;}
}
