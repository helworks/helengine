#ifdef DrawText
#undef DrawText
#endif
#include "AnchorComponent.hpp"
#include "runtime/native_exceptions.hpp"
#include "int2.hpp"
#include "float3.hpp"
#include "Entity.hpp"
#include "AnchorData.hpp"
#include "runtime/native_nullable.hpp"
#include "Component.hpp"
#include "IAnchorBoundsProvider.hpp"
#include "IAnchorSizeProvider.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_list.hpp"
#include "Core.hpp"
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
#include "system/diagnostics/stopwatch.hpp"
#include "system/guid.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream-reader.hpp"
#include "system/io/stream.hpp"
#include "system/io/string-reader.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/security/cryptography/sha256.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "system/text/string-builder.hpp"

AnchorComponent::AnchorComponent() : IsSubscribedToWindowResize(), anchorBoundsProvider(), anchorData()
{
}

bool AnchorComponent::get_IsAnchored()
{
return this->anchorData != nullptr;
}

void AnchorComponent::ComponentAdded(::Entity* entity)
{
Component::ComponentAdded(entity);
    if (this->anchorData != nullptr)
    {
this->RefreshSubscriptions();
this->RefreshAnchoring();
    }
}

void AnchorComponent::ComponentRemoved(::Entity* entity)
{
Component::ComponentRemoved(entity);
this->DisableAnchoring();
}

void AnchorComponent::DisableAnchoring()
{
this->DetachFromBoundsProvider();
this->DetachFromWindowResize();
this->anchorData = nullptr;
}

