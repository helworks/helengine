#ifdef DrawText
#undef DrawText
#endif
#include "CheckBoxComponent.hpp"
#include "Component.hpp"
#include "RenderOrder2D.hpp"
#include "Entity.hpp"
#include "RoundedRectComponent.hpp"
#include "InteractableComponent.hpp"
#include "PointerCursorKind.hpp"
#include "TextComponent.hpp"
#include "ThemeManager.hpp"
#include "PointerInteraction.hpp"
#include "float3.hpp"
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
#include "system/binary_primitives.hpp"
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

bool CheckBoxComponent::get_IsChecked()
{
return this->IsCheckedValue;}

void CheckBoxComponent::set_IsChecked(bool value)
{
this->SetCheckedState(value, false);
}

int2* CheckBoxComponent::get_Size()
{
return this->SizeValue;}

void CheckBoxComponent::set_Size(int2* value)
{
this->SizeValue = value;
    if (this->Background != nullptr)
    {
this->Background->set_Size(value);
    }
    if (this->Interactable != nullptr)
    {
this->Interactable->set_Size(value);
    }
this->UpdateCheckMarkLayout();
}

CheckBoxComponent::CheckBoxComponent(int2* size, ::FontAsset* font, bool isChecked) : CheckedChanged(), Background(), BackgroundRenderOrder(), CheckMark(), CheckMarkEntity(), CheckMarkRenderOrder(), Font(), HasRenderOrderOverrides(), Interactable(), IsCheckedValue(), IsHovering(), IsPressed(), SizeValue()
{
    if (font == nullptr)
    {
throw new ArgumentNullException("font");
    }
this->SizeValue = size;
this->Font = font;
this->IsCheckedValue = isChecked;
}

void CheckBoxComponent::ComponentAdded(::Entity* entity)
{
Component::ComponentAdded(entity);
uint8_t backgroundOrder = RenderOrder2D::PanelSurface;
uint8_t textOrder = RenderOrder2D::PanelForeground;
    if (this->HasRenderOrderOverrides)
    {
backgroundOrder = this->BackgroundRenderOrder;
textOrder = this->CheckMarkRenderOrder;
    }
this->Background = new ::RoundedRectComponent();
this->Background->set_Size(this->SizeValue);
this->Background->set_Radius(static_cast<float>((Math::Min(this->SizeValue->X, this->SizeValue->Y) * 0.15)));
this->Background->set_BorderThickness(2.0f);
this->Background->set_RenderOrder2D(backgroundOrder);
entity->AddComponent(this->Background);
this->Interactable = new ::InteractableComponent();
this->Interactable->set_Size(this->SizeValue);
this->Interactable->set_HoverCursor(PointerCursorKind::Hand);
this->Interactable->CursorEvent += &CheckBoxComponent::HandleCursorEvent;
entity->AddComponent(this->Interactable);
this->CheckMarkEntity = new ::Entity();
this->CheckMarkEntity->set_LayerMask(entity->get_LayerMask());
this->CheckMarkEntity->set_Enabled(true);
this->CheckMarkEntity->InitComponents();
    if (entity->get_Children() == nullptr)
    {
entity->InitChildren();
    }
entity->AddChild(this->CheckMarkEntity);
this->CheckMark = new ::TextComponent();
this->CheckMark->set_Font(this->Font);
this->CheckMark->set_Color(ThemeManager::get_Colors()->get_InputForegroundPrimary());
this->CheckMark->set_RenderOrder2D(textOrder);
this->CheckMarkEntity->AddComponent(this->CheckMark);
this->UpdateCheckMarkLayout();
this->UpdateVisualState();
}

void CheckBoxComponent::ComponentRemoved(::Entity* entity)
{
Component::ComponentRemoved(entity);
this->IsHovering = false;
this->IsPressed = false;
}

