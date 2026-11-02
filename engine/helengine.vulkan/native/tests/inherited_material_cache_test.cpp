#include "vulkan_inherited_material_cache.hpp"
#include <iostream>
#include <stdexcept>
using namespace helengine::vulkan;
/// <summary>Models mutable generated layouts and material ancestry against the production cache.</summary>
class Fixture final {
public:
    /// <summary>Provides generated name access with an injectable capture failure.</summary>
    struct Name {
        /// <summary>Owns mutable binding text.</summary>
        std::string Value="Params";
        /// <summary>Returns the live text.</summary>
        const std::string& raw() const {return Value;}
        /// <summary>Reports a present managed name.</summary>
        bool is_null() const {return false;}
    };
    /// <summary>Exposes exact mutable descriptor fields.</summary>
    struct Binding {
        /// <summary>Stores the binding name.</summary>
        Name Text;
        /// <summary>Allows in-place layout mutation.</summary>
        int Size=4;
        /// <summary>Injects failed signature capture.</summary>
        bool Fail=false;
        /// <summary>Returns current name or interrupts capture.</summary>
        const Name& get_Name() const {if(Fail) throw std::runtime_error("capture failure");return Text;}
        /// <summary>Exposes resource type.</summary>
        int get_ResourceType() const {return 1;}
        /// <summary>Exposes resource set.</summary>
        int get_Set() const {return 0;}
        /// <summary>Exposes resource slot.</summary>
        int get_Slot() const {return 3;}
        /// <summary>Exposes current size.</summary>
        int get_Size() const {return Size;}
    };
    /// <summary>Models ordered generated binding arrays.</summary>
    struct Layout {
        /// <summary>Owns the mutable descriptor.</summary>
        Binding Item;
        /// <summary>Exposes array length.</summary>
        int Length=1;
        /// <summary>Exposes the single descriptor.</summary>
        Binding* operator[](int) {++Reads;return &Item;}
        /// <summary>Tracks controlled writes independently of the material association.</summary>
        uint64_t Revision=0;
        /// <summary>Detects accidental binding scans in the inherited hot path.</summary>
        int Reads=0;
        /// <summary>Returns the live version or reproduces disposed/capture failure.</summary>
        uint64_t get_Revision() {if(Item.Fail) throw std::runtime_error("capture failure");return Revision;}
        /// <summary>Exposes controlled indexed reads.</summary>
        Binding* get_Item(int index) {return (*this)[index];}
        /// <summary>Exposes the generated collection length.</summary>
        int get_Length() {return Length;}
        /// <summary>Exposes texture bindings.</summary>
        Layout* get_TextureBindings() {return this;}
        /// <summary>Exposes constant bindings.</summary>
        Layout* get_ConstantBufferBindings() {return this;}
    };
    /// <summary>Models mutable property revision.</summary>
    struct Properties {
        /// <summary>Tracks writes.</summary>
        uint64_t Revision=0;
        /// <summary>Returns current revision.</summary>
        uint64_t get_Revision() {return Revision;}
    };
    /// <summary>Owns this material's descriptor configuration.</summary>
    Layout LayoutValue;
    /// <summary>Owns this material's revision.</summary>
    Properties PropertyValue;
    /// <summary>Borrows a mutable parent.</summary>
    Fixture* Parent=nullptr;
    /// <summary>Models disposed layouts and property blocks.</summary>
    bool Disposed=false;
    /// <summary>Tracks layout replacement.</summary>
    uint64_t LayoutRevision=0;
    /// <summary>Provides a polymorphic material base like generated runtime materials.</summary>
    virtual ~Fixture()=default;
    /// <summary>Returns parent identity.</summary>
    Fixture* get_ParentMaterial() {return Parent;}
    /// <summary>Returns live layout only.</summary>
    Layout* get_Layout() {return Disposed?nullptr:&LayoutValue;}
    /// <summary>Returns live properties only.</summary>
    Properties* get_Properties() {return Disposed?nullptr:&PropertyValue;}
    /// <summary>Returns layout generation.</summary>
    uint64_t get_LayoutRevision() {return LayoutRevision;}
};
/// <summary>Stops at the first violated cache contract.</summary>
static void Require(bool value,const char* message) {if(!value) throw std::runtime_error(message);}
/// <summary>Builds a distinguishable resolved constant payload.</summary>
static VulkanRootMaterialCache::Payload Values(uint8_t value) {VulkanRootMaterialCache::Payload result;result.Constants.push_back({3,{value}});return result;}
/// <summary>Checks independent child retention, ancestor invalidation and transactional lifecycle behavior.</summary>
int main() {
    try {
        Fixture root,other,child,sibling;child.Parent=&root;sibling.Parent=&root;
        VulkanInheritedMaterialCache a,b;
        Require(VulkanInheritedMaterialCache::ResolveRoot(&child)==&root,"wrong root");
        Require(!a.Read(&child),"empty hit");a.Publish(&child,Values(1));b.Publish(&sibling,Values(2));
        Require(a.Read(&child)->Constant(3)[0]==1 && b.Read(&sibling)->Constant(3)[0]==2,"children share payload");
        Require(root.LayoutValue.Reads==0 && child.LayoutValue.Reads==0 && sibling.LayoutValue.Reads==0,"inherited warm hit enumerated bindings");
        ++root.PropertyValue.Revision;Require(!a.Read(&child) && !b.Read(&sibling),"parent edit ignored");
        a.Publish(&child,Values(3));Require(a.Read(&child) && !b.Read(&sibling),"leaf refresh mutated sibling");
        ++child.PropertyValue.Revision;Require(!a.Read(&child),"leaf override ignored");a.Publish(&child,Values(4));
        child.Parent=&other;Require(!a.Read(&child),"reparent ignored");a.Publish(&child,Values(5));
        other.LayoutValue.Item.Size=8;++other.LayoutValue.Revision;Require(!a.Read(&child),"ancestor mutable layout ignored");a.Publish(&child,Values(6));
        ++other.LayoutRevision;Require(!a.Read(&child),"ancestor layout revision ignored");a.Publish(&child,Values(7));
        const auto retained=a.GetRetainedBytes();other.LayoutValue.Item.Fail=true;bool failed=false;
        try {a.Publish(&child,Values(8));}catch(const std::runtime_error&) {failed=true;}
        other.LayoutValue.Item.Fail=false;
        Require(failed && a.GetRetainedBytes()==retained && a.Read(&child)->Constant(3)[0]==7,"failed build replaced valid state");
        Require(a.DependsOn(&child) && a.DependsOn(&other) && !a.DependsOn(&root),"dependency identities wrong");
        other.Disposed=true;failed=false;try {VulkanInheritedMaterialCache::ResolveRoot(&child);}catch(const std::invalid_argument&) {failed=true;}
        Require(failed,"disposed ancestor accepted");other.Disposed=false;other.Parent=&child;failed=false;
        try {VulkanInheritedMaterialCache::ResolveRoot(&child);}catch(const std::invalid_argument&) {failed=true;}
        Require(failed,"cycle accepted");other.Parent=nullptr;
        a.Clear();Require(!a.Read(&child) && !a.DependsOn(&other) && a.GetRetainedBytes()==0,"clear retained dependencies");
        std::cout<<"PASS inherited material cache: independent children, ancestry, overrides, reparenting, mutable layout, failed publish, release, cycles\n";
        return 0;
    }catch(const std::exception& error) {std::cerr<<error.what()<<'\n';return 1;}
}
