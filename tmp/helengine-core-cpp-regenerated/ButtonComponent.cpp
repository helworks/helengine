#ifdef DrawText
#undef DrawText
#endif
#include "ButtonComponent.hpp"
#include "runtime/native_exceptions.hpp"
#include "Component.hpp"
#include "RenderOrder2D.hpp"
#include "Entity.hpp"
#include "float3.hpp"
#include "byte4.hpp"
#include "FontTightMetrics.hpp"
#include "FontAsset.hpp"
#include "system/math.hpp"
#include "ThemeManager.hpp"
#include "Keys.hpp"
#include "RoundedRectComponent.hpp"
#include "InteractableComponent.hpp"
#include "TextComponent.hpp"
#include "int2.hpp"
#include "PointerInteraction.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
#include "runtime/native_cast.hpp"
#include "runtime/native_datetime.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_disposable.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_equatable.hpp"
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
#include "system/io/directory.hpp"
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

bool ButtonComponent::get_CanReceiveFocus()
{
return Parent != nullptr && Parent->get_IsHierarchyEnabled() && this->interactableComponent != nullptr;
}

::IFocusGroup* ButtonComponent::get_FocusGroup()
{
return this->FocusGroup;
}

void ButtonComponent::set_FocusGroup(::IFocusGroup* value)
{
this->FocusGroup = value;
}

bool ButtonComponent::get_IsDefaultTarget()
{
return this->IsDefaultTarget;
}

void ButtonComponent::set_IsDefaultTarget(bool value)
{
this->IsDefaultTarget = value;
}

bool ButtonComponent::get_IsKeyboardFocused()
{
return this->IsKeyboardFocused;
}

void ButtonComponent::set_IsKeyboardFocused(bool value)
{
this->IsKeyboardFocused = value;
}

::int2 ButtonComponent::get_Size()
{
return this->size;
}

int32_t ButtonComponent::get_TabIndex()
{
return this->TabIndex;
}

void ButtonComponent::set_TabIndex(int32_t value)
{
this->TabIndex = value;
}

void ButtonComponent::ActivateFromKey(::Keys key)
{
    if (!this->CanActivateWithKey(key))
    {
return;    }
if (this->onClickAction != nullptr)
{
(*this->onClickAction)();
}
}

ButtonComponent::ButtonComponent(std::string text, ::int2 size, ::FontAsset* font, Action<>* onClickAction, float borderThickness) : FocusGroup(), Hovered(), IsDefaultTarget(), IsKeyboardFocused(), TabIndex(0), BackgroundRenderOrder(), ButtonTextColor(), CornerRadius(), HasRenderOrderOverrides(), HoverCursorKind(), TextRenderOrder(), UsesHoverOnlyBackground(), UsesSquareCorners(), borderThickness(), font(), interactableComponent(), isHovering(), isPressed(), onClickAction(), roundedRect(), size(), text(), textComponent(), textEntity()
{
this->text = text;
this->size = size;
this->font = font;
this->onClickAction = onClickAction;
this->borderThickness = borderThickness;
this->ButtonTextColor = ThemeManager::get_Colors()->get_TextOnAccent();
this->CornerRadius = MathF::Min(size.X, size.Y) * 0.15f;
}

bool ButtonComponent::CanActivateWithKey(::Keys key)
{
return key == Keys::Enter || key == Keys::Space;}

