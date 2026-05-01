#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class Component;
class IFocusTarget;
class RenderOrder2D;
class Entity;
class float3;
class byte4;
class FontTightMetrics;
class FontAsset;
class IFocusGroup;
class int2;
class InteractableComponent;
class RoundedRectComponent;
class TextComponent;

#include "Component.hpp"
#include "IFocusTarget.hpp"
#include "runtime/native_exceptions.hpp"
#include "Component.hpp"
#include "RenderOrder2D.hpp"
#include "RenderOrder2D.hpp"
#include "Entity.hpp"
#include "float3.hpp"
#include "byte4.hpp"
#include "FontTightMetrics.hpp"
#include "FontAsset.hpp"
#include "system/math.hpp"
#include "system/math.hpp"
#include "byte4.hpp"
#include "IFocusGroup.hpp"
#include "runtime/native_event.hpp"
#include "int2.hpp"
#include "PointerCursorKind.hpp"
#include "FontAsset.hpp"
#include "InteractableComponent.hpp"
#include "system/action.hpp"
#include "RoundedRectComponent.hpp"
#include "runtime/native_string.hpp"
#include "TextComponent.hpp"
#include "Entity.hpp"
#include "Keys.hpp"
#include "PointerInteraction.hpp"

class ButtonComponent : public Component, public IFocusTarget
{
public:
    bool get_CanReceiveFocus();

    ::IFocusGroup* FocusGroup;

    ::IFocusGroup* get_FocusGroup();
    void set_FocusGroup(::IFocusGroup* value);

    ::Event Hovered;

    bool IsDefaultTarget;

    bool get_IsDefaultTarget();
    void set_IsDefaultTarget(bool value);

    bool IsKeyboardFocused;

    bool get_IsKeyboardFocused();
    void set_IsKeyboardFocused(bool value);

    ::int2 get_Size();

    int32_t TabIndex;

    int32_t get_TabIndex();
    void set_TabIndex(int32_t value);

    void ActivateFromKey(::Keys key);

    ButtonComponent(std::string text, ::int2 size, ::FontAsset* font, Action<>* onClickAction, float borderThickness);

    bool CanActivateWithKey(::Keys key);

    void ComponentAdded(::Entity* entity);

    void ComponentRemoved(::Entity* entity);

    bool ContainsScreenPoint(::int2 point);

    void ParentEnabledChange(bool newEnabled);

    void SetHoverCursor(::PointerCursorKind cursor);

    void SetRenderOrders(uint8_t backgroundOrder, uint8_t textOrder);

    void SetSize(::int2 newSize);

    void SetTargetFocused(bool isFocused);

    void SetTextColor(::byte4 color);

    void UseHoverOnlyBackground();

    void UseSquareCorners();

    ::Entity* get_Parent();

    void set_Parent(::Entity* value);
private:
    static ::byte4 TransparentBackgroundColor;

    uint8_t BackgroundRenderOrder;

    ::byte4 ButtonTextColor;

    float CornerRadius;

    bool HasRenderOrderOverrides;

    ::PointerCursorKind HoverCursorKind;

    uint8_t TextRenderOrder;

    bool UsesHoverOnlyBackground;

    bool UsesSquareCorners;

    float borderThickness;

    ::FontAsset* font;

    ::InteractableComponent* interactableComponent;

    bool isHovering;

    bool isPressed;

    Action<>* onClickAction;

    ::RoundedRectComponent* roundedRect;

    ::int2 size;

    std::string text;

    ::TextComponent* textComponent;

    ::Entity* textEntity;

    void ApplyTextLayout();

    ::byte4 GetIdleBorderColor();

    ::byte4 GetIdleFillColor();

    void OnCursorEvent(::int2 relPos, ::int2 delta, ::PointerInteraction state);

    void RaiseHovered();

    void UpdateButtonColor();
};