void AnchorComponent::EnableAnchoring(bool left, bool right, bool top, bool bottom)
{
    if (!left && !right && !top && !bottom)
    {
this->DisableAnchoring();
return;    }
    if (Parent == nullptr)
    {
throw new InvalidOperationException("AnchorComponent must be attached before anchoring can be enabled.");
    }
int2 *anchorBounds = this->GetAnchorBounds();
int2 *anchoredSize = this->GetAnchorSize();
::float3 localPosition = Parent->get_LocalPosition();
this->anchorData = ([&]() {
auto __object_0000017A = new ::AnchorData();
__object_0000017A->set_LeftDistance(left ? Nullable<float>(localPosition.X) : Nullable<float>(nullptr));
__object_0000017A->set_RightDistance(right ? Nullable<float>(anchorBounds->X - localPosition.X - anchoredSize->X) : Nullable<float>(nullptr));
__object_0000017A->set_TopDistance(top ? Nullable<float>(localPosition.Y) : Nullable<float>(nullptr));
__object_0000017A->set_BottomDistance(bottom ? Nullable<float>(anchorBounds->Y - localPosition.Y - anchoredSize->Y) : Nullable<float>(nullptr));
return __object_0000017A;
})();
this->RefreshSubscriptions();
this->RefreshAnchoring();
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

void AnchorComponent::RefreshAnchoring()
{
    if (this->anchorData == nullptr || Parent == nullptr)
    {
return;    }
this->RefreshSubscriptions();
int2 *anchorBounds = this->GetAnchorBounds();
int2 *anchorSize = this->GetAnchorSize();
::float3 localPosition = Parent->get_LocalPosition();
    if (this->anchorData->get_LeftDistance().HasValue)
    {
localPosition.X = this->anchorData->get_LeftDistance().Value;
    }
else     if (this->anchorData->get_RightDistance().HasValue)
    {
localPosition.X = anchorBounds->X - this->anchorData->get_RightDistance().Value - anchorSize->X;
    }
    if (this->anchorData->get_TopDistance().HasValue)
    {
localPosition.Y = this->anchorData->get_TopDistance().Value;
    }
else     if (this->anchorData->get_BottomDistance().HasValue)
    {
localPosition.Y = anchorBounds->Y - this->anchorData->get_BottomDistance().Value - anchorSize->Y;
    }
Parent->set_LocalPosition(localPosition);
}

void AnchorComponent::SetAnchorDistances(Nullable<float> left, Nullable<float> right, Nullable<float> top, Nullable<float> bottom)
{
    if (this->anchorData == nullptr)
    {
this->anchorData = new ::AnchorData();
    }
this->anchorData->set_LeftDistance(left);
this->anchorData->set_RightDistance(right);
this->anchorData->set_TopDistance(top);
this->anchorData->set_BottomDistance(bottom);
    if (!left.HasValue && !right.HasValue && !top.HasValue && !bottom.HasValue)
    {
this->DisableAnchoring();
return;    }
this->RefreshSubscriptions();
this->RefreshAnchoring();
}

::Entity* AnchorComponent::get_Parent()
{
return this->Component::get_Parent();
}

void AnchorComponent::set_Parent(::Entity* value)
{
this->Component::set_Parent(value);
}

void AnchorComponent::AttachToWindowResize()
{
    if (this->IsSubscribedToWindowResize)
    {
return;    }
Core::get_Instance()->get_RenderManager3D()->WindowResized += &AnchorComponent::HandleWindowResized;
this->IsSubscribedToWindowResize = true;
}

void AnchorComponent::DetachFromBoundsProvider()
{
    if (this->anchorBoundsProvider != nullptr)
    {
this->anchorBoundsProvider->AnchorBoundsChanged -= &AnchorComponent::HandleAnchorBoundsChanged;
this->anchorBoundsProvider = nullptr;
    }
}

void AnchorComponent::DetachFromWindowResize()
{
    if (!this->IsSubscribedToWindowResize)
    {
return;    }
Core::get_Instance()->get_RenderManager3D()->WindowResized -= &AnchorComponent::HandleWindowResized;
this->IsSubscribedToWindowResize = false;
}

int32_t AnchorComponent::GetAnchorArea(int2* size)
{
    if (size->X < 0 || size->Y < 0)
    {
return -1;    }
return size->X * size->Y;}

int2* AnchorComponent::GetAnchorBounds()
{
    if (this->anchorBoundsProvider != nullptr)
    {
return this->anchorBoundsProvider->get_AnchorBounds();    }
return Core::get_Instance()->get_RenderManager3D()->get_MainWindowSize();}

int2* AnchorComponent::GetAnchorSize()
{
    if (Parent == nullptr)
    {
return new int2(0, 0);    }
::IAnchorSizeProvider *bestProvider = nullptr;
int32_t bestArea = -1;
    IAnchorSizeProvider* parentProvider = he_cpp_try_cast<IAnchorSizeProvider>(Parent);
    if (parentProvider != nullptr)
    {
bestProvider = parentProvider;
bestArea = this->GetAnchorArea(parentProvider->get_AnchorSize());
    }
for (int32_t i = 0; i < Parent->get_Components()->Count(); i++) {
    IAnchorSizeProvider* sizeProvider = he_cpp_try_cast<IAnchorSizeProvider>((*Parent->get_Components())[i]);
    if (sizeProvider != nullptr)
    {
const int32_t area = this->GetAnchorArea(sizeProvider->get_AnchorSize());
    if (area > bestArea)
    {
bestProvider = sizeProvider;
bestArea = area;
    }
    }
}
    if (bestProvider == nullptr)
    {
return new int2(0, 0);    }
return bestProvider->get_AnchorSize();}

void AnchorComponent::HandleAnchorBoundsChanged()
{
this->RefreshAnchoring();
}

void AnchorComponent::HandleWindowResized(intptr_t handle, int32_t newWidth, int32_t newHeight)
{
this->RefreshAnchoring();
}

void AnchorComponent::RefreshSubscriptions()
{
::IAnchorBoundsProvider *newProvider = this->ResolveAnchorBoundsProvider();
    if (!(this->anchorBoundsProvider == newProvider))
    {
this->DetachFromBoundsProvider();
this->anchorBoundsProvider = newProvider;
    if (this->anchorBoundsProvider != nullptr)
    {
this->anchorBoundsProvider->AnchorBoundsChanged += &AnchorComponent::HandleAnchorBoundsChanged;
    }
    }
    if (this->anchorBoundsProvider == nullptr)
    {
this->AttachToWindowResize();
    }
else {
this->DetachFromWindowResize();
}
}

::IAnchorBoundsProvider* AnchorComponent::ResolveAnchorBoundsProvider()
{
::Entity *current = Parent;
while (current != nullptr) {
    IAnchorBoundsProvider* provider = he_cpp_try_cast<IAnchorBoundsProvider>(current);
    if (provider != nullptr)
    {
return provider;    }
current = current->get_Parent();
}
return nullptr;}