void ButtonComponent::ComponentAdded(::Entity* entity)
{
Component::ComponentAdded(entity);
    if (!entity->get_Enabled())
    {
return;    }
uint8_t backgroundOrder = RenderOrder2D::PanelSurface;
uint8_t textOrder = RenderOrder2D::PanelForeground;
    if (this->HasRenderOrderOverrides)
    {
backgroundOrder = this->BackgroundRenderOrder;
textOrder = this->TextRenderOrder;
    }
this->roundedRect = new ::RoundedRectComponent();
this->roundedRect->set_Size(this->size);
this->roundedRect->set_Radius(this->CornerRadius);
this->roundedRect->set_BorderThickness(this->borderThickness);
this->roundedRect->set_FillColor(ThemeManager::get_Colors()->get_AccentSecondary());
this->roundedRect->set_BorderColor(ThemeManager::get_Colors()->get_AccentTertiary());
this->roundedRect->set_RenderOrder2D(backgroundOrder);
entity->AddComponent(this->roundedRect);
this->UpdateButtonColor();
this->interactableComponent = new ::InteractableComponent();
this->interactableComponent->set_Size(this->size);
this->interactableComponent->set_HoverCursor(this->HoverCursorKind);
this->interactableComponent->CursorEvent += &ButtonComponent::OnCursorEvent;
entity->AddComponent(this->interactableComponent);
this->textEntity = new ::Entity();
this->textEntity->set_LayerMask(entity->get_LayerMask());
this->textEntity->set_Enabled(true);
this->textEntity->InitComponents();
entity->InitChildren();
entity->AddChild(this->textEntity);
this->textComponent = new ::TextComponent();
this->textComponent->set_Text(this->text);
this->textComponent->set_Font(this->font);
this->textComponent->set_Color(this->ButtonTextColor);
this->textComponent->set_Size(::int2(1, 1));
this->textComponent->set_RenderOrder2D(textOrder);
this->textEntity->AddComponent(this->textComponent);
this->ApplyTextLayout();
}

void ButtonComponent::ComponentRemoved(::Entity* entity)
{
Component::ComponentRemoved(entity);
this->isHovering = false;
this->isPressed = false;
this->SetTargetFocused(false);
}

bool ButtonComponent::ContainsScreenPoint(::int2 point)
{
    if (Parent == nullptr)
    {
return false;    }
::float3 position = Parent->get_Position();
return point.X >= position.X && point.X < position.X + this->size.X && point.Y >= position.Y && point.Y < position.Y + this->size.Y;}

void ButtonComponent::ParentEnabledChange(bool newEnabled)
{
Component::ParentEnabledChange(newEnabled);
    if (!newEnabled)
    {
this->isHovering = false;
this->isPressed = false;
this->SetTargetFocused(false);
    }
    if (this->textEntity != nullptr)
    {
this->textEntity->set_Enabled(newEnabled);
    }
}

void ButtonComponent::SetHoverCursor(::PointerCursorKind cursor)
{
this->HoverCursorKind = cursor;
    if (this->interactableComponent != nullptr)
    {
this->interactableComponent->set_HoverCursor(cursor);
    }
}

void ButtonComponent::SetRenderOrders(uint8_t backgroundOrder, uint8_t textOrder)
{
this->HasRenderOrderOverrides = true;
this->BackgroundRenderOrder = backgroundOrder;
this->TextRenderOrder = textOrder;
    if (this->roundedRect != nullptr)
    {
this->roundedRect->set_RenderOrder2D(backgroundOrder);
    }
    if (this->textComponent != nullptr)
    {
this->textComponent->set_RenderOrder2D(textOrder);
    }
}

void ButtonComponent::SetSize(::int2 newSize)
{
    if (newSize.X < 1 || newSize.Y < 1)
    {
throw ([&]() {
auto __ctor_arg_0bc1a3b1 = "newSize";
auto __ctor_arg_14237ae8 = "Button size must be positive.";
return new ArgumentOutOfRangeException(__ctor_arg_0bc1a3b1, __ctor_arg_14237ae8);
})();
    }
this->size = newSize;
    if (!this->UsesSquareCorners)
    {
this->CornerRadius = MathF::Min(this->size.X, this->size.Y) * 0.15f;
    }
    if (this->roundedRect != nullptr)
    {
this->roundedRect->set_Size(this->size);
this->roundedRect->set_Radius(this->CornerRadius);
    }
    if (this->interactableComponent != nullptr)
    {
this->interactableComponent->set_Size(this->size);
this->interactableComponent->set_HoverCursor(this->HoverCursorKind);
    }
    if (this->textEntity == nullptr || this->textComponent == nullptr)
    {
return;    }
this->ApplyTextLayout();
}

void ButtonComponent::SetTargetFocused(bool isFocused)
{
    if (this->IsKeyboardFocused == isFocused)
    {
this->UpdateButtonColor();
return;    }
this->set_IsKeyboardFocused(isFocused);
this->UpdateButtonColor();
}

