#ifdef DrawText
#undef DrawText
#endif
#include "AnchorComponent.hpp"
#include "int2.hpp"
#include "Core.hpp"
#include "RenderManager3D.hpp"
#include "Component.hpp"
#include "float3.hpp"
#include "Entity.hpp"
#include "AnchorData.hpp"
#include "runtime/native_nullable.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_list.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
#include "runtime/native_cast.hpp"
#include "runtime/native_datetime.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_disposable.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_event.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_nullable.hpp"
#include "runtime/native_span.hpp"
#include "runtime/native_stack.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/app_context.hpp"
#include "system/bit_converter.hpp"
#include "system/diagnostics/debug.hpp"
#include "system/io/directory.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream.hpp"
#include "system/io/string-reader.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/security/cryptography/sha256.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "system/text/string-builder.hpp"

AnchorComponent::AnchorComponent() : anchorData()
{
}

bool AnchorComponent::get_IsAnchored()
{
return this->anchorData != nullptr;
}

void AnchorComponent::ComponentRemoved(::Entity* entity)
{
Component::ComponentRemoved(entity);
this->DisableAnchoring();
}

void AnchorComponent::DisableAnchoring()
{
    if (this->anchorData != nullptr)
    {
Core::get_Instance()->get_RenderManager3D()->WindowResized -= &AnchorComponent::OnWindowResized;
this->anchorData = nullptr;
    }
}

void AnchorComponent::EnableAnchoring(bool left, bool right, bool top, bool bottom)
{
    if (!left && !right && !top && !bottom)
    {
this->DisableAnchoring();
return;    }
::int2 windowSize = Core::get_Instance()->get_RenderManager3D()->get_MainWindowSize();
this->anchorData = ([&]() {
auto __object_0ee4172c = new ::AnchorData();
__object_0ee4172c->set_LeftDistance(left ? Nullable<float>(Parent->get_Position().X) : Nullable<float>(nullptr));
__object_0ee4172c->set_RightDistance(right ? Nullable<float>(windowSize.X - Parent->get_Position().X) : Nullable<float>(nullptr));
__object_0ee4172c->set_TopDistance(top ? Nullable<float>(Parent->get_Position().Y) : Nullable<float>(nullptr));
__object_0ee4172c->set_BottomDistance(bottom ? Nullable<float>(windowSize.Y - Parent->get_Position().Y) : Nullable<float>(nullptr));
return __object_0ee4172c;
})();
Core::get_Instance()->get_RenderManager3D()->WindowResized += &AnchorComponent::OnWindowResized;
}

std::string AnchorComponent::GetAnchorInfo()
{
    if (!this->get_IsAnchored())
    {
return "Not anchored";    }
std::string info = "Anchored to: ";
List<std::string> *anchors = new List<std::string>();
    if (this->anchorData->get_LeftDistance().HasValue)
    {
anchors->Add(std::string("Left (") + std::to_string(this->anchorData->get_LeftDistance().Value) + std::string("px)"));
    }
    if (this->anchorData->get_RightDistance().HasValue)
    {
anchors->Add(std::string("Right (") + std::to_string(this->anchorData->get_RightDistance().Value) + std::string("px)"));
    }
    if (this->anchorData->get_TopDistance().HasValue)
    {
anchors->Add(std::string("Top (") + std::to_string(this->anchorData->get_TopDistance().Value) + std::string("px)"));
    }
    if (this->anchorData->get_BottomDistance().HasValue)
    {
anchors->Add(std::string("Bottom (") + std::to_string(this->anchorData->get_BottomDistance().Value) + std::string("px)"));
    }
return String::Concat(info, String::JoinArray(", ", anchors->ToArray()));}

void AnchorComponent::SetAnchorDistances(Nullable<float> left, Nullable<float> right, Nullable<float> top, Nullable<float> bottom)
{
    if (this->anchorData == nullptr)
    {
this->anchorData = new ::AnchorData();
Core::get_Instance()->get_RenderManager3D()->WindowResized += &AnchorComponent::OnWindowResized;
    }
this->anchorData->set_LeftDistance(left);
this->anchorData->set_RightDistance(right);
this->anchorData->set_TopDistance(top);
this->anchorData->set_BottomDistance(bottom);
    if (!left.HasValue && !right.HasValue && !top.HasValue && !bottom.HasValue)
    {
this->DisableAnchoring();
    }
}

::Entity* AnchorComponent::get_Parent()
{
return this->Component::get_Parent();
}

void AnchorComponent::set_Parent(::Entity* value)
{
this->Component::set_Parent(value);
}

void AnchorComponent::OnWindowResized(intptr_t handle, int32_t newWidth, int32_t newHeight)
{
    if (this->anchorData == nullptr || Parent == nullptr)
    {
return;    }
::float3 pos = Parent->get_Position();
    if (this->anchorData->get_LeftDistance().HasValue)
    {
pos.X = this->anchorData->get_LeftDistance().Value;
    }
else     if (this->anchorData->get_RightDistance().HasValue)
    {
pos.X = newWidth - this->anchorData->get_RightDistance().Value;
    }
    if (this->anchorData->get_TopDistance().HasValue)
    {
pos.Y = this->anchorData->get_TopDistance().Value;
    }
else     if (this->anchorData->get_BottomDistance().HasValue)
    {
pos.Y = newHeight - this->anchorData->get_BottomDistance().Value;
    }
Parent->set_Position(pos);
}