void CheckBoxComponent::ParentEnabledChange(bool newEnabled)
{
Component::ParentEnabledChange(newEnabled);
    if (!newEnabled)
    {
this->IsHovering = false;
this->IsPressed = false;
this->UpdateVisualState();
    }
    if (this->CheckMarkEntity != nullptr)
    {
this->CheckMarkEntity->set_Enabled(newEnabled);
    }
}

void CheckBoxComponent::SetRenderOrders(uint8_t backgroundOrder, uint8_t checkMarkOrder)
{
this->HasRenderOrderOverrides = true;
this->BackgroundRenderOrder = backgroundOrder;
this->CheckMarkRenderOrder = checkMarkOrder;
    if (this->Background != nullptr)
    {
this->Background->set_RenderOrder2D(backgroundOrder);
    }
    if (this->CheckMark != nullptr)
    {
this->CheckMark->set_RenderOrder2D(checkMarkOrder);
    }
}

::Entity* CheckBoxComponent::get_Parent()
{
return this->Component::get_Parent();
}

void CheckBoxComponent::set_Parent(::Entity* value)
{
this->Component::set_Parent(value);
}

void CheckBoxComponent::HandleCursorEvent(int2* relPos, int2* delta, ::PointerInteraction state)
{
switch (state) {
case PointerInteraction::Hover: {
    if (!this->IsHovering)
    {
this->IsHovering = true;
this->UpdateVisualState();
    }
break;
}
case PointerInteraction::Press: {
this->IsPressed = true;
this->UpdateVisualState();
break;
}
case PointerInteraction::Release: {
    if (this->IsPressed && this->IsHovering)
    {
this->SetCheckedState(!this->IsCheckedValue, true);
    }
this->IsPressed = false;
this->UpdateVisualState();
break;
}
case PointerInteraction::Leave: {
    if (this->IsHovering || this->IsPressed)
    {
this->IsHovering = false;
this->IsPressed = false;
this->UpdateVisualState();
    }
break;
}
case PointerInteraction::None: {
break;
}
}

}

void CheckBoxComponent::SetCheckedState(bool isChecked, bool raiseEvent)
{
    if (this->IsCheckedValue == isChecked)
    {
this->UpdateVisualState();
return;    }
this->IsCheckedValue = isChecked;
this->UpdateVisualState();
    if (raiseEvent && true)
    {
this->CheckedChanged.Invoke(this, IsCheckedValue);
    }
}

void CheckBoxComponent::UpdateCheckMarkLayout()
{
    if (this->CheckMarkEntity == nullptr || this->CheckMark == nullptr)
    {
return;    }
this->CheckMarkEntity->set_Position(::float3(0.0f, 0.0f, 0.1f));
this->CheckMark->set_Size(([&]() {
auto __ctor_arg_0000017B = 1;
auto __ctor_arg_0000017C = Math::Max(1, static_cast<int32_t>(Math::Ceiling(Math::Max(static_cast<double>(this->Font->get_LineHeight()), 1.0))));
return new int2(__ctor_arg_0000017B, __ctor_arg_0000017C);
})());
}

void CheckBoxComponent::UpdateVisualState()
{
    if (this->Background == nullptr)
    {
return;    }
this->Background->set_BorderColor(this->IsCheckedValue || this->IsHovering ? ThemeManager::get_Colors()->get_AccentPrimary() : ThemeManager::get_Colors()->get_AccentTertiary());
    if (this->IsPressed)
    {
this->Background->set_FillColor(ThemeManager::get_Colors()->get_AccentTertiary());
    }
else     if (this->IsCheckedValue)
    {
this->Background->set_FillColor(ThemeManager::get_Colors()->get_AccentSecondary());
    }
else     if (this->IsHovering)
    {
this->Background->set_FillColor(ThemeManager::get_Colors()->get_SurfaceInput());
    }
else {
this->Background->set_FillColor(ThemeManager::get_Colors()->get_SurfaceInput());
}
    if (this->CheckMark != nullptr)
    {
this->CheckMark->set_Text(String::Empty);
    }
}