void ButtonComponent::SetTextColor(::byte4 color)
{
this->ButtonTextColor = color;
    if (this->textComponent != nullptr)
    {
this->textComponent->set_Color(color);
    }
}

void ButtonComponent::UseHoverOnlyBackground()
{
this->UsesHoverOnlyBackground = true;
this->UpdateButtonColor();
}

void ButtonComponent::UseSquareCorners()
{
this->UsesSquareCorners = true;
this->CornerRadius = 0.0f;
    if (this->roundedRect != nullptr)
    {
this->roundedRect->set_Radius(this->CornerRadius);
    }
}

::Entity* ButtonComponent::get_Parent()
{
return this->Component::get_Parent();
}

void ButtonComponent::set_Parent(::Entity* value)
{
this->Component::set_Parent(value);
}

::byte4 ButtonComponent::TransparentBackgroundColor = ::byte4(255, 255, 255, 0);

void ButtonComponent::ApplyTextLayout()
{
    if (this->textEntity == nullptr || this->textComponent == nullptr)
    {
return;    }
::FontTightMetrics tight = this->font->MeasureTight(this->text);
const float lineHeight = MathF::Max(this->font->get_LineHeight(), 1.0f);
float px = (this->size.X - tight.Width) / 2.0f;
float py = (this->size.Y - lineHeight) / 2.0f;
px = MathF::Round(px);
py = MathF::Round(py);
this->textEntity->set_Position(::float3(px, py, 0.1f));
this->textComponent->set_Size(([&]() {
auto __ctor_arg_bf9a9621 = static_cast<int32_t>(Math::Ceiling(tight.Width));
auto __ctor_arg_43ccd0f7 = static_cast<int32_t>(Math::Ceiling(lineHeight));
return ::int2(__ctor_arg_bf9a9621, __ctor_arg_43ccd0f7);
})());
}

::byte4 ButtonComponent::GetIdleBorderColor()
{
    if (this->UsesHoverOnlyBackground)
    {
return TransparentBackgroundColor;    }
return ThemeManager::get_Colors()->get_AccentTertiary();}

::byte4 ButtonComponent::GetIdleFillColor()
{
    if (this->UsesHoverOnlyBackground)
    {
return TransparentBackgroundColor;    }
return ThemeManager::get_Colors()->get_AccentSecondary();}

void ButtonComponent::OnCursorEvent(::int2 relPos, ::int2 delta, ::PointerInteraction state)
{
switch (state) {
case PointerInteraction::Hover: {
    if (!this->isHovering)
    {
this->isHovering = true;
this->UpdateButtonColor();
this->RaiseHovered();
    }
break;
}
case PointerInteraction::Press: {
this->isPressed = true;
this->UpdateButtonColor();
break;
}
case PointerInteraction::Release: {
    if (this->isPressed && this->isHovering)
    {
if (this->onClickAction != nullptr)
{
(*this->onClickAction)();
}
    }
this->isPressed = false;
this->UpdateButtonColor();
break;
}
case PointerInteraction::Leave: {
    if (this->isHovering || this->isPressed)
    {
this->isHovering = false;
this->isPressed = false;
this->UpdateButtonColor();
    }
break;
}
case PointerInteraction::None: {
break;
}
}

}

void ButtonComponent::RaiseHovered()
{
    if (true)
    {
this->Hovered.Invoke();
    }
}

void ButtonComponent::UpdateButtonColor()
{
    if (this->roundedRect == nullptr)
    {
return;    }
this->roundedRect->set_BorderColor(this->IsKeyboardFocused ? ThemeManager::get_Colors()->get_AccentPrimary() : GetIdleBorderColor());
    if (this->isPressed)
    {
this->roundedRect->set_FillColor(ThemeManager::get_Colors()->get_AccentTertiary());
    }
else     if (this->isHovering)
    {
this->roundedRect->set_FillColor(ThemeManager::get_Colors()->get_AccentPrimary());
    }
else {
this->roundedRect->set_FillColor(this->GetIdleFillColor());
}
}

